using Godot;
using System;

namespace HellSyncer.Midi
{
    public partial class UnsupportedEvent : TrackEvent
    {
        public UnsupportedEvent() { }
        public UnsupportedEvent(ulong tick) : base(tick) { }
    }
}
