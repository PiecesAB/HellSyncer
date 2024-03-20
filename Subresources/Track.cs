using Godot;
using System;

namespace HellSyncer.Midi
{
    public partial class Track : Resource
    {
        [Export()]
        public TrackEvent[] trackEvents;
        [Export()]
        public FullNoteInfo[] fullNoteInfos;

        public Track() { }
        public Track(TrackEvent[] trackEvents, FullNoteInfo[] fullNoteInfos)
        {
            this.trackEvents = trackEvents;
            this.fullNoteInfos = fullNoteInfos;
        }

        /// <summary>
        /// Binary search internal helper.
        /// startIndex, endIndex are included in the search interval.
        /// </summary>
        private int GetFullNoteIndexAtTick(ulong tick, int startIndex, int endIndex)
        {
            if (startIndex == endIndex) 
            {
                ulong startTick = fullNoteInfos[startIndex].tick;
                if (tick <= startTick) 
                { 
                    // The startTick has not yet elapsed; this is the desired index.
                    return startIndex; 
                }
                else
                {
                    // The startTick has elapsed; all events have passed.
                    return startIndex + 1;
                }
            }
            int midIndex = startIndex + (endIndex - startIndex) / 2;
            ulong midTick = fullNoteInfos[midIndex].tick;
            if (tick <= midTick)
            {
                // The midTick has not yet elapsed; the earlier half contains the desired index.
                // Note how midIndex could be what we're looking for, so include it.
                return GetFullNoteIndexAtTick(tick, startIndex, midIndex);
            }
            else
            {
                // The midTick has elapsed; the later half contains the desired index.
                return GetFullNoteIndexAtTick(tick, midIndex + 1, endIndex);
            }
        }

        /// <summary>
        /// Return the earliest event index that begins on or after the tick (has not yet occurred).
        /// If no such event exists, it will return trackEvents.Length (representing the end).
        /// </summary>
        public int GetFullNoteIndexAtTick(ulong tick)
        {
            if (fullNoteInfos.Length == 0) { return 0; }
            return GetFullNoteIndexAtTick(tick, 0, fullNoteInfos.Length - 1);
        }
    }
}
