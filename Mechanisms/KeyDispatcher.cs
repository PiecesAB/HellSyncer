using Godot;
using System;

namespace HellSyncer.Mechanisms
{
    /// <summary>
    /// Helps to efficiently delegate responses for a keyed visualizer,
    /// without each key having to listen on the Instrument.
    /// This will auto-connect with KeyResponder objects.
    /// </summary>
    public partial class KeyDispatcher : Node
    {
        /// <summary>
        /// I will listen to this instrument for note events.
        /// </summary>
        [Export] public Instrument instrument;

        [Signal] public delegate void OnNoteEventHandler(int midiTone, int velocity, float duration);

        public delegate void OnNotifyKey(int midiTone, int velocity, float duration);

        public OnNotifyKey[] responders = new OnNotifyKey[128];

        public override void _Ready()
        {
            Connect(SignalName.OnNote, new Callable(this, MethodName.Dispatch));
        }

        public void Dispatch(int midiTone, int velocity, float duration)
        {
            if (midiTone < 0 || midiTone >= 128) { return; }
            responders[midiTone]?.Invoke(midiTone, velocity, duration);
        }
    }
}
