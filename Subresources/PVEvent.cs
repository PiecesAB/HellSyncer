using Godot;
using System;

namespace HellSyncer.Midi
{
    public partial class PVEvent : ChannelEvent
    {
        [Export()]
        public byte note;
        [Export()]
        public byte velocity;
        public PVEvent() { }
        public PVEvent(ulong tick, byte channel, byte note, byte velocity) : base(tick, channel)
        {
            this.note = note;
            this.velocity = velocity;
        }
    }
}
