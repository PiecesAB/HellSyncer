using Godot;
using System;

namespace HellSyncer.Midi
{
    public partial class BaseTextEvent : TrackEvent
    {
        [Export()]
        public string text;
        public BaseTextEvent() { }
        public BaseTextEvent(ulong tick, string text) : base(tick)
        {
            this.text = text;
        }
    }
}
