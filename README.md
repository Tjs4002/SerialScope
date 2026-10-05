<p align="center">
  <img src="docs/banner.png" alt="SerialScope: serial monitor and live plotter for ESP32, Arduino and more" width="100%">
</p>

<p align="center">
  <a href="https://github.com/Tjs4002/SerialScope/releases/latest"><img src="https://img.shields.io/github/v/release/Tjs4002/SerialScope?style=flat-square&label=release&color=16a34a" alt="Latest release"></a>
  <a href="https://github.com/Tjs4002/SerialScope/releases"><img src="https://img.shields.io/github/downloads/Tjs4002/SerialScope/total?style=flat-square&color=16a34a" alt="Downloads"></a>
  <a href="https://github.com/Tjs4002/SerialScope/actions/workflows/build.yml"><img src="https://img.shields.io/github/actions/workflow/status/Tjs4002/SerialScope/build.yml?style=flat-square&label=build%20%26%20tests" alt="Build and test status"></a>
  <img src="https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-0078d4?style=flat-square" alt="Platform: Windows 10 and 11">
  <a href="LICENSE"><img src="https://img.shields.io/github/license/Tjs4002/SerialScope?style=flat-square&color=blue" alt="MIT license"></a>
</p>

<p align="center">
  <a href="https://apps.microsoft.com/detail/9NSVHTVWKC2X?mode=direct"><img src="https://get.microsoft.com/images/en-us%20dark.svg" width="200" alt="Get it from Microsoft"></a>
</p>

<p align="center">
  <a href="https://tjs4002.github.io/SerialScope/"><b>Website</b></a>
  &nbsp;·&nbsp;
  <a href="https://github.com/Tjs4002/SerialScope/releases/latest"><b>Download</b></a>
  &nbsp;·&nbsp;
  <a href="#features"><b>Features</b></a>
  &nbsp;·&nbsp;
  <a href="#plotter"><b>Plotter</b></a>
  &nbsp;·&nbsp;
  <a href="#highlighting-filtering-and-search"><b>Highlighting</b></a>
  &nbsp;·&nbsp;
  <a href="#port-settings"><b>Port settings</b></a>
  &nbsp;·&nbsp;
  <a href="#build-from-source"><b>Build</b></a>
  &nbsp;·&nbsp;
  <a href="#faq"><b>FAQ</b></a>
</p>

<br>

**SerialScope** is a fast, good-looking serial monitor and plotter for Windows. Open it, pick your board's port and see what it's saying, as text or as live graphs. It's a single `.exe` of under 250 KB, with nothing to install. It's also on the [Microsoft Store](https://apps.microsoft.com/detail/9NSVHTVWKC2X?mode=direct).

<table>
  <tr>
    <td width="33%" valign="top">
      <h3>⚡ Instant</h3>
      One small file that opens in a blink. No installer, no runtime to download, no account.
    </td>
    <td width="33%" valign="top">
      <h3>📈 Live plotter</h3>
      Numbers become scrolling graphs, using the Arduino Serial Plotter format. Zoom, pan, stats and CSV recording.
    </td>
    <td width="33%" valign="top">
      <h3>🔌 Works with any device</h3>
      ESP32 and Arduino friendly out of the box, with full port settings for everything else.
    </td>
  </tr>
</table>

## Features

**Connecting**
- **Port picker with device names:** shows `COM3 — Silicon Labs CP210x…` instead of a bare number, and updates when you plug devices in or out.
- **Any baud rate:** 300 to 2,000,000 built in, plus **Custom…** for anything else.
- **Full port settings:** data bits, parity, stop bits, flow control (RTS/CTS, XON/XOFF) and the DTR/RTS lines. See [Port settings](#port-settings).
- **Auto-reconnect:** unplug the board or flash new firmware, and SerialScope picks it back up when the port returns.
- **No surprise resets:** DTR and RTS stay low by default, so your board keeps running when you connect.
- **Several devices at once:** **New window** (<kbd>Ctrl</kbd> + <kbd>N</kbd>) opens another window on a different port.

**Reading**
- **Error and warning highlighting** for ESP-IDF (`E (123) wifi:`) and Arduino-ESP32 (`[E][main.cpp:42]`) logs, plus **your own highlight rules** by word or regular expression.
- **Search and filter:** find every match, or show only (or hide) the lines that contain a word.
- **Hex view** for binary devices: offsets, raw bytes and printable characters.
- **Live plotter** with zoom, pan, hover values, min/max/average stats, a time axis and fixed ranges. See [Plotter](#plotter).
- **Record to CSV** for Excel, Google Sheets or Python, and **save graphs** as PNG images.
- **Timestamps** to the millisecond, and **Pause display** without losing data.

**Sending**
- **Send box** with a choice of line ending.
- **History:** <kbd>↑</kbd> / <kbd>↓</kbd> bring back the last 50 messages.
- **Saved commands:** keep the messages you send often one click away (★ button).

**Comfort**
- **Session logs:** optionally save everything to a file, one per connection.
- **Portable mode:** keep settings and logs next to the `.exe`, to run it from a USB stick.
- **Command-line options** to open a port and connect from a shortcut or script.
- **Update notice** in the status bar when a new version is out, and a **friendly crash screen** that can file a bug report for you.
- **Dark and light themes,** right down to the title bar, scroll bars and menus.

<table>
  <tr>
    <td><img src="docs/screenshot-dark.png" alt="SerialScope highlighting errors in red and warnings in amber from an ESP32"></td>
    <td><img src="docs/screenshot-light.png" alt="SerialScope plotter with a time axis in light mode"></td>
  </tr>
  <tr>
    <td align="center"><sub>Error and warning highlighting (dark theme)</sub></td>
    <td align="center"><sub>Live plotter with time axis (light theme)</sub></td>
  </tr>
</table>

## Download

**Microsoft Store (recommended):** signed by Microsoft, updates automatically, and no SmartScreen warning.

<a href="https://apps.microsoft.com/detail/9NSVHTVWKC2X?mode=direct"><img src="https://get.microsoft.com/images/en-us%20dark.svg" width="200" alt="Get it from Microsoft"></a>

Or from a terminal:

```powershell
winget install 9NSVHTVWKC2X
```

**Direct download:** a single `.exe` you can also run from a USB stick ([portable mode](#more-ways-to-run-it)).

1. Open the [**latest release**](https://github.com/Tjs4002/SerialScope/releases/latest).
2. Download **`SerialScope.exe`**.
3. Run it. That's all.

**With [Scoop](https://scoop.sh):**

```powershell
scoop install https://github.com/Tjs4002/SerialScope/releases/latest/download/serialscope.json
```

Each release also includes `SHA256SUMS.txt`, so you can check your download with `Get-FileHash SerialScope.exe`.

> [!NOTE]
> **"Windows protected your PC"?** The downloaded `.exe` isn't code-signed yet, so SmartScreen may warn you the first time. Click **More info → Run anyway**, or install from the [Microsoft Store](https://apps.microsoft.com/detail/9NSVHTVWKC2X?mode=direct) instead, which is signed by Microsoft. Prefer not to trust a downloaded binary? [Build it yourself](#build-from-source) in a few seconds; every release is also built and tested publicly by [GitHub Actions](https://github.com/Tjs4002/SerialScope/actions).

**Requirements:** Windows 10 or 11. Windows 7 and 8.1 with .NET Framework 4.5 or later should also work.

## Quick start

1. Plug in your board and choose its **Port**.
2. Pick the **Baud** rate from your code, e.g. `115200` for `Serial.begin(115200)`.
3. Click **Connect**, or press <kbd>F5</kbd>.

> [!TIP]
> Only one program can use a COM port at a time. Click **Disconnect** before uploading from the Arduino IDE, PlatformIO or esptool, then connect again afterwards.

## Plotter

<p align="center">
  <img src="docs/screenshot-plotter.png" alt="SerialScope plotter showing three live waveforms with min, max and average in the legend" width="820">
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
| Min, max and average | **Stats** button on the graph (for the part you're looking at) |
| Hide or show a line | Click its name in the legend |
| Record to CSV | **● Record**, then **■ Stop** to save |
| Save the graph as an image | Camera button next to **Record** |
| Clock time on the bottom axis | **Time axis** button on the graph |
| Fixed value range (e.g. 0–4095) | **Y range…** button on the graph; **Automatic** to undo |

## Highlighting, filtering and search

- **Built-in highlighting** colours whole lines: red for errors, amber for warnings, dimmed for debug output. It recognises ESP-IDF logs (`E (1234) wifi: …`), Arduino-ESP32 logs (`[  1234][W][main.cpp:42] …`) and words like *error*, *panic*, *failed* or *timeout*.
- **Your own rules** (⚙ **Settings → Highlight rules…**): colour lines that contain a word, like `TEMP` in blue or `OK` in green, or that match a regular expression. Rules are checked from top to bottom and come before the built-in colours.
- **Search** (<kbd>Ctrl</kbd> + <kbd>F</kbd>) highlights every match. <kbd>Enter</kbd> / <kbd>Shift</kbd> + <kbd>Enter</kbd> (or <kbd>F3</kbd>) jump between them; <kbd>Esc</kbd> closes the bar.
- **Filter:** in the search bar, set **Show** to *Only matching* or *Hide matching* to cut the noise. It applies to everything already received, not just new lines.
- **Hex view** shows 16 bytes per line with the offset, the bytes in hex and their printable characters, for GPS modules, Modbus devices and other binary protocols.

<table>
  <tr>
    <td><img src="docs/screenshot-rules.png" alt="Highlight rules dialog"></td>
    <td><img src="docs/screenshot-filter.png" alt="Search bar showing only lines that contain wifi"></td>
  </tr>
  <tr>
    <td align="center"><sub>Your own highlight rules</sub></td>
    <td align="center"><sub>Showing only the lines that mention "wifi"</sub></td>
  </tr>
</table>

Try them with [`examples/LogDemo`](examples/LogDemo/LogDemo.ino): it prints log lines at every level, answers what you send, and sends raw bytes when you type `binary`.

## Port settings

<img src="docs/screenshot-port-settings.png" alt="Port settings dialog" width="360" align="right">

The **8N1** button next to the baud rate opens the port settings:

- **Data bits** (5–8), **parity** (none, odd, even, mark, space) and **stop bits** (1, 1.5, 2). The button always shows the current setting, e.g. `7E1`.
- **Flow control:** none, RTS/CTS (hardware), XON/XOFF (software), or both.
- **DTR and RTS** control lines. Leave them off for ESP32 and Arduino boards, whose reset circuit uses them.

Changes apply straight away, even while connected.

<br clear="right">

## More ways to run it

**Several devices at once.** **New window** (<kbd>Ctrl</kbd> + <kbd>N</kbd>, or the window button in the toolbar) opens another SerialScope window on a different port. Use Windows Snap (<kbd>Win</kbd> + <kbd>←</kbd> / <kbd>→</kbd>) to put them side by side.

**Command line.** Open a port and connect straight from a shortcut, script or IDE:

```bat
SerialScope.exe --port COM3 --baud 115200 --connect --plot
```

| Option | Does |
|---|---|
| `--port COM3` | Select a port |
| `--baud 115200` | Select a baud rate |
| `--connect` | Connect straight away |
| `--plot` / `--text` | Open in plotter or text view |
| `--hex` | Turn on hex view |
| `--log` | Save a session log |
| `--theme dark` / `--theme light` | Choose the theme |
| `--help` | Show all options |

**Portable mode** (downloaded `.exe` only). Turn on ⚙ **Settings → Portable mode** and SerialScope keeps its settings in `SerialScope.ini` next to the `.exe`, and session logs in a `Logs` folder beside it. Copy the folder to a USB stick and your setup comes with you.

## Keyboard shortcuts

| Shortcut | Action |
|---|---|
| <kbd>F5</kbd> | Connect / disconnect |
| <kbd>Ctrl</kbd> + <kbd>N</kbd> | New window |
| <kbd>Ctrl</kbd> + <kbd>1</kbd> / <kbd>2</kbd> | Text view / Plotter |
| <kbd>Ctrl</kbd> + <kbd>F</kbd> | Find in output |
| <kbd>F3</kbd> / <kbd>Shift</kbd> + <kbd>F3</kbd> | Next / previous match |
| <kbd>Ctrl</kbd> + <kbd>L</kbd> | Clear output and graph |
| <kbd>Ctrl</kbd> + <kbd>S</kbd> | Save log |
| <kbd>Ctrl</kbd> + <kbd>+</kbd> / <kbd>−</kbd> / <kbd>0</kbd> | Bigger / smaller / default text size |
| <kbd>Enter</kbd> in the send box | Send |
| <kbd>↑</kbd> / <kbd>↓</kbd> in the send box | Previous / next sent message |

## Build from source

No Visual Studio or SDK needed: the C# compiler already ships with Windows.

```bat
git clone https://github.com/Tjs4002/SerialScope.git
cd SerialScope
build.bat
```

The app is written to `bin\SerialScope.exe`. Run the automated tests with `tests\run-tests.bat`; GitHub runs them on every change too.

<details>
<summary><b>Project layout</b></summary>

```
SerialScope/
├── src/
│   ├── MainForm.cs            Main window: connection, output, sending, settings
│   ├── OutputView.cs          Coloured output, built-in highlighting and search
│   ├── HighlightRules.cs      Your own highlight rules
│   ├── PlotView.cs            Live graph with zoom, pan, stats, time axis and hover values
│   ├── Recorder.cs            Records plotted readings to CSV
│   ├── PortConfig.cs          Data bits, parity, stop bits, flow control, DTR/RTS
│   ├── CommandLine.cs         Command-line options
│   ├── CrashHandler.cs        Friendly crash screen and bug report link
│   ├── UpdateChecker.cs       Checks GitHub for a newer release
│   ├── Settings.cs            Saved preferences (AppData, or next to the exe in portable mode)
│   ├── Controls.cs            Themed buttons, drop-downs, check boxes, menus
│   ├── Theme.cs               Dark and light colour palettes
│   ├── PortInfo.cs            Port list with friendly device names
│   ├── *Form.cs               Dialogs: about, port settings, highlight rules, baud, range
│   ├── AppInfo.cs             Name, version, author, links
│   ├── Program.cs             Entry point
│   ├── app.ico                Application icon
│   └── app.manifest           DPI awareness and modern Windows controls
├── tests/                     Automated tests (tests\run-tests.bat)
├── examples/
│   ├── PlotterDemo            Prints test waveforms for the plotter
│   └── LogDemo                Prints log levels and answers commands
├── packaging/                 Scoop template, winget manifests and Microsoft Store manifest
├── tools/                     Icon, banner, winget manifest, Store package and Store art scripts
├── docs/                      Website (GitHub Pages), screenshots and README images
└── build.bat                  One-step build
```
</details>

## FAQ

<details>
<summary><b>It says the port is busy or in use.</b></summary>

Another program has the port open: usually the Arduino IDE's Serial Monitor, an upload in progress, or another SerialScope window. Close it and click **Connect** again.
</details>

<details>
<summary><b>I only see garbage characters.</b></summary>

The baud rate doesn't match your code. Pick the same number you used in `Serial.begin(...)`. ESP32 boot messages use 115200. For non-Arduino devices, also check the [port settings](#port-settings) (for example 7E1 instead of 8N1).
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
<summary><b>The same few characters repeat thousands of times.</b></summary>

That's a USB-serial adapter glitch, usually after switching flow control or right after a firmware upload. Unplug the board and plug it back in.
</details>

<details>
<summary><b>Does SerialScope connect to the internet?</b></summary>

Only to check for updates: at most twice a day it asks GitHub's public API for the latest release version. No data about you or your devices is sent. Turn it off under ⚙ **Settings → Check for updates automatically**. The Microsoft Store version doesn't check at all, because the Store keeps it up to date. Crash reports are only sent if you click **Report on GitHub** and submit the form yourself.
</details>

<details>
<summary><b>Where are session logs saved?</b></summary>

In `Documents\SerialScope\Logs` (or a `Logs` folder next to the `.exe` in portable mode), one file per connection named after the port and time, e.g. `COM3-2026-10-04-143012.txt`. Turn them on and open the folder from ⚙ **Settings**.
</details>

<details>
<summary><b>Does it work on macOS or Linux?</b></summary>

Not yet. SerialScope uses Windows' built-in tools to stay tiny and install-free. A cross-platform version may come later; [open an issue](https://github.com/Tjs4002/SerialScope/issues) if you'd use it.
</details>

## Contributing

Bug reports, ideas and pull requests are welcome. Start with an [issue](https://github.com/Tjs4002/SerialScope/issues/new/choose), and see [CONTRIBUTING.md](CONTRIBUTING.md) for how to build, test and submit changes.

If SerialScope saves you time, a ⭐ on the repo helps other people find it.

## License

Released under the [MIT License](LICENSE). © 2026 Tjs4002

See also the [code signing and privacy policy](CODE_SIGNING.md).
