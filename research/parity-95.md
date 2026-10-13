# Desktop completion target

Reference: the local PC game 1.17.3. Target: Chrome, Edge, Firefox; gameplay and appearance; local PC-save conversion. Cloud, account, Steam services, Windows window management and physical mobile/Safari qualification are outside this release.

Completion requires at least 95% of the fixed acceptance checks in **each** domain. Critical gameplay, save integrity and reported regressions must all pass. Editor checks do not establish browser or PC visual parity.

## Work in this implementation

- Deferred Work/Break and desk transitions preserve a running activity and its props; latest desk request is committed at a safe boundary.
- Deterministic clock/random replay makes time-dependent behaviour reproducible.
- Habit creation dates, pause intervals, deleted history and calendar removal; v1-to-v2 migration and edit cancellation.
- Player-status reactions use the source delay/cooldown rather than playing immediately.
- Local PC save parser preserves numeric dictionary keys and 64-bit IDs; reads ES3, JSON, gzip, folder/ZIP inputs. Preview precedes replacement. Unmapped data is explicitly reported.
- PC music UUIDs, favorites, exclusions, ordering and file relinking are represented locally.
- Tutorial, episode-32 reconnection gates, completed timer rest, loading/credits/farewell directions and cancellation are connected to the source rules.
- Greetings use date boundaries, the hundredth login and persistent shuffled episode bags. Small-talk prerequisites, lottery and purchased-environment conversations use source conditions.
- Birthday/April Fools conversations wait for interaction; Christmas has its opening and temporary props. New Year begins preparation at 23:59:40, waits for the desk base, then starts at :50.
- Native Magica reference runner operates in an isolated Windows player. Its DLL is read from the user's game and is not redistributed. Eighteen motions have been captured, including the reported window poses and desk transitions. Recordings settle loops, preserve loop seams and transfer the visible pose to live particles. Browser visual matching still requires verification.
- COZY atmosphere gradients, sun orbit and time inputs are extracted from the source, replacing hand-picked lighting values. Renderer comparison is still required.
- Save v2 keeps verified backups, migration snapshots and a browser-managed writer lock. PC conversion was checked read-only against 31 local source files, without unmapped-record warnings for that fixture. Other source fixtures still need qualification.
- Storage denial allows read-only startup; navigation flushes the save before releasing the writer lock and cached-page restoration reacquires it. Imported saves now reapply room appearance. Local penguin-click progress follows the source five-click rule.
- Native recording playback uses a smaller lossless float32 binary asset and an eased transition to live physics. A diagnostic capture measures Unity frame intervals separately from browser RAF callbacks.

## Acceptance matrix

| Domain | Mandatory acceptance |
| --- | --- |
| Story | Every used command and choice; 36 main episodes; replay/skip/resume; tutorial; episode 32; credits restore; no stuck camera or effect. |
| Satone | Work/break/desk boundaries; object readiness; weighted/cooldown actions; sleep/wake; sofa/kitchen; snacks/drinks/window/musicbox; pointer/status reactions; continuous poses. |
| UI | Every window at 720p/1080p/ultrawide and 100/125/150% scale; input, focus, drag, scroll, hover, cursor; no overlapping controls. |
| Graphics | Same PC/web camera, pose, outfit and time; materials, lightmaps, probes, shadows, sky/weather, post, desk lighting; quality presets. |
| Physics | All outfits; book/break/standing/window/stretch/return; no visible clipping or pose snaps; native/reference comparison; stable suspend/resume. |
| Audio | Playlist visibility/filter/exclude/order/shuffle/repeat; imported codecs; voice/SFX/ambient/timer volume; fades, interruption and restoration; bounded loading. |
| Tools | Notes/lists/task dates; diary/calendar; historical habit schedules; statistics and midnight/year boundaries; presets/unlocks/points/local achievement progress. |
| Save | Legacy web migration/backup; PC Demo/Release v1/v2 conversion; retained IDs and history; corrupt/partial/quota/conflict paths; no original save writes. |
| Stability | Full workflows in three browsers; GPU/frame-time budgets; resizing/focus/context lifecycle; two-hour soak without crashes, lost saves or continued memory growth. |

## Evidence and remaining gates

`parity-core-check.json` is an Editor behavioural result. `pc-save-check.json` includes a read-only check of the local PC files; it does not claim every source save type is mapped. `native-physics-reference-check.json` records native solver output, not PC screenshot parity.

Before declaring 95%, qualify the remaining PC Demo/Release fixtures and compare graphics/physics with matching PC captures. Full DPI/input workflows, sustained GPU timing and the two-hour soak remain acceptance gates. No completion percentage is claimed yet.

Browser qualification exposed a dynamic-font crash when opening decorations in WebGL. The symbolized trace identifies TMP_FontAsset.TryAddCharacterInternal. Recovered atlases are now read-only; runtime-created dynamic atlases handle additional glyphs. Final-build browser results and fingerprints are recorded in `parity-browser-evidence.json`; each result states its limited scope. Editor success alone is insufficient.

The final build also fixes the browser frame scheduler: a 15-second capture on the RTX 4070 Laptop GPU measured 57.01 FPS with a 60 FPS setting, median frame interval 17 ms and p95 19 ms. This is a short Unity frame-interval check, not GPU render-duration measurement or a long-term performance guarantee. A bounded Chrome layout pass captured 22 windows/popups at 1280×720 and 150% pixel density; selected Audio, Birthday and Habit views were visually reviewed. It does not replace the full layout matrix. The earlier soak was cancelled after the build changed and does not count as a two-hour pass.
