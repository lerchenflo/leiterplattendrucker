#include <Stepper.h>
#include <Wire.h>

#define VERSION 1

#define PIN_TRIGGER 12
#define PIN_ECHO 13

#define INT1 8
#define INT1 9
#define INT1 10
#define INT1 11

#define TESTLED 13
#define SLAVE_ADDR 9

int cmd = 0; // public variable for instructions over uart
int x = 0;

void setup() {
  Serial.begin(9600);
  Serial.println("hello world");
  // put your setup code here, to run once:
  pinMode(TESTLED, OUTPUT);
  Wire.begin(SLAVE_ADDR);
  // Attach a function to trigger when something is received.
  Wire.onReceive(receiveEvent);
}

void receiveEvent(int bytes) {
  x = Wire.read();    // read one character from the I2C
}


void loop() {
  Serial.println(x);
  if(x == 1){
    digitalWrite(TESTLED, HIGH);
  }
}
