# Motion continuity — 1.20.1

Returning from dialogue used to replace the character controller, call `Rebind`, restore the desk pose more than once, then reinstall autonomous activities. This discarded the outgoing pose before the next animation frame.

The room now uses the full source character controller after story data loads, including when autonomy is disabled. Ordinary actions update `MotionType` on that controller, allowing its authored transitions and exit clips to run. At story exit, a 0.25-second crossfade targets the native work/rest entry. This also handles script-only poses, such as the leaning pose in episode 32, that have no ordinary work exit. Scene-history replay still supports immediate pose reconstruction.

Routine motion changes retain hair/clothing simulation state. Return look weight fades out over 0.25 seconds, and desk/chair transforms interpolate into their live target over the same interval. Short reactions retain their existing activity, listening posture, self-talk count and schedule.

## Source comparison

`HeroineService.ChangeAnimation` / `ChangeHeroineAnimationForInteger` use the same Animator and `MotionType`; `HeroineAI.FinishMainStory` and `FinishHeroineClickReaction` select the next matching action. The source also has an explicit immediate `Play` path at main-story boundaries. This change removes the port's additional controller resets; it does not assert that every transition in the original game is blended.

## Verification

Unity checks cover all three work modes with autonomy on/off from both a normal story pose and the episode-32 leaning pose, unchanged outgoing controller/state/pose, native loop completion, look fade, retained cloth dynamics, short-reaction activity state and interpolated chair return. Sampled at 60 steps per second over 20 seconds; the largest tracked-bone rotation between steps is recorded in [the report](motion-continuity-check.json). These samples are not a proof of every motion pair.

The final WebGL build and selected Chrome checkpoints are recorded in [browser verification](browser-motion-continuity-check.json). The port remains standalone; cloud is deferred. Exact Windows graphics and physics parity remains unverified.
