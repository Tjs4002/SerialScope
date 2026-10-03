using System;
using System.Drawing;
using System.IO.Ports;
using System.Windows.Forms;

namespace SerialScope
{
    // Data bits, parity, stop bits, flow control and the DTR/RTS control lines
    internal sealed class PortSettingsForm : Form
    {
        private readonly FlatComboBox dataBitsBox = new FlatComboBox();
        private readonly FlatComboBox parityBox = new FlatComboBox();
        private readonly FlatComboBox stopBitsBox = new FlatComboBox();
        private readonly FlatComboBox flowBox = new FlatComboBox();
        private readonly FlatCheckBox dtrBox = new FlatCheckBox();
        private readonly FlatCheckBox rtsBox = new FlatCheckBox();

        public PortConfig Result { get; private set; }

        public PortSettingsForm(Theme theme, PortConfig current)
        {
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = "Port settings";
            Font = new Font("Segoe UI", 9F);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(400, 330);
            BackColor = theme.Window;
            ForeColor = theme.Text;

            foreach (int b in PortConfig.DataBitOptions) dataBitsBox.Items.Add(b.ToString());
            foreach (Parity p in PortConfig.ParityOptions) parityBox.Items.Add(p.ToString());
            stopBitsBox.Items.AddRange(new object[] { "1", "1.5", "2" });
            foreach (Handshake h in PortConfig.HandshakeOptions) flowBox.Items.Add(PortConfig.HandshakeText(h));

            AddRow(theme, "Data bits", dataBitsBox, 20);
            AddRow(theme, "Parity", parityBox, 58);
            AddRow(theme, "Stop bits", stopBitsBox, 96);
            AddRow(theme, "Flow control", flowBox, 134);

            Controls.Add(new Label
            {
                Text = "CONTROL LINES",
                AutoSize = true,
                Location = new Point(20, 182),
                ForeColor = theme.Muted,
                Font = new Font("Segoe UI Semibold", 8F)
            });
            dtrBox.Text = "DTR on";
            rtsBox.Text = "RTS on";
            dtrBox.Location = new Point(20, 204);
            rtsBox.Location = new Point(120, 204);
            dtrBox.AutoSize = rtsBox.AutoSize = true;
            dtrBox.ApplyTheme(theme);
            rtsBox.ApplyTheme(theme);
            Controls.Add(dtrBox);
            Controls.Add(rtsBox);
            Controls.Add(new Label
            {
                Text = "Leave both off for ESP32 and Arduino boards: their reset circuit uses these lines, so turning them on can restart the board or hold it in reset.",
                Location = new Point(20, 230),
                Size = new Size(360, 46),
                ForeColor = theme.Muted
            });

            var defaults = new FlatButton("Defaults (8N1)", ButtonKind.Normal) { MinimumSize = new Size(110, 30), Location = new Point(20, 284) };
            var cancel = new FlatButton("Cancel", ButtonKind.Normal) { MinimumSize = new Size(80, 30), Location = new Point(214, 284), DialogResult = DialogResult.Cancel };
            var ok = new FlatButton("OK", ButtonKind.Success) { MinimumSize = new Size(80, 30), Location = new Point(300, 284) };
            foreach (var b in new[] { defaults, cancel, ok }) b.ApplyTheme(theme);
            defaults.Click += delegate { Display(new PortConfig()); };
            ok.Click += delegate { Result = Read(); DialogResult = DialogResult.OK; };
            flowBox.SelectedIndexChanged += delegate { UpdateRts(); };
            AcceptButton = ok;
            CancelButton = cancel;
            Controls.AddRange(new Control[] { defaults, cancel, ok });

            Display(current);
        }

        private void AddRow(Theme theme, string label, FlatComboBox box, int y)
        {
            Controls.Add(new Label { Text = label, AutoSize = true, Location = new Point(20, y + 6), ForeColor = theme.Text });
            box.Location = new Point(140, y);
            box.Width = 240;
            box.ApplyTheme(theme);
            Controls.Add(box);
        }

        private void Display(PortConfig c)
        {
            dataBitsBox.SelectedIndex = Array.IndexOf(PortConfig.DataBitOptions, c.DataBits);
            parityBox.SelectedIndex = Array.IndexOf(PortConfig.ParityOptions, c.Parity);
            stopBitsBox.SelectedIndex = Array.IndexOf(PortConfig.StopBitOptions, c.StopBits);
            flowBox.SelectedIndex = Array.IndexOf(PortConfig.HandshakeOptions, c.Handshake);
            dtrBox.Checked = c.Dtr;
            rtsBox.Checked = c.Rts;
            UpdateRts();
        }

        private PortConfig Read()
        {
            return new PortConfig
            {
                DataBits = PortConfig.DataBitOptions[Math.Max(0, dataBitsBox.SelectedIndex)],
                Parity = PortConfig.ParityOptions[Math.Max(0, parityBox.SelectedIndex)],
                StopBits = PortConfig.StopBitOptions[Math.Max(0, stopBitsBox.SelectedIndex)],
                Handshake = PortConfig.HandshakeOptions[Math.Max(0, flowBox.SelectedIndex)],
                Dtr = dtrBox.Checked,
                Rts = rtsBox.Checked
            };
        }

        // With RTS/CTS flow control the RTS line is driven automatically
        private void UpdateRts()
        {
            bool auto = flowBox.SelectedIndex == 1 || flowBox.SelectedIndex == 3;
            rtsBox.Enabled = !auto;
            if (auto) rtsBox.Checked = false;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.ApplyTitleBar(this, BackColor.GetBrightness() < 0.5f);
        }
    }
}
