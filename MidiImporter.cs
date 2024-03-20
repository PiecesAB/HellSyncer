using Godot;
using Godot.Collections;
using System;

namespace HellSyncer
{
    public partial class MidiImporter : EditorImportPlugin
    {
        public override float _GetPriority() { return 2f; }
        public override int _GetImportOrder() { return -1; }
        public override string _GetImporterName() { return "hellsyncer.midi"; }
        public override string _GetVisibleName() { return "Midi"; }
        public override string[] _GetRecognizedExtensions() { return new string[] { "mid", "midi" }; }
        public override string _GetSaveExtension() { return "tres"; }
        public override string _GetResourceType() { return "Resource"; }
        public override int _GetPresetCount() { return 1; }
        public override string _GetPresetName(int presetIndex) { return "Default"; }

        public override Godot.Collections.Array<Godot.Collections.Dictionary> _GetImportOptions(string path, int presetIndex)
        {
            return new Godot.Collections.Array<Godot.Collections.Dictionary>()
            {
                new Godot.Collections.Dictionary()
                {
                    {"name", "trackRanges"},
                    {"default_value", new Vector2[0] },
                    {"hint_string", 
                        "Defines the ranges of MIDI tracks to import, other than Track 0, which is always imported.\n" +
                        "Each Vector2 is treated as a min-max range with the ends both included.\n" +
                        "So having one vector (1, 4) would import tracks 1, 2, 3, 4; having (8, 8) would import track 8." 
                    }
                }
            };
        }

        public override bool _GetOptionVisibility(string path, StringName optionName, Dictionary options)
        {
            return true;
        }

        public override Error _Import(string sourceFile, string savePath,
            Godot.Collections.Dictionary options,
            Godot.Collections.Array<string> platformVariants,
            Godot.Collections.Array<string> genFiles)
        {
            using var file = FileAccess.Open(sourceFile, FileAccess.ModeFlags.Read);
            if (file.GetError() != Error.Ok) { return Error.Failed; }
            var pmid = new ParsedMidi();

            pmid.ParseRaw(file.GetBuffer((long)file.GetLength()), options["trackRanges"].AsVector2Array());

            string filename = $"{savePath}.{_GetSaveExtension()}";
            return (Error)(int)ResourceSaver.Save(pmid, filename);
        }
    }
}
