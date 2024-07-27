using Blastula.Sounds;
using Blastula.VirtualVariables;
using Godot;
using HellSyncer.Midi;
using System;

namespace HellSyncer
{
    /// <summary>
    /// Emits a signal for text events embedded in the MIDI.
    /// </summary>
    /// <remarks>
    /// Naturally, AudioStreamSynced.main must exist and be currently playing for this to emit any signals.
    /// </remarks>
    [GlobalClass]
    [Icon(Persistent.NODE_ICON_PATH + "/textbook.png")]
    public partial class MidiTextReader : MidiStreamEventResponder
    {
        /// <summary>
        /// Filter out any text event that doesn't match this type.
        /// </summary>
        [Export] public TextEventID textEventType = TextEventID.Any;
        /// <summary>
        /// If not empty, filter out any text event that doesn't contain this substring.
        /// </summary>
        [Export] public string magicPhrase = "";

        [Signal] public delegate void OnTextEventHandler(string text);

        private bool TextTypeMatches(BaseTextEvent @event)
        {
            if (textEventType == TextEventID.Any) { return true; }
            return (int)@event.displayID == (int)textEventType;
        }

        public override bool FilterEvent(TrackEvent e)
        {
            var t = (BaseTextEvent)e;
            if (!TextTypeMatches(t)) { return false; }
            if (magicPhrase != null && magicPhrase != "" && !t.text.Contains(magicPhrase)) { return false; }
            return true;
        }

        public override void FireEvent(TrackEvent e)
        {
            GD.Print(Name + ": " + ((BaseTextEvent)e).text);
            EmitSignal(SignalName.OnText, ((BaseTextEvent)e).text);
        }

        public override void RemoveFromMoment()
        {
            SelectStreamHead()?.RemoveTextListener(this);
        }

        public override void AddToMoment()
        {
            SelectStreamHead()?.AddTextListener(this);
        }
    }
}

