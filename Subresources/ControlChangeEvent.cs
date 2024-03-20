using Godot;
using System;

namespace HellSyncer.Midi
{
    public partial class ControlChangeEvent : ChannelEvent
    {
        [Export()]
        public byte controllerNumber;
        [Export()]
        public byte newValue;
        public ControlChangeEvent() { }
        public ControlChangeEvent(ulong tick, byte channel, byte controllerNumber, byte newValue) : base(tick, channel)
        {
            displayID = EventID.ControlChange;
            this.controllerNumber = controllerNumber;
            this.newValue = newValue;
        }
    }
}
