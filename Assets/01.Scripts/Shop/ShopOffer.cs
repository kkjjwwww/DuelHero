namespace DuelHero.Shop
{
    public sealed class ShopOffer
    {
        public string CardId { get; }
        public int Price { get; }
        public bool Sold { get; internal set; }
        internal ShopOffer(string cardId, int price) { CardId = cardId; Price = price; }
    }
}
