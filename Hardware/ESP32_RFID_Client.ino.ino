#include <WiFi.h>
#include <HTTPClient.h>
#include <ArduinoJson.h>
#include <SPI.h>
#include <MFRC522.h>
#include <Wire.h>
#include <Adafruit_GFX.h>
#include <Adafruit_SSD1306.h>
#include <WiFiClientSecure.h>

#define RFID_SS_PIN 5
#define RFID_RST_PIN 27

#define LED_GREEN 25
#define LED_RED 26
#define LED_WIFI 13

#define SKIP_BUTTON_PIN 14

#define I2C_SDA 21
#define I2C_SCL 22

#define SCREEN_WIDTH 128
#define SCREEN_HEIGHT 64
#define OLED_RESET -1
#define OLED_ADDRESS 0x3C

const char* WIFI_SSID = "Alex king";
const char* WIFI_PASSWORD = "alex0000";

const char* DEVICE_KEY = "BOARD01";
const char* SERVER_BASE_URL = "https://se-lab-testapp-005-alex.azurewebsites.net";

MFRC522 rfid(RFID_SS_PIN, RFID_RST_PIN);
Adafruit_SSD1306 display(SCREEN_WIDTH, SCREEN_HEIGHT, &Wire, OLED_RESET);

String currentMode = "Idle";
String displayLine1 = "Smart Attendance";
String displayLine2 = "Starting...";

unsigned long lastStateCheck = 0;
const unsigned long stateCheckInterval = 2000;

bool lastSkipState = HIGH;
unsigned long lastSkipPress = 0;

void ledsOff() {
  digitalWrite(LED_GREEN, LOW);
  digitalWrite(LED_RED, LOW);
}

String fitText(String value) {
  if (value.length() <= 21) {
    return value;
  }

  return value.substring(0, 21);
}

void showScreen(String line1, String line2) {
  display.clearDisplay();

  display.setTextColor(SSD1306_WHITE);

  display.setTextSize(1);
  display.setCursor(0, 0);
  display.println("Smart Attendance");

  display.drawLine(0, 12, 127, 12, SSD1306_WHITE);

  display.setTextSize(1);
  display.setCursor(0, 22);
  display.println(fitText(line1));

  display.setCursor(0, 42);
  display.println(fitText(line2));

  display.display();
}

void blinkLed(int pin, int times, int delayMs) {
  for (int i = 0; i < times; i++) {
    digitalWrite(pin, HIGH);
    delay(delayMs);
    digitalWrite(pin, LOW);
    delay(delayMs);
  }
}

String getUidString() {
  String uid = "";

  for (byte i = 0; i < rfid.uid.size; i++) {
    if (rfid.uid.uidByte[i] < 0x10) {
      uid += "0";
    }

    uid += String(rfid.uid.uidByte[i], HEX);

    if (i < rfid.uid.size - 1) {
      uid += ":";
    }
  }

  uid.toUpperCase();
  return uid;
}

bool scanTag() {
  if (!rfid.PICC_IsNewCardPresent()) {
    return false;
  }

  if (!rfid.PICC_ReadCardSerial()) {
    Serial.println("Card detected, but UID could not be read.");
    showScreen("Card read error", "Try again");
    blinkLed(LED_RED, 2, 120);
    return false;
  }

  return true;
}

void finishScan() {
  rfid.PICC_HaltA();
  rfid.PCD_StopCrypto1();
  delay(900);
}

void connectWifi() {
  Serial.println();
  Serial.print("Connecting to Wi-Fi: ");
  Serial.println(WIFI_SSID);

  showScreen("Connecting Wi-Fi", WIFI_SSID);

  WiFi.begin(WIFI_SSID, WIFI_PASSWORD);

  int attempts = 0;

  while (WiFi.status() != WL_CONNECTED && attempts < 30) {
    digitalWrite(LED_WIFI, !digitalRead(LED_WIFI));
    delay(500);
    Serial.print(".");
    attempts++;
  }

  Serial.println();

  if (WiFi.status() == WL_CONNECTED) {
    digitalWrite(LED_WIFI, HIGH);

    Serial.println("Wi-Fi connected.");
    Serial.print("ESP32 IP: ");
    Serial.println(WiFi.localIP());

    showScreen("Wi-Fi connected", WiFi.localIP().toString());
  } else {
    digitalWrite(LED_WIFI, LOW);
    Serial.println("Wi-Fi connection failed.");

    showScreen("Wi-Fi failed", "Check hotspot");
  }
}

String httpGet(String url) {
  if (WiFi.status() != WL_CONNECTED) {
    return "";
  }

  WiFiClientSecure client;
  client.setInsecure();

  HTTPClient http;
  http.begin(client, url);
  http.setTimeout(20000);

  int code = http.GET();
  String response = "";

  Serial.print("GET ");
  Serial.println(url);
  Serial.print("HTTP code: ");
  Serial.println(code);

  if (code > 0) {
    response = http.getString();
  }

  http.end();
  return response;
}

String httpPostJson(String url, String jsonBody) {
  if (WiFi.status() != WL_CONNECTED) {
    return "";
  }

  WiFiClientSecure client;
  client.setInsecure();

  HTTPClient http;
  http.begin(client, url);
  http.addHeader("Content-Type", "application/json");
  http.setTimeout(20000);

  int code = http.POST(jsonBody);
  String response = "";

  Serial.print("POST ");
  Serial.println(url);
  Serial.print("Body: ");
  Serial.println(jsonBody);
  Serial.print("HTTP code: ");
  Serial.println(code);

  if (code > 0) {
    response = http.getString();
  }

  http.end();
  return response;
}

void readDeviceState() {
  String url = String(SERVER_BASE_URL) + "/api/device/state?deviceKey=" + DEVICE_KEY;

  String response = httpGet(url);

  if (response.length() == 0) {
    digitalWrite(LED_WIFI, LOW);
    Serial.println("Server not reachable or empty response.");

    showScreen("Server offline", "Check site");
    return;
  }

  digitalWrite(LED_WIFI, HIGH);

  StaticJsonDocument<1024> doc;
  DeserializationError error = deserializeJson(doc, response);

  if (error) {
    Serial.println("Could not parse state JSON.");
    Serial.println(response);

    showScreen("Server error", "Bad response");
    return;
  }

  currentMode = doc["mode"] | "Idle";
  displayLine1 = doc["displayLine1"] | "";
  displayLine2 = doc["displayLine2"] | "";

  Serial.println();
  Serial.println("Device state:");
  Serial.print("Mode: ");
  Serial.println(currentMode);
  Serial.print("Line 1: ");
  Serial.println(displayLine1);
  Serial.print("Line 2: ");
  Serial.println(displayLine2);

  showScreen(displayLine1, displayLine2);
}

void handleScanResponse(String response) {
  if (response.length() == 0) {
    Serial.println("Empty server response.");

    showScreen("Server error", "Empty response");
    blinkLed(LED_RED, 3, 120);
    return;
  }

  StaticJsonDocument<1024> doc;
  DeserializationError error = deserializeJson(doc, response);

  if (error) {
    Serial.println("Could not parse scan response.");
    Serial.println(response);

    showScreen("Response error", "Bad JSON");
    blinkLed(LED_RED, 3, 120);
    return;
  }

  bool success = doc["success"] | false;
  String message = doc["message"] | "";
  String line1 = doc["displayLine1"] | "";
  String line2 = doc["displayLine2"] | "";
  String newMode = doc["mode"] | currentMode;

  currentMode = newMode;

  Serial.println();
  Serial.println("Server response:");
  Serial.print("Success: ");
  Serial.println(success ? "true" : "false");
  Serial.print("Message: ");
  Serial.println(message);
  Serial.print("Line 1: ");
  Serial.println(line1);
  Serial.print("Line 2: ");
  Serial.println(line2);

  showScreen(line1, line2);

  if (success) {
    blinkLed(LED_GREEN, 3, 160);
  } else {
    blinkLed(LED_RED, 3, 160);
  }

  delay(1500);
  readDeviceState();
}

void sendTagScan(String uid) {
  String endpoint = "";

  if (currentMode == "Assign") {
    endpoint = "/api/device/assign-scan";
  } else if (currentMode == "Attendance") {
    endpoint = "/api/device/attendance-scan";
  } else {
    Serial.println("Device is idle. Scan ignored.");

    showScreen("Device idle", "Scan ignored");
    blinkLed(LED_RED, 2, 120);
    return;
  }

  String url = String(SERVER_BASE_URL) + endpoint;

  String body = "{";
  body += "\"deviceKey\":\"";
  body += DEVICE_KEY;
  body += "\",";
  body += "\"uid\":\"";
  body += uid;
  body += "\"";
  body += "}";

  Serial.println();
  Serial.print("Scanned UID: ");
  Serial.println(uid);

  showScreen("Scanning tag", uid);

  String response = httpPostJson(url, body);
  handleScanResponse(response);
}

void sendSkip() {
  if (currentMode != "Assign") {
    Serial.println("Skip ignored. Device is not in assignment mode.");

    showScreen("Skip ignored", "Not assign mode");
    blinkLed(LED_RED, 2, 120);
    return;
  }

  String url = String(SERVER_BASE_URL) + "/api/device/skip";

  String body = "{";
  body += "\"deviceKey\":\"";
  body += DEVICE_KEY;
  body += "\"";
  body += "}";

  Serial.println();
  Serial.println("Sending skip request...");

  showScreen("Skipping", "Current student");

  String response = httpPostJson(url, body);
  handleScanResponse(response);
}

void checkSkipButton() {
  bool state = digitalRead(SKIP_BUTTON_PIN);

  if (lastSkipState == HIGH && state == LOW) {
    unsigned long now = millis();

    if (now - lastSkipPress > 700) {
      lastSkipPress = now;
      sendSkip();
    }
  }

  lastSkipState = state;
}

void setup() {
  Serial.begin(115200);
  delay(500);

  pinMode(LED_GREEN, OUTPUT);
  pinMode(LED_RED, OUTPUT);
  pinMode(LED_WIFI, OUTPUT);
  pinMode(SKIP_BUTTON_PIN, INPUT_PULLUP);

  ledsOff();
  digitalWrite(LED_WIFI, LOW);

  Wire.begin(I2C_SDA, I2C_SCL);

  if (!display.begin(SSD1306_SWITCHCAPVCC, OLED_ADDRESS)) {
    Serial.println("OLED not found");
    digitalWrite(LED_RED, HIGH);

    while (true) {
      delay(1000);
    }
  }

  showScreen("Smart Attendance", "Starting...");

  Serial.println();
  Serial.println("Smart Attendance ESP32 RFID Client");

  SPI.begin(18, 19, 23, RFID_SS_PIN);
  rfid.PCD_Init();

  delay(300);

  byte version = rfid.PCD_ReadRegister(rfid.VersionReg);

  Serial.print("RC522 version: 0x");
  Serial.println(version, HEX);

  if (version == 0x00 || version == 0xFF) {
    Serial.println("RC522 not detected. Check wiring.");

    showScreen("RFID error", "Check wiring");
    digitalWrite(LED_RED, HIGH);

    while (true) {
      delay(1000);
    }
  }

  Serial.println("RC522 ready.");

  showScreen("RFID ready", "Connecting Wi-Fi");

  connectWifi();
  readDeviceState();
}

void loop() {
  if (WiFi.status() != WL_CONNECTED) {
    digitalWrite(LED_WIFI, LOW);
    connectWifi();
  }

  if (millis() - lastStateCheck > stateCheckInterval) {
    lastStateCheck = millis();
    readDeviceState();
  }

  checkSkipButton();

  if (scanTag()) {
    String uid = getUidString();
    sendTagScan(uid);
    finishScan();
  }
}