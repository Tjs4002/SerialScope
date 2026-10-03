using System;
using System.Drawing;
using System.Windows.Forms;

namespace SerialScope
{
    // Small dialog for entering a baud rate that is not in the list
    internal sealed class CustomBaudForm : Form
    {
        private readonly TextBox input = new TextBox();

        public int BaudRate { get; private set; }

        public CustomBaudForm(Theme theme)
        {
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = "Custom baud rate";
            Font = new Font("Segoe UI", 9F);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(300, 130);
            BackColor = theme.Window;
            ForeColor = theme.Text;

            var label = new Label { Text = "Baud rate", AutoSize = true, Location = new Point(20, 18), ForeColor = theme.Text };

            var frame = new BorderPanel { Location = new Point(20, 40), Size = new Size(260, 30), Padding = new Padding(8, 7, 8, 4) };
            input.BorderStyle = BorderStyle.None;
            input.Dock = DockStyle.Fill;
            input.BackColor = theme.Surface;
            input.ForeColor = theme.Text;
            input.Font = new Font("Consolas", 10F);
            frame.Controls.Add(input);
            frame.ApplyTheme(theme);
            NativeMethods.SetPlaceholder(input, "e.g. 31250");

            var ok = new FlatButton("OK", ButtonKind.Success) { MinimumSize = new Size(80, 30), Location = new Point(114, 86) };
            var cancel = new FlatButton("Cancel", ButtonKind.Normal) { MinimumSize = new Size(80, 30), Location = new Point(200, 86), DialogResult = DialogResult.Cancel };
            ok.ApplyTheme(theme);
            cancel.ApplyTheme(theme);
            ok.Click += delegate { Accept(); };

            AcceptButton = ok;
            CancelButton = cancel;
            Controls.AddRange(new Control[] { label, frame, ok, cancel });
        }

        private void Accept()
        {
            int rate;
            if (int.TryParse(input.Text.Trim(), out rate) && rate > 0 && rate <= 20000000)
            {
                BaudRate = rate;
                DialogResult = DialogResult.OK;
            }
            else
            {
                MessageBox.Show(this, "Enter a whole number, for example 31250.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                input.Focus();
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.ApplyTitleBar(this, BackColor.GetBrightness() < 0.5f);
        }
    }
}
