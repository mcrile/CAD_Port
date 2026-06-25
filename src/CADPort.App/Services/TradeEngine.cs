using CADPort.App.Models;

namespace CADPort.App.Services
{
    /// <summary>
    /// Derives trades deterministically from (PostTradeShares - CurrentShares) for
    /// every real-account position. No optimization. Cash is never traded directly
    /// (it is the residual funding security).
    /// </summary>
    public static class TradeEngine
    {
        private const double ShareEpsilon = 0.5; // whole-share threshold

        public static List<TradeProposal> GenerateTrades(Portfolio portfolio)
        {
            var trades = new List<TradeProposal>();

            foreach (var pos in portfolio.Positions)
            {
                if (pos.IsCash) continue; // cash is not directly traded

                var delta = Math.Round(pos.PostTradeShares) - pos.CurrentShares;
                if (Math.Abs(delta) < ShareEpsilon) continue;

                trades.Add(delta < 0
                    ? BuildSale(pos, -delta)
                    : BuildPurchase(pos, delta));
            }

            return trades;
        }

        /// <summary>
        /// Push the proposed cash level onto each account's cash post-trade plane:
        /// sales raise cash, purchases consume it, plus the external cash adjustment.
        /// This keeps the cash column visually in sync with the derived trades.
        /// </summary>
        public static void SyncCashPostTrade(Portfolio portfolio, IEnumerable<TradeProposal> trades)
        {
            var tradeList = trades as IList<TradeProposal> ?? trades.ToList();

            foreach (var account in portfolio.Accounts)
            {
                var cashPos = portfolio.GetCashPosition(account);
                if (cashPos == null) continue;

                var proceeds = tradeList
                    .Where(t => t.Account == account && t.Side == TradeSide.Sell)
                    .Sum(t => t.MarketValue);
                var cost = tradeList
                    .Where(t => t.Account == account && t.Side == TradeSide.Buy)
                    .Sum(t => t.MarketValue);

                var proposedCash = cashPos.CurrentMarketValue + account.CashAdjustment + proceeds - cost;
                cashPos.PostTradeShares = Math.Max(0, Math.Round(proposedCash)); // cash price == 1
            }
        }

        private static TradeProposal BuildSale(SecurityPosition pos, double sharesToSell)
        {
            var price = pos.Security.Price;
            var proposal = new TradeProposal
            {
                Account = pos.Account,
                Security = pos.Security,
                Side = TradeSide.Sell,
                Quantity = sharesToSell,
                MarketValue = sharesToSell * price
            };

            var remaining = sharesToSell;
            double realized = 0.0;

            // Sell from the most attractive lots first (largest loss%).
            foreach (var lot in pos.LotsInSellOrder)
            {
                if (remaining <= ShareEpsilon) break;

                var take = Math.Min(remaining, lot.Shares);
                if (take <= 0) continue;

                var lotRealized = take * (price - lot.CostBasisPerShare);
                realized += lotRealized;
                remaining -= take;

                proposal.LotsImpacted.Add(new LotImpact
                {
                    Lot = lot,
                    SharesSold = take,
                    RealizedGainLoss = lotRealized
                });
            }

            proposal.RealizedGainLoss = realized;
            return proposal;
        }

        private static TradeProposal BuildPurchase(SecurityPosition pos, double sharesToBuy)
        {
            var price = pos.Security.Price;
            return new TradeProposal
            {
                Account = pos.Account,
                Security = pos.Security,
                Side = TradeSide.Buy,
                Quantity = sharesToBuy,
                MarketValue = sharesToBuy * price,
                RealizedGainLoss = 0.0
            };
        }

        /// <summary>
        /// Apply accepted trades to holdings: reduce/remove sold lots, add a new
        /// cost-basis lot for purchases, and reconcile each account's cash for the
        /// net proceeds plus its external cash adjustment. Post-trade planes are
        /// then re-synced to the new current holdings.
        /// </summary>
        public static void ApplyTrades(Portfolio portfolio, IEnumerable<TradeProposal> trades)
        {
            // net cash delta per account from trades
            var cashDelta = new Dictionary<Account, double>();
            foreach (var a in portfolio.Accounts) cashDelta[a] = 0.0;

            foreach (var trade in trades)
            {
                var pos = portfolio.GetPosition(trade.Account, trade.Security);
                if (pos == null) continue;

                if (trade.Side == TradeSide.Sell)
                {
                    foreach (var impact in trade.LotsImpacted)
                        impact.Lot.Shares -= impact.SharesSold;
                    pos.Lots.RemoveAll(l => l.Shares <= 1e-6);
                    cashDelta[trade.Account] += trade.MarketValue; // proceeds
                }
                else
                {
                    pos.Lots.Add(new TaxLot
                    {
                        Account = trade.Account,
                        Security = trade.Security,
                        Shares = trade.Quantity,
                        CostBasisPerShare = trade.Security.Price, // new lot at market
                        Acquired = DateTime.Today
                    });
                    cashDelta[trade.Account] -= trade.MarketValue; // cost
                }
            }

            // reconcile cash positions and consume the external adjustment
            foreach (var account in portfolio.Accounts)
            {
                var cashPos = portfolio.GetCashPosition(account);
                if (cashPos != null)
                {
                    var cashLot = cashPos.Lots.FirstOrDefault();
                    if (cashLot == null)
                    {
                        cashLot = new TaxLot
                        {
                            Account = account,
                            Security = cashPos.Security,
                            CostBasisPerShare = 1.0,
                            Acquired = DateTime.Today
                        };
                        cashPos.Lots.Add(cashLot);
                    }
                    cashLot.Shares += cashDelta[account] + account.CashAdjustment;
                    if (cashLot.Shares < 0) cashLot.Shares = 0;
                }
                account.CashAdjustment = 0; // requirement consumed
            }

            SampleDataService.InitializePostTradeToCurrent(portfolio);
        }
    }
}
