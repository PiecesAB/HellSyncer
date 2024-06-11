using Blastula.Sounds;
using Blastula.VirtualVariables;
using Godot;
using HellSyncer.Midi;
using System.Collections.Generic;
using System;

namespace HellSyncer
{
    /// <summary>
    /// Handles the game's background music, with special syncing logic.
    /// </summary>
    /// <remarks>
    /// This is also the singleton MusicManager.
    /// </remarks>
    public partial class SyncedMusicManager : Blastula.Sounds.MusicManager
    {
        /// <summary>
        /// If the audible music gets this far from the MIDI (in seconds), then we will force the music to lag or skip.
        /// The reason this happens is because of unpredictable lag in the game,
        /// but the MIDI needs to run deterministically (for replays).
        /// It's not pretty, but it keeps the sync.
        /// </summary>
        public const float TOLERANCE = 0.03f;
        /// <summary>
        /// Occurs early in the frame to allow other items to respond to the changes
        /// </summary>
        public const int PROCESS_PRIORITY = -1000000;
        private float estimatedLatency;
        private ulong startFrame;
        private class TempoRegion
        {
            public TempoEvent tempoEvent;
            public ulong startTick;
            public ulong endTick;

            public ulong GetDurationInTicks() { return endTick - startTick; }
            public float GetDurationInSeconds()
            {
                float ticksPerQN = midi.ticksPerQN;
                float secondsPerQN = 60f / tempoEvent.GetBPM();
                float secondsPerTick = secondsPerQN / ticksPerQN;
                return (endTick - startTick) * secondsPerTick;
            }
        }
        private List<TempoRegion> tempoMap = new List<TempoRegion>();
        private List<TimeSignatureEvent> timeSignatureMap = new List<TimeSignatureEvent>();
        private List<KeySignatureEvent> keySignatureMap = new List<KeySignatureEvent>();
        private List<BaseTextEvent> textMap = new List<BaseTextEvent>();
        private List<int> trackEventIndices = new List<int>();
        public delegate void OnNote(FullNoteInfo info);
        public delegate void OnText(BaseTextEvent text);
        private static Dictionary<int, OnNote> noteAlerts = new Dictionary<int, OnNote>();
        private static OnText textAlert;

        private ulong currentMidiTick;
        private float currentBeat = 0;
        private ulong currentMeasure = 0;
        /// <summary>
        /// Quarter notes per minute
        /// </summary>
        private float currentTempo = 120;
        private float startTimeOfTempoRegion = 0;
        private ulong startTickOfTempoRegion = 0;
        private Vector2I currentTimeSignature = new Vector2I(4, 4);
        private ulong startMeasureOfMeterRegion = 0;
        private float startTimeOfMeterRegion = 0;
        private ulong startTickOfMeterRegion = 0;
        private KeySignatureEvent currentKeySignature = new KeySignatureEvent(0, 0, false);
        private (ulong, ulong) loopRegionAsTicks = (0, 0);

        public static SyncedMusicManager mainSynced;
        public static ParsedMidi midi;

        public new static void PlayImmediate(string nodeName)
        {
            if (mainSynced == null) { return; }
            if (!main.musicsByNodeName.ContainsKey(nodeName)) { return; }
            Music nextMusic = main.musicsByNodeName[nodeName];
            if (nextMusic is not SyncedMusic) { return; }
            SyncedMusic sm = (SyncedMusic)nextMusic;
            MusicManager.PlayImmediate(nodeName);
            mainSynced.startFrame = Blastula.FrameCounter.stageFrame;
            if ((midi = sm.midi) == null)
            {
                midi = new ParsedMidi();
                midi.GenerateForBeat(sm.generatedBpm, sm.generatedTimeSignature);
            }

            mainSynced.InitializeMidiStuff();

            if (sm.loopRegion == Vector2.Zero) { mainSynced.loopRegionAsTicks = (0, 0); }
            else
            {
                mainSynced.loopRegionAsTicks =
                    (mainSynced.TimeToTick(sm.loopRegion.X), mainSynced.TimeToTick(sm.loopRegion.Y));
            }

            mainSynced.MidiSeek(0);
            mainSynced.ProcessMidi(0);
        }

        private void InitializeMidiStuff()
        {
            PopulateMaps();
            foreach (Track track in midi.tracks)
            {
                trackEventIndices.Add(0);
            }
        }

        private float GetQuarterNoteDuration()
        {
            return 60f / currentTempo;
        }

        /// <summary>
        /// Converts MIDI tick count to track time in seconds.
        /// </summary>
        public float TickToTime(ulong tick)
        {
            // 1. Find which TempoRegion contains tick
            TempoRegion container = null;
            float currentStartTime = 0;
            float currentEndTime = 0;
            for (int i = 0; i < tempoMap.Count; ++i)
            {
                TempoRegion region = tempoMap[i];
                currentEndTime = currentStartTime + region.GetDurationInSeconds();
                if (region.startTick <= tick && tick < region.endTick)
                {
                    container = region;
                    break;
                }
                currentStartTime = currentEndTime;
            }
            if (container == null) { return 0; }
            // 2. Get the progress in the TempoRegion, convert to time progress
            float progress = 0;
            if (container.startTick < container.endTick)
            {
                progress = (float)((tick - container.startTick) / (double)(container.endTick - container.startTick));
            }
            return Mathf.Lerp(currentStartTime, currentEndTime, progress);
        }

        /// <summary>
        /// Converts track time in seconds to MIDI tick count.
        /// </summary>
        /// <remarks>
        /// Important to seek somewhere in a MIDI using only a number of seconds.
        /// </remarks>
        public ulong TimeToTick(float time)
        {
            // 1. Find which TempoRegion contains time
            TempoRegion container = null;
            float currentStartTime = 0;
            float currentEndTime = 0;
            for (int i = 0; i < tempoMap.Count; ++i)
            {
                TempoRegion region = tempoMap[i];
                currentEndTime = currentStartTime + region.GetDurationInSeconds();
                if (currentStartTime <= time && time < currentEndTime)
                {
                    container = region;
                    break;
                }
                currentStartTime = currentEndTime;
            }
            if (container == null) { return 0; }
            // 2. Get the progress in the TempoRegion, convert to tick progress
            float progress = 0;
            if (currentStartTime < currentEndTime)
            {
                progress = (time - currentStartTime) / (currentEndTime - currentStartTime);
            }
            return (ulong)System.Math.Round(
                ((1f - progress) * (double)container.startTick)
                + (progress * (double)container.endTick)
            );
        }

        private void MidiSeek(float time)
        {
            ulong tick = TimeToTick(time);
            currentMidiTick = tick;
            currentBeat = 0;
            currentMeasure = 0;
            currentTempo = 120;
            startTimeOfTempoRegion = 0;
            startTickOfTempoRegion = 0;
            currentTimeSignature = new Vector2I(4, 4);
            startMeasureOfMeterRegion = 0;
            startTimeOfMeterRegion = 0;
            startTickOfMeterRegion = 0;

            trackEventIndices[0] = 0;
            if (tick > 0) { ProcessTrack0(tick - 1, false); }
            for (int i = 1; i < midi.tracks.Length; ++i)
            {
                trackEventIndices[i] = midi.tracks[i].GetFullNoteIndexAtTick(tick);
            }
        }

        public static void AddInstrumentListener(Instrument ins)
        {
            foreach (int trackId in ins.trackIds)
            {
                if (!noteAlerts.ContainsKey(trackId))
                {
                    noteAlerts[trackId] = new OnNote(ins.SelfOnNote);
                }
                else
                {
                    noteAlerts[trackId] += ins.SelfOnNote;
                }
            }

        }

        public static void RemoveInstrumentListener(Instrument ins)
        {
            foreach (int trackID in ins.trackIds)
            {
                if (noteAlerts.ContainsKey(trackID))
                {
                    noteAlerts[trackID] -= ins.SelfOnNote;
                }
            }
        }

        public static void AddTextListener(MidiTextReader con)
        {
            textAlert += con.SelfOnText;
        }

        public static void RemoveTextListener(MidiTextReader con)
        {
            textAlert -= con.SelfOnText;
        }


        private ulong SolveTargetTick(float goalPlaybackPos)
        {
            float timeSinceStartOfRegion = goalPlaybackPos - startTimeOfTempoRegion;
            float beatsSinceStartOfRegion = timeSinceStartOfRegion / GetQuarterNoteDuration();
            ulong ticksSinceStartOfRegion = (ulong)Math.Round(beatsSinceStartOfRegion * midi.ticksPerQN);
            return startTickOfTempoRegion + ticksSinceStartOfRegion;
        }

        private void ProcessTrack0(ulong targetTick, bool sendTextEvents = true)
        {
            // Scan through raw Track 0 events until arriving at the target tick.
            Track ctrack = midi.tracks[0];
            int eventIndex = trackEventIndices[0];
            if (eventIndex < 0) { eventIndex = trackEventIndices[0] = 0; }
            while (eventIndex < ctrack.trackEvents.Length)
            {
                TrackEvent @event = ctrack.trackEvents[eventIndex];
                if (@event.tick > targetTick) { break; }
                switch (@event.displayID)
                {
                    case EventID.Tempo:
                        {
                            ulong ticksPassedSinceLastRegion = @event.tick - startTickOfTempoRegion;
                            float calculatedDurationOfLastRegion = (ticksPassedSinceLastRegion / (float)midi.ticksPerQN) * GetQuarterNoteDuration();
                            startTickOfTempoRegion = @event.tick;
                            startTimeOfTempoRegion += calculatedDurationOfLastRegion;
                            currentTempo = ((TempoEvent)@event).GetBPM();
                            break;
                        }
                    case EventID.TimeSignature:
                        {
                            ulong ticksPassedSinceLastRegion = @event.tick - startTickOfMeterRegion;
                            float calculatedDurationOfLastRegion = (ticksPassedSinceLastRegion / (float)midi.ticksPerQN) * GetQuarterNoteDuration();
                            float ticksPerDenom = midi.ticksPerQN * (4f / currentTimeSignature.Y);
                            float beatCountOfLastRegion = ticksPassedSinceLastRegion / ticksPerDenom;
                            ulong measureCountOfLastRegion = (ulong)Mathf.Ceil(beatCountOfLastRegion / currentTimeSignature.X - 0.002f);
                            startTickOfMeterRegion = @event.tick;
                            startTimeOfMeterRegion += calculatedDurationOfLastRegion;
                            startMeasureOfMeterRegion += measureCountOfLastRegion;
                            currentTimeSignature = new Vector2I(
                                ((TimeSignatureEvent)@event).numerator,
                                ((TimeSignatureEvent)@event).denominator
                            );
                            break;
                        }
                    case EventID.KeySignature:
                        {
                            currentKeySignature = (KeySignatureEvent)@event;
                            break;
                        }
                }
                if (sendTextEvents && @event is BaseTextEvent)
                {
                    textAlert?.Invoke((BaseTextEvent)@event);
                }
                trackEventIndices[0] = ++eventIndex;
            }
        }

        private void ProcessMidi(ulong targetTick)
        {
            // In Track 0 we expect all meter, tempo, and text information.
            // In all other tracks we expect instruments, and use only the parsed full note data.
            ProcessTrack0(targetTick);
            // Scan through full notes of other tracks until arriving at the target tick.
            for (int trackIndex = 1; trackIndex < midi.tracks.Length; ++trackIndex)
            {
                Track itrack = midi.tracks[trackIndex];
                int noteIndex = trackEventIndices[trackIndex];
                if (noteIndex < 0) { noteIndex = trackEventIndices[trackIndex] = 0; }
                while (noteIndex < itrack.fullNoteInfos.Length)
                {
                    FullNoteInfo note = itrack.fullNoteInfos[noteIndex];
                    if (note.tick > targetTick) { break; }
                    if (noteAlerts.ContainsKey(trackIndex))
                    {
                        noteAlerts[trackIndex].Invoke(note);
                    }
                    trackEventIndices[trackIndex] = ++noteIndex;
                }
            }

            currentMidiTick = targetTick;
        }

        private float GetBeatsSinceLastMeterRegion()
        {
            ulong ticksPassedSinceLastRegion = currentMidiTick - startTickOfMeterRegion;
            float ticksPerDenom = midi.ticksPerQN * (4f / currentTimeSignature.Y);
            return ticksPassedSinceLastRegion / ticksPerDenom;
        }

        private void CalculateBeatAndMeasure()
        {
            float b = GetBeatsSinceLastMeterRegion();
            currentBeat = b % currentTimeSignature.X;
            currentMeasure = startMeasureOfMeterRegion + (ulong)Mathf.Floor(b / currentTimeSignature.X);
        }

        public (ulong measure, float beat) GetBeatAndMeasure()
        {
            return (currentMeasure, currentBeat);
        }

        /// <returns>Quarter notes per minute.</returns>
        public float GetTempo()
        {
            return currentTempo;
        }

        private void PopulateMaps()
        {
            foreach (TrackEvent e in midi.tracks[0].trackEvents)
            {
                if (e is TempoEvent)
                {
                    TempoEvent te = (TempoEvent)e;
                    if (tempoMap.Count > 0)
                    {
                        tempoMap[tempoMap.Count - 1].endTick = te.tick;
                    }
                    tempoMap.Add(new TempoRegion
                    {
                        startTick = te.tick,
                        endTick = ulong.MaxValue,
                        tempoEvent = te
                    });
                    //GD.Print($"on tick {te.tick} tempo becomes {te.GetBPM()}");
                }
                else if (e is TimeSignatureEvent)
                {
                    TimeSignatureEvent te = (TimeSignatureEvent)e;
                    timeSignatureMap.Add(te);
                    //GD.Print($"on tick {te.tick} time signature becomes {te.GetFractionString()}");
                }
                else if (e is BaseTextEvent)
                {
                    BaseTextEvent te = (BaseTextEvent)e;
                    textMap.Add(te);
                    //GD.Print($"on tick {te.tick} text of type {te.displayID} says: {te.text}");
                }
                else if (e is KeySignatureEvent)
                {
                    KeySignatureEvent ke = (KeySignatureEvent)e;
                    keySignatureMap.Add(ke);
                }
            }
        }

        public override void _Ready()
        {
            base._Ready();
            mainSynced = this;
            estimatedLatency = -0.045f;
            GD.Print("latency is " + estimatedLatency + " s");
            ProcessPriority = PROCESS_PRIORITY;
        }

        public override void _Process(double delta)
        {
            if (currentMusic == null) { return; }
            HandleDuckMultiplier();
            HandleFadeMultiplier();
            HandleVolume();
            HandlePause();
            if (currentMusic is null or not SyncedMusic || !currentMusic.Playing) { return; }
            SyncedMusic sm = (SyncedMusic)currentMusic;
            Calculate:
            float playbackPos = sm.GetPlaybackPosition();
            float goalPlaybackPos = (Blastula.FrameCounter.stageFrame - startFrame) / (float)Blastula.VirtualVariables.Persistent.SIMULATED_FPS;
            ulong targetTick = SolveTargetTick(Mathf.Max(0, goalPlaybackPos - estimatedLatency));
            ProcessMidi(targetTick);
            ulong lastCurrentMeasure = currentMeasure;
            if (sm.loopRegion == Vector2.Zero || targetTick < loopRegionAsTicks.Item2)
            {
                CalculateBeatAndMeasure();
            }

            // Attempted correction if the playback position is intolerably different
            if (Mathf.Abs(goalPlaybackPos - playbackPos) > TOLERANCE)
            {
                Seek(Mathf.Max(0f, 0.5f * (playbackPos + goalPlaybackPos)));
            }

            if (sm.loopRegion != Vector2.Zero)
            {
                // Loop the music
                if (goalPlaybackPos >= sm.loopRegion.Y)
                {
                    startFrame += (ulong)Math.Round((sm.loopRegion.Y - sm.loopRegion.X) * Blastula.VirtualVariables.Persistent.SIMULATED_FPS);
                    Seek(playbackPos - sm.loopRegion.Y + sm.loopRegion.X);
                    MidiSeek(goalPlaybackPos - sm.loopRegion.Y + sm.loopRegion.X);
                    goto Calculate;
                }
            }
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            if (mainSynced == this) { mainSynced = null; }
        }
    }
}
