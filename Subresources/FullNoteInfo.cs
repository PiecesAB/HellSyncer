using Godot;
using System;

namespace HellSyncer.Midi
{
    // In which we correspond NoteOn with NoteOff to get the full information of a note
    public partial class FullNoteInfo : NoteOnEvent
    {
        [Export()]
        public ulong durationInTicks;

        public FullNoteInfo() { }

        public FullNoteInfo(ulong tick, byte channel, byte note, byte velocity, ulong durationInTicks) : base(tick, channel, note, velocity)
        {
            this.durationInTicks = durationInTicks;
        }

        public FullNoteInfo(NoteOnEvent noteOnEvent, ulong durationInTicks) : base(noteOnEvent.tick, noteOnEvent.channel, noteOnEvent.note, noteOnEvent.velocity)
        {
            this.durationInTicks = durationInTicks;
        }

        /// <summary>
        /// Duration in seconds at current tempo
        /// </summary>
        public float GetDuration()
        {
            if (AudioStreamSynced.main == null) { return 0f; }
            if (AudioStreamSynced.main.midi == null) { return 0f; }
            // Quarter notes per minute
            float currentTempo = AudioStreamSynced.main.GetTempo();
            float secondsInQuarterNote = 60f / currentTempo;
            float quarterDurationsInNote = durationInTicks / (float)AudioStreamSynced.main.midi.ticksPerQN;
            return secondsInQuarterNote * quarterDurationsInNote;
        }
    }
}
