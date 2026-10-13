# Dialogue layering — 1.19.2

Opening a popup raised the entire `Canvas/UI` group above the story controls. Closing the popup hid its window but left that group at the front. `UI/BottomBackImage` then shaded dialogue, speaker names and History/Auto/Skip while the music controls stayed bright.

The recovered scene places `UI` before `StorySystemUI` and the dialogue buttons. The web adapter now remembers that sibling position during initialization and restores it when a window closes. Popups can still appear above dialogue while open. The native text positions, fonts, background opacity and message-type selection remain unchanged.

Unity regression checks open and close Talk Log, Settings, History and Playlist and require the original layer order after each close. Browser verification resumes the exact Valentine episode line shown in the reported screenshot, then checks dialogue after another popup cycle.
