namespace Game.Core;

/// <summary>Indices into <see cref="MapData.GameTextures"/>.</summary>
public static class GameTextureIndex
{
    public const int EnemyGuard = 0;
    public const int Weapons = 1;
    /// <summary>Legacy <c>Objects.png</c> used by pickup sprites.</summary>
    public const int Objects = 2;
    /// <summary>Dog spritesheet. Placeholder loads the guard sheet until dedicated art ships.</summary>
    public const int EnemyDog = 3;
    /// <summary>Officer spritesheet. Placeholder loads the guard sheet until dedicated art ships.</summary>
    public const int EnemyOfficer = 4;
    /// <summary>Runtime atlas of Wolf static sprites 002..049 for the objects layer.</summary>
    public const int StaticObjects = 5;
}
