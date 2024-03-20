using Godot;
using System;

namespace HellSyncer.Midi
{
    public partial class NoteOffEvent : PVEvent
    {
        public NoteOffEvent() { }
        public NoteOffEvent(ulong tick, byte channel, byte note, byte velocity) : base(tick, channel, note, velocity) 
        {
            displayID = EventID.NoteOff;
        }
    }
}
