# Nexus upload checklist — Map Markers 0.1.0

Game page: https://www.nexusmods.com/games/graveyardkeeper2 → Upload a mod.

## 1. Mod details

| Field | Value |
| --- | --- |
| Name | `Map Markers - NPCs and Points of Interest` |
| Author | `a632079` (must match the plugin metadata author) |
| Version | `0.1.0` (matches `<Version>` in `GK2.MapMarkers.csproj` and `Plugin.PluginVersion`) |
| Category | **User Interface** (alternatives: Utilities) |
| Language | English |
| Brief overview (summary) | see below |
| Detailed description | paste `nexus/description.bbcode` |
| Contains adult content | No |

Brief overview (205 chars):

```
See every NPC on the world map, live, as a portrait on a hand-inked parchment tag. Optional markers for ores, fishing spots (fish left), caves, portals and more. Configurable in the GK2 Mod Framework menu.
```

## 2. Requirements tab

- Nexus requirement: **GK2 Mod Framework** (mods/42). Note: `0.1.x`.
- Off-site requirement: **BepInEx 5.4.23.5 x64**, https://github.com/BepInEx/BepInEx/releases/tag/v5.4.23.5
- DLC: none.

## 3. Files tab

- Build the archive: `pwsh -File package.ps1` → `dist/GK2.MapMarkers-0.1.0.zip`.
  It contains only `BepInEx/plugins/GK2.MapMarkers.dll`, so extracting it into the game folder (manual or Vortex) installs the mod.
- File name: `Map Markers`, version `0.1.0`, category **Main files**.
- File description: `Extract into the Graveyard Keeper 2 folder. Requires BepInEx 5.4.23.5 x64 and GK2 Mod Framework 0.1.x.`
- Changelog: paste the 0.1.0 section of `nexus/CHANGELOG.md` into the version's changelog.

## 4. Images

Primary image (thumbnail): `nexus/images/marker-showcase.png` (1280x720). It is rendered with the mod's own
generator code, **not an in-game screenshot**; its caption says so.

In-game screenshot available: `nexus/images/ingame-map.png` (3840x2160, build 25533739): NPC busts across the
town, port and village, including a spread row of guards at the square.

Further in-game screenshots still to take (with the release build, default settings, 1920x1080 or larger):

1. The world map with several NPC markers in the village. Hover one so its name label shows.
2. A zoomed crop of a few NPC busts next to the vanilla player icon and a teleport milestone, to show they line up.
3. Point-of-interest categories enabled: ores, fishing spots (one showing a fish count), caves.
4. The Mods menu → Map Markers → Settings page (General + NPC sections).
5. A category section in settings (rules / color / icon), to show it is configurable.

## 5. Permissions (decide before publishing)

The source is public on GitHub but **the repository has no LICENSE yet**. Pick one and set Nexus permissions to match. For comparison, GK2 Mod Framework uses MIT.

Suggested Nexus permissions if you go with a permissive license:
- Upload elsewhere: ask first. Modify / convert: allowed with credit.
- Assets: the mod ships no image assets; portraits are read from the game at runtime.

## 6. Tags (suggested)

`User Interface`, `Map`, `Quality of Life`, `BepInEx`

## 7. Before you press Publish

- [ ] `BepInEx/LogOutput.log` shows `MAP_MARKERS_ENABLED` with the packaged DLL, and no exceptions from `Map Markers`.
- [ ] NPC markers, fishing counts and at least one cave/ore marker verified in game on the current build.
- [ ] Screenshots uploaded and captioned.
- [ ] License decided and permissions filled.
- [ ] Source link in the description points to https://github.com/greenhat616/GraveyardKeeper2NpcMapIndicator
