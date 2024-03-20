using Blastula.Sounds;
using Blastula.VirtualVariables;
using Godot;
using HellSyncer.Midi;
using System;

namespace HellSyncer
{
    /// <summary>
    /// Metronome emit a signal when measures and particular intervals happen.
    /// </summary>
    [GlobalClass]
    [Icon(Persistent.NODE_ICON_PATH + "/xylophone.png")]
    public partial class Instrument : Node
    {
        [Export] public int[] trackIds = new int[1] { 1 };
        [Export] public Vector2[] noteRanges = new Vector2[1] { new Vector2(0, 127) };
        [Export] public int minimumVelocity = 0;

        public const int PROCESS_PRIORITY = AudioStreamSynced.PROCESS_PRIORITY + 1;

        [Signal] public delegate void OnNoteEventHandler(int midiTone, int velocity, float duration);

        private bool IsNoteInRange(FullNoteInfo note)
        {
            int val = note.note;
            foreach (Vector2 range in noteRanges)
            {
                if (range.X <= val && val <= range.Y) { return true; }
            }
            return false;
        }

        public void SelfOnNote(FullNoteInfo note)
        {
            //GD.Print(Name, ": note was played: ", note.note, " that is ", note.GetDuration(), " s long");
            if (!IsNoteInRange(note)) { return; }
            if (note.velocity < minimumVelocity) { return; }
            EmitSignal(SignalName.OnNote, note.note, note.velocity, note.GetDuration());
        }

        public override void _Ready()
        {
            base._Ready();
            AudioStreamSynced.AddInstrumentListener(this);
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            AudioStreamSynced.RemoveInstrumentListener(this);
        }
    }
}

