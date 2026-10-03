using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace SerialScope
{
    // Edit the list of highlight rules
    internal sealed class HighlightRulesForm : Form
    {
        private readonly Theme theme;
        private readonly List<HighlightRule> rules = new List<HighlightRule>();
        private readonly ListBox list = new ListBox();
        private readonly TextBox patternBox = new TextBox();
        private readonly FlatComboBox colorBox = new FlatComboBox();
        private readonly FlatCheckBox regexBox = new FlatCheckBox();
        private readonly FlatCheckBox caseBox = new FlatCheckBox();
        private readonly FlatCheckBox enabledBox = new FlatCheckBox();
        private readonly Label errorLabel = new Label();
        private readonly List<Control> editors = new List<Control>();
        private bool loading;

        public List<HighlightRule> Rules
        {
            get { return rules; }
        }

        public HighlightRulesForm(Theme theme, List<HighlightRule> current)
        {
            this.theme = theme;
            foreach (HighlightRule r in current) rules.Add(r.Clone());

            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = "Highlight rules";
            Font = new Font("Segoe UI", 9F);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(520, 430);
            BackColor = theme.Window;
            ForeColor = theme.Text;

            Controls.Add(new Label
            {
                Text = "Lines that match a rule are shown in its colour. Rules are checked from top to bottom, " +
                       "and come before the built-in error and warning colours.",
                Location = new Point(20, 14),
                Size = new Size(480, 36),
                ForeColor = theme.Muted
            });

            var listFrame = new BorderPanel { Location = new Point(20, 56), Size = new Size(480, 150), Padding = new Padding(1) };
            list.Dock = DockStyle.Fill;
            list.BorderStyle = BorderStyle.None;
            list.DrawMode = DrawMode.OwnerDrawFixed;
            list.ItemHeight = 26;
            list.IntegralHeight = false;
            list.BackColor = theme.Surface;
            list.ForeColor = theme.Text;
            list.DrawItem += DrawRule;
            list.SelectedIndexChanged += delegate { LoadSelected(); };
            listFrame.Controls.Add(list);
            listFrame.ApplyTheme(theme);
            Controls.Add(listFrame);

            int x = 20;
            foreach (var spec in new[] { "Add rule", "Remove", "Move up", "Move down" })
            {
                var b = new FlatButton(spec, ButtonKind.Normal) { Location = new Point(x, 214) };
                b.ApplyTheme(theme);
                Controls.Add(b);
                string which = spec;
                b.Click += delegate { ListAction(which); };
                if (which != "Add rule") editors.Add(b);
                x += b.PreferredSize.Width + 6;
            }

            Controls.Add(new Label { Text = "Text or pattern", AutoSize = true, Location = new Point(20, 258), ForeColor = theme.Text });
            var patternFrame = new BorderPanel { Location = new Point(20, 278), Size = new Size(320, 30), Padding = new Padding(8, 7, 8, 4) };
            patternBox.BorderStyle = BorderStyle.None;
            patternBox.Dock = DockStyle.Fill;
            patternBox.BackColor = theme.Surface;
            patternBox.ForeColor = theme.Text;
            patternBox.Font = new Font("Consolas", 10F);
            patternBox.TextChanged += delegate { EditSelected(); };
            NativeMethods.SetPlaceholder(patternBox, "e.g. TEMP or connected");
            patternFrame.Controls.Add(patternBox);
            patternFrame.ApplyTheme(theme);
            Controls.Add(patternFrame);
            editors.Add(patternBox);

            Controls.Add(new Label { Text = "Colour", AutoSize = true, Location = new Point(356, 258), ForeColor = theme.Text });
            colorBox.Items.AddRange(HighlightRule.ColorNames);
            colorBox.Location = new Point(356, 278);
            colorBox.Width = 144;
            colorBox.ApplyTheme(theme);
            colorBox.SelectedIndexChanged += delegate { EditSelected(); };
            Controls.Add(colorBox);
            editors.Add(colorBox);

            AddCheck(regexBox, "Regular expression", 20);
            AddCheck(caseBox, "Match case", 180);
            AddCheck(enabledBox, "Enabled", 300);

            errorLabel.Location = new Point(20, 346);
            errorLabel.Size = new Size(480, 20);
            errorLabel.ForeColor = theme.Danger;
            Controls.Add(errorLabel);

            var cancel = new FlatButton("Cancel", ButtonKind.Normal) { MinimumSize = new Size(80, 30), Location = new Point(334, 384), DialogResult = DialogResult.Cancel };
            var ok = new FlatButton("OK", ButtonKind.Success) { MinimumSize = new Size(80, 30), Location = new Point(420, 384) };
            cancel.ApplyTheme(theme);
            ok.ApplyTheme(theme);
            ok.Click += delegate { Accept(); };
            AcceptButton = ok;
            CancelButton = cancel;
            Controls.Add(cancel);
            Controls.Add(ok);

            RefreshList();
            if (rules.Count > 0) list.SelectedIndex = 0;
            else LoadSelected();
        }

        private void AddCheck(FlatCheckBox box, string text, int x)
        {
            box.Text = text;
            box.AutoSize = true;
            box.Location = new Point(x, 320);
            box.ApplyTheme(theme);
            box.CheckedChanged += delegate { EditSelected(); };
            Controls.Add(box);
            editors.Add(box);
        }

        private HighlightRule Selected
        {
            get { return list.SelectedIndex >= 0 && list.SelectedIndex < rules.Count ? rules[list.SelectedIndex] : null; }
        }

        private void RefreshList()
        {
            int keep = list.SelectedIndex;
            list.BeginUpdate();
            list.Items.Clear();
            foreach (HighlightRule r in rules) list.Items.Add(r);
            list.EndUpdate();
            if (keep >= 0 && keep < rules.Count) list.SelectedIndex = keep;
        }

        private void LoadSelected()
        {
            HighlightRule r = Selected;
            loading = true;
            foreach (Control c in editors) c.Enabled = r != null;
            patternBox.Text = r == null ? "" : r.Pattern;
            colorBox.SelectedIndex = r == null ? 0 : r.Color;
            regexBox.Checked = r != null && r.IsRegex;
            caseBox.Checked = r != null && r.MatchCase;
            enabledBox.Checked = r == null || r.Enabled;
            loading = false;
            Validate(r);
        }

        private void EditSelected()
        {
            if (loading) return;
            HighlightRule r = Selected;
            if (r == null) return;
            r.Pattern = patternBox.Text;
            r.Color = Math.Max(0, colorBox.SelectedIndex);
            r.IsRegex = regexBox.Checked;
            r.MatchCase = caseBox.Checked;
            r.Enabled = enabledBox.Checked;
            list.Invalidate();
            Validate(r);
        }

        private bool Validate(HighlightRule r)
        {
            string error = null;
            bool ok = r == null || r.IsValid(out error);
            errorLabel.Text = ok ? "" : error;
            return ok;
        }

        private void ListAction(string action)
        {
            int i = list.SelectedIndex;
            switch (action)
            {
                case "Add rule":
                    rules.Add(new HighlightRule { Color = rules.Count % HighlightRule.ColorNames.Length });
                    RefreshList();
                    list.SelectedIndex = rules.Count - 1;
                    patternBox.Focus();
                    break;
                case "Remove":
                    if (i < 0) return;
                    rules.RemoveAt(i);
                    RefreshList();
                    list.SelectedIndex = Math.Min(i, rules.Count - 1);
                    if (rules.Count == 0) LoadSelected();
                    break;
                case "Move up":
                case "Move down":
                    int j = action == "Move up" ? i - 1 : i + 1;
                    if (i < 0 || j < 0 || j >= rules.Count) return;
                    HighlightRule moved = rules[i];
                    rules[i] = rules[j];
                    rules[j] = moved;
                    RefreshList();
                    list.SelectedIndex = j;
                    break;
            }
        }

        private void DrawRule(object sender, DrawItemEventArgs e)
        {
            if (e.Index < 0 || e.Index >= rules.Count) return;
            HighlightRule r = rules[e.Index];
            bool selected = (e.State & DrawItemState.Selected) != 0;
            using (var back = new SolidBrush(selected ? theme.SurfaceHover : theme.Surface))
                e.Graphics.FillRectangle(back, e.Bounds);

            Color swatch = HighlightRule.ColorFor(r.Color, theme.IsDark);
            using (var b = new SolidBrush(r.Enabled ? swatch : theme.Border))
                e.Graphics.FillRectangle(b, e.Bounds.X + 10, e.Bounds.Y + 8, 10, 10);

            string text = r.Pattern.Length == 0 ? "(empty rule)" : r.Pattern;
            Color textColor = !r.Enabled || r.Pattern.Length == 0 ? theme.Muted : swatch;
            TextRenderer.DrawText(e.Graphics, text, new Font("Consolas", 10F), new Rectangle(e.Bounds.X + 28, e.Bounds.Y, e.Bounds.Width - 160, e.Bounds.Height),
                textColor, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);

            var flags = new List<string>();
            if (r.IsRegex) flags.Add("regex");
            if (r.MatchCase) flags.Add("case");
            if (!r.Enabled) flags.Add("off");
            TextRenderer.DrawText(e.Graphics, string.Join(" · ", flags.ToArray()), Font, new Rectangle(e.Bounds.Right - 130, e.Bounds.Y, 120, e.Bounds.Height),
                theme.Muted, TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
        }

        private void Accept()
        {
            rules.RemoveAll(r => r.Pattern.Trim().Length == 0);
            for (int i = 0; i < rules.Count; i++)
            {
                string error;
                if (!rules[i].IsValid(out error))
                {
                    RefreshList();
                    list.SelectedIndex = i;
                    errorLabel.Text = "Rule " + (i + 1) + ": " + error;
                    return;
                }
            }
            DialogResult = DialogResult.OK;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            Theme.ApplyTitleBar(this, theme.IsDark);
            NativeMethods.SetScrollBarTheme(list, theme.IsDark);
        }
    }
}
