using Blastula.VirtualVariables;
using Godot;
using HellSyncer;
using System.Threading.Tasks;

namespace Blastula.Schedules
{
    /// <summary>
    /// Waits a number of metronome intervals.
    /// </summary>
    [GlobalClass]
    [Icon(HellSyncer.Persistent.NODE_ICON_PATH + "/trebleClock.png")]
    public partial class WaitForMetronome : BaseSchedule
    {
        public enum Mode { Intervals, Measures }
        [Export] public Mode mode = Mode.Intervals;
        [Export] public Metronome metronome;
        [Export] public string count = "1";

        private ulong measureCounter = 0;
        private ulong intervalCounter = 0;

        public override async Task Execute(IVariableContainer source)
        {
            if (source != null) { ExpressionSolver.currentLocalContainer = source; }
            int count = Solve("count").AsInt32();
            count = Mathf.Max(1, count);
            if (metronome == null)
            {
                switch (mode)
                {
                    case Mode.Intervals: await this.WaitSeconds(0.5f * count); break;
                    case Mode.Measures: await this.WaitSeconds(2f * count); break;
                }
            }
            else
            {
                ulong target = 0;
                switch (mode)
                {
                    case Mode.Intervals:
                        target = intervalCounter + (ulong)count;
                        await this.WaitUntil(() => intervalCounter == target);
                        break;
                    case Mode.Measures:
                        target = measureCounter + (ulong)count;
                        await this.WaitUntil(() => measureCounter == target);
                        break;
                }
            }
        }

        public void OnMeasure(ulong measure)
        {
            ++measureCounter;
        }

        public void OnInterval(ulong measure, float beat) 
        {
            ++intervalCounter;
        }

        public override void _Ready()
        {
            base._Ready();
            if (metronome != null)
            {
                metronome.OnMeasure += OnMeasure;
                metronome.OnInterval += OnInterval;
            }
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            if (metronome != null)
            {
                metronome.OnMeasure -= OnMeasure;
                metronome.OnInterval -= OnInterval;
            }
        }
    }
}
