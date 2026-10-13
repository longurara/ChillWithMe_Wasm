# WebGL 1.23.0: original startup screen

The penguin, phone, halo and NESTOPI screen now appears as soon as the page opens and stays visible while WebAssembly and room data load. It uses the recovered Boot assets and the original loading, finish and fade keyframes. No video or replacement artwork is used.

Before Unity is available, an HTML Canvas2D renderer draws Boot's rectangle hierarchy, source image visibility, scales and animation curves. `prepare_loading_screen.py` exports both PC and portrait layouts plus seven original PNGs. Assets and the generated manifest remain excluded from Git; `finish_preview.py` regenerates them from the local recovered game.

The runtime releases startup after story data, the asset catalog, settings and saved-room restoration are ready. It then waits for the browser's finish/fade completion callback before enabling autonomous reactions or greetings. Resolving `createUnityInstance` alone does not dismiss loading. The native two-second minimum, 1.5-second loop, 1.333-second finish and 0.567-second fade are retained. `SplashFinish` waits for the current loop to end, as its controller transition specifies, keeping the phone's pose continuous. The HTML layer blocks room input until completion. Failed engine/data requests show a retry button.

Verification:

- [Source extraction](loading-screen-source-check.json): original pixels, two layouts and all mapped curves from the three startup clips.
- [Callback gate](startup-gate-check.json): rejects premature completion, waits for animation, handles repeated callbacks and blocks failed initialization.
- [Real browser startup](browser-startup-check.json): delayed story/catalog requests, actual finish/fade into the room, usable settings, an HTTP 503 fixture and successful retry. Portrait composition is also inspected with a mock engine before Unity loads.
- The full existing Unity checks still pass. [Browser physics/input regression](browser-physics-check.json) covers desktop, landscape and Chrome touch emulation after startup.

This adapts the normal Boot flow for standalone web loading. The chapter-specific native error animation and game-end loading direction are not connected by this change. Physical phones and Safari remain unverified; this is not full Windows visual parity.
