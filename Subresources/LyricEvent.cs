using Godot;
using System;

namespace HellSyncer.Midi
{
    public partial class LyricEvent : BaseTextEvent
    {
        public LyricEvent() { }
        public LyricEvent(ulong tick, string text) : base(tick, text) 
        {
            displayID = EventID.Lyric;
        }
    }
}
