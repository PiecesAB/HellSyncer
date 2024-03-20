using Godot;
using System;

namespace HellSyncer.Midi
{
    public partial class ProgramChangeEvent : ChannelEvent
    {
        [Export()]
        public byte newProgramNumber;
        public ProgramChangeEvent() { }
        public ProgramChangeEvent(ulong tick, byte channel, byte newProgramNumber) : base(tick, channel)
        {
            displayID = EventID.ProgramChange;
            this.newProgramNumber = newProgramNumber;
        }
    }
}
