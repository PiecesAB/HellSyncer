using Godot;
using System;

namespace HellSyncer.Midi
{
    public partial class CopyrightEvent : BaseTextEvent
    {
        public CopyrightEvent() { }
        public CopyrightEvent(ulong tick, string text) : base(tick, text) 
        {
            displayID = EventID.Copyright;
        }
    }
}
