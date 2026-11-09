using System.Numerics;
using Game.Core.Level;
using Game.Features.LevelProgress;
using ImGuiNET;
using static Raylib_cs.Raylib;

namespace Game.Editor;

/// <summary>Desktop ImGui wall / secret-wall properties panel.</summary>
public sealed class ImGuiWallProperties
{
    private const float FaceCellWidth = 118f;
    private const float FaceThumbSize = 40f;

    public void Render(EditorState state, ref bool showWindow, float guiScale)
    {
        if (!state.ShouldShowWallPropertiesPanel(showWindow))
            return;

        int wallX = state.SecretWallTool.SelectedTileX;
        int wallY = state.SecretWallTool.SelectedTileY;
        bool single = state.IsSingleWallSelected;
        uint? commonDefault = state.TryGetCommonSelectedDefaultTile();
        var secret = state.GetSelectedSecretPlacement();
        bool isSecret = secret != null;
        SecretWallDirection direction = secret?.Direction ?? SecretWallDirection.North;
        int travelTiles = secret?.TravelTiles ?? 1;

        ImGui.SetNextWindowPos(new Vector2(GetScreenWidth() - 300, 420), ImGuiCond.FirstUseEver);
        ImGui.Begin("Wall Properties", ref showWindow, ImGuiWindowFlags.AlwaysAutoResize);
        ImGui.SetWindowFontScale(guiScale);

        if (single)
            ImGui.Text($"Wall ({wallX}, {wallY})");
        else
            ImGui.Text($"{state.SelectedWallCount} walls selected");

        ImGui.Separator();
        ImGui.Text("Faces");
        ImGui.TextDisabled(single
            ? "Pick a tile in the palette, then Assign."
            : "Assign applies the selected tile to every selected wall.");

        DrawFaceCompass(state, commonDefault);

        if (ImGui.Button("Reset all faces", new Vector2(FaceCellWidth * 3f, 0f)))
            state.ResetSelectedWallFaces();

        if (single)
        {
            ImGui.Separator();

            if (ImGui.Checkbox("Secret wall", ref isSecret))
            {
                state.SetWallSecret(isSecret, direction, travelTiles);
                secret = state.GetSelectedSecretPlacement();
                isSecret = secret != null;
                direction = secret?.Direction ?? SecretWallDirection.North;
                travelTiles = secret?.TravelTiles ?? 1;
            }

            if (isSecret)
            {
                ImGui.Spacing();
                ImGui.Text("Travel direction:");
                foreach (SecretWallDirection value in Enum.GetValues<SecretWallDirection>())
                {
                    if (ImGui.RadioButton(value.ToString(), direction == value))
                    {
                        direction = value;
                        travelTiles = state.ClampSecretTravelTiles(wallX, wallY, direction, travelTiles);
                        state.SetWallSecret(true, direction, travelTiles);
                    }
                }

                int maxTravel = state.GetMaxSecretTravelTiles(wallX, wallY, direction);
                if (ImGui.InputInt("Travel tiles", ref travelTiles))
                {
                    travelTiles = state.ClampSecretTravelTiles(wallX, wallY, direction, travelTiles);
                    state.SetWallSecret(true, direction, travelTiles);
                }

                ImGui.TextDisabled($"Max in direction: {maxTravel}");
            }
        }

        ImGui.End();
    }

    private static void DrawFaceCompass(EditorState state, uint? commonDefault)
    {
        var flags = ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.NoPadOuterX;
        if (!ImGui.BeginTable("face_compass", 3, flags))
            return;

        ImGui.TableSetupColumn("west", ImGuiTableColumnFlags.WidthFixed, FaceCellWidth);
        ImGui.TableSetupColumn("center", ImGuiTableColumnFlags.WidthFixed, FaceCellWidth);
        ImGui.TableSetupColumn("east", ImGuiTableColumnFlags.WidthFixed, FaceCellWidth);

        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(1);
        DrawFaceCell(state, WallFace.North);

        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(0);
        DrawFaceCell(state, WallFace.West);
        ImGui.TableSetColumnIndex(1);
        DrawDefaultCell(commonDefault, state);
        ImGui.TableSetColumnIndex(2);
        DrawFaceCell(state, WallFace.East);

        ImGui.TableNextRow();
        ImGui.TableSetColumnIndex(1);
        DrawFaceCell(state, WallFace.South);

        ImGui.EndTable();
    }

    private static void DrawFaceCell(EditorState state, WallFace face)
    {
        uint? commonTile = state.TryGetCommonSelectedWallFaceTile(face);
        bool? isOverride = state.TryGetSelectedWallFaceIsOverride(face);

        ImGui.PushID((int)face);
        ImGui.BeginGroup();
        CenterText(face.ToString());
        CenterThumbnail(state, commonTile ?? 0, FaceThumbSize);
        CenterText(DescribeFace(commonTile, isOverride), disabled: true);
        CenterButton("Assign", () => state.AssignSelectedTileToWallFace(face));
        CenterButton("Reset", () => state.ResetSelectedWallFace(face));
        ImGui.EndGroup();
        ImGui.PopID();
    }

    private static void DrawDefaultCell(uint? commonDefault, EditorState state)
    {
        ImGui.BeginGroup();
        CenterText("Default");
        CenterThumbnail(state, commonDefault ?? 0, FaceThumbSize);
        CenterText(
            commonDefault is uint id and > 0 ? $"ID {id}" : "mixed",
            disabled: true);
        ImGui.EndGroup();
    }

    private static string DescribeFace(uint? commonTile, bool? isOverride)
    {
        if (commonTile == null || isOverride == null)
            return "mixed";
        return isOverride.Value
            ? $"ID {commonTile.Value}"
            : $"default ({commonTile.Value})";
    }

    private static void CenterText(string text, bool disabled = false)
    {
        float textWidth = ImGui.CalcTextSize(text).X;
        float offset = MathF.Max(0f, (FaceCellWidth - textWidth) * 0.5f);
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offset);
        if (disabled)
            ImGui.TextDisabled(text);
        else
            ImGui.Text(text);
    }

    private static void CenterThumbnail(EditorState state, uint tileId, float size)
    {
        float offset = MathF.Max(0f, (FaceCellWidth - size) * 0.5f);
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offset);
        DrawTileThumbnail(state, tileId, size);
    }

    private static void CenterButton(string label, Action onClick)
    {
        var size = new Vector2(FaceCellWidth - 12f, 0f);
        float offset = MathF.Max(0f, (FaceCellWidth - size.X) * 0.5f);
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offset);
        if (ImGui.Button(label, size))
            onClick();
    }

    private static void DrawTileThumbnail(EditorState state, uint tileId, float size)
    {
        int index = WallTilePalette.GetTextureIndex(tileId);
        if (index < 0 || index >= state.MapData.TileTextures.Count)
        {
            ImGui.Dummy(new Vector2(size, size));
            return;
        }

        var texture = state.MapData.TileTextures[index];
        ImGui.Image(new IntPtr(texture.Id), new Vector2(size, size), Vector2.Zero, Vector2.One);
    }
}
