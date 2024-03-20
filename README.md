# HellSyncer

HellSyncer is a dependent add-on for [Blastula](https://github.com/PiecesAB/Blastula) that can synchronize the background music with bullet patterns or other game events of your choosing.

It achieves this by using a MIDI file and playing the music file to match its time with the MIDI. This way, timing remains deterministic despite any audio or game lag, as is important for precision games as danmaku, and potentially allows replays to be consistent.
However, an artifact of this choice is that it intentionally stutters or skips the music when it's too far off, so if the game lags too much, the music will sound terrible.

If a matching MIDI file of the music is not available, you can generate a simple one with a tempo and time signature. But with a more comprehensive MIDI file, there is support for time signature and tempo changes in the middle of the track, and syncing to instrument melodies or text cues. Many common DAWs support exporting MIDI for portability, and you can edit MIDIs with freely-available software. The software I use is [Sekaiju](https://openmidiproject.opal.ne.jp/Sekaiju_en.html).

## Setup

1. Setup a Godot project that can load the [Blastula framework](https://github.com/PiecesAB/Blastula), following the instructions there.
2. git clone or copy this repository folder into the addons folder of the project. Ensure the capitalization is correct: "HellSyncer"
3. Enable the plugin in Godot's Project Settings.
4. That should be all.

More information will be found [on the wiki.](https://piecesab.github.io/hellsyncer)
