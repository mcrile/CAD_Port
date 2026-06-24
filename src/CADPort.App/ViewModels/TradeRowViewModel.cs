using CADPort.App.Models;

namespace CADPort.App.ViewModels
{
    /// <summary>One row in the proposed-trades list.</summary>
    public class TradeRowViewModel
    {
        private readonly TradeProposal _t;

        public TradeRowViewModel(TradeProposal trade) => _t = trade;

        public string Account => _t.Account?.Name;
        public string Security => _t.Security?.Symbol;
        public string Side => _t.Side.ToString().ToUpperInvariant();
        public string Quantity => Math.Round(_t.Quantity).ToString("N0");
        public string MarketValue => _t.MarketValue.ToString("C0", System.Globalization.CultureInfo.GetCultureInfo("en-US"));

        public string RealizedGainLoss => _t.Side == TradeSide.Sell
            ? _t.RealizedGainLoss.ToString("C0", System.Globalization.CultureInfo.GetCultureInfo("en-US"))
            : "-";

        public System.Windows.Media.Brush SideBrush => _t.Side == TradeSide.Sell
            ? System.Windows.Media.Brushes.IndianRed
            : System.Windows.Media.Brushes.CornflowerBlue;
    }
}
