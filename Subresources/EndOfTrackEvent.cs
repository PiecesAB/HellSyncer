using Godot;
using System;

namespace HellSyncer.Midi
{
    public partial class EndOfTrackEvent : TrackEvent
    {
        public EndOfTrackEvent() { }
        public EndOfTrackEvent(ulong tick) : base(tick) 
        {
            displayID = EventID.EndOfTrack;
        }
    }
}
