using Game.Engine.Movement;
using Game.Features.Doors;
using Game.Features.Enemies;
using Game.Features.LevelProgress;
using Game.Features.Players;
using Game.Features.WorldObjects;
using Raylib_cs;
using static Raylib_cs.Raylib;
using Color = Raylib_cs.Color;

namespace Game.Editor;

/// <summary>
/// Level editor scene for the web/Blazor build. Uses EditorState, EditorCamera,
/// and EditorMapRenderer for all logic and rendering. Has zero ImGui/rlImGui dependencies.
/// The HTML UI is provided by Blazor components that bind to EditorState.
/// </summary>
public class WebEditorScene : IScene
{
    public readonly EditorState State;
    private readonly EditorMapRenderer _mapRenderer;
    private readonly EditorMapInteractionController _mapInteraction;

    public WebEditorScene(MapData mapData, EnemySystem enemySystem,
        DoorSystem doorSystem, SecretSystem secretSystem, Player player)
    {
        State = new EditorState(mapData, enemySystem, doorSystem, secretSystem, player);
        _mapRenderer = new EditorMapRenderer(mapData);
        var collisionSystem = new CollisionSystem(
            new LevelData(mapData),
            new CompositeMovementBlocker(doorSystem, secretSystem),
            ObjectCollisionRules.Instance);
        _mapInteraction = new EditorMapInteractionController(State, collisionSystem);
    }

    public void OnEnter()
    {
        ShowCursor();
    }

    public void OnExit()
    {
        State.IsSimulating = false;
    }

    public void Update(float deltaTime)
    {
        if (IsKeyPressed(KeyboardKey.C))
        {
            State.CursorInfoFollowsMouse = !State.CursorInfoFollowsMouse;
        }

        bool ctrlHeld = IsKeyDown(KeyboardKey.LeftControl) || IsKeyDown(KeyboardKey.RightControl);
        KeyboardKey[] numKeys = {
            KeyboardKey.One, KeyboardKey.Two, KeyboardKey.Three,
            KeyboardKey.Four, KeyboardKey.Five, KeyboardKey.Six,
            KeyboardKey.Seven, KeyboardKey.Eight, KeyboardKey.Nine
        };
        for (int i = 0; i < numKeys.Length && i < State.Layers.Count; i++)
        {
            if (IsKeyPressed(numKeys[i]))
            {
                if (ctrlHeld)
                    State.Layers[i].IsVisible = !State.Layers[i].IsVisible;
                else
                    State.SetActiveLayerIndex(i);
            }
        }

        if (IsMouseButtonReleased(MouseButton.Left))
            _mapInteraction.EndLeftMouseGesture();

        if (IsKeyPressed(KeyboardKey.Z) && ctrlHeld)
            State.Undo();
        else if (IsKeyPressed(KeyboardKey.Y) && ctrlHeld)
            State.Redo();

        State.UpdateStatusTimer(deltaTime);

        if (IsKeyPressed(KeyboardKey.P))
        {
            State.ToggleSimulation();
        }

        if (State.IsSimulating)
        {
            _mapInteraction.UpdatePlayerMovement(deltaTime);
            State.EnemySystem.Update(deltaTime);
            State.UpdateInteractablesDuringSimulation(deltaTime, IsKeyPressed(KeyboardKey.E));
        }

        State.Camera.HandleInput(deltaTime, ctrlHeld, State.IsMouseOverUI, disableKeyboardPan: State.IsSimulating);

        if (State.IsEditingPatrolPath)
            _mapInteraction.HandlePatrolPathInput(State.IsMouseOverUI);
        else
            _mapInteraction.HandleMapInput(State.IsMouseOverUI, supportsWallSelect: false);
    }

    public void Render()
    {
        BeginDrawing();
        ClearBackground(new Color(40, 40, 40, 255));

        _mapRenderer.DrawMapGrid(State.Camera);

        for (int i = 0; i < State.Layers.Count; i++)
        {
            var layer = State.Layers[i];
            if (!layer.IsVisible) continue;

            if (layer.Name == EditorState.EnemiesLayerName)
            {
                _mapRenderer.RenderEnemyLayer(
                    State.Camera, State.EnemySystem, State.IsMouseOverUI,
                    State.IsSimulating, State.DrawEnemyLineOfSight, State.ShowPatrolPaths,
                    ref State.HoveredEnemyIndex, State.SelectedEnemyIndex,
                    State.IsEditingPatrolPath, State.PatrolEditEnemyIndex, State.PatrolPathInProgress);
            }
            else if (layer.Name == EditorState.PickupsLayerName)
            {
                _mapRenderer.RenderPickupLayer(
                    State.Camera, State.IsMouseOverUI,
                    ref State.HoveredPickupIndex, State.SelectedPickupIndex);
            }
            else if (layer.Name == EditorState.ObjectsLayerName)
            {
                _mapRenderer.RenderObjectLayer(State.Camera);
            }
            else if (State.IsSimulating && layer.Name == EditorState.DoorsLayerName)
            {
                _mapRenderer.RenderLiveDoors(State.DoorSystem, State.Camera);
            }
            else
            {
                _mapRenderer.RenderLayer(layer, State.Camera);
            }
        }

        _mapRenderer.RenderPlayerIndicator(
            State.Player, State.Camera, State.MapData.Spawn.Rotation,
            State.HoveredPlayer, State.IsPlayerSelected, State.IsDraggingPlayer);

        var mouseScreen = GetMousePosition();
        var worldPos = State.Camera.ScreenToWorld(mouseScreen);
        int tileX = (int)MathF.Floor(worldPos.X);
        int tileY = (int)MathF.Floor(worldPos.Y);
        bool tileInBounds = tileX >= 0 && tileX < State.MapData.Width && tileY >= 0 && tileY < State.MapData.Height;

        if (tileInBounds && State.ShouldShowTileHighlight())
        {
            _mapRenderer.DrawTileHighlight(tileX, tileY, State.Camera);
        }

        DrawText("Level Editor - Tab to return to game", 10, GetScreenHeight() - 70, 20, Color.White);
        DrawText($"Zoom: {State.Camera.Zoom:F2}x", 10, GetScreenHeight() - 45, 20, Color.LightGray);

        if (State.IsEditingPatrolPath)
        {
            const string msg = "EDITING PATROL PATH - LMB: Add waypoint | Enter: Confirm | Esc: Cancel";
            int msgW = MeasureText(msg, 24);
            DrawText(msg, (GetScreenWidth() - msgW) / 2, GetScreenHeight() - 100, 24, Color.Yellow);
        }

        // Status message
        if (State.StatusTimer > 0 && !string.IsNullOrEmpty(State.StatusMessage))
        {
            const int fontSize = 30;
            int textWidth = MeasureText(State.StatusMessage, fontSize);
            int x = (GetScreenWidth() - textWidth) / 2;
            var statusColor = State.StatusMessage.StartsWith("Error") ? Color.Red : Color.Green;
            DrawText(State.StatusMessage, x, 55, fontSize, statusColor);
        }

        EndDrawing();
    }
}
