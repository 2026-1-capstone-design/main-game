public sealed class MarketGladiatorOffer
{
    public int SlotIndex { get; }
    public OwnedGladiatorData Gladiator { get; } // 시장 슬롯에 진열된 검투사 preview 데이터
    public int Price { get; }
    public bool IsSold { get; private set; } // 해당 슬롯이 이미 구매 처리됐는지 나타낸다.

    // 하루가 바뀌기 전까지는 sold 상태(여부)가 유지됨. 아래의 MarkSold()로 판매 후 mark하여 그 슬롯을 사용 불가 상태로 전환

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
