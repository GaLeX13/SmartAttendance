#include <Adafruit_Fingerprint.h>

#define FINGERPRINT_RX 16
#define FINGERPRINT_TX 17

#define LED_GREEN 25
#define LED_RED 26
#define LED_WHITE 13

#define ENROLL_BUTTON 14

HardwareSerial fingerprintSerial(2);
Adafruit_Fingerprint fingerprint(&fingerprintSerial);

uint16_t nextFingerprintId = 1;
bool previousButtonState = HIGH;

void setLeds(bool whiteState, bool greenState, bool redState)
{
  digitalWrite(LED_WHITE, whiteState ? HIGH : LOW);
  digitalWrite(LED_GREEN, greenState ? HIGH : LOW);
  digitalWrite(LED_RED, redState ? HIGH : LOW);
}

void showReady()
{
  setLeds(true, false, false);
}

void showSuccess()
{
  setLeds(false, true, false);
  delay(1200);
  showReady();
}

void showError()
{
  setLeds(false, false, true);
  delay(1200);
  showReady();
}

void waitForFingerRemoval()
{
  while (fingerprint.getImage() != FINGERPRINT_NOFINGER)
  {
    delay(100);
  }

  delay(300);
}

uint8_t waitForFinger()
{
  uint8_t result;

  while (true)
  {
    result = fingerprint.getImage();

    if (result == FINGERPRINT_OK)
      return FINGERPRINT_OK;

    if (result != FINGERPRINT_NOFINGER)
      return result;

    delay(100);
  }
}

bool enrollFingerprint(uint16_t id)
{
  Serial.println();
  Serial.print("Starting enrollment for local ID ");
  Serial.println(id);
  Serial.println("Place finger on the sensor.");

  setLeds(true, false, false);

  uint8_t result = waitForFinger();

  if (result != FINGERPRINT_OK)
  {
    Serial.println("Could not capture the first fingerprint image.");
    showError();
    return false;
  }

  result = fingerprint.image2Tz(1);

  if (result != FINGERPRINT_OK)
  {
    Serial.println("Could not process the first fingerprint image.");
    showError();
    waitForFingerRemoval();
    return false;
  }

  Serial.println("First scan completed.");
  Serial.println("Remove finger.");

  waitForFingerRemoval();

  Serial.println("Place the same finger again.");

  result = waitForFinger();

  if (result != FINGERPRINT_OK)
  {
    Serial.println("Could not capture the second fingerprint image.");
    showError();
    return false;
  }

  result = fingerprint.image2Tz(2);

  if (result != FINGERPRINT_OK)
  {
    Serial.println("Could not process the second fingerprint image.");
    showError();
    waitForFingerRemoval();
    return false;
  }

  result = fingerprint.createModel();

  if (result == FINGERPRINT_ENROLLMISMATCH)
  {
    Serial.println("The two scans do not match.");
    showError();
    waitForFingerRemoval();
    return false;
  }

  if (result != FINGERPRINT_OK)
  {
    Serial.println("Could not create fingerprint model.");
    showError();
    waitForFingerRemoval();
    return false;
  }

  result = fingerprint.storeModel(id);

  if (result != FINGERPRINT_OK)
  {
    Serial.println("Could not store fingerprint.");
    showError();
    waitForFingerRemoval();
    return false;
  }

  Serial.print("Fingerprint stored successfully with local ID ");
  Serial.println(id);

  waitForFingerRemoval();
  showSuccess();

  return true;
}

void identifyFingerprint()
{
  uint8_t result = fingerprint.getImage();

  if (result == FINGERPRINT_NOFINGER)
    return;

  if (result != FINGERPRINT_OK)
  {
    Serial.println("Fingerprint image capture error.");
    showError();
    return;
  }

  result = fingerprint.image2Tz();

  if (result != FINGERPRINT_OK)
  {
    Serial.println("Fingerprint image could not be processed.");
    showError();
    waitForFingerRemoval();
    return;
  }

  result = fingerprint.fingerFastSearch();

  if (result == FINGERPRINT_OK)
  {
    Serial.println();
    Serial.println("Fingerprint recognized.");

    Serial.print("Local ID: ");
    Serial.println(fingerprint.fingerID);

    Serial.print("Confidence: ");
    Serial.println(fingerprint.confidence);

    waitForFingerRemoval();
    showSuccess();
    return;
  }

  if (result == FINGERPRINT_NOTFOUND)
  {
    Serial.println();
    Serial.println("Fingerprint not recognized.");

    waitForFingerRemoval();
    showError();
    return;
  }

  Serial.println("Fingerprint search error.");
  waitForFingerRemoval();
  showError();
}

void setup()
{
  Serial.begin(115200);

  pinMode(LED_GREEN, OUTPUT);
  pinMode(LED_RED, OUTPUT);
  pinMode(LED_WHITE, OUTPUT);
  pinMode(ENROLL_BUTTON, INPUT_PULLUP);

  setLeds(false, false, false);

  Serial.println();
  Serial.println("Smart Attendance");
  Serial.println("Fingerprint demonstration");
  Serial.println("Starting sensor...");

  fingerprintSerial.begin(
    57600,
    SERIAL_8N1,
    FINGERPRINT_RX,
    FINGERPRINT_TX
  );

  delay(1500);

  if (!fingerprint.verifyPassword())
  {
    Serial.println("Fingerprint sensor not detected.");
    setLeds(false, false, true);

    while (true)
    {
      delay(1000);
    }
  }

  fingerprint.getParameters();
  fingerprint.getTemplateCount();

  nextFingerprintId = fingerprint.templateCount + 1;

  Serial.println("Fingerprint sensor detected.");

  Serial.print("Sensor capacity: ");
  Serial.println(fingerprint.capacity);

  Serial.print("Stored fingerprints: ");
  Serial.println(fingerprint.templateCount);

  Serial.print("Next local ID: ");
  Serial.println(nextFingerprintId);

  Serial.println();
  Serial.println("Press the button to enroll a fingerprint.");
  Serial.println("Place a finger on the sensor to identify it.");

  showReady();
}

void loop()
{
  bool currentButtonState = digitalRead(ENROLL_BUTTON);

  if (previousButtonState == HIGH && currentButtonState == LOW)
  {
    delay(30);

    if (digitalRead(ENROLL_BUTTON) == LOW)
    {
      if (nextFingerprintId > fingerprint.capacity)
      {
        Serial.println("Fingerprint database is full.");
        showError();
      }
      else
      {
        if (enrollFingerprint(nextFingerprintId))
        {
          fingerprint.getTemplateCount();
          nextFingerprintId = fingerprint.templateCount + 1;

          Serial.print("Next available local ID: ");
          Serial.println(nextFingerprintId);
        }
      }

      while (digitalRead(ENROLL_BUTTON) == LOW)
      {
        delay(20);
      }
    }
  }

  previousButtonState = currentButtonState;

  identifyFingerprint();

  delay(80);
}