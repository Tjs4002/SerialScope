using System;
using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

namespace SerialScope
{
    internal sealed class AboutForm : Form
    {
        public AboutForm(Theme theme)
        {
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = "About " + AppInfo.Name;
            Font = new Font("Segoe UI", 9F);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(400, 340);
            BackColor = theme.Window;
            ForeColor = theme.Text;

            var layout = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(24, 20, 24, 16)
            };

            var icon = new PictureBox { Size = new Size(48, 48), SizeMode = PictureBoxSizeMode.Zoom, Margin = new Padding(0, 0, 0, 10) };
            try { icon.Image = Icon.ExtractAssociatedIcon(Application.ExecutablePath).ToBitmap(); } catch (Exception) { }
            layout.Controls.Add(icon);

            layout.Controls.Add(new Label
            {
                Text = AppInfo.Name,
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 14F),
                Margin = new Padding(0, 0, 0, 2)
            });
            layout.Controls.Add(new Label
            {
                Text = "Version " + AppInfo.Version + "  ·  by " + AppInfo.Author,
                AutoSize = true,
                ForeColor = theme.Muted,
                Margin = new Padding(0, 0, 0, 12)
            });
            layout.Controls.Add(new Label
            {
                Text = "A clean, lightweight serial monitor and plotter for Windows.\nReleased under the " + AppInfo.License + ".",
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 16)
            });

            // Creator section
            layout.Controls.Add(new Label
            {
                Text = "CREATED BY",
                AutoSize = true,
                ForeColor = theme.Muted,
                Font = new Font("Segoe UI Semibold", 8F),
                Margin = new Padding(0, 0, 0, 2)
            });
            layout.Controls.Add(new Label
            {
                Text = AppInfo.Author,
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 11F),
                Margin = new Padding(0, 0, 0, 2)
            });
            layout.Controls.Add(MakeLink(theme, "GitHub: " + AppInfo.ProfileUrl.Replace("https://", ""), AppInfo.ProfileUrl, new Padding(0, 0, 0, 8)));
            layout.Controls.Add(MakeLink(theme, "Source code: " + AppInfo.RepoUrl.Replace("https://", ""), AppInfo.RepoUrl, Padding.Empty));

            var close = new FlatButton("Close", ButtonKind.Normal);
            close.ApplyTheme(theme);
            close.DialogResult = DialogResult.OK;
            close.Anchor = AnchorStyles.Right | AnchorStyles.Bottom;
            close.Location = new Point(ClientSize.Width - 24 - 80, ClientSize.Height - 16 - 30);
            close.MinimumSize = new Size(80, 30);
            AcceptButton = close;
            CancelButton = close;

            Controls.Add(close);
            Controls.Add(layout);
            close.BringToFront();
        }

        private static LinkLabel MakeLink(Theme theme, string text, string url, Padding margin)
        {
            var link = new LinkLabel
            {
                Text = text,
                AutoSize = true,
                LinkColor = theme.Success,
                VisitedLinkColor = theme.Success,
                ActiveLinkColor = theme.Text,
                LinkBehavior = LinkBehavior.HoverUnderline,
                Margin = margin
            };
            link.LinkClicked += delegate
            {
                try { Process.Start(url); } catch (Exception) { }
            };
            return link;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.ApplyTitleBar(this, BackColor.GetBrightness() < 0.5f);
        }
    }
}
