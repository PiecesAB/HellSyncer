using Godot;
using System;

namespace HellSyncer.Midi
{
    public partial class TimeSignatureEvent : TrackEvent
    {
        [Export()]
        public byte numerator;
        [Export()]
        public byte denominator;
        // There's more metronome info but we don't care lol
        public TimeSignatureEvent() { }
        public TimeSignatureEvent(ulong tick, byte numerator, byte denominator) : base(tick)
        {
            displayID = EventID.TimeSignature;
            this.numerator = numerator;
            this.denominator = denominator;
        }
        public string GetFractionString() { return numerator.ToString() + "/" + denominator.ToString(); }
    }
}
