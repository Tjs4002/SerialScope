using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SerialScope
{
    internal sealed class Theme
    {
        public string Name;
        public bool IsDark;
        public Color Window;
        public Color Surface;
        public Color SurfaceHover;
        public Color Border;
        public Color Text;
        public Color Muted;
        public Color OutputBack;
        public Color OutputText;
        public Color Success;
        public Color Danger;
        public Color Warning;
        public Color OnAccent;

        public static readonly Theme Dark = new Theme
        {
            Name = "dark",
            IsDark = true,
            Window = Color.FromArgb(24, 24, 27),
            Surface = Color.FromArgb(39, 39, 42),
            SurfaceHover = Color.FromArgb(52, 52, 56),
            Border = Color.FromArgb(63, 63, 70),
            Text = Color.FromArgb(228, 228, 231),
            Muted = Color.FromArgb(161, 161, 170),
            OutputBack = Color.FromArgb(9, 9, 11),
            OutputText = Color.FromArgb(212, 212, 216),
            Success = Color.FromArgb(22, 163, 74),
            Danger = Color.FromArgb(220, 38, 38),
            Warning = Color.FromArgb(234, 179, 8),
            OnAccent = Color.White
        };

        public static readonly Theme Light = new Theme
        {
            Name = "light",
            IsDark = false,
            Window = Color.FromArgb(244, 244, 245),
            Surface = Color.White,
            SurfaceHover = Color.FromArgb(228, 228, 231),
            Border = Color.FromArgb(212, 212, 216),
            Text = Color.FromArgb(24, 24, 27),
            Muted = Color.FromArgb(113, 113, 122),
            OutputBack = Color.White,
            OutputText = Color.FromArgb(24, 24, 27),
            Success = Color.FromArgb(22, 163, 74),
            Danger = Color.FromArgb(220, 38, 38),
            Warning = Color.FromArgb(202, 138, 4),
            OnAccent = Color.White
        };

        public static Theme FromName(string name)
        {
            return name == "light" ? Light : Dark;
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hwnd, IntPtr after, int x, int y, int cx, int cy, uint flags);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam);

        private const int WM_NCACTIVATE = 0x0086;

        // Dark title bar on Windows 10 1809+ / Windows 11; silently ignored elsewhere
        public static void ApplyTitleBar(Form form, bool dark)
        {
            if (!form.IsHandleCreated) return;
            int value = dark ? 1 : 0;
            try
            {
                if (DwmSetWindowAttribute(form.Handle, 20, ref value, sizeof(int)) != 0)
                    DwmSetWindowAttribute(form.Handle, 19, ref value, sizeof(int));
                // Make Windows repaint the frame now rather than on the next activation
                SetWindowPos(form.Handle, IntPtr.Zero, 0, 0, 0, 0, 0x0027);   // NOSIZE | NOMOVE | NOZORDER | FRAMECHANGED
                if (form.Visible)
                {
                    // Windows 10 only redraws the caption colour on (de)activation, so nudge it
                    bool active = Form.ActiveForm == form;
                    SendMessage(form.Handle, WM_NCACTIVATE, (IntPtr)(active ? 0 : 1), IntPtr.Zero);
                    SendMessage(form.Handle, WM_NCACTIVATE, (IntPtr)(active ? 1 : 0), IntPtr.Zero);
                }
            }
            catch (Exception)
            {
            }
        }

        public void Apply(Control root)
        {
            foreach (Control c in root.Controls)
            {
                var themed = c as IThemed;
                if (themed != null) themed.ApplyTheme(this);
                else if (c is Label) { c.ForeColor = c.Tag as string == "muted" ? Muted : Text; c.BackColor = Color.Transparent; }
                else if (c is CheckBox) StyleCheckBox((CheckBox)c);
                else if (c is TextBox) { c.BackColor = c.Tag as string == "output" ? OutputBack : Surface; c.ForeColor = c.Tag as string == "output" ? OutputText : Text; }
                else if (c is Panel || c is FlowLayoutPanel || c is TableLayoutPanel) c.BackColor = c.Tag as string == "surface" ? Surface : Window;

                if (c.HasChildren) Apply(c);
            }
        }

        private void StyleCheckBox(CheckBox box)
        {
            box.FlatStyle = FlatStyle.Flat;
            box.FlatAppearance.BorderColor = Border;
            box.FlatAppearance.CheckedBackColor = Surface;
            box.FlatAppearance.MouseOverBackColor = SurfaceHover;
            box.BackColor = Color.Transparent;
            box.ForeColor = Text;
        }
    }

    internal interface IThemed
    {
        void ApplyTheme(Theme theme);
    }
}
