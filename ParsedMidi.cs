using Godot;
using Godot.Collections;
using System.Collections.Generic;
using System;
using System.Text;
using HellSyncer.Midi;

namespace HellSyncer
{
    public partial class ParsedMidi : Resource
    {
        [Export()]
        public Track[] tracks;

        [Export()]
        public ushort ticksPerQN;

        private byte[] raw;
        private ushort trackCount;
        private byte holdoverEventID;
        private ulong trackTickPos = 0;
        private uint readHead = 0;

        #region Basic Readers
        public byte ReadByte()
        {
            byte ret = raw[readHead];
            readHead += 1;
            return ret;
        }

        public (byte, byte) ReadTwoBytes()
        {
            byte retA = raw[readHead];
            byte retB = raw[readHead + 1];
            readHead += 2;
            return (retA, retB);
        }

        public byte[] ReadNBytes(uint n)
        {
            byte[] ret = new byte[n];
            for (int i = 0; i < n; ++i) { ret[i] = raw[readHead + i]; }
            readHead += n;
            return ret;
        }

        public ushort ReadShort()
        {
            ushort ret = (ushort)(
                (raw[readHead] * 0x100)
                | (raw[readHead + 1])
            );
            readHead += 2;
            return ret;
        }

        public uint ReadInt()
        {
            uint ret = (uint)(
                (raw[readHead] * 0x1000000)
                | (raw[readHead + 1] * 0x10000)
                | (raw[readHead + 2] * 0x100)
                | (raw[readHead + 3])
            );
            readHead += 4;
            return ret;
        }

        public uint ReadIntVarLen()
        {
            byte curr = 0;
            uint ret = 0;
            do
            {
                curr = raw[readHead];
                ret <<= 7;
                ret |= (uint)(curr & 0x7F);
                readHead++;
            }
            while ((curr & 0x80) != 0);
            return ret;
        }

        public string ReadString()
        {
            byte len = ReadByte();
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < len; ++i) { sb.Append((char)ReadByte()); }
            return sb.ToString();
        }

        #endregion

        public void ReadHeader()
        {
            ushort format = ReadShort();
            if (format != 1) { throw new FormatException("Sorry, I only understand MIDI format 1."); }
            trackCount = ReadShort();
            ticksPerQN = ReadShort();
            if (ticksPerQN >= 0x8000) { throw new FormatException("Sorry, I don't understand SMPTE timing."); }
        }

        public TrackEvent ReadTrackEvent()
        {
            TrackEvent ret = null;
            uint tickDelta = ReadIntVarLen();
            trackTickPos += tickDelta;
            byte ID = ReadByte();

        Identify:

            byte channel = (byte)(ID & 0xF);
            int highID = ID >> 4;
            switch (highID)
            {
                case 0x8:
                    {
                        (byte n, byte v) = ReadTwoBytes();
                        ret = new NoteOffEvent(trackTickPos, channel, n, v);
                    }
                    break;
                case 0x9:
                    {
                        (byte n, byte v) = ReadTwoBytes();
                        ret = new NoteOnEvent(trackTickPos, channel, n, v);
                    }
                    break;
                case 0xA:
                    {
                        (byte n, byte v) = ReadTwoBytes();
                        ret = new KeyPressureEvent(trackTickPos, channel, n, v);
                    }
                    break;
                case 0xB:
                    {
                        (byte n, byte v) = ReadTwoBytes();
                        ret = new ControlChangeEvent(trackTickPos, channel, n, v);
                    }
                    break;
                case 0xC:
                    ret = new ProgramChangeEvent(trackTickPos, channel, ReadByte());
                    break;
                case 0xD:
                    ret = new ChannelPressureEvent(trackTickPos, channel, ReadByte());
                    break;
                case 0xE:
                    {
                        (byte low7, byte high7) = ReadTwoBytes();
                        short bend = (short)((high7 << 7) + low7 - 0x2000);
                        ret = new PitchBendEvent(trackTickPos, channel, bend);
                    }
                    break;
                case 0xF:
                    switch (channel) // not actually channel. abuse of notation
                    {
                        case 0x0:
                            {
                                SysExF0Event f0 = new SysExF0Event(trackTickPos);
                                byte dataLength = ReadByte();
                                f0.data = new byte[dataLength];
                                for (int i = 0; i < dataLength; ++i) { f0.data[i] = ReadByte(); }
                                ret = f0;
                            }
                            break;
                        case 0x7:
                            {
                                SysExF0Event f7 = new SysExF0Event(trackTickPos);
                                byte dataLength = ReadByte();
                                f7.data = new byte[dataLength];
                                for (int i = 0; i < dataLength; ++i) { f7.data[i] = ReadByte(); }
                                ret = f7;
                            }
                            break;
                        case 0xF:
                            // meta-event
                            byte metaID = ReadByte();
                            switch (metaID)
                            {
                                case 0x01:
                                    ret = new TextEvent(trackTickPos, ReadString());
                                    break;
                                case 0x02:
                                    ret = new CopyrightEvent(trackTickPos, ReadString());
                                    break;
                                case 0x03:
                                    ret = new TrackNameEvent(trackTickPos, ReadString());
                                    break;
                                case 0x04:
                                    ret = new InstrumentNameEvent(trackTickPos, ReadString());
                                    break;
                                case 0x05:
                                    ret = new LyricEvent(trackTickPos, ReadString());
                                    break;
                                case 0x06:
                                    ret = new MarkerEvent(trackTickPos, ReadString());
                                    break;
                                case 0x07:
                                    ret = new CueEvent(trackTickPos, ReadString());
                                    break;
                                case 0x2F:
                                    ret = new EndOfTrackEvent(trackTickPos);
                                    ReadByte(); // length of this event is always 0
                                    break;
                                case 0x51:
                                    ret = new TempoEvent(trackTickPos, ReadInt() & 0x00FFFFFF);
                                    break;
                                case 0x58:
                                    {
                                        byte[] info = ReadNBytes(5);
                                        // info[0] is the length of the following data; always 4
                                        byte num = info[1];
                                        byte denom = (byte)(1 << info[2]);
                                        // we aren't using info[3] nor info[4]. they are metronome info
                                        ret = new TimeSignatureEvent(trackTickPos, num, denom);
                                    }
                                    break;
                                case 0x59:
                                    {
                                        byte[] info = ReadNBytes(3);
                                        // info[0] is the length of the following data; always 2
                                        sbyte sf = (sbyte)(info[1] >= 0x80 ? (info[1] - 0x100) : info[1]);
                                        bool minor = info[2] == 0x1;
                                        ret = new KeySignatureEvent(trackTickPos, sf, minor);
                                    }
                                    break;
                                default:
                                    ret = new UnsupportedEvent(trackTickPos);
                                    ReadString(); // events that get here contain a string; throw it away
                                    break;
                            }
                            break;
                        default:
                            // events that get here contain no data.
                            ret = new UnsupportedEvent(trackTickPos);
                            break;
                    }
                    break;
                default:
                    int highHoldoverID = holdoverEventID >> 4;
                    if (highHoldoverID >= 0x8 && highHoldoverID <= 0xE)
                    {
                        ID = holdoverEventID;
                        readHead -= 1;
                        goto Identify;
                    }
                    throw new FormatException($"Hey! That's not a MIDI event! (byte {readHead - 1} = {ID})");
            }

            holdoverEventID = ID;
            return ret;
        }

        public class SortFullNotesByStart : IComparer<FullNoteInfo>
        {
            public int Compare(FullNoteInfo x, FullNoteInfo y)
            {
                return x.tick.CompareTo(y.tick);
            }
        }

        public Track ReadTrack(bool skip)
        {
            ReadInt(); // MTrk
            uint trackLenBytes = ReadInt();
            uint endOfTrackByte = readHead + trackLenBytes;

            if (skip)
            {
                readHead = endOfTrackByte;
                return new Track(
                    new TrackEvent[1] { new EndOfTrackEvent(0) },
                    new FullNoteInfo[0]
                );
            }

            List<TrackEvent> eventsTemp = new List<TrackEvent>();
            trackTickPos = 0;
            do { eventsTemp.Add(ReadTrackEvent()); }
            while (readHead < endOfTrackByte);

            // correspond NoteOn with NoteOff to create full note info
            List<FullNoteInfo> fullNotesTemp = new List<FullNoteInfo>();
            List<NoteOnEvent> notesPlaying = new List<NoteOnEvent>();
            for (int i = 0; i < eventsTemp.Count; ++i)
            {
                if (eventsTemp[i] is NoteOnEvent && ((NoteOnEvent)eventsTemp[i]).velocity > 0)
                {
                    notesPlaying.Add((NoteOnEvent)eventsTemp[i]);
                }
                else if (eventsTemp[i] is NoteOffEvent
                    || (eventsTemp[i] is NoteOnEvent && ((NoteOnEvent)eventsTemp[i]).velocity == 0))
                {
                    PVEvent pv = (PVEvent)eventsTemp[i];
                    bool closed = false;
                    for (int j = 0; j < notesPlaying.Count; ++j)
                    {
                        if (notesPlaying[j].channel == pv.channel && notesPlaying[j].note == pv.note)
                        {
                            fullNotesTemp.Add(
                                new FullNoteInfo(notesPlaying[j], pv.tick - notesPlaying[j].tick)
                            );
                            notesPlaying.RemoveAt(j);
                            closed = true;
                            break;
                        }
                    }
                    if (!closed)
                    {
                        //GD.PushWarning("Couldn't find a NoteOn to suit a NoteOff. The parser may be erroneous, or the MIDI is malformed.");
                    }
                }
            }
            for (int j = 0; j < notesPlaying.Count; ++j)
            {
                fullNotesTemp.Add(
                    new FullNoteInfo(notesPlaying[j], ticksPerQN)
                );
                //GD.PushWarning("Couldn't find a NoteOff to suit a NoteOn. The parser may be erroneous, or the MIDI is malformed.");
            }
            fullNotesTemp.Sort(new SortFullNotesByStart());
            //GD.Print(fullNotesTemp.Count);

            return new Track(eventsTemp.ToArray(), fullNotesTemp.ToArray());
        }

        private bool TrackIsInRange(int trackId, ref Vector2[] trackRanges)
        {
            if (trackId == 0) { return true; }
            foreach (Vector2 trackRange in trackRanges)
            {
                if (trackRange.X <= trackId && trackId <= trackRange.Y)
                {
                    return true;
                }
            }
            return false;
        }

        public void ParseRaw(byte[] raw, Vector2[] trackRanges)
        {
            this.raw = raw;
            readHead = 0;
            ReadInt(); // MThd
            ReadInt(); // header length, but it's always 6
            ReadHeader();
            tracks = new Track[trackCount];
            for (int i = 0; i < trackCount; ++i)
            {
                bool skip = !TrackIsInRange(i, ref trackRanges);
                tracks[i] = ReadTrack(skip);
            }
        }

        public void GenerateForBeat(float bpm, Vector2I timeSignature)
        {
            // only a control track.
            ticksPerQN = 360;
            TrackEvent[] events = new TrackEvent[3];
            events[0] = new TempoEvent(0, (ulong)Math.Round(1e6f * 60f / bpm));
            events[1] = new TimeSignatureEvent(0, (byte)timeSignature.X, (byte)timeSignature.Y);
            events[2] = new EndOfTrackEvent(0);
            tracks = new Track[1] { new Track(events, new FullNoteInfo[0]) };
        }
    }
}
