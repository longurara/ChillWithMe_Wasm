# Gameplay polish — 1.20.0

This release keeps the port standalone and leaves cloud/account integration deferred.

## Story and dialogue

- Checked all 607 scripted groups, 36 main episodes, 2,289 commands, branch targets and 1,333 referenced voice clips. This is a static catalog check, not a browser playthrough of every episode.
- Read normal-character speed from the language master and punctuation/newline/last-character waits from the recovered typewriter components. Clicking while typing accelerates normal characters to 0.01 seconds. Wait actions and rich-text scopes are handled without displaying action tags.
- Separate scenario BGM from the playlist player. Story stop commands cannot stop a user's playlist. Long stories pause and restore user music/ambient playback; MusicService commands still select the user's playlist channel.
- Restore the loop bank when resuming the interactive text-loop scene, use a shuffled cycle, and expose choices after the first voice/text completes. Choice waits belong to the reader coroutine and end with the story.
- Preserve the native selection prefab's width/height and normal/used text colors. Added an actual raycast check so the dialogue backdrop cannot intercept the choice button.
- Tutorial introductions hide the room HUD, facility focus temporarily reveals it, and leaving the tutorial clears its guides.

The punctuation categories and action syntax follow the [Febucci typewriter documentation](https://tools.febucci.com/text-animator/unity/docs/1.x/typewriter/text-animator-players) and [action documentation](https://tools.febucci.com/text-animator/unity/docs/2.x/typewriter/wait-actions-when-typing), alongside the game's recovered configuration. The actual novel data contains no custom animated rich-text effects.

## Room activities and presentation

- Object readiness tracks cup servings, hot/cold selection, refill/ice cooldowns, window state/cooldowns and musicbox readiness. Animation events update the state. Activities wait for their current motion before changing.
- Musicbox audio has a separate source and releases its loaded asset when closed. Character clicks test the animated mesh, avoiding reactions from empty space inside renderer bounds.
- The three room camera transforms (desk, sofa, bed) are selectable and saved. Desktop retains the source 35-degree FOV. Portrait mobile uses an adaptive desk composition instead of the recovered uninitialized intro-camera placeholder.
- This does **not** add a missing native bed activity: the recovered `_heroineTransform` array is empty, and the animation master has no bed movement/pose. Sofa visits continue to use the original leave-chair animation.
- The source and port both use Linear color space. Existing source materials, lighting and four time-of-day profiles remain in use. Beautify/Cinemachine/MagicaCloth are adapted; exact Windows pixel/physics parity is not established.

## Native timer controls

- Timer bindings distinguish duplicate sibling groups in the original scene; unused duplicate controls are hidden. This avoids selecting one copy while another visible copy retains its highlight.
- Timer mode changes clear the previous hover/selected images, following `TimerParentUI.UpdateTimerSettingMenuUI`.
- Decorative timer images do not intercept clicks on the other mode button; both directions have an actual UI raycast check.
- Shared hover/use images retain the visible layer alpha. Selectable tint no longer overwrites the native interaction images.
- Idle Play uses the original hover-only green ring; it is hidden when the pointer leaves.

## Browser lifecycle and verification

- Coarse-pointer layouts use the available viewport and safe-area insets. Device pixel ratio is capped on touch devices. Dialogue/voice/scenario BGM pause across focus loss, so a suspended page cannot consume its Auto delay.
- Unity checks cover typing, channel isolation, choices, resume/cancellation, object cooldowns, mesh picking and source camera transforms. Chrome verifies selected gameplay checkpoints and a day/evening/night/cloudy visual matrix.
- WebKit on this Windows test host lacks Web Audio API. Its emulation cannot establish audio compatibility with real iOS Safari. Physical phone tests and exact Windows visual comparison remain unverified.

See [catalog checks](story-catalog-check.json), [Unity checks](gameplay-parity-check.json), and [browser checks](browser-gameplay-polish-check.json).
