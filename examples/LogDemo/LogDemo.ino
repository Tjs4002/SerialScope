// SerialScope log demo
// Prints log lines at different levels (ESP-IDF and Arduino styles) so you can try
// SerialScope's error/warning highlighting, search and hex view. It also answers
// what you send from SerialScope's send box.
// Works on ESP32, ESP8266, Arduino and most other boards.
//
// Open SerialScope at 115200 baud. Send "help" to see the commands.

const unsigned long INTERVAL_MS = 700;

unsigned long lastLine = 0;
unsigned long lineCount = 0;
bool logging = true;
char buf[96];

void setup() {
  Serial.begin(115200);
  delay(500);
  Serial.println("SerialScope log demo started. Send 'help' for commands.");
}

void loop() {
  if (Serial.available()) {
    String command = Serial.readStringUntil('\n');
    command.trim();
    handleCommand(command);
  }

  if (!logging || millis() - lastLine < INTERVAL_MS) return;
  lastLine = millis();
  unsigned long ms = millis();

  switch (lineCount % 8) {
    case 0: snprintf(buf, sizeof(buf), "I (%lu) app: Heartbeat %lu", ms, lineCount); break;
    case 1: snprintf(buf, sizeof(buf), "D (%lu) sensor: Raw reading %ld", ms, (long)random(200, 900)); break;
    case 2: snprintf(buf, sizeof(buf), "Plain text line, temperature %ld C", (long)random(20, 30)); break;
    case 3: snprintf(buf, sizeof(buf), "W (%lu) wifi: Signal weak, RSSI -%ld dBm", ms, (long)random(70, 90)); break;
    case 4: snprintf(buf, sizeof(buf), "[%6lu][I][main.cpp:42] loop(): Arduino-style info", ms); break;
    case 5: snprintf(buf, sizeof(buf), "E (%lu) audio: I2S write failed, error 0x103", ms); break;
    case 6: snprintf(buf, sizeof(buf), "[%6lu][W][sensor.cpp:88] read(): Sensor timeout, retrying", ms); break;
    default: snprintf(buf, sizeof(buf), "[%6lu][E][storage.cpp:17] mount(): Card not found", ms); break;
  }
  Serial.println(buf);
  lineCount++;
}

void handleCommand(const String &command) {
  if (command.length() == 0) return;

  if (command == "help") {
    Serial.println("Commands: help, status, pause, resume, binary");
  } else if (command == "status") {
    snprintf(buf, sizeof(buf), "Status: up %lu s, %lu lines sent, logging %s",
             millis() / 1000, lineCount, logging ? "on" : "off");
    Serial.println(buf);
  } else if (command == "pause") {
    logging = false;
    Serial.println("Logging paused. Send 'resume' to continue.");
  } else if (command == "resume") {
    logging = true;
    Serial.println("Logging resumed.");
  } else if (command == "binary") {
    // 32 raw bytes (0x00-0x1F) followed by "Hello" - turn on Hex view to see them
    for (int i = 0; i < 32; i++) Serial.write((uint8_t)i);
    Serial.write("Hello\n");
  } else {
    Serial.print("You sent: ");
    Serial.println(command);
  }
}
