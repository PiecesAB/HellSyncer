using Godot;
using System;

namespace HellSyncer.Midi
{
    /// <summary>
    /// A NoteOn MIDI event for which a NoteOff event has been corresponded.
    /// We can thus get the duration of the note and store it together as a "full note" here.
    /// </summary>
    /// <example>
    /// Getting the duration of notes is important for certain barrage effects.
    /// It can be used, for instance, to shoot a piano roll of lasers, where laser length and note length exactly correspond.
    /// </example>
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
        /// Duration in seconds at current tempo.
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
