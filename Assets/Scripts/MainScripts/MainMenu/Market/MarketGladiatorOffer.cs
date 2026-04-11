public sealed class MarketGladiatorOffer
{
    public int SlotIndex { get; }
    public OwnedGladiatorData Gladiator { get; }
    public int Price { get; }
    public bool IsSold { get; private set; }

    public bool IsAvailable => Gladiator != null && !IsSold;

    public MarketGladiatorOffer(int slotIndex, OwnedGladiatorData gladiator, int price)
    {
        SlotIndex = slotIndex;
        Gladiator = gladiator;
        Price = price < 0 ? 0 : price;
        IsSold = false;
    }

    public void MarkSold()
    {
        IsSold = true;
    }
}
