using gerber2coordinatesTEST;
using System.IO.Ports;

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

            if (Druckerserver.USEUSB_DEBUG)
            {
                sp.Open();
            }
            
        }


        //Testen ob Port geöffnet werden kann
        public static bool testport(string port)
        {
            Druckerserver.logtoconsole($"COMPort {port} wird getestet", 3);
            bool functions = false;

            try
            {
    
                CancellationTokenSource cts = new CancellationTokenSource();

                Thread t = new Thread((uebergebenerport) =>
                {
                    string _port = (string)uebergebenerport;
                    SerialComm s1 = new SerialComm(_port);
                    s1.send("t");
                    while (true)
                    {
                        string rec = s1.readlines(1);
                        if (rec.StartsWith("V"))
                        {
                            Druckerserver.logtoconsole($"Arduinoversion: {rec}", 3);
                            break;
                        }
                       
                    }
                    cts.Cancel();
                    s1.close();
                });
                t.Start(port);
                

                Thread.Sleep(2000);

                if (cts.IsCancellationRequested)
                {
                    return true;
                }
                else
                {
                    return false;
                }

                
                
                //s.close();
            }
            catch (Exception e)
            {
                functions = false;
            }
            
            return functions;
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
            try
            {
                sp.Write(sendMsg);
            }
            catch (Exception e)
            {
                Druckerserver.logtoconsole("Fehler: SerialComm: " + e.Message, 1);
            }
            
        }

        public override string ToString()
        {
            return recMessage;
        }

        public void driveXY(int x, int y, bool draw)
        {
            int travel_height = 5;//mm 
            string z_dir_up = "b";
            string z_dir_down = "f";

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
            }

            //drive both directions
            sendCommand("b", Math.Abs(x), dirX, Math.Abs(y), dirY);

        }

        public void driveto00()
        {
            //TODO: Testen
            sendCommand("0", 0, "0", 0, "0");
        }

        public void sendCommand(string motor, int mm1, string dir1, int mm2, string dir2)
        {
            string cmd = motor + mm1.ToString("D5") + dir1 + mm2.ToString("D5") + dir2;
            send(cmd);

            while (true) //Polling for mc to be ready
            {
                string red = read();
                if (red.StartsWith("finish"))
                {
                    break;
                }
            }

        }
    }
}
