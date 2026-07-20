namespace Game.Features.Doors;

/// <summary>
/// Minimal key inventory for door interaction.
/// Owned by Doors; implemented by the play actor (e.g. Player).
/// </summary>
public interface IDoorKeyInventory
{
    bool HasGoldKey { get; }
    bool HasSilverKey { get; }
}
