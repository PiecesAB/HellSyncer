using Godot;
using System;

namespace HellSyncer.Midi
{
    public enum EventID {
        Unsupported = 0x0000,
        NoteOff = 0x8000,
        NoteOn = 0x9000,
        KeyPressure = 0xA000,
        ControlChange = 0xB000,
        ProgramChange = 0xC000,
        ChannelPressure = 0xD000,
        PitchBend = 0xE000,
        SysExF0 = 0xF000,
        SysExF7 = 0xF700,
        Text = 0xFF01,
        Copyright = 0xFF02,
        TrackName = 0xFF03,
        InstrumentName = 0xFF04,
        Lyric = 0xFF05,
        Marker = 0xFF06,
        Cue = 0xFF07,
        EndOfTrack = 0xFF2F,
        Tempo = 0xFF51,
        TimeSignature = 0xFF58,
        KeySignature = 0xFF59,
    }

    public enum TextEventID
    {
        Any = 0xFF00,
        Text = 0xFF01,
        Copyright = 0xFF02,
        TrackName = 0xFF03,
        InstrumentName = 0xFF04,
        Lyric = 0xFF05,
        Marker = 0xFF06,
        Cue = 0xFF07,
    }
}
