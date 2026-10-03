// SerialScope plotter demo
// Prints three named waveforms about 50 times a second, plus an occasional status
// message, so you can try SerialScope's plotter without wiring up any sensors.
// Works on ESP32, ESP8266, Arduino and most other boards.
//
// Open SerialScope at 115200 baud and click "Plotter".

const unsigned long INTERVAL_MS = 20;

unsigned long lastSample = 0;
unsigned long sampleCount = 0;

void setup() {
  Serial.begin(115200);
  delay(500);
  Serial.println("SerialScope plotter demo started");
}

void loop() {
  if (millis() - lastSample < INTERVAL_MS) return;
  lastSample = millis();

  float t = sampleCount * 0.05f;
  float sine = 50.0f * sin(t);
  float square = (sampleCount / 60) % 2 == 0 ? 30.0f : -30.0f;
  float sensor = 20.0f + 8.0f * sin(t * 3.1f) + random(-30, 31) / 10.0f;   // noisy "sensor"

  // name:value pairs separated by commas -> one named line per value
  Serial.print("sine:");
  Serial.print(sine, 2);
  Serial.print(",square:");
  Serial.print(square, 0);
  Serial.print(",sensor:");
  Serial.println(sensor, 2);

  // Text lines are shown in the text panel and skipped by the graph
  if (sampleCount % 250 == 0) {
    Serial.print("status: running, ");
    Serial.print(sampleCount);
    Serial.println(" samples sent");
  }

  sampleCount++;
}
