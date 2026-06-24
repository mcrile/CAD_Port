using CADPort.App.Models;

namespace CADPort.App.ViewModels
{
    /// <summary>
    /// Editable per-account configuration row: taxable toggle and cash adjustment.
    /// Changes are pushed straight onto the domain <see cref="Account"/> and then
    /// trigger a recompute/redraw via the supplied callback.
    /// </summary>
    public class AccountConfigViewModel : ObservableObject
    {
        private readonly Account _account;
        private readonly Action _onChanged;

        public AccountConfigViewModel(Account account, Action onChanged)
        {
            _account = account;
            _onChanged = onChanged;
        }

        public string Name => _account.Name;

        public bool IsTaxable
        {
            get => _account.IsTaxable;
            set
            {
                if (_account.IsTaxable == value) return;
                _account.IsTaxable = value;
                Raise();
                _onChanged?.Invoke();
            }
        }

        public double CashAdjustment
        {
            get => _account.CashAdjustment;
            set
            {
                if (Math.Abs(_account.CashAdjustment - value) < 1e-9) return;
                _account.CashAdjustment = value;
                Raise();
                _onChanged?.Invoke();
            }
        }
    }
}
