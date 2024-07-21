using Godot;
using System;

namespace HellSyncer.Mechanisms
{
    /// <summary>
    /// Responds to a KeyDispatcher.
    /// It will automatically connect with a dispatcher upon creation.
    /// </summary>
    public partial class KeyResponder : Node
    {
        [Export] public KeyDispatcher dispatcher;
        /// <summary>
        /// Which MIDI tones do I respond to?
        /// </summary>
        /// <remarks>
        /// Warning: it is assumed that the list will only change via the ChangeTones method.
        /// </remarks>
        [Export] public int[] myTones = new int[] { 69 };

        public void ChangeTones(int[] newTones)
        {
            foreach (int tone in myTones) { dispatcher.responders[tone] -= OnNote; }
            myTones = (int[])newTones.Clone();
            foreach (int tone in myTones) { dispatcher.responders[tone] += OnNote; }
        }

        public override void _Ready()
        {
            foreach (int tone in myTones) { dispatcher.responders[tone] += OnNote; }
        }

        public override void _ExitTree()
        {
            foreach (int tone in myTones) { dispatcher.responders[tone] -= OnNote; }
        }

        public virtual void OnNote(int midiTone, int velocity, float duration)
        {

        }
    }
}
