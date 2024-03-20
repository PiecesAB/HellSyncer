using Godot;
using System;

namespace HellSyncer.Midi
{
    public partial class TextEvent : BaseTextEvent
    {
        public TextEvent() { }
        public TextEvent(ulong tick, string text) : base(tick, text) 
        {
            displayID = EventID.Text;
        }
    }
}
