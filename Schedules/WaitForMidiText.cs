using Blastula.VirtualVariables;
using Godot;
using HellSyncer;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Blastula.Schedules
{
    /// <summary>
    /// Waits for a MIDI text event to occur, as notified by a MidiTextReader.
    /// </summary>
    [GlobalClass]
    [Icon(HellSyncer.Persistent.NODE_ICON_PATH + "/trebleClock.png")]
    public partial class WaitForMidiText : BaseSchedule
    {
        /// <summary>
        /// Respond to when this MidiTextReader encounters a matching text event.
        /// </summary>
        [Export] public MidiTextReader textReader;
        /// <summary>
        /// If true, text events will build up while we're waiting elsewhere.
        /// This causes no wait to occur when new events are available.
        /// </summary>
        [Export] public bool buildup = true;
        /// <summary>
        /// If not empty, the text will be set locally in this variable name.
        /// </summary>
        [Export] public string textVarName = "";

        private Queue<string> textQueue = new Queue<string>();

        private bool receptive = true;

        public override async Task Execute(IVariableContainer source)
        {
            if (source != null) { ExpressionSolver.currentLocalContainer = source; }

            if (!buildup) { textQueue.Clear(); receptive = true; }

            await this.WaitUntil(() => textQueue.Count > 0);

            string text = textQueue.Dequeue();

            if (textVarName != null && textVarName != "")
            {
                source.SetVar(textVarName, text);
            }

            if (!buildup) { receptive = false; }
        }

        /// <summary>
        /// Recieves text signal from MidiTextReader.
        /// </summary>
        public void OnText(string text)
        {
            if (!receptive) { return; }
            textQueue.Enqueue(text);
        }

        public override void _Ready()
        {
            base._Ready();
            receptive = buildup;
            if (textReader != null)
            {
                textReader.OnText += OnText;
            }
        }

        public override void _ExitTree()
        {
            base._ExitTree();
            if (textReader != null)
            {
                textReader.OnText -= OnText;
            }
        }
    }
}
