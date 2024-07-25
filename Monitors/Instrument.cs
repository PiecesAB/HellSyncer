using Blastula;
using Blastula.Sounds;
using Blastula.VirtualVariables;
using Godot;
using HellSyncer.Midi;
using System;
using System.Collections.Generic;

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
        /// The lookahead (or delay when negative) for the signal of this instrument to be emitted.
        /// </summary>
        [Export] public float lookahead = 0f;
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

        private MidiStreamHead SelectStreamHead()
        {
            if (lookahead <= 0f) return SyncedMusicManager.momentaryStreamHead;
            else if (lookahead <= SyncedMusicManager.mainSynced.lookahead) return SyncedMusicManager.envisionStreamHead;
            throw new ArgumentOutOfRangeException(
                "Instrument is trying to look too far ahead " +
                $"({lookahead} seconds, but the maximum is {SyncedMusicManager.mainSynced.lookahead}). " +
                "If you'd like to look further ahead at a computational cost, " +
                "increase the max lookahead in the SyncedMusicManager."
            );
        }

        private ulong GetTargetNoteFrame(FullNoteInfo note, bool mayHaveDiscrepancy)
        {
            // If it may not have a discrepancy, directly determine the frame in the future.
            // Otherwise we are forced to calculate the note's stage frame.
            if (lookahead <= 0f)
            {
                ulong frameDelay = (ulong)Mathf.RoundToInt(-lookahead * Blastula.VirtualVariables.Persistent.SIMULATED_FPS);
                if (mayHaveDiscrepancy) return SyncedMusicManager.GetStageFrameForTick(note.tick) + frameDelay;
                else return FrameCounter.stageFrame + frameDelay;
            }
            else if (lookahead <= SyncedMusicManager.mainSynced.lookahead)
            {
                float adjustedLookahead = lookahead - SyncedMusicManager.mainSynced.lookahead;
                ulong frameDelay = (ulong)Mathf.RoundToInt(-adjustedLookahead * Blastula.VirtualVariables.Persistent.SIMULATED_FPS);
                if (mayHaveDiscrepancy) return SyncedMusicManager.GetStageFrameForTick(note.tick) + frameDelay;
                else return FrameCounter.stageFrame + frameDelay;
            }
            else return 0;
        }

        public void SelfOnNote(FullNoteInfo note, bool mayHaveDiscrepancy)
        {
            if (mayHaveDiscrepancy)
            {
                GD.Print(Name, ": note was played with discrepancy: ", note.note, " that is ", note.GetDuration(SelectStreamHead()), " s long");
            }
                
            if (!IsNoteInRange(note)) { return; }
            if (note.velocity < minimumVelocity) { return; }
            // Fire instantly or enqueue it to fire when the time is right.
            if (lookahead == 0f || lookahead == SyncedMusicManager.mainSynced.lookahead)
            {
                EmitSignal(SignalName.OnNote, note.note, note.velocity, note.GetDuration(SelectStreamHead()));
            }
            else
            {
                ulong targetFrame = GetTargetNoteFrame(note, mayHaveDiscrepancy);
                // If the frame has already passed, skip the note.
                if (targetFrame >= FrameCounter.stageFrame)
                {
                    noteQueue.Enqueue((targetFrame, note));
                    ProcessMode = ProcessModeEnum.Always;
                }
            }
        }

        private Callable removeFromMoment;
        private Callable addToMoment;

        private void RemoveFromMoment()
        {
            SelectStreamHead()?.RemoveInstrumentListener(this);
        }

        private void AddToMoment()
        {
            SelectStreamHead()?.AddInstrumentListener(this);
        }

        public override void _Ready()
        {
            base._Ready();
            AddToMoment();
            removeFromMoment = new Callable(this, MethodName.RemoveFromMoment);
            addToMoment = new Callable(this, MethodName.AddToMoment);
            SyncedMusicManager.mainSynced.Connect(SyncedMusicManager.SignalName.MusicChangeImminent, removeFromMoment);
            SyncedMusicManager.mainSynced.Connect(SyncedMusicManager.SignalName.MusicChangeComplete, addToMoment);
            ProcessPriority = PROCESS_PRIORITY;
            // Process only runs when there is a queue of notes to empty.
            ProcessMode = ProcessModeEnum.Disabled;
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            RemoveFromMoment();
            SyncedMusicManager.mainSynced.Disconnect(SyncedMusicManager.SignalName.MusicChangeImminent, removeFromMoment);
            SyncedMusicManager.mainSynced.Disconnect(SyncedMusicManager.SignalName.MusicChangeComplete, addToMoment);
        }

        private Queue<(ulong targetFrame, FullNoteInfo note)> noteQueue = new();
        public override void _Process(double deltaTime)
        {
            if (Session.main.paused) return;
            if (noteQueue.Count == 0) ProcessMode = ProcessModeEnum.Disabled;
            while (noteQueue.Count > 0 && noteQueue.Peek().targetFrame <= FrameCounter.stageFrame)
            {
                (_, FullNoteInfo note) = noteQueue.Dequeue();
                EmitSignal(SignalName.OnNote, note.note, note.velocity, note.GetDuration(SelectStreamHead()));
            }
        }
    }
}

