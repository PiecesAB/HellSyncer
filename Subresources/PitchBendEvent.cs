using Godot;
using System;

namespace HellSyncer.Midi
{
    public partial class PitchBendEvent : ChannelEvent
    {
        [Export()]
        public short newValue;
        public PitchBendEvent() { }
        public PitchBendEvent(ulong tick, byte channel, short newValue) : base(tick, channel)
        {
            displayID = EventID.PitchBend;
            this.newValue = newValue;
        }
    }
}
