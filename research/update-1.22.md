# Update 1.22.0

This release fixes story flow, room activity returns, and missing native UI behavior. Cloud services remain deferred.

## Story and activities

- Manual dialogue now opens its following selection after the voice and typewriter finish, matching ScenarioReader. Resuming inside a selection restores every option.
- Selecting an option clears its index, so later voice lines cannot be skipped by an old choice.
- VoiceText changes text/voice only. Text-loop choices use the source Arg4 delay; repeated lines no longer receive the extra duplicated wait. Resume restores voice-defect settings.
- Story entry blends into the script's first pose. Room returns retain the controller and existing secondary-motion state.
- Work-to-break reactions install an activity for the new timer phase. Same-phase reactions keep the current posture and schedule. Pending timer speech is retained while another activity is busy.
- Direct returns to break reading/tea restore the corresponding held object, as HeroineUseObjectController does. Taking a book starts its idle page animation instead of showing its exported folded pose.
- The phone button now starts the native game-end pose and farewell selection. The web saves and tells the player they can close the tab.

## Interface

- Separate settings headings from their scroll content; align the five volume rows; remove duplicate graphics reset controls and use the actual build version.
- Move custom room presets out of the native preset controls. Habit editing has separate save/discard actions. Task lists have a picker and a collapsible completed-task drawer.
- Birthday editing uses separate month/day fields and explicit confirm/cancel. February 29 is celebrated on March 1 in non-leap years, matching BirthdayUtility. The web keeps birthdays editable.
- Error notices expire after six seconds and can be dismissed. Auto uses one caption and changes color, as in the source.
- Extract 603 InteractableUI bindings and 124 tooltip targets. The eleven requested toolbar buttons reuse their original glow graphics; activation/exit fades use 0.15/0.2 seconds. Tooltips retain the source 0.22-second activation delay and 0.2-second exit/fade values.
- Restore the native green first-timer guide: clockwise rotation every 1.8 seconds, a 2.3-second scale pulse up to 1.13, and 0.5-second fades. It follows the Level 1/new-session guide conditions, hides for long stories/tutorials, and stops after starting a timer.
- Use CursorService's default/talk/talk-block PNGs and (7, 0) hotspot. Reapply the cursor after WebGL visibility/viewport resets. The click PNG exists in the source, but UpdateCursor does not use it.

## Coat movement

The source coat has zero restoration strength. The web's additional spring pulled its hem toward the rigid exported pose; that spring is now removed for the coat. The source depth-dependent angle curve is retained with a 90-degree web ceiling. Bake eligibility includes the solver revision, so earlier recordings are rejected. Human-joint rotations and all bone translations remain owned by the Animator.

The six hem tips drop by an average of approximately 3 cm in a fixed leave-chair test. This measures the web solver and does not establish PC MagicaCloth parity.

The coat also uses the source's ordered, open-front strip connections. The web now retains twenty horizontal edges, applies its source distance-stiffness curve, and checks contacts along those edges. Previously, the two back strips could sit outside the hip while the panel between them cut through it. Capsule extraction now retains radius separation, reverse direction and center alignment; disabled separation uses the same radius at both ends. These semantics were checked against [Magica's BoneCloth guide](https://magicasoft.jp/en/mc2_bonecloth_mesh/) and [capsule settings](https://magicasoft.jp/en/mc2_capsulecollidercomponent/).

[Window coat check](window-coat-check.json) evaluates actual transformed back-hem edges in eight fixed poses from native opening/closing motions 57/58. It finds zero hip penetration at the sampled points. This does not certify every cloth triangle or self-collision.

## Startup inspection

BootBehavior and EntryBehavior use LoadDirectionService with a Canvas/Animator (`StartSplash`, `SplashFinish`, `SplashFadeIn`). The camera-touch introduction uses character animation 400, a camera animation, post effects and noise audio. These flows are not videos. The browser currently uses its own HTML download loader; the full native splash sequence has not been ported.

The supplied penguin/phone/NESTOPI screen is present in `Boot.unity`: `LoadingCanvas` and `LoadingCanvasMobile` contain `SatoneIcon`, `Phone`, `Halo`, and `COLOGO`. Their recovered sprites are `t_ui_loading_icon`, `t_ui_loading_phone`, `t_ui_loading_halo`, and `t_ui_loading_logo` (plus active/error variants). `Loading.controller` runs `anim_ui_loading` as a 1.5-second loop; its phone transform and graphic alpha curves animate the UI. The finish and game fade-in clips last approximately 1.333 and 0.567 seconds. Entry waits for the room to load, with a two-second minimum loading phase, before finishing the direction and activating the room. The related original files remain in the local recovered assets, excluded from Git.

## Verification and limits

- [Story flow](story-flow-check.json): 607 script groups, 77 selection cursors and 223 commits. This is not a full browser playthrough of all episodes.
- [Native motion sweep](story-motion-sweep.json): 106 script motions enter and become ready in their required contexts.
- [Break props](break-props-check.json), [pane layout](pane-layout-check.json), [hover/guide](hover-check.json), and existing dialogue/command/motion checks pass.
- [Physics](physics-check.json): 3,600 frames across five outfits and eight motions, including opening/closing the window, fixed-step invariance, contacts, suspension and rebinding. [Coat gravity](coat-gravity-check.json), [window hem](window-coat-check.json), and [bake guards](physics-bake-check.json) also pass.
- Browser evidence is separate: [physics](browser-physics-check.json) and [story/UI](browser-story-ui-check.json). Each report identifies its tested build and scope.

PC visual/physics parity, physical phones and Safari remain unverified. The repository contains source and reports; original assets, screenshots and generated builds remain excluded.
