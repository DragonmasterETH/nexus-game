Runtime-loaded UI sprites (Resources folder)
=============================================

Board backdrop (in-match only, under hexes):
  background.png  ->  Resources path "Sprites/background" (fallback "Sprites/Background")

Place PNGs here so scripts can use Resources.Load without Inspector wiring.

Rubium (currency):
  Rubium.png   -> "Sprites/Rubium"

Victory points:
  VP.png       -> "Sprites/VP"  (fallback: "Sprites/Vp")

Mine yield (tile popup — matches ExtraMineYield 1, 2, or 3):
  Code tries, in order: OreChip1, Ore_Chip_1, "Ore Chip 1" (with spaces — matches default filenames).
  Same pattern for 2 and 3.
  PNGs are often imported as Sprite (2D); loading uses Sprite or Texture2D automatically.

Unrevealed exploration hexes (flat quad on tile until revealed):
  Ore Unrevealed.png  -> "Sprites/Ore Unrevealed"  (fallback: "Sprites/OreUnrevealed")
  If missing, markers use the previous yellow tint.

Importer: Texture Type "Default" works well with IMGUI. For "Sprite (2D)", assign Sprites on DemoHUD instead of relying on Resources.
