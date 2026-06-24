using CADPort.App.Models;

namespace CADPort.App.Services
{
    /// <summary>
    /// Auto modes never create trades directly - they only place post-trade planes
    /// (set PostTradeShares). The standard <see cref="TradeEngine"/> then derives the
    /// resulting trades.
    /// </summary>
    public static class RebalanceService
    {
        public static void Rebalance(Portfolio portfolio, RebalanceMode mode)
        {
            if (mode == RebalanceMode.AccountLevel)
                AccountLevel(portfolio);
            else
                HouseholdAggregate(portfolio);
        }

        /// <summary>
        /// Mode B - match the model inside every account. Each account's post-trade
        /// value equals its current value plus its cash adjustment, and securities
        /// are set to model weight x that value. Cash absorbs the remainder.
        /// </summary>
        private static void AccountLevel(Portfolio portfolio)
        {
            foreach (var account in portfolio.Accounts)
            {
                var postValue = portfolio.AccountPostValue(account);
                double invested = 0.0;

                foreach (var security in portfolio.Securities)
                {
                    if (security.IsCash) continue;
                    var pos = portfolio.GetPosition(account, security);
                    if (pos == null) continue;

                    var targetValue = portfolio.ModelWeight(security) * postValue;
                    var shares = Math.Round(targetValue / security.Price);
                    if (shares < 0) shares = 0;
                    pos.PostTradeShares = shares;
                    invested += shares * security.Price;
                }

                SetCash(portfolio, account, postValue - invested);
            }
        }

        /// <summary>
        /// Mode A - match the model at the aggregate household level while letting
        /// individual accounts drift. Each security's household target is distributed
        /// across accounts in proportion to their current holding of that security
        /// (falling back to account size when nothing is held yet). Cash is the
        /// per-account residual, so every account's total still equals its post value
        /// and the cash requirement is respected.
        /// </summary>
        private static void HouseholdAggregate(Portfolio portfolio)
        {
            var householdPostValue = portfolio.HouseholdPostValue;

            foreach (var security in portfolio.Securities)
            {
                if (security.IsCash) continue;

                var aggTargetValue = portfolio.ModelWeight(security) * householdPostValue;
                var aggCurrentValue = portfolio.AggregateCurrentValue(security);

                foreach (var account in portfolio.Accounts)
                {
                    var pos = portfolio.GetPosition(account, security);
                    if (pos == null) continue;

                    double share;
                    if (aggCurrentValue > 1e-6)
                        share = pos.CurrentMarketValue / aggCurrentValue;          // keep existing tilt
                    else
                        share = SafeDiv(portfolio.AccountPostValue(account), householdPostValue); // by size

                    var targetValue = aggTargetValue * share;
                    var shares = Math.Round(targetValue / security.Price);
                    if (shares < 0) shares = 0;
                    pos.PostTradeShares = shares;
                }
            }

            // cash residual per account keeps each account total at its post value
            foreach (var account in portfolio.Accounts)
            {
                var invested = portfolio.Securities
                    .Where(s => !s.IsCash)
                    .Sum(s => (portfolio.GetPosition(account, s)?.PostTradeShares ?? 0.0) * s.Price);

                SetCash(portfolio, account, portfolio.AccountPostValue(account) - invested);
            }
        }

        private static void SetCash(Portfolio portfolio, Account account, double dollars)
        {
            var cashPos = portfolio.GetCashPosition(account);
            if (cashPos == null) return;
            cashPos.PostTradeShares = Math.Max(0, Math.Round(dollars)); // cash price == 1
        }

        private static double SafeDiv(double a, double b) => Math.Abs(b) < 1e-9 ? 0.0 : a / b;
    }
}
