using Godot;
using System;

namespace HellSyncer.Midi
{
    public partial class TempoEvent : TrackEvent
    {
        [Export()]
        public ulong microsecondsPerQN;
        public TempoEvent() { }
        public TempoEvent(ulong tick, ulong microsecondsPerQN) : base(tick)
        {
            displayID = EventID.Tempo;
            this.microsecondsPerQN = microsecondsPerQN;
        }
        public float GetBPM() { return 1e6f * 60f / microsecondsPerQN; }
    }
}
