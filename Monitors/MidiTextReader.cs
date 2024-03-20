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
    [GlobalClass]
    [Icon(Persistent.NODE_ICON_PATH + "/textbook.png")]
    public partial class MidiTextReader : Node
    {
        /// <summary>
        /// Filter out any text event that doesn't match this type.
        /// </summary>
        [Export] public TextEventID textEventType = TextEventID.Any;
        /// <summary>
        /// If not empty, filter out any text event that doesn't contain this substring.
        /// </summary>
        [Export] public string magicPhrase = "";

        public const int PROCESS_PRIORITY = AudioStreamSynced.PROCESS_PRIORITY + 1;

        [Signal] public delegate void OnTextEventHandler(string text);

        private bool TextTypeMatches(BaseTextEvent @event)
        {
            if (textEventType == TextEventID.Any) { return true; }
            return (int)@event.displayID == (int)textEventType;
        }

        public void SelfOnText(BaseTextEvent @event)
        {
            if (!TextTypeMatches(@event)) { return; }
            if (magicPhrase != null && magicPhrase != "" && !@event.text.Contains(magicPhrase)) { return; }
            //GD.Print($"recieved text signal of type {@event.displayID}: ", @event.text);
            EmitSignal(SignalName.OnText, @event.text);
        }

        public override void _Ready()
        {
            base._Ready();
            AudioStreamSynced.AddTextListener(this);
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            AudioStreamSynced.RemoveTextListener(this);
        }
    }
}

