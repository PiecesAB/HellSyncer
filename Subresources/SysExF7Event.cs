using Godot;
using System;

namespace HellSyncer.Midi
{
    public partial class SysExF7Event : TrackEvent
    {
        [Export()]
        public byte[] data;
        public SysExF7Event() { }
        public SysExF7Event(ulong tick) : base(tick) 
        {
            displayID = EventID.SysExF7;
        }
    }
}
