using Leiterplattendrucker_V1;
using lpd_ansteuerung;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;

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
                                        if (startPrinting())
                                        {
                                            logtoconsole("Drucker: Drucken wird gestartet...");
                                        }
                                        else
                                        {
                                            logtoconsole("---Drucker: FEHLER: Gerberobjekt nicht initialisiert");
                                        }
                                        
                                    }
                                    break;

                                case "pauseprinting":
                                    logtoconsole("Drucker: Drucken wird pausiert...");
                                    pausePrinting();
                                    break;

                                case "stopprinting":
                                    logtoconsole("Drucker: Drucken wird gestoppt");
                                    stopPrinting();
                                    break;

                                case "initgerberfile":
                                    logtoconsole("Drucker: Drucker wird initialisiert");
                                    initPrinting(requestBody, COMPort);
                                    break;

                                default:
                                    logtoconsole($"Webserver: Fehler - Ungültige Aktion: {action}");
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
                                    
                                    break;

                                case "getgerberpreview":
                                    if (gerberfileinfo != null)
                                    {
                                        responseString = getpreview();
                                    }
                                    else
                                    {
                                        responseString = "Keine Preview";
                                    }
                                    break;

                                case "isprinting":
                                    //todo: flo bitte coole logik macha dia segt ob da drucker am drucken isch oder ned
                                    responseString = "false";
                                    break;

                                default:
                                    responseString = "0";
                                    break;
                            }
                        }
                        else
                        {
                            
                            //zurückgeben der Webpage (Ausgelesen aus einem File), je nachdem was angefragt wird
                            string contenttypeHeader = "";
                            int statuscode = 200;
                            bool picture = false;

                            //Bytearray, damit auch Bilder richtig übertragen werden
                            byte[] response;
                            (response, contenttypeHeader, statuscode, picture) = Website.getResponse(context.Request.Url.AbsolutePath);

                            context.Response.Headers.Add("Content-Type", contenttypeHeader);
                            context.Response.StatusCode = statuscode;

                            using (Stream output = context.Response.OutputStream)
                            {
                                context.Response.ContentLength64 = response.Length;
                                await output.WriteAsync(response, 0, response.Length);
                                context.Response.Close();
                                goto Finish;
                            }

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

            Finish: 
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
            logtoconsole(serialconn.ToString());


        }

        public string getpreview()
        {
            return gerberfileinfo.getlinelist_as_json();
        }

        /// <summary>
        /// Serial connection schließen um Fehler beim nächsten start zu vermeiden
        /// </summary>
        public void stopSerialConnection()
        {
            if(serialconn != null)
            {
                serialconn.close(); 
            }
            
        }


        public bool startPrinting()
        {
            if (gerberfileinfo != null)
            {
                serialconn.driveto00();
                printing = true;

                printThread = new Thread(print);
                printThread.Start();
                return true;
            }
            return false;
        }

        private bool pausePrinting()
        {
            printing = !printing;
            return printing;
        }

        private bool stopPrinting()
        {
            if (printThread != null)
            {
                stopprinttoken.Cancel();
                return true;
            }
            return false;
        }

        private void endprint()
        {
            //Druckkopf zum Startpunkt fahren
            serialconn.driveto00();

            logtoconsole("Drucker: Druck fertig, Objekte werden geleert");

            serialconn.close();
            printing = false;
            gerberfileinfo = null;
            serialconn = null;
            
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
                }
                else
                {
                    //Verhindern von unnötiger CPU - Auslastung
                    Thread.Sleep(500);
                }
            }

            endprint();

        }


        public static void logtoconsole(string message)
        {
            Console.WriteLine(DateTime.Now.Hour + ":" + DateTime.Now.Minute + ":" + DateTime.Now.Second + ": " + message);
        }

    }
}