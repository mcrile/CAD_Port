namespace CADPort.App.Models
{
    /// <summary>
    /// The fundamental geometric primitive of the application. Each lot is rendered
    /// as an individual 3D box whose height is proportional to its market value.
    /// </summary>
    public class TaxLot
    {
        public Security Security { get; set; }
        public Account Account { get; set; }

        public double Shares { get; set; }

        /// <summary>Acquisition cost per share. Cash uses 1.0 (no embedded gain/loss).</summary>
        public double CostBasisPerShare { get; set; }

        public DateTime Acquired { get; set; }

        public double MarketValue => Shares * Security.Price;
        public double CostBasis => Shares * CostBasisPerShare;
        public double UnrealizedGainLoss => MarketValue - CostBasis;

        /// <summary>
        /// Unrealized return relative to cost basis. Used for stack ordering
        /// (largest loss% at the top, largest gain% at the bottom).
        /// </summary>
        public double GainLossPercent => CostBasis > 0 ? UnrealizedGainLoss / CostBasis : 0.0;

        /// <summary>
        /// Tax-aware sort key. A lot is only ordered by its embedded gain/loss when
        /// selling it actually has a tax consequence - i.e. a taxable, non-cash lot.
        /// Tax-exempt accounts and cash sort as neutral (0), so flipping an account
        /// to tax-exempt drops its lots out of the harvest ordering.
        /// </summary>
        public double SortGainLossPercent =>
            (Account != null && Account.IsTaxable && !Security.IsCash) ? GainLossPercent : 0.0;

        /// <summary>
        /// Unrealized gain/loss divided by market value. Drives color intensity
        /// per the spec.
        /// </summary>
        public double GainLossIntensity => MarketValue > 0 ? UnrealizedGainLoss / MarketValue : 0.0;
    }
}
