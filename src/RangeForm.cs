using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace SerialScope
{
    // Dialog for fixing the plotter's value range (or returning to automatic scaling)
    internal sealed class RangeForm : Form
    {
        private readonly TextBox minBox = new TextBox();
        private readonly TextBox maxBox = new TextBox();

        public double Minimum { get; private set; }
        public double Maximum { get; private set; }
        public bool UseAutomatic { get; private set; }

        public RangeForm(Theme theme, bool isFixed, double min, double max)
        {
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = "Value range";
            Font = new Font("Segoe UI", 9F);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(340, 176);
            BackColor = theme.Window;
            ForeColor = theme.Text;

            var hint = new Label
            {
                Text = "Keep the graph between these values instead of scaling automatically.",
                Location = new Point(20, 14),
                Size = new Size(300, 34),
                ForeColor = theme.Muted
            };

            Controls.Add(hint);
            AddField(theme, "Minimum", minBox, 20, isFixed ? Format(min) : "");
            AddField(theme, "Maximum", maxBox, 180, isFixed ? Format(max) : "");

            var apply = new FlatButton("Apply", ButtonKind.Success) { MinimumSize = new Size(80, 30), Location = new Point(240, 128) };
            var auto = new FlatButton("Automatic", ButtonKind.Normal) { MinimumSize = new Size(90, 30), Location = new Point(20, 128) };
            var cancel = new FlatButton("Cancel", ButtonKind.Normal) { MinimumSize = new Size(80, 30), Location = new Point(154, 128), DialogResult = DialogResult.Cancel };
            foreach (var b in new[] { apply, auto, cancel }) b.ApplyTheme(theme);
            apply.Click += delegate { Accept(); };
            auto.Click += delegate { UseAutomatic = true; DialogResult = DialogResult.OK; };

            AcceptButton = apply;
            CancelButton = cancel;
            Controls.AddRange(new Control[] { apply, auto, cancel });
        }

        private void AddField(Theme theme, string label, TextBox box, int x, string value)
        {
            Controls.Add(new Label { Text = label, AutoSize = true, Location = new Point(x, 56), ForeColor = theme.Text });
            var frame = new BorderPanel { Location = new Point(x, 76), Size = new Size(140, 30), Padding = new Padding(8, 7, 8, 4) };
            box.BorderStyle = BorderStyle.None;
            box.Dock = DockStyle.Fill;
            box.BackColor = theme.Surface;
            box.ForeColor = theme.Text;
            box.Font = new Font("Consolas", 10F);
            box.Text = value;
            frame.Controls.Add(box);
            frame.ApplyTheme(theme);
            Controls.Add(frame);
        }

        private void Accept()
        {
            double min, max;
            if (!TryParse(minBox.Text, out min) || !TryParse(maxBox.Text, out max) || min == max)
            {
                MessageBox.Show(this, "Enter two different numbers, for example 0 and 4095.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            Minimum = Math.Min(min, max);
            Maximum = Math.Max(min, max);
            DialogResult = DialogResult.OK;
        }

        private static bool TryParse(string text, out double value)
        {
            return double.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static string Format(double v)
        {
            return v.ToString("0.######", CultureInfo.InvariantCulture);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.ApplyTitleBar(this, BackColor.GetBrightness() < 0.5f);
        }
    }
}
