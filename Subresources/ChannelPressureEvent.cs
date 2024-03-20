using Godot;
using System;

namespace HellSyncer.Midi
{
    public partial class ChannelPressureEvent : ChannelEvent
    {
        [Export()]
        public byte pressure;
        public ChannelPressureEvent() { }
        public ChannelPressureEvent(ulong tick, byte channel, byte pressure) : base(tick, channel)
        {
            displayID = EventID.ChannelPressure;
            this.pressure = pressure;
        }
    }
}
