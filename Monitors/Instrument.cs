using Blastula.Sounds;
using Blastula.VirtualVariables;
using Godot;
using HellSyncer.Midi;
using System;

namespace HellSyncer
{
    /// <summary>
    /// Monitor to filter and emit signals when MIDI note events play.
    /// </summary>
    /// <remarks>
    /// Naturally, AudioStreamSynced.main must exist and be currently playing for this to emit any signals.
    /// </remarks>
    [GlobalClass]
    [Icon(Persistent.NODE_ICON_PATH + "/xylophone.png")]
    public partial class Instrument : Node
    {
        /// <summary>
        /// A whitelist to choose which MIDI tracks can use notes to trigger this instrument.
        /// </summary>
        /// <remarks>
        /// To view the tracks of a MIDI file and get an idea of what each track number contains,
        /// you will need an external editor. Be aware that HellSyncer considers the first track "Track 0",
        /// because it's possible the external MIDI editor considers the first track "Track 1".
        /// </remarks>
        [Export] public int[] trackIds = new int[1] { 1 };
        /// <summary>
        /// A whitelist to restrict the range of notes which trigger this instrument.
        /// Each vector (X, Y) is an interval of MIDI tones X to Y, including X and Y.
        /// </summary>
        /// <remarks>MIDI tones are integers 0-127 that each correspond to one semitone.</remarks>
        /// <example>Middle C = C4 = MIDI tone 60</example>
        /// <example>One semitone above middle C = C#4 = MIDI tone 61</example>
        /// <example>Lowest A on the standard piano = A0 = MIDI tone 21</example>
        [Export] public Vector2[] noteRanges = new Vector2[1] { new Vector2(0, 127) };
        /// <summary>
        /// The minimum velocity of note which triggers this instrument.
        /// In other words, the note must be at least this loud.
        /// </summary>
        [Export] public int minimumVelocity = 0;

        public const int PROCESS_PRIORITY = SyncedMusicManager.PROCESS_PRIORITY + 1;

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
            SyncedMusicManager.AddInstrumentListener(this);
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            SyncedMusicManager.RemoveInstrumentListener(this);
        }
    }
}

