namespace CADPort.App.Models
{
    /// <summary>
    /// A portfolio account (one row on the Y axis). The synthetic Aggregate row
    /// is also represented by an Account with <see cref="IsAggregate"/> set.
    /// </summary>
    public class Account
    {
        public string Name { get; set; }

        /// <summary>Taxable accounts get gain/loss coloring; tax-exempt render white.</summary>
        public bool IsTaxable { get; set; } = true;

        /// <summary>
        /// External cash flow the rebalance must respect.
        /// Positive == add cash (deposit), negative == raise cash (withdrawal).
        /// </summary>
        public double CashAdjustment { get; set; }

        /// <summary>Position on the Y axis.</summary>
        public int YIndex { get; set; }

        /// <summary>True only for the informational aggregate household row.</summary>
        public bool IsAggregate { get; set; }

        public override string ToString() => Name;
    }
}
