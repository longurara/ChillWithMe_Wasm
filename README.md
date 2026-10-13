# ChillWithMe Wasm

A work-in-progress browser port of **Chill with You: Lo-Fi Story**. Spend time with Satone, listen to music, and use focus tools in a cozy 3D room.

## Features

- Original room, character animations, and game interface.
- Story episodes with dialogue, voices, and choices.
- Music playlists, ambient sounds, and personal music imports.
- Pomodoro and stopwatch timers.
- Notes, task lists, calendar, diary, habits, and work statistics.
- Room decorations, outfits, and local profiles.
- Local saves with backups, JSON import/export, and Windows save import.

## Current Status

Version **1.24.0** has passed basic story, timer, and input checks on Chrome, Edge, and Firefox. The port is unfinished: visuals, physics, and some behavior still differ from the Windows game. Cloud saves are deferred. Mobile and Safari support have not been fully verified.

## Run Locally

**This repository contains source code, not a ready-to-play game.** Game assets and generated builds are excluded. You need your own copy of the Windows game to prepare a build.

If you already have the prepared build, run this from the project folder:

```powershell
powershell -ExecutionPolicy Bypass -File .\start-preview.ps1
```

Open [http://127.0.0.1:8093/](http://127.0.0.1:8093/) in Chrome, Edge, or Firefox. Keep the server running while playing.

## Build from Source

You need **Unity 2022.3.62f2** with an activated license and WebGL Build Support, plus Python 3.12 and Node.js.

From the repository folder:

```powershell
python -m pip install -r requirements.txt
node src/graphics/recover-project.mjs "<Windows game folder>"
python src/graphics/prepare_project.py
python src/graphics/asset_pipeline.py
powershell -ExecutionPolicy Bypass -File .\build.ps1
```

Close the project's Unity Editor before building. For later builds, run only `build.ps1`.

## Saves

Progress stays in your browser. Use **Settings → General** to export a backup or import a Windows save folder/ZIP. Only one tab can write at a time. Imported music is stored separately and is not included in the save file.

## More Details

- [Build tools](src/graphics/README.md)
- [Implemented features and remaining differences](research/source-parity-audit.md)
- [Completion plan and verification limits](research/parity-95.md)
