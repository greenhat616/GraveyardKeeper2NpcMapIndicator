# Changelog

## 0.1.0

First public release.

- Shows NPCs on the world map at their live positions, using the game's own map projection.
- Crops the game's NPC portraits to head-and-shoulders busts and draws them on semi-transparent pixel-art parchment tags; name labels in the game language.
- Places NPCs inside interiors at the building's map anchor.
- Spreads overlapping markers side by side, with ink leader lines to their real positions.
- Name labels stay visible while hovering (the layout pauses under the pointer).
- Point-of-interest categories, each switchable in the Mods menu:
  - On by default: ores & quarries, fishing spots (fish left, faded when empty), caves & descents, portals.
  - Off by default: teleport pillars, stones, doors & basements.
- Per-category settings: rules, color, icon, scale, label mode. Global settings: pixel size, parchment opacity, refresh interval.
- Public provider API (`IMapMarkerProvider`, `MapMarkersApi`) so other mods can add their own marker categories.
- Ctrl+F9 writes the ids of all world objects in the loaded save to a file, for writing rules.
- Verified against Steam builds 25467846 and 25533739.
