# GK2 Map Markers

Graveyard Keeper 2 BepInEx mod built on [GK2 Mod Framework](https://www.nexusmods.com/graveyardkeeper2/mods/42) (0.1.x).
Draws NPCs on the world map (live position, portrait, name on hover) and provides an extensible marker
system for future categories: mines, fishing spots, teleport pillars, cave entrances.

## Install

Requires BepInEx 5.4.23.5 x64 and GK2 Mod Framework 0.1.x. Copy `GK2.MapMarkers.dll` into `BepInEx/plugins`.
Settings: main menu → **Mods** → **Map Markers** → Settings (stored in `BepInEx/config/a632079.gk2.mapmarkers.cfg`).

## Build

```powershell
dotnet build -c Release                       # builds and copies the DLL to ..\..\BepInEx\plugins
dotnet build -c Release -p:DeployToPlugins=false
dotnet build -c Release -p:GameDir="D:\Games\Graveyard Keeper 2"
```

## How it works

- Harmony postfix on `MapPageWidget.Redraw` adds an overlay under the map's `mapRect`, just below the player icon
  (fog-of-war clouds still cover it).
- Positions use the same projection as the game's player icon / milestones (`GUIElements.WorldMin/WorldMax`).
  Objects inside interiors are placed on the interior's map anchor (`worldZonePoints`) and fanned out on a ring.
- Marker art is generated at runtime (no bundled images): a semi-transparent pixel-art parchment tag with an
  ink border, drop shadow and a pointer whose tip marks the exact position. NPC portraits (`portrait_icon_*`,
  full-figure ~31x48 px) are read back from the atlas, the blue outline key is recolored to ink, and a
  head-and-shoulders bust is cropped automatically (silhouette widening = shoulders). Other categories show a
  pixel-art glyph per category (pickaxe, fish, pillar, cave, ladder, coin, cross, star, gem). `Pixel size` keeps art pixels square and crisp (screen px per art px).
- While the map is open, markers refresh every `RefreshInterval` seconds (NPCs keep moving).

## Adding categories

### Without code — rule categories

Built-in categories (ids verified against the game's balance data, build 25467846):

| Category | Default | Icon | Default rules |
| --- | --- | --- | --- |
| Teleport pillars | off (vanilla map already shows them) | Pillar | `type:TeleportMilestone` |
| Portals | on | Star | `prefix:portal_builder` |
| Mining spots | on | Pickaxe | `iron_ore`, `copper_vein`, `marble_source`, `marble_player_source`, `stone_player_source`, `clay_spot`, `sand_pit` (minus containers / conveyors) |
| Fishing spots | on | Fish | `type:Reservoir` — label shows fish left, marker fades when empty |
| Caves & descents | on | Ladder | `descent_ladder*`, `quarry_cave*`, `mine_forest_blockage`, `basement_blockage_mine` |
| Cave passages | on | Cave | scene transition GD points: `tp_cave*`, `*ruined_temple*` |
| Boulders | off (numerous) | Gem | `prefix:stone_crash` |

Trees, bushes, grass and decor are intentionally not offered (thousands of instances).

Each `Category: …` section in the Mods menu has `Enabled`, `Rules`, `Color`, `Icon`, `Scale`, `Labels`. Rules (comma-separated):

| Rule | Matches |
| --- | --- |
| `id:x` | id equals `x` |
| `prefix:x` (or bare `x`) | id starts with `x` |
| `contains:x` | id contains `x` |
| `group:x` | `WGODef.wgoGroup` equals `x` (world objects only) |
| `type:X` | `WGODef.interactionType` equals enum name `X` (world objects only) |
| `!<rule>` | exclusion |

To add a category in code, append one `RuleCategory` to `Providers/BuiltInCategories.cs`.
Load a save and press **Ctrl+F9** to write `BepInEx/MapMarkers_wgo_dump.txt` (all world-object ids with counts,
interaction types, groups, names and sample positions, plus all transit GD points) to check or write rules.

### With code — providers

```csharp
using GK2.MapMarkers.Api;

public sealed class MyProvider : IMapMarkerProvider
{
    public string Id => "my_category";
    public string DisplayName => "My category";
    public bool IsEnabled => true;
    public MarkerLabelMode LabelMode => MarkerLabelMode.Hover;

    public void Collect(MapMarkerContext context, List<MapMarker> output)
    {
        foreach (WgoData wgo in context.AllWgo)
        {
            if (wgo.id != "something") continue;
            output.Add(new MapMarker
            {
                CategoryId = Id, Key = wgo.UniqueId.Guid.ToString(),
                WorldPosition = wgo.Position, Color = Color.cyan, Scale = 1f, Label = "Something"
            });
        }
    }
}

// From your plugin (declare a BepInDependency on "a632079.gk2.mapmarkers"):
MapMarkersApi.RegisterProvider(new MyProvider());
```

## Known limitations

- Runtime behaviour has not been tested in game yet (compiled against build 25467846 assemblies).
- Name labels appear on mouse hover; with a gamepad set `Labels = Always`.
- Sprites that are tight-packed or rotated in an atlas cannot be read back and fall back to a gem glyph.
