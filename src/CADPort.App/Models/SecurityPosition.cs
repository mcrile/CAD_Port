namespace CADPort.App.Models
{
    /// <summary>
    /// One cell of the workspace: the holdings of a single security inside a single
    /// account, plus its model target and editable post-trade state.
    /// </summary>
    public class SecurityPosition
    {
        public Account Account { get; set; }
        public Security Security { get; set; }

        public List<TaxLot> Lots { get; } = new();

        /// <summary>Model target weight for this security (fraction of the account / household).</summary>
        public double TargetWeight { get; set; }

        /// <summary>
        /// The editable post-trade share count. Trades are derived from
        /// (PostTradeShares - CurrentShares). Always whole shares.
        /// </summary>
        public double PostTradeShares { get; set; }

        public double CurrentShares => Lots.Sum(l => l.Shares);
        public double CurrentMarketValue => Lots.Sum(l => l.MarketValue);
        public double PostTradeMarketValue => PostTradeShares * Security.Price;

        public bool IsCash => Security != null && Security.IsCash;

        /// <summary>
        /// Lots ordered for selling: largest loss% first (top of stack, most
        /// attractive to harvest) through largest gain% last (most expensive to sell).
        /// </summary>
        public IEnumerable<TaxLot> LotsInSellOrder => Lots.OrderBy(l => l.GainLossPercent);

        /// <summary>
        /// Lots ordered bottom-to-top for stacking: the reverse of the sell order
        /// so that the most attractive lot ends up visually on top.
        /// </summary>
        public IEnumerable<TaxLot> LotsBottomToTop => Lots.OrderByDescending(l => l.GainLossPercent);
    }
}
