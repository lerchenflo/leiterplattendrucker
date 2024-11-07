using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Security.Cryptography.Xml;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using static System.Net.Mime.MediaTypeNames;

namespace gerber2coordinatesTEST
{
    public class Gerberfileinfo
    {
        //Klasse für die Informationen, welche aus dem Gerber File ausgelesen wurden.


        /*
         * Grundsätzliche Informationen zum Gerberfile:
         * 
         * Zeilen, die mit "%" starten und mit "*% aufhören sind Kommentaare
         * 
         * Koordinaten: Es sind immer Punkte angegeben, mit X, Y und D. X und Y sind koordinaten,
         * bei KiCad mit *e-6 für die Koordinate in mm.
         * 
         * 
         * 
         */






        public string _gerberfilecontent { get; set; } = "";

        public string _unit { get; set; } = "none";
        public List<GerberLine> _lines { get; set; } = new List<GerberLine>();


        public Gerberfileinfo(string GerberfileContent)
        {
            _gerberfilecontent = GerberfileContent;

            //Einheit herausfinden
            getunit();

            //Kommentare aus dem Gerberfile entfernen
            _gerberfilecontent = remove_comments(GerberfileContent);

            //Aus dem Gerberfile die Linien auslesen
            _lines = converttogerberlines(GerberfileContent);

            //Linien nach Reihenfolge Anordnen
            sortlines();

            //Linien richtig drehen, damit der Startpunkt beim ende der vorherigen linie ist und der Drucker durchfahren kann
            turnlines();

            //Verbindungslinien für den Drucker hinzufügen
            calculateroute();
        }



        /// <summary>
        /// Unit aus dem Gerberfile auslesen
        /// </summary>
        private void getunit()
        {
            int startindex = _gerberfilecontent.IndexOf("unit");
            if (startindex != -1)
            {
                //Unit ist angegeben
                _unit = _gerberfilecontent.Substring(startindex+4).Split(")")[0].Trim();
                Debug.WriteLine("Unit: " + _unit);
            }

        }


        /// <summary>
        /// Alle Kommentaare aus dem Gerberfile entfernen
        /// </summary>
        private string remove_comments(string GerberfileContent) 
        {
            //File in einzelne Zeilen zerlegen
            string[] lines = GerberfileContent.Split(new[] { "\r\n", "\n" }, StringSplitOptions.TrimEntries);

            //Durch jede Zeile iterieren und Kommentare entfernen
            string ReturnString = "";
            for(int i = 0; i < lines.Length; i++)
            {
                if (!lines[i].StartsWith("%") && !lines[i].EndsWith("*%"))
                {
                    ReturnString += lines[i] + "\n";
                }
            }

            return ReturnString;
        }



        /// <summary>
        /// Anzahl der Zeilen aus dem File auslesen
        /// </summary>
        public int get_file_lenght(string GerberfileContent) 
        {
            //File bei Zeilenumbrüchen zerlegen und Länge zurückgeben
            string[] lines = GerberfileContent.Split(new[] { "\r\n", "\n" }, StringSplitOptions.TrimEntries);
            
            return lines.Length;
        }



        /// <summary>
        /// Konvertierung der Zeilen aus dem Gerberfile in GerberLine Objekte
        /// </summary>
        /// <param name="GerberfileContent"></param>
        /// <returns></returns>
        private List<GerberLine> converttogerberlines(string GerberfileContent)
        {
            List<GerberLine> gerberLines = new List<GerberLine>();

            //File in einzelne Zeilen zerlegen
            string[] lines = GerberfileContent.Split(new[] { "\r\n", "\n" }, StringSplitOptions.TrimEntries);

            //For - Schleife durch alle Zeilen des Gerbercodes
            for (int i = 0; i < lines.Length - 1; i++)
            {
                
                //Wenn die 2 darauffolgenden Zeilen mit X beginnen (Koordinatenanweisung)
                if (lines[i].StartsWith("X", comparisonType:StringComparison.OrdinalIgnoreCase) && lines[i + 1].StartsWith("X", comparisonType: StringComparison.OrdinalIgnoreCase))
                {

                    //Wenn die Zeilen eine Linie bilden (Zeile 1 mit D01 Start und Zeile 2 mit D02 Stopp)
                    if (lines[i].EndsWith("D02*") && lines[i + 1].EndsWith("D01*"))
                    {
                        //Neues GerberLine - Objekt erstellen und zur Liste hinzufügen
                        gerberLines.Add(new GerberLine(lines[i], lines[i + 1]));
                    }
                    
                }
            }

            return gerberLines;
        }


        /// <summary>
        /// Linien der Reihe nach sortieren, damit der Drucker nicht hin und her muss.
        /// </summary>
        public void sortlines()
        {
            //Start finden
            List<GerberLine> newlines = new List<GerberLine>();

            GerberPoint startpoint = getstartpoint();
            newlines.Add(find_line_with_same_start(startpoint));

            //Startpunkt aus alter liste entfernen
            _lines.Remove(newlines[0]);

            //Durch die restlichen linien cyclen und sortieren
            for (int i = _lines.Count-1; i >= 0; i--)
            {
                GerberLine line = find_line_with_same_start(newlines[newlines.Count-1].startpoint);
                if (line == null)
                {
                    line = find_line_with_same_start(newlines[newlines.Count-1].endpoint);
                }

                if (line != null)
                {
                    newlines.Add(line);
                    _lines.Remove(line);
                }
                else //Keine linie mit gleichem startpunkt gefunden
                {
                    GerberLine line1 = find_nearest_line(newlines[newlines.Count-1].startpoint);
                    if(line1 == null)
                    {
                        line1 = find_nearest_line(newlines[newlines.Count - 1].endpoint);
                    }

                    if (line1 != null)
                    {
                        newlines.Add(line1);
                        _lines.Remove(line1);
                    }
                    else
                    {
                        Debug.WriteLine("Keine Linie gefunden");
                    }

                }
            }

            _lines = newlines;
        }


        /// <summary>
        /// Start und Enden vertauschen, damit die nächste Linie dort anfängt wo die vorherige aufhört
        /// </summary>
        public void turnlines()
        {
            //Erste linie muss richtig starten
            if (getstartpoint() != _lines[0].startpoint)
            {
                _lines[0].switchstartend();
            }

            for (int i = 1; i < _lines.Count; i++)
            {
                //Wenn start und ende nicht zusammenpassen, dann die Linie drehen
                if (_lines[i].startpoint != _lines[i-1].endpoint)
                {
                    _lines[i].switchstartend();
                }

                //Wenn nach dem drehen die Linien nicht zusammenpassen, dann wird der n
                if (_lines[i].startpoint != _lines[i - 1].endpoint)
                {
                    //Wenn die Distanz zwischen ende - start größer ist als ende - ende
                    if (Gerbertoolbox.getdistance(_lines[i-1].endpoint, _lines[i].startpoint) > Gerbertoolbox.getdistance(_lines[i - 1].endpoint, _lines[i].endpoint))
                    {
                        _lines[i].switchstartend();
                    }
                }
            }
        }


        /// <summary>
        /// Linien zwischen den Leiterbahnen erstellen, welche vom Drucker abgefahren werden, ohne gezeichnet zu werden
        /// </summary>
        public void calculateroute()
        {
            //Startpunkt zu erster linie


            //Wenn startpunkt nicht bei 0,0 dann dorthin fahren
            GerberPoint beginpoint = new GerberPoint(0, 0);
            if (!_lines[0].startpoint.Equals(beginpoint))
            {
                Debug.WriteLine("Startpunkt nicht bei 0,0");
                GerberLine startline = new GerberLine(beginpoint, _lines[0].startpoint, false);
                _lines.Insert(0, startline);
            }

            for (int i = 1; i < _lines.Count; i++)
            {
                //Wenn der Startpunkt nicht beim endpunkt der vorherigen linie ist
                GerberPoint endpoint = _lines[i-1].endpoint;
                GerberPoint startpoint = _lines[i].startpoint;

                if (startpoint != endpoint)
                {
                    GerberLine connectionline = new GerberLine(endpoint, startpoint, false);
                    _lines.Insert(i, connectionline);
                }
            }
        }



        /// <summary>
        /// Startpunkt finden, an dem angefangen wird zu zeichnen
        /// </summary>
        /// <returns>Punkt: Startpunkt der ersten linie</returns>
        public GerberPoint getstartpoint()
        {
            int index = 0;
            double distance = 1000000;
            for (int i = 0; i < _lines.Count; i++)
            {
                double[] currentdist = _lines[i].getdistance(0, 0);
                if (currentdist[0] > distance)
                {
                    index = i;
                    distance = currentdist[0];
                }
            }
            if (_lines[index].getdistance(0, 0)[1] == 0)
            {
                return _lines[index].startpoint;
            }
            else
            {
                return _lines[index].endpoint;
            }
        }


        /// <summary>
        /// Linie mit gleichem Startpunkt finden
        /// </summary>
        /// <param name="point"></param>
        /// <returns>Linie mit gleichem Startpunkt</returns>
        public GerberLine find_line_with_same_start(GerberPoint point)
        {
            for (int i = 0; i < _lines.Count; i++)
            {
                double[] distance = _lines[i].getdistance(point.X, point.Y);

                if (distance[0] == 0)
                {
                    return _lines[i];
                }
            }
            return null;
        }


        /// <summary>
        /// Linie finden, die in der nähe liegt. Wird verwendet, wenn keine weiterführende Linie vorhanden ist
        /// </summary>
        /// <param name="point"></param>
        /// <returns>Nächste Linie</returns>
        public GerberLine find_nearest_line(GerberPoint point)
        {
            double shortestdistance = 1000000;
            int shortestdistindex = 0;
            for (int i = 0; i < _lines.Count; i++)
            {
                double[] distance = _lines[i].getdistance(point.X, point.Y);

                if (distance[0] < shortestdistance)
                {
                    shortestdistance = distance[0];
                    shortestdistindex = i;
                    
                }
            }
            Debug.WriteLine($"Index: {shortestdistindex}");
            return _lines[shortestdistindex];
        }


        
        int currentline = 0;
        /// <summary>
        /// Nächste Linie bekommen, wird der Reihe nach ausgegeben
        /// </summary>
        public GerberLine get_next_line()
        {
            int currentline_volatile = currentline;
            currentline++;

            if(currentline_volatile >= _lines.Count)
            {
                return null;
            }

            return _lines[currentline_volatile];
            
        }


        /// <summary>
        /// Aktuellen Druckfortschritt in Prozent bekommen
        /// </summary>
        /// <returns></returns>
        public int getprintpercentage()
        {
            return (int)((double)currentline / _lines.Count * 100);
        }


        /// <summary>
        /// Linien in einem Canvas zeichnen
        /// </summary>
       
        /*
        public void drawlinestocanvas(Canvas cananas, int drawmultiplier)
        {
            cananas.Children.Clear();


            for (int i = 0; i < get_file_lenght(_gerberfilecontent); i++)
            {
                Line l = new Line();

                l.StrokeThickness = 1;
                

                GerberLine line = get_next_line();

                if (line != null)
                {
                    if (line._paint)
                    {
                        l.Stroke = Brushes.Gold;
                    }
                    else
                    {
                        l.Stroke = Brushes.Red;
                    }

                    l.X1 = line.startpoint.X * drawmultiplier;
                    l.Y1 = line.startpoint.Y * drawmultiplier;
                    l.X2 = line.endpoint.X * drawmultiplier;
                    l.Y2 = line.endpoint.Y * drawmultiplier;

                    cananas.Children.Add(l);
                    
                    
                    //Textblock am anfang der Linie
                    TextBlock tb = new TextBlock();
                    tb.Text = i + "";
                    tb.Foreground = Brushes.Blue;
                    tb.FontSize = 7;
                    Canvas.SetLeft(tb, l.X1+4); Canvas.SetTop(tb, l.Y1+2);
                    //Text transformieren, da das Canvas gespiegelt ist
                    TransformGroup transformGroup1 = new TransformGroup();
                    //Spiegeln
                    transformGroup1.Children.Add(new ScaleTransform(-1, 1));
                    //180° Drehen
                    transformGroup1.Children.Add(new RotateTransform(180));
                    tb.RenderTransform = transformGroup1;
                    tb.RenderTransformOrigin = new GerberPoint(0.5, 0.5);
                    
                    cananas.Children.Add(tb);
                    

                    //Beschreibung
                    TextBlock text = new TextBlock
                    {
                        Text = i + "",
                        Foreground = Brushes.Black,
                        FontSize = 10
                    };

                    //Text knapp neben die Mitte setzen
                    Canvas.SetLeft(text, (line.startpoint.X + line.endpoint.X) / 2 * drawmultiplier + 5);
                    Canvas.SetTop(text, (line.startpoint.Y + line.endpoint.Y) / 2 * drawmultiplier);

                    //Text transformieren, da das Canvas gespiegelt ist
                    TransformGroup transformGroup = new TransformGroup();
                    //Spiegeln
                    transformGroup.Children.Add(new ScaleTransform(-1, 1));
                    //180° Drehen
                    transformGroup.Children.Add(new RotateTransform(180));
                    text.RenderTransform = transformGroup;
                    text.RenderTransformOrigin = new GerberPoint(0.5, 0.5);

                    cananas.Children.Add(text);
                }
            }
        }
        */
    }


    public class GerberLine
    {
        //Klasse für Linien, die aus dem Gerberfile entnommen wurden

        public GerberPoint startpoint {  get; set; } = new GerberPoint();
        public GerberPoint endpoint { get; set; } = new GerberPoint();

        //Boolean für Zeichnen und nicht Zeichnen
        public bool _paint { get; set; } = false;
        public bool painted { get; set; } = false;


        //public GerberLine(string X_start, string Y_start, string X_end, string Y_end, bool paint)
        //{
        //    setcoordinates(X_start,Y_start, X_end, Y_end, paint);
        //}
        public GerberLine(GerberPoint startpoint_, GerberPoint endpoint_, bool paint)
        {
            startpoint = startpoint_;
            endpoint = endpoint_;
            _paint = paint;
        }

        public GerberLine(string GerberFileLine1, string GerberFileLine2) 
        {
            if (GerberFileLine1.StartsWith("X") && GerberFileLine1.EndsWith("*"))
            {
                if (GerberFileLine2.StartsWith("X") && GerberFileLine2.EndsWith("*"))
                {
                    double X_start = Gerbertoolbox.getXY(GerberFileLine1)[0];
                    double Y_start = Gerbertoolbox.getXY(GerberFileLine1)[1];

                    double X_end = Gerbertoolbox.getXY(GerberFileLine2)[0];
                    double Y_end = Gerbertoolbox.getXY(GerberFileLine2)[1];

                    bool paint = Gerbertoolbox.getpaint(GerberFileLine2);


                    setcoordinates(X_start, Y_start, X_end, Y_end, paint);
                }
            }
        }

        public void setcoordinates(double X_start, double Y_start, double X_end, double Y_end, bool paint)
        {
            startpoint = new GerberPoint(X_start, Y_start);
            endpoint = new GerberPoint(X_end, Y_end);
            _paint = paint;

            Debug.WriteLine($"Zeile: X1: {X_start} Y1: {Y_start} X2: {X_end} Y2: {Y_end} Paint: {paint}");
        }


        public void switchstartend()
        {
            GerberPoint startpointold = startpoint;
            GerberPoint endpointold = endpoint;

            startpoint = endpointold;
            endpoint = startpointold;
        }


        /// <summary>
        /// Abstand zwischen dem Start / Endpunkt und einem Übergebenen Punkt berechnen
        /// </summary>
        /// <param name="X"></param>
        /// <param name="Y"></param>
        /// <returns>Abstand der Punkte als Double</returns>
        public double[] getdistance(double X, double Y)
        {
            double distancestartX = Math.Abs(startpoint.X - X);
            double distancestartY = Math.Abs(startpoint.Y - Y);

            double distanceendX = Math.Abs(endpoint.X - X);
            double distanceendY = Math.Abs(endpoint.Y - Y);

            //Abstand mithilfe des Satz von pythagoras berechnen
            double distancestart = Math.Sqrt(distancestartX * distancestartX + distancestartY * distancestartY);
            double distanceend = Math.Sqrt(distanceendX * distanceendX + distanceendY * distanceendY);

            if (distancestart > distanceend)
            {
                return new double[] { distanceend, 0};
            }
            else
            {
                return new double[] { distancestart, 1 };
            }
        }

    }

    public class Gerbertoolbox
    {
        public static string getcharsbetween(string Content, string Startchar, string Endchar) 
        {
            //Erstes vorkommen finden
            int startindex = Content.IndexOf(Startchar);
            int endindex = Content.IndexOf(Endchar);

            //Start & Endindex müssen existieren, und der Endindex muss später vorkommen als der Startindex
            if (startindex != -1 && endindex != -1 && endindex > startindex)
            {
                //Den String dazwischen zurückgeben
                return Content.Substring(startindex + 1, endindex - startindex - 1);
            }
            else
            {
                return string.Empty;
            }
        }


        public static double[] getXY(string Line)
        {
            //String aus der Zeile auslesen
            string X_coords = getcharsbetween(Line, "X", "Y");
            string Y_coords = getcharsbetween(Line, "Y", "D");

            //Kommastelle verschieben
            double X = Convert.ToDouble((string)X_coords) * 1e-6;
            double Y = Convert.ToDouble((string)Y_coords) * 1e-6;


            return (new double[] { X, Y });

        }


        public static int getdistance(GerberPoint p1, GerberPoint p2)
        {
            double dx = p2.X - p1.X;
            double dy = p2.Y - p1.Y;
            return (int)Math.Sqrt(dx * dx + dy * dy);
        }


        /// <summary>
        /// Immer die zweite Zeile übergeben!
        /// </summary>
        /// <param name="Line"></param>
        /// <returns>Boolean Paint: Zeichnen oder nicht Zeichnen</returns>
        public static bool getpaint(string Line)
        {
            //Zeichnen
            if (getcharsbetween(Line, "D", "*") == "01")
            {
                return true;
            }
            else if (getcharsbetween(Line, "D", "*") == "02")
            {
                return false;
            }
            return false;
        }


        public static GerberPoint getrelativecoords(GerberPoint p1, GerberPoint p2)
        {
            // Calculate the difference in x and y
            double relativeX = p2.X - p1.X;
            double relativeY = p2.Y - p1.Y;

            return new GerberPoint(relativeX, relativeY);
        }

       
    }

    //Gerberpoint klasse, weil GerberPoint nur in WPF verfügbar ist
    public class GerberPoint
    {
        public double X { get; set; } = 0;
        public double Y { get; set; } = 0;

        public GerberPoint(double x, double y)
        {
            X = x;
            Y = y;
        }

        public GerberPoint() { }
    }
}
