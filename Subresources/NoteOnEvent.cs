using Godot;
using System;

namespace HellSyncer.Midi
{
    public partial class NoteOnEvent : PVEvent
    {
        public NoteOnEvent() { }
        public NoteOnEvent(ulong tick, byte channel, byte note, byte velocity) : base(tick, channel, note, velocity)
        {
            displayID = EventID.NoteOn;
        }
    }
}
