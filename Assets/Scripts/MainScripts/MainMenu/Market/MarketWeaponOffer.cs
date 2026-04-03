public sealed class MarketWeaponOffer
{
    public int SlotIndex { get; }
    public OwnedWeaponData Weapon { get; }
    public int Price { get; }
    public bool IsSold { get; private set; }

    public bool IsAvailable => Weapon != null && !IsSold;

    public MarketWeaponOffer(int slotIndex, OwnedWeaponData weapon, int price)
    {
        SlotIndex = slotIndex;
        Weapon = weapon;
        Price = price < 0 ? 0 : price;
        IsSold = false;
    }

    public void MarkSold()
    {
        IsSold = true;
    }
}