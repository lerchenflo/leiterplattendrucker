using Leiterplattendrucker_V1;
using lpd_ansteuerung;
using Microsoft.VisualBasic;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mime;
using System.Reflection.Metadata;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Text.Unicode;
using System.Threading.Tasks;
using System.Windows;
using static System.Collections.Specialized.BitVector32;
using static System.Net.Mime.MediaTypeNames;

namespace gerber2coordinatesTEST
{
    public class Druckerserver
    {
        private HttpListener Httplistener;
        public string COMPort = "";
        private Website website;


        public string ipAddress = "";
        public int port = 80;


        public Druckerserver(string ipAddress, int port, string COMPort)
        {
            this.ipAddress = ipAddress;
            this.port = port;
            this.COMPort = COMPort;
            Httplistener = new HttpListener();
            Httplistener.Prefixes.Add($"http://{ipAddress}:{port}/");

            website = new Website();
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


            //Response auf UTF-8 festlegen (Für Umlaute)
            context.Response.AddHeader("content-type", "text/html; charset=utf-8");

            //Response Header damit der Browser die Response Aktzeptiert:
            context.Response.Headers.Add("Access-Control-Allow-Origin", "*");
            context.Response.Headers.Add("Access-Control-Allow-Headers", "*");
            context.Response.Headers.Add("Access-Control-Expose-Headers", "*");


            string action = context.Request.Headers["action"];

            if (context.Request.HttpMethod == "POST")
            {
                using (Stream body = context.Request.InputStream)
                {
                    using (StreamReader reader = new StreamReader(body, context.Request.ContentEncoding))
                    {
                        Console.WriteLine("Neuer Post");
                        string requestBody = await reader.ReadToEndAsync();

                        if (action != null)
                        {
                            switch (action)
                            {
                                case "startprinting":
                                    if (!printing)
                                    {
                                        Console.WriteLine("Drucker: Drucken wird gestartet...");
                                        startPrinting();
                                    }
                                    break;
                                case "pauseprinting":
                                    Console.WriteLine("Drucker: Drucken wird pausiert...");
                                    pausePrinting();
                                    break;
                                case "stopprinting":
                                    Console.WriteLine("Drucker: Drucken wird gestoppt");
                                    stopPrinting();
                                    break;
                                case "initgerberfile":
                                    Console.WriteLine("Drucker: Drucker wird initialisiert");
                                    initPrinting(requestBody, COMPort);

                                    break;

                                default:

                                    break;
                            }
                        }
                        
                    }
                }
            }
            else if (context.Request.HttpMethod == "GET")
            {
                using (Stream body = context.Request.InputStream)
                {
                    using (StreamReader reader = new StreamReader(body, context.Request.ContentEncoding))
                    {
                        string requestBody = await reader.ReadToEndAsync();

                        //Website übergibt eine aktion
                        if (action != null)
                        {
                            switch (action)
                            {
                                case "getprintpercentage":
                                    if (gerberfileinfo != null)
                                    {
                                        responseString = Convert.ToString(gerberfileinfo.getprintpercentage());
                                    }
                                    else
                                    {
                                        responseString = "0";
                                    }
                                    //Console.WriteLine(responseString + "%");
                                    break;
                                case "getgerberpreview":
                                    Console.WriteLine("Drucker: Preview wird geholt");
                                    if (gerberfileinfo != null)
                                    {
                                        responseString = getpreview();
                                    }
                                    else
                                    {
                                        responseString = "Keine Preview";
                                    }
                                    break;
                                
                                default:
                                    responseString = "0";
                                    break;
                            }
                        }
                        else
                        {
                            Console.WriteLine("Website geholt");
                            //zurückgeben der Webpage (Ausgelesen aus einem File), je nachdem was angefragt wird
                            string contenttypeHeader = "";
                            int statuscode = 200;
                            (responseString, contenttypeHeader, statuscode) = website.getResponse(context.Request.Url.AbsolutePath);

                            context.Response.Headers.Add("Content-Type", contenttypeHeader);
                            context.Response.StatusCode = statuscode;
                            Console.WriteLine("Content - Type: " + contenttypeHeader);
                            Console.WriteLine("statuscode: " + statuscode);
                            Console.WriteLine("Requesturl: " + context.Request.Url.AbsolutePath);

                           
                            Console.WriteLine("aaa" + Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Website", context.Request.Url.AbsolutePath));
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
        private Thread printThread;
        private bool printing = false;
        private CancellationTokenSource stopprinttoken = new CancellationTokenSource();


        public void initPrinting(string Gerberfilecontent, string COM)
        {
            gerberfileinfo = new Gerberfileinfo(Gerberfilecontent);
            serialconn = new SerialComm(COM);
        }

        public string getpreview()
        {
            return gerberfileinfo.getlinelist_as_json();
        }


        public void startPrinting()
        {
            if (gerberfileinfo != null)
            {
                printing = true;

                printThread = new Thread(print);
                printThread.Start();
            }
            else
            {
                Console.WriteLine("---Drucker: FEHLER: Gerberobjekt nicht initialisiert");
            }
        }

        private void pausePrinting()
        {
            printing = !printing;
        }

        private void stopPrinting()
        {
            if (printThread != null)
            {
                Console.WriteLine("Drucker: Druck stoppen");
                
                stopprinttoken.Cancel();
            }
        }

        private void endprint()
        {
            Console.WriteLine("Druck fertig, Objekte werden geleert");
            serialconn.close();
            printing = false;
            gerberfileinfo = null;
            serialconn = null;

            //Zum Startpunkt fahren??
        }

        private void print()
        {
            while (gerberfileinfo.getprintpercentage() != 100 && !stopprinttoken.IsCancellationRequested)
            {
                if (printing)
                {
                    GerberLine currentline = gerberfileinfo.get_next_line();
                    GerberPoint drivecoords = Gerbertoolbox.getrelativecoords(currentline._startpoint, currentline._endpoint);

                    Debug.WriteLine("X: " + drivecoords.X + "\nY: " + drivecoords.Y + "\nPaint: " + currentline._paint);
                    //An Arduino senden
                    serialconn.driveXY((int)drivecoords.X, (int)drivecoords.Y, currentline._paint);
                    Console.WriteLine("Drawing_Getline: " + currentline._paint);
                }
            }

            endprint();

        }

        
    }
}