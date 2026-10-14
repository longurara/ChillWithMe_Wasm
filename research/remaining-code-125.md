# Code completion — 1.25.0

Implemented the six follow-up code tasks:

- External gameplay assets: 30-second request timeout, three attempts, bounded queue waits, latest-request cancellation, and bundle lease cleanup.
- WebGL loss: save before recovery, one automatic restart per minute, and manual retry when browser storage is unavailable.
- Local achievement hooks: original category IDs, legacy exit-key migration, elapsed work-hour progress, completed hot-drink and environment-change triggers. No Steam services.
- Added UI translations: 79 entries in seven existing languages. User-created names remain unchanged. Technical error strings and every original label are not all covered by this catalog.
- Physics: 366 playable states recorded, comprising 18 native PC captures and 348 web CPU simulations. Compatible shared bones use recordings; changed garment bones use the live solver. This does not establish PC visual parity.
- Settings → General → Unlock for testing: toggles availability without completing episodes or granting achievements.

Validation: full Unity feature suite, achievement boundaries, queued loader cancellation, localization catalog completeness, bake coverage and solver fingerprint, context recovery unit checks, and Chrome smoke checks across 12 panes and seven story languages. The smoke run used a short timer start/reset, not a new full Work→Break boundary test. Five-outfit physics checks passed in Unity. Active network interruption and every outfit/action combination still require broader runtime coverage.

All browser checks use an isolated test profile. PC saves and cloud services were not modified. Build artifacts and licensed game assets remain excluded from Git.
