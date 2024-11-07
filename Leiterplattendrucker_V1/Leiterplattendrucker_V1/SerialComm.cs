using System;
using System.Diagnostics;
using System.IO.Ports;
using System.Threading;

namespace lpd_ansteuerung
{
    internal class SerialComm
    {
        string port;
        int baudRate = 9600;
        public static SerialPort sp;
        public static bool _continue = true;
        private string endFlag = ">";
        public static string recMessage { get; set; } = "";

        private static bool drawing = true;

        public SerialComm(string _port)
        {
            sp = new SerialPort();
            port = _port;
            sp.PortName = port;
            sp.BaudRate = baudRate;

            sp.Open();
        }
      

        public void close()
        {
            sp.Close();
        }

        public static string[] getAvalibablePorts()
        {
            return SerialPort.GetPortNames();
        }

        public string readtimeout()
        {
            var task = Task.Run(() => readlines(50));
            if (task.Wait(TimeSpan.FromSeconds(0.1)))
            {
                return task.Result;
            }
            else
            {
                return "";
            }
          
        }
        public string read()
        {
            string msg = "";
            try
            {
                msg = sp.ReadLine();
            }
            catch (Exception)
            {
            }
            //Console.WriteLine(msg);
            return msg;
        }
        public string readTillEndflag()
        {
            
            string msg = "";
            string retmsg = "";
            while (msg != (endFlag + "\r"))
            {
                msg = sp.ReadLine();
                if (msg != (endFlag + "\r"))
                {
                    retmsg += msg;
                }
            } 
            
            return retmsg;
        }

        public string readlines(int lines)
        {
            string msg = "";

            for (int i = 0; i < lines; i++)
            {
                try
                {
                    msg += sp.ReadLine();
                }
                catch (Exception)
                {
                }
            }
            

            return msg;
        }

        public void send(string sendMsg)
        {
            sp.Write(sendMsg);
        }

        public override string ToString()
        {
            return recMessage;
        }

        public void driveXY(int x, int y, bool draw)
        {
            int travel_height = 5;//mm 
            string z_dir_up = "f";
            string z_dir_down = "b";

            // Convert 2 values into the Protocol the arduino can understand
            string dirX = "f";
            string dirY = "f";
            if (x > 0) // convert +/- for the protocol
            {
                dirX = "f"; // fals fallsch in b ändern
            }
            else
            {
                dirX = "b"; // fals fallsch in b ändern
            }
            if (y > 0)
            {
                dirY = "f";// fals fallsch in "b" ändern
            }
            else
            {
                dirY = "b";
            }

            // drive x up or down
            Console.WriteLine(drawing);
            Debug.WriteLine("Fabisdrawing: " + drawing);
            if (draw != drawing)
            {
                if (draw)
                {
                    sendCommand("z", travel_height, z_dir_down, 0, "f");
                    drawing = true;
                }
                else
                {
                    sendCommand("z", travel_height, z_dir_up, 0, "f");
                    drawing = false;
                }
                //Thread.Sleep(5000);
            }
            
            //drive both directions
            sendCommand("b", Math.Abs(x), dirX, Math.Abs(y), dirY);

        }

        public void sendCommand(string motor, int mm1, string dir1, int mm2, string dir2)
        {
            Console.WriteLine("Command sendet");
            //string red = read();

            

            string cmd = motor + mm1.ToString("D5") + dir1 + mm2.ToString("D5") + dir2;
            send(cmd);

            while (true) //Polling for mc to be ready
            {
                string red = read();
                Console.WriteLine(red);
                if (red.StartsWith("finish"))
                {
                    Console.WriteLine("Finished driving");
                    break;
                }
            }

        }
    }
}
