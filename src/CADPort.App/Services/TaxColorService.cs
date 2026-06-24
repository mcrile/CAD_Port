using System.Windows.Media;
using CADPort.App.Models;

namespace CADPort.App.Services
{
    /// <summary>
    /// Maps a lot's unrealized gain/loss intensity to the dark-green -> white ->
    /// red gradient. Loss is attractive (green); gain is expensive (red).
    /// </summary>
    public static class TaxColorService
    {
        // Intensity (UGL / MV) magnitude that saturates the gradient.
        private const double Saturation = 0.30;

        private static readonly Color DarkGreen = Color.FromRgb(0x1B, 0x9E, 0x3C);
        private static readonly Color LightGreen = Color.FromRgb(0x9B, 0xE0, 0xA8);
        private static readonly Color White = Color.FromRgb(0xF2, 0xF2, 0xF2);
        private static readonly Color Pink = Color.FromRgb(0xF2, 0xA8, 0xB8);
        private static readonly Color Red = Color.FromRgb(0xD6, 0x2C, 0x3B);

        /// <summary>Resolve the fill color for a lot given its account's tax status.</summary>
        public static Color ForLot(TaxLot lot)
        {
            if (lot.Security.IsCash) return White;                 // cash always white
            if (lot.Account == null || !lot.Account.IsTaxable) return White; // tax-exempt white
            return ForIntensity(lot.GainLossIntensity);
        }

        /// <summary>
        /// Map intensity in roughly [-Saturation, +Saturation] to the gradient.
        /// Negative (loss) -> green, zero -> white, positive (gain) -> red.
        /// </summary>
        public static Color ForIntensity(double intensity)
        {
            var t = Math.Max(-1.0, Math.Min(1.0, intensity / Saturation));

            if (t < 0)
            {
                // loss: white -> light green -> dark green
                var a = -t; // 0..1
                return a < 0.5
                    ? Lerp(White, LightGreen, a / 0.5)
                    : Lerp(LightGreen, DarkGreen, (a - 0.5) / 0.5);
            }

            if (t > 0)
            {
                // gain: white -> pink -> red
                var a = t; // 0..1
                return a < 0.5
                    ? Lerp(White, Pink, a / 0.5)
                    : Lerp(Pink, Red, (a - 0.5) / 0.5);
            }

            return White;
        }

        private static Color Lerp(Color from, Color to, double t)
        {
            t = Math.Max(0.0, Math.Min(1.0, t));
            return Color.FromRgb(
                (byte)(from.R + (to.R - from.R) * t),
                (byte)(from.G + (to.G - from.G) * t),
                (byte)(from.B + (to.B - from.B) * t));
        }
    }
}
