# Story presentation fixes — 1.19.3

Long stories now hide the original scene's eleven HUD groups. Dialogue, its backdrop, and History remain usable. Ending a story restores the previous HUD state, including manual hiding. The web tutorial still keeps its facility controls available.

The original `ScenarioReader.CommandText` waits for text and voice to finish before calling `AutoNext(Arg2)`. The port previously counted that pause while audio was playing. It now starts the pause after both finish and reads typewriter speeds from the original language master.

The original `PlayScreenMove` and `PlayGlitch` start immediately after their direction delay and stop after `Arg2` seconds; `-1` means persistent. The port treated these as persistent amplitude tweens, leaving Ep. 0 shaking indefinitely. Finite effects now stop, expired effects stay off on resume, and old timeouts cannot cancel newer effects. The separate AdjustAllUnstable command retains its tween behavior.

Unity checks cover HUD restoration, History layering, Auto timing, language speeds, finite/persistent effects, resume, and stale timeouts. Chrome checks use an isolated browser profile and selected story checkpoints; they do not establish parity for every episode or mobile browser.

- [Unity checks](story-presentation-check.json)
- [Chrome checks](browser-story-presentation-check.json)
