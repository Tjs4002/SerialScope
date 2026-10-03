# Changelog

All notable changes to SerialScope are listed here.
The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and versions follow [Semantic Versioning](https://semver.org/).

## [1.1.0] - 2026-10-04

### Added
- **Highlighting:** error lines in red, warnings in amber and debug lines dimmed, recognising ESP-IDF (`E (123) tag:`), Arduino-ESP32 (`[E][file.cpp:12]`) and common words like "error" and "failed". Can be turned off in Settings.
- **Search** with <kbd>Ctrl</kbd>+<kbd>F</kbd>: highlights every match, with next/previous, match count and a match-case option.
- **Hex view** showing the raw bytes, offsets and printable characters, for binary devices.
- **Send history:** <kbd>↑</kbd>/<kbd>↓</kbd> in the send box recall the last 50 messages.
- **Saved commands:** keep messages you send often and send them with one click.
- **Session logs:** optionally save everything to `Documents\SerialScope\Logs`, one file per connection.
- **Update check:** a link in the status bar when a newer version is released (at most twice a day; can be turned off).
- **Plotter:** save the graph as a PNG image, show clock time on the bottom axis, and set a fixed value range.
- Right-click menu on the output (copy, select all, find, clear).
- Settings menu (⚙) for auto-reconnect, highlighting, session logs and updates.
- Releases now include a SHA-256 checksum file and a Scoop manifest.
- `LogDemo` example sketch for trying highlighting, search, hex view and saved commands.

### Changed
- Data is now read as raw bytes, so multi-byte UTF-8 characters split across reads display correctly.

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

[1.1.0]: https://github.com/Tjs4002/SerialScope/releases/tag/v1.1.0
[1.0.0]: https://github.com/Tjs4002/SerialScope/releases/tag/v1.0.0
