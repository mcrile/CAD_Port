namespace CADPort.App.Models
{
    /// <summary>
    /// A tradeable instrument. Cash is modelled as a normal security (price 1.0)
    /// and always occupies the first slot on the X axis.
    /// </summary>
    public class Security
    {
        public string Symbol { get; set; }
        public string Name { get; set; }

        /// <summary>Current market price per share. Cash uses 1.0.</summary>
        public double Price { get; set; }

        public bool IsCash { get; set; }

        /// <summary>Position on the X axis (0 == cash).</summary>
        public int XIndex { get; set; }

        public override string ToString() => Symbol;
    }
}
