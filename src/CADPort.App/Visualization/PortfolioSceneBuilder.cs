using System.Windows.Media;
using System.Windows.Media.Media3D;
using CADPort.App.Models;
using CADPort.App.Services;
using HelixToolkit.Wpf;

namespace CADPort.App.Visualization
{
    /// <summary>Result of a scene build: the visuals plus the hit-test maps used for selection and dragging.</summary>
    public class SceneBuildResult
    {
        public List<Visual3D> Visuals { get; } = new();

        /// <summary>Visuals (lots + planes) that select a position when clicked.</summary>
        public Dictionary<Visual3D, SecurityPosition> Selectable { get; } = new();

        /// <summary>Draggable post-trade plane visuals (real accounts only).</summary>
        public Dictionary<Visual3D, SecurityPosition> PostTradePlanes { get; } = new();
    }

    /// <summary>
    /// Translates a <see cref="Portfolio"/> into the 3D workspace: stacked tax-lot
    /// boxes (current holdings), translucent model target planes, thick draggable
    /// post-trade planes, and trade overlays. X = security, Y = account, Z = size.
    /// </summary>
    public class PortfolioSceneBuilder
    {
        public const double XSpacing = 3.2;
        public const double YSpacing = 3.4;
        private const double ZTarget = 6.0;

        private const double LotW = 1.5;
        private const double LotL = 1.5;
        private const double PlaneW = 1.95;
        private const double PlaneL = 1.95;
        private const double OverlayW = 1.6;
        private const double OverlayL = 1.6;
        private const double ModelPlaneThickness = 0.03;
        private const double PostPlaneThickness = 0.07;

        private readonly Portfolio _portfolio;

        public ZAxisMode Mode { get; }
        public double ZScale { get; private set; }

        public PortfolioSceneBuilder(Portfolio portfolio, ZAxisMode mode)
        {
            _portfolio = portfolio;
            Mode = mode;
            ZScale = ComputeZScale();
        }

        // ---- Public geometry helpers (used by the drag handler) ----------------

        public Point3D CellOrigin(Account account, Security security)
            => new(security.XIndex * XSpacing, account.YIndex * YSpacing, 0);

        public double Denominator(Account account)
            => account.IsAggregate ? _portfolio.HouseholdValue : _portfolio.AccountValue(account);

        /// <summary>Inverse of the Z mapping: scene Z -> market value for an account.</summary>
        public double ZToMarketValue(double z, Account account)
        {
            var metric = ZScale > 0 ? z / ZScale : 0;
            if (Mode == ZAxisMode.MarketValue) return metric;
            var denom = Denominator(account);
            return metric * denom; // metric is a weight
        }

        // ---- Build -------------------------------------------------------------

        public SceneBuildResult Build()
        {
            var result = new SceneBuildResult();

            AddGround(result);
            AddAxisLabels(result);

            foreach (var account in _portfolio.Accounts)
                foreach (var security in _portfolio.Securities)
                    BuildCell(result, _portfolio.GetPosition(account, security));

            BuildAggregateRow(result);
            return result;
        }

        private void BuildCell(SceneBuildResult result, SecurityPosition pos)
        {
            if (pos == null) return;
            var origin = CellOrigin(pos.Account, pos.Security);

            BuildHoldingsStack(result, pos, origin);
            BuildModelPlane(result, origin, pos.TargetWeight * _portfolio.AccountValue(pos.Account), pos.Account);
            BuildPostTradePlane(result, pos, origin);
            BuildOverlays(result, pos, origin);
        }

        private void BuildHoldingsStack(SceneBuildResult result, SecurityPosition pos, Point3D origin)
        {
            double cumulative = 0;
            foreach (var lot in pos.LotsBottomToTop)
            {
                var h = Z(lot.MarketValue, pos.Account);
                if (h <= 0) continue;

                var color = TaxColorService.ForLot(lot);
                var box = MakeBox(origin.X, origin.Y, cumulative + h / 2.0,
                    LotW, LotL, h, color, 255);
                cumulative += h;

                result.Visuals.Add(box);
                result.Selectable[box] = pos;
            }
        }

        private void BuildModelPlane(SceneBuildResult result, Point3D origin, double targetValue, Account account)
        {
            var z = Z(targetValue, account);
            // Thin translucent reference plane.
            var plane = MakeBox(origin.X, origin.Y, z + ModelPlaneThickness / 2.0,
                PlaneW, PlaneL, ModelPlaneThickness, Color.FromRgb(0x88, 0xAA, 0xCC), 70);
            result.Visuals.Add(plane);
        }

        private void BuildPostTradePlane(SceneBuildResult result, SecurityPosition pos, Point3D origin)
        {
            var z = Z(pos.PostTradeMarketValue, pos.Account);
            // Thick, highly visible, draggable plane.
            var plane = MakeBox(origin.X, origin.Y, z + PostPlaneThickness / 2.0,
                PlaneW, PlaneL, PostPlaneThickness, Color.FromRgb(0xFF, 0xD1, 0x4A), 190);
            result.Visuals.Add(plane);
            result.Selectable[plane] = pos;

            // Cash is the residual funding security - its level is derived from the
            // trades on other securities, so it is not directly draggable.
            if (!pos.IsCash)
                result.PostTradePlanes[plane] = pos;
        }

        private void BuildOverlays(SceneBuildResult result, SecurityPosition pos, Point3D origin)
        {
            if (pos.IsCash) return; // no trade overlay for cash

            var currentZ = Z(pos.CurrentMarketValue, pos.Account);
            var postZ = Z(pos.PostTradeMarketValue, pos.Account);

            if (postZ < currentZ - 1e-4)
            {
                // proposed sale: holdings above the post-trade plane (red)
                var h = currentZ - postZ;
                var box = MakeBox(origin.X, origin.Y, postZ + h / 2.0,
                    OverlayW, OverlayL, h, Color.FromRgb(0xE5, 0x3E, 0x3E), 110);
                result.Visuals.Add(box);
                result.Selectable[box] = pos;
            }
            else if (postZ > currentZ + 1e-4)
            {
                // proposed purchase: space between holdings and post-trade plane (blue)
                var h = postZ - currentZ;
                var box = MakeBox(origin.X, origin.Y, currentZ + h / 2.0,
                    OverlayW, OverlayL, h, Color.FromRgb(0x3E, 0x8C, 0xE5), 110);
                result.Visuals.Add(box);
                result.Selectable[box] = pos;
            }
        }

        private void BuildAggregateRow(SceneBuildResult result)
        {
            var agg = _portfolio.Aggregate;
            if (agg == null) return;

            foreach (var security in _portfolio.Securities)
            {
                var origin = CellOrigin(agg, security);

                // Combined holdings stack across all real accounts.
                double cumulative = 0;
                var lots = security.IsCash
                    ? _portfolio.AggregateLots(security)
                    : _portfolio.AggregateLots(security).OrderByDescending(l => l.SortGainLossPercent);

                foreach (var lot in lots)
                {
                    var h = Z(lot.MarketValue, agg);
                    if (h <= 0) continue;
                    var color = TaxColorService.ForLot(lot);
                    var box = MakeBox(origin.X, origin.Y, cumulative + h / 2.0,
                        LotW, LotL, h, color, 255);
                    cumulative += h;
                    result.Visuals.Add(box);
                }

                // Household model plane (informational).
                BuildModelPlane(result, origin, _portfolio.HouseholdTargetValue(security), agg);

                // Aggregate post-trade plane (sum of account post-trades, not draggable).
                var postZ = Z(_portfolio.AggregatePostTradeValue(security), agg);
                var plane = MakeBox(origin.X, origin.Y, postZ + PostPlaneThickness / 2.0,
                    PlaneW, PlaneL, PostPlaneThickness, Color.FromRgb(0xB0, 0x8A, 0xD8), 150);
                result.Visuals.Add(plane);
            }
        }

        // ---- Scaffolding -------------------------------------------------------

        private void AddGround(SceneBuildResult result)
        {
            var width = (_portfolio.Securities.Count + 1) * XSpacing;
            var length = (_portfolio.Accounts.Count + 2) * YSpacing;
            var centerX = (_portfolio.Securities.Count - 1) * XSpacing / 2.0;
            var centerY = (_portfolio.Accounts.Count) * YSpacing / 2.0;

            var grid = new GridLinesVisual3D
            {
                Center = new Point3D(centerX, centerY, -0.02),
                Normal = new Vector3D(0, 0, 1),
                LengthDirection = new Vector3D(1, 0, 0),
                Width = length,
                Length = width,
                MinorDistance = XSpacing,
                MajorDistance = XSpacing,
                Thickness = 0.012,
                Fill = new SolidColorBrush(Color.FromRgb(0x3A, 0x3F, 0x4A))
            };
            result.Visuals.Add(grid);
        }

        private void AddAxisLabels(SceneBuildResult result)
        {
            // Security labels along the front (X axis).
            foreach (var security in _portfolio.Securities)
            {
                result.Visuals.Add(new BillboardTextVisual3D
                {
                    Text = security.Symbol,
                    Position = new Point3D(security.XIndex * XSpacing, -YSpacing * 0.7, 0.2),
                    Foreground = Brushes.White,
                    FontSize = 13,
                    FontWeight = System.Windows.FontWeights.Bold
                });
            }

            // Account labels down the side (Y axis), including aggregate.
            var rows = new List<Account>(_portfolio.Accounts);
            if (_portfolio.Aggregate != null) rows.Add(_portfolio.Aggregate);
            foreach (var account in rows)
            {
                result.Visuals.Add(new BillboardTextVisual3D
                {
                    Text = account.Name,
                    Position = new Point3D(-XSpacing * 0.85, account.YIndex * YSpacing, 0.2),
                    Foreground = account.IsAggregate ? Brushes.Violet : Brushes.LightGray,
                    FontSize = 13,
                    FontWeight = System.Windows.FontWeights.Bold
                });
            }
        }

        // ---- Math --------------------------------------------------------------

        private double Metric(double marketValue, Account account)
        {
            if (Mode == ZAxisMode.MarketValue) return marketValue;
            var denom = Denominator(account);
            return denom > 1e-9 ? marketValue / denom : 0.0;
        }

        private double Z(double marketValue, Account account) => Metric(marketValue, account) * ZScale;

        private double ComputeZScale()
        {
            double max = 0;

            foreach (var pos in _portfolio.Positions)
            {
                max = Math.Max(max, Metric(pos.CurrentMarketValue, pos.Account));
                max = Math.Max(max, Metric(pos.PostTradeMarketValue, pos.Account));
            }

            if (_portfolio.Aggregate != null)
            {
                foreach (var s in _portfolio.Securities)
                {
                    max = Math.Max(max, Metric(_portfolio.AggregateCurrentValue(s), _portfolio.Aggregate));
                    max = Math.Max(max, Metric(_portfolio.AggregatePostTradeValue(s), _portfolio.Aggregate));
                }
            }

            return max > 1e-9 ? ZTarget / max : 1.0;
        }

        private static BoxVisual3D MakeBox(double cx, double cy, double cz,
            double w, double l, double h, Color color, byte alpha)
        {
            var brush = new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
            brush.Freeze();

            // Flat, matte material (no specular highlight) so the box shows its true
            // color from every angle. This is a data-viz workspace, not a 3D game -
            // legibility of color matters more than realistic shading. A faint
            // emissive component keeps the color readable even on shadowed faces.
            var material = new MaterialGroup();
            material.Children.Add(new DiffuseMaterial(brush));
            var emissiveColor = Color.FromArgb((byte)(alpha * 0.25),
                color.R, color.G, color.B);
            material.Children.Add(new EmissiveMaterial(new SolidColorBrush(emissiveColor)));
            material.Freeze();

            var box = new BoxVisual3D
            {
                Center = new Point3D(cx, cy, cz),
                Width = w,
                Length = l,
                Height = Math.Max(h, 1e-4),
                Material = material,
                BackMaterial = material
            };
            return box;
        }
    }
}
