using Blastula.VirtualVariables;
using Godot;
using HellSyncer.Midi;
using System;
using System.Threading.Tasks;

namespace HellSyncer
{
    /// <summary>
    /// Represents a track of background music with an associated MIDI track.
    /// Should have a unique name and be a descendent of the MusicManager.
    /// </summary>
    [GlobalClass]
    [Icon(Persistent.NODE_ICON_PATH + "/wolfDark.png")]
    public partial class SyncedMusic : Blastula.Sounds.Music
    {
        /// <summary>
        /// The MIDI file which this audio player is tied to. 
        /// If null, generatedBpm and generatedTimeSignature will be used to create a simple MIDI file for the beat.
        /// </summary>
        [Export] public ParsedMidi midi;
        /// <summary>
        /// Used when midi == null. Quarter notes per minute.
        /// </summary>
        [ExportGroup("Beat MIDI Generator")]
        [Export] public float generatedBpm = 120f;
        /// <summary>
        /// Used when midi == null. (X, Y) is (numerator, denominator) of the time signature.
        /// </summary>
        [Export] public Vector2I generatedTimeSignature = new Vector2I(4, 4);
    }
}
