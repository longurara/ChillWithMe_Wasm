# Native HUD sprite recovery

The exported sprite YAML replaced logical `m_Rect` with tightly cropped polygon bounds. The original atlas pixels, `atlasRectOffset`, and `uvTransform` remain available. For unrotated, centered UI sprites, recover the logical extent as twice the distance from the atlas origin to the UV center. Validate integer dimensions and texture bounds before applying recovery.

Write recovered copies under `Assets/Prototype/NativeSprites`; keep the texture rect, atlas reference, UVs, and polygon geometry unchanged. Restore the logical rectangle, centered pivot, and textureRectOffset. This lets Unity UI draw the original trim padding. Using Sprite.Create on the entire atlas region would risk drawing neighboring tightly packed pixels.

Examples verified in the Editor:

| Sprite | Export crop | Original logical size | Bottom trim padding |
| --- | --- | --- | --- |
| t_ui_buttonicon_active_exit | 48.85 × 21.85 | 80 × 80 | 29.08 |
| t_ui_buttonicon_hide | 39.85 × 29.90 | 80 × 80 | 25.08 |
| t_ui_pomodoro_stop | 20.85 × 25.85 | 52 × 52 | 13.08 |
| t_ui_pomodoro_play_active | 27.85 × 26.90 | 52 × 52 | 13.03 |

The exit sprite is the original telephone, not a replacement headphone asset. Runtime mapping also covers prefab images and button sprite states; empty image overrides remain empty so play/pause can change dynamically.

Related layout/state fixes: playlist rows have the native prefab height instead of zero preferred height; mute/unmute reflects actual volume; the volume popup follows pointer/drag state; timer pause keeps its running-panel layout, skip hides while paused, and the progress ring follows work/break direction. See `native-hud-check.json` and `preview-native-hud-*.png` at the workspace root. These checks use in-memory data and Editor renders, without browser gameplay or user save writes. This does not establish overall visual parity with the original game.

The Level idle frame belongs to a vertical animation strip (256 × 3840). Its exported crop is about 130 × 128, but its logical frame is 256 × 256. Recover the individual frame around the original UV center instead of treating the entire strip as the UI sprite. Keep the original level root scale, anchors, and position; the drawing-bounds check confirms a gap between the Level and timer circles. Hide the EXP flash while idle.

Timer controls now read native TimerParentUI, CountupTimerUI/StateView and all five PomodoroPresetInstance bindings. Pomodoro defaults to two loops, 25 work minutes and five break minutes; loop/minute limits follow the original services. Countup has separate ready/running/paused/reset states and credits work time. Presets are saved in the web save. Mode selection and hover images follow the source InteractableUI image arrays; this adapter applies their final alpha state without the original tween.

Talk Log cells contain both a root Button and a child play Button. Both must open the same chapter; the child receives the first raycast over its icon and previously had no action. Editor raycasts confirm that the child receives pointer events and belongs to the selected row. Full chapter playback in the browser remains a manual check.
