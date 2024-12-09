using gerber2coordinatesTEST;
using lpd_ansteuerung;
using System.Net;
using System.Net.Sockets;

namespace Leiterplattendrucker_V1
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Verfügbare COM - Ports:");

            //Ausgeben aller verfügbaren Ports, an denen der Arduino Nano angeschlossen sein könntr
            string[] comPorts = SerialComm.getAvalibablePorts();
            for (int i = 0; i < comPorts.Length; i++)
            {
                Console.WriteLine($"{i} - {comPorts[i]}");
            }

            
            int comportindex = 0;
            while (true)
            {
                try
                {
                    Console.WriteLine("\nCOM - Port Nummer mit verbundenem Arduino eingeben:");
                    comportindex = Convert.ToInt32(Console.ReadLine());

                    //Wenn COMport nicht funktioniert
                    //bool functions = SerialComm.testport(comPorts[comportindex]);

                    bool functions = true; //Für debugging
                    if (!functions)
                    {
                        throw new Exception("COMPort konnte nicht geöffnet werden");
                    }
                    else
                    {
                        break;
                    }

                    
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }

            



            //Aktuelle IP herausfinden
            string localIP;
            int localport = 6851;
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                socket.Connect("8.8.8.8", 65530);
                IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                localIP = endPoint.Address.ToString();
            }

            //Druckerserver - Objekt erstellen
            Druckerserver d = new Druckerserver(localIP, localport, comPorts[comportindex]);
            d.Start();

            /*
            using (StreamReader sr = new StreamReader("C:\\Users\\Flo\\OneDrive - HTL-Rankweil\\HTL Rankweil\\DA_Leiterplattendrucker\\code\\Gerber - File to Coordinats\\Gerber_file_test\\gerber_file_test\\test13.gerber"))
            {
                d.initPrinting(sr.ReadToEnd(), "COM10");
                d.startPrinting();
            };
            */

            Console.Title = localIP + ":" + localport;
            Console.WriteLine("\nLokale IP: " + localIP);
            Console.WriteLine("\nServer läuft");

            

            while (true)
            {
                //Arbeitsschleife
            }

        }
    }
}
