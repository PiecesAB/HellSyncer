using Godot;
using System;

namespace HellSyncer.Midi
{
    public partial class InstrumentNameEvent : BaseTextEvent
    {
        public InstrumentNameEvent() { }
        public InstrumentNameEvent(ulong tick, string text) : base(tick, text) 
        {
            displayID = EventID.InstrumentName;
        }
    }
}
