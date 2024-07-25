using Godot;
using System;
using System.Collections.Generic;
using HellSyncer.Midi;

namespace HellSyncer
{
    /// <summary>
    /// Generalized class for reading and broadcasting MIDI data, scanning until a song position for events.
    /// This doesn't execute on its own, as it's not a Node! Its play time must be driven somewhere else.
    /// </summary>
    public partial class MidiStreamHead
    {
        private class TempoRegion
        {
            public TempoEvent tempoEvent;
            public ulong startTick;
            public ulong endTick;

            public ulong GetDurationInTicks() { return endTick - startTick; }
            public float GetDurationInSeconds()
            {
                float ticksPerQN = SyncedMusicManager.midi.ticksPerQN;
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
        /// <summary>
        /// mayHaveDiscrepancy == true means the actual note time should be recalculated;
        /// saying that it started this frame is predicted not to be accurate.
        /// This can happen if a skip occurs between reads, like at the beginning of a track that looks ahead.
        /// </summary>
        public delegate void OnNote(FullNoteInfo info, bool mayHaveDiscrepancy);
        public delegate void OnText(BaseTextEvent text, bool mayHaveDiscrepancy);
        private Dictionary<int, OnNote> noteAlerts = new Dictionary<int, OnNote>();
        private OnText textAlert;

        private ulong currentMidiTick;
        private float currentBeat = 0;
        private ulong currentMeasure = 0;
        public ulong GetCurrentMeasure() { return currentMeasure; }
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

        public void Initialize()
        {
            PopulateMaps();
            foreach (Track track in SyncedMusicManager.midi.tracks)
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

        public void MidiSeek(float time)
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
            for (int i = 1; i < SyncedMusicManager.midi.tracks.Length; ++i)
            {
                trackEventIndices[i] = SyncedMusicManager.midi.tracks[i].GetFullNoteIndexAtTick(tick);
            }
        }

        public void AddInstrumentListener(Instrument ins)
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

        public void RemoveInstrumentListener(Instrument ins)
        {
            foreach (int trackID in ins.trackIds)
            {
                if (noteAlerts.ContainsKey(trackID))
                {
                    noteAlerts[trackID] -= ins.SelfOnNote;
                }
            }
        }

        public void AddTextListener(MidiTextReader con)
        {
            textAlert += con.SelfOnText;
        }

        public void RemoveTextListener(MidiTextReader con)
        {
            textAlert -= con.SelfOnText;
        }

        public ulong SolveTargetTick(float goalPlaybackPos)
        {
            float timeSinceStartOfRegion = goalPlaybackPos - startTimeOfTempoRegion;
            float beatsSinceStartOfRegion = timeSinceStartOfRegion / GetQuarterNoteDuration();
            ulong ticksSinceStartOfRegion = (ulong)Math.Round(beatsSinceStartOfRegion * SyncedMusicManager.midi.ticksPerQN);
            return startTickOfTempoRegion + ticksSinceStartOfRegion;
        }

        private void ProcessTrack0(ulong targetTick, bool sendTextEvents = true, bool mayHaveDiscrepancy = false)
        {
            // Scan through raw Track 0 events until arriving at the target tick.
            Track ctrack = SyncedMusicManager.midi.tracks[0];
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
                            float calculatedDurationOfLastRegion = (ticksPassedSinceLastRegion / (float)SyncedMusicManager.midi.ticksPerQN) * GetQuarterNoteDuration();
                            startTickOfTempoRegion = @event.tick;
                            startTimeOfTempoRegion += calculatedDurationOfLastRegion;
                            currentTempo = ((TempoEvent)@event).GetBPM();
                            break;
                        }
                    case EventID.TimeSignature:
                        {
                            ulong ticksPassedSinceLastRegion = @event.tick - startTickOfMeterRegion;
                            float calculatedDurationOfLastRegion = (ticksPassedSinceLastRegion / (float)SyncedMusicManager.midi.ticksPerQN) * GetQuarterNoteDuration();
                            float ticksPerDenom = SyncedMusicManager.midi.ticksPerQN * (4f / currentTimeSignature.Y);
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
                    textAlert?.Invoke((BaseTextEvent)@event, mayHaveDiscrepancy);
                }
                trackEventIndices[0] = ++eventIndex;
            }
        }

        public void ProcessMidi(ulong targetTick)
        {
            // mayHaveDiscrepancy == true means the actual note time should be recalculated;
            // saying that it started this frame is predicted not to be accurate.
            // This can happen if a skip occurs between reads, like at the beginning of a track that looks ahead.
            bool mayHaveDiscrepancy 
                = TickToTime(targetTick) - TickToTime(currentMidiTick) >= 1.5f / Blastula.VirtualVariables.Persistent.SIMULATED_FPS;
            // In Track 0 we expect all meter, tempo, and text information.
            // In all other tracks we expect instruments, and use only the parsed full note data.
            ProcessTrack0(targetTick, true, mayHaveDiscrepancy);
            // Scan through full notes of other tracks until arriving at the target tick.
            for (int trackIndex = 1; trackIndex < SyncedMusicManager.midi.tracks.Length; ++trackIndex)
            {
                Track itrack = SyncedMusicManager.midi.tracks[trackIndex];
                int noteIndex = trackEventIndices[trackIndex];
                if (noteIndex < 0) { noteIndex = trackEventIndices[trackIndex] = 0; }
                while (noteIndex < itrack.fullNoteInfos.Length)
                {
                    FullNoteInfo note = itrack.fullNoteInfos[noteIndex];
                    if (note.tick > targetTick) { break; }
                    if (noteAlerts.ContainsKey(trackIndex))
                    {
                        noteAlerts[trackIndex].Invoke(note, mayHaveDiscrepancy);
                    }
                    trackEventIndices[trackIndex] = ++noteIndex;
                }
            }

            currentMidiTick = targetTick;
        }

        private float GetBeatsSinceLastMeterRegion()
        {
            ulong ticksPassedSinceLastRegion = currentMidiTick - startTickOfMeterRegion;
            float ticksPerDenom = SyncedMusicManager.midi.ticksPerQN * (4f / currentTimeSignature.Y);
            return ticksPassedSinceLastRegion / ticksPerDenom;
        }

        public void CalculateBeatAndMeasure()
        {
            float b = GetBeatsSinceLastMeterRegion();
            currentBeat = b % currentTimeSignature.X;
            currentMeasure = startMeasureOfMeterRegion + (ulong)Mathf.Floor(b / currentTimeSignature.X);
        }

        /// <returns>
        /// "beat" is a number of beats since the start of the measure.
        /// It is expected to be in the interval [0, currentTimeSignature.X).
        /// So the first beat is the zeroth.
        /// The first measure is also the zeroth.
        /// </returns>
        public (ulong measure, float beat) GetBeatAndMeasure(bool forceQuarterNoteBeat = false)
        {
            if (forceQuarterNoteBeat) return (currentMeasure, currentBeat * (4f / currentTimeSignature.Y));
            else return (currentMeasure, currentBeat);
        }

        /// <returns>
        /// The current fractional part representing progress throughout the measure.
        /// It is expected to be in the interval [0, 1).
        /// For example, a value of 0.5 means the measure is halfway complete.
        /// </returns>
        public double GetMeasureProgress()
        {
            return GetBeatAndMeasure().beat / (float)currentTimeSignature.X;
        }

        /// <returns>Quarter notes per minute.</returns>
        public float GetTempo()
        {
            return currentTempo;
        }

        private void PopulateMaps()
        {
            foreach (TrackEvent e in SyncedMusicManager.midi.tracks[0].trackEvents)
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
    }
}

