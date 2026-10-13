# Physics update 1.21.0

The standalone WebGL port now uses a 60 Hz CPU solver for secondary bone rotations.
The Animator continues to own human joint rotations and all bone translations.

## Changes

- Fixed-step accumulator with interpolated animation targets and rendered offsets.
  Catch-up is limited to six steps; long frames no longer reset the entire rig.
- Focus suspension and rigid anchor rebasing preserve secondary offsets on resume
  and teleports. Normal story/activity changes keep simulation state.
- Source damping, gravity, radius/depth curves, inertia, speed limits, angle limits,
  restoration and maximum-distance settings are extracted from the recovered scene.
  Web safety caps are hair 30°, coat 40°, hat 25°, custom sleeves 12°.
- Original sphere/tapered-capsule contacts plus five neck/arm capsules. Point and
  two interior edge samples limit crossings. Authored rest intersections get a
  fixed allowance, rather than a new allowance for every animated pose.
- Outfit changes remove stale chains and register both custom sleeves. Existing
  chains retain particles and velocity. Source sleeve tweens remain authoritative;
  custom sleeve physics is a small web-specific additive adaptation.
- Body transforms are cached per frame and parsed bake data is reused across
  outfit changes to limit repeated work and allocation.
- Three difficult motions (50, 752, 1500) have recorded rotation offsets and a
  0.12-second blend to/from live simulation. Playback requires the same profile,
  bone paths, default costume, exact state, no transition and no manual look IK.
  The recordings come from the **web CPU solver**, not native MagicaCloth.

## Verification

[Rig checks](physics-check.json) cover 2,700 frames, five outfits, six native
motions at 30/60 FPS, and repeated outfit swaps. Tests assert no human-joint or
local-position writes, finite states, bounded output, fixed ticks at 24/30/60/120
FPS, pause/resume, translation/rotation teleports and contact geometry.

[Bake checks](physics-bake-check.json) verify recorded offsets, playback eligibility,
profile mismatch rejection and return to live simulation. The full checked build
also runs existing story, daily-life, UI and motion-continuity checks.

Browser evidence is recorded separately in [browser-physics-check.json](browser-physics-check.json).
It covers Chrome outfit changes, visibility suspension/resume, and 60/30 FPS
settings in desktop/landscape viewports. Browser screenshots are visual checkpoints,
not a quantitative frame-time benchmark.

A separate Chrome mobile context also checks coarse-pointer/touch input, outfit
changes and focus resume at 30 FPS. This is emulation, not a physical-phone test.

## Rebuild and compare

Run `python src/graphics/prepare_physics.py` to regenerate the source profile.
`build.ps1` runs this and the checked build automatically regenerates motion bakes.
For an incremental Editor-only check, copy Runtime/Editor into the prepared
project and execute `ChillPhysicsCheck.Check`; use `ChillPhysicsBake.Build` to
regenerate and validate the recordings without building the player.

The [source profile report](physics-source-profile.json) documents the original
values. For a visual PC comparison, use the same costume, motion, camera and time
of day; compare head turns, stretches and story returns at 30/60 FPS. Capture
both versions after the same warm-up and repeat on a physical phone.

## Remaining differences

This is not the original MagicaCloth solver. Mesh cloth, self-collision, weighted
tangent interpolation and every per-vertex contact are not reproduced. Rotation
limits can prevent complete mesh collision correction. Bake fallback is limited
to three recorded states with the default costume. PC runtime visual matching,
physical mobile performance and Safari compatibility remain unverified.

Cloud remains deferred. The preview stays separate from hoilauchay.
