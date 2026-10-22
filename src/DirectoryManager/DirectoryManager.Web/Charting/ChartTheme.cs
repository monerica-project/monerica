using System.Collections;
using System.Reflection;
using ScottPlot;

namespace DirectoryManager.Web.Charting
{
    /// <summary>
    /// One dark, high-visibility look for every chart image: black background, light text,
    /// grid and legend, and series colours brightened to neon (hue kept, so series stay
    /// distinguishable). Applied right before a chart is rendered.
    /// </summary>
    public static class ChartTheme
    {
        public const string BackgroundHex = "#05070a";
        public const string InkHex = "#f2f2f2";
        public const string MutedHex = "#9aa3ad";
        public const string GridHex = "#1f242b";

        private static readonly Color Background = Color.FromHex(BackgroundHex);
        private static readonly Color Ink = Color.FromHex(InkHex);

        // Accent for fills that were black/grey (e.g. single-series bar charts): the site's orange.
        private static readonly Color Accent = Color.FromHex("#ff8c1a");

        /// <summary>Renders the chart with the dark theme applied (same arguments as GetImageBytes).</summary>
        public static byte[] GetDarkImageBytes(this Plot plt, int width, int height, ImageFormat format = ImageFormat.Png)
        {
            ApplyDark(plt);
            return plt.GetImageBytes(width, height, format);
        }

        public static void ApplyDark(Plot plt)
        {
            plt.FigureBackground.Color = Background;
            plt.DataBackground.Color = Background;
            plt.Axes.Color(Color.FromHex(MutedHex));
            plt.Axes.Title.Label.ForeColor = Ink;
            plt.Grid.MajorLineColor = Color.FromHex(GridHex);
            plt.Legend.BackgroundColor = Color.FromHex("#0d1117");
            plt.Legend.FontColor = Ink;
            plt.Legend.OutlineColor = Color.FromHex("#30363d");

            // One chart's colours are adapted once: the same colour is often exposed through several
            // properties (Color, LineColor, LineStyle.Color…), and adapting an already-adapted colour
            // again would change it a second time (e.g. a black line → ink → background = invisible).
            var adapted = new HashSet<Color>();
            foreach (var p in plt.GetPlottables())
            {
                Recolor(p, depth: 0, inFill: false, adapted);
            }
        }

        // Walks a plottable's colour properties (and nested styles / bar / slice lists) and
        // adapts each colour for a black background.
        private static void Recolor(object? target, int depth, bool inFill, HashSet<Color> adapted)
        {
            if (target is null || depth > 3)
            {
                return;
            }

            if (target is IEnumerable list && target is not string)
            {
                foreach (var item in list)
                {
                    if (item != null && IsScottPlotType(item.GetType()))
                    {
                        Recolor(item, depth + 1, inFill, adapted);
                    }
                }

                return;
            }

            foreach (var prop in target.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (prop.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                if (prop.PropertyType == typeof(Color))
                {
                    if (!prop.CanRead || !prop.CanWrite)
                    {
                        continue;
                    }

                    var c = (Color)prop.GetValue(target)!;
                    if (adapted.Contains(c))
                    {
                        continue; // already a themed colour
                    }

                    var themed = Adapt(c, inFill ? "Fill" + prop.Name : prop.Name);
                    adapted.Add(themed);
                    prop.SetValue(target, themed);
                }
                else if (prop.CanRead && IsNestedStyle(prop))
                {
                    object? value;
                    try
                    {
                        value = prop.GetValue(target);
                    }
                    catch
                    {
                        continue;
                    }

                    // Colours inside a FillStyle are fills even when the property is just "Color".
                    Recolor(value, depth + 1, inFill || prop.Name.Contains("Fill", StringComparison.Ordinal), adapted);
                }
            }
        }

        private static bool IsScottPlotType(Type t) => t.Namespace?.StartsWith("ScottPlot", StringComparison.Ordinal) == true;

        // Style objects (LineStyle, FillStyle, MarkerStyle, LabelStyle…) and the Bars / Slices lists.
        private static bool IsNestedStyle(PropertyInfo p)
        {
            var t = p.PropertyType;
            if (p.Name is "Bars" or "Slices")
            {
                return true;
            }

            return IsScottPlotType(t) && !t.IsValueType && !t.IsEnum &&
                   (t.Name.EndsWith("Style", StringComparison.Ordinal) || t.Name is "Bar" or "PieSlice");
        }

        // Text → light if it was dark. Backgrounds of labels → dark if they were white.
        // Lines / fills / markers → near-black becomes ink, other colours are brightened.
        private static Color Adapt(Color c, string propertyName)
        {
            if (c.A == 0)
            {
                return c;
            }

            var lum = Luminance(c);
            var name = propertyName;

            if (name.Contains("Font", StringComparison.Ordinal) || name.Contains("Fore", StringComparison.Ordinal))
            {
                return lum < 0.5 ? Ink.WithAlpha(c.A) : c;
            }

            if (name.Contains("Background", StringComparison.Ordinal))
            {
                return lum > 0.85 ? Background.WithAlpha(c.A) : c;
            }

            if (lum > 0.92)
            {
                // White separators / borders (e.g. between pie slices) → background colour.
                return name.Contains("Line", StringComparison.Ordinal) || name.Contains("Border", StringComparison.Ordinal) || name.Contains("Outline", StringComparison.Ordinal)
                    ? Background.WithAlpha(c.A)
                    : c;
            }

            return Brighten(c, isFill: name.Contains("Fill", StringComparison.Ordinal));
        }

        private static double Luminance(Color c) => ((0.2126 * c.R) + (0.7152 * c.G) + (0.0722 * c.B)) / 255.0;

        // Keep the hue; push saturation and lightness up so it glows on black. Greys stay grey (lighter).
        private static Color Brighten(Color c, bool isFill)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
            double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
            double l = (max + min) / 2, h = 0, s = 0;
            if (max - min > 1e-6)
            {
                double d = max - min;
                s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
                h = max == r ? ((g - b) / d) + (g < b ? 6 : 0) : max == g ? ((b - r) / d) + 2 : ((r - g) / d) + 4;
                h /= 6;
            }

            if (s < 0.12)
            {
                // Grey / black: fills become the orange accent (bars), lines/markers become light ink.
                if (isFill)
                {
                    return l < 0.2 ? Accent.WithAlpha(c.A) : Color.FromHex("#8b949e").WithAlpha(c.A);
                }

                return l < 0.2 ? Ink.WithAlpha(c.A) : Color.FromHex("#c8ced6").WithAlpha(c.A);
            }

            s = Math.Max(s, 0.85);
            l = Math.Clamp(Math.Max(l, 0.6), 0, 0.72);

            double q = l < 0.5 ? l * (1 + s) : l + s - (l * s);
            double p2 = (2 * l) - q;
            byte ToByte(double t)
            {
                if (t < 0)
                {
                    t += 1;
                }

                if (t > 1)
                {
                    t -= 1;
                }

                double v = t < 1.0 / 6 ? p2 + ((q - p2) * 6 * t)
                         : t < 0.5 ? q
                         : t < 2.0 / 3 ? p2 + ((q - p2) * ((2.0 / 3) - t) * 6)
                         : p2;
                return (byte)Math.Round(Math.Clamp(v, 0, 1) * 255);
            }

            return new Color(ToByte(h + (1.0 / 3)), ToByte(h), ToByte(h - (1.0 / 3)), c.A);
        }
    }
}
