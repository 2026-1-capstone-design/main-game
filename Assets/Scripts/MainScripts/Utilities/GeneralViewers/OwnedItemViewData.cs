using UnityEngine;

public readonly struct OwnedItemViewData
{
    public readonly Sprite Icon;
    public readonly Texture RawIcon;
    public readonly string DisplayName;
    public readonly string LevelText;
    public readonly string EquippedMarkText;
    public readonly object Source;
    public readonly bool IsPlaceholder;

    public OwnedItemViewData(Sprite icon, string displayName, object source)
    {
        Icon = icon;
        RawIcon = null;
        DisplayName = displayName;
        LevelText = string.Empty;
        EquippedMarkText = string.Empty;
        Source = source;
        IsPlaceholder = false;
    }

    public OwnedItemViewData(Sprite icon, string displayName, string levelText, string equippedMarkText, object source)
    {
        Icon = icon;
        RawIcon = null;
        DisplayName = displayName;
        LevelText = levelText;
        EquippedMarkText = equippedMarkText;
        Source = source;
        IsPlaceholder = false;
    }

    public OwnedItemViewData(
        Sprite icon,
        Texture rawIcon,
        string displayName,
        string levelText,
        string equippedMarkText,
        object source,
        bool isPlaceholder
    )
    {
        Icon = icon;
        RawIcon = rawIcon;
        DisplayName = displayName;
        LevelText = levelText;
        EquippedMarkText = equippedMarkText;
        Source = source;
        IsPlaceholder = isPlaceholder;
    }

    public static OwnedItemViewData Placeholder(Sprite icon)
    {
        return new OwnedItemViewData(icon, null, string.Empty, string.Empty, string.Empty, null, true);
    }

    public static OwnedItemViewData Placeholder(Texture rawIcon)
    {
        return new OwnedItemViewData(null, rawIcon, string.Empty, string.Empty, string.Empty, null, true);
    }
}
