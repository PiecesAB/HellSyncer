#if TOOLS
using Godot;
using System;
using HellSyncer.Midi;

[Tool]
public partial class HellSyncerPlugin : EditorPlugin
{
    MidiImporter midiImporter;
    public override void _EnterTree()
    {
        midiImporter = new MidiImporter();
        AddImportPlugin(midiImporter);
    }

    public override void _ExitTree()
    {
        RemoveImportPlugin(midiImporter);
        midiImporter = null;
    }
}
#endif
