<p align="center">
  <img src="docs/icon.png" width="96" alt="SerialScope icon">
</p>

<h1 align="center">SerialScope</h1>

<p align="center">
  A clean, lightweight serial monitor for Windows.<br>
  Built for ESP32, Arduino and any other device that talks over a COM port.
</p>

<p align="center">
  <a href="https://github.com/Tjs4002/SerialScope/releases/latest"><img src="https://img.shields.io/github/v/release/Tjs4002/SerialScope?label=download&color=16a34a" alt="Latest release"></a>
  <a href="https://github.com/Tjs4002/SerialScope/actions/workflows/build.yml"><img src="https://github.com/Tjs4002/SerialScope/actions/workflows/build.yml/badge.svg" alt="Build status"></a>
  <img src="https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-0078d4" alt="Platform: Windows 10 and 11">
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-blue" alt="MIT license"></a>
</p>

<p align="center">
  <img src="docs/screenshot-dark.png" alt="SerialScope showing ESP32 boot output in dark mode" width="800">
</p>

---

## Why SerialScope?

Opening a whole IDE just to read a few lines of serial output is slow, and most standalone terminals look like they're from 1998. SerialScope is a single small `.exe` that opens instantly, shows you what your board is saying, and gets out of the way.

- **No install.** One file, about 50 KB. Download it and run it.
- **No dependencies.** Uses the .NET Framework that already ships with Windows 10 and 11.
- **Doesn't reset your board.** Opening the port leaves DTR and RTS low, so ESP32 and Arduino boards keep running and you see output from the moment you connect.

## Features

| | |
|---|---|
| **Port picker with device names** | Shows `COM3 — Silicon Labs CP210x…` instead of a bare port number, so you can spot your board. The list updates by itself when you plug devices in or out. |
| **Any baud rate** | All the common rates from 300 to 2,000,000, plus **Custom…** for anything else. |
| **Auto-reconnect** | Unplug the board, or flash new firmware, and SerialScope reconnects when the port comes back. |
| **Live plotter** | Turns numeric output into scrolling line graphs, compatible with the Arduino Serial Plotter format. Text output stays visible underneath. |
| **Zoom and pan** | Zoom time or values with the scroll wheel, drag to scroll back through the last 50,000 readings, right-drag to zoom into an area, hover for exact values. |
| **Record to CSV** | Record readings while you watch and save them as a CSV file for Excel, Google Sheets or Python. |
| **Timestamps** | Optional millisecond timestamps on every line. |
| **Pause display** | Freeze the view to read something. Nothing is lost: incoming data is kept and shown when you resume. |
| **Send data** | Type into the box at the bottom, with your choice of line ending (none, LF, CR or CR+LF). |
| **Save log** | Save everything in the window to a `.txt` or `.log` file. |
| **Dark and light themes** | Including a dark title bar and scroll bars on Windows 10 1809 and later. |
| **Remembers your setup** | Port, baud rate, theme, options, font size and window size are restored next time. |

<p align="center">
  <img src="docs/screenshot-light.png" alt="SerialScope in light mode" width="640">
</p>

## Download

1. Go to the [**latest release**](https://github.com/Tjs4002/SerialScope/releases/latest).
2. Download **`SerialScope.exe`**.
3. Double-click it. That's it.

> **"Windows protected your PC"?** The app isn't code-signed (a signing certificate costs hundreds of dollars a year), so Windows SmartScreen may warn you the first time. Click **More info → Run anyway**. If you'd rather not trust a downloaded binary, [build it yourself](#build-from-source) from the source in this repository. It takes a few seconds.

**Requirements:** Windows 10 or 11 (Windows 7/8.1 with .NET Framework 4.5 or later should also work).

## Usage

1. Plug in your board and pick its **Port**.
2. Choose the **Baud** rate your code uses, e.g. `115200` for `Serial.begin(115200)`.
3. Click **Connect**.

> **Uploading new firmware?** Only one program can use a COM port at a time. Click **Disconnect** before uploading from the Arduino IDE, PlatformIO or esptool, or leave **Auto-reconnect** on, disconnect, upload, and reconnect afterwards.

### Plotter

<p align="center">
  <img src="docs/screenshot-plotter.png" alt="SerialScope plotter showing three live waveforms with the text output below" width="800">
</p>

Click **Plotter** (or press <kbd>Ctrl</kbd> + <kbd>2</kbd>) to see your data as a live graph. Want to try it right away? Flash [`examples/PlotterDemo`](examples/PlotterDemo/PlotterDemo.ino) to any Arduino-compatible board. Print one reading per line, in the same format as the Arduino Serial Plotter:

```cpp
Serial.println(analogRead(A0));                 // one line on the graph
Serial.printf("%d %d %d\n", x, y, z);           // three lines: Value 1, Value 2, Value 3
Serial.printf("temp:%.1f,hum:%.1f\n", t, h);    // named lines: temp and hum
```

Values can be separated by spaces, commas or tabs. Lines that aren't numbers (log messages and so on) are skipped by the graph but still appear in the text panel below it.

| Action | How |
|---|---|
| Zoom time | Scroll wheel, or the **−** / **+** buttons |
| Zoom values | <kbd>Ctrl</kbd> + scroll wheel |
| Scroll back | Drag left/right, then **Live ▸** to jump back to the newest data |
| Zoom to an area | Right-drag a box |
| Back to auto-fit | Double-click, or **Fit** |
| Exact values | Hover over the graph |
| Hide/show a line | Click its name in the legend |
| Record | **● Record**, then **■ Stop** to save a CSV file |

### Keyboard shortcuts

| Shortcut | Action |
|---|---|
| <kbd>F5</kbd> | Connect / disconnect |
| <kbd>Ctrl</kbd> + <kbd>1</kbd> / <kbd>2</kbd> | Text view / Plotter |
| <kbd>Ctrl</kbd> + <kbd>L</kbd> | Clear output and graph |
| <kbd>Ctrl</kbd> + <kbd>S</kbd> | Save log |
| <kbd>Ctrl</kbd> + <kbd>+</kbd> / <kbd>−</kbd> / <kbd>0</kbd> | Bigger / smaller / default text size |
| <kbd>Enter</kbd> (in the send box) | Send |

## Build from source

No Visual Studio needed. The C# compiler is already part of Windows.

```bat
git clone https://github.com/Tjs4002/SerialScope.git
cd SerialScope
build.bat
```

The app is written to `bin\SerialScope.exe`.

<details>
<summary>Project layout</summary>

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
├── tools/make-icon.ps1    Regenerates app.ico
├── docs/                  Screenshots and README images
└── build.bat              One-step build
```
</details>

## Contributing

Bug reports and ideas are welcome. Please [open an issue](https://github.com/Tjs4002/SerialScope/issues). See [CONTRIBUTING.md](CONTRIBUTING.md) if you'd like to send a pull request.

## License

[MIT](LICENSE) © 2026 Tejas
