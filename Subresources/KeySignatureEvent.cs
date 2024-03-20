using Godot;
using System;

namespace HellSyncer.Midi
{
    public partial class KeySignatureEvent : TrackEvent
    {
        [Export()]
        public sbyte sharpCount;
        [Export()]
        public bool isMinor;
        public KeySignatureEvent() { }
        public KeySignatureEvent(ulong tick, sbyte sharpCount, bool isMinor) : base(tick)
        {
            displayID = EventID.KeySignature;
            this.sharpCount = sharpCount;
            this.isMinor = isMinor;
        }
    }
}
