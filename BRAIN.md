# Agent handoff — ChillWithMe Wasm

Updated: 2026-10-14, Asia/Saigon. Read this before continuing. This is a handoff, not proof that the port is complete.

## User intent and working preferences

Continue completing the browser port against the actual PC code and assets. The user repeatedly asked to inspect original behavior rather than approximate it from screenshots. Target is eventually 95% acceptance in each domain, but **95% has not been demonstrated**. Cloud save/accounts and Steam services are deferred. Existing deployment hosting is in scope.

User speaks Vietnamese, prefers action and short updates, and was frustrated by a previous six-hour wait. Avoid expanding scope or asking for repeated approvals. README must be concise English for general readers. User authorized small logical commits and pushes to main. Preserve unrelated work in the hosting repository. Do not overwrite the user's browser or PC saves during tests.

No new agent has been started by this handoff. The next agent should work in its own conversation using this file.

## Repositories and local paths

| Purpose | Path / URL |
| --- | --- |
| Game source | `E:/02_Projects/chill-with-you-web` |
| Game remote | `https://github.com/longurara/ChillWithMe_Wasm.git` |
| Hosting repository | `E:/04_Backups_Archive/Data-old/du_an_2026/fuo/hoilauchay` |
| Hosting remote | `https://github.com/longurara/fuo_get_link` |
| Original PC game 1.17.3 | `E:/02_Projects/decr/Chill with You Lo-Fi Story` |
| Original assemblies | Original game directory + `/Chill With You_Data/Managed` |
| Recovered C# reference | Game repo + `/Recovered/ExportedProject/Assets/Scripts/Assembly-CSharp` |
| Local preview | `http://127.0.0.1:8093/` |
| Public game | `https://hoilauchay.vercel.app/chill` |
| Asset Worker | `https://chill-hlc.hikarilong.workers.dev/chill` |

Original game/assemblies are read-only references. Do not redistribute original DLLs or commit licensed generated assets.

## Current state

Game version **1.25.0** built successfully and deployed. Game working tree was clean after push. Hosting tree contains substantial pre-existing GTA, Office and research changes: do not stage all, reset, clean, or commit them.

Game commits on main:

- `ad2af84`: bounded asset loading and WebGL recovery.
- `0fb8911`: physics coverage across playable states.
- `855ced6`: localized controls, achievement hooks, safe test unlock, verification and version bump.

Hosting commit `8bba8d18`: deploy 1.25 and derive manifest version from build index.
Worker deployed version ID: `902b8642-cc61-4b51-87b6-036106caa221`.
Current build fingerprint: `3b75f90cc8a5fbacfd87ff355f8eb32765a8aac3007ccaea51d99c39613d963d`.
Deployment manifest: 272 files, 658,540,019 logical bytes. Includes on-demand packs.

## Architecture and source ownership

Edit `src/graphics/Runtime`, `src/graphics/Editor`, `src/graphics/Plugins`, and `src/gameplay-ui`. `UnityProject` is generated/copied from these. Do not make source fixes only inside UnityProject.

- `ChillStoryRuntime*.cs`: save, story, feature actions, daily behavior, local achievements.
- `ChillNativeUI*.cs`: native Unity canvas UI, controls and layout. This is not a DOM UI.
- `ChillAnimationActions*.cs`: animation behavior and source events.
- `ChillBonePhysics*.cs`, `ChillBoneBake*.cs`: live physics and recorded replay.
- `ChillLibraryBrowser*.cs`: external asset loading and bundle leases.
- `src/gameplay-ui/index-native.html`: Unity bootstrap.
- `src/gameplay-ui/context-recovery.mjs`: GPU context lifecycle.
- `src/gameplay-ui/save-store.mjs`: browser save/backup/writer session.
- `src/gameplay-ui/i18n.json`: 79 additional port labels in En/Ja/ZhHans/ZhHant/Pt/Ko/Ru, not the complete original localization data.

Generated/ignored: UnityProject, WebPlayer, AssetLibrary, .tools, logs, output, .playwright-cli. Local preview maps `/assetpacks/` to `AssetLibrary/assetpacks`, outside WebPlayer. Those packs are REQUIRED for voices/music/outfits.

## Latest six tasks: implementation and limits

1. **Asset loader:** gameplay external requests have 30-second timeout, three attempts with backoff, 120-second queue wait, per-slot versions/cancellation, and lease cleanup. In-flight asset loads are not unloaded mid-operation. Tests cover queued cancellation, callbacks and lock ownership. Active real network interruption/retry is still a runtime acceptance gap. Legacy inspection/startup branches are not all converted to identical retry behavior.
2. **WebGL recovery:** saves before restart; loss warning and Retry; automatic restore restart limited to once per minute. Missing/denied session storage leads to manual retry. Unit checks and actual Chrome WEBGL_lose_context loss/restore passed; test save survived restart.
3. **Local achievements:** 22 source category keys, migration of EXIT_GAME_COUNT to FINISH_GAME_FROM_EXIT_CALL_BUTTON_COUNT, elapsed-hour counter, successful hot-drink completion and actual environment-change hooks. Existing episode/timer/penguin/tool hooks remain. No Steam integration. New progress helper does not constitute a completely redesigned achievement UI.
4. **i18n:** 79 extra labels/messages, seven original supported languages, exact and prefix/value matching. Dynamic labels refresh; user preset/task names remain unchanged. Some technical errors and original labels remain outside this catalog. No Vietnamese locale was added.
5. **Physics:** HCB3 binary with per-clip native flags; HCB2/JSON compatibility. 366 playable states: **18 native PC captures + 348 web CPU recordings**, ~54.5 MB bake. Shared compatible bones can replay; changed garment bones use live simulation/contact constraints. This is NOT 366 native captures and does not establish clipping-free or PC-equivalent rendering. Optional `capture-native-physics.ps1 -AllMotions` exists but has NOT been run.
6. **Test unlock:** Settings → General → Unlock for testing. Actual mouse click verified ON→OFF without changing level, completed chapters or achievements. Toggle controls availability; it does not mark episodes complete. Normal gameplay can still earn achievements.

## What has actually been verified

- Unity full feature suite and final build returned 0. Latest logs: `logs/unity-remaining125.log`, `logs/unity-remaining125-final.log`.
- `research/remaining-code-check.json`: migration, hour boundary, queued cancellation, unlock safety, 79 labels/seven locales.
- `research/physics-coverage-check.json`: 366 states, 18 native/348 CPU, solver fingerprint.
- Five outfits/eight sampled motions/3,600 physics frames passed Editor checks. Editor checks do not prove browser visual matching.
- `node research/check-context-recovery.mjs` passed save order, restart throttling, manual retry, disposal/storage-denial cases.
- Chrome 1.25 smoke: 12 panes, seven story language entries, zero JS errors or missing-glyph messages. Timer start/reset only; **the 65-second Work→Break wait was reduced to one second for this smoke run**, so this is NOT a new full boundary pass. Do not reuse its printed scope as evidence of a real boundary.
- Actual Chrome settings screenshot reviewed; unlock clicked by mouse. Actual context loss/restore restart retained test save.
- Earlier 1.24 checks passed basic Chrome/Edge/Firefox workflows, but these are not full 1.25 cross-browser qualification.
- Hosting streaming and pinned-download tests: 10 passed.
- Public `/chill` returned 1.25.0; `/chill/ui/i18n.json` returned 79 entries; `/chill/ui/context-recovery.mjs` returned 200. These are the correct UI paths, not `/chill/i18n.json`.
- A request to `/chill/health` returned 404 although source contains that route. Cause was not investigated. Do not claim health endpoint was verified.

Browser QA used a separate Playwright session `chill125`, closed after testing. Screenshots remain in `output/playwright/125-*.png` and `24-chrome-*.png` (the reused prefix does not mean the latest screenshots are version 1.24). Test profiles hold fixture saves, not user's gameplay save.

## Next work, in priority order

These are acceptance gaps, not an instruction to invent more features. Reproduce, inspect PC reference, then fix demonstrated failures.

1. Real active pack failure/cancel/retry tests: interrupted download, failed dependency, rapid song/outfit switches, then ensure queue recovers and leases/memory stabilize.
2. Physics visual comparison against PC with matched camera/pose/time/outfit: book/page turn, break props, standing/window/return, stretch and coat contacts. Test all five outfits in browser. Decide whether more native captures are necessary; do not label CPU recordings as native parity.
3. Complete 1.25 Chrome/Edge/Firefox lifecycle/input qualification: repeat context loss, tab suspend/resume, resizing, writer conflicts, denied/quota storage and import backup safety.
4. Full story commands/choices/36 main episodes; tutorial, episode 32, credits, camera adjustment, restoration of normal UI. UI hiding and Auto timing were prior user-reported issues and must remain regression cases.
5. UI layout at 720p/1080p/ultrawide with 100/125/150% scaling, actual typing/drag/scroll/hover/cursor. Finish translation coverage only where needed, preserving user text.
6. Real Work→Break→Work transitions during each activity; object readiness and smooth return. Include midnight/year/birthday/holiday behavior.
7. PC Demo/Release save fixture coverage beyond the existing 31-file local check; corrupt/partial saves and retained numeric IDs/history.
8. Matching graphics/shaders/weather/desk lighting and sustained GPU timing; two-hour soak. Prior soak was canceled after a build change and does NOT count as a pass.
9. Investigate `/chill/health` route discrepancy if deployment diagnostics are needed.

Detailed fixed acceptance matrix: `research/parity-95.md`. Other useful records: `research/source-parity-audit.md`, `research/remaining-code-125.md`, `research/parity-browser-evidence.json`, `research/physics-bake-check.json`, `research/pc-save-check.json`. Old reports have their own scope/version; do not treat all as current proof.

## Build and test workflow

Tools on this Windows host:

- Unity: `C:/Program Files/Unity/Hub/Editor/2022.3.62f2/Editor/Unity.exe`
- Python: `C:/Users/Hikari/AppData/Local/Programs/Python/Python312/python.exe` (use `-X utf8`)
- Node/npx: `C:/Program Files/nodejs/node.exe`, `npx.cmd`
- gh: `C:/Program Files/GitHub CLI/gh.exe`

Normal build from game repository: `powershell -ExecutionPolicy Bypass -File .\build.ps1`. Close that project's Editor first. Background launch uses hidden window. Full six-task verification entry is `ChillFeaturesCheck.BuildChecked`; final candidate used `ChillRemainingCheck.BuildUsingExistingBake` after a full bake.

The fast existing-bake path verifies a solver fingerprint and binary bounds. It is ONLY suitable when bake inputs/animations/default physics remain unchanged. It does not hash every possible dependency. Rebuild bakes for solver/animation/profile changes. The stored fingerprint is `8e00af3180f9c40c2d38d464354111ed27046dce761ef7271757a49389ca41dd`.

After Unity finishes successfully, run `python -X utf8 finish_preview.py`. Do not open a fresh browser while WebPlayer files are being replaced. Preview: `start-preview.ps1`.

Node checks: `research/check-context-recovery.mjs`, `research/check-save-store.mjs`, `research/check-pc-save.mjs` (inspect arguments before running). Playwright CLI skill is installed at `C:/Users/Hikari/.codex/skills/playwright/SKILL.md`. Use isolated profiles; Unity UI interactions require mouse coordinates/screenshots or public SendMessage commands. A black initial tutorial with `......` may be legitimate intro, not render failure. Fixture startup can race immediate StopStory; wait for startup and stop again if necessary.

## Deployment workflow and past traps

From hosting repository:

```powershell
node scripts/prepare-chill.mjs 'E:/02_Projects/chill-with-you-web/WebPlayer' 'E:/02_Projects/chill-with-you-web/AssetLibrary/assetpacks'
node --test chill-worker/test/stream.test.mjs scripts/download-pinned-artifact.test.mjs
node dl-worker/node_modules/wrangler/bin/wrangler.js deploy --config chill-worker/wrangler.jsonc
```

Worker asset directory is ignored. Manifest and source/config are committed. Vercel rewrites `/chill` and subpaths to Worker. Updating Worker changes the game without rebuilding unrelated SPA code; hosting Git push can also trigger its normal Vercel integration. Wrangler OAuth worked at last deploy; Vercel CLI auth was unavailable.

Files split into 20 MiB chunks (Cloudflare asset limit). **Keep FixedLengthStream/native pipeTo streaming**. The earlier custom JS stream silently truncated large Unity data around 55 MB. Test complete bodies, not merely status 200.

`prepare-chill.mjs` inserts base `/chill/`, absolute `/chill/StreamingAssets`, and cache-busted core build URLs; version now comes from productVersion. Include AssetLibrary packs or game appears loaded but voice/music/outfits fail. Unchanged chunks deduplicate on upload; old local chunks remain, so local physical size differs from logical manifest size.

Earlier Vercel build failure was Minecraft CDN transfer termination. `scripts/download-pinned-artifact.mjs` now resumes HTTP Range transfers, validates hash/length and bounds retries. Do not confuse that unrelated build fetch failure with Chill runtime. Existing source fixes are committed.

Before committing, inspect status/diff. Stage only owned changes in hosting repository; avoid `git add .`. Use small logical commits and push main. Do not commit user saves, generated packs, original DLLs, credentials, or browser profiles.
