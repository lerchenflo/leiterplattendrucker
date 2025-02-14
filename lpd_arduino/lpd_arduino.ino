#include <Wire.h>
// Code für Arduino Nano, Motoransteuerung der Stepper motoren, Befehle über uart
#define VERSION 1

// X-Axis Driver
#define X_PUL 2
#define X_DIR 3
#define X_ENA 4

// Y-Axis Driver
#define Y_PUL 5
#define Y_DIR 6
#define Y_ENA 7

// Y-Axis Driver
#define Z_PUL 8
#define Z_DIR 9
#define Z_ENA 10

#define EX1 A2
#define EX2 A3
#define EY1 A4
#define EY2 A5
#define EZ1 11 // A6 & A7 sind keine GPIO pins
#define EZ2 12
// https://edistechlab.com/wp-content/uploads/2023/11/Pinout_arduino_nano.png

#define PREASSURE_SNS A0

// directions to the 0 or home position
#define DIRX0 HIGH 
#define DIRY0 HIGH
#define HOMEX 50000
#define HOMEY 50000

// Constants
#define StepsPerMM 1282 // innacuratly measured
#define SPEED 50 //Period of the pulse in us (lower value = higher speed)
#define TRAVEL_HEIGT 8000 // Steps to drive up or down while driving to 0,0. should be obselete as soon as z endswitches are implemented

#define SLAVE_ADDR 9

String cmd = ""; // public variable for instructions over uart

void init_motor_pins(){
  // Set the Pin Mode for all Inputs and outputs
  pinMode(X_PUL, OUTPUT);
  pinMode(X_DIR, OUTPUT);
  pinMode(X_ENA, OUTPUT);
  pinMode(Y_PUL, OUTPUT);
  pinMode(Y_DIR, OUTPUT);
  pinMode(Y_ENA, OUTPUT);
  pinMode(Z_PUL, OUTPUT);
  pinMode(Z_DIR, OUTPUT);
  pinMode(Z_ENA, OUTPUT);

  // Endschalter hier
  pinMode(EX1, INPUT_PULLUP);
  pinMode(EX2, INPUT_PULLUP);
  pinMode(EY1, INPUT_PULLUP);
  pinMode(EY2, INPUT_PULLUP);
  pinMode(EZ1, INPUT_PULLUP);
  pinMode(EZ2, INPUT_PULLUP);

  // Set the Enable pins low so the motors are free
  digitalWrite(X_ENA, LOW);
  digitalWrite(Y_ENA, LOW);
  digitalWrite(Z_ENA, LOW);
}

void drive2ways(long x_steps, long y_steps,bool x_dir, bool y_dir, int speed){

  //todo: verbesserung minus zahl für andere richtung

  // get the higher number
  long max = x_steps;
  if(y_steps > x_steps){
    max = y_steps;
  }

  // create parameters so we can use them
  long rounds = 1;
  long x_round = 0;
  long y_round = 0;

  // check for minimal steps possible
  for(long i = max; i>0; i--){
    float x_div = float(x_steps)/float(i);
    float y_div = float(y_steps)/float(i);
    if(!(x_div > long(x_div) || y_div > long(y_div))) { // mir hond theoretisch es bestmögliche gfunda
      rounds = i;
      x_round = long(x_div);
      y_round = long(y_div);
      break;
    }
  }

  // set signals for direction and enable
  digitalWrite(X_DIR, x_dir);
  digitalWrite(Y_DIR, y_dir);
  digitalWrite(X_ENA, HIGH);
  digitalWrite(Y_ENA, HIGH);
  
  // drive the calculated steps
  for(long i=0; i < rounds; i++){
     // for safety check if endstop is pressed
    for(long x=0; x < x_round; x++){   //Drive the X steps for this round
      if(digitalRead(EX1) && x_dir || digitalRead(EX2) && !x_dir){ // check if an endstop in the same direction is hit
        digitalWrite(X_PUL,HIGH);
        delayMicroseconds(speed);
        digitalWrite(X_PUL,LOW);
        delayMicroseconds(speed);
      }else{
        Serial.println("Endstop hit");
        digitalWrite(X_ENA, LOW);
        digitalWrite(Y_ENA, LOW);
      }
    }
    for(long y=0; y < y_round; y++){   //Drive the Y steps for this round
      if(digitalRead(EY1) && y_dir || digitalRead(EY2) && !y_dir){ // check if an endstop in the same direction is hit
        digitalWrite(Y_PUL,HIGH);
        delayMicroseconds(speed);
        digitalWrite(Y_PUL,LOW);
        delayMicroseconds(speed);
      }else{
        digitalWrite(X_ENA, LOW);
        digitalWrite(Y_ENA, LOW);
      }
    }
  }

  // Disable the drivers again
  digitalWrite(X_ENA, LOW);
  digitalWrite(Y_ENA, LOW);

}

void drive(int axis, bool dir_val, long steps, long speed){

  // get the correct pins for the axis
  int PUL = 0;
  int DIR = 0;
  int ENA = 0;
  int ES1 = 0; // Endstop 1, 2
  int ES2 = 0;

  if(axis == 0){
    PUL = X_PUL;
    DIR = X_DIR;
    ENA = X_ENA;
    ES1 = EX1; 
    ES2 = EX2;
  }else if(axis == 1)
  {
    PUL = Y_PUL;
    DIR = Y_DIR;
    ENA = Y_ENA;
    ES1 = EY1; 
    ES2 = EY2;
  }else if(axis == 2)
  {
    PUL = Z_PUL;
    DIR = Z_DIR;
    ENA = Z_ENA;
    ES1 = EZ1; 
    ES2 = EZ2;
  }

  // send signals to the motor
  digitalWrite(DIR, dir_val);
  digitalWrite(ENA,HIGH);
  for (long i=0; i<steps; i++)
    {
      if(digitalRead(ES1) && dir_val || digitalRead(ES2) && !dir_val){
        digitalWrite(PUL,HIGH);
        delayMicroseconds(speed);
        digitalWrite(PUL,LOW);
        delayMicroseconds(speed);
      }else{
        Serial.println("end_switch_hit"); // send a message back to the controll programm
        break; // end driving
      }
    }
  digitalWrite(ENA,LOW);

}

void drive_preassure(int stopPreassure){ // drive the z axis down until the set preassure is reached
  int speed = 10; //hardcoded speed
  // send signals to the motor
  digitalWrite(Z_DIR, LOW); // drive down
  digitalWrite(Z_ENA,HIGH);
  
  while(analogRead(PREASSURE_SNS) < stopPreassure) // check if wanted preassure is reached
    {
      if(true){ // endstop placeholder
        digitalWrite(Z_PUL,HIGH);
        delayMicroseconds(speed);
        digitalWrite(Z_PUL,LOW);
        delayMicroseconds(speed);
      }else{
        Serial.println("end stop reached");
        break; // end driving
      }
    }
    Serial.println("preassure reached"); // send a message back to the controll programm
    digitalWrite(Z_ENA,LOW);
}

// todo
void drive_distance(int stopPreassure){ // drive the z axis down according to the distande the ultrasonic sensor is reading
  int speed = 10; //hardcoded speed
  // send signals to the motor
  digitalWrite(Z_DIR, LOW); // drive down
  digitalWrite(Z_ENA,HIGH);

  //dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd
  
  while(analogRead(PREASSURE_SNS) < stopPreassure) // check if wanted preassure is reached
    {
      if(true){ // endstop placeholder
        digitalWrite(Z_PUL,HIGH);
        delayMicroseconds(speed);
        digitalWrite(Z_PUL,LOW);
        delayMicroseconds(speed);
      }else{
        Serial.println("end stop reached");
        break; // end driving
      }
    }
    Serial.println("preassure reached"); // send a message back to the controll programm
    digitalWrite(Z_ENA,LOW);
}



void zero_pos(int speed){  //drive to 0,0 position until hitting an endstop
  //drive z up while positioning to 0
  drive(2, true, TRAVEL_HEIGT, speed); // drive z axis up while traveling to 0, 0
  

  // set signals for direction and enable
  digitalWrite(X_DIR, DIRX0);
  digitalWrite(Y_DIR, DIRY0);
  digitalWrite(X_ENA, HIGH);
  digitalWrite(Y_ENA, HIGH);

  bool x_reached = false;
  bool y_reached = false;
  
  while(!x_reached || !y_reached){ // drive until both endstops are reached
    if(digitalRead(EX1) && DIRX0 || digitalRead(EX2) && !DIRX0){ // check if x endstop is reached
      digitalWrite(X_PUL,HIGH); 
    }
    else{
      digitalWrite(X_ENA, LOW); // set the enable to low to reduce wired noises
      x_reached = true;
    }
    if(digitalRead(EY1) && DIRY0 || digitalRead(EY2) && !DIRY0){ // check if y endstop is reached
      digitalWrite(Y_PUL,HIGH);
    }else{
      digitalWrite(Y_ENA, LOW); // set the enable to low to reduce wired noises
      y_reached=true;
    }
    delayMicroseconds(speed); // delay 1
    digitalWrite(X_PUL,LOW);  // Low can always be written
    digitalWrite(Y_PUL,LOW);
    delayMicroseconds(speed); // delay 2
  }

  digitalWrite(X_ENA, LOW);
  digitalWrite(Y_ENA, LOW);

  drive(2, false, TRAVEL_HEIGT, speed); // drive z axis back down for debuggin purposes
  Serial.println("Endstops reached");
}

long mmToSteps(int mm){ // convert mm to steps using the "StepsPerMM" constant
  //todo: z-motor vellicht anders
  return long(mm)*StepsPerMM;
}

void setup() {
  init_motor_pins();

  // Initialise Serial Communication
  Serial.begin(9600);
  Serial.println("Leiterplattendrucker Ansteuerung");

  Wire.begin(); // join i2c bus (address optional for master)

}



void loop() {
  
  if(cmd != ""){
    Serial.println(cmd);
    /*
    Protocoll:
    0: x, y, z, b (b for 2 directions), p (preassure), 0 (drive to home position)
    1-5: int mm (max 32767)
    6: f (forward), b (backward)
    7-11: mm for 2. direction (in 2 direction mode)
    12: f (forward), b (backward) for 2. direction (in 2 direction mode)
    */

    // Split the information into Variables
    char axisCmd = cmd[0];
    char dirCmd = cmd[6];
    char dir2Cmd = cmd[12];
    int mm1 = cmd.substring(1,6).toInt();
    int mm2 = cmd.substring(7,12).toInt();

    cmd = ""; //reset the variable
    Serial.println("ready"); // tell the server to send new commands

    // Convert the axis into numbers
    int axis = 0;
    if(axisCmd == 'x'){
      axis = 0;
      Serial.println("Driving axis X");
    }
    else if(axisCmd == 'y'){
      axis = 1;
      Serial.println("Driving axis Y");
    }
    else if(axisCmd == 'z'){
      axis = 2;
      Serial.println("Driving axis Z");
    }
    else if(axisCmd == 'b'){
      axis = 3;
      Serial.println("Driving 2 directions");
    }else if(axisCmd == 'p'){ // drive to set pressure
      axis = 4;
      Serial.println("Driving z to preassure");
    }else if (axisCmd == '0'){
      axis = 5;
      //Drive to startpoint (Absolute Werte vom Startwert müssen irgendwo gespeichert sein)
      zero_pos(30); // drive to 0 enstops with higher speed
      //drive2ways(HOMEX, HOMEY, !DIRX0, !DIRY0, SPEED); //drive to the home position
    }else if(axisCmd == 't'){
      delay(100);
      Serial.print("V:");
      Serial.println(VERSION);
    }else if(axisCmd == 'e'){ // code vor testing purposes, e = led ein
      Serial.println("sending over iic");
      Wire.beginTransmission(SLAVE_ADDR); // transmit to device #9
      Wire.write(1); // sends x
      Wire.endTransmission(); // stop transmitting
      delay(500);

    }else if(axisCmd == 'a'){ // code vor testing purposes, a = led aus
      Serial.println(0);
    }

    // Convert text into boolean values for the driving directions
    bool dir = LOW;
    bool dir2 = LOW;
    if(dirCmd == 'b'){
      dir = HIGH;
    }else{
      dir = LOW;
    }
    if(dir2Cmd == 'b'){
      dir2 = HIGH;
    }else{
      dir2 = LOW;
    }
  
    //Debugging outputs
    /*
    Serial.println(mm1);
    Serial.println(mm2);
    Serial.println(dirCmd);
    Serial.println(dir2Cmd);
    */
    // run the Motor driving functions
    if(axis < 3){ // axis x,y,z
      drive(axis, dir, mmToSteps(mm1), SPEED);
    }else if(axis == 3){ // Drive in 2 directions
      drive2ways(mmToSteps(mm1), mmToSteps(mm2), dir, dir2, SPEED);
    }else if(axis == 4){
      drive_preassure(100); //drive the z-axis until a certain preassure is reached
    }

    Serial.println("finish"); // send finish so the steering pc knows that the move is finished
  }
}

void serialEvent() {
  if (Serial.available() > 0) // Polling for Command over serial
  {
    cmd = Serial.readString(); //read the avaliable command over serial
  }
}
