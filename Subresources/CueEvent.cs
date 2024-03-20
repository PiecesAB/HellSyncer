using Godot;
using System;

namespace HellSyncer.Midi
{
    public partial class CueEvent : BaseTextEvent
    {
        public CueEvent() { }
        public CueEvent(ulong tick, string text) : base(tick, text) 
        {
            displayID = EventID.Cue;
        }
    }
}
