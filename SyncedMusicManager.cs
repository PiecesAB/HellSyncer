using Blastula.Sounds;
using Blastula.VirtualVariables;
using Godot;
using HellSyncer.Midi;
using System.Collections.Generic;
using System;

namespace HellSyncer
{
    /// <summary>
    /// Handles the game's background music, with special syncing logic.
    /// </summary>
    /// <remarks>
    /// This is also the singleton MusicManager.
    /// </remarks>
    public partial class SyncedMusicManager : Blastula.Sounds.MusicManager
    {
        /// <summary>
        /// If the audible music gets this far from the MIDI (in seconds), then we will force the music to lag or skip.
        /// The reason this happens is because of unpredictable lag in the game,
        /// but the MIDI needs to run deterministically (for replays).
        /// It's not pretty, but it keeps the sync.
        /// </summary>
        public const float TOLERANCE = 0.03f;
        /// <summary>
        /// Occurs early in the frame to allow other items to respond to the changes
        /// </summary>
        public const int PROCESS_PRIORITY = -1000000;
        
        private ulong startFrame;
        private (ulong, ulong) loopRegionAsTicks = (0, 0);

        public static SyncedMusicManager mainSynced;
        public static ParsedMidi midi;
        /// <summary>
        /// This MidiStreamHead will fire off events "exactly when they occur in the music".
        /// </summary>
        public static MidiStreamHead momentaryStreamHead;
        /// <summary>
        /// This helps to sync because it takes a tiny amount of time for a sound to load,
        /// and because the player expects that game actions happen slightly before their sound, as in animation.
        /// </summary>
        private float MOMENTARY_LOOKAHEAD = 0.045f;

        [Signal] public delegate void MusicChangeImminentEventHandler();
        [Signal] public delegate void MusicChangeCompleteEventHandler();

        public new static void PlayImmediate(string nodeName)
        {
            if (mainSynced == null) { return; }
            if (!main.musicsByNodeName.ContainsKey(nodeName)) { return; }
            Music nextMusic = main.musicsByNodeName[nodeName];
            if (nextMusic is not SyncedMusic) { return; }

            mainSynced.EmitSignal(SignalName.MusicChangeImminent);

            SyncedMusic sm = (SyncedMusic)nextMusic;
            MusicManager.PlayImmediate(nodeName);
            mainSynced.startFrame = Blastula.FrameCounter.stageFrame;
            if ((midi = sm.midi) == null)
            {
                midi = new ParsedMidi();
                midi.GenerateForBeat(sm.generatedBpm, sm.generatedTimeSignature);
            }
            momentaryStreamHead = new MidiStreamHead();

            momentaryStreamHead.Initialize();

            if (sm.loopRegion == Vector2.Zero) { mainSynced.loopRegionAsTicks = (0, 0); }
            else
            {
                mainSynced.loopRegionAsTicks =
                    (momentaryStreamHead.TimeToTick(sm.loopRegion.X), momentaryStreamHead.TimeToTick(sm.loopRegion.Y));
            }

            mainSynced.EmitSignal(SignalName.MusicChangeComplete);

            momentaryStreamHead.MidiSeek(0);
            momentaryStreamHead.ProcessMidi(0);
        }

        public override void _Ready()
        {
            base._Ready();
            mainSynced = this;
            ProcessPriority = PROCESS_PRIORITY;
        }

        public override void _Process(double delta)
        {
            if (currentMusic == null) { return; }
            HandleDuckMultiplier();
            HandleFadeMultiplier();
            HandleVolume();
            HandlePause();
            if (currentMusic is null or not SyncedMusic || !currentMusic.Playing) { return; }
            SyncedMusic sm = (SyncedMusic)currentMusic;
            Calculate:
            float playbackPos = sm.GetPlaybackPosition();
            float goalPlaybackPos = (Blastula.FrameCounter.stageFrame - startFrame) / (float)Blastula.VirtualVariables.Persistent.SIMULATED_FPS;
            
            ulong momentaryTargetTick = momentaryStreamHead.SolveTargetTick(Mathf.Max(0, goalPlaybackPos + MOMENTARY_LOOKAHEAD));
            momentaryStreamHead.ProcessMidi(momentaryTargetTick);
            ulong lastCurrentMeasure = momentaryStreamHead.GetCurrentMeasure();
            if (sm.loopRegion == Vector2.Zero || momentaryTargetTick < loopRegionAsTicks.Item2)
            {
                momentaryStreamHead.CalculateBeatAndMeasure();
            }

            // Attempted correction if the playback position is intolerably different
            if (Mathf.Abs(goalPlaybackPos - playbackPos) > TOLERANCE)
            {
                Seek(Mathf.Max(0f, 0.5f * (playbackPos + goalPlaybackPos)));
            }

            if (sm.loopRegion != Vector2.Zero)
            {
                // Loop the music
                if (goalPlaybackPos >= sm.loopRegion.Y)
                {
                    startFrame += (ulong)Math.Round((sm.loopRegion.Y - sm.loopRegion.X) * Blastula.VirtualVariables.Persistent.SIMULATED_FPS);
                    Seek(playbackPos - sm.loopRegion.Y + sm.loopRegion.X);
                    momentaryStreamHead.MidiSeek(goalPlaybackPos - sm.loopRegion.Y + sm.loopRegion.X);
                    goto Calculate;
                }
            }
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            if (mainSynced == this) { mainSynced = null; }
        }
    }
}
