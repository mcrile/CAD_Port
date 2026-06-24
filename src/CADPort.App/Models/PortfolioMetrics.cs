namespace CADPort.App.Models
{
    /// <summary>Snapshot of household-level metrics, recomputed whenever planes change.</summary>
    public class PortfolioMetrics
    {
        public double CurrentPortfolioValue { get; set; }
        public double CurrentCash { get; set; }
        public double ProposedCash { get; set; }
        public double RealizedGains { get; set; }
        public double RealizedLosses { get; set; }   // negative number
        public double NetTaxImpact { get; set; }      // net realized gain/loss
        public double HarvestedLosses { get; set; }   // magnitude of realized losses
        public double EstimatedTaxCost { get; set; }  // taxable accounts only
    }
}
