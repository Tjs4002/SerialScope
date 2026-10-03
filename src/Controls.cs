using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SerialScope
{
    internal enum ButtonKind { Normal, Success, Danger, Icon }

    internal sealed class FlatButton : Button, IThemed
    {
        public static readonly Font IconFont = new Font("Segoe MDL2 Assets", 10F);

        private ButtonKind kind;
        private Theme theme = Theme.Dark;

        public FlatButton(string text, ButtonKind kind)
        {
            Text = text;
            this.kind = kind;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 1;
            Cursor = Cursors.Hand;
            UseVisualStyleBackColor = false;
            Height = 30;
            Margin = new Padding(0, 0, 6, 0);
            if (kind == ButtonKind.Icon)
            {
                Font = IconFont;
                Width = 30;
                Padding = Padding.Empty;
            }
            else
            {
                AutoSize = true;
                AutoSizeMode = AutoSizeMode.GrowAndShrink;
                Padding = new Padding(10, 0, 10, 0);
                MinimumSize = new Size(0, 30);
            }
        }

        public ButtonKind Kind
        {
            get { return kind; }
            set { kind = value; ApplyTheme(theme); }
        }

        public void ApplyTheme(Theme t)
        {
            theme = t;
            Color back, border, hover, fore;
            switch (kind)
            {
                case ButtonKind.Success:
                    back = border = t.Success; hover = ControlPaint.Light(t.Success, 0.2f); fore = t.OnAccent; break;
                case ButtonKind.Danger:
                    back = border = t.Danger; hover = ControlPaint.Light(t.Danger, 0.2f); fore = t.OnAccent; break;
                default:
                    back = t.Surface; border = t.Border; hover = t.SurfaceHover; fore = t.Text; break;
            }
            BackColor = back;
            ForeColor = fore;
            FlatAppearance.BorderColor = border;
            FlatAppearance.MouseOverBackColor = hover;
            FlatAppearance.MouseDownBackColor = hover;
        }

        protected override bool ShowFocusCues
        {
            get { return false; }
        }
    }

    // Drop-down list drawn entirely in theme colours (closed box and open list)
    internal sealed class FlatComboBox : ComboBox, IThemed
    {
        private const int WM_PAINT = 0x000F;
        private Theme theme = Theme.Dark;
        private bool hover;

        public FlatComboBox()
        {
            DropDownStyle = ComboBoxStyle.DropDownList;
            FlatStyle = FlatStyle.Flat;
            DrawMode = DrawMode.OwnerDrawFixed;
            ItemHeight = 20;
            Margin = new Padding(0, 3, 6, 0);
            Cursor = Cursors.Hand;
        }

        public void ApplyTheme(Theme t)
        {
            theme = t;
            BackColor = t.Surface;
            ForeColor = t.Text;
            Invalidate();
        }

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            bool selected = (e.State & DrawItemState.Selected) != 0 && (e.State & DrawItemState.ComboBoxEdit) == 0;
            using (var back = new SolidBrush(selected ? theme.SurfaceHover : theme.Surface))
                e.Graphics.FillRectangle(back, e.Bounds);
            var bounds = new Rectangle(e.Bounds.X + 6, e.Bounds.Y, e.Bounds.Width - 6, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, GetItemText(Items[e.Index]), Font, bounds, theme.Text,
                TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == WM_PAINT) PaintClosed();
        }

        // Repaint the closed box so it never shows the system's light border, arrow or grey disabled state
        private void PaintClosed()
        {
            using (Graphics g = Graphics.FromHwnd(Handle))
            {
                var r = ClientRectangle;
                Color back = !Enabled ? theme.Window : hover ? theme.SurfaceHover : theme.Surface;
                using (var b = new SolidBrush(back)) g.FillRectangle(b, r);

                int arrowWidth = 22;
                var textRect = new Rectangle(r.X + 6, r.Y, r.Width - arrowWidth - 6, r.Height);
                TextRenderer.DrawText(g, Text, Font, textRect, Enabled ? theme.Text : theme.Muted,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

                // Chevron
                g.SmoothingMode = SmoothingMode.AntiAlias;
                float cx = r.Right - arrowWidth / 2f - 2, cy = r.Height / 2f;
                using (var pen = new Pen(Enabled ? theme.Muted : theme.Border, 1.5f))
                    g.DrawLines(pen, new[] { new PointF(cx - 4, cy - 2), new PointF(cx, cy + 2), new PointF(cx + 4, cy - 2) });
                g.SmoothingMode = SmoothingMode.None;

                using (var pen = new Pen(theme.Border))
                    g.DrawRectangle(pen, 0, 0, r.Width - 1, r.Height - 1);
            }
        }
    }

    // Check box with a themed box and tick instead of the system glyph
    internal sealed class FlatCheckBox : CheckBox, IThemed
    {
        private Theme theme = Theme.Dark;
        private bool hover;

        public FlatCheckBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Cursor = Cursors.Hand;
        }

        public void ApplyTheme(Theme t)
        {
            theme = t;
            BackColor = t.Window;
            ForeColor = t.Text;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

        public override Size GetPreferredSize(Size proposedSize)
        {
            Size text = TextRenderer.MeasureText(Text, Font);
            return new Size(text.Width + 22, Math.Max(text.Height, 18));
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.Clear(theme.Window);
            int size = 14;
            var box = new Rectangle(0, (Height - size) / 2, size, size);

            g.SmoothingMode = SmoothingMode.AntiAlias;
            if (Checked)
            {
                using (var b = new SolidBrush(Enabled ? theme.Success : theme.Border)) g.FillRectangle(b, box);
                using (var pen = new Pen(theme.OnAccent, 1.8f))
                {
                    pen.StartCap = pen.EndCap = LineCap.Round;
                    g.DrawLines(pen, new[]
                    {
                        new PointF(box.X + 3.2f, box.Y + 7.2f),
                        new PointF(box.X + 6f, box.Y + 10f),
                        new PointF(box.X + 10.8f, box.Y + 4.2f)
                    });
                }
            }
            else
            {
                using (var b = new SolidBrush(hover ? theme.SurfaceHover : theme.Surface)) g.FillRectangle(b, box);
                using (var pen = new Pen(hover ? theme.Muted : theme.Border)) g.DrawRectangle(pen, box.X, box.Y, size - 1, size - 1);
            }
            g.SmoothingMode = SmoothingMode.None;

            var textRect = new Rectangle(size + 7, 0, Width - size - 7, Height);
            TextRenderer.DrawText(g, Text, Font, textRect, Enabled ? theme.Text : theme.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
        }
    }

    // Panel that draws a one-pixel border around its padded content
    internal sealed class BorderPanel : Panel, IThemed
    {
        private Color borderColor = Color.Gray;
        public bool UseOutputBackground;

        public BorderPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
        }

        public void ApplyTheme(Theme t)
        {
            borderColor = t.Border;
            BackColor = UseOutputBackground ? t.OutputBack : t.Surface;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(borderColor))
                e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        }
    }

    internal enum DotState { Idle, Connected, Waiting }

    // Small status indicator circle
    internal sealed class StatusDot : Control, IThemed
    {
        private DotState state;
        private Theme theme = Theme.Dark;

        public StatusDot()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.UserPaint | ControlStyles.SupportsTransparentBackColor, true);
            Size = new Size(10, 10);
            Margin = new Padding(0, 6, 8, 0);
            BackColor = Color.Transparent;
        }

        public DotState State
        {
            get { return state; }
            set { state = value; Invalidate(); }
        }

        public void ApplyTheme(Theme t)
        {
            theme = t;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Color c = state == DotState.Connected ? theme.Success : state == DotState.Waiting ? theme.Warning : theme.Muted;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int d = Math.Min(Width, Height) - 1;
            using (var b = new SolidBrush(c))
                e.Graphics.FillEllipse(b, 0, (Height - d) / 2, d, d);
        }
    }

    // Menu colours that follow the app theme
    internal sealed class ThemedColorTable : ProfessionalColorTable
    {
        private readonly Theme t;
        public ThemedColorTable(Theme theme) { t = theme; UseSystemColors = false; }

        public override Color ToolStripDropDownBackground { get { return t.Surface; } }
        public override Color ImageMarginGradientBegin { get { return t.Surface; } }
        public override Color ImageMarginGradientMiddle { get { return t.Surface; } }
        public override Color ImageMarginGradientEnd { get { return t.Surface; } }
        public override Color MenuBorder { get { return t.Border; } }
        public override Color MenuItemBorder { get { return t.SurfaceHover; } }
        public override Color MenuItemSelected { get { return t.SurfaceHover; } }
        public override Color MenuItemSelectedGradientBegin { get { return t.SurfaceHover; } }
        public override Color MenuItemSelectedGradientEnd { get { return t.SurfaceHover; } }
        public override Color MenuItemPressedGradientBegin { get { return t.SurfaceHover; } }
        public override Color MenuItemPressedGradientEnd { get { return t.SurfaceHover; } }
        public override Color SeparatorDark { get { return t.Border; } }
        public override Color SeparatorLight { get { return t.Surface; } }
        public override Color CheckBackground { get { return t.Surface; } }
        public override Color CheckSelectedBackground { get { return t.SurfaceHover; } }
        public override Color CheckPressedBackground { get { return t.SurfaceHover; } }
    }

    internal sealed class ThemedMenuRenderer : ToolStripProfessionalRenderer
    {
        private readonly Theme t;

        public ThemedMenuRenderer(Theme theme) : base(new ThemedColorTable(theme))
        {
            t = theme;
            RoundedEdges = false;
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = e.Item.Enabled ? t.Text : t.Muted;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = t.Muted;
            base.OnRenderArrow(e);
        }

        // Green tick instead of the system check glyph
        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            var r = e.ImageRectangle;
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var pen = new Pen(t.Success, 2f))
            {
                pen.StartCap = pen.EndCap = LineCap.Round;
                g.DrawLines(pen, new[]
                {
                    new PointF(r.Left + r.Width * 0.22f, r.Top + r.Height * 0.52f),
                    new PointF(r.Left + r.Width * 0.42f, r.Top + r.Height * 0.72f),
                    new PointF(r.Left + r.Width * 0.80f, r.Top + r.Height * 0.30f)
                });
            }
            g.SmoothingMode = SmoothingMode.None;
        }

        public static void Apply(ToolStrip strip, Theme theme)
        {
            strip.Renderer = new ThemedMenuRenderer(theme);
            strip.BackColor = theme.Surface;
            strip.ForeColor = theme.Text;
            foreach (ToolStripItem item in strip.Items)
            {
                item.ForeColor = theme.Text;
                var sub = item as ToolStripMenuItem;
                if (sub != null && sub.HasDropDownItems) Apply(sub.DropDown, theme);
            }
        }
    }

    internal static class NativeMethods
    {
        private const int EM_SETCUEBANNER = 0x1501;

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, string lParam);

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(IntPtr hWnd, string appName, string idList);

        // Dark or light scroll bars (dark needs Windows 10 1809 or later; older versions keep the default)
        public static void SetScrollBarTheme(Control control, bool dark)
        {
            if (!control.IsHandleCreated) return;
            try { SetWindowTheme(control.Handle, dark ? "DarkMode_Explorer" : "Explorer", null); }
            catch (Exception) { }
        }

        // Grey placeholder text in a single-line TextBox
        public static void SetPlaceholder(TextBox box, string text)
        {
            if (box.IsHandleCreated) SendMessage(box.Handle, EM_SETCUEBANNER, (IntPtr)1, text);
            else box.HandleCreated += delegate { SendMessage(box.Handle, EM_SETCUEBANNER, (IntPtr)1, text); };
        }
    }
}
