public sealed class MarketArtifactOffer
{
    public int SlotIndex { get; }
    public PerkSO Artifact { get; }
    public int Price { get; }
    public bool IsSold { get; private set; }

    public bool IsAvailable => Artifact != null && !IsSold;

    public MarketArtifactOffer(int slotIndex, PerkSO artifact, int price)
    {
        SlotIndex = slotIndex;
        Artifact = artifact;
        Price = price < 0 ? 0 : price;
        IsSold = false;
    }

    public void MarkSold()
    {
        IsSold = true;
    }
}
