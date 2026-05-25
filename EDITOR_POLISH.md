# Editor polish (`polish/editor`)

Summary of level editor improvements on branch `polish/editor` (web Blazor overlay + desktop ImGui editor).

## Options and panels

- **Options** menu (web + desktop): **Show Patrol Paths** toggles authored patrol path drawing for all enemies (`EditorState.ShowPatrolPaths`; per-enemy **Show Path** still applies when global is on).
- **Debug Log** window defaults to **hidden** in the desktop ImGui editor (web was already hidden).

## Pickup palette and map rendering

- Pickup palette uses **`Objects.png`** sprites with magenta color-key (`#980088`), black background, and **64×64** icons in a **3-column** grid (same layout as tile palette).
- Pickups on the 2D map render as color-keyed sprites (not circles), with a **visual-only** Y offset of half a tile so art sits centered on the tile.
- Pickup hover/selection rings use **`tileSize * 0.35f`** (same radius as enemies).

## Door palette

- Door palette uses **`door.png`** previews at **64×64**, **2 columns**, with rotation and **G** / **S** lock badges (gold/silver).
- Palette entries and tooltips document: Door H/V, Gold H/V, Silver H/V.

## Layer switching and placement rules

- From **Enemies** layer, clicking a **pickup** or **door** on the map switches to **Pickups** or **Doors** and selects that entity (no stray enemy placement).
- Pickup/door palette clicks switch to the correct layer.
- **Pickups** cannot be placed or dragged onto **walls** or **doors**.

## Player spawn

- **Player** is click-and-draggable on the map (any layer, not while simulating).
- Spawn stored on **`MapData`** (`PlayerSpawnTileX/Y`, `PlayerSpawnWorldY`) and serialized in JSON as **`PlayerSpawn`**.
- World position uses **tile anchor** (`tileX * QuadSize`) so the editor’s half-tile screen centering matches stored data; game restart/load uses the same spawn.
- Hover: yellow rings; drag: white rings (same pattern as enemies).

## Selection and tile highlight

- **Exclusive selection**: selecting an enemy clears pickup selection (and vice versa).
- Yellow **tile-under-cursor** highlight is hidden when hovering or **dragging** player, enemy, or pickup.

## Files touched (main)

| Area | Files |
|------|--------|
| State / serialization | `EditorState.cs`, `MapData.cs`, `LevelSerializer.cs`, `DoorTileEncoding.cs`, `PickupSprites.cs` |
| Rendering | `EditorMapRenderer.cs`, `EditorGui.cs` |
| Input | `WebEditorScene.cs`, `LevelEditorScene.cs` |
| Web UI | `WebEditorMenuBar.razor`, `WebEditorPickupPalette.razor`, `WebEditorDoorPalette.razor`, `editor.css` |
| Game | `World.cs` |

## JSON level format addition

```json
"PlayerSpawn": {
  "TileX": 30,
  "TileY": 28,
  "WorldY": 2
}
```

Older levels without `PlayerSpawn` keep defaults (30, 28, 2).
