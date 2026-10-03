# Changelog

All notable changes to SerialScope are listed here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow [Semantic Versioning](https://semver.org/).

## [1.0.0] - 2026-10-04

First public release.

### Added
- Port list with friendly device names; refreshes automatically when devices are plugged in or removed.
- Baud rates from 300 to 2,000,000, plus custom rates.
- Connects without resetting the board (DTR and RTS held low).
- Auto-reconnect when a board is unplugged and plugged back in.
- Live plotter compatible with the Arduino Serial Plotter format, with text output shown below the graph.
- Plotter zoom (time and values), panning through the last 50,000 readings, box zoom, hover values and a clickable legend.
- Record plotted readings and save them as CSV.
- Optional millisecond timestamps.
- Pause display without losing incoming data.
- Send box with selectable line ending.
- Save log to a text file.
- Dark and light themes, with a dark title bar and scroll bars.
- Keyboard shortcuts and adjustable text size.
- Saved preferences: port, baud rate, theme, options, font size and window size.
- Creator credit with GitHub profile link in the status bar and About dialog.

[1.0.0]: https://github.com/Tjs4002/SerialScope/releases/tag/v1.0.0
