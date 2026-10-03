using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Windows.Forms;

namespace SerialScope
{
    // Live line chart for numeric serial output, using the Arduino Serial Plotter format:
    //   "23.5"                  one series
    //   "23.5 41 1013"          several series (space, comma or tab separated)
    //   "temp:23.5,hum:41"      named series
    // Lines containing anything that is not a number are ignored.
    //
    // Mouse: wheel zooms time, Ctrl/Shift+wheel zooms values, drag pans, right-drag zooms to a box,
    // double-click fits everything again. Hovering shows the values under the cursor.
    internal sealed class PlotView : Control, IThemed
    {
        private const int MaxSeries = 12;
        private const int History = 50000;        // samples kept per series for scrolling back
        private const int MinViewWidth = 5;

        private static readonly Color[] DarkPalette =
        {
            Color.FromArgb(34, 197, 94), Color.FromArgb(96, 165, 250), Color.FromArgb(245, 158, 11),
            Color.FromArgb(244, 114, 182), Color.FromArgb(167, 139, 250), Color.FromArgb(34, 211, 238),
            Color.FromArgb(248, 113, 113), Color.FromArgb(163, 230, 53)
        };

        private static readonly Color[] LightPalette =
        {
            Color.FromArgb(22, 163, 74), Color.FromArgb(37, 99, 235), Color.FromArgb(217, 119, 6),
            Color.FromArgb(219, 39, 119), Color.FromArgb(124, 58, 237), Color.FromArgb(8, 145, 178),
            Color.FromArgb(220, 38, 38), Color.FromArgb(101, 163, 13)
        };

        private sealed class Series
        {
            public string Name;
            public int ColorIndex;
            public double[] Values;
            public double Last = double.NaN;
            public bool Visible = true;
        }

        private enum OverlayAction { ZoomOut, ZoomIn, Fit, Live, TimeAxis, Range, Stats }

        // Raised when the user clicks "Y range..."; the host shows a dialog and calls SetFixedRange
        public event EventHandler RangeRequested;

        private readonly List<Series> series = new List<Series>();
        private readonly List<KeyValuePair<Rectangle, Series>> legendHits = new List<KeyValuePair<Rectangle, Series>>();
        private readonly List<KeyValuePair<Rectangle, OverlayAction>> overlayHits = new List<KeyValuePair<Rectangle, OverlayAction>>();
        private readonly Font labelFont = new Font("Segoe UI", 8.25F);
        private readonly Font legendFont = new Font("Segoe UI", 9F);
        private readonly Font valueFont = new Font("Consolas", 9F);

        private Theme theme = Theme.Dark;
        private int head;            // next write position in the ring buffers
        private int count;           // samples currently stored (up to History)
        private long totalSamples;   // samples received since the last clear
        private bool dirty;

        // View state
        private int defaultWidth = 500;    // "Show last N points"
        private double viewWidth = 500;    // samples across the plot
        private bool followLatest = true;  // right edge tracks the newest sample
        private double viewEnd;            // right edge when not following
        private bool autoY = true;
        private double yMin, yMax;         // used when autoY is off
        private bool fixedRange;           // user-set value range that Fit keeps
        private double fixedMin, fixedMax;
        private bool timeAxis;             // label the bottom axis with clock time instead of reading numbers
        private bool showStats;            // min / max / average of the visible part in the legend
        private readonly long[] times = new long[History];   // arrival time of each stored reading
        private bool exporting;            // drawing for "Save image": no buttons or hover box

        // Values from the last paint, used to convert mouse positions
        private Rectangle area;
        private double drawX0, drawX1, drawMin, drawMax;

        // Mouse state
        private Point mouse;
        private bool mouseInside;
        private Point dragStart;
        private bool panning, boxZooming;
        private double panStartEnd, panStartMin, panStartMax;

        public PlotView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
        }

        // Number of readings shown when the view is fitted ("Show last N points")
        public int ViewWidth
        {
            get { return defaultWidth; }
            set
            {
                defaultWidth = Math.Max(MinViewWidth, Math.Min(History, value));
                Fit();
            }
        }

        public void ApplyTheme(Theme t)
        {
            theme = t;
            BackColor = t.OutputBack;
            Invalidate();
        }

        public void Clear()
        {
            series.Clear();
            head = count = 0;
            totalSamples = 0;
            Fit();
        }

        // Back to auto-scaled values (or the fixed range) and the latest readings
        public void Fit()
        {
            viewWidth = defaultWidth;
            followLatest = true;
            ResetValueAxis();
            Invalidate();
        }

        private void ResetValueAxis()
        {
            if (fixedRange) { autoY = false; yMin = fixedMin; yMax = fixedMax; }
            else autoY = true;
        }

        public bool HasFixedRange { get { return fixedRange; } }
        public double FixedMin { get { return fixedMin; } }
        public double FixedMax { get { return fixedMax; } }

        public void SetFixedRange(double min, double max)
        {
            fixedRange = true;
            fixedMin = Math.Min(min, max);
            fixedMax = Math.Max(min, max);
            if (fixedMax - fixedMin < 1e-12) fixedMax = fixedMin + 1;
            ResetValueAxis();
            Invalidate();
        }

        public void ClearFixedRange()
        {
            fixedRange = false;
            ResetValueAxis();
            Invalidate();
        }

        public bool TimeAxis
        {
            get { return timeAxis; }
            set { timeAxis = value; Invalidate(); }
        }

        // Saves the current view as a PNG, without the on-graph buttons or hover box
        public void SaveImage(string path)
        {
            using (var bmp = new Bitmap(Math.Max(1, Width), Math.Max(1, Height)))
            {
                exporting = true;
                try { DrawToBitmap(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height)); }
                finally { exporting = false; }
                bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            }
            Invalidate();
        }

        // ------------------------------------------------------------------ data

        // Adds one line of serial output; returns false if it is not plottable
        public bool AddLine(string line)
        {
            return AddLine(line, new List<string>(), new List<double>());
        }

        // Same, also returning the parsed names and values (used for recording)
        public bool AddLine(string line, List<string> names, List<double> values)
        {
            names.Clear();
            values.Clear();
            if (!Parse(line, names, values)) return false;

            for (int i = 0; i < values.Count; i++)
            {
                Series s = Find(names[i]);
                if (s == null)
                {
                    if (series.Count >= MaxSeries) continue;
                    s = new Series { Name = names[i], ColorIndex = series.Count, Values = new double[History] };
                    for (int j = 0; j < History; j++) s.Values[j] = double.NaN;
                    series.Add(s);
                }
                s.Values[head] = values[i];
                s.Last = values[i];
            }

            // Series missing from this line get a gap
            foreach (Series s in series)
            {
                if (!names.Contains(s.Name)) s.Values[head] = double.NaN;
            }

            times[head] = DateTime.Now.Ticks;
            head = (head + 1) % History;
            if (count < History) count++;
            totalSamples++;
            dirty = true;
            return true;
        }

        // Repaints only when new data arrived since the last call
        public void RefreshIfChanged()
        {
            if (!dirty) return;
            dirty = false;
            Invalidate();
        }

        private static bool Parse(string line, List<string> names, List<double> values)
        {
            line = line.Trim();
            if (line.Length == 0) return false;

            int unnamed = 0;
            foreach (string field in line.Split(new[] { ',', '\t', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string f = field.Trim();
                if (f.Length == 0) continue;

                int sep = f.IndexOfAny(new[] { ':', '=' });
                if (sep > 0)
                {
                    double v;
                    if (!TryNumber(f.Substring(sep + 1), out v)) return false;
                    names.Add(f.Substring(0, sep).Trim());
                    values.Add(v);
                }
                else
                {
                    foreach (string part in f.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        double v;
                        if (!TryNumber(part, out v)) return false;
                        unnamed++;
                        names.Add("Value " + unnamed);
                        values.Add(v);
                    }
                }
            }
            return values.Count > 0;
        }

        private static bool TryNumber(string text, out double value)
        {
            return double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                   && !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private Series Find(string name)
        {
            foreach (Series s in series) if (s.Name == name) return s;
            return null;
        }

        private long FirstStored
        {
            get { return totalSamples - count; }
        }

        // Value of a series at an absolute sample number, NaN if not stored
        private double ValueAt(Series s, long sample)
        {
            if (sample < FirstStored || sample >= totalSamples) return double.NaN;
            long back = totalSamples - sample;   // 1 = newest
            return s.Values[(int)(((head - back) % History + History) % History)];
        }

        // Arrival time of a stored reading, or DateTime.MinValue if it is no longer stored
        private DateTime TimeAt(long sample)
        {
            if (sample < FirstStored || sample >= totalSamples) return DateTime.MinValue;
            long back = totalSamples - sample;
            return new DateTime(times[(int)(((head - back) % History + History) % History)]);
        }

        private void GetXRange(out double x0, out double x1)
        {
            if (followLatest)
            {
                // Fill from the left until there is enough data, then scroll
                x1 = Math.Max(totalSamples - 1, viewWidth - 1);
            }
            else
            {
                x1 = viewEnd;
            }
            x0 = x1 - (viewWidth - 1);
        }

        // ------------------------------------------------------------------ view changes

        private void ZoomTime(double factor, double anchor)
        {
            double x0, x1;
            GetXRange(out x0, out x1);
            double width = Math.Max(MinViewWidth, Math.Min(History, viewWidth * factor));
            if (!followLatest)
            {
                double newX0 = anchor - (anchor - x0) * width / viewWidth;
                viewEnd = newX0 + width - 1;
            }
            viewWidth = width;
            ClampPan();
            Invalidate();
        }

        private void ZoomValues(double factor, double anchor)
        {
            if (autoY) { yMin = drawMin; yMax = drawMax; autoY = false; }
            yMin = anchor - (anchor - yMin) * factor;
            yMax = anchor + (yMax - anchor) * factor;
            Invalidate();
        }

        // Keeps at least part of the stored data on screen; reaching the newest sample resumes following
        private void ClampPan()
        {
            if (followLatest) return;
            double latest = Math.Max(totalSamples - 1, viewWidth - 1);
            if (viewEnd >= latest)
            {
                viewEnd = latest;
                if (!panning) followLatest = true;   // while dragging, decide on release
                return;
            }
            double earliest = FirstStored + Math.Min(viewWidth - 1, count) * 0.1;
            if (viewEnd < earliest) viewEnd = earliest;
        }

        private void StopFollowing()
        {
            if (!followLatest) return;
            double x0, x1;
            GetXRange(out x0, out x1);
            viewEnd = x1;
            followLatest = false;
        }

        private double SampleAtX(int x)
        {
            return drawX0 + (double)(x - area.Left) / Math.Max(1, area.Width) * (drawX1 - drawX0);
        }

        private double ValueAtY(int y)
        {
            return drawMin + (double)(area.Bottom - y) / Math.Max(1, area.Height) * (drawMax - drawMin);
        }

        // ------------------------------------------------------------------ mouse

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (series.Count == 0 || area.Width <= 0) return;
            double factor = e.Delta > 0 ? 0.8 : 1.25;
            if ((ModifierKeys & (Keys.Control | Keys.Shift)) != 0)
                ZoomValues(factor, ValueAtY(Math.Max(area.Top, Math.Min(area.Bottom, e.Y))));
            else
                ZoomTime(factor, SampleAtX(Math.Max(area.Left, Math.Min(area.Right, e.X))));
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            if (series.Count == 0) return;

            foreach (var hit in overlayHits)
            {
                if (!hit.Key.Contains(e.Location)) continue;
                if (e.Button == MouseButtons.Left) RunOverlay(hit.Value);
                return;
            }
            foreach (var hit in legendHits)
            {
                if (!hit.Key.Contains(e.Location)) continue;
                if (e.Button == MouseButtons.Left) { hit.Value.Visible = !hit.Value.Visible; Invalidate(); }
                return;
            }

            if (!area.Contains(e.Location)) return;
            dragStart = e.Location;
            if (e.Button == MouseButtons.Left)
            {
                panning = true;
                StopFollowing();
                panStartEnd = viewEnd;
                panStartMin = autoY ? drawMin : yMin;
                panStartMax = autoY ? drawMax : yMax;
                Cursor = Cursors.SizeAll;
            }
            else if (e.Button == MouseButtons.Right)
            {
                boxZooming = true;
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            mouse = e.Location;
            mouseInside = true;

            if (panning)
            {
                double samplesPerPixel = (drawX1 - drawX0) / Math.Max(1, area.Width);
                viewEnd = panStartEnd - (e.X - dragStart.X) * samplesPerPixel;
                if (!autoY)
                {
                    double valuesPerPixel = (panStartMax - panStartMin) / Math.Max(1, area.Height);
                    double shift = (e.Y - dragStart.Y) * valuesPerPixel;
                    yMin = panStartMin + shift;
                    yMax = panStartMax + shift;
                }
                ClampPan();
                Invalidate();
                return;
            }

            if (!boxZooming)
            {
                bool hand = false;
                foreach (var hit in overlayHits) if (hit.Key.Contains(e.Location)) hand = true;
                foreach (var hit in legendHits) if (hit.Key.Contains(e.Location)) hand = true;
                Cursor = hand ? Cursors.Hand : area.Contains(e.Location) ? Cursors.Cross : Cursors.Default;
            }
            Invalidate();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (boxZooming)
            {
                boxZooming = false;
                Rectangle box = SelectionBox();
                if (box.Width > 8 && box.Height > 8)
                {
                    double x0 = SampleAtX(box.Left), x1 = SampleAtX(box.Right);
                    double top = ValueAtY(box.Top), bottom = ValueAtY(box.Bottom);
                    StopFollowing();
                    viewWidth = Math.Max(MinViewWidth, x1 - x0 + 1);
                    viewEnd = x1;
                    yMin = bottom;
                    yMax = top;
                    autoY = false;
                    ClampPan();
                }
            }
            if (panning)
            {
                panning = false;
                ClampPan();   // dropped at the newest reading: follow live data again
            }
            Cursor = area.Contains(e.Location) ? Cursors.Cross : Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (area.Contains(e.Location)) Fit();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            mouseInside = false;
            Invalidate();
        }

        private void RunOverlay(OverlayAction action)
        {
            double centre = (drawX0 + drawX1) / 2;
            switch (action)
            {
                case OverlayAction.ZoomIn: ZoomTime(0.5, followLatest ? drawX1 : centre); break;
                case OverlayAction.ZoomOut: ZoomTime(2.0, followLatest ? drawX1 : centre); break;
                case OverlayAction.Fit: Fit(); break;
                case OverlayAction.Live: followLatest = true; Invalidate(); break;
                case OverlayAction.TimeAxis: TimeAxis = !timeAxis; break;
                case OverlayAction.Stats: ShowStats = !showStats; break;
                case OverlayAction.Range: if (RangeRequested != null) RangeRequested(this, EventArgs.Empty); break;
            }
        }

        private Rectangle SelectionBox()
        {
            int x = Math.Max(area.Left, Math.Min(dragStart.X, mouse.X));
            int y = Math.Max(area.Top, Math.Min(dragStart.Y, mouse.Y));
            int r = Math.Min(area.Right, Math.Max(dragStart.X, mouse.X));
            int b = Math.Min(area.Bottom, Math.Max(dragStart.Y, mouse.Y));
            return new Rectangle(x, y, Math.Max(0, r - x), Math.Max(0, b - y));
        }

        // ------------------------------------------------------------------ drawing

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(theme.OutputBack);
            legendHits.Clear();
            overlayHits.Clear();

            if (series.Count == 0)
            {
                area = Rectangle.Empty;
                DrawEmptyState(g);
                return;
            }

            Color[] palette = theme.IsDark ? DarkPalette : LightPalette;
            int legendBottom = DrawLegend(g, palette);

            double x0, x1;
            GetXRange(out x0, out x1);
            long s0 = Math.Max(FirstStored, (long)Math.Floor(x0) - 1);
            long s1 = Math.Min(totalSamples - 1, (long)Math.Ceiling(x1) + 1);

            // Value range: automatic from the visible samples, or the zoomed range
            double min, max;
            if (autoY)
            {
                min = double.MaxValue; max = double.MinValue;
                foreach (Series s in series)
                {
                    if (!s.Visible) continue;
                    for (long k = s0; k <= s1; k++)
                    {
                        double v = ValueAt(s, k);
                        if (double.IsNaN(v)) continue;
                        if (v < min) min = v;
                        if (v > max) max = v;
                    }
                }
                if (min > max) { min = 0; max = 1; }
                if (max - min < 1e-9) { min -= 1; max += 1; }
                double pad = (max - min) * 0.08;
                min -= pad;
                max += pad;
            }
            else
            {
                min = yMin; max = yMax;
                if (max - min < 1e-12) max = min + 1e-12;
            }

            double step = NiceStep((max - min) / 5);
            double firstTick = Math.Ceiling(min / step) * step;

            int labelWidth = 0;
            for (double t = firstTick; t <= max; t += step)
                labelWidth = Math.Max(labelWidth, TextRenderer.MeasureText(FormatTick(t, step), labelFont).Width);
            area = new Rectangle(labelWidth + 14, legendBottom + 10, Width - labelWidth - 30, Height - legendBottom - 54);
            if (area.Width < 40 || area.Height < 40) return;
            drawX0 = x0; drawX1 = x1; drawMin = min; drawMax = max;

            using (var gridPen = new Pen(Color.FromArgb(theme.IsDark ? 40 : 60, theme.Muted)))
            {
                for (double t = firstTick; t <= max; t += step)
                {
                    int y = (int)Math.Round(MapY(t));
                    g.DrawLine(gridPen, area.Left, y, area.Right, y);
                    string label = FormatTick(t, step);
                    Size size = TextRenderer.MeasureText(label, labelFont);
                    TextRenderer.DrawText(g, label, labelFont, new Point(area.Left - size.Width - 6, y - size.Height / 2), theme.Muted);
                }

                // Bottom axis: reading numbers (or their arrival times) at round intervals
                double xStep = Math.Max(1, NiceStep((x1 - x0) / 6));
                double spanSeconds = (TimeAt(s1) - TimeAt(s0)).TotalSeconds;
                for (double t = Math.Ceiling(x0 / xStep) * xStep; t <= x1; t += xStep)
                {
                    if (t < 0) continue;
                    float x = MapX(t);
                    g.DrawLine(gridPen, x, area.Top, x, area.Bottom);
                    string label;
                    if (timeAxis)
                    {
                        DateTime at = TimeAt((long)t);
                        if (at == DateTime.MinValue) continue;
                        label = at.ToString(spanSeconds < 20 ? "HH:mm:ss.f" : "HH:mm:ss");
                    }
                    else
                    {
                        label = ((long)t).ToString();
                    }
                    Size size = TextRenderer.MeasureText(label, labelFont);
                    int lx = Math.Max(area.Left - 6, Math.Min(area.Right - size.Width + 4, (int)x - size.Width / 2));   // keep labels on screen
                    TextRenderer.DrawText(g, label, labelFont, new Point(lx, area.Bottom + 5), theme.Muted);
                }
            }

            using (var axisPen = new Pen(theme.Border))
                g.DrawRectangle(axisPen, area);

            DrawSeries(g, palette, s0, s1);
            if (exporting) return;
            DrawOverlayButtons(g);

            if (boxZooming)
            {
                Rectangle box = SelectionBox();
                using (var fill = new SolidBrush(Color.FromArgb(40, theme.Text)))
                using (var pen = new Pen(theme.Muted) { DashStyle = DashStyle.Dash })
                {
                    g.FillRectangle(fill, box);
                    g.DrawRectangle(pen, box);
                }
            }
            else if (mouseInside && !panning && area.Contains(mouse))
            {
                DrawCrosshair(g, palette);
            }

            string hint = "Scroll: zoom time   ·   Ctrl+scroll: zoom values   ·   Drag: pan   ·   Right-drag: zoom to area   ·   Double-click: fit";
            TextRenderer.DrawText(g, hint, labelFont, new Point(area.Left, Height - 18), theme.Border);
        }

        private void DrawSeries(Graphics g, Color[] palette, long s0, long s1)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.SetClip(area);
            var run = new List<PointF>();
            foreach (Series s in series)
            {
                if (!s.Visible) continue;
                using (var pen = new Pen(palette[s.ColorIndex % palette.Length], 1.8f))
                using (var dot = new SolidBrush(pen.Color))
                {
                    pen.LineJoin = LineJoin.Round;
                    run.Clear();
                    for (long k = s0; k <= s1 + 1; k++)
                    {
                        double v = k <= s1 ? ValueAt(s, k) : double.NaN;
                        if (!double.IsNaN(v))
                        {
                            run.Add(new PointF(MapX(k), MapY(v)));
                            continue;
                        }
                        if (run.Count == 1) g.FillEllipse(dot, run[0].X - 2, run[0].Y - 2, 4, 4);
                        else if (run.Count > 1) g.DrawLines(pen, run.ToArray());
                        run.Clear();
                    }
                }
            }
            g.ResetClip();
            g.SmoothingMode = SmoothingMode.None;
        }

        // Small buttons in the top-right corner of the plot
        private void DrawOverlayButtons(Graphics g)
        {
            var items = new List<KeyValuePair<string, OverlayAction>>();
            if (!followLatest) items.Add(new KeyValuePair<string, OverlayAction>("Live ▸", OverlayAction.Live));
            items.Add(new KeyValuePair<string, OverlayAction>("Stats", OverlayAction.Stats));
            items.Add(new KeyValuePair<string, OverlayAction>("Time axis", OverlayAction.TimeAxis));
            items.Add(new KeyValuePair<string, OverlayAction>(fixedRange ? "Y range ✓" : "Y range…", OverlayAction.Range));
            items.Add(new KeyValuePair<string, OverlayAction>("−", OverlayAction.ZoomOut));
            items.Add(new KeyValuePair<string, OverlayAction>("+", OverlayAction.ZoomIn));
            items.Add(new KeyValuePair<string, OverlayAction>("Fit", OverlayAction.Fit));

            int x = area.Right - 8, y = area.Top + 8, h = 24;
            for (int i = items.Count - 1; i >= 0; i--)
            {
                int w = Math.Max(28, TextRenderer.MeasureText(items[i].Key, legendFont).Width + 14);
                var r = new Rectangle(x - w, y, w, h);
                bool hover = mouseInside && r.Contains(mouse);
                OverlayAction action = items[i].Value;
                bool live = action == OverlayAction.Live
                            || (action == OverlayAction.TimeAxis && timeAxis)
                            || (action == OverlayAction.Range && fixedRange)
                            || (action == OverlayAction.Stats && showStats);   // highlighted when active
                using (var b = new SolidBrush(live ? theme.Success : hover ? theme.SurfaceHover : theme.Surface))
                    g.FillRectangle(b, r);
                using (var pen = new Pen(live ? theme.Success : theme.Border))
                    g.DrawRectangle(pen, r.X, r.Y, r.Width - 1, r.Height - 1);
                TextRenderer.DrawText(g, items[i].Key, legendFont, r, live ? theme.OnAccent : theme.Text,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                overlayHits.Add(new KeyValuePair<Rectangle, OverlayAction>(r, items[i].Value));
                x -= w + 4;
            }
        }

        // Vertical line at the hovered reading with a box listing each series' value
        private void DrawCrosshair(Graphics g, Color[] palette)
        {
            long sample = (long)Math.Round(SampleAtX(mouse.X));
            if (sample < FirstStored || sample >= totalSamples) return;
            foreach (var hit in overlayHits) if (hit.Key.Contains(mouse)) return;

            float x = MapX(sample);
            using (var pen = new Pen(Color.FromArgb(120, theme.Muted)) { DashStyle = DashStyle.Dot })
                g.DrawLine(pen, x, area.Top, x, area.Bottom);

            var lines = new List<KeyValuePair<Series, string>>();
            foreach (Series s in series)
            {
                if (!s.Visible) continue;
                double v = ValueAt(s, sample);
                lines.Add(new KeyValuePair<Series, string>(s, s.Name + "   " + (double.IsNaN(v) ? "–" : FormatValue(v))));
                if (!double.IsNaN(v))
                {
                    using (var b = new SolidBrush(palette[s.ColorIndex % palette.Length]))
                    {
                        g.SmoothingMode = SmoothingMode.AntiAlias;
                        g.FillEllipse(b, x - 3.5f, MapY(v) - 3.5f, 7, 7);
                        g.SmoothingMode = SmoothingMode.None;
                    }
                }
            }

            DateTime arrived = TimeAt(sample);
            string title = "Reading #" + sample + (arrived != DateTime.MinValue ? "   " + arrived.ToString("HH:mm:ss.fff") : "");
            int width = TextRenderer.MeasureText(title, labelFont).Width;
            foreach (var l in lines) width = Math.Max(width, TextRenderer.MeasureText(l.Value, valueFont).Width + 16);
            int height = 22 + lines.Count * 17;
            int bx = (int)x + 14, by = Math.Max(area.Top + 4, mouse.Y - height / 2);
            if (bx + width + 16 > area.Right) bx = (int)x - width - 30;
            if (by + height > area.Bottom - 4) by = area.Bottom - 4 - height;
            var box = new Rectangle(bx, by, width + 16, height);

            using (var b = new SolidBrush(theme.Surface)) g.FillRectangle(b, box);
            using (var pen = new Pen(theme.Border)) g.DrawRectangle(pen, box);
            TextRenderer.DrawText(g, title, labelFont, new Point(box.X + 8, box.Y + 4), theme.Muted);
            int ly = box.Y + 21;
            foreach (var l in lines)
            {
                using (var b = new SolidBrush(palette[l.Key.ColorIndex % palette.Length]))
                    g.FillRectangle(b, box.X + 8, ly + 4, 8, 8);
                TextRenderer.DrawText(g, l.Value, valueFont, new Point(box.X + 20, ly), theme.Text);
                ly += 17;
            }
        }

        // Min / max / average of one series over a range of samples; false if it has no values there
        public bool TryGetStats(string name, out double min, out double max, out double average)
        {
            double x0, x1;
            GetXRange(out x0, out x1);
            long s0 = Math.Max(FirstStored, (long)Math.Ceiling(x0));
            long s1 = Math.Min(totalSamples - 1, (long)Math.Floor(x1));
            return Stats(Find(name), s0, s1, out min, out max, out average);
        }

        private bool Stats(Series s, long s0, long s1, out double min, out double max, out double average)
        {
            min = double.MaxValue; max = double.MinValue; average = 0;
            if (s == null) return false;
            double sum = 0;
            long n = 0;
            for (long k = s0; k <= s1; k++)
            {
                double v = ValueAt(s, k);
                if (double.IsNaN(v)) continue;
                if (v < min) min = v;
                if (v > max) max = v;
                sum += v;
                n++;
            }
            if (n == 0) return false;
            average = sum / n;
            return true;
        }

        public bool ShowStats
        {
            get { return showStats; }
            set { showStats = value; Invalidate(); }
        }

        // Legend across the top: colour swatch, name and latest value (plus min/max/avg of what's on screen
        // when Stats is on); click a name to show/hide that line
        private int DrawLegend(Graphics g, Color[] palette)
        {
            int x = 14, y = 10, rowHeight = 22;
            double vx0, vx1;
            GetXRange(out vx0, out vx1);
            long v0 = Math.Max(FirstStored, (long)Math.Ceiling(vx0));
            long v1 = Math.Min(totalSamples - 1, (long)Math.Floor(vx1));
            foreach (Series s in series)
            {
                string value = double.IsNaN(s.Last) ? "" : FormatValue(s.Last);
                double mn, mx, avg;
                if (showStats && Stats(s, v0, v1, out mn, out mx, out avg))
                    value += "   min " + FormatValue(mn) + "  max " + FormatValue(mx) + "  avg " + FormatValue(avg);
                int nameWidth = TextRenderer.MeasureText(s.Name, legendFont).Width;
                int valueWidth = TextRenderer.MeasureText(value, valueFont).Width;
                int itemWidth = 16 + nameWidth + 4 + valueWidth + 18;

                if (x + itemWidth > Width - 10 && x > 14) { x = 14; y += rowHeight; }

                Color c = palette[s.ColorIndex % palette.Length];
                using (var b = new SolidBrush(s.Visible ? c : theme.Border))
                    g.FillRectangle(b, x, y + 5, 10, 10);
                TextRenderer.DrawText(g, s.Name, legendFont, new Point(x + 16, y + 2), s.Visible ? theme.Text : theme.Border);
                TextRenderer.DrawText(g, value, valueFont, new Point(x + 16 + nameWidth + 4, y + 3), s.Visible ? theme.Muted : theme.Border);

                legendHits.Add(new KeyValuePair<Rectangle, Series>(new Rectangle(x - 4, y, itemWidth - 10, rowHeight), s));
                x += itemWidth;
            }
            return y + rowHeight;
        }

        private void DrawEmptyState(Graphics g)
        {
            var title = "Waiting for numbers…";
            var hint = "Print one reading per line, for example\n23.5     or     23.5 41 1013     or     temp:23.5,hum:41";
            var bounds = ClientRectangle;
            Size titleSize = TextRenderer.MeasureText(title, legendFont);
            int top = bounds.Height / 2 - 30;
            TextRenderer.DrawText(g, title, legendFont, new Rectangle(0, top, bounds.Width, titleSize.Height), theme.Text,
                TextFormatFlags.HorizontalCenter);
            TextRenderer.DrawText(g, hint, labelFont, new Rectangle(0, top + titleSize.Height + 8, bounds.Width, 40), theme.Muted,
                TextFormatFlags.HorizontalCenter);
        }

        private float MapX(double sample)
        {
            return (float)(area.Left + (sample - drawX0) / Math.Max(1e-9, drawX1 - drawX0) * area.Width);
        }

        private float MapY(double v)
        {
            return (float)(area.Bottom - (v - drawMin) / (drawMax - drawMin) * area.Height);
        }

        private static double NiceStep(double rough)
        {
            if (rough <= 0) return 1;
            double magnitude = Math.Pow(10, Math.Floor(Math.Log10(rough)));
            double n = rough / magnitude;
            double nice = n < 1.5 ? 1 : n < 3 ? 2 : n < 7 ? 5 : 10;
            return nice * magnitude;
        }

        private static string FormatTick(double v, double step)
        {
            if (Math.Abs(v) < step * 1e-6) v = 0;
            int decimals = Math.Max(0, (int)-Math.Floor(Math.Log10(step)));
            return v.ToString("F" + Math.Min(decimals, 6), CultureInfo.InvariantCulture);
        }

        private static string FormatValue(double v)
        {
            return v.ToString(Math.Abs(v) >= 10000 ? "0.#" : "0.###", CultureInfo.InvariantCulture);
        }
    }
}
