# Microsoft Store listing for Serial Scope

Text and answers for Partner Center. Screenshots (1920 × 1080) are in this folder.

## Package

Upload `bin\SerialScope-<version>.msix`, built with:

```bat
build.bat
powershell -ExecutionPolicy Bypass -File tools\make-msix.ps1
```

## Pricing and availability

- **Markets:** all markets
- **Pricing:** Free
- **Visibility:** Public audience, discoverable in the Store

## Properties

- **Category:** Developer tools
- **Privacy policy URL:** https://github.com/Tjs4002/SerialScope/blob/main/CODE_SIGNING.md#privacy-policy
- **Website:** https://tjs4002.github.io/SerialScope/
- **Support contact info:** https://github.com/Tjs4002/SerialScope/issues
- **Product declarations:** leave the defaults. It doesn't need a camera, microphone, touch or any special hardware.
- **System requirements:** Keyboard and Mouse (recommended); everything else unset.

## Age ratings

Choose **All Other App Types**, then answer **No** to every question: no violence, no sexual content, no gambling, no user-to-user communication or content sharing, no location sharing, no digital purchases, no unrestricted internet access. The expected rating is the lowest one (3+ / Everyone).

## Store listing (English)

### Description

SerialScope is a fast, free serial monitor and live plotter for Windows. Plug in your ESP32, Arduino or any USB-serial device, pick its port and see what it's saying, as colour-coded text or as live graphs.

READING YOUR DEVICE
• Error and warning highlighting for ESP-IDF and Arduino-ESP32 logs: errors in red, warnings in amber, debug output dimmed
• Your own highlight rules, by word or regular expression
• Search with match highlighting, and a filter to show only (or hide) matching lines
• Hex view for binary protocols
• Millisecond timestamps, and Pause display without losing data

LIVE PLOTTER
• Reads the Arduino Serial Plotter format: one value per line, several values, or named values like temp:23.5,hum:41
• Zoom, pan through 50,000 readings, box zoom and hover values
• Min, max and average for what you're looking at
• Record readings to CSV and save graphs as PNG images

CONNECTING
• Port list with friendly device names that updates as you plug devices in and out
• Any baud rate from 300 to 2,000,000, plus custom rates
• Full port settings: data bits, parity, stop bits, RTS/CTS and XON/XOFF flow control, DTR and RTS lines
• Connects without resetting your board, and reconnects automatically after you flash new firmware
• Several windows at once, one per device

SENDING AND COMFORT
• Send box with line ending choice, history and saved commands
• Session logs, one file per connection
• Dark and light themes

SerialScope is open source under the MIT License. It doesn't collect any data.

### What's new in this version

First release on the Microsoft Store.

### Product features

1. Error and warning highlighting for ESP32 and Arduino logs
2. Live plotter compatible with the Arduino Serial Plotter format
3. Zoom, pan, statistics and CSV recording for plotted data
4. Search, filter and custom highlight rules
5. Hex view for binary devices
6. Baud rates up to 2,000,000 and full port settings
7. Connects without resetting your board, and reconnects automatically
8. Dark and light themes

### Screenshots (in this order)

1. `screenshot-1-log-dark.png`: Colour-coded ESP32 logs: errors in red, warnings in amber
2. `screenshot-2-plot-dark.png`: Live plotter with the text output underneath
3. `screenshot-3-plot-light.png`: Light theme
4. `screenshot-4-log-light.png`: Log highlighting in the light theme

### Search terms

serial monitor, serial plotter, ESP32, Arduino, COM port, UART, terminal

### Short title

SerialScope

### Short description

Serial monitor and live plotter for ESP32, Arduino and any serial device.

### Copyright and trademark info

© 2026 Tjs4002. Released under the MIT License.

### Developed by

Tjs4002

## Submission options

**Restricted capabilities: why the app needs `runFullTrust`:**

SerialScope is a classic Windows desktop (Win32 / .NET Framework) application packaged with the Desktop Bridge. runFullTrust is required for any desktop app of this kind to run, and it is needed to open USB-serial (COM) ports with System.IO.Ports and to list connected serial devices with their names. The app does not run elevated and makes no system changes.

**Notes for certification:**

SerialScope is a serial monitor. To see it working, connect any USB-serial device (for example an Arduino or ESP32 board), choose its port and click Connect. Without a device, the app opens normally and shows an empty port list. No account or sign-in is needed.
