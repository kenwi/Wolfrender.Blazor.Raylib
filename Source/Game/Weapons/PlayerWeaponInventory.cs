namespace Game.Weapons;

public sealed class PlayerWeaponInventory
{
    private readonly HashSet<WeaponId> _owned = new();

    public WeaponId ActiveWeapon { get; set; } = WeaponId.Knife;

    public WeaponId DefaultWeapon => WeaponId.Knife;

    public bool IsOwned(WeaponId id) => _owned.Contains(id);

    public void Grant(WeaponId id) => _owned.Add(id);

    public void Reset()
    {
        _owned.Clear();
        _owned.Add(WeaponId.Knife);
        _owned.Add(WeaponId.Pistol);
        ActiveWeapon = DefaultWeapon;
    }

    public bool TrySetActive(WeaponId id)
    {
        if (!_owned.Contains(id))
            return false;
        ActiveWeapon = id;
        return true;
    }
}
