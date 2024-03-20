using Godot;
using System;

namespace HellSyncer.Midi
{
    public partial class MarkerEvent : BaseTextEvent
    {
        public MarkerEvent() { }
        public MarkerEvent(ulong tick, string text) : base(tick, text) 
        {
            displayID = EventID.Marker;
        }
    }
}
