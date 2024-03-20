using Godot;
using System;

namespace HellSyncer.Midi
{
    public partial class SysExF0Event : TrackEvent
    {
        [Export()]
        public byte[] data;
        public SysExF0Event() { }
        public SysExF0Event(ulong tick) : base(tick) 
        {
            displayID = EventID.SysExF0;
        }
    }
}
