using CADPort.App.Models;

namespace CADPort.App.ViewModels
{
    /// <summary>Formatted, bindable view of the household metrics panel.</summary>
    public class MetricsViewModel : ObservableObject
    {
        private PortfolioMetrics _m = new();

        public void Update(PortfolioMetrics metrics)
        {
            _m = metrics ?? new PortfolioMetrics();
            foreach (var name in new[]
            {
                nameof(CurrentPortfolioValue), nameof(CurrentCash), nameof(ProposedCash),
                nameof(RealizedGains), nameof(RealizedLosses), nameof(NetTaxImpact),
                nameof(HarvestedLosses), nameof(EstimatedTaxCost)
            })
            {
                Raise(name);
            }
        }

        private static string C(double v) => v.ToString("C0", System.Globalization.CultureInfo.GetCultureInfo("en-US"));

        public string CurrentPortfolioValue => C(_m.CurrentPortfolioValue);
        public string CurrentCash => C(_m.CurrentCash);
        public string ProposedCash => C(_m.ProposedCash);
        public string RealizedGains => C(_m.RealizedGains);
        public string RealizedLosses => C(_m.RealizedLosses);
        public string NetTaxImpact => C(_m.NetTaxImpact);
        public string HarvestedLosses => C(_m.HarvestedLosses);
        public string EstimatedTaxCost => C(_m.EstimatedTaxCost);
    }
}
