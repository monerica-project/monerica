using System.Globalization;
using DirectoryManager.Common.Helpers;
using DirectoryManager.Data.Enums;
using DirectoryManager.Data.Models.SponsoredListings;
using DirectoryManager.Web.Constants;
using DirectoryManager.Web.Helpers;
using ScottPlot;

namespace DirectoryManager.Web.Charting
{
    public class InvoicePlotting
    {
        private const string Culture = StringConstants.EnglishUS;

        public byte[] CreateMonthlyAvgPerListingDailyPriceChart(
            IEnumerable<SponsoredListingInvoice> invoices,
            Currency displayCurrency,
            DateTime rangeStart,
            DateTime rangeEnd,
            string? filterLabel = null)
        {
            var paid = (invoices ?? Enumerable.Empty<SponsoredListingInvoice>()).ToList();
            if (!paid.Any())
            {
                return Array.Empty<byte>();
            }

            // Build inclusive month list for [rangeStart, rangeEnd]
            var firstMonth = new DateTime(rangeStart.Year, rangeStart.Month, 1);
            var lastMonth = new DateTime(rangeEnd.Year, rangeEnd.Month, 1);

            var months = new List<DateTime>();
            for (var m = firstMonth; m <= lastMonth; m = m.AddMonths(1))
            {
                months.Add(m);
            }

            // Helper: inclusive day count (>=1 for valid ranges)
            static int InclusiveDays(DateTime start, DateTime end)
            {
                var s = start.Date;
                var eOpen = end.Date.AddDays(1);
                var days = (int)(eOpen - s).TotalDays;
                return Math.Max(0, days);
            }

            // For each month:
            //   - compute each invoice's per-day rate: amount / total_campaign_days
            //   - weight by the number of overlap days WITHIN that month
            //   - Month's metric = (sum(rate * overlapDays)) / (sum(overlapDays))
            //     i.e., a weighted average of per-day price across all active listing-days
            var monthData = months.Select(m =>
            {
                var ms = m;
                var me = m.AddMonths(1).AddDays(-1);

                decimal weightedSum = 0m;
                long weightDays = 0;

                foreach (var inv in paid)
                {
                    var amt = inv.AmountIn(displayCurrency);
                    if (amt <= 0m)
                    {
                        continue;
                    }

                    var s = inv.CampaignStartDate.Date;
                    var e = inv.CampaignEndDate.Date;
                    if (e < s)
                    {
                        continue;
                    }

                    var totalCampaignDays = InclusiveDays(s, e);
                    if (totalCampaignDays <= 0)
                    {
                        continue;
                    }

                    // overlap with this month (inclusive)
                    var os = s > ms ? s : ms;
                    var oe = e < me ? e : me;
                    if (oe < os)
                    {
                        continue;
                    }

                    var overlapDays = InclusiveDays(os, oe);
                    if (overlapDays <= 0)
                    {
                        continue;
                    }

                    var perDay = amt / totalCampaignDays; // daily price this listing paid
                    weightedSum += perDay * overlapDays;
                    weightDays += overlapDays;
                }

                decimal avgPerListingPerDay = weightDays > 0 ? (weightedSum / weightDays) : 0m;
                return new { Month = m, AvgPerListingPerDay = avgPerListingPerDay, SampleListingDays = weightDays };
            }).ToList();

            if (monthData.All(d => d.AvgPerListingPerDay <= 0m))
            {
                return Array.Empty<byte>();
            }

            // Bars (you can swap to a line if you prefer)
            var bars = monthData.Select((d, idx) => new Bar { Position = idx, Value = (double)d.AvgPerListingPerDay }).ToList();

            var plt = new Plot();
            plt.Add.Bars(bars);

            ApplyMonthCategoryTicks(plt, months);
            plt.Axes.Margins(left: 0.08, right: 0.08, bottom: 0.30, top: 0.18);
            plt.Axes.AutoScale();
            PadXAxisForBars(plt, bars.Count, rightPad: 1.0);

            double maxBar = Math.Max(0, bars.Max(b => b.Value));
            double yOffset = Math.Max(maxBar * 0.025, 0.001);
            var lim = plt.Axes.GetLimits();
            double neededTop = Math.Max(lim.Top, maxBar + Math.Max(yOffset * 1.15, 0.002));
            if (lim.Bottom != 0 || lim.Top < neededTop)
            {
                plt.Axes.SetLimitsY(0, neededTop);
            }

            string ValueLabel(decimal v)
            {
                if (displayCurrency == Currency.USD)
                {
                    return v.ToString("C", CultureInfo.CreateSpecificCulture(Culture));
                }

                // Non-USD: print compact decimals
                return v >= 1m ? v.ToString("0.000")
                     : v >= 0.1m ? v.ToString("0.0000")
                     : v >= 0.01m ? v.ToString("0.00000")
                     : v >= 0.001m ? v.ToString("0.000000")
                     : v.ToString("0.0000000").TrimEnd('0').TrimEnd('.');
            }

            for (int i = 0; i < bars.Count; i++)
            {
                double x = bars[i].Position;
                double y = bars[i].Value;
                var txt = plt.Add.Text(ValueLabel((decimal)y), x, y + yOffset);
                txt.Alignment = ScottPlot.Alignment.LowerCenter;
                txt.LabelFontSize = 12;
            }

            string unit = displayCurrency == Currency.USD ? "USD" : displayCurrency.ToString();
            plt.Title($"Avg Daily Price per Listing ({unit}/day)");
            plt.XLabel("Month");
            plt.YLabel($"{unit} per listing per day");

            // Show filter + sample size in subtitle for context
            if (!string.IsNullOrWhiteSpace(filterLabel))
            {
                var totalListingDays = monthData.Sum(d => d.SampleListingDays);
                var subtitle = $"{filterLabel} — Weighted by listing-days (total sample: {totalListingDays:n0})";
                AddSubtitleBelowTitle(plt, subtitle);
            }

            return plt.GetImageBytes(1200, 600, ImageFormat.Png);
        }

        /// <summary>
        /// Pricing-trends chart: the average PAID price-per-day for a sponsorship slot, bucketed by
        /// calendar day across the range. When <paramref name="sponsorshipType"/> is a single type
        /// it renders as a daily BAR chart for that placement; when null it overlays all three
        /// placement types (Main / Category / Subcategory) as daily line-with-markers series so the
        /// trends can be compared. Only PAID invoices should be passed in.
        /// </summary>
        public byte[] CreateDailyAvgPriceByTypeChart(
            IEnumerable<SponsoredListingInvoice> invoices,
            Currency displayCurrency,
            DateTime rangeStart,
            DateTime rangeEnd,
            SponsorshipType? sponsorshipType,
            string? filterLabel = null,
            int width = 1400,
            int height = 640,
            IEnumerable<SponsoredListingInvoice>? highlightInvoices = null,
            string? highlightLabel = null,
            IReadOnlyDictionary<SponsorshipType, decimal>? currentMarketDailyByType = null)
        {
            var data = PricingTrendCalculator.Build(invoices, displayCurrency, rangeStart, rangeEnd);
            if (data.Days.Count == 0 || !data.HasAnyData)
            {
                return Array.Empty<byte>();
            }

            // Single-placement view with no paid data for that specific placement → nothing to draw.
            if (sponsorshipType.HasValue &&
                (!data.Stats.TryGetValue(sponsorshipType.Value, out var selStat) || selStat.DaysWithData == 0))
            {
                return Array.Empty<byte>();
            }

            var days = data.Days;
            int n = days.Count;
            string unit = AxisUnitLabel(displayCurrency);

            string DailyLabel(decimal v)
            {
                if (displayCurrency == Currency.USD)
                {
                    return v.ToString("C", CultureInfo.CreateSpecificCulture(Culture));
                }

                return v >= 1m ? v.ToString("0.000")
                     : v >= 0.01m ? v.ToString("0.00000")
                     : v.ToString("0.0000000").TrimEnd('0').TrimEnd('.');
            }

            var plt = new Plot();

            // ScottPlot's ShowLegend(Edge) overload adds a NEW legend panel on every call, so calling
            // it per-series stacks duplicate legends at the bottom. Flag it here and show it exactly once.
            bool showLegend = false;

            var typesToPlot = sponsorshipType.HasValue
                ? new[] { sponsorshipType.Value }
                : PricingTrendCalculator.Types;

            double maxVal = 0;

            // Current market price (per day) for each plotted placement — what a slot costs to buy
            // right now, so buyers can compare today's price against the historical average.
            var currentDaily = new List<(SponsorshipType Type, double Value)>();
            if (currentMarketDailyByType != null)
            {
                foreach (var t in typesToPlot)
                {
                    if (currentMarketDailyByType.TryGetValue(t, out var d) && d > 0m)
                    {
                        currentDaily.Add((t, (double)d));
                    }
                }
            }

            if (sponsorshipType.HasValue)
            {
                // Single placement → daily bars.
                var t = sponsorshipType.Value;
                var series = data.DailyValues[t];
                var fill = Color.FromHex(TypeBarColorHex(t));
                var bars = new List<Bar>();
                for (int i = 0; i < n; i++)
                {
                    double y = double.IsNaN(series[i]) ? 0d : series[i];
                    if (y > maxVal)
                    {
                        maxVal = y;
                    }

                    bars.Add(new Bar { Position = i, Value = y, FillColor = fill });
                }

                var barPlot = plt.Add.Bars(bars);
                PadXAxisForBars(plt, n, rightPad: 0.5);

                // Per-bar value labels are intentionally omitted — at daily granularity they overlap
                // and become unreadable; the Y axis already shows the price. Read exact values off the
                // axis / gridlines instead.

                // Overlay: the buyer's OWN segment as a black dotted line, so they can see how their
                // slice (their subcategory / category, or their own listing for main) compares to the
                // overall average for this placement — but only when that segment has paid history.
                if (highlightInvoices != null)
                {
                    var subData = PricingTrendCalculator.Build(highlightInvoices, displayCurrency, rangeStart, rangeEnd);

                    if (subData.Stats.TryGetValue(t, out var subStat) && subStat.DaysWithData > 0)
                    {
                        var subSeries = subData.DailyValues[t];
                        foreach (var v in subSeries)
                        {
                            if (!double.IsNaN(v) && v > maxVal)
                            {
                                maxVal = v;
                            }
                        }

                        var subXs = Enumerable.Range(0, n).Select(i => (double)i).ToArray();
                        var subLine = plt.Add.Scatter(subXs, subSeries);
                        subLine.Color = Colors.Black;
                        subLine.LineWidth = 2;
                        subLine.LinePattern = LinePattern.Dotted;
                        subLine.MarkerSize = 0;
                        subLine.LegendText = string.IsNullOrWhiteSpace(highlightLabel) ? "Your subcategory" : highlightLabel;

                        barPlot.LegendText = $"All {FriendlyType(t)} sponsors";
                        showLegend = true;
                    }
                    else
                    {
                        // No activity for this segment WITHIN the 6-month window, but it may have older
                        // paid history — show its historical average per-day as a flat dotted reference
                        // line so the buyer still sees what their niche has paid before.
                        decimal totalAmount = 0m;
                        long totalDays = 0;
                        foreach (var inv in highlightInvoices)
                        {
                            if (inv.SponsorshipType != t)
                            {
                                continue;
                            }

                            var amt = inv.AmountIn(displayCurrency);
                            if (amt <= 0m)
                            {
                                continue;
                            }

                            var cs = inv.CampaignStartDate.Date;
                            var ce = inv.CampaignEndDate.Date;
                            if (ce < cs)
                            {
                                continue;
                            }

                            var span = ce.AddDays(1) - cs;
                            var campaignDays = (int)span.TotalDays;
                            if (campaignDays <= 0)
                            {
                                continue;
                            }

                            totalAmount += amt;
                            totalDays += campaignDays;
                        }

                        if (totalDays > 0)
                        {
                            decimal avgPerDay = totalAmount / totalDays;
                            double refVal = (double)avgPerDay;
                            if (refVal > maxVal)
                            {
                                maxVal = refVal;
                            }

                            var refLine = plt.Add.HorizontalLine(refVal);
                            refLine.Color = Colors.Black;
                            refLine.LineWidth = 2;
                            refLine.LinePattern = LinePattern.Dotted;
                            refLine.LegendText =
                                $"{(string.IsNullOrWhiteSpace(highlightLabel) ? "Your subcategory" : highlightLabel)} (past avg)";

                            barPlot.LegendText = $"All {FriendlyType(t)} sponsors";
                            showLegend = true;
                        }
                    }
                }

                plt.Title($"Avg Daily Slot Price — {FriendlyType(t)} ({unit}/day)");
            }
            else
            {
                // All placements → overlaid daily line-with-markers series (NaN = gap).
                var xs = Enumerable.Range(0, n).Select(i => (double)i).ToArray();
                foreach (var t in typesToPlot)
                {
                    var series = data.DailyValues[t];
                    if (data.Stats[t].DaysWithData == 0)
                    {
                        continue;
                    }

                    foreach (var v in series)
                    {
                        if (!double.IsNaN(v) && v > maxVal)
                        {
                            maxVal = v;
                        }
                    }

                    var sc = plt.Add.Scatter(xs, series);
                    sc.Color = Color.FromHex(TypeColorHex(t));
                    sc.LineWidth = 2;
                    sc.MarkerSize = n <= 120 ? 5 : 0;
                    sc.LegendText = $"{FriendlyType(t)} (avg {DailyLabel(data.Stats[t].AvgPerDay)}/day)";
                }

                showLegend = true;
                plt.Title($"Avg Daily Slot Price by Placement ({unit}/day)");
            }

            // Current market price line(s), in orange, so today's asking price stands out against history.
            foreach (var c in currentDaily)
            {
                if (c.Value > maxVal)
                {
                    maxVal = c.Value;
                }

                var nowLine = plt.Add.HorizontalLine(c.Value);
                nowLine.Color = Colors.Black;
                nowLine.LineWidth = 2;
                nowLine.LinePattern = LinePattern.Dashed;
                nowLine.LegendText = sponsorshipType.HasValue
                    ? $"Current market price ({DailyLabel((decimal)c.Value)}/day)"
                    : $"{FriendlyType(c.Type)} current ({DailyLabel((decimal)c.Value)}/day)";
            }

            if (currentDaily.Count > 0)
            {
                showLegend = true;
            }

            if (showLegend)
            {
                plt.ShowLegend(Edge.Bottom);
            }

            ApplyDailyTicksThinned(plt, days);
            plt.Axes.Margins(left: 0.06, right: 0.06, bottom: 0.28, top: 0.18);
            plt.Axes.AutoScale();

            if (sponsorshipType.HasValue)
            {
                PadXAxisForBars(plt, n, rightPad: 0.5);
            }

            var lim = plt.Axes.GetLimits();
            double top = Math.Max(lim.Top, (maxVal * 1.12) + 0.0001);
            plt.Axes.SetLimitsY(0, top);

            // The final (far-right) date tick is centered on the last day, which sits at the very
            // edge — without extra room its right half is clipped off the image. Reserve ~half a
            // date label's width on the right, scaled to the pixel size so it works at any width.
            var xLim = plt.Axes.GetLimits();
            double xRange = xLim.Right - xLim.Left;
            if (xRange > 0)
            {
                double unitsPerPixel = xRange / Math.Max(1.0, width * 0.90);

                // Reserve extra room on the right when we're printing the current-price label there,
                // so the USD amount is fully visible and not clipped at the edge.
                double reservePixels = currentDaily.Count > 0 ? 165.0 : 46.0;
                double reserve = reservePixels * unitsPerPixel;
                plt.Axes.SetLimitsX(xLim.Left, xLim.Right + reserve);
            }

            // Print the current market price at the far right, on its line, in the reserved margin.
            if (currentDaily.Count > 0)
            {
                var lbl = plt.Axes.GetLimits();
                foreach (var c in currentDaily)
                {
                    var txt = plt.Add.Text($"Now: {DailyLabel((decimal)c.Value)}/day", lbl.Right, c.Value);
                    txt.Alignment = ScottPlot.Alignment.LowerRight;
                    txt.LabelFontSize = 13;
                    txt.LabelBold = true;
                    txt.LabelFontColor = Colors.Black;
                }
            }

            plt.XLabel("Day");
            plt.YLabel($"{unit} per day");

            if (!string.IsNullOrWhiteSpace(filterLabel))
            {
                var totalListingDays = typesToPlot.Sum(t => data.Stats[t].ActiveListingDays);
                AddSubtitleBelowTitle(plt, $"{filterLabel} — {totalListingDays:n0} active listing-days in range");
            }

            return plt.GetImageBytes(width, height, ImageFormat.Png);
        }

        // Saturated colours for the compare-mode LINES (need contrast on a white background).
        private static string TypeColorHex(SponsorshipType t) => t switch
        {
            SponsorshipType.MainSponsor => "#e68c28",       // orange
            SponsorshipType.CategorySponsor => "#3c9a5f",   // green
            SponsorshipType.SubcategorySponsor => "#3a6ea5", // blue
            _ => "#8a8a8a",
        };

        // Lighter fills for BAR charts so the black dotted overlay line stays easy to see on top.
        private static string TypeBarColorHex(SponsorshipType t) => t switch
        {
            SponsorshipType.MainSponsor => "#f4b877",       // light orange
            SponsorshipType.CategorySponsor => "#82cfa4",   // light green
            SponsorshipType.SubcategorySponsor => "#9cc6ea", // light blue
            _ => "#c2c2c2",
        };

        private static string FriendlyType(SponsorshipType t) => t switch
        {
            SponsorshipType.MainSponsor => "Main",
            SponsorshipType.CategorySponsor => "Category",
            SponsorshipType.SubcategorySponsor => "Subcategory",
            _ => t.ToString(),
        };

        private static void ApplyDailyTicksThinned(ScottPlot.Plot plt, IReadOnlyList<DateTime> days)
        {
            int count = days.Count;
            if (count == 0)
            {
                return;
            }

            const int target = 12;
            int stride = Math.Max(1, (int)Math.Ceiling(count / (double)target));

            var ticks = new List<double>();
            var labels = new List<string>();

            void AddTick(int i)
            {
                ticks.Add(i);
                labels.Add($"{days[i]:MMM d}\n{days[i]:yyyy}");
            }

            for (int i = 0; i < count; i += stride)
            {
                AddTick(i);
            }

            // Always anchor the final day so the range end is labeled — but if the last regular
            // tick lands within ~0.6 strides of it, the two date labels collide (the "partial
            // last month" overwrite). In that case drop the crowding regular tick first.
            int last = count - 1;
            if (ticks.Count == 0)
            {
                AddTick(last);
            }
            else if ((int)ticks[ticks.Count - 1] != last)
            {
                if (last - (int)ticks[ticks.Count - 1] < stride * 0.6)
                {
                    ticks.RemoveAt(ticks.Count - 1);
                    labels.RemoveAt(labels.Count - 1);
                }

                AddTick(last);
            }

            plt.Axes.Bottom.TickGenerator =
                new ScottPlot.TickGenerators.NumericManual(ticks.ToArray(), labels.ToArray());
            plt.Axes.Bottom.TickLabelStyle.Rotation = 0;
        }

        public byte[] CreateMonthlyIncomeBarChart(
            IEnumerable<SponsoredListingInvoice> invoices,
            Currency displayCurrency,
            string? filterLabel = null)
        {
            var list = invoices?.ToList() ?? new ();
            if (list.Count == 0)
            {
                return Array.Empty<byte>();
            }

            var grouped = list
                .GroupBy(i => new DateTime(i.CreateDate.Year, i.CreateDate.Month, 1))
                .OrderBy(g => g.Key)
                .Select(g => new { Month = g.Key, Total = g.Sum(inv => inv.AmountIn(displayCurrency)) })
                .Where(x => x.Total > 0m)
                .ToList();

            if (grouped.Count == 0)
            {
                return Array.Empty<byte>();
            }

            var bars = grouped.Select((d, idx) => new Bar { Position = idx, Value = (double)d.Total }).ToList();

            var plt = new Plot();
            plt.Add.Bars(bars);

            ApplyMonthCategoryTicks(plt, grouped.Select(g => g.Month).ToList());
            plt.Axes.Margins(left: 0.08, right: 0.08, bottom: 0.30, top: 0.18);
            plt.Axes.AutoScale();
            PadXAxisForBars(plt, bars.Count, rightPad: 1.0);

            double maxBar = Math.Max(0, bars.Max(b => b.Value));
            double yOffset = Math.Max(maxBar * 0.025, 0.001);

            var lim = plt.Axes.GetLimits();
            double neededTop = Math.Max(lim.Top, maxBar + (yOffset * 1.15));
            if (lim.Bottom != 0 || lim.Top < neededTop)
            {
                plt.Axes.SetLimitsY(0, neededTop);
            }

            for (int i = 0; i < bars.Count; i++)
            {
                double x = bars[i].Position;
                double y = bars[i].Value;
                string label = displayCurrency == Currency.USD
                    ? ((decimal)y).ToString("C0", CultureInfo.CreateSpecificCulture(Culture))
                    : $"{(decimal)y:0.######}";
                var txt = plt.Add.Text(label, x, y + yOffset);
                txt.Alignment = ScottPlot.Alignment.LowerCenter;
                txt.LabelFontSize = 12;
            }

            string unit = displayCurrency == Currency.USD ? "USD" : displayCurrency.ToString();
            plt.Title($"Monthly Income ({unit})");
            plt.YLabel($"Total ({unit})");

            plt.Title($"Monthly Income ({unit})");
            plt.YLabel($"Total ({unit})");

            AddSubtitleBelowTitle(plt, filterLabel);

            return plt.GetImageBytes(1200, 800, ImageFormat.Png);
        }

        public byte[] CreateMonthlyAvgDailyRevenueChart(
            IEnumerable<SponsoredListingInvoice> invoices,
            Currency displayCurrency,
            DateTime rangeStart,
            DateTime rangeEnd,
            string? filterLabel = null)
        {
            var paid = (invoices ?? Enumerable.Empty<SponsoredListingInvoice>()).ToList();
            if (!paid.Any())
            {
                return Array.Empty<byte>();
            }

            var firstMonth = new DateTime(rangeStart.Year, rangeStart.Month, 1);
            var lastMonth = new DateTime(rangeEnd.Year, rangeEnd.Month, 1);

            var months = new List<DateTime>();
            for (var m = firstMonth; m <= lastMonth; m = m.AddMonths(1))
            {
                months.Add(m);
            }

            var data = months.Select(m =>
            {
                int daysInMonth = DateTime.DaysInMonth(m.Year, m.Month);
                var ms = m;
                var me = m.AddMonths(1).AddDays(-1);
                decimal total = 0m;

                foreach (var inv in paid)
                {
                    var amt = inv.AmountIn(displayCurrency);
                    if (amt <= 0m)
                    {
                        continue;
                    }

                    var s = inv.CampaignStartDate.Date;
                    var e = inv.CampaignEndDate.Date;
                    if (e < s)
                    {
                        continue;
                    }

                    // inclusive campaign days (matches your other code)
                    var spanDays = (decimal)((e - s).TotalDays + 1);
                    if (spanDays <= 0)
                    {
                        continue;
                    }

                    // overlap with the month
                    var os = s > ms ? s : ms;
                    var oe = e < me ? e : me;
                    if (oe < os)
                    {
                        continue;
                    }

                    var overlapDays = (decimal)((oe - os).TotalDays + 1);
                    total += (amt / spanDays) * overlapDays;
                }

                return new { Month = m, AvgPerDay = daysInMonth > 0 ? total / daysInMonth : 0m };
            }).ToList();

            if (data.All(d => d.AvgPerDay <= 0m))
            {
                return Array.Empty<byte>();
            }

            var now = DateTime.UtcNow;

            var bars = data.Select((d, idx) => new Bar
            {
                Position = idx,
                Value = (double)d.AvgPerDay,
                FillColor = (d.Month.Year == now.Year && d.Month.Month == now.Month)
                    ? Color.FromHex("#000000")
                    : Color.FromHex("#dddddd"),
            }).ToList();

            var plt = new Plot();
            plt.Add.Bars(bars);

            ApplyMonthCategoryTicks(plt, months);
            plt.Axes.Margins(left: 0.08, right: 0.08, bottom: 0.30, top: 0.18);
            plt.Axes.AutoScale();
            PadXAxisForBars(plt, bars.Count, rightPad: 1.0);

            double maxBar = Math.Max(0, bars.Max(b => b.Value));
            double yOffset = Math.Max(maxBar * 0.025, 0.001);
            var lim = plt.Axes.GetLimits();
            double neededTop = Math.Max(lim.Top, maxBar + Math.Max(yOffset * 1.15, 0.002));
            if (lim.Bottom != 0 || lim.Top < neededTop)
            {
                plt.Axes.SetLimitsY(0, neededTop);
            }

            string ValueLabel(decimal v)
            {
                if (displayCurrency == Currency.USD)
                {
                    return v.ToString("C", CultureInfo.CreateSpecificCulture(Culture));
                }

                // Longer format for any non-USD currency (one extra decimal place)
                return v >= 1m ? v.ToString("0.000")
                     : v >= 0.1m ? v.ToString("0.0000")
                     : v >= 0.01m ? v.ToString("0.00000")
                     : v >= 0.001m ? v.ToString("0.000000")
                     : v.ToString("0.0000000").TrimEnd('0').TrimEnd('.');
            }

            for (int i = 0; i < bars.Count; i++)
            {
                double x = bars[i].Position;
                double y = bars[i].Value;
                var txt = plt.Add.Text(ValueLabel((decimal)y), x, y + yOffset);
                txt.Alignment = ScottPlot.Alignment.LowerCenter;
                txt.LabelFontSize = 12;
            }

            string unit = displayCurrency == Currency.USD ? "USD" : displayCurrency.ToString();
            plt.Title($"Average Daily Revenue ({unit}/day)");
            plt.XLabel("Month");
            plt.YLabel($"{unit} per day");

            plt.Title($"Average Daily Revenue ({unit}/day)");
            plt.XLabel("Month");
            plt.YLabel($"{unit} per day");

            AddSubtitleBelowTitle(plt, filterLabel);

            return plt.GetImageBytes(1200, 600, ImageFormat.Png);
        }

        public byte[] CreateMonthlyIncomeBarChart(
            IEnumerable<SponsoredListingInvoice> invoices,
            Currency displayCurrency,
            DateTime rangeStart,
            DateTime rangeEnd,
            string? filterLabel = null)
        {
            var list = (invoices ?? Enumerable.Empty<SponsoredListingInvoice>()).ToList();
            if (list.Count == 0)
            {
                return Array.Empty<byte>();
            }

            // Build inclusive month list for [rangeStart, rangeEnd]
            var firstMonth = new DateTime(rangeStart.Year, rangeStart.Month, 1);
            var lastMonth = new DateTime(rangeEnd.Year, rangeEnd.Month, 1);
            var months = new List<DateTime>();
            for (var m = firstMonth; m <= lastMonth; m = m.AddMonths(1))
            {
                months.Add(m);
            }

            // Accrue each invoice’s amount across overlap days per month
            var monthlyTotals = new List<decimal>(months.Count);
            foreach (var m in months)
            {
                var ms = m;
                var me = m.AddMonths(1).AddDays(-1);
                decimal total = 0m;

                foreach (var inv in list)
                {
                    var amt = inv.AmountIn(displayCurrency);
                    if (amt <= 0m)
                    {
                        continue;
                    }

                    var s = inv.CampaignStartDate.Date;
                    var e = inv.CampaignEndDate.Date;
                    if (e < s)
                    {
                        continue;
                    }

                    var spanDays = (decimal)((e - s).TotalDays + 1);
                    if (spanDays <= 0)
                    {
                        continue;
                    }

                    var os = s > ms ? s : ms;
                    var oe = e < me ? e : me;
                    if (oe < os)
                    {
                        continue;
                    }

                    var overlapDays = (decimal)((oe - os).TotalDays + 1);
                    total += (amt / spanDays) * overlapDays;
                }

                monthlyTotals.Add(total);
            }

            // Plot
            var bars = monthlyTotals.Select((v, i) => new Bar { Position = i, Value = (double)v }).ToList();
            if (bars.All(b => b.Value == 0))
            {
                return Array.Empty<byte>();
            }

            var plt = new Plot();
            plt.Add.Bars(bars);

            ApplyMonthCategoryTicks(plt, months);
            plt.Axes.Margins(left: 0.08, right: 0.08, bottom: 0.30, top: 0.18);
            plt.Axes.AutoScale();
            PadXAxisForBars(plt, bars.Count, rightPad: 1.0);

            double maxBar = Math.Max(0, bars.Max(b => b.Value));
            double yOffset = Math.Max(maxBar * 0.025, 0.001);
            var lim = plt.Axes.GetLimits();
            double neededTop = Math.Max(lim.Top, maxBar + (yOffset * 1.15));
            if (lim.Bottom != 0 || lim.Top < neededTop)
            {
                plt.Axes.SetLimitsY(0, neededTop);
            }

            for (int i = 0; i < bars.Count; i++)
            {
                double x = bars[i].Position;
                double y = bars[i].Value;
                string label = displayCurrency == Currency.USD
                    ? ((decimal)y).ToString("C0", CultureInfo.CreateSpecificCulture(Culture))
                    : $"{(decimal)y:0.######}";
                var txt = plt.Add.Text(label, x, y + yOffset);
                txt.Alignment = ScottPlot.Alignment.LowerCenter;
                txt.LabelFontSize = 12;
            }

            string unit = displayCurrency == Currency.USD ? "USD" : displayCurrency.ToString();
            plt.Title($"Monthly Income ({unit})");
            plt.YLabel($"Total ({unit})");

            plt.Title($"Monthly Income ({unit})");
            plt.YLabel($"Total ({unit})");

            AddSubtitleBelowTitle(plt, filterLabel);

            return plt.GetImageBytes(1200, 800, ImageFormat.Png);
        }

        public byte[] CreateSubcategoryRevenuePieChart(
            IEnumerable<SponsoredListingInvoice> invoices,
            IDictionary<int, string> categoryNames,
            IDictionary<int, string> subcategoryNames,
            IDictionary<int, int> subcategoryToCategory,
            Currency displayCurrency)
        {
            var list = (invoices ?? Enumerable.Empty<SponsoredListingInvoice>()).ToList();

            var breakdown = list
                .Where(i => i.SubCategoryId.HasValue)
                .GroupBy(i => i.SubCategoryId!.Value)
                .Select(g =>
                {
                    int subId = g.Key;
                    subcategoryToCategory.TryGetValue(subId, out var catId);
                    categoryNames.TryGetValue(catId, out var catLabel);
                    subcategoryNames.TryGetValue(subId, out var subLabel);

                    string label = CategoryFormatter.Format(catLabel ?? $"(Unknown Cat {catId})", subLabel ?? $"(Unknown Sub {subId})");
                    decimal total = g.Sum(i => i.AmountIn(displayCurrency));
                    return (subId, label, total);
                })
                .Where(x => x.total > 0m)
                .OrderByDescending(x => x.total)
                .ToArray();

            if (breakdown.Length == 0)
            {
                return Array.Empty<byte>();
            }

            double grand = (double)breakdown.Sum(x => x.total);

            // palette (unchanged)
            var hexPalette = new[]
            {
                // D3 Category20
                "#1f77b4", "#ff7f0e", "#2ca02c", "#d62728", "#9467bd",
                "#8c564b", "#e377c2", "#7f7f7f", "#bcbd22", "#17becf",
                "#393b79", "#637939", "#8c6d31", "#843c39", "#7b4173",
                "#3182bd", "#31a354", "#756bb1", "#636363", "#e6550d",

                // D3 Category20b
                "#393b79", "#5254a3", "#6b6ecf", "#9c9ede", "#637939",
                "#8ca252", "#b5cf6b", "#cedb9c", "#8c6d31", "#bd9e39",
                "#e7ba52", "#e7cb94", "#843c39", "#ad494a", "#d6616b",
                "#e7969c", "#7b4173", "#a55194", "#ce6dbd", "#de9ed6"
            };
            var palette = hexPalette.Select(Color.FromHex).ToArray();

            var slices = breakdown.Select((row, idx) =>
            {
                double val = (double)row.total;
                double pct = grand > 0 ? Math.Round(val * 100.0 / grand, 1) : 0;
                var slice = new PieSlice(val, palette[idx % palette.Length], $"{pct}%");
                slice.LegendText = $"{row.label} — {FormatValue((decimal)val, displayCurrency)}";
                return slice;
            }).ToArray();

            var plt = new Plot();
            plt.HideAxesAndGrid();

            var pie = plt.Add.Pie(slices);
            pie.DonutFraction = 0;
            pie.SliceLabelDistance = 1.2;
            pie.Rotation = Angle.FromDegrees(-90);

            plt.Title($"Revenue by Subcategory ({AxisUnitLabel(displayCurrency)})");
            plt.ShowLegend(Edge.Right);

            return plt.GetImageBytes(800, 600, ImageFormat.Png);
        }

        public byte[] CreateCountryRevenuePieChart(
            IEnumerable<SponsoredListingInvoice> invoices,
            Currency displayCurrency)
        {
            var list = (invoices ?? Enumerable.Empty<SponsoredListingInvoice>()).ToList();

            var breakdown = list
                .GroupBy(i =>
                {
                    var cc = i.DirectoryEntry?.CountryCode;
                    return string.IsNullOrWhiteSpace(cc) ? "Unknown" : cc.Trim().ToUpperInvariant();
                })
                .Select(g => (code: g.Key, total: g.Sum(i => i.AmountIn(displayCurrency))))
                .Where(x => x.total > 0m)
                .OrderByDescending(x => x.total)
                .ToArray();

            if (breakdown.Length == 0)
            {
                return Array.Empty<byte>();
            }

            double grand = (double)breakdown.Sum(x => x.total);

            var hexPalette = new[]
            {
                // D3 Category20
                "#1f77b4", "#ff7f0e", "#2ca02c", "#d62728", "#9467bd",
                "#8c564b", "#e377c2", "#7f7f7f", "#bcbd22", "#17becf",
                "#393b79", "#637939", "#8c6d31", "#843c39", "#7b4173",
                "#3182bd", "#31a354", "#756bb1", "#636363", "#e6550d",

                // D3 Category20b
                "#393b79", "#5254a3", "#6b6ecf", "#9c9ede", "#637939",
                "#8ca252", "#b5cf6b", "#cedb9c", "#8c6d31", "#bd9e39",
                "#e7ba52", "#e7cb94", "#843c39", "#ad494a", "#d6616b",
                "#e7969c", "#7b4173", "#a55194", "#ce6dbd", "#de9ed6"
            };
            var palette = hexPalette.Select(Color.FromHex).ToArray();

            var slices = breakdown.Select((row, idx) =>
            {
                double val = (double)row.total;
                double pct = grand > 0 ? Math.Round(val * 100.0 / grand, 1) : 0;
                var slice = new PieSlice(val, palette[idx % palette.Length], $"{pct}%");
                slice.LegendText = $"{CountryLabel(row.code)} — {FormatValue((decimal)val, displayCurrency)}";
                return slice;
            }).ToArray();

            var plt = new Plot();
            plt.HideAxesAndGrid();

            var pie = plt.Add.Pie(slices);
            pie.DonutFraction = 0;
            pie.SliceLabelDistance = 1.2;
            pie.Rotation = Angle.FromDegrees(-90);

            plt.Title($"Revenue by Country ({AxisUnitLabel(displayCurrency)})");
            plt.ShowLegend(Edge.Right);

            return plt.GetImageBytes(800, 600, ImageFormat.Png);
        }

        private static string CountryLabel(string code)
        {
            if (string.IsNullOrWhiteSpace(code) ||
                code.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
            {
                return "Unknown";
            }

            try
            {
                return $"{code} ({new System.Globalization.RegionInfo(code).EnglishName})";
            }
            catch (ArgumentException)
            {
                return code;
            }
        }

        public byte[] CreateIncomeForecastChart(
            IReadOnlyList<DateTime> historyMonths,
            IReadOnlyList<decimal> historyValues,
            IReadOnlyList<DateTime> forecastMonths,
            IReadOnlyList<decimal> forecastExpected,
            IReadOnlyList<decimal> forecastLow,
            IReadOnlyList<decimal> forecastHigh,
            Currency displayCurrency,
            decimal? runRateReference = null,
            decimal? recentAverageReference = null)
        {
            historyMonths ??= Array.Empty<DateTime>();
            historyValues ??= Array.Empty<decimal>();
            forecastMonths ??= Array.Empty<DateTime>();
            forecastExpected ??= Array.Empty<decimal>();
            forecastLow ??= Array.Empty<decimal>();
            forecastHigh ??= Array.Empty<decimal>();

            int h = historyMonths.Count;
            int f = forecastMonths.Count;

            if (h == 0 && f == 0)
            {
                return Array.Empty<byte>();
            }

            var combinedMonths = new List<DateTime>(h + f);
            combinedMonths.AddRange(historyMonths);
            combinedMonths.AddRange(forecastMonths);

            var plt = new Plot();

            // ---- history as grey bars ----
            if (h > 0)
            {
                var bars = new List<Bar>(h);
                for (int i = 0; i < h; i++)
                {
                    bars.Add(new Bar
                    {
                        Position = i,
                        Value = (double)historyValues[i],
                        FillColor = Color.FromHex("#bdbdbd"),
                    });
                }

                plt.Add.Bars(bars);
            }

            // ---- forecast lines (expected solid, low/high dotted) ----
            if (f > 0)
            {
                var xsExp = new List<double>();
                var ysExp = new List<double>();
                var xsLow = new List<double>();
                var ysLow = new List<double>();
                var xsHigh = new List<double>();
                var ysHigh = new List<double>();

                // Anchor the forecast at the last actual so the line visibly connects.
                if (h > 0)
                {
                    double anchorX = h - 1;
                    double anchorY = (double)historyValues[h - 1];
                    xsExp.Add(anchorX);
                    ysExp.Add(anchorY);
                    xsLow.Add(anchorX);
                    ysLow.Add(anchorY);
                    xsHigh.Add(anchorX);
                    ysHigh.Add(anchorY);
                }

                for (int j = 0; j < f; j++)
                {
                    double x = h + j;
                    xsExp.Add(x);
                    ysExp.Add((double)forecastExpected[j]);
                    xsLow.Add(x);
                    ysLow.Add((double)forecastLow[j]);
                    xsHigh.Add(x);
                    ysHigh.Add((double)forecastHigh[j]);
                }

                var high = plt.Add.Scatter(xsHigh.ToArray(), ysHigh.ToArray());
                high.Color = Color.FromHex("#9ecae1");
                high.LineWidth = 1;
                high.LinePattern = LinePattern.Dotted;
                high.MarkerSize = 0;
                high.LegendText = "High (optimistic)";

                var low = plt.Add.Scatter(xsLow.ToArray(), ysLow.ToArray());
                low.Color = Color.FromHex("#9ecae1");
                low.LineWidth = 1;
                low.LinePattern = LinePattern.Dotted;
                low.MarkerSize = 0;
                low.LegendText = "Low (conservative)";

                var exp = plt.Add.Scatter(xsExp.ToArray(), ysExp.ToArray());
                exp.Color = Color.FromHex("#1f77b4");
                exp.LineWidth = 2;
                exp.MarkerSize = 4;
                exp.LegendText = "Forecast (expected)";
            }

            // ---- divider between history and forecast ----
            if (h > 0 && f > 0)
            {
                var vline = plt.Add.VerticalLine(h - 0.5);
                vline.Color = Color.FromHex("#999999");
                vline.LinePattern = LinePattern.Dashed;
                vline.LineWidth = 1;
            }

            // ---- reference lines: recent average + forward run-rate ----
            if (recentAverageReference.HasValue && recentAverageReference.Value > 0m)
            {
                var avgLine = plt.Add.HorizontalLine((double)recentAverageReference.Value);
                avgLine.Color = Color.FromHex("#6b6b6b");
                avgLine.LinePattern = LinePattern.Dashed;
                avgLine.LineWidth = 1;
                avgLine.LegendText = "Recent 3-mo average";
            }

            if (runRateReference.HasValue && runRateReference.Value > 0m)
            {
                var rrLine = plt.Add.HorizontalLine((double)runRateReference.Value);
                rrLine.Color = Color.FromHex("#2ca02c");
                rrLine.LinePattern = LinePattern.Dotted;
                rrLine.LineWidth = 2;
                rrLine.LegendText = "Live run-rate";
            }

            ApplyMonthCategoryTicksThinned(plt, combinedMonths);
            plt.Axes.Margins(left: 0.05, right: 0.05, bottom: 0.30, top: 0.18);
            plt.Axes.AutoScale();

            var lim = plt.Axes.GetLimits();
            if (lim.Bottom != 0)
            {
                plt.Axes.SetLimitsY(0, lim.Top);
            }

            string unit = AxisUnitLabel(displayCurrency);
            plt.Title($"Income Forecast ({unit})");
            plt.YLabel($"Monthly income ({unit})");
            plt.XLabel("Month");
            plt.ShowLegend(Edge.Right);

            return plt.GetImageBytes(1400, 700, ImageFormat.Png);
        }

        private static void ApplyMonthCategoryTicksThinned(ScottPlot.Plot plt, IReadOnlyList<DateTime> months)
        {
            int count = months.Count;
            int stride = count <= 18 ? 1 : (count <= 30 ? 2 : 3);

            var tickList = new List<double>();
            var labelList = new List<string>();
            for (int i = 0; i < count; i++)
            {
                if (i % stride != 0 && i != count - 1)
                {
                    continue;
                }

                tickList.Add(i);
                labelList.Add($"{months[i]:MMM}\n{months[i]:yyyy}");
            }

            plt.Axes.Bottom.TickGenerator =
                new ScottPlot.TickGenerators.NumericManual(tickList.ToArray(), labelList.ToArray());
            plt.Axes.Bottom.TickLabelStyle.Rotation = 0;
        }

        private static string FormatValue(decimal v, Currency currency)
        {
            if (currency == Currency.USD)
            {
                return v.ToString("C0", CultureInfo.CreateSpecificCulture(Culture));
            }

            return $"{v:0.######} {currency}";
        }

        private static string AxisUnitLabel(Currency currency) =>
            currency == Currency.USD ? "USD" : currency.ToString();

        private static void ApplyMonthCategoryTicks(ScottPlot.Plot plt, IReadOnlyList<DateTime> months)
        {
            double[] ticks = Enumerable.Range(0, months.Count).Select(i => (double)i).ToArray();
            string[] labels = months.Select(m => $"{m:MMM}\n{m:yyyy}").ToArray(); // two-line labels

            plt.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual(ticks, labels);

            // Keep axis text horizontal (multi-line), which reduces height dramatically vs 90° rotation
            plt.Axes.Bottom.TickLabelStyle.Rotation = 0;
        }

        private static void PadXAxisForBars(ScottPlot.Plot plt, int barCount, double rightPad = 1.0)
        {
            double left = -0.5;
            double right = (barCount - 0.5) + rightPad; // ~one extra bar of breathing room
            plt.Axes.SetLimitsX(left, right);
        }

        private static void AddSubtitleBelowTitle(ScottPlot.Plot plt, string? subtitle)
        {
            if (string.IsNullOrWhiteSpace(subtitle))
            {
                return;
            }

            // create a little vertical headroom above current top
            var lim = plt.Axes.GetLimits();
            double spanY = lim.Top - lim.Bottom;
            double extra = Math.Max(spanY * 0.12, 1.0); // 12% of span or at least 1 unit

            plt.Axes.SetLimitsY(lim.Bottom, lim.Top + extra);

            // recompute after expanding
            lim = plt.Axes.GetLimits();
            double xMid = (lim.Left + lim.Right) / 2.0;
            double y = lim.Top - (extra * 0.35); // place subtitle inside the new padding

            var t = plt.Add.Text(subtitle, xMid, y);
            t.Alignment = ScottPlot.Alignment.UpperCenter;
            t.LabelFontSize = 14;
        }
    }
}