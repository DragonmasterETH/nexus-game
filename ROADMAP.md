# Nexus Ops — product roadmap (living checklist)

This tracks the full feature list you provided. Items marked **[stub]** have a placeholder or partial wiring in code; **[done]** means implemented to spec.

## Testing & quality
- [ ] Mobile testing (devices + profiles)
- [ ] More console output for battle resolution & bug catching → **[stub]** `NexusBattleDebug`
- [ ] AI vs AI watch mode → **[stub]** `Watch AI vs AI` + `GameController.WatchAiVsAiMode`

## Core rules
- [ ] Retreat rules: cannot retreat into another fight; retreat only as first movement of turn → **[stub]** `MovementRetreatRules` + `GameController.EnforceRetreatRules`
- [ ] Monolith special rules (documented + linked to draw logic) → **[stub]** `MonolithRulesDoc` (code already draws extra cards when alone on Monolith)

## UX / content
- [ ] Unit descriptions & abilities — bottom sheet → **[stub]** `UnitCodexData` + `DemoHUD` panel
- [ ] UI for card mechanics (Energize / deployment summary) → **[stub]** `DemoHUD` cards panel
- [ ] Animation when collecting Rubium at turn start → **[stub]** income flash in `DemoHUD`

## Meta & economy
- [ ] Store: skins, currency or XP from battles + UI → **[stub]** `MetaProgression` + store panel in `DemoHUD`
- [ ] Menu space for store skins (holding slots) → **[stub]** store grid in `DemoHUD`
- [ ] Ranking system → **[stub]** `MetaProgression` + label in `DemoHUD`

## Audio
- [ ] Background music in menus → **[stub]** `MenuMusicController` (assign `AudioClip` in Inspector)

## Multiplayer & session
- [ ] Replay match / join new match / find match / lobby / scoring UI / leave game → **[stub]** `LobbyStub` + main menu buttons (placeholders)

## Modes
- [ ] Secondary game modes + mechanics → **[stub]** `GameModeCatalog` (data only)

---

When you finish a line item, change `[ ]` to `[x]` and remove the **[stub]** tag if fully shipped.
