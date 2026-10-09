using System;
using System.Globalization;
using System.Windows.Media;

using System.Collections.Generic;
namespace Learnest.Theming
{
    /// <summary>
    /// Colour maths used by the Material Design 3 inspired theme layer: parsing, mixing,
    /// contrast decisions and accent extraction from a background image.
    /// </summary>
    public static class ColorUtil
    {
        public static Color Parse(string hex, Color fallback)
        {
            if (string.IsNullOrEmpty(hex)) { return fallback; }
            string value = hex.Trim();
            if (value.StartsWith("#", StringComparison.Ordinal)) { value = value.Substring(1); }
            try
            {
                if (value.Length == 3)
                {
                    byte r = Nibble(value[0]);
                    byte g = Nibble(value[1]);
                    byte b = Nibble(value[2]);
                    return Color.FromRgb((byte)(r * 17), (byte)(g * 17), (byte)(b * 17));
                }
                if (value.Length == 6)
                {
                    return Color.FromRgb(
                        byte.Parse(value.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                        byte.Parse(value.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                        byte.Parse(value.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                }
                if (value.Length == 8)
                {
                    return Color.FromArgb(
                        byte.Parse(value.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                        byte.Parse(value.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                        byte.Parse(value.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                        byte.Parse(value.Substring(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                }
            }
            catch
            {
            }
            return fallback;
        }

        private static byte Nibble(char c)
        {
            return byte.Parse(c.ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }

        public static string ToHex(Color color)
        {
            return "#" + color.R.ToString("X2", CultureInfo.InvariantCulture) +
                         color.G.ToString("X2", CultureInfo.InvariantCulture) +
                         color.B.ToString("X2", CultureInfo.InvariantCulture);
        }

        public static string ToHexArgb(Color color)
        {
            return "#" + color.A.ToString("X2", CultureInfo.InvariantCulture) + ToHex(color).Substring(1);
        }

        /// <summary>WCAG relative luminance, 0 (black) to 1 (white).</summary>
        public static double Luminance(Color color)
        {
            double r = Channel(color.R);
            double g = Channel(color.G);
            double b = Channel(color.B);
            return 0.2126 * r + 0.7152 * g + 0.0722 * b;
        }

        private static double Channel(byte value)
        {
            double v = value / 255.0;
            return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        }

        public static bool IsDark(Color color)
        {
            return Luminance(color) < 0.42;
        }

        /// <summary>Black or white text, whichever reads better on the given background.</summary>
        public static Color ContrastText(Color background)
        {
            return IsDark(background) ? Colors.White : Color.FromRgb(0x11, 0x14, 0x1A);
        }

        /// <summary>Same as <see cref="ContrastText"/> but honouring a preferred light/dark tone.</summary>
        public static Color ContrastText(Color background, bool preferLight)
        {
            Color preferred = preferLight ? Colors.White : Color.FromRgb(0x11, 0x14, 0x1A);
            double ratio = ContrastRatio(background, preferred);
            if (ratio >= 3.0) { return preferred; }
            return ContrastText(background);
        }

        public static double ContrastRatio(Color a, Color b)
        {
            double la = Luminance(a);
            double lb = Luminance(b);
            double lighter = Math.Max(la, lb);
            double darker = Math.Min(la, lb);
            return (lighter + 0.05) / (darker + 0.05);
        }

        /// <summary>Blend toward white (amount &gt; 0) or black (amount &lt; 0).</summary>
        public static Color Shade(Color color, double amount)
        {
            if (amount > 1) { amount = 1; }
            if (amount < -1) { amount = -1; }
            if (amount >= 0)
            {
                return Color.FromRgb(
                    Mix(color.R, 255, amount),
                    Mix(color.G, 255, amount),
                    Mix(color.B, 255, amount));
            }
            return Color.FromRgb(
                Mix(color.R, 0, -amount),
                Mix(color.G, 0, -amount),
                Mix(color.B, 0, -amount));
        }

        public static Color Tint(Color color, double amount)
        {
            return Shade(color, Math.Abs(amount));
        }

        private static byte Mix(byte from, byte to, double amount)
        {
            double value = from + (to - from) * amount;
            if (value < 0) { value = 0; }
            if (value > 255) { value = 255; }
            return (byte)Math.Round(value);
        }

        public static Color Blend(Color a, Color b, double amount)
        {
            if (amount < 0) { amount = 0; }
            if (amount > 1) { amount = 1; }
            return Color.FromArgb(
                (byte)Math.Round(a.A + (b.A - a.A) * amount),
                Mix(a.R, b.R, amount),
                Mix(a.G, b.G, amount),
                Mix(a.B, b.B, amount));
        }

        /// <summary>Rotate the hue of a colour, keeping saturation and brightness.</summary>
        public static Color ShiftHue(Color color, double degrees)
        {
            double h, s, v;
            ToHsv(color, out h, out s, out v);
            h = (h + degrees) % 360;
            if (h < 0) { h += 360; }
            return FromHsv(h, s, v);
        }

        public static void ToHsv(Color color, out double hue, out double saturation, out double value)
        {
            double r = color.R / 255.0;
            double g = color.G / 255.0;
            double b = color.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            double delta = max - min;

            value = max;
            saturation = max <= 0 ? 0 : delta / max;
            if (delta <= 0) { hue = 0; return; }
            if (max == r) { hue = 60 * (((g - b) / delta) % 6); }
            else if (max == g) { hue = 60 * (((b - r) / delta) + 2); }
            else { hue = 60 * (((r - g) / delta) + 4); }
            if (hue < 0) { hue += 360; }
        }

        public static Color FromHsv(double hue, double saturation, double value)
        {
            hue = ((hue % 360) + 360) % 360;
            double c = value * saturation;
            double x = c * (1 - Math.Abs(((hue / 60) % 2) - 1));
            double m = value - c;
            double r = 0, g = 0, b = 0;
            if (hue < 60) { r = c; g = x; }
            else if (hue < 120) { r = x; g = c; }
            else if (hue < 180) { g = c; b = x; }
            else if (hue < 240) { g = x; b = c; }
            else if (hue < 300) { r = x; b = c; }
            else { r = c; b = x; }
            return Color.FromRgb(
                (byte)Math.Round((r + m) * 255),
                (byte)Math.Round((g + m) * 255),
                (byte)Math.Round((b + m) * 255));
        }

        /// <summary>
        /// Make a seed colour usable as an MD3 primary: keep the hue, clamp saturation and
        /// lightness into a range that reads well on both light and dark surfaces.
        /// </summary>
        public static Color NormaliseAccent(Color seed, bool darkTheme)
        {
            double hue, saturation, value;
            ToHsv(seed, out hue, out saturation, out value);
            if (saturation < 0.18) { saturation = 0.18; }
            if (saturation > 0.85) { saturation = 0.85; }
            if (darkTheme)
            {
                if (value < 0.62) { value = 0.62; }
                if (value > 0.95) { value = 0.95; }
            }
            else
            {
                if (value < 0.42) { value = 0.42; }
                if (value > 0.82) { value = 0.82; }
            }
            return FromHsv(hue, saturation, value);
        }

        public static Color FromBytes(byte r, byte g, byte b)
        {
            return Color.FromRgb(r, g, b);
        }

        /// <summary>
        /// Pick a representable, attractive accent out of an image: the most common colour that
        /// is colourful enough to read as a brand colour, biased away from near-black and
        /// near-white.  Returns null when the image has no usable colour.
        /// </summary>
        public static Color? DominantAccent(System.Windows.Media.Imaging.BitmapSource source)
        {
            if (source == null) { return null; }
            try
            {
                // Work on a small thumbnail: fast, and it averages away noise.
                System.Windows.Media.Imaging.TransformedBitmap scaled =
                    new System.Windows.Media.Imaging.TransformedBitmap(source,
                        new ScaleTransform(40.0 / Math.Max(1, source.PixelWidth), 40.0 / Math.Max(1, source.PixelWidth)));
                System.Windows.Media.Imaging.FormatConvertedBitmap converted =
                    new System.Windows.Media.Imaging.FormatConvertedBitmap(scaled, PixelFormats.Bgra32, null, 0);
                int stride = converted.PixelWidth * 4;
                byte[] pixels = new byte[stride * converted.PixelHeight];
                converted.CopyPixels(pixels, stride, 0);

                Dictionary<int, int> buckets = new Dictionary<int, int>();
                Dictionary<int, long[]> sums = new Dictionary<int, long[]>();
                for (int i = 0; i + 3 < pixels.Length; i += 4)
                {
                    byte b = pixels[i];
                    byte g = pixels[i + 1];
                    byte r = pixels[i + 2];
                    byte a = pixels[i + 3];
                    if (a < 128) { continue; }

                    Color sample = Color.FromRgb(r, g, b);
                    double hue, saturation, value;
                    ToHsv(sample, out hue, out saturation, out value);
                    if (saturation < 0.20) { continue; }     // grey / white / black
                    if (value < 0.18 || value > 0.96) { continue; }

                    int key = ((r >> 4) << 8) | ((g >> 4) << 4) | (b >> 4);
                    int count;
                    buckets.TryGetValue(key, out count);
                    buckets[key] = count + 1;

                    long[] accumulator;
                    if (!sums.TryGetValue(key, out accumulator)) { accumulator = new long[3]; sums[key] = accumulator; }
                    accumulator[0] += r;
                    accumulator[1] += g;
                    accumulator[2] += b;
                }

                if (buckets.Count == 0) { return null; }

                int bestKey = -1;
                int bestCount = -1;
                foreach (KeyValuePair<int, int> pair in buckets)
                {
                    // Slight preference for saturated, mid bright colours via a weighted score.
                    long[] accumulator = sums[pair.Key];
                    Color average = Color.FromRgb(
                        (byte)(accumulator[0] / pair.Value),
                        (byte)(accumulator[1] / pair.Value),
                        (byte)(accumulator[2] / pair.Value));
                    double hue, saturation, value;
                    ToHsv(average, out hue, out saturation, out value);
                    double score = pair.Value * (0.55 + saturation) * (1 - Math.Abs(value - 0.6));
                    if (score > bestCount)
                    {
                        bestCount = (int)Math.Round(score);
                        bestKey = pair.Key;
                    }
                }
                if (bestKey < 0) { return null; }
                long[] chosen = sums[bestKey];
                int n = buckets[bestKey];
                return Color.FromRgb((byte)(chosen[0] / n), (byte)(chosen[1] / n), (byte)(chosen[2] / n));
            }
            catch
            {
                return null;
            }
        }

        /// <summary>Soft shadow colour derived from the theme tone.</summary>
        public static Color ShadowFor(bool dark)
        {
            return dark ? Color.FromArgb(0x66, 0, 0, 0) : Color.FromArgb(0x1F, 0x0F, 0x17, 0x2A);
        }
    }
}
