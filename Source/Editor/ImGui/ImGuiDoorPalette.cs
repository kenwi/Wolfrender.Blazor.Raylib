using System.Numerics;
using Game.Features.Doors;
using Game.Features.Pickups;
using ImGuiNET;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Editor;

/// <summary>Desktop ImGui door-layer palette (icons + selection). Mirrors Components/Editor/Doors.</summary>
public sealed class ImGuiDoorPalette
{
    private Dictionary<uint, RenderTexture2D>? _icons;

    public void EnsureIcons(MapData mapData)
    {
        if (_icons != null) return;
        int size = PickupSprites.PaletteIconSize;
        _icons = new Dictionary<uint, RenderTexture2D>();
        foreach (var entry in DoorTileEncoding.PaletteEntries)
        {
            if (entry.TextureIndex < 0 || entry.TextureIndex >= mapData.TileTextures.Count)
                continue;

            var doorTex = mapData.TileTextures[entry.TextureIndex];
            if (doorTex.Id == 0) continue;

            var rt = LoadRenderTexture(size, size);
            BeginTextureMode(rt);
            ClearBackground(Color.Black);
            DrawDoorPaletteIcon(doorTex, size, entry.Vertical, entry.LockKind);
            EndTextureMode();
            _icons[entry.Id] = rt;
        }
    }

    public void RenderButtons(EditorState editorState, ref uint selectedTileId, float buttonSize)
    {
        EnsureIcons(editorState.MapData);
        bool hasIcons = _icons is { Count: > 0 };
        int columns = DoorTileEncoding.PaletteColumns;

        int index = 0;
        foreach (var entry in DoorTileEncoding.PaletteEntries)
        {
            if (index % columns != 0)
                ImGui.SameLine();

            ImGui.PushID(index + 600);

            bool selected = selectedTileId == entry.Id;
            if (selected)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.5f, 0.8f, 1f));
                ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(1f, 1f, 0f, 1f));
                ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 3f);
            }

            bool picked = false;
            if (hasIcons && _icons!.TryGetValue(entry.Id, out var icon))
            {
                var texId = new IntPtr(icon.Texture.Id);
                if (ImGui.ImageButton($"{entry.Id}_door", texId, new Vector2(buttonSize, buttonSize),
                        new Vector2(0, 1), new Vector2(1, 0)))
                    picked = true;
            }
            else if (ImGui.Button($"{entry.ShortLabel}", new Vector2(buttonSize, buttonSize)))
            {
                picked = true;
            }

            if (picked)
            {
                selectedTileId = entry.Id;
                editorState.SwitchToDoorLayer();
            }

            if (selected)
            {
                ImGui.PopStyleVar();
                ImGui.PopStyleColor(2);
            }

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip($"{entry.ShortLabel}\n{entry.Description}\nTile ID: {entry.Id}");

            ImGui.PopID();
            index++;
        }
    }

    private static void DrawDoorPaletteIcon(Texture2D doorTex, int size, bool vertical, DoorLockKind lockKind)
    {
        float half = size / 2f;
        if (vertical)
        {
            DrawTexturePro(
                doorTex,
                new Rectangle(0, 0, doorTex.Width, doorTex.Height),
                new Rectangle(half, half, size, size),
                new Vector2(half, half),
                90f,
                Color.White);
        }
        else
        {
            DrawTexturePro(
                doorTex,
                new Rectangle(0, 0, doorTex.Width, doorTex.Height),
                new Rectangle(0, 0, size, size),
                Vector2.Zero,
                0f,
                Color.White);
        }

        if (lockKind != DoorLockKind.None)
            DrawDoorLockBadge(size, lockKind);
    }

    private static void DrawDoorLockBadge(int tileSize, DoorLockKind lockKind)
    {
        var lockColor = lockKind == DoorLockKind.Gold
            ? new Color(255, 210, 40, 255)
            : new Color(200, 220, 255, 255);
        string label = lockKind == DoorLockKind.Gold ? "G" : "S";
        float cx = tileSize * 0.75f;
        float cy = tileSize * 0.25f;
        float radius = tileSize * 0.2f;
        DrawCircle((int)cx, (int)cy, radius, lockColor);
        int fontSize = (int)(tileSize * 0.35f);
        DrawText(label, (int)(tileSize * 0.65f), (int)(tileSize * 0.12f), fontSize, Color.Black);
    }
}
