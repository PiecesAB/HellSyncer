using Blastula;
using Blastula.VirtualVariables;
using Godot;
using HellSyncer.Midi;
using System;
using System.Collections.Generic;

namespace HellSyncer
{
    [GlobalClass]
    public partial class AudioStreamSynced : AudioStreamPlayer
    {
        [Export] public Vector2 mainLoop = Vector2.Zero;
        [ExportGroup("MIDI")]
        [Export] public ParsedMidi midi;
        [ExportGroup("Beat MIDI Generator")]
        // "BPM" is actually quarter notes per minute.
        [Export] public float generatedBpm = 120f;
        [Export] public Vector2I generatedTimeSignature = new Vector2I(4, 4);

        /// <summary>
        /// If the audible music gets this far from the MIDI (in seconds), then we will force the music to lag or skip.
        /// The reason this happens is because of unpredictable lag in the game,
        /// but the MIDI needs to run deterministically (for replays).
        /// It's not pretty, but we can have our cake and eat it too.
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
                float ticksPerQN = main.midi.ticksPerQN;
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

        /// <summary>
        /// There can only be one... for now.
        /// </summary>
        public static AudioStreamSynced main;

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
            return (ulong)Math.Round(
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
            main = this;
            estimatedLatency = (float)(AudioServer.GetTimeToNextMix() + AudioServer.GetOutputLatency());
            if (midi == null)
            {
                midi = new ParsedMidi();
                midi.GenerateForBeat(generatedBpm, generatedTimeSignature);
            }
            InitializeMidiStuff();
            if (Autoplay) { PlaySynced(); }
            ProcessPriority = PROCESS_PRIORITY;
        }

        public void PlaySynced() {
            startFrame = FrameCounter.stageFrame;
            MidiSeek(0);
            ProcessMidi(0);
            Play(0);
        }

        public override void _Process(double delta)
        {
            base._Process(delta);
            if (Session.IsPaused() || Blastula.Debug.GameFlow.frozen)
            {
                if (Playing) { StreamPaused = true; }
                return;
            }
            if (StreamPaused) { StreamPaused = false; }
            if (Playing)
            {
                float playbackPos = GetPlaybackPosition();
                float goalPlaybackPos = (FrameCounter.stageFrame - startFrame) / (float)Blastula.VirtualVariables.Persistent.SIMULATED_FPS;
                ulong targetTick = SolveTargetTick(Mathf.Max(0, goalPlaybackPos - estimatedLatency));
                ProcessMidi(targetTick);
                ulong lastCurrentMeasure = currentMeasure;
                CalculateBeatAndMeasure();

                // Attempted correction if the playback position is intolerably different
                if (Mathf.Abs(goalPlaybackPos - playbackPos) > TOLERANCE)
                {
                    Seek(Mathf.Max(0f, 0.5f * (playbackPos + goalPlaybackPos)));
                }

                if (mainLoop != Vector2.Zero)
                {
                    // Loop the music
                    if (goalPlaybackPos >= mainLoop.Y)
                    {
                        startFrame += (ulong)Math.Round((mainLoop.Y - mainLoop.X) * Blastula.VirtualVariables.Persistent.SIMULATED_FPS);
                        Seek(playbackPos - mainLoop.Y + mainLoop.X);
                        MidiSeek(goalPlaybackPos - mainLoop.Y + mainLoop.X);
                    }
                }
            }
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            if (main == this) { main = null; }
        }
    }
}
