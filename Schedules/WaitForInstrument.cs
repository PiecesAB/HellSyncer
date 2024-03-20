using Blastula.VirtualVariables;
using Godot;
using HellSyncer;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Blastula.Schedules
{
    /// <summary>
    /// Waits for a note to be played.
    /// </summary>
    [GlobalClass]
    [Icon(HellSyncer.Persistent.NODE_ICON_PATH + "/trebleClock.png")]
    public partial class WaitForInstrument : BaseSchedule
    {
        [Export] public Instrument instrument;
        /// <summary>
        /// If true, notes will build up while we're waiting elsewhere.
        /// This causes no wait to occur when new notes are available.
        /// Important for polyphony when this is the only wait in the schedule loop.
        /// </summary>
        [Export] public bool buildup = true;
        /// <summary>
        /// The note's MIDI tone, which is an integer from 0-127, will be set locally in this variable name.
        /// Middle C (C4) is 60, and each integer corresponds to one semitone.
        /// </summary>
        [Export] public string toneVarName = "";
        /// <summary>
        /// The note's MIDI velocity, which is an integer from 0-127, will be set locally in this variable name.
        /// 1 is as quiet as possible, and 127 is as loud as possible.
        /// </summary>
        [Export] public string velocityVarName = "";
        /// <summary>
        /// The duration of the note in seconds will be set locally in this variable name.
        /// </summary>
        [Export] public string durationVarName = "";

        private Queue<NoteInfo> textQueue = new Queue<NoteInfo>();
        private struct NoteInfo
        {
            public int midiTone;
            public int velocity;
            public float duration;
        }

        private bool receptive = true;

        public override async Task Execute(IVariableContainer source)
        {
            if (source != null) { ExpressionSolver.currentLocalContainer = source; }

            if (!buildup) { textQueue.Clear(); receptive = true; }

            await this.WaitUntil(() => textQueue.Count > 0);

            NoteInfo noteInfo = textQueue.Dequeue();

            if (toneVarName != null && toneVarName != "")
            {
                source.SetVar(toneVarName, noteInfo.midiTone);
            }

            if (velocityVarName != null && velocityVarName != "")
            {
                source.SetVar(velocityVarName, noteInfo.velocity);
            }

            if (durationVarName != null && durationVarName != "")
            {
                source.SetVar(durationVarName, noteInfo.duration);
            }

            if (!buildup) { receptive = false; }
        }

        public void OnNote(int midiTone, int velocity, float duration)
        {
            if (!receptive) { return; }
            textQueue.Enqueue(new NoteInfo { midiTone = midiTone, velocity = velocity, duration = duration });
        }

        public override void _Ready()
        {
            base._Ready();
            receptive = buildup;
            if (instrument != null)
            {
                instrument.OnNote += OnNote;
            }
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            if (instrument != null)
            {
                instrument.OnNote -= OnNote;
            }
        }
    }
}
