using Blastula.Sounds;
using Blastula.VirtualVariables;
using Godot;
using System;

namespace HellSyncer
{
    /// <summary>
    /// Monitor to filter and emit signals based on the MIDI's rhythm.
    /// This emits two kinds of signals: one for when the measure begins,
    /// and one for when an "interval" in that measure begins, which has the duration of a customizable number of beats.
    /// </summary>
    /// <remarks>
    /// Naturally, AudioStreamSynced.main must exist and be currently playing for this to emit any signals.
    /// </remarks>
    [GlobalClass]
    [Icon(Persistent.NODE_ICON_PATH + "/metronome.png")]
    public partial class Metronome : Node
    {
        /// <summary>
        /// The length of an interval, which is a number of beats (denominator of time signature).
        /// Usually the denominator is a quarter note, so the examples assume the beat is one quarter note.
        /// </summary>
        /// <example>0.25: an interval is one sixteenth note long.</example>
        /// <example>2: an interval is one half note long.</example>
        /// <example>1.5: an interval is one dotted quarter note long. (Note: a new interval begins every measure, so depending on time signature, the last measure's final interval may be cut short.)</example>
        /// <example>0.333: an interval is one triplet eighth note long.</example>
        /// <remarks>
        /// If this is extremely small (think 64th notes), it's possible that an interval is shorter than one frame.
        /// When this happens, only one OnInterval signal is sent per frame, and the metronome will lag behind.
        /// So avoid extremely small intervals if you can.
        /// </remarks>
        [Export] public float intervalBeatCount = 1f;
        /// <summary>
        /// If true, the beat will be forced as a quarter note, even when the time signature disagrees.
        /// </summary>
        [Export] public bool forceQuarterNoteBeat = false;
        /// <summary>
        /// Sound ID to play when a measure begins. Useful for testing a rhythm.
        /// </summary>
        [ExportGroup("Debug")]
        [Export] public string debugMeasureSound = "";
        /// <summary>
        /// Sound ID to play when an interval begins. Useful for testing a rhythm.
        /// </summary>
        [Export] public string debugBeatSound = "";

        public const int PROCESS_PRIORITY = SyncedMusicManager.PROCESS_PRIORITY + 1;

        private ulong lastMeasure = ulong.MaxValue;
        private float lastBeat = -1f;
        private float targetBeat = 0f;

        [Signal] public delegate void OnMeasureEventHandler(ulong measureNumber);
        [Signal] public delegate void OnIntervalEventHandler(ulong measureNumber, float beat);

        public override void _Ready()
        {
            _Process(0);
        }

        public override void _Process(double delta)
        {
            if (SyncedMusicManager.main == null) { return; }
            if (Session.main.paused || Blastula.Debug.GameFlow.frozen) { return; }
            if (SyncedMusicManager.momentaryStreamHead == null) { return; }
            (ulong currMeasure, float currBeat) = SyncedMusicManager.momentaryStreamHead.GetBeatAndMeasure(forceQuarterNoteBeat);
            if (currMeasure != lastMeasure)
            {
                EmitSignal(SignalName.OnMeasure, currMeasure);
                EmitSignal(SignalName.OnInterval, currMeasure, 0);
                //GD.Print(currMeasure + " " + currBeat);
                if (debugMeasureSound != null && debugMeasureSound != "")
                {
                    CommonSFXManager.PlayByName(debugMeasureSound, 1, 1, default, true);
                }
                targetBeat = intervalBeatCount;
            }
            else if (currBeat >= targetBeat)
            {
                EmitSignal(SignalName.OnInterval, currMeasure, targetBeat);
                //GD.Print(currMeasure + " " + currBeat);
                if (debugBeatSound != null && debugBeatSound != "")
                {
                    CommonSFXManager.PlayByName(debugBeatSound, 1, 1, default, true);
                }
                targetBeat += intervalBeatCount;
            }
            (lastMeasure, lastBeat) = (currMeasure, currBeat);
        }
    }
}

