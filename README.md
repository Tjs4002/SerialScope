<p align="center">
  <img src="docs/banner.png" alt="SerialScope: serial monitor and live plotter for ESP32, Arduino and more" width="100%">
</p>

<p align="center">
  <a href="https://github.com/Tjs4002/SerialScope/releases/latest"><img src="https://img.shields.io/github/v/release/Tjs4002/SerialScope?style=flat-square&label=release&color=16a34a" alt="Latest release"></a>
  <a href="https://github.com/Tjs4002/SerialScope/releases"><img src="https://img.shields.io/github/downloads/Tjs4002/SerialScope/total?style=flat-square&color=16a34a" alt="Downloads"></a>
  <a href="https://github.com/Tjs4002/SerialScope/actions/workflows/build.yml"><img src="https://img.shields.io/github/actions/workflow/status/Tjs4002/SerialScope/build.yml?style=flat-square&label=build" alt="Build status"></a>
  <img src="https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-0078d4?style=flat-square" alt="Platform: Windows 10 and 11">
  <a href="LICENSE"><img src="https://img.shields.io/github/license/Tjs4002/SerialScope?style=flat-square&color=blue" alt="MIT license"></a>
</p>

<p align="center">
  <a href="https://github.com/Tjs4002/SerialScope/releases/latest"><b>Download</b></a>
  &nbsp;·&nbsp;
  <a href="#features"><b>Features</b></a>
  &nbsp;·&nbsp;
  <a href="#plotter"><b>Plotter</b></a>
  &nbsp;·&nbsp;
  <a href="#build-from-source"><b>Build</b></a>
  &nbsp;·&nbsp;
  <a href="#faq"><b>FAQ</b></a>
</p>

<br>

**SerialScope** is a fast, good-looking serial monitor and plotter for Windows. Open it, pick your board's port and see what it's saying, as text or as live graphs. It's a single 50 KB `.exe`, with nothing to install.

<table>
  <tr>
    <td width="33%" valign="top">
      <h3>⚡ Instant</h3>
      One small file that opens in a blink. No installer, no runtime to download, no account.
    </td>
    <td width="33%" valign="top">
      <h3>📈 Live plotter</h3>
      Numbers become scrolling graphs, using the Arduino Serial Plotter format. Zoom, pan, hover and record to CSV.
    </td>
    <td width="33%" valign="top">
      <h3>🔌 Board friendly</h3>
      Connecting doesn't reset your ESP32 or Arduino, and it reconnects by itself after you unplug or reflash.
    </td>
  </tr>
</table>

## Features

**Connecting**
- **Port picker with device names:** shows `COM3 — Silicon Labs CP210x…` instead of a bare number, and updates when you plug devices in or out.
- **Any baud rate:** 300 to 2,000,000 built in, plus **Custom…** for anything else.
- **Auto-reconnect:** unplug the board or flash new firmware, and SerialScope picks it back up when the port returns.
- **No surprise resets:** DTR and RTS stay low, so your board keeps running when you connect.

**Reading**
- **Live plotter** with zoom, pan, box zoom, hover values and a clickable legend. See [Plotter](#plotter).
- **Record to CSV** for Excel, Google Sheets or Python.
- **Timestamps** to the millisecond, **Pause display** without losing data, and **Save log** to a text file.
- **Send data** back to the board, with a choice of line ending.

**Comfort**
- **Dark and light themes,** right down to the title bar and scroll bars.
- **Keyboard shortcuts** and adjustable text size.
- **Remembers everything:** port, baud rate, theme, view, options and window size.

<table>
  <tr>
    <td><img src="docs/screenshot-dark.png" alt="SerialScope in dark mode showing ESP32 boot output"></td>
    <td><img src="docs/screenshot-light.png" alt="SerialScope in light mode"></td>
  </tr>
  <tr>
    <td align="center"><sub>Dark theme</sub></td>
    <td align="center"><sub>Light theme</sub></td>
  </tr>
</table>

## Download

1. Open the [**latest release**](https://github.com/Tjs4002/SerialScope/releases/latest).
2. Download **`SerialScope.exe`**.
3. Run it. That's all.

> [!NOTE]
> **"Windows protected your PC"?** SerialScope isn't code-signed yet, so SmartScreen may warn you the first time. Click **More info → Run anyway**. Prefer not to trust a downloaded binary? [Build it yourself](#build-from-source) in a few seconds; every release is also built publicly by [GitHub Actions](https://github.com/Tjs4002/SerialScope/actions).

**Requirements:** Windows 10 or 11. Windows 7 and 8.1 with .NET Framework 4.5 or later should also work.

## Quick start

1. Plug in your board and choose its **Port**.
2. Pick the **Baud** rate from your code, e.g. `115200` for `Serial.begin(115200)`.
3. Click **Connect**, or press <kbd>F5</kbd>.

> [!TIP]
> Only one program can use a COM port at a time. Click **Disconnect** before uploading from the Arduino IDE, PlatformIO or esptool, then connect again afterwards.

## Plotter

<p align="center">
  <img src="docs/screenshot-plotter.png" alt="SerialScope plotter showing three live waveforms with the text output below" width="820">
</p>

Click **Plotter** (or press <kbd>Ctrl</kbd> + <kbd>2</kbd>). Print one reading per line, in the same format as the Arduino Serial Plotter:

```cpp
Serial.println(analogRead(A0));                 // one line on the graph
Serial.printf("%d %d %d\n", x, y, z);           // three lines: Value 1, Value 2, Value 3
Serial.printf("temp:%.1f,hum:%.1f\n", t, h);    // named lines: temp and hum
```

Values can be separated by spaces, commas or tabs. Text lines such as log messages are left out of the graph but still show in the text panel underneath it.

Want to try it now? Flash [`examples/PlotterDemo`](examples/PlotterDemo/PlotterDemo.ino) to any Arduino-compatible board.

| Action | How |
|---|---|
| Zoom time | Scroll wheel, or the **−** / **+** buttons |
| Zoom values | <kbd>Ctrl</kbd> + scroll wheel |
| Scroll back through history | Drag sideways, then **Live ▸** to return to the newest data |
| Zoom into an area | Right-drag a box |
| Fit everything | Double-click, or **Fit** |
| Exact values | Hover over the graph |
| Hide or show a line | Click its name in the legend |
| Record to CSV | **● Record**, then **■ Stop** to save |

## Keyboard shortcuts

| Shortcut | Action |
|---|---|
| <kbd>F5</kbd> | Connect / disconnect |
| <kbd>Ctrl</kbd> + <kbd>1</kbd> / <kbd>2</kbd> | Text view / Plotter |
| <kbd>Ctrl</kbd> + <kbd>L</kbd> | Clear output and graph |
| <kbd>Ctrl</kbd> + <kbd>S</kbd> | Save log |
| <kbd>Ctrl</kbd> + <kbd>+</kbd> / <kbd>−</kbd> / <kbd>0</kbd> | Bigger / smaller / default text size |
| <kbd>Enter</kbd> in the send box | Send |

## Build from source

No Visual Studio or SDK needed: the C# compiler already ships with Windows.

```bat
git clone https://github.com/Tjs4002/SerialScope.git
cd SerialScope
build.bat
```

The app is written to `bin\SerialScope.exe`.

<details>
<summary><b>Project layout</b></summary>

```
SerialScope/
├── src/
│   ├── MainForm.cs        Main window: connection, output, sending
│   ├── PlotView.cs        Live graph with zoom, pan and hover values
│   ├── Recorder.cs        Records plotted readings to CSV
│   ├── Controls.cs        Themed buttons, drop-downs, check boxes, status dot
│   ├── Theme.cs           Dark and light colour palettes
│   ├── PortInfo.cs        Port list with friendly device names
│   ├── Settings.cs        Saved preferences (%APPDATA%\SerialScope\settings.ini)
│   ├── AboutForm.cs       About dialog
│   ├── CustomBaudForm.cs  Custom baud rate dialog
│   ├── AppInfo.cs         Name, version, author, links
│   ├── Program.cs         Entry point
│   ├── app.ico            Application icon
│   └── app.manifest       DPI awareness and modern Windows controls
├── examples/PlotterDemo   Arduino sketch that prints test waveforms
├── tools/                 Icon and banner generators
├── docs/                  Screenshots and README images
└── build.bat              One-step build
```
</details>

## FAQ

<details>
<summary><b>It says the port is busy or in use.</b></summary>

Another program has the port open: usually the Arduino IDE's Serial Monitor, an upload in progress, or a second copy of SerialScope. Close it and click **Connect** again.
</details>

<details>
<summary><b>I only see garbage characters.</b></summary>

The baud rate doesn't match your code. Pick the same number you used in `Serial.begin(...)`. ESP32 boot messages use 115200.
</details>

<details>
<summary><b>My board doesn't appear in the port list.</b></summary>

Try a different USB cable (many are charge-only), then install the driver for your board's USB chip. That's usually the **CP210x** (Silicon Labs) or **CH340** (WCH); the chip name is printed on the board next to the USB port.
</details>

<details>
<summary><b>The plotter says "Waiting for numbers".</b></summary>

Each line must contain only numbers, optionally with names, like `23.5`, `1 2 3` or `temp:23.5,hum:41`. Lines with other text are shown in the text panel but not plotted.
</details>

<details>
<summary><b>Does it work on macOS or Linux?</b></summary>

Not yet. SerialScope uses Windows' built-in tools to stay tiny and install-free. A cross-platform version may come later; [open an issue](https://github.com/Tjs4002/SerialScope/issues) if you'd use it.
</details>

## Contributing

Bug reports, ideas and pull requests are welcome. Start with an [issue](https://github.com/Tjs4002/SerialScope/issues/new/choose), and see [CONTRIBUTING.md](CONTRIBUTING.md) for how to build and submit changes.

If SerialScope saves you time, a ⭐ on the repo helps other people find it.

## License

Released under the [MIT License](LICENSE). © 2026 Tjs4002
