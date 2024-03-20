using Godot;
using System;

namespace HellSyncer.Midi
{
    public partial class TrackEvent : Resource
    {
        [Export()]
        public EventID displayID = EventID.Unsupported;
        [Export()]
        public ulong tick;

        public TrackEvent() { }
        public TrackEvent(ulong tick) { this.tick = tick; }
    }
}
