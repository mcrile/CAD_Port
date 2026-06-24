namespace CADPort.App.Models
{
    /// <summary>
    /// The complete workspace state: securities (X), accounts (Y), the model,
    /// and the per-cell positions. The aggregate household row is computed on the
    /// fly and is informational only.
    /// </summary>
    public class Portfolio
    {
        /// <summary>X axis. Index 0 is always cash.</summary>
        public List<Security> Securities { get; } = new();

        /// <summary>Real accounts (excludes the synthetic aggregate row).</summary>
        public List<Account> Accounts { get; } = new();

        /// <summary>The informational aggregate household row.</summary>
        public Account Aggregate { get; set; }

        /// <summary>Household model weights keyed by security symbol (sum to 1.0).</summary>
        public Dictionary<string, double> ModelWeights { get; } = new();

        /// <summary>Every account x security cell, for real accounts only.</summary>
        public List<SecurityPosition> Positions { get; } = new();

        /// <summary>Flat tax rate used for the estimated tax cost metric.</summary>
        public double TaxRate { get; set; } = 0.25;

        public SecurityPosition GetPosition(Account account, Security security)
            => Positions.FirstOrDefault(p => p.Account == account && p.Security == security);

        public SecurityPosition GetCashPosition(Account account)
            => Positions.FirstOrDefault(p => p.Account == account && p.Security.IsCash);

        /// <summary>Current total value of an account (all securities incl. cash).</summary>
        public double AccountValue(Account account)
            => Positions.Where(p => p.Account == account).Sum(p => p.CurrentMarketValue);

        /// <summary>Account value after its external cash adjustment is applied.</summary>
        public double AccountPostValue(Account account)
            => AccountValue(account) + account.CashAdjustment;

        /// <summary>Current household value (sum of real accounts).</summary>
        public double HouseholdValue
            => Accounts.Sum(AccountValue);

        /// <summary>Household value after all external cash adjustments.</summary>
        public double HouseholdPostValue
            => Accounts.Sum(AccountPostValue);

        // ---- Aggregate (household) helpers -------------------------------------

        public double AggregateCurrentShares(Security security)
            => Accounts.Sum(a => GetPosition(a, security)?.CurrentShares ?? 0.0);

        public double AggregateCurrentValue(Security security)
            => Accounts.Sum(a => GetPosition(a, security)?.CurrentMarketValue ?? 0.0);

        public double AggregatePostTradeShares(Security security)
            => Accounts.Sum(a => GetPosition(a, security)?.PostTradeShares ?? 0.0);

        public double AggregatePostTradeValue(Security security)
            => AggregatePostTradeShares(security) * security.Price;

        /// <summary>Household model target value for a security.</summary>
        public double HouseholdTargetValue(Security security)
            => ModelWeight(security) * HouseholdPostValue;

        public double ModelWeight(Security security)
            => ModelWeights.TryGetValue(security.Symbol, out var w) ? w : 0.0;

        /// <summary>All lots for a security across every real account (for the aggregate stack).</summary>
        public IEnumerable<TaxLot> AggregateLots(Security security)
            => Accounts.SelectMany(a => GetPosition(a, security)?.Lots ?? Enumerable.Empty<TaxLot>());
    }
}
