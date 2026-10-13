# Story and Satone activities — 1.19.1

The greeting used the main episode text window, which placed its text and controls over the music bar. The port now chooses the original message window for each scenario. Greetings and short reactions use the normal message bubble above the music controls.

Story command 50 now waits for a character click instead of generic Next. Timer voice command 22 waits for the matching `PlayPomodoroVoice` animation event; cancellation clears old cues. Story BGM respects authored gain, start position and fades. Playlist commands retain the selected track, and live story mode remains distinct from Talk Log playback when saving or resuming.

Satone's idle behavior now includes the source sleepiness progression and recovery, weighted sofa actions and cooldowns, desk eligibility for PC breaks, and an invitation to an unlocked unseen episode while resting. Short reactions keep the listening posture and activity duration. Activity sequences block automatic dialogue interruptions; snack reactions return through the native return animation before restoring the desk.

Native activity machines sometimes enter a sibling loop without visiting the exported motion's named entry. Motion completion now recognizes these related loops while excluding unrelated previous states and pending transitions.

Unity checks cover message geometry, controller waits, voice cue matching/cancellation, music policy, sleep progression, sofa bindings/weights and native animation readiness. Chrome checks confirm tutorial resume/click advancement, automatic story invitations, live story saves and a timer voice reaction. Detailed results are in [browser-story-daily-check.json](browser-story-daily-check.json).

This remains a partial port. All episodes, AI transitions, object readiness rules and separate scenario/playlist audio mixing have not been verified against the Windows engine. Cloud remains deferred.
