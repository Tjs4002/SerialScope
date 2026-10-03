using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace SerialScope
{
    internal sealed class MainForm : Form
    {
        private const int MaxOutputChars = 1000000;   // trim the oldest output beyond this
        private const int TrimToChars = 800000;
        private const float DefaultFontSize = 10F;
        private const int MaxSearchMatches = 5000;

        private static readonly int[] BaudRates =
        {
            300, 1200, 2400, 4800, 9600, 19200, 38400, 57600, 74880,
            115200, 230400, 250000, 460800, 500000, 921600, 1000000, 2000000
        };

        private static readonly string[] LineEndings = { "", "\n", "\r", "\r\n" };
        private static readonly int[] PlotWindowSizes = { 100, 250, 500, 1000, 2500, 5000 };

        private readonly Settings settings = Settings.Load();
        private Theme theme;

        // Toolbar
        private readonly FlatComboBox portBox = new FlatComboBox();
        private readonly FlatComboBox baudBox = new FlatComboBox();
        private readonly FlatButton refreshButton = new FlatButton("", ButtonKind.Icon);
        private readonly FlatButton connectButton = new FlatButton("Connect", ButtonKind.Success);
        private readonly FlatButton clearButton = new FlatButton("", ButtonKind.Icon);
        private readonly FlatButton saveButton = new FlatButton("", ButtonKind.Icon);
        private readonly FlatButton settingsButton = new FlatButton("", ButtonKind.Icon);
        private readonly FlatButton themeButton = new FlatButton("", ButtonKind.Icon);
        private readonly FlatButton aboutButton = new FlatButton("", ButtonKind.Icon);
        private readonly ContextMenuStrip settingsMenu = new ContextMenuStrip();
        private readonly ToolStripMenuItem reconnectItem = new ToolStripMenuItem("Auto-reconnect");
        private readonly ToolStripMenuItem highlightItem = new ToolStripMenuItem("Highlight errors and warnings");
        private readonly ToolStripMenuItem updateStartupItem = new ToolStripMenuItem("Check for updates automatically");
        private readonly ToolStripMenuItem updateNowItem = new ToolStripMenuItem("Check for updates now");
        private readonly LinkLabel updateLink = new LinkLabel();
        private string updateUrl;
        private readonly ToolStripMenuItem sessionLogItem = new ToolStripMenuItem("Save session logs automatically");
        private readonly ToolStripMenuItem openLogsItem = new ToolStripMenuItem("Open logs folder");
        private StreamWriter sessionLog;
        private string sessionLogPath;

        private static readonly string LogsFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), AppInfo.Name, "Logs");

        // Options row
        private readonly FlatCheckBox timestampBox = new FlatCheckBox();
        private readonly FlatCheckBox autoScrollBox = new FlatCheckBox();
        private readonly FlatCheckBox pauseBox = new FlatCheckBox();
        private readonly FlatCheckBox hexBox = new FlatCheckBox();
        private readonly FlatButton textViewButton = new FlatButton("Text", ButtonKind.Normal);
        private readonly FlatButton plotViewButton = new FlatButton("Plotter", ButtonKind.Normal);
        private readonly Label plotPointsLabel = new Label();
        private readonly FlatComboBox plotPointsBox = new FlatComboBox();
        private readonly FlatButton recordButton = new FlatButton("●  Record", ButtonKind.Normal);
        private readonly FlatButton imageButton = new FlatButton("", ButtonKind.Icon);

        // Find bar
        private readonly Panel findBar = new Panel();
        private readonly TextBox findBox = new TextBox();
        private readonly Label findCount = new Label();
        private readonly FlatCheckBox findCaseBox = new FlatCheckBox();
        private List<int> findMatches = new List<int>();
        private int findIndex = -1;
        private int findVersion = -1;   // output version the matches belong to

        // Output
        private readonly OutputView output = new OutputView();
        private readonly ContextMenuStrip outputMenu = new ContextMenuStrip();
        private readonly BorderPanel outputFrame = new BorderPanel();
        private readonly SplitContainer plotSplit = new SplitContainer();
        private readonly PlotView plot = new PlotView();
        private double plotSplitRatio = 0.7;
        private bool plotMode;

        // Send row
        private readonly TextBox sendBox = new TextBox();
        private readonly BorderPanel sendFrame = new BorderPanel();
        private readonly FlatComboBox lineEndingBox = new FlatComboBox();
        private readonly FlatButton sendButton = new FlatButton("Send", ButtonKind.Normal);
        private readonly FlatButton commandsButton = new FlatButton("", ButtonKind.Icon);
        private readonly ContextMenuStrip commandsMenu = new ContextMenuStrip();
        private readonly List<string> sendHistory = new List<string>();
        private readonly List<string> savedCommands = new List<string>();
        private int historyIndex = -1;   // -1 = editing a new message
        private string historyDraft = "";
        private const int MaxHistory = 50;
        private const char ListSeparator = '\u001F';

        // Status bar
        private readonly StatusDot statusDot = new StatusDot();
        private readonly Label statusLabel = new Label();
        private readonly Label statsLabel = new Label();
        private readonly Label creditSeparator = new Label();
        private readonly LinkLabel creditLink = new LinkLabel();
        private readonly ToolTip tips = new ToolTip();

        private readonly System.Windows.Forms.Timer flushTimer = new System.Windows.Forms.Timer();
        private readonly System.Windows.Forms.Timer watchTimer = new System.Windows.Forms.Timer();

        // Connection
        private readonly ConcurrentQueue<byte[]> incoming = new ConcurrentQueue<byte[]>();
        private SerialPort port;
        private volatile bool portLost;
        private long bytesReceived;
        private long bytesSent;
        private string[] knownPorts = new string[0];
        private string reconnectPort;     // set while waiting for a lost port to come back
        private int reconnectBaud;
        private bool autoReconnect = true;

        // Output formatting
        private readonly Decoder decoder = Encoding.UTF8.GetDecoder();
        private char[] charBuffer = new char[4096];
        private readonly StringBuilder pending = new StringBuilder();   // text waiting to be appended in pendingColor
        private Color pendingColor;
        private bool atLineStart = true;
        private int lineContentStart;                                   // output position where the current line's text starts
        private bool lineHasEarlierPart;                                // part of the current line was already appended
        private readonly StringBuilder lineText = new StringBuilder();  // current line text not yet appended
        private readonly StringBuilder rawLine = new StringBuilder();   // whole current line, for colouring
        private bool highlight = true;
        private bool hexMode;
        private int hexCount;
        private long hexOffset;
        private readonly StringBuilder hexAscii = new StringBuilder();
        private int outputVersion;

        // Plotter and recording
        private readonly StringBuilder plotLine = new StringBuilder();
        private readonly Recorder recorder = new Recorder();
        private readonly List<string> parsedNames = new List<string>();
        private readonly List<double> parsedValues = new List<double>();

        public MainForm()
        {
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = AppInfo.Name;
            Font = new Font("Segoe UI", 9F);
            MinimumSize = new Size(900, 480);
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(settings.GetInt("width", 1000), settings.GetInt("height", 660));
            KeyPreview = true;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch (Exception) { }

            BuildLayout();
            LoadPreferences();
            RefreshPorts();
            UpdateUiState();

            flushTimer.Interval = 50;
            flushTimer.Tick += delegate { FlushIncoming(); };
            flushTimer.Start();

            watchTimer.Interval = 1000;
            watchTimer.Tick += delegate { WatchPorts(); };
            watchTimer.Start();
        }

        // ------------------------------------------------------------------ layout

        private void BuildLayout()
        {
            // Toolbar: connection controls on the left, actions on the right
            var toolbar = new Panel { Dock = DockStyle.Top, Height = 54, Padding = new Padding(12, 12, 12, 0) };

            var actions = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, WrapContents = false, Padding = Padding.Empty };
            AddIconButton(actions, clearButton, "Clear output (Ctrl+L)", delegate { ClearOutput(); });
            AddIconButton(actions, saveButton, "Save log (Ctrl+S)", delegate { SaveLog(); });
            AddIconButton(actions, settingsButton, "Settings", delegate { settingsMenu.Show(settingsButton, new Point(0, settingsButton.Height + 2)); });
            AddIconButton(actions, themeButton, "Switch light / dark theme", delegate { ToggleTheme(); });
            AddIconButton(actions, aboutButton, "About " + AppInfo.Name, delegate { ShowAbout(); });
            aboutButton.Margin = Padding.Empty;
            BuildSettingsMenu();

            var connection = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = Padding.Empty };
            connection.Controls.Add(MakeLabel("Port", false));
            portBox.Width = 300;
            connection.Controls.Add(portBox);
            AddIconButton(connection, refreshButton, "Rescan ports", delegate { RefreshPorts(); });
            refreshButton.Margin = new Padding(0, 0, 14, 0);
            connection.Controls.Add(MakeLabel("Baud", false));
            baudBox.Width = 104;
            foreach (int rate in BaudRates) baudBox.Items.Add(rate.ToString());
            baudBox.Items.Add(CustomBaudItem);
            baudBox.SelectedIndexChanged += delegate { OnBaudSelected(); };
            connection.Controls.Add(baudBox);
            connectButton.MinimumSize = new Size(100, 30);
            connectButton.Click += delegate { ToggleConnection(); };
            tips.SetToolTip(connectButton, "Connect / disconnect (F5)");
            connection.Controls.Add(connectButton);

            toolbar.Controls.Add(connection);
            toolbar.Controls.Add(actions);

            // Display options on the left, view switch on the right
            var optionsRow = new Panel { Dock = DockStyle.Top, Height = 44, Padding = new Padding(10, 7, 12, 7) };
            var views = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, WrapContents = false, Padding = Padding.Empty };
            plotPointsLabel.Text = "Show last";
            plotPointsLabel.AutoSize = true;
            plotPointsLabel.Margin = new Padding(0, 7, 6, 0);
            plotPointsLabel.Tag = "muted";
            views.Controls.Add(plotPointsLabel);
            foreach (int n in PlotWindowSizes) plotPointsBox.Items.Add(n + " points");
            plotPointsBox.Width = 110;
            plotPointsBox.Margin = new Padding(0, 3, 14, 0);
            plotPointsBox.SelectedIndexChanged += delegate
            {
                if (plotPointsBox.SelectedIndex >= 0) plot.ViewWidth = PlotWindowSizes[plotPointsBox.SelectedIndex];
            };
            tips.SetToolTip(plotPointsBox, "How many readings the graph shows at once");
            views.Controls.Add(plotPointsBox);
            recordButton.MinimumSize = new Size(96, 30);
            recordButton.Margin = new Padding(0, 0, 14, 0);
            recordButton.Click += delegate { ToggleRecording(); };
            tips.SetToolTip(recordButton, "Record readings and save them as a CSV file for Excel or Google Sheets");
            views.Controls.Add(recordButton);
            imageButton.Margin = new Padding(0, 0, 14, 0);
            imageButton.Click += delegate { SavePlotImage(); };
            tips.SetToolTip(imageButton, "Save the graph as a PNG image");
            views.Controls.Add(imageButton);
            views.Controls.SetChildIndex(imageButton, views.Controls.GetChildIndex(recordButton));
            textViewButton.Margin = Padding.Empty;
            textViewButton.MinimumSize = new Size(64, 30);
            textViewButton.Click += delegate { SetView(false); };
            tips.SetToolTip(textViewButton, "Show serial output as text (Ctrl+1)");
            views.Controls.Add(textViewButton);
            plotViewButton.Margin = Padding.Empty;
            plotViewButton.MinimumSize = new Size(64, 30);
            plotViewButton.Click += delegate { SetView(true); };
            tips.SetToolTip(plotViewButton, "Draw numbers as a live graph (Ctrl+2)");
            views.Controls.Add(plotViewButton);

            var options = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 6, 0, 0) };
            optionsRow.Controls.Add(options);
            optionsRow.Controls.Add(views);
            AddOption(options, timestampBox, "Timestamps", "Prefix each line with the time it arrived");
            AddOption(options, autoScrollBox, "Auto-scroll", "Keep the newest output in view");
            AddOption(options, pauseBox, "Pause display", "Freeze the display; incoming data is kept and shown when you resume");
            AddOption(options, hexBox, "Hex view", "Show the raw bytes in hexadecimal, for binary devices");
            pauseBox.CheckedChanged += delegate { if (!pauseBox.Checked) FlushIncoming(); UpdateStatusText(); };
            hexBox.CheckedChanged += delegate { SetHexMode(hexBox.Checked); };

            // Output
            var outputHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 0, 12, 0) };
            BuildFindBar();
            outputFrame.Dock = DockStyle.Fill;
            outputFrame.Padding = new Padding(1);
            outputFrame.UseOutputBackground = true;
            output.Dock = DockStyle.Fill;
            output.HandleCreated += delegate { if (theme != null) NativeMethods.SetScrollBarTheme(output, theme.IsDark); };
            BuildOutputMenu();

            // Graph on top, text below; in text view the graph half is collapsed
            plotSplit.Dock = DockStyle.Fill;
            plotSplit.Orientation = Orientation.Horizontal;
            plotSplit.SplitterWidth = 5;
            plotSplit.Panel1MinSize = 120;
            plotSplit.Panel2MinSize = 50;
            plotSplit.TabStop = false;
            plotSplit.Panel2.Padding = new Padding(9, 7, 1, 1);
            plotSplit.SplitterMoved += delegate
            {
                if (plotMode && plotSplit.Height > 0) plotSplitRatio = (double)plotSplit.SplitterDistance / plotSplit.Height;
            };
            plot.Dock = DockStyle.Fill;
            plot.RangeRequested += delegate { ChoosePlotRange(); };
            plotSplit.Panel1.Controls.Add(plot);
            plotSplit.Panel2.Controls.Add(output);
            outputFrame.Controls.Add(plotSplit);
            outputHost.Controls.Add(outputFrame);
            outputHost.Controls.Add(findBar);

            // Send row
            var sendBar = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 52, ColumnCount = 4, RowCount = 1, Padding = new Padding(12, 10, 6, 8) };
            sendBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            sendBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            sendBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            sendBar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            sendBar.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            sendFrame.Dock = DockStyle.Fill;
            sendFrame.Padding = new Padding(8, 7, 8, 4);
            sendFrame.Margin = new Padding(0, 0, 6, 0);
            sendBox.BorderStyle = BorderStyle.None;
            sendBox.Dock = DockStyle.Fill;
            sendBox.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter) { SendLine(); e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.Up) { BrowseHistory(-1); e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.Down) { BrowseHistory(1); e.SuppressKeyPress = true; }
            };
            NativeMethods.SetPlaceholder(sendBox, "Type a message and press Enter  (↑ ↓ for history)");
            sendFrame.Controls.Add(sendBox);
            sendBar.Controls.Add(sendFrame, 0, 0);
            commandsButton.Margin = new Padding(0, 0, 6, 0);
            commandsButton.Click += delegate { ShowCommandsMenu(); };
            tips.SetToolTip(commandsButton, "Saved commands");
            sendBar.Controls.Add(commandsButton, 1, 0);
            lineEndingBox.Items.AddRange(new object[] { "No line ending", "Newline (LF)", "Carriage return (CR)", "Both (CR+LF)" });
            lineEndingBox.Width = 160;
            lineEndingBox.Margin = new Padding(0, 4, 6, 0);
            tips.SetToolTip(lineEndingBox, "Characters added after each message you send");
            sendBar.Controls.Add(lineEndingBox, 2, 0);
            sendButton.MinimumSize = new Size(72, 30);
            sendButton.Click += delegate { SendLine(); };
            sendBar.Controls.Add(sendButton, 3, 0);

            // Status bar
            var statusBar = new Panel { Dock = DockStyle.Bottom, Height = 30, Padding = new Padding(14, 0, 14, 6), Tag = "surface" };
            var statusLeft = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 6, 0, 0), Tag = "surface" };
            statusDot.Margin = new Padding(0, 4, 8, 0);
            statusLeft.Controls.Add(statusDot);
            statusLabel.AutoSize = true;
            statusLabel.Margin = Padding.Empty;
            statusLeft.Controls.Add(statusLabel);
            updateLink.AutoSize = true;
            updateLink.Margin = new Padding(16, 0, 0, 0);
            updateLink.LinkBehavior = LinkBehavior.HoverUnderline;
            updateLink.Cursor = Cursors.Hand;
            updateLink.Visible = false;
            updateLink.LinkClicked += delegate { if (updateUrl != null) OpenUrl(updateUrl); };
            statusLeft.Controls.Add(updateLink);
            statsLabel.Dock = DockStyle.Right;
            statsLabel.AutoSize = true;
            statsLabel.Padding = new Padding(0, 6, 0, 0);
            statsLabel.Tag = "muted";
            // Credit: "Made by Tejas · @Tjs4002", the handle opens the GitHub profile
            var credit = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, WrapContents = false, Padding = new Padding(0, 6, 0, 0), Tag = "surface" };
            creditSeparator.Text = "|";
            creditSeparator.AutoSize = true;
            creditSeparator.Margin = new Padding(14, 0, 14, 0);
            creditSeparator.Tag = "muted";
            credit.Controls.Add(creditSeparator);
            var madeBy = new Label { Text = "Made by " + AppInfo.Author + "  ·", AutoSize = true, Margin = new Padding(0), Tag = "muted" };
            credit.Controls.Add(madeBy);
            creditLink.Text = "@" + AppInfo.GitHubUser;
            creditLink.AutoSize = true;
            creditLink.Margin = new Padding(2, 0, 0, 0);
            creditLink.LinkBehavior = LinkBehavior.HoverUnderline;
            creditLink.Cursor = Cursors.Hand;
            creditLink.LinkClicked += delegate { OpenUrl(AppInfo.ProfileUrl); };
            tips.SetToolTip(creditLink, "Open " + AppInfo.ProfileUrl.Replace("https://", "") + " on GitHub");
            credit.Controls.Add(creditLink);

            statusBar.Controls.Add(statusLeft);
            statusBar.Controls.Add(statsLabel);
            statusBar.Controls.Add(credit);

            // Last added docks outermost
            Controls.Add(outputHost);
            Controls.Add(optionsRow);
            Controls.Add(toolbar);
            Controls.Add(sendBar);
            Controls.Add(statusBar);
        }

        private void BuildSettingsMenu()
        {
            reconnectItem.CheckOnClick = true;
            reconnectItem.ToolTipText = "Reconnect automatically when the board is unplugged and plugged back in";
            reconnectItem.CheckedChanged += delegate { autoReconnect = reconnectItem.Checked; };
            highlightItem.CheckOnClick = true;
            highlightItem.ToolTipText = "Show error lines in red, warnings in amber and debug lines dimmed";
            highlightItem.CheckedChanged += delegate { highlight = highlightItem.Checked; };
            updateStartupItem.CheckOnClick = true;
            updateStartupItem.ToolTipText = "Ask GitHub for a newer version at most twice a day";
            updateNowItem.Click += delegate { CheckForUpdates(true); };
            sessionLogItem.CheckOnClick = true;
            sessionLogItem.ToolTipText = "Write everything you see to a file in Documents\\SerialScope\\Logs, one file per connection";
            sessionLogItem.CheckedChanged += delegate
            {
                if (sessionLogItem.Checked && port != null) StartSessionLog();
                else if (!sessionLogItem.Checked) StopSessionLog();
            };
            openLogsItem.Click += delegate
            {
                try { Directory.CreateDirectory(LogsFolder); System.Diagnostics.Process.Start("explorer.exe", "\"" + LogsFolder + "\""); }
                catch (Exception ex) { ShowMessage("Could not open the logs folder.\n\n" + ex.Message, MessageBoxIcon.Warning); }
            };
            settingsMenu.Items.Add(reconnectItem);
            settingsMenu.Items.Add(highlightItem);
            settingsMenu.Items.Add(new ToolStripSeparator());
            settingsMenu.Items.Add(sessionLogItem);
            settingsMenu.Items.Add(openLogsItem);
            settingsMenu.Items.Add(new ToolStripSeparator());
            settingsMenu.Items.Add(updateStartupItem);
            settingsMenu.Items.Add(updateNowItem);
            settingsMenu.ShowItemToolTips = true;
        }

        private void BuildOutputMenu()
        {
            var copy = new ToolStripMenuItem("Copy", null, delegate { output.Copy(); }) { ShortcutKeyDisplayString = "Ctrl+C" };
            var selectAll = new ToolStripMenuItem("Select all", null, delegate { output.SelectAll(); }) { ShortcutKeyDisplayString = "Ctrl+A" };
            var find = new ToolStripMenuItem("Find…", null, delegate { ShowFindBar(); }) { ShortcutKeyDisplayString = "Ctrl+F" };
            var clear = new ToolStripMenuItem("Clear", null, delegate { ClearOutput(); }) { ShortcutKeyDisplayString = "Ctrl+L" };
            outputMenu.Items.AddRange(new ToolStripItem[] { copy, selectAll, new ToolStripSeparator(), find, new ToolStripSeparator(), clear });
            outputMenu.Opening += delegate { copy.Enabled = output.SelectionLength > 0; };
            output.ContextMenuStrip = outputMenu;
        }

        private void BuildFindBar()
        {
            findBar.Dock = DockStyle.Top;
            findBar.Height = 40;
            findBar.Padding = new Padding(0, 0, 0, 6);
            findBar.Visible = false;

            var row = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = Padding.Empty };
            var frame = new BorderPanel { Width = 280, Height = 30, Padding = new Padding(8, 7, 8, 4), Margin = new Padding(0, 0, 8, 0) };
            findBox.BorderStyle = BorderStyle.None;
            findBox.Dock = DockStyle.Fill;
            NativeMethods.SetPlaceholder(findBox, "Find in output");
            findBox.TextChanged += delegate { RunSearch(true); };
            findBox.KeyDown += delegate(object s, KeyEventArgs e)
            {
                if (e.KeyCode == Keys.Enter) { NextMatch(!e.Shift); e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.Escape) { HideFindBar(); e.SuppressKeyPress = true; }
            };
            frame.Controls.Add(findBox);
            row.Controls.Add(frame);

            var prev = new FlatButton("", ButtonKind.Icon);
            prev.Click += delegate { NextMatch(false); };
            tips.SetToolTip(prev, "Previous match (Shift+Enter)");
            row.Controls.Add(prev);
            var next = new FlatButton("", ButtonKind.Icon);
            next.Click += delegate { NextMatch(true); };
            tips.SetToolTip(next, "Next match (Enter)");
            row.Controls.Add(next);

            findCount.AutoSize = true;
            findCount.Margin = new Padding(6, 7, 14, 0);
            findCount.Tag = "muted";
            row.Controls.Add(findCount);

            findCaseBox.Text = "Match case";
            findCaseBox.AutoSize = true;
            findCaseBox.Margin = new Padding(0, 6, 14, 0);
            findCaseBox.CheckedChanged += delegate { RunSearch(true); };
            row.Controls.Add(findCaseBox);

            var close = new FlatButton("", ButtonKind.Icon);
            close.Click += delegate { HideFindBar(); };
            tips.SetToolTip(close, "Close (Esc)");
            row.Controls.Add(close);

            findBar.Controls.Add(row);
        }

        private void AddIconButton(Control parent, FlatButton button, string tip, EventHandler onClick)
        {
            button.Click += onClick;
            tips.SetToolTip(button, tip);
            parent.Controls.Add(button);
        }

        private void AddOption(Control parent, FlatCheckBox box, string text, string tip)
        {
            box.Text = text;
            box.AutoSize = true;
            box.Margin = new Padding(0, 0, 18, 0);
            box.Cursor = Cursors.Hand;
            tips.SetToolTip(box, tip);
            parent.Controls.Add(box);
        }

        private static Label MakeLabel(string text, bool muted)
        {
            return new Label
            {
                Text = text,
                AutoSize = true,
                Margin = new Padding(0, 7, 6, 0),
                Tag = muted ? "muted" : null
            };
        }

        // ------------------------------------------------------------------ theme & preferences

        private void LoadPreferences()
        {
            SelectBaud(settings.GetInt("baud", 115200));
            timestampBox.Checked = settings.GetBool("timestamps", false);
            autoScrollBox.Checked = settings.GetBool("autoscroll", true);
            reconnectItem.Checked = settings.GetBool("reconnect", true);
            highlightItem.Checked = settings.GetBool("highlight", true);
            hexBox.Checked = settings.GetBool("hex", false);
            updateStartupItem.Checked = settings.GetBool("checkUpdates", true);
            sessionLogItem.Checked = settings.GetBool("sessionLog", false);
            sendHistory.AddRange(SplitList(settings.Get("history", "")));
            savedCommands.AddRange(SplitList(settings.Get("commands", "")));
            lineEndingBox.SelectedIndex = Math.Max(0, Math.Min(3, settings.GetInt("lineEnding", 1)));
            SetOutputFont(settings.GetInt("fontSize", (int)DefaultFontSize));
            int points = Array.IndexOf(PlotWindowSizes, settings.GetInt("plotPoints", 500));
            plotPointsBox.SelectedIndex = points >= 0 ? points : 2;
            plotSplitRatio = settings.GetInt("plotSplit", 70) / 100.0;
            SetView(settings.Get("view", "text") == "plot");
            if (settings.GetBool("maximized", false)) WindowState = FormWindowState.Maximized;
            SetTheme(Theme.FromName(settings.Get("theme", "dark")));
        }

        private void SavePreferences()
        {
            settings.Set("baud", SelectedBaud());
            settings.Set("timestamps", timestampBox.Checked);
            settings.Set("autoscroll", autoScrollBox.Checked);
            settings.Set("reconnect", autoReconnect);
            settings.Set("highlight", highlight);
            settings.Set("hex", hexMode);
            settings.Set("checkUpdates", updateStartupItem.Checked);
            settings.Set("sessionLog", sessionLogItem.Checked);
            settings.Set("history", string.Join(ListSeparator.ToString(), sendHistory.ToArray()));
            settings.Set("commands", string.Join(ListSeparator.ToString(), savedCommands.ToArray()));
            settings.Set("lineEnding", lineEndingBox.SelectedIndex);
            settings.Set("fontSize", (int)output.Font.Size);
            settings.Set("view", plotMode ? "plot" : "text");
            settings.Set("plotPoints", plot.ViewWidth);
            settings.Set("plotSplit", (int)Math.Round(plotSplitRatio * 100));
            settings.Set("theme", theme.Name);
            settings.Set("maximized", WindowState == FormWindowState.Maximized);
            if (WindowState == FormWindowState.Normal)
            {
                settings.Set("width", Width);
                settings.Set("height", Height);
            }
            settings.Save();
        }

        private void SetTheme(Theme t)
        {
            theme = t;
            BackColor = t.Window;
            ForeColor = t.Text;
            t.Apply(this);
            themeButton.Text = t.IsDark ? "" : "";   // sun in dark mode, moon in light mode
            Theme.ApplyTitleBar(this, t.IsDark);
            NativeMethods.SetScrollBarTheme(output, t.IsDark);
            ThemedMenuRenderer.Apply(settingsMenu, t);
            ThemedMenuRenderer.Apply(outputMenu, t);
            creditLink.LinkColor = t.Success;
            creditLink.ActiveLinkColor = t.Text;
            creditLink.VisitedLinkColor = t.Success;
            updateLink.LinkColor = updateLink.VisitedLinkColor = t.Success;
            updateLink.ActiveLinkColor = t.Text;
            plotSplit.BackColor = t.Border;   // the divider
            plotSplit.Panel1.BackColor = t.OutputBack;
            plotSplit.Panel2.BackColor = t.OutputBack;
            Invalidate(true);
        }

        private void ToggleTheme()
        {
            SetTheme(theme.IsDark ? Theme.Light : Theme.Dark);
        }

        private void SetOutputFont(float size)
        {
            size = Math.Max(7F, Math.Min(28F, size));
            output.Font = new Font("Consolas", size);
            sendBox.Font = new Font("Consolas", DefaultFontSize);
            findBox.Font = new Font("Segoe UI", 9.5F);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (theme != null) Theme.ApplyTitleBar(this, theme.IsDark);
        }

        // ------------------------------------------------------------------ ports

        private void RefreshPorts()
        {
            string previous = SelectedPortName() ?? settings.Get("port", null);
            List<PortInfo> ports = PortInfo.List();
            knownPorts = PortInfo.GetNames();

            portBox.BeginUpdate();
            portBox.Items.Clear();
            foreach (PortInfo p in ports) portBox.Items.Add(p);
            portBox.EndUpdate();

            for (int i = 0; i < portBox.Items.Count; i++)
            {
                if (((PortInfo)portBox.Items[i]).Name == previous) { portBox.SelectedIndex = i; break; }
            }
            if (portBox.SelectedIndex < 0 && portBox.Items.Count > 0)
                portBox.SelectedIndex = portBox.Items.Count - 1;   // the newest port is usually the board

            UpdateStatusText();
        }

        // ------------------------------------------------------------------ baud rate

        private const string CustomBaudItem = "Custom…";
        private int lastBaudIndex = -1;

        private int SelectedBaud()
        {
            int baud;
            return int.TryParse(baudBox.SelectedItem as string, out baud) ? baud : 115200;
        }

        // Selects a rate, adding it to the list (in order) if it is not a standard one
        private void SelectBaud(int rate)
        {
            string text = rate.ToString();
            int index = baudBox.Items.IndexOf(text);
            if (index < 0)
            {
                index = baudBox.Items.Count - 1;   // before "Custom..."
                for (int i = 0; i < baudBox.Items.Count - 1; i++)
                {
                    if (int.Parse((string)baudBox.Items[i]) > rate) { index = i; break; }
                }
                baudBox.Items.Insert(index, text);
            }
            baudBox.SelectedIndex = index;
        }

        private void OnBaudSelected()
        {
            if ((baudBox.SelectedItem as string) != CustomBaudItem)
            {
                lastBaudIndex = baudBox.SelectedIndex;
                return;
            }

            using (var prompt = new CustomBaudForm(theme))
            {
                if (prompt.ShowDialog(this) == DialogResult.OK) SelectBaud(prompt.BaudRate);
                else if (lastBaudIndex >= 0) baudBox.SelectedIndex = lastBaudIndex;
            }
        }

        private string SelectedPortName()
        {
            var item = portBox.SelectedItem as PortInfo;
            return item == null ? null : item.Name;
        }

        // Runs every second: notices plugged / unplugged devices and handles auto-reconnect
        private void WatchPorts()
        {
            string[] names = PortInfo.GetNames();
            bool changed = string.Join(",", names) != string.Join(",", knownPorts);

            if (reconnectPort != null)
            {
                knownPorts = names;
                if (Array.IndexOf(names, reconnectPort) >= 0 && OpenPort(reconnectPort, reconnectBaud, false))
                {
                    WriteSystemLine("Reconnected to " + reconnectPort);
                    reconnectPort = null;
                    UpdateUiState();
                }
                return;
            }

            if (port == null && changed) RefreshPorts();
            else knownPorts = names;
        }

        // ------------------------------------------------------------------ connection

        private void ToggleConnection()
        {
            if (reconnectPort != null)
            {
                reconnectPort = null;
                WriteSystemLine("Stopped waiting for the board");
                StopSessionLog();
                UpdateUiState();
            }
            else if (port != null)
            {
                ClosePort();
                WriteSystemLine("Disconnected");
                StopSessionLog();
                UpdateUiState();
            }
            else
            {
                Connect();
            }
        }

        private void Connect()
        {
            string name = SelectedPortName();
            if (name == null)
            {
                ShowMessage("No serial port selected. Plug in your board, then pick its port from the list.", MessageBoxIcon.Information);
                return;
            }

            int baud = SelectedBaud();

            if (OpenPort(name, baud, true))
            {
                Interlocked.Exchange(ref bytesReceived, 0);
                bytesSent = 0;
                settings.Set("port", name);
                settings.Set("baud", baud);
                if (sessionLogItem.Checked) StartSessionLog();
                WriteSystemLine("Connected to " + name + " at " + baud + " baud");
                UpdateUiState();
            }
        }

        private bool OpenPort(string name, int baud, bool showErrors)
        {
            var sp = new SerialPort(name, baud, Parity.None, 8, StopBits.One)
            {
                // Both control lines low, so opening the port does not reset boards with auto-reset circuits
                DtrEnable = false,
                RtsEnable = false,
                ReadTimeout = 500,
                WriteTimeout = 1000
            };
            sp.DataReceived += OnDataReceived;
            sp.ErrorReceived += delegate { portLost = true; };

            try
            {
                sp.Open();
            }
            catch (UnauthorizedAccessException)
            {
                sp.Dispose();
                if (showErrors)
                    ShowMessage(name + " is being used by another program.\n\nClose any other serial monitor, or wait for an upload to finish, then try again.", MessageBoxIcon.Warning);
                return false;
            }
            catch (Exception ex)
            {
                sp.Dispose();
                if (showErrors) ShowMessage("Could not open " + name + ".\n\n" + ex.Message, MessageBoxIcon.Warning);
                return false;
            }

            // Drop anything the driver buffered before we opened the port. It is stale, and after a
            // firmware upload it was received at the uploader's baud rate, so it would show as garbage.
            try { sp.DiscardInBuffer(); } catch (Exception) { }

            port = sp;
            portLost = false;
            decoder.Reset();
            return true;
        }

        private void ClosePort()
        {
            SerialPort sp = port;
            port = null;
            if (sp == null) return;
            sp.DataReceived -= OnDataReceived;
            // Closing can hang on some USB drivers after the device disappears, so do it off the UI thread
            ThreadPool.QueueUserWorkItem(delegate
            {
                try { sp.Close(); } catch (Exception) { }
                try { sp.Dispose(); } catch (Exception) { }
            });
        }

        // Raw bytes are queued here (serial thread) and formatted on the UI thread
        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                var sp = (SerialPort)sender;
                int available = sp.BytesToRead;
                if (available <= 0) return;
                var buffer = new byte[available];
                int read = sp.Read(buffer, 0, available);
                if (read <= 0) return;
                if (read < available) Array.Resize(ref buffer, read);
                incoming.Enqueue(buffer);
                Interlocked.Add(ref bytesReceived, read);
            }
            catch (Exception)
            {
                portLost = true;   // cable unplugged or the device reset its USB connection
            }
        }

        private void HandlePortLost()
        {
            string name = port.PortName;
            int baud = port.BaudRate;
            DrainIncoming();
            ClosePort();

            if (autoReconnect)
            {
                reconnectPort = name;
                reconnectBaud = baud;
                WriteSystemLine("Lost connection to " + name + " - waiting for it to come back");
            }
            else
            {
                WriteSystemLine("Lost connection to " + name);
            }
            UpdateUiState();
        }

        private void SendLine()
        {
            if (port == null) return;
            if (SendText(sendBox.Text)) sendBox.Clear();
        }

        // Writes a message plus the chosen line ending and remembers it in the history
        private bool SendText(string message)
        {
            if (port == null) return false;
            string text = message + LineEndings[Math.Max(0, lineEndingBox.SelectedIndex)];
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(text);
                port.Write(bytes, 0, bytes.Length);
                bytesSent += bytes.Length;
                AddToHistory(message);
                UpdateStatsText();
                return true;
            }
            catch (Exception ex)
            {
                WriteSystemLine("Send failed: " + ex.Message);
                return false;
            }
        }

        // ------------------------------------------------------------------ send history & saved commands

        private void AddToHistory(string message)
        {
            historyIndex = -1;
            if (message.Length == 0) return;
            sendHistory.Remove(message);   // keep one copy, as the newest
            sendHistory.Add(message);
            if (sendHistory.Count > MaxHistory) sendHistory.RemoveAt(0);
        }

        // step -1 = older, +1 = newer
        private void BrowseHistory(int step)
        {
            if (sendHistory.Count == 0) return;
            if (historyIndex == -1)
            {
                if (step > 0) return;
                historyDraft = sendBox.Text;
                historyIndex = sendHistory.Count - 1;
            }
            else
            {
                historyIndex += step;
                if (historyIndex < 0) historyIndex = 0;
                if (historyIndex >= sendHistory.Count)
                {
                    historyIndex = -1;
                    sendBox.Text = historyDraft;
                    sendBox.SelectionStart = sendBox.TextLength;
                    return;
                }
            }
            sendBox.Text = sendHistory[historyIndex];
            sendBox.SelectionStart = sendBox.TextLength;
        }

        private void ShowCommandsMenu()
        {
            commandsMenu.Items.Clear();
            if (savedCommands.Count == 0)
            {
                commandsMenu.Items.Add(new ToolStripMenuItem("No saved commands yet") { Enabled = false });
            }
            foreach (string command in savedCommands)
            {
                string c = command;
                var item = new ToolStripMenuItem(Shorten(c, 48), null, delegate { UseCommand(c); });
                item.ToolTipText = port != null ? "Send \"" + c + "\"" : "Put \"" + c + "\" in the send box";
                commandsMenu.Items.Add(item);
            }
            commandsMenu.Items.Add(new ToolStripSeparator());

            string current = sendBox.Text.Trim();
            var save = new ToolStripMenuItem("Save current message", null, delegate
            {
                savedCommands.Add(current);
                statusLabel.Text = "Saved command \"" + Shorten(current, 40) + "\"";
            });
            save.Enabled = current.Length > 0 && !savedCommands.Contains(current);
            if (current.Length == 0) save.ToolTipText = "Type a message in the send box first";
            commandsMenu.Items.Add(save);

            var remove = new ToolStripMenuItem("Remove");
            foreach (string command in savedCommands)
            {
                string c = command;
                remove.DropDownItems.Add(new ToolStripMenuItem(Shorten(c, 48), null, delegate { savedCommands.Remove(c); }));
            }
            remove.Enabled = savedCommands.Count > 0;
            commandsMenu.Items.Add(remove);

            commandsMenu.ShowItemToolTips = true;
            ThemedMenuRenderer.Apply(commandsMenu, theme);
            commandsMenu.Show(commandsButton, new Point(0, -commandsMenu.PreferredSize.Height - 2));
        }

        private void UseCommand(string command)
        {
            if (port != null)
            {
                SendText(command);
            }
            else
            {
                sendBox.Text = command;
                sendBox.SelectionStart = sendBox.TextLength;
                sendBox.Focus();
            }
        }

        private static string Shorten(string text, int max)
        {
            return text.Length <= max ? text : text.Substring(0, max - 1) + "…";
        }

        private static List<string> SplitList(string value)
        {
            var list = new List<string>();
            if (string.IsNullOrEmpty(value)) return list;
            foreach (string s in value.Split(ListSeparator)) if (s.Length > 0) list.Add(s);
            return list;
        }

        // ------------------------------------------------------------------ output

        private void FlushIncoming()
        {
            if (port != null && (portLost || !port.IsOpen))
            {
                HandlePortLost();
                return;
            }

            UpdateStatsText();
            UpdateRecordingUi();
            if (pauseBox.Checked || incoming.IsEmpty) return;
            DrainIncoming();
        }

        // Follow new output unless auto-scroll is off or the user is searching
        private bool FollowOutput
        {
            get { return autoScrollBox.Checked && !findBar.Visible; }
        }

        private void DrainIncoming()
        {
            if (incoming.IsEmpty) return;
            output.BeginAppend(FollowOutput);
            byte[] chunk;
            while (incoming.TryDequeue(out chunk))
            {
                if (charBuffer.Length < chunk.Length + 4) charBuffer = new char[chunk.Length + 4];
                int count = decoder.GetChars(chunk, 0, chunk.Length, charBuffer, 0);
                string text = new string(charBuffer, 0, count);
                FeedPlot(text);
                if (hexMode) FormatHex(chunk);
                else FormatText(text);
            }

            // Show the unfinished end of the current line now; it is coloured when the line completes
            if (lineText.Length > 0)
            {
                Emit(lineText.ToString(), output.TextColor);
                lineText.Length = 0;
                lineHasEarlierPart = true;
            }
            FlushPending();
            TrimOutput();
            output.EndAppend();
            if (plotMode) plot.RefreshIfChanged();
        }

        private void FormatText(string text)
        {
            foreach (char c in text)
            {
                if (c == '\r' || c == '\0') continue;
                if (atLineStart)
                {
                    if (timestampBox.Checked) Emit(DateTime.Now.ToString("HH:mm:ss.fff") + "   ", output.MutedColor);
                    lineContentStart = output.TextLength + pending.Length;
                    lineHasEarlierPart = false;
                    rawLine.Length = 0;
                    atLineStart = false;
                }

                if (c == '\n')
                {
                    lineText.Append('\n');
                    LineKind kind = highlight ? LogHighlighter.Classify(rawLine.ToString()) : LineKind.Normal;
                    if (!lineHasEarlierPart)
                    {
                        Emit(lineText.ToString(), output.ColorFor(kind));
                    }
                    else
                    {
                        // Part of this line is already on screen: add the rest, then colour the whole line
                        Emit(lineText.ToString(), output.TextColor);
                        if (kind != LineKind.Normal)
                        {
                            FlushPending();
                            output.ColorRange(lineContentStart, output.TextLength - lineContentStart, output.ColorFor(kind));
                        }
                    }
                    lineText.Length = 0;
                    atLineStart = true;
                }
                else
                {
                    lineText.Append(c);
                    if (rawLine.Length < 4096) rawLine.Append(c);
                }
            }
        }

        // 16 bytes per line: offset (or time), hex bytes, then the printable characters
        private void FormatHex(byte[] data)
        {
            foreach (byte b in data)
            {
                if (hexCount == 0)
                {
                    string prefix = timestampBox.Checked ? DateTime.Now.ToString("HH:mm:ss.fff") : hexOffset.ToString("X8");
                    Emit(prefix + "   ", output.MutedColor);
                }
                Emit(b.ToString("X2") + (hexCount == 7 ? "  " : " "), output.TextColor);
                hexAscii.Append(b >= 32 && b < 127 ? (char)b : '.');
                hexCount++;
                hexOffset++;
                if (hexCount == 16 || b == 0x0A) EndHexLine();
            }
        }

        private void EndHexLine()
        {
            if (hexCount == 0) return;
            int missing = 16 - hexCount;
            string pad = new string(' ', missing * 3 + (hexCount <= 7 ? 1 : 0));
            Emit(pad + "  " + hexAscii + "\n", output.MutedColor);
            hexAscii.Length = 0;
            hexCount = 0;
        }

        private void SetHexMode(bool on)
        {
            if (on == hexMode) return;
            DrainIncoming();
            output.BeginAppend(FollowOutput);
            if (on) EndTextLine();
            else EndHexLine();
            hexMode = on;
            hexOffset = 0;
            FlushPending();
            output.EndAppend();
        }

        // Finishes a partly received text line so the next output starts on a new line
        private void EndTextLine()
        {
            if (atLineStart && lineText.Length == 0) return;
            Emit(lineText.ToString() + "\n", output.TextColor);
            lineText.Length = 0;
            rawLine.Length = 0;
            atLineStart = true;
        }

        // Queues text in a colour; consecutive text in the same colour is appended in one go
        private void Emit(string text, Color color)
        {
            if (text.Length == 0) return;
            if (pending.Length > 0 && color != pendingColor) FlushPending();
            pendingColor = color;
            pending.Append(text);
        }

        private void FlushPending()
        {
            if (pending.Length == 0) return;
            string text = pending.ToString();
            output.Append(text, pendingColor);
            pending.Length = 0;
            outputVersion++;
            if (sessionLog != null) WriteSessionLog(text);
        }

        // ------------------------------------------------------------------ session log

        private void StartSessionLog()
        {
            if (sessionLog != null || port == null) return;
            try
            {
                Directory.CreateDirectory(LogsFolder);
                sessionLogPath = Path.Combine(LogsFolder, port.PortName + "-" + DateTime.Now.ToString("yyyy-MM-dd-HHmmss") + ".txt");
                sessionLog = new StreamWriter(sessionLogPath, false, new UTF8Encoding(false));
                UpdateStatusText();
            }
            catch (Exception ex)
            {
                sessionLog = null;
                sessionLogItem.Checked = false;
                ShowMessage("Could not create the session log.\n\n" + ex.Message, MessageBoxIcon.Warning);
            }
        }

        private void StopSessionLog()
        {
            if (sessionLog == null) return;
            try { sessionLog.Dispose(); } catch (Exception) { }
            sessionLog = null;
            UpdateStatusText();
        }

        private void WriteSessionLog(string text)
        {
            try
            {
                sessionLog.Write(text.Replace("\n", "\r\n"));
                sessionLog.Flush();
            }
            catch (Exception ex)
            {
                StopSessionLog();
                sessionLogItem.Checked = false;
                WriteSystemLine("Session log stopped: " + ex.Message);
            }
        }

        private void TrimOutput()
        {
            int removed = output.Trim(MaxOutputChars, TrimToChars);
            if (removed > 0) lineContentStart = Math.Max(0, lineContentStart - removed);
        }

        private void WriteSystemLine(string message)
        {
            DrainIncoming();
            output.BeginAppend(FollowOutput);
            if (hexMode) EndHexLine();
            else EndTextLine();
            Emit("—— " + message + " ——\n", output.MutedColor);
            FlushPending();
            TrimOutput();
            output.EndAppend();
            UpdateStatusText();
        }

        private void ClearOutput()
        {
            output.Clear();
            pending.Length = 0;
            lineText.Length = 0;
            rawLine.Length = 0;
            atLineStart = true;
            lineHasEarlierPart = false;
            lineContentStart = 0;
            hexCount = 0;
            hexAscii.Length = 0;
            outputVersion++;
            plot.Clear();
            plotLine.Length = 0;
            if (findBar.Visible) RunSearch(true);
        }

        private void SaveLog()
        {
            using (var dialog = new SaveFileDialog())
            {
                dialog.Title = "Save log";
                dialog.Filter = "Text file (*.txt)|*.txt|Log file (*.log)|*.log|All files (*.*)|*.*";
                dialog.FileName = "serial-" + DateTime.Now.ToString("yyyy-MM-dd-HHmmss") + ".txt";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    File.WriteAllText(dialog.FileName, output.Text.Replace("\n", "\r\n"), Encoding.UTF8);
                    statusLabel.Text = "Saved log to " + Path.GetFileName(dialog.FileName);
                }
                catch (Exception ex)
                {
                    ShowMessage("Could not save the log.\n\n" + ex.Message, MessageBoxIcon.Warning);
                }
            }
        }

        // ------------------------------------------------------------------ find

        private void ShowFindBar()
        {
            findBar.Visible = true;
            if (output.SelectionLength > 0 && output.SelectionLength < 100 && output.SelectedText.IndexOf('\n') < 0)
                findBox.Text = output.SelectedText;
            findBox.Focus();
            findBox.SelectAll();
            RunSearch(true);
        }

        private void HideFindBar()
        {
            findBar.Visible = false;
            output.ClearHighlights();
            findMatches.Clear();
            findIndex = -1;
            if (autoScrollBox.Checked) output.ScrollToEnd();
            output.Focus();
        }

        // Highlights all matches; jumpToNearest picks the first match at or after the current position
        private void RunSearch(bool jumpToNearest)
        {
            string term = findBox.Text;
            findMatches = output.HighlightAll(term, findCaseBox.Checked, MaxSearchMatches);
            findVersion = outputVersion;
            findIndex = -1;
            if (findMatches.Count > 0 && jumpToNearest)
            {
                findIndex = findMatches.Count - 1;   // newest match by default, like scrolling up from the bottom
                output.ShowMatch(findMatches[findIndex], term.Length);
            }
            UpdateFindCount();
        }

        private void NextMatch(bool forward)
        {
            if (findVersion != outputVersion) RunSearch(false);   // new output arrived since the last search
            if (findMatches.Count == 0) { UpdateFindCount(); return; }
            if (findIndex < 0) findIndex = forward ? 0 : findMatches.Count - 1;
            else findIndex = (findIndex + (forward ? 1 : -1) + findMatches.Count) % findMatches.Count;
            output.ShowMatch(findMatches[findIndex], findBox.Text.Length);
            UpdateFindCount();
        }

        private void UpdateFindCount()
        {
            if (findBox.Text.Length == 0) findCount.Text = "";
            else if (findMatches.Count == 0) findCount.Text = "No matches";
            else findCount.Text = (findIndex + 1) + " of " + findMatches.Count + (findMatches.Count >= MaxSearchMatches ? "+" : "");
        }

        // ------------------------------------------------------------------ plotter

        // The plotter always receives data, so switching views shows the full history
        private void FeedPlot(string chunk)
        {
            foreach (char c in chunk)
            {
                if (c == '\n')
                {
                    if (plot.AddLine(plotLine.ToString(), parsedNames, parsedValues) && recorder.IsRecording)
                        recorder.Add(parsedNames, parsedValues);
                    plotLine.Length = 0;
                }
                else if (c != '\r' && plotLine.Length < 1024)
                {
                    plotLine.Append(c);
                }
            }
        }

        private void SavePlotImage()
        {
            using (var dialog = new SaveFileDialog())
            {
                dialog.Title = "Save graph image";
                dialog.Filter = "PNG image (*.png)|*.png";
                dialog.FileName = "graph-" + DateTime.Now.ToString("yyyy-MM-dd-HHmmss") + ".png";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    plot.SaveImage(dialog.FileName);
                    statusLabel.Text = "Saved graph to " + Path.GetFileName(dialog.FileName);
                }
                catch (Exception ex)
                {
                    ShowMessage("Could not save the image.\n\n" + ex.Message, MessageBoxIcon.Warning);
                }
            }
        }

        private void ChoosePlotRange()
        {
            using (var dialog = new RangeForm(theme, plot.HasFixedRange, plot.FixedMin, plot.FixedMax))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                if (dialog.UseAutomatic) plot.ClearFixedRange();
                else plot.SetFixedRange(dialog.Minimum, dialog.Maximum);
            }
        }

        private void SetView(bool showPlot)
        {
            plotMode = showPlot;
            plotSplit.Panel1Collapsed = !showPlot;
            if (showPlot) ApplySplitRatio();
            textViewButton.Kind = showPlot ? ButtonKind.Normal : ButtonKind.Success;
            plotViewButton.Kind = showPlot ? ButtonKind.Success : ButtonKind.Normal;
            plotPointsLabel.Visible = showPlot;
            plotPointsBox.Visible = showPlot;
            recordButton.Visible = showPlot || recorder.IsRecording;
            imageButton.Visible = showPlot;
            if (showPlot) plot.Invalidate();
        }

        private void ApplySplitRatio()
        {
            int h = plotSplit.Height;
            if (h < plotSplit.Panel1MinSize + plotSplit.Panel2MinSize + plotSplit.SplitterWidth) return;
            int distance = (int)(h * Math.Max(0.2, Math.Min(0.9, plotSplitRatio)));
            distance = Math.Max(plotSplit.Panel1MinSize, Math.Min(h - plotSplit.Panel2MinSize - plotSplit.SplitterWidth, distance));
            plotSplit.SplitterDistance = distance;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            if (plotMode) ApplySplitRatio();   // the real height is only known once the window is shown

            // A newer version seen on an earlier check is shown straight away
            string known = settings.Get("latestVersion", null);
            if (known != null && UpdateChecker.IsNewer(known, AppInfo.Version))
                ShowUpdateAvailable(known, settings.Get("latestUrl", AppInfo.RepoUrl + "/releases/latest"));

            long last;
            long.TryParse(settings.Get("lastUpdateCheck", "0"), out last);
            if (updateStartupItem.Checked && DateTime.UtcNow - new DateTime(Math.Max(0, last), DateTimeKind.Utc) > TimeSpan.FromHours(12))
                CheckForUpdates(false);
        }

        // ------------------------------------------------------------------ updates

        private void CheckForUpdates(bool manual)
        {
            updateNowItem.Enabled = false;
            UpdateChecker.CheckAsync(this, delegate(UpdateChecker.Result r)
            {
                updateNowItem.Enabled = true;
                if (!r.Success)
                {
                    if (manual) ShowMessage("Couldn't check for updates.\n\n" + r.Error, MessageBoxIcon.Warning);
                    return;
                }

                settings.Set("lastUpdateCheck", DateTime.UtcNow.Ticks.ToString());
                settings.Set("latestVersion", r.LatestVersion);
                settings.Set("latestUrl", r.ReleaseUrl);
                settings.Save();

                if (UpdateChecker.IsNewer(r.LatestVersion, AppInfo.Version))
                {
                    ShowUpdateAvailable(r.LatestVersion, r.ReleaseUrl);
                    if (manual && MessageBox.Show(this, "SerialScope " + r.LatestVersion + " is available (you have " + AppInfo.Version + ").\n\nOpen the download page?",
                            AppInfo.Name, MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                        OpenUrl(r.ReleaseUrl);
                }
                else
                {
                    updateLink.Visible = false;
                    if (manual) ShowMessage("You're up to date. SerialScope " + AppInfo.Version + " is the latest version.", MessageBoxIcon.Information);
                }
            });
        }

        private void ShowUpdateAvailable(string version, string url)
        {
            updateUrl = url;
            updateLink.Text = "Update available: v" + version;
            tips.SetToolTip(updateLink, "Open the download page for SerialScope " + version);
            updateLink.Visible = true;
        }

        // ------------------------------------------------------------------ recording

        private void ToggleRecording()
        {
            if (!recorder.IsRecording)
            {
                recorder.Start();
                recordButton.Kind = ButtonKind.Danger;
                UpdateRecordingUi();
                return;
            }

            recorder.Stop();
            SaveRecording();
        }

        private void SaveRecording()
        {
            recordButton.Kind = ButtonKind.Normal;
            recordButton.Text = "●  Record";
            recordButton.Visible = plotMode;
            UpdateStatusText();

            if (recorder.RowCount == 0)
            {
                ShowMessage("Nothing was recorded. The recording only captures lines of numbers, such as \"23.5\" or \"temp:23.5,hum:41\".", MessageBoxIcon.Information);
                recorder.Discard();
                return;
            }

            while (true)
            {
                using (var dialog = new SaveFileDialog())
                {
                    dialog.Title = "Save recording";
                    dialog.Filter = "CSV file (*.csv)|*.csv|All files (*.*)|*.*";
                    dialog.FileName = "recording-" + DateTime.Now.ToString("yyyy-MM-dd-HHmmss") + ".csv";
                    if (dialog.ShowDialog(this) == DialogResult.OK)
                    {
                        try
                        {
                            recorder.Save(dialog.FileName);
                            statusLabel.Text = "Saved " + recorder.RowCount + " readings to " + Path.GetFileName(dialog.FileName);
                            recorder.Discard();
                            return;
                        }
                        catch (Exception ex)
                        {
                            ShowMessage("Could not save the recording.\n\n" + ex.Message, MessageBoxIcon.Warning);
                            continue;
                        }
                    }
                }

                var answer = MessageBox.Show(this, "Discard the recording of " + recorder.RowCount + " readings?",
                    AppInfo.Name, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
                if (answer == DialogResult.Yes)
                {
                    recorder.Discard();
                    return;
                }
            }
        }

        private void UpdateRecordingUi()
        {
            if (!recorder.IsRecording) return;
            if (recorder.IsFull)
            {
                recorder.Stop();
                ShowMessage("The recording reached " + Recorder.MaxRows.ToString("N0") + " readings and was stopped.", MessageBoxIcon.Information);
                SaveRecording();
                return;
            }
            TimeSpan t = recorder.Elapsed;
            recordButton.Text = "■  Stop  " + ((int)t.TotalMinutes).ToString("00") + ":" + t.Seconds.ToString("00");
            recordButton.Visible = true;
            UpdateStatusText();
        }

        // ------------------------------------------------------------------ state & status

        private void UpdateUiState()
        {
            bool connected = port != null;
            bool waiting = reconnectPort != null;
            bool idle = !connected && !waiting;

            connectButton.Text = connected ? "Disconnect" : waiting ? "Stop waiting" : "Connect";
            connectButton.Kind = idle ? ButtonKind.Success : ButtonKind.Danger;
            portBox.Enabled = idle;
            baudBox.Enabled = idle;
            refreshButton.Enabled = idle;
            sendBox.Enabled = connected;
            sendButton.Enabled = connected;
            lineEndingBox.Enabled = connected;

            statusDot.State = connected ? DotState.Connected : waiting ? DotState.Waiting : DotState.Idle;
            Text = connected ? port.PortName + " · " + AppInfo.Name : AppInfo.Name;
            UpdateStatusText();
            UpdateStatsText();
        }

        private void UpdateStatusText()
        {
            if (port != null)
                statusLabel.Text = "Connected to " + port.PortName + " at " + port.BaudRate + " baud" + (pauseBox.Checked ? "  (display paused)" : "");
            else if (reconnectPort != null)
                statusLabel.Text = "Waiting for " + reconnectPort + " to come back…";
            else if (portBox.Items.Count == 0)
                statusLabel.Text = "No serial ports found — plug in your board";
            else
                statusLabel.Text = "Not connected";

            if (recorder.IsRecording)
                statusLabel.Text += "   ·   Recording " + recorder.RowCount.ToString("N0") + " readings";
            if (sessionLog != null)
                statusLabel.Text += "   ·   Logging to " + Path.GetFileName(sessionLogPath);
        }

        private void UpdateStatsText()
        {
            statsLabel.Text = "RX " + FormatBytes(Interlocked.Read(ref bytesReceived)) + "   ·   TX " + FormatBytes(bytesSent);
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024.0).ToString("0.0") + " KB";
            return (bytes / (1024.0 * 1024.0)).ToString("0.0") + " MB";
        }

        private void ShowMessage(string text, MessageBoxIcon icon)
        {
            MessageBox.Show(this, text, AppInfo.Name, MessageBoxButtons.OK, icon);
        }

        private static void OpenUrl(string url)
        {
            try { System.Diagnostics.Process.Start(url); } catch (Exception) { }
        }

        private void ShowAbout()
        {
            using (var about = new AboutForm(theme)) about.ShowDialog(this);
        }

        // ------------------------------------------------------------------ keyboard

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            switch (keyData)
            {
                case Keys.F5: ToggleConnection(); return true;
                case Keys.Control | Keys.F: ShowFindBar(); return true;
                case Keys.F3: if (findBar.Visible) NextMatch(true); else ShowFindBar(); return true;
                case Keys.Shift | Keys.F3: if (findBar.Visible) NextMatch(false); return true;
                case Keys.Control | Keys.L: ClearOutput(); return true;
                case Keys.Control | Keys.D1: SetView(false); return true;
                case Keys.Control | Keys.D2: SetView(true); return true;
                case Keys.Control | Keys.S: SaveLog(); return true;
                case Keys.Control | Keys.Oemplus:
                case Keys.Control | Keys.Add: SetOutputFont(output.Font.Size + 1); return true;
                case Keys.Control | Keys.OemMinus:
                case Keys.Control | Keys.Subtract: SetOutputFont(output.Font.Size - 1); return true;
                case Keys.Control | Keys.D0:
                case Keys.Control | Keys.NumPad0: SetOutputFont(DefaultFontSize); return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (recorder.IsRecording && recorder.RowCount > 0)
            {
                var answer = MessageBox.Show(this, "A recording is in progress. Save it before closing?",
                    AppInfo.Name, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (answer == DialogResult.Cancel) { e.Cancel = true; return; }
                recorder.Stop();
                if (answer == DialogResult.Yes) SaveRecording();
            }

            flushTimer.Stop();
            watchTimer.Stop();
            DrainIncoming();
            StopSessionLog();
            SavePreferences();
            ClosePort();
            base.OnFormClosing(e);
        }
    }
}
