using System.Collections.ObjectModel;
using System.Windows.Input;
using CADPort.App.Models;
using CADPort.App.Services;

namespace CADPort.App.ViewModels
{
    /// <summary>
    /// Owns the portfolio state and the workspace logic. The 3D scene is rebuilt by
    /// the window code-behind whenever <see cref="SceneInvalidated"/> fires.
    /// </summary>
    public class MainViewModel : ObservableObject
    {
        public Portfolio Portfolio { get; }

        public ObservableCollection<AccountConfigViewModel> Accounts { get; } = new();
        public ObservableCollection<TradeRowViewModel> Trades { get; } = new();
        public MetricsViewModel Metrics { get; } = new();

        /// <summary>Raised when the 3D scene needs to be rebuilt (data or mode changed).</summary>
        public event Action SceneInvalidated;

        public ICommand HouseholdRebalanceCommand { get; }
        public ICommand AccountRebalanceCommand { get; }
        public ICommand ResetCommand { get; }
        public ICommand AcceptTradesCommand { get; }

        public MainViewModel()
        {
            Portfolio = SampleDataService.Create();

            foreach (var account in Portfolio.Accounts)
                Accounts.Add(new AccountConfigViewModel(account, OnAccountConfigChanged));

            HouseholdRebalanceCommand = new RelayCommand(() =>
            {
                RebalanceService.Rebalance(Portfolio, RebalanceMode.HouseholdAggregate);
                Recompute();
            });

            AccountRebalanceCommand = new RelayCommand(() =>
            {
                RebalanceService.Rebalance(Portfolio, RebalanceMode.AccountLevel);
                Recompute();
            });

            ResetCommand = new RelayCommand(() =>
            {
                SampleDataService.InitializePostTradeToCurrent(Portfolio);
                Recompute();
            });

            AcceptTradesCommand = new RelayCommand(() =>
            {
                var trades = TradeEngine.GenerateTrades(Portfolio);
                TradeEngine.ApplyTrades(Portfolio, trades);
                SelectedPosition = null;
                Recompute();
            }, () => Trades.Count > 0);

            Recompute();
        }

        // ---- Z axis mode -------------------------------------------------------

        private ZAxisMode _zMode = ZAxisMode.MarketValue;
        public ZAxisMode ZMode
        {
            get => _zMode;
            private set
            {
                if (Set(ref _zMode, value))
                {
                    Raise(nameof(IsMarketValueMode));
                    Raise(nameof(IsWeightMode));
                    SceneInvalidated?.Invoke();
                }
            }
        }

        public bool IsMarketValueMode
        {
            get => _zMode == ZAxisMode.MarketValue;
            set { if (value) ZMode = ZAxisMode.MarketValue; }
        }

        public bool IsWeightMode
        {
            get => _zMode == ZAxisMode.Weight;
            set { if (value) ZMode = ZAxisMode.Weight; }
        }

        // ---- Selected position editor -----------------------------------------

        private SecurityPosition _selectedPosition;
        public SecurityPosition SelectedPosition
        {
            get => _selectedPosition;
            set
            {
                _selectedPosition = value;
                RaiseSelectionProperties();
            }
        }

        public bool HasSelection => _selectedPosition != null;

        public string SelectedTitle => _selectedPosition == null
            ? "No position selected"
            : $"{_selectedPosition.Account.Name}  -  {_selectedPosition.Security.Symbol}";

        public string SelectedCurrentText => _selectedPosition == null
            ? string.Empty
            : $"Current: {_selectedPosition.CurrentShares:N0} sh   ({Money(_selectedPosition.CurrentMarketValue)})";

        public string SelectedDeltaText
        {
            get
            {
                if (_selectedPosition == null) return string.Empty;
                var delta = Math.Round(_selectedPosition.PostTradeShares) - _selectedPosition.CurrentShares;
                if (Math.Abs(delta) < 0.5) return "No trade";
                var verb = delta < 0 ? "SELL" : "BUY";
                return $"{verb} {Math.Abs(delta):N0} sh   ({Money(Math.Abs(delta) * _selectedPosition.Security.Price)})";
            }
        }

        public double SelectedPostShares
        {
            get => _selectedPosition?.PostTradeShares ?? 0;
            set
            {
                if (_selectedPosition == null) return;
                var snapped = Math.Max(0, Math.Round(value));
                if (Math.Abs(_selectedPosition.PostTradeShares - snapped) < 1e-9) return;
                _selectedPosition.PostTradeShares = snapped;
                Recompute();
                RaiseSelectionProperties();
            }
        }

        public double SelectedSliderMax => _selectedPosition == null
            ? 1
            : Math.Max(10, Math.Ceiling(Math.Max(_selectedPosition.CurrentShares, _selectedPosition.PostTradeShares) * 2.0));

        public void NudgeSelected(double deltaShares)
        {
            if (_selectedPosition == null) return;
            SelectedPostShares = _selectedPosition.PostTradeShares + deltaShares;
        }

        private void RaiseSelectionProperties()
        {
            Raise(nameof(SelectedPosition));
            Raise(nameof(HasSelection));
            Raise(nameof(SelectedTitle));
            Raise(nameof(SelectedCurrentText));
            Raise(nameof(SelectedDeltaText));
            Raise(nameof(SelectedPostShares));
            Raise(nameof(SelectedSliderMax));
        }

        // ---- Pipeline ----------------------------------------------------------

        private void OnAccountConfigChanged() => Recompute();

        /// <summary>Regenerate trades + metrics, refresh bindings, and rebuild the scene.</summary>
        public void Recompute()
        {
            var trades = TradeEngine.GenerateTrades(Portfolio);

            // Reflect the net proceeds/cost of the proposed trades in each account's
            // cash column so cash visibly rises on sales and falls on purchases.
            TradeEngine.SyncCashPostTrade(Portfolio, trades);

            Trades.Clear();
            foreach (var t in trades.OrderBy(t => t.Account.Name).ThenBy(t => t.Security.XIndex))
                Trades.Add(new TradeRowViewModel(t));

            Metrics.Update(MetricsService.Compute(Portfolio, trades));

            RaiseSelectionProperties();
            SceneInvalidated?.Invoke();
            CommandManager.InvalidateRequerySuggested();
        }

        private static string Money(double v) =>
            v.ToString("C0", System.Globalization.CultureInfo.GetCultureInfo("en-US"));
    }
}
