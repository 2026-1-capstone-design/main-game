using UnityEngine;

public readonly struct OwnedItemViewData
{
    public readonly Sprite Icon;
    public readonly string DisplayName;
    public readonly string LevelText;
    public readonly string EquippedMarkText;
    public readonly object Source;

    public OwnedItemViewData(Sprite icon, string displayName, object source)
    {
        Icon = icon;
        DisplayName = displayName;
        LevelText = string.Empty;
        EquippedMarkText = string.Empty;
        Source = source;
    }

    public OwnedItemViewData(
        Sprite icon,
        string displayName,
        string levelText,
        string equippedMarkText,
        object source)
    {
        Icon = icon;
        DisplayName = displayName;
        LevelText = levelText;
        EquippedMarkText = equippedMarkText;
        Source = source;
    }
}
