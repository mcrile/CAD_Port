namespace CADPort.App.Models
{
    /// <summary>The impact of a sale on a single tax lot.</summary>
    public class LotImpact
    {
        public TaxLot Lot { get; set; }
        public double SharesSold { get; set; }
        public double RealizedGainLoss { get; set; }
    }

    /// <summary>
    /// A deterministic trade derived from the difference between current holdings
    /// and the post-trade plane. Held separately from holdings until accepted.
    /// </summary>
    public class TradeProposal
    {
        public Account Account { get; set; }
        public Security Security { get; set; }
        public TradeSide Side { get; set; }

        /// <summary>Whole shares to trade.</summary>
        public double Quantity { get; set; }

        public double MarketValue { get; set; }

        /// <summary>Realized gain/loss (sales only; zero for purchases).</summary>
        public double RealizedGainLoss { get; set; }

        public List<LotImpact> LotsImpacted { get; } = new();

        public bool IsTaxable => Account != null && Account.IsTaxable;
    }
}
