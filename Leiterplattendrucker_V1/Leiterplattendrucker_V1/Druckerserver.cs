using lpd_ansteuerung;
using Microsoft.VisualBasic;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.Unicode;
using System.Threading.Tasks;
using System.Windows;
using static System.Net.Mime.MediaTypeNames;

namespace gerber2coordinatesTEST
{
    public class Druckerserver
    {
        private HttpListener Httplistener;
        public string COMPort = "";


        public string ipAddress = "";
        public int port = 80;


        public Druckerserver(string ipAddress, int port, string COMPort)
        {
            this.ipAddress = ipAddress;
            this.port = port;
            this.COMPort = COMPort;
            Httplistener = new HttpListener();
            Httplistener.Prefixes.Add($"http://{ipAddress}:{port}/");
        }

        /// <summary>
        /// Httpserver starten
        /// </summary>
        public void Start()
        {
            Httplistener.Start(); //Bei Fehler Visual Studio als Admin starten

            Task.Run(() => AcceptRequests());
        }

        /// <summary>
        /// Funktion zum aktzeptieren mehrerer Anfragen gleichzeitig
        /// </summary>
        /// <returns></returns>
        private async Task AcceptRequests()
        {
            while (true)
            {
                HttpListenerContext context = await Httplistener.GetContextAsync();
                Task.Run(() => HandleRequest(context));
            }
        }

        /// <summary>
        /// Die einzelnen Anfragen werden verarbeitet
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        private async Task HandleRequest(HttpListenerContext context)
        {
            string responseString = "";

            //Response auf UTF-8 festlegen
            context.Response.AddHeader("content-type", "text/html; charset=utf-8");

            if (context.Request.HttpMethod == "POST")
            {

            }
            else if (context.Request.HttpMethod == "GET")
            {
                

                using (Stream body = context.Request.InputStream)
                {
                    using (StreamReader reader = new StreamReader(body, context.Request.ContentEncoding))
                    {
                        string requestBody = await reader.ReadToEndAsync();
                        string action = context.Request.Headers["action"];

                        if (action != null)
                        {
                            switch (action)
                            {
                                case "startprint":
                                    if (!printing)
                                    {
                                        startPrinting(requestBody, COMPort);
                                    }
                                    break;
                                case "pauseprint":
                                    pausePrinting();
                                    break;
                                case "stopprint":
                                    stopPrinting();
                                    break;


                                default:

                                    break;
                            }
                        }
                        else
                        {
                            //zurückgeben der Webpage (Ausgelese aus einem File)
                            responseString = "<!Doctype html><h1>Leiterplattendrucker webpage jö</h1>";
                        }
                    }
                }

                

            }


            if (context.Request.HttpMethod == "GET")
            {
                byte[] buffer = Encoding.UTF8.GetBytes(responseString);
                context.Response.ContentLength64 = buffer.Length;
                using (Stream output = context.Response.OutputStream)
                {
                    await output.WriteAsync(buffer, 0, buffer.Length);
                }
            }

            context.Response.Close();
        }




        private Gerberfileinfo gerberfileinfo;
        private SerialComm serialconn;
        Thread printThread;
        private bool printing = false;
        public void startPrinting(string Gerberfilecontent, string COM)
        {
            gerberfileinfo = new Gerberfileinfo(Gerberfilecontent);
            serialconn = new SerialComm(COM);
            printing = true;

            printThread = new Thread(print);
            printThread.Start();
        }

        private void pausePrinting()
        {
            printing = !printing;
        }

        private void stopPrinting()
        {
            if (printThread != null)
            {
                printThread.Abort();
            }
        }

        private void print()
        {
            Debug.WriteLine(gerberfileinfo.getprintpercentage());
            while (gerberfileinfo.getprintpercentage() != 100)
            {
                if (printing)
                {
                    GerberLine currentline = gerberfileinfo.get_next_line();
                    GerberPoint drivecoords = Gerbertoolbox.getrelativecoords(currentline.startpoint, currentline.endpoint);

                    Debug.WriteLine("X: " + drivecoords.X + "\nY: " + drivecoords.Y + "\nPaint: " + currentline._paint);
                    //An Arduino senden
                    serialconn.driveXY((int)drivecoords.X, (int)drivecoords.Y, currentline._paint);
                }
            }

            serialconn.close();
            printThread.Join();
            
            //Zum Startpunkt fahren??

        }
    }
}