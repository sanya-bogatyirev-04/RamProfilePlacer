namespace RamProfilePlacer.Core.Geometry;

/// <summary>
/// Сторона проёма для размещения профиля.
/// </summary>
public enum OpeningSide
{
    /// <summary>Нижний профиль (порог/подоконник). Для дверей не используется.</summary>
    Bottom,

    /// <summary>Верхний профиль.</summary>
    Top,

    /// <summary>Левый профиль (если смотреть изнутри на стену).</summary>
    Left,

    /// <summary>Правый профиль (если смотреть изнутри на стену).</summary>
    Right
}
