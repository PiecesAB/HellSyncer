using Blastula.Sounds;
using Blastula.VirtualVariables;
using Godot;
using System;

namespace HellSyncer
{
    /// <summary>
    /// Metronome emit a signal when measures and particular intervals happen.
    /// </summary>
    [GlobalClass]
    [Icon(Persistent.NODE_ICON_PATH + "/metronome.png")]
    public partial class Metronome : Node
    {
        [Export] public float intervalQuarterNotes = 1f;
        [ExportGroup("Debug")]
        [Export] public string debugMeasureSound = "";
        [Export] public string debugBeatSound = "";

        public const int PROCESS_PRIORITY = AudioStreamSynced.PROCESS_PRIORITY + 1;

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
            if (AudioStreamSynced.main == null) { return; }
            if (Session.main.paused || Blastula.Debug.GameFlow.frozen) { return; }
            (ulong currMeasure, float currBeat) = AudioStreamSynced.main.GetBeatAndMeasure();
            if (currMeasure != lastMeasure)
            {
                EmitSignal(SignalName.OnMeasure, currMeasure);
                EmitSignal(SignalName.OnInterval, currMeasure, 0);
                if (debugMeasureSound != null && debugMeasureSound != "")
                {
                    CommonSFXManager.PlayByName(debugMeasureSound, 1, 1, default, true);
                }
                targetBeat = intervalQuarterNotes;
            }
            else if (currBeat >= targetBeat)
            {
                EmitSignal(SignalName.OnInterval, currMeasure, targetBeat);
                if (debugBeatSound != null && debugBeatSound != "")
                {
                    CommonSFXManager.PlayByName(debugBeatSound, 1, 1, default, true);
                }
                targetBeat += intervalQuarterNotes;
            }
            (lastMeasure, lastBeat) = (currMeasure, currBeat);
        }
    }
}

