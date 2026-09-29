# Changelog

## 0.3.0

- New: markers are layered with the map's fog clouds. A marker in front of (south of) a cloud is drawn over it, so revealed places near the fog edge are no longer covered; markers inside or behind the fog stay hidden. Toggle: General > "Markers in front of distant fog".
- New: "Show guards & mercenaries" (off by default) hides village, forest, bonfire and city guards and the barracks mercenaries. Herbert, the head of the guards, is always shown.
- New: separate scales for main NPCs (with a reputation, default 1.25), other NPCs (0.85) and town vendors (0.85). Scale sliders now range 0.25-4 and accept typed values with two decimals.
- Portrait markers now share one size before scaling, instead of depending on each portrait's crop.
- Fixed: NPCs that the game leaves without a world zone (e.g. Herbert in the barracks) were missing; they now appear at their building's map point.
- Ctrl+F9 dump now also lists every NPC instance, hidden ones included.

## 0.2.0

- New: town vendors you built are marked with the vendor's portrait; hover to see the vendor type (Apothecary, Baker, ...). Toggle and tune it in the new "Town vendors" settings section.
- New: gamepad support. Move the map cursor onto a marker: the cursor frame snaps onto it and its name shows, like the teleport pillars.
- Verified against Steam build 25533739.

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
