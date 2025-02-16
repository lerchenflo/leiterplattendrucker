using Leiterplattendrucker_V1;
using lpd_ansteuerung;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http.Headers;
using System.Text;



namespace gerber2coordinatesTEST
{
    public class Druckerserver
    {
        public static bool USEUSB_DEBUG = false;



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


            //Response Header damit der Browser die Response Akzeptiert:
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
                        string requestBody = await reader.ReadToEndAsync();

                        if (action != null)
                        {
                            
                            switch (action)
                            {
                                case "startprinting":

                                    //Wenn das Drucken noch nicht gestartet wurde
                                    if (!printing)
                                    {
                                        //Wenn der Druck gestartet wurde
                                        if (startPrinting())
                                        {
                                            logtoconsole("Drucker: Drucken wird gestartet...");
                                        }
                                        else //Wenn der Druck nicht gestartet wurde
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

                                //Settings
                                case "settings":
                                    string padfill = context.Request.Headers["setpadfill"];
                                    string polygonfill = context.Request.Headers["setpolygonfill"];
                                    string offsetx = context.Request.Headers["offsetx"];
                                    string offsety = context.Request.Headers["offsety"];
                                    string mirror = context.Request.Headers["mirror"];

                                    setpadfill(Convert.ToDouble(padfill));
                                    setpolygonfill(Convert.ToDouble(polygonfill));
                                    setoffset(Convert.ToDouble(offsetx), Convert.ToDouble(offsety));
                                    setmirror(Convert.ToBoolean(mirror));
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
                                    responseString = printing.ToString();
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

            //Finish block ausführen wenn nicht vorher ausgeführt
            goto Finish;

            Finish: 
                context.Response.Close();
        }




        private Gerberfileinfo? gerberfileinfo = null;
        private SerialComm? serialconn = null;
        private Thread? printThread = null;
        private bool printing = false;
        private CancellationTokenSource stopprinttoken = new CancellationTokenSource();


        public void initPrinting(string Gerberfilecontent, string COM)
        {
            if (serialconn != null)
            {
                serialconn.close();
                serialconn = null;
            }

            gerberfileinfo = new Gerberfileinfo(Gerberfilecontent);
            if (Druckerserver.USEUSB_DEBUG)
            {
                serialconn = new SerialComm(COM);
            }
            
        }


        public void setpadfill(double value)
        {
            if (gerberfileinfo != null)
            {
                
                gerberfileinfo._settings.setpadwidth(value);
            }
            else
            {
                logtoconsole("Setting ungültig, Gerberfile nicht initialisiert", 3);
            }
        }

        public void setpolygonfill(double value)
        {
            if (gerberfileinfo != null)
            {
                
                gerberfileinfo._settings.setpolygoninfill(value);
            }
            else
            {
                logtoconsole("Setting ungültig, Gerberfile nicht initialisiert", 3);
            }
        }

        public void setoffset(double x, double y)
        {
            if (gerberfileinfo != null)
            {

                gerberfileinfo._settings.setoffset(x, y);
            }
            else
            {
                logtoconsole("Setting ungültig, Gerberfile nicht initialisiert", 3);
            }
        }

        public void setmirror(bool value)
        {
            if (gerberfileinfo != null)
            {

                gerberfileinfo._settings.setmirror(value);
            }
            else
            {
                logtoconsole("Setting ungültig, Gerberfile nicht initialisiert", 3);
            }
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
            if (gerberfileinfo != null && !printing)
            {
                
                printing = true;
                if (USEUSB_DEBUG)
                {
                    serialconn.driveto00();
                }
                

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
            if (USEUSB_DEBUG)
            {
                serialconn.driveto00();
            }

            logtoconsole("Drucker: Druck fertig, Objekte werden geleert");

            if (USEUSB_DEBUG)
            {
                serialconn.close();
            }
            
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
                    logtoconsole("Druckfortschritt: " + gerberfileinfo.getprintpercentage(), 3);
                    GerberLine currentline = gerberfileinfo.get_next_line();
                    GerberPoint drivecoords = Gerbertoolbox.getrelativecoords(currentline._startpoint, currentline._endpoint);

                    Debug.WriteLine("X: " + drivecoords.X + "\nY: " + drivecoords.Y + "\nPaint: " + currentline._paint);
                    //An Arduino senden

                    if (USEUSB_DEBUG)
                    {
                        serialconn.driveXY((int)drivecoords.X, (int)drivecoords.Y, currentline._paint);
                    }
                }
                else
                {
                    //Verhindern von unnötiger CPU - Auslastung
                    Thread.Sleep(500);
                }
            }

            endprint();

        }

        /// <summary>
        /// Allgemeine Konsolenausgabe mit aktueller Zeit
        /// </summary>
        /// <param name="message">Zu loggender String</param>
        /// <param name="color">0 = Keine Farbe 1 = Rote Farbe 2 = Grüne Farbe</param>
        public static void logtoconsole(string message, int color=0, bool withtime=true)
        {
            switch (color)
            {
                case 1:
                    Console.ForegroundColor = ConsoleColor.Red;
                    break;

                case 2:
                    Console.ForegroundColor = ConsoleColor.Green;
                    break;

                case 3:
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    break;

                default:
                    break;
            }

            if (withtime)
            {
                Console.WriteLine("[" + DateTime.Now.Hour + ":" + DateTime.Now.Minute + ":" + DateTime.Now.Second + "]\t" + message);

            }
            else
            {
                Console.WriteLine("\t\t" + message);
            }
            Console.ResetColor();
        }

        public static void logemptytoconsole(int Lines = 1)
        {
            for (int i = 0; i < Lines; i++)
            {
                Console.WriteLine();
            }

        }

    }
}