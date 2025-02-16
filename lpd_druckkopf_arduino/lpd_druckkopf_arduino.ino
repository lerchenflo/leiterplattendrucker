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

unsigned long duration;

void setup() {
  Serial.begin(9600); // initailise Serial connection
  Serial.println("Leiterplattendrucker Druckkopfsteuerung");
  //pinMode(TESTLED, OUTPUT); // define Testled as output

  // Set PinMode for ultrasonic sensor
  pinMode(PIN_TRIGGER, OUTPUT);
  pinMode(PIN_ECHO, INPUT);

  // Initialise the IIC communication as slave
  Wire.begin(SLAVE_ADDR);
  Wire.onReceive(receiveEvent);
  Wire.onRequest(requestEvent);
}

void receiveEvent(int bytes) {
  x = Wire.read();    // read one character from the I2C
}

void requestEvent() {
  //Measure the Distance of the ultrasonic sensor and return the data
  Wire.write(measureDistance());
}

int measureDistance(){
  digitalWrite(PIN_TRIGGER, LOW);
  delayMicroseconds(2);

  digitalWrite(PIN_TRIGGER, HIGH);
  delayMicroseconds(10);
  digitalWrite(PIN_TRIGGER, LOW);

  duration = pulseIn(PIN_ECHO, HIGH);
  unsigned int distance = duration * 0.344 / 2;
  Serial.println(distance);
  return distance;
}


void loop() {
  Serial.println(x);
  if(x == 1){
    digitalWrite(TESTLED, HIGH);
  }else if(x == 2){
    digitalWrite(TESTLED, LOW);
  }
   
}
