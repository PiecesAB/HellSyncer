using Godot;
using System;

namespace HellSyncer.Midi
{
    public partial class TrackNameEvent : BaseTextEvent
    {
        public TrackNameEvent() { }
        public TrackNameEvent(ulong tick, string text) : base(tick, text) 
        {
            displayID = EventID.TrackName;
        }
    }
}
