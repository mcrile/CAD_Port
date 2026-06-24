using CADPort.App.Models;

namespace CADPort.App.Services
{
    /// <summary>Computes the household metrics panel from current state + derived trades.</summary>
    public static class MetricsService
    {
        public static PortfolioMetrics Compute(Portfolio portfolio, IEnumerable<TradeProposal> trades)
        {
            var tradeList = trades as IList<TradeProposal> ?? trades.ToList();

            var currentCash = portfolio.Accounts
                .Sum(a => portfolio.GetCashPosition(a)?.CurrentMarketValue ?? 0.0);

            var saleProceeds = tradeList.Where(t => t.Side == TradeSide.Sell).Sum(t => t.MarketValue);
            var purchaseCost = tradeList.Where(t => t.Side == TradeSide.Buy).Sum(t => t.MarketValue);
            var cashAdjustments = portfolio.Accounts.Sum(a => a.CashAdjustment);

            // Realized gains/losses across all lot impacts (sales only).
            double realizedGains = 0, realizedLosses = 0, taxableNetRealized = 0;
            foreach (var trade in tradeList)
            {
                foreach (var impact in trade.LotsImpacted)
                {
                    if (impact.RealizedGainLoss >= 0) realizedGains += impact.RealizedGainLoss;
                    else realizedLosses += impact.RealizedGainLoss; // negative
                }
                if (trade.IsTaxable) taxableNetRealized += trade.RealizedGainLoss;
            }

            return new PortfolioMetrics
            {
                CurrentPortfolioValue = portfolio.HouseholdValue,
                CurrentCash = currentCash,
                ProposedCash = currentCash + cashAdjustments + saleProceeds - purchaseCost,
                RealizedGains = realizedGains,
                RealizedLosses = realizedLosses,
                NetTaxImpact = realizedGains + realizedLosses,
                HarvestedLosses = -realizedLosses,
                EstimatedTaxCost = taxableNetRealized * portfolio.TaxRate
            };
        }
    }
}
