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

        private static readonly int[] BaudRates =
        {
            300, 1200, 2400, 4800, 9600, 19200, 38400, 57600, 74880,
            115200, 230400, 250000, 460800, 500000, 921600, 1000000, 2000000
        };

        private static readonly string[] LineEndings = { "", "\n", "\r", "\r\n" };

        private readonly Settings settings = Settings.Load();
        private Theme theme;

        private readonly FlatComboBox portBox = new FlatComboBox();
        private readonly FlatComboBox baudBox = new FlatComboBox();
        private readonly FlatButton refreshButton = new FlatButton("", ButtonKind.Icon);
        private readonly FlatButton connectButton = new FlatButton("Connect", ButtonKind.Success);
        private readonly FlatButton clearButton = new FlatButton("", ButtonKind.Icon);
        private readonly FlatButton saveButton = new FlatButton("", ButtonKind.Icon);
        private readonly FlatButton themeButton = new FlatButton("", ButtonKind.Icon);
        private readonly FlatButton aboutButton = new FlatButton("", ButtonKind.Icon);
        private readonly FlatCheckBox timestampBox = new FlatCheckBox();
        private readonly FlatCheckBox autoScrollBox = new FlatCheckBox();
        private readonly FlatCheckBox reconnectBox = new FlatCheckBox();
        private readonly FlatCheckBox pauseBox = new FlatCheckBox();
        private readonly TextBox output = new TextBox();
        private readonly BorderPanel outputFrame = new BorderPanel();
        private readonly TextBox sendBox = new TextBox();
        private readonly BorderPanel sendFrame = new BorderPanel();
        private readonly FlatComboBox lineEndingBox = new FlatComboBox();
        private readonly FlatButton sendButton = new FlatButton("Send", ButtonKind.Normal);
        private readonly StatusDot statusDot = new StatusDot();
        private readonly Label statusLabel = new Label();
        private readonly Label statsLabel = new Label();
        private readonly ToolTip tips = new ToolTip();
        private readonly PlotView plot = new PlotView();
        private readonly FlatButton textViewButton = new FlatButton("Text", ButtonKind.Normal);
        private readonly FlatButton plotViewButton = new FlatButton("Plotter", ButtonKind.Normal);
        private readonly Label plotPointsLabel = new Label();
        private readonly FlatComboBox plotPointsBox = new FlatComboBox();
        private readonly StringBuilder plotLine = new StringBuilder();
        private bool plotMode;
        private readonly Label creditSeparator = new Label();
        private readonly LinkLabel creditLink = new LinkLabel();
        private readonly SplitContainer plotSplit = new SplitContainer();
        private double plotSplitRatio = 0.7;
        private readonly FlatButton recordButton = new FlatButton("●  Record", ButtonKind.Normal);
        private readonly Recorder recorder = new Recorder();
        private readonly List<string> parsedNames = new List<string>();
        private readonly List<double> parsedValues = new List<double>();

        private static readonly int[] PlotWindowSizes = { 100, 250, 500, 1000, 2500, 5000 };

        private readonly System.Windows.Forms.Timer flushTimer = new System.Windows.Forms.Timer();
        private readonly System.Windows.Forms.Timer watchTimer = new System.Windows.Forms.Timer();

        private readonly ConcurrentQueue<string> incoming = new ConcurrentQueue<string>();
        private SerialPort port;
        private volatile bool portLost;
        private bool atLineStart = true;
        private long bytesReceived;
        private long bytesSent;
        private string[] knownPorts = new string[0];
        private string reconnectPort;     // set while waiting for a lost port to come back
        private int reconnectBaud;

        public MainForm()
        {
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;
            Text = AppInfo.Name;
            Font = new Font("Segoe UI", 9F);
            MinimumSize = new Size(720, 420);
            StartPosition = FormStartPosition.CenterScreen;
            Size = new Size(settings.GetInt("width", 980), settings.GetInt("height", 640));
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
            AddIconButton(actions, themeButton, "Switch light / dark theme", delegate { ToggleTheme(); });
            AddIconButton(actions, aboutButton, "About " + AppInfo.Name, delegate { ShowAbout(); });
            aboutButton.Margin = Padding.Empty;

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
            AddOption(options, reconnectBox, "Auto-reconnect", "Reconnect automatically when the board is unplugged and plugged back in");
            AddOption(options, pauseBox, "Pause display","Freeze the display; incoming data is kept and shown when you resume");
            pauseBox.CheckedChanged += delegate { if (!pauseBox.Checked) FlushIncoming(); UpdateStatusText(); };

            // Output
            var outputHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 0, 12, 0) };
            outputFrame.Dock = DockStyle.Fill;
            outputFrame.Padding = new Padding(1);
            outputFrame.UseOutputBackground = true;
            output.Tag = "output";
            output.Multiline = true;
            output.ReadOnly = true;
            output.WordWrap = false;
            output.ScrollBars = ScrollBars.Both;
            output.BorderStyle = BorderStyle.None;
            output.Dock = DockStyle.Fill;
            output.MaxLength = 0;
            output.HideSelection = false;
            output.HandleCreated += delegate { if (theme != null) NativeMethods.SetScrollBarTheme(output, theme.IsDark); };
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
            plotSplit.Panel1.Controls.Add(plot);
            plotSplit.Panel2.Controls.Add(output);
            outputFrame.Controls.Add(plotSplit);
            outputHost.Controls.Add(outputFrame);

            // Send row
            var sendBar = new TableLayoutPanel { Dock = DockStyle.Bottom, Height = 52, ColumnCount = 3, RowCount = 1, Padding = new Padding(12, 10, 6, 8) };
            sendBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
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
            };
            NativeMethods.SetPlaceholder(sendBox, "Type a message and press Enter");
            sendFrame.Controls.Add(sendBox);
            sendBar.Controls.Add(sendFrame, 0, 0);
            lineEndingBox.Items.AddRange(new object[] { "No line ending", "Newline (LF)", "Carriage return (CR)", "Both (CR+LF)" });
            lineEndingBox.Width = 160;
            lineEndingBox.Margin = new Padding(0, 4, 6, 0);
            tips.SetToolTip(lineEndingBox, "Characters added after each message you send");
            sendBar.Controls.Add(lineEndingBox, 1, 0);
            sendButton.MinimumSize = new Size(72, 30);
            sendButton.Click += delegate { SendLine(); };
            sendBar.Controls.Add(sendButton, 2, 0);

            // Status bar
            var statusBar = new Panel { Dock = DockStyle.Bottom, Height = 30, Padding = new Padding(14, 0, 14, 6), Tag = "surface" };
            var statusLeft = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 6, 0, 0), Tag = "surface" };
            statusDot.Margin = new Padding(0, 4, 8, 0);
            statusLeft.Controls.Add(statusDot);
            statusLabel.AutoSize = true;
            statusLabel.Margin = Padding.Empty;
            statusLeft.Controls.Add(statusLabel);
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
            reconnectBox.Checked = settings.GetBool("reconnect", true);
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
            settings.Set("reconnect", reconnectBox.Checked);
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
            creditLink.LinkColor = t.Success;
            creditLink.ActiveLinkColor = t.Text;
            creditLink.VisitedLinkColor = t.Success;
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
                UpdateUiState();
            }
            else if (port != null)
            {
                ClosePort();
                WriteSystemLine("Disconnected");
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
                Encoding = Encoding.UTF8,
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

            port = sp;
            portLost = false;
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

        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string data = ((SerialPort)sender).ReadExisting();
                if (data.Length == 0) return;
                incoming.Enqueue(data);
                Interlocked.Add(ref bytesReceived, data.Length);
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

            if (reconnectBox.Checked)
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
            string text = sendBox.Text + LineEndings[Math.Max(0, lineEndingBox.SelectedIndex)];
            try
            {
                port.Write(text);
                bytesSent += Encoding.UTF8.GetByteCount(text);
                sendBox.Clear();
                UpdateStatsText();
            }
            catch (Exception ex)
            {
                WriteSystemLine("Send failed: " + ex.Message);
            }
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

        private void DrainIncoming()
        {
            var sb = new StringBuilder();
            string chunk;
            while (incoming.TryDequeue(out chunk))
            {
                FormatChunk(sb, chunk);
                FeedPlot(chunk);
            }
            if (sb.Length > 0) AppendOutput(sb.ToString());
            if (plotMode) plot.RefreshIfChanged();
        }

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

        // Asks where to save; returns false only if the user chose to keep the recording unsaved and go back
        private bool SaveRecording()
        {
            recordButton.Kind = ButtonKind.Normal;
            recordButton.Text = "●  Record";
            recordButton.Visible = plotMode;
            UpdateStatusText();

            if (recorder.RowCount == 0)
            {
                ShowMessage("Nothing was recorded. The recording only captures lines of numbers, such as \"23.5\" or \"temp:23.5,hum:41\".", MessageBoxIcon.Information);
                recorder.Discard();
                return true;
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
                            return true;
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
                    return true;
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
        }

        // Converts line endings for the TextBox and adds timestamps
        private void FormatChunk(StringBuilder sb, string chunk)
        {
            foreach (char c in chunk)
            {
                if (c == '\r' || c == '\0') continue;
                if (atLineStart && timestampBox.Checked)
                    sb.Append(DateTime.Now.ToString("HH:mm:ss.fff")).Append("   ");
                atLineStart = false;

                if (c == '\n') { sb.Append("\r\n"); atLineStart = true; }
                else sb.Append(c);
            }
        }

        private void WriteSystemLine(string message)
        {
            DrainIncoming();
            string prefix = atLineStart ? "" : "\r\n";
            AppendOutput(prefix + "—— " + message + " ——\r\n");
            atLineStart = true;
            UpdateStatusText();
        }

        private void AppendOutput(string text)
        {
            if (output.TextLength + text.Length > MaxOutputChars)
            {
                string all = output.Text + text;
                int cut = all.IndexOf('\n', all.Length - TrimToChars);
                output.Text = cut >= 0 ? all.Substring(cut + 1) : all.Substring(all.Length - TrimToChars);
            }
            else if (autoScrollBox.Checked)
            {
                output.AppendText(text);
            }
            else
            {
                // Add text without moving the user's scroll position or selection
                int start = output.SelectionStart, length = output.SelectionLength;
                output.Select(output.TextLength, 0);
                output.SelectedText = text;
                output.Select(start, length);
            }

            if (autoScrollBox.Checked)
            {
                output.SelectionStart = output.TextLength;
                output.ScrollToCaret();
            }
        }

        private void ClearOutput()
        {
            output.Clear();
            atLineStart = true;
            plot.Clear();
            plotLine.Length = 0;
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
                    File.WriteAllText(dialog.FileName, output.Text, Encoding.UTF8);
                    statusLabel.Text = "Saved log to " + Path.GetFileName(dialog.FileName);
                }
                catch (Exception ex)
                {
                    ShowMessage("Could not save the log.\n\n" + ex.Message, MessageBoxIcon.Warning);
                }
            }
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
            SavePreferences();
            ClosePort();
            base.OnFormClosing(e);
        }
    }
}
