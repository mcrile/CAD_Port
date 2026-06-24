namespace CADPort.App.Models
{
    /// <summary>What the Z (vertical) axis encodes for every box and plane.</summary>
    public enum ZAxisMode
    {
        MarketValue,
        Weight
    }

    /// <summary>Direction of a derived trade proposal.</summary>
    public enum TradeSide
    {
        Buy,
        Sell
    }

    /// <summary>The two deterministic auto-rebalance modes.</summary>
    public enum RebalanceMode
    {
        /// <summary>Mode A - match model at the aggregate household level; accounts may drift.</summary>
        HouseholdAggregate,

        /// <summary>Mode B - match model within every account.</summary>
        AccountLevel
    }
}
