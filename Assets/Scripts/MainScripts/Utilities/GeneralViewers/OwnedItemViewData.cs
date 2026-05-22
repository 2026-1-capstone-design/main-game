using UnityEngine;

public readonly struct OwnedItemViewData
{
    public readonly Sprite Icon;
    public readonly Texture RawIcon;
    public readonly GameObject ModelPrefab;
    public readonly int[] ModelCustomizeIndicates;
    public readonly string DisplayName;
    public readonly string LevelText;
    public readonly string EquippedMarkText;
    public readonly object Source;
    public readonly bool IsPlaceholder;

    public OwnedItemViewData(Sprite icon, string displayName, object source)
    {
        Icon = icon;
        RawIcon = null;
        ModelPrefab = null;
        ModelCustomizeIndicates = null;
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
        ModelPrefab = null;
        ModelCustomizeIndicates = null;
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
        ModelPrefab = null;
        ModelCustomizeIndicates = null;
        DisplayName = displayName;
        LevelText = levelText;
        EquippedMarkText = equippedMarkText;
        Source = source;
        IsPlaceholder = isPlaceholder;
    }

    public OwnedItemViewData(
        GameObject modelPrefab,
        int[] modelCustomizeIndicates,
        Sprite fallbackIcon,
        string displayName,
        string levelText,
        string equippedMarkText,
        object source
    )
    {
        Icon = fallbackIcon;
        RawIcon = null;
        ModelPrefab = modelPrefab;
        ModelCustomizeIndicates = modelCustomizeIndicates;
        DisplayName = displayName;
        LevelText = levelText;
        EquippedMarkText = equippedMarkText;
        Source = source;
        IsPlaceholder = false;
    }

    public OwnedItemViewData(
        GameObject modelPrefab,
        Sprite fallbackIcon,
        string displayName,
        string levelText,
        string equippedMarkText,
        object source
    )
        : this(modelPrefab, null, fallbackIcon, displayName, levelText, equippedMarkText, source) { }

    public static OwnedItemViewData Placeholder(Sprite icon)
    {
        return new OwnedItemViewData(icon, null, string.Empty, string.Empty, string.Empty, null, true);
    }

    public static OwnedItemViewData Placeholder(Texture rawIcon)
    {
        return new OwnedItemViewData(null, rawIcon, string.Empty, string.Empty, string.Empty, null, true);
    }
}
