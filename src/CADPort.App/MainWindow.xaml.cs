using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Media3D;
using CADPort.App.Models;
using CADPort.App.ViewModels;
using CADPort.App.Visualization;
using HelixToolkit.Wpf;

namespace CADPort.App
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm;

        private PortfolioSceneBuilder _builder;
        private SceneBuildResult _scene;
        private bool _firstBuild = true;

        // Drag state for the post-trade plane.
        private bool _dragging;
        private SecurityPosition _dragPosition;

        public MainWindow()
        {
            InitializeComponent();

            _vm = new MainViewModel();
            DataContext = _vm;
            _vm.SceneInvalidated += RebuildScene;
            _vm.ViewChanged += () => { RebuildScene(); Viewport.ZoomExtents(); };

            Loaded += (_, _) => RebuildScene();

            // Camera controls are on by default; set them explicitly so nothing can
            // leave them disabled. Left-drag orbits, right-drag pans, wheel zooms.
            Viewport.IsRotationEnabled = true;
            Viewport.IsPanEnabled = true;
            Viewport.IsZoomEnabled = true;

            Viewport.PreviewMouseLeftButtonDown += Viewport_PreviewMouseLeftButtonDown;
            Viewport.PreviewMouseMove += Viewport_PreviewMouseMove;
            Viewport.PreviewMouseLeftButtonUp += Viewport_PreviewMouseLeftButtonUp;
            Viewport.LostMouseCapture += (_, _) => _dragging = false;
        }

        // ---- Scene -------------------------------------------------------------

        private void RebuildScene()
        {
            // Remove previously added portfolio visuals (leaves lights/cube intact).
            if (_scene != null)
                foreach (var v in _scene.Visuals)
                    Viewport.Children.Remove(v);

            _builder = new PortfolioSceneBuilder(_vm.Portfolio, _vm.ZMode,
                _vm.VisibleAccounts, _vm.ShowAggregate);
            _scene = _builder.Build();

            foreach (var v in _scene.Visuals)
                Viewport.Children.Add(v);

            if (_firstBuild)
            {
                _firstBuild = false;
                Viewport.ZoomExtents();
            }
        }

        // ---- Selection + drag --------------------------------------------------

        private void Viewport_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (_scene == null) return;

            var pt = e.GetPosition(Viewport);
            var visual = Viewport.FindNearestVisual(pt);
            if (visual == null) return;

            if (_scene.PostTradePlanes.TryGetValue(visual, out var dragPos))
            {
                // Begin dragging this post-trade plane. Handling the event (plus the
                // mouse capture) stops the camera controller from orbiting while we
                // drag - without ever disabling the global rotation flag, so orbit
                // can't get stuck off.
                _vm.SelectedPosition = dragPos;
                _dragPosition = dragPos;
                _dragging = true;
                Viewport.CaptureMouse();
                e.Handled = true;
                return;
            }

            if (_scene.Selectable.TryGetValue(visual, out var selPos))
            {
                // Plain selection - not handled, so left-drag still orbits the camera.
                _vm.SelectedPosition = selPos;
            }
        }

        private void Viewport_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (!_dragging || _dragPosition == null || e.LeftButton != MouseButtonState.Pressed)
                return;

            var pt = e.GetPosition(Viewport);
            var ray = Viewport3DHelper.GetRay(Viewport.Viewport, pt);
            if (ray == null) return;

            // Intersect the mouse ray with a vertical plane through this cell that
            // faces the camera, then read off the Z (size) coordinate.
            var origin = _builder.CellOrigin(_dragPosition.Account, _dragPosition.Security);
            var normal = HorizontalCameraNormal();
            var hit = ray.PlaneIntersection(origin, normal);
            if (hit == null) return;

            var z = Math.Max(0, hit.Value.Z);
            var marketValue = _builder.ZToMarketValue(z, _dragPosition.Account);
            var price = _dragPosition.Security.Price;
            if (price <= 0) return;

            var shares = marketValue / price; // share-level snapping happens in the VM setter
            _vm.SelectedPosition = _dragPosition;
            _vm.SelectedPostShares = shares;
            e.Handled = true;
        }

        private void Viewport_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_dragging) return;
            _dragging = false;
            _dragPosition = null;
            Viewport.ReleaseMouseCapture();
            e.Handled = true;
        }

        private Vector3D HorizontalCameraNormal()
        {
            if (Viewport.Camera is ProjectionCamera cam)
            {
                var look = cam.LookDirection;
                var n = new Vector3D(look.X, look.Y, 0);
                if (n.LengthSquared > 1e-6)
                {
                    n.Normalize();
                    return n;
                }
            }
            return new Vector3D(0, 1, 0);
        }

        // ---- Selected position nudge buttons -----------------------------------

        private void OnNudgeMinus10(object sender, RoutedEventArgs e) => _vm.NudgeSelected(-10);
        private void OnNudgeMinus1(object sender, RoutedEventArgs e) => _vm.NudgeSelected(-1);
        private void OnNudgePlus1(object sender, RoutedEventArgs e) => _vm.NudgeSelected(+1);
        private void OnNudgePlus10(object sender, RoutedEventArgs e) => _vm.NudgeSelected(+10);
    }
}
