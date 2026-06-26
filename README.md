# Tax-Aware 3D Portfolio Workspace — MVP Prototype

A Windows desktop prototype that turns portfolio management into a navigable 3D
workspace. Portfolios are geometric objects built from tax-lot boxes; you compare
holdings to model targets, drag post-trade planes to construct trades, and run
deterministic household / account rebalances — all visually.

Built with **.NET 8 (WPF) + HelixToolkit** using an MVVM structure.

---

## Build & Run (Windows)

Requirements: **Windows 10/11**, **.NET 8 SDK**, and either **Visual Studio 2022**
(17.8+) or the `dotnet` CLI. (WPF only runs on Windows — it will not build on Linux/macOS.)

```powershell
# from the repository root
dotnet restore CADPort.sln
dotnet build CADPort.sln -c Debug
dotnet run --project src/CADPort.App/CADPort.App.csproj
```

Or open `CADPort.sln` in Visual Studio and press **F5**.

> NuGet restore pulls `HelixToolkit.Wpf` (pinned to 2.25.0 in the csproj). If your
> feed doesn't have that exact version, bump it to the latest `2.x`.

---

## Using the workspace

- **Camera:** left-drag = orbit, right-drag = pan, wheel = zoom, view cube (top-right) for snap views.
- **Read the geometry:**
  - **X axis = security** (Cash first), **Y axis = account** (+ an Aggregate row), **Z axis = position size**.
  - Each **box is a tax lot**; height ∝ market value. Lots stack with the most
    attractive-to-sell lot (largest loss %) on top.
  - **Color (taxable accounts):** dark red = large loss → white = neutral → green =
    large gain. Tax-exempt accounts and cash are white. Materials are flat/matte
    (no specular) so colors stay legible from any angle.
  - **Thin translucent plane** = model target. **Gold plane** = post-trade (editable).
  - **Yellow overlay** = proposed trade — a light-yellow block filling the gap
    between current holdings and the post-trade plane (overlapping the stack = sale,
    floating above it = purchase). Also shown on the aggregate row.
- **Construct a trade manually:** click a stack or its gold plane to select it, then
  **drag the gold plane** up/down (snaps to whole shares), or use the slider / ±1 / ±10
  buttons in the Selected Position panel. Trades, metrics, and overlays update live.
- **Auto rebalance:** *Mode A* (household aggregate) or *Mode B* (account-level) place
  post-trade planes; the trade engine derives the trades.
- **Z toggle:** switch between Market Value and Portfolio Weight.
- **View selector (top-right):** focus on a single portfolio or show all. In
  single-portfolio mode only that account renders (no aggregate row) and the Z-scale
  and camera refit to it — useful for dense models with many securities.
- **Accept Trades:** applies proposals to holdings (current holdings only change here),
  reconciles cash, and resets post-trade planes.

---

## Architecture

```
src/CADPort.App/
  Models/          Domain: Security, Account, TaxLot, SecurityPosition,
                   TradeProposal, Portfolio, PortfolioMetrics, enums
  Services/        SampleDataService  – seeded household
                   TaxColorService    – gain/loss gradient
                   TradeEngine        – deterministic trade derivation + apply
                   RebalanceService   – Mode A / Mode B plane placement
                   MetricsService     – household metrics
  Visualization/   PortfolioSceneBuilder – portfolio -> 3D visuals + hit maps
  ViewModels/      MainViewModel and supporting bindable VMs
  MainWindow.xaml  3D viewport + control panel; code-behind handles selection/drag
```

**Data flow:** the `MainViewModel` owns the `Portfolio`. Any change (drag, slider,
auto-rebalance, account config) calls `Recompute()` → regenerates trades + metrics →
raises `SceneInvalidated` → the window rebuilds the 3D scene from
`PortfolioSceneBuilder`. Trades are always derived; never stored on holdings until accepted.

---

## Modeling assumptions (prototype interpretations)

These are deliberate, documented choices where the spec left room:

- **Cash adjustment:** an account's post-trade total value = current value +
  `CashAdjustment` (positive = add/deposit, negative = raise/withdraw). Both the model
  target plane and the rebalance apply the model weights to that post value, so a
  deposit/withdrawal flows prorata into every security's target. The model carries a
  small residual cash target (~3%); cash is never traded directly (it's the residual
  funding security), so raising cash forces net sales.
- **Mode A vs B:** Mode B sets every account to model weight × its post value (cash =
  residual). Mode A matches the model at the household level, distributing each
  security's household target across accounts in proportion to current holdings (so
  accounts keep their tilts and *drift*), with cash as the per-account residual so each
  account total still equals its post value.
- **Lot ordering / coloring:** stack/sell order by a *tax-aware* unrealized return
  vs cost basis (largest loss first). Lots in tax-exempt accounts (and cash) have no
  tax consequence, so they sort as neutral rather than by gain/loss - flipping an
  account to tax-exempt drops its lots out of the harvest ordering. Color intensity
  uses unrealized G/L ÷ market value, saturating at ±30%; tax-exempt/cash render white.
- **Cash follows the trades:** cash is the residual funding security and is not
  dragged directly. Each account's cash post-trade level is derived from its proposed
  trades (sales raise cash, purchases consume it) plus the external cash adjustment,
  so the cash column moves live as you build trades.
- **Estimated tax cost:** flat 25% rate applied to net realized gain/loss from
  **taxable** accounts only (negative = tax benefit). No wash-sale logic (out of scope).

## Explicit non-goals (not implemented, per spec)

Wash-sale logic, multi-currency, transaction costs, custodian/OMS integration, trade
execution, performance attribution, historical playback, multi-user collaboration.

---

## MVP success criteria → where it lives

| # | Capability | Implementation |
|---|------------|----------------|
| 1 | Load multiple accounts | `SampleDataService` (3 accounts + aggregate) |
| 2 | Load tax lots | `SampleDataService` lots with embedded gains/losses |
| 3 | View portfolios in 3D | `PortfolioSceneBuilder`, `HelixViewport3D` |
| 4 | Rotate / inspect | HelixToolkit camera (orbit/pan/zoom/view cube) |
| 5 | Tax lots as boxes | per-lot `BoxVisual3D`, height ∝ MV |
| 6 | Embedded gains/losses visually | `TaxColorService` gradient |
| 7 | Compare to model targets | translucent model plane |
| 8 | Adjust post-trade planes manually | drag + slider/±buttons |
| 9 | Deterministic trade proposals | `TradeEngine` |
| 10 | Household rebalance | `RebalanceService` Mode A |
| 11 | Account rebalance | `RebalanceService` Mode B |
| 12 | Tax impacts in real time | `MetricsService` + live recompute |
| 13 | Aggregate household positioning | aggregate row in scene builder |
| 14 | Dollar / percentage toggle | `ZAxisMode` + Z-axis radios |
