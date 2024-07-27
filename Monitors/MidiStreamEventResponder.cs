using Godot;
using System;
using System.Collections.Generic;
using HellSyncer.Midi;
using Blastula;
using Blastula.VirtualVariables;

namespace HellSyncer;

/// <summary>
/// This class is a shared way to queue note events to happen before or after their "real" time.
/// </summary>
/// <remarks>
/// It will attempt to disable and enable _Process on its own to be efficient.
/// </remarks>
public partial class MidiStreamEventResponder : Node
{
    /// <summary>
    /// The lookahead (or delay when negative) for the signal to be emitted.
    /// </summary>
    [Export] public float lookahead = 0f;

    public const int PROCESS_PRIORITY = SyncedMusicManager.PROCESS_PRIORITY + 1;

    /// <summary>
    /// If the lookahead of this instrument doesn't coincide with a MidiStreamHead,
    /// Then we'll need a way to delay events until our lookahead time.
    /// This queue is the strategy for the delay.
    /// </summary>
    protected Queue<(ulong targetFrame, TrackEvent)> midiEventQueue = new();

    protected MidiStreamHead SelectStreamHead()
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

    private ulong GetTargetEventFrame(ulong tick, bool mayHaveDiscrepancy)
    {
        // If it may not have a discrepancy, directly determine the frame in the future.
        // Otherwise we are forced to calculate the note's stage frame.
        if (lookahead <= 0f)
        {
            ulong frameDelay = (ulong)Mathf.RoundToInt(-lookahead * Blastula.VirtualVariables.Persistent.SIMULATED_FPS);
            if (mayHaveDiscrepancy) return SyncedMusicManager.GetStageFrameForTick(tick) + frameDelay;
            else return FrameCounter.stageFrame + frameDelay;
        }
        else if (lookahead <= SyncedMusicManager.mainSynced.lookahead)
        {
            ulong maxLookahead = (ulong)Mathf.RoundToInt(SyncedMusicManager.mainSynced.lookahead * Blastula.VirtualVariables.Persistent.SIMULATED_FPS);
            ulong frameCut = (ulong)Mathf.RoundToInt(lookahead * Blastula.VirtualVariables.Persistent.SIMULATED_FPS);
            if (mayHaveDiscrepancy) return SyncedMusicManager.GetStageFrameForTick(tick) - frameCut;
            else return FrameCounter.stageFrame + maxLookahead - frameCut;
        }
        else return 0;
    }

    private Callable removeFromMoment;
    private Callable addToMoment;

    public virtual void AddToMoment()
    {

    }

    public virtual void RemoveFromMoment()
    {
        midiEventQueue.Clear();
    }

    public virtual bool FilterEvent(TrackEvent e) => true;

    public void OnRecieveEvent(TrackEvent e, bool mayHaveDiscrepancy)
    {
        if (!FilterEvent(e)) { return; }
        // Fire instantly or enqueue it to fire when the time is right.
        if (lookahead == 0f || lookahead == SyncedMusicManager.mainSynced.lookahead)
        {
            FireEvent(e);
        }
        else
        {
            ulong targetFrame = GetTargetEventFrame(e.tick, mayHaveDiscrepancy);
            // If the frame has already passed, skip the note.
            if (targetFrame >= FrameCounter.stageFrame)
            {
                midiEventQueue.Enqueue((targetFrame, e));
                ProcessMode = ProcessModeEnum.Always;
            }
        }
    }

    public virtual void FireEvent(TrackEvent e)
    {

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

    public override void _Process(double deltaTime)
    {
        if (Session.main.paused) return;
        if (midiEventQueue.Count == 0) ProcessMode = ProcessModeEnum.Disabled;
        while (midiEventQueue.Count > 0 && midiEventQueue.Peek().targetFrame <= FrameCounter.stageFrame)
        {
            (_, TrackEvent e) = midiEventQueue.Dequeue();
            FireEvent(e);
        }
    }
}
