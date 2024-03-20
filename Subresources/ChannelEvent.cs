using Godot;
using System;

namespace HellSyncer.Midi
{
    public partial class ChannelEvent : TrackEvent
    {
        [Export()]
        public byte channel;
        public ChannelEvent() { }
        public ChannelEvent(ulong tick, byte channel) : base(tick) { this.channel = channel; }
    }
}
