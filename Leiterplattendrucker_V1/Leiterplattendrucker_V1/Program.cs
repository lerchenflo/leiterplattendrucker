using System.Net.Sockets;
using System.Net;
using gerber2coordinatesTEST;
using lpd_ansteuerung;

namespace Leiterplattendrucker_V1
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Leiterplattendrucker V1");

            //Ausgeben aller verfügbaren Ports, an denen der Arduino Nano angeschlossen sein könntr
            string[] comPorts = SerialComm.getAvalibablePorts();
            for (int i = 0; i < comPorts.Length; i++)
            {
                Console.WriteLine($"COMPort {i}: {comPorts[i]}");
            }


            //Aktuelle IP herausfinden
            string localIP;
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                socket.Connect("8.8.8.8", 65530);
                IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                localIP = endPoint.Address.ToString();
            }

            //Druckerserver - Objekt erstellen
            Druckerserver d = new Druckerserver(localIP, 6850, "COM3");
            d.Start();

            
            while (true)
            {
                //Arbeitsschleife
            }

        }
    }
}
