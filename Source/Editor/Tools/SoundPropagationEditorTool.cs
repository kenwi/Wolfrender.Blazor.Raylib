using System.Numerics;
using Game.Features.Doors;
using Game.Features.SoundPropagation;

namespace Game.Editor;

/// <summary>
/// Desktop editor sound-reach visualizer (pick origin, overlay reachable tiles briefly).
/// </summary>
public sealed class SoundPropagationEditorTool
{
    public const float OverlayDurationSeconds = 2f;

    private readonly MapData _mapData;
    private readonly DoorSystem _doorSystem;
    private readonly Func<bool> _isSimulating;
    private readonly Action<string> _setStatus;
    private readonly Action _notifyChanged;
    private readonly Action _cancelSiblingPick;

    public bool IsPicking { get; private set; }
    public List<Vector2>? OverlayTiles { get; private set; }
    public float OverlayShowUntil { get; private set; }

    public SoundPropagationEditorTool(
        MapData mapData,
        DoorSystem doorSystem,
        Func<bool> isSimulating,
        Action<string> setStatus,
        Action notifyChanged,
        Action cancelSiblingPick)
    {
        _mapData = mapData;
        _doorSystem = doorSystem;
        _isSimulating = isSimulating;
        _setStatus = setStatus;
        _notifyChanged = notifyChanged;
        _cancelSiblingPick = cancelSiblingPick;
    }

    public void StartPick()
    {
        _cancelSiblingPick();
        IsPicking = true;
        _notifyChanged();
    }

    public void CancelPick()
    {
        if (!IsPicking) return;
        IsPicking = false;
        _notifyChanged();
    }

    /// <summary>Cancel pick without notifying (used when another tool starts picking).</summary>
    public void CancelPickSilent()
    {
        IsPicking = false;
    }

    public void RunTest(int tileX, int tileY, float now)
    {
        IsPicking = false;

        if (tileX < 0 || tileX >= _mapData.Width || tileY < 0 || tileY >= _mapData.Height)
            return;

        bool treatAllDoorsClosed = !_isSimulating();
        var reach = SoundPropagation.ComputeReach(
            _mapData, _doorSystem.Doors, tileX, tileY, treatAllDoorsClosed);

        if (reach.Count == 0)
        {
            OverlayTiles = null;
            OverlayShowUntil = 0;
            _setStatus("Sound propagation: invalid origin (wall or closed door)");
            _notifyChanged();
            return;
        }

        OverlayTiles = reach
            .Select(t => new Vector2(t.X, t.Y))
            .ToList();
        OverlayShowUntil = now + OverlayDurationSeconds;
        _setStatus($"Sound reached {reach.Count} tiles");
        _notifyChanged();
    }

    public void TickOverlay(float now)
    {
        if (OverlayTiles == null || OverlayShowUntil <= 0)
            return;

        if (now >= OverlayShowUntil)
        {
            OverlayTiles = null;
            OverlayShowUntil = 0;
            _notifyChanged();
        }
    }

    public void Clear()
    {
        IsPicking = false;
        OverlayTiles = null;
        OverlayShowUntil = 0;
        _notifyChanged();
    }
}
