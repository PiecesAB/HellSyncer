using Godot;
using System;

namespace HellSyncer.Midi
{
    public partial class KeyPressureEvent : PVEvent
    {
        public KeyPressureEvent() { }
        public KeyPressureEvent(ulong tick, byte channel, byte note, byte velocity) : base(tick, channel, note, velocity)
        {
            displayID = EventID.KeyPressure;
        }
    }
}
