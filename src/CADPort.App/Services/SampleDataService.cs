using CADPort.App.Models;

namespace CADPort.App.Services
{
    /// <summary>
    /// Builds a realistic seeded household: three accounts (two taxable, one
    /// tax-exempt) holding cash plus four ETFs, with lots deliberately spanning
    /// embedded gains and losses so the coloring and harvesting story is visible.
    /// </summary>
    public static class SampleDataService
    {
        public static Portfolio Create()
        {
            var portfolio = new Portfolio { TaxRate = 0.25 };

            // ---- Securities (X axis, cash first) -------------------------------
            var cash = new Security { Symbol = "CASH", Name = "Cash", Price = 1.0, IsCash = true, XIndex = 0 };
            var spy = new Security { Symbol = "SPY", Name = "US Equity", Price = 550.0, XIndex = 1 };
            var vea = new Security { Symbol = "VEA", Name = "Intl Equity", Price = 50.0, XIndex = 2 };
            var bnd = new Security { Symbol = "BND", Name = "US Bonds", Price = 72.0, XIndex = 3 };
            var vnq = new Security { Symbol = "VNQ", Name = "Real Estate", Price = 85.0, XIndex = 4 };
            portfolio.Securities.AddRange(new[] { cash, spy, vea, bnd, vnq });

            // ---- Household model (sums to 1.0) ---------------------------------
            portfolio.ModelWeights["CASH"] = 0.05;
            portfolio.ModelWeights["SPY"] = 0.40;
            portfolio.ModelWeights["VEA"] = 0.20;
            portfolio.ModelWeights["BND"] = 0.25;
            portfolio.ModelWeights["VNQ"] = 0.10;

            // ---- Accounts (Y axis) ---------------------------------------------
            var accountA = new Account { Name = "Account A", IsTaxable = true, CashAdjustment = 0, YIndex = 0 };
            var accountB = new Account { Name = "Account B", IsTaxable = true, CashAdjustment = -100_000, YIndex = 1 };
            var accountC = new Account { Name = "Account C", IsTaxable = false, CashAdjustment = 50_000, YIndex = 2 };
            portfolio.Accounts.AddRange(new[] { accountA, accountB, accountC });
            portfolio.Aggregate = new Account { Name = "Aggregate", IsAggregate = true, YIndex = 3 };

            // ---- Holdings -------------------------------------------------------
            // Account A (taxable) ~ $1.0M
            AddCash(portfolio, accountA, 60_000);
            AddLot(portfolio, accountA, spy, 600, 400);   // gain
            AddLot(portfolio, accountA, spy, 300, 560);   // small loss
            AddLot(portfolio, accountA, vea, 2000, 55);   // loss
            AddLot(portfolio, accountA, vea, 1500, 45);   // gain
            AddLot(portfolio, accountA, bnd, 2500, 75);   // loss
            AddLot(portfolio, accountA, vnq, 800, 70);    // gain

            // Account B (taxable, raising $100k) ~ $0.9M
            AddCash(portfolio, accountB, 40_000);
            AddLot(portfolio, accountB, spy, 400, 500);   // gain
            AddLot(portfolio, accountB, spy, 200, 580);   // loss
            AddLot(portfolio, accountB, vea, 1000, 48);   // gain
            AddLot(portfolio, accountB, bnd, 1500, 80);   // loss
            AddLot(portfolio, accountB, vnq, 500, 95);    // loss
            AddLot(portfolio, accountB, vnq, 300, 60);    // gain

            // Account C (tax-exempt, adding $50k) ~ $0.6M
            AddCash(portfolio, accountC, 30_000);
            AddLot(portfolio, accountC, spy, 300, 450);
            AddLot(portfolio, accountC, vea, 800, 52);
            AddLot(portfolio, accountC, bnd, 2000, 70);
            AddLot(portfolio, accountC, vnq, 600, 80);

            EnsureAllCells(portfolio);
            InitializePostTradeToCurrent(portfolio);
            return portfolio;
        }

        /// <summary>Reset every post-trade plane back to current holdings.</summary>
        public static void InitializePostTradeToCurrent(Portfolio portfolio)
        {
            foreach (var p in portfolio.Positions)
                p.PostTradeShares = p.CurrentShares;
        }

        private static void AddCash(Portfolio portfolio, Account account, double dollars)
        {
            var cash = portfolio.Securities.First(s => s.IsCash);
            var pos = GetOrCreate(portfolio, account, cash);
            pos.Lots.Add(new TaxLot
            {
                Account = account,
                Security = cash,
                Shares = dollars,
                CostBasisPerShare = 1.0,
                Acquired = new DateTime(2024, 1, 1)
            });
        }

        private static void AddLot(Portfolio portfolio, Account account, Security security,
            double shares, double costPerShare)
        {
            var pos = GetOrCreate(portfolio, account, security);
            pos.Lots.Add(new TaxLot
            {
                Account = account,
                Security = security,
                Shares = shares,
                CostBasisPerShare = costPerShare,
                Acquired = new DateTime(2023, 6, 15)
            });
        }

        private static SecurityPosition GetOrCreate(Portfolio portfolio, Account account, Security security)
        {
            var pos = portfolio.GetPosition(account, security);
            if (pos == null)
            {
                pos = new SecurityPosition
                {
                    Account = account,
                    Security = security,
                    TargetWeight = portfolio.ModelWeight(security)
                };
                portfolio.Positions.Add(pos);
            }
            return pos;
        }

        /// <summary>
        /// Guarantee every account x security combination exists (even empty),
        /// so the grid is complete and targets are always present.
        /// </summary>
        private static void EnsureAllCells(Portfolio portfolio)
        {
            foreach (var account in portfolio.Accounts)
                foreach (var security in portfolio.Securities)
                {
                    var pos = GetOrCreate(portfolio, account, security);
                    pos.TargetWeight = portfolio.ModelWeight(security);
                }
        }
    }
}
