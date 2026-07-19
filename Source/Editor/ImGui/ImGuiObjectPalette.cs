using System.Numerics;
using Game.Features.Pickups;
using Game.Features.WorldObjects;
using ImGuiNET;
using Raylib_cs;
using static Raylib_cs.Raylib;

namespace Game.Editor;

/// <summary>Desktop ImGui objects-layer palette. Mirrors Components/Editor/Objects.</summary>
public sealed class ImGuiObjectPalette
{
    private RenderTexture2D[]? _icons;

    public void EnsureIcons(MapData mapData)
    {
        if (_icons != null) return;
        if (mapData.GameTextures.Count <= PickupSprites.ObjectsTextureIndex) return;

        var objectsTex = mapData.GameTextures[PickupSprites.ObjectsTextureIndex];
        if (objectsTex.Id == 0) return;

        int size = ObjectSprites.PaletteIconSize;
        _icons = new RenderTexture2D[ObjectSprites.ObjectCount];
        for (int i = 0; i < ObjectSprites.ObjectCount; i++)
        {
            var rt = LoadRenderTexture(size, size);
            BeginTextureMode(rt);
            ClearBackground(Color.Black);
            PrimitiveRenderer.DrawScreenSprite(
                objectsTex,
                ObjectSprites.GetFrameRect(i),
                new Rectangle(0, 0, size, size),
                Color.White);
            EndTextureMode();
            _icons[i] = rt;
        }
    }

    public void RenderButtons(EditorState editorState, ref uint selectedTileId, float buttonSize)
    {
        EnsureIcons(editorState.MapData);
        bool hasIcons = _icons != null;
        int columns = ObjectSprites.PaletteColumns;

        for (int i = 0; i < ObjectSprites.ObjectCount; i++)
        {
            if (i % columns != 0)
                ImGui.SameLine();

            uint objectId = (uint)(i + 1);
            ImGui.PushID(i + 700);

            bool selected = selectedTileId == objectId;
            if (selected)
            {
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.2f, 0.5f, 0.8f, 1f));
                ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(1f, 1f, 0f, 1f));
                ImGui.PushStyleVar(ImGuiStyleVar.FrameBorderSize, 3f);
            }

            bool picked = false;
            if (hasIcons && _icons![i].Texture.Id != 0)
            {
                var texId = new IntPtr(_icons[i].Texture.Id);
                if (ImGui.ImageButton($"object_{objectId}", texId, new Vector2(buttonSize, buttonSize),
                        new Vector2(0, 1), new Vector2(1, 0)))
                    picked = true;
            }
            else if (ImGui.Button($"Obj {objectId}", new Vector2(buttonSize, buttonSize)))
            {
                picked = true;
            }

            if (picked)
            {
                selectedTileId = objectId;
                editorState.SwitchToObjectLayer();
            }

            if (selected)
            {
                ImGui.PopStyleVar();
                ImGui.PopStyleColor(2);
            }

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip($"Object ID: {objectId}");

            ImGui.PopID();
        }
    }
}
