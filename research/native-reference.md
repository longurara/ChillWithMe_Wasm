# Native physics reference

The optional Windows recorder reads MagicaClothV2.dll from your own PC game. It runs only the recovered rig, Animator and source cloth settings in an isolated player. It does not start the game, access player saves, copy the plugin or include it in WebGL.

After preparing the project, close Unity and run:

```powershell
.\capture-native-physics.ps1 -PCManaged '<game folder>\Chill With You_Data\Managed'
.\build.ps1
```

The recorder simulates at 60 Hz and stores 30 Hz rotation samples. Looping motions settle for two cycles before recording. Eighteen motions cover the desk bases and transitions, window opening/closing, common break bases, stretches and one story base. WebGL checks the profile, rig and state before blending to a recording; other motions and custom garment rigs use the CPU fallback.

The generated recordings and local reference player are excluded from Git. Without a recording, the build uses the web CPU bake. Editor capture is unsuitable because the PC plugin was compiled without Unity Editor native-container safety fields; use the Windows player.

The browser ships a binary float32 recording rather than the reference JSON. The current dataset shrinks from 34,294,345 bytes to 5,647,160 bytes while reproducing all tested reference rotations. A 0.25-second eased blend reduces the measured first return-to-live step from 15.215° to 1.387° across the eighteen motions. These are Editor measurements; visual matching is a separate gate.

Matching recorded rotations is a data check. It does not prove matching PC pixels, cloth translation, self-collision or every outfit.

The vendor explicitly excludes WebGL because Burst compilation is unavailable: [MagicaCloth2 platform limitations](https://magicasoft.jp/en/mc2_about/). The reference recordings and CPU fallback are adaptations; loading the PC DLL directly into a WebGL player is not a supported route to parity.
