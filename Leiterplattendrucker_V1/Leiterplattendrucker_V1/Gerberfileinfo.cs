using System.Diagnostics;
using System.Text.Json;


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
         * Koordinaten: Es sind immer Punkte angegeben, mit X, Y und D. X und Y sind Koordinaten,
         * bei KiCad mit *e-6 für die Koordinate in mm.
         * 
         * 
         * 
         */



        //Gerberfile als String
        public string _gerberfilecontent { get; set; } = "";

        //Unit des Gerberfiles
        public string _unit { get; set; } = "none";

        //Linien die der Drucker zu fahren hat
        public List<GerberLine> _lines { get; set; } = new List<GerberLine>();

        //Settings für den Druck
        public GerberSettingsList Settings { get; set; } = new GerberSettingsList();

        //Aktuelle linie die der Drucker gerade abarbeitet
        public int currentline = 0;



        public Gerberfileinfo(string GerberfileContent)
        {
            //Gerberfile initialisieren
            Initgerberfile(GerberfileContent);

            //Callback beim ändern einer Setting (Preview muss geupdated werden)
            Settings.setonchangecallback(Initgerberfile, GerberfileContent);
        }



        private void Initgerberfile(string GerberfileContent)
        {
            Debug.WriteLine("Gerberfile init");
            try
            {
                if (GerberfileContent == string.Empty)
                {
                    throw new Exception("Gerberfilecontent leer");
                }
                _gerberfilecontent = GerberfileContent;

                //Einheit herausfinden
                getunit();

                //Kommentare aus dem Gerberfile entfernen
                _gerberfilecontent = remove_comments(GerberfileContent);

                //Aus dem Gerberfile die Linien auslesen
                _lines.AddRange(converttogerberlines(_gerberfilecontent));

                //Polygons füllen
                _lines.AddRange(fillpolygonswithlines(GerberfileContent));

                //Aus dem Gerberfile Pads auslesen
                _lines.AddRange(fillpadsandpoints(_gerberfilecontent));

                //Doppelte Linien entfernen, falls es welche gibt
                removeduplicates();

                //Offsets korrigieren, falls negative Koordinaten dabei sind (Zeichnung auf Druckfläche schieben)
                correctnegativeoffsets();

                //Zeichnung ins Eck schieben
                movetopad();

                //Linien nach Reihenfolge anordnen
                sortlines();

                //Linien richtig drehen, damit der Startpunkt beim Ende der vorherigen linie ist und der Drucker durchfahren kann
                turnlines();

                //Verbindungslinien für den Drucker hinzufügen
                calculateroute();
            }
            catch (Exception e)
            {
                throw new Exception("Fehler beim initialisieren des Files: " + e.StackTrace);
            }
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
                _unit = _gerberfilecontent.Substring(startindex + 4).Split(")")[0].Trim();
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
            for (int i = 0; i < lines.Length; i++)
            {
                //Wenn kein Kommentar
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


        private List<GerberLine> fillpolygonswithlines(string GerberfileContent)
        {
            List<GerberPolygon> polygons = new List<GerberPolygon>();

            string[] lines = GerberfileContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            //Durch alle Linien
            for (int i = 0; i < lines.Length; i++)
            {
                //Wenn ein Polygon beginnt
                if (lines[i].StartsWith("G36"))
                {
                    List<GerberPoint> polygonpoints = new List<GerberPoint>();

                    for (int j = 1; j < lines.Length - i; j++)
                    {
                        //wenn Polygon fertig dann aufhören sonst Eckpunkt hinzufügen
                        if (lines[i + j].StartsWith("G37"))
                        {
                            polygons.Add(new GerberPolygon(polygonpoints));
                            break;
                        }
                        else
                        {
                            if (lines[i + j].StartsWith("X"))
                            {
                                double[] xy = Gerbertoolbox.getXY(lines[i + j]);

                                polygonpoints.Add(new GerberPoint(xy[0], xy[1]));
                            }
                        }
                    }
                }
            }

            //Gerberlines zückgeben
            List<GerberLine> returnlist = new List<GerberLine>();

            double polygonfillrate = Settings.getpolygoninfill();

            for (int i = 0; i < polygons.Count; i++)
            {
                returnlist.AddRange(polygons[i].getgerberlines(polygonfillrate));
            }

            return returnlist;
        }


        private List<GerberLine> fillpadsandpoints(string GerberfileContent)
        {
            string[] lines = GerberfileContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            List<GerberPoint> points = new List<GerberPoint>();

            //Punkte aus dem File auslesen
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].EndsWith("D03*"))
                {
                    points.Add(new GerberPoint(Gerbertoolbox.getXY(lines[i])));
                }
            }

            //Punkte füllen
            double size = Settings.getpadwidth();

            List<GerberLine> returnlist = new List<GerberLine>();

            for (int i = 0; i < points.Count; i++)
            {
                GerberPoint ol = new GerberPoint(Math.Round(points[i].X - size, 2), Math.Round(points[i].Y + size, 2)); //Punkt oben links
                GerberPoint or = new GerberPoint(Math.Round(points[i].X + size, 2), Math.Round(points[i].Y + size, 2)); //Punkt oben rechts
                GerberPoint ul = new GerberPoint(Math.Round(points[i].X - size, 2), Math.Round(points[i].Y - size, 2)); //Punkt unten links
                GerberPoint ur = new GerberPoint(Math.Round(points[i].X + size, 2), Math.Round(points[i].Y - size, 2)); //Punkt unten rechts

                returnlist.Add(new GerberLine(new GerberPoint(ol.X, ol.Y), new GerberPoint(or.X, or.Y)));  // Oben
                returnlist.Add(new GerberLine(new GerberPoint(or.X, or.Y), new GerberPoint(ur.X, ur.Y)));  // Rechts
                returnlist.Add(new GerberLine(new GerberPoint(ur.X, ur.Y), new GerberPoint(ul.X, ul.Y)));  // Unten
                returnlist.Add(new GerberLine(new GerberPoint(ul.X, ul.Y), new GerberPoint(ol.X, ol.Y)));  // Links
                returnlist.Add(new GerberLine(new GerberPoint(ol.X, ol.Y), new GerberPoint(ur.X, ur.Y)));  // Quer
                returnlist.Add(new GerberLine(new GerberPoint(or.X, or.Y), new GerberPoint(ul.X, ul.Y)));  // Quer
            }
            return returnlist;
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
                if (lines[i].StartsWith("X", comparisonType: StringComparison.OrdinalIgnoreCase) && lines[i + 1].StartsWith("X", comparisonType: StringComparison.OrdinalIgnoreCase))
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


        private void removeduplicates()
        {
            var uniqueLines = new List<GerberLine>();

            foreach (var line in _lines)
            {
                if (!uniqueLines.Any(l => (l._startpoint == line._startpoint && l._endpoint == line._endpoint) ||
                                          (l._startpoint == line._endpoint && l._endpoint == line._startpoint)))
                {
                    uniqueLines.Add(line);
                }
            }
            Debug.WriteLine("Doppelte Linien entfernt: " + (_lines.Count - uniqueLines.Count));
            _lines = uniqueLines;
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
            for (int i = _lines.Count - 1; i >= 0; i--)
            {
                GerberLine line = find_line_with_same_start(newlines[newlines.Count - 1]._startpoint);
                if (line == null)
                {
                    line = find_line_with_same_start(newlines[newlines.Count - 1]._endpoint);
                }

                if (line != null)
                {
                    newlines.Add(line);
                    _lines.Remove(line);
                }
                else //Keine linie mit gleichem startpunkt gefunden
                {
                    GerberLine line1 = find_nearest_line(newlines[newlines.Count - 1]._startpoint);
                    if (line1 == null)
                    {
                        line1 = find_nearest_line(newlines[newlines.Count - 1]._endpoint);
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
        /// und die optimalste Verbindung schafft.
        /// </summary>
        public void turnlines()
        {
            //Erste linie muss richtig starten
            GerberPoint startpoint = getstartpoint();
            if (getstartpoint() != _lines[0]._startpoint)
            {
                _lines[0].switchstartend();
            }

            for (int i = 1; i < _lines.Count; i++)
            {
                // Wenn start und ende nicht zusammenpassen, dann die Linie drehen
                if (_lines[i]._startpoint != _lines[i - 1]._endpoint)
                {
                    _lines[i].switchstartend();
                }

                // Wenn nach dem drehen die Linien nicht zusammenpassen, dann wird die nächste Linie so gedreht, dass ihr Startpunkt am nächsten zum Endpunkt der vorherigen Linie liegt
                if (_lines[i]._startpoint != _lines[i - 1]._endpoint)
                {
                    // Wenn die Distanz zwischen ende - start größer ist als ende - ende
                    if (Gerbertoolbox.getdistance(_lines[i - 1]._endpoint, _lines[i]._startpoint) > Gerbertoolbox.getdistance(_lines[i - 1]._endpoint, _lines[i]._endpoint))
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
            // Startpunkt zu erster Linie
            GerberPoint beginpoint = new GerberPoint(0, 0);
            if (!_lines[0]._startpoint.Equals(beginpoint))
            {
                Debug.WriteLine("Startpunkt nicht bei 0,0");
                GerberLine startline = new GerberLine(beginpoint, _lines[0]._startpoint, false);
                _lines.Insert(0, startline);
            }

            for (int i = 1; i < _lines.Count; i++)
            {
                GerberPoint endpoint = _lines[i - 1]._endpoint;
                GerberPoint startpoint = _lines[i]._startpoint;

                if (startpoint != endpoint)
                {
                    // Check if the next line starts where the current line ends
                    bool found = false;
                    for (int j = i + 1; j < _lines.Count; j++)
                    {
                        if (_lines[j]._startpoint == endpoint)
                        {
                            // Swap lines to ensure continuity
                            var temp = _lines[i];
                            _lines[i] = _lines[j];
                            _lines[j] = temp;
                            found = true;
                            break;
                        }
                        else if (_lines[j]._endpoint == endpoint)
                        {
                            // Swap lines and reverse to ensure continuity
                            _lines[j].switchstartend();
                            var temp = _lines[i];
                            _lines[i] = _lines[j];
                            _lines[j] = temp;
                            found = true;
                            break;
                        }
                    }

                    // If no direct continuation is found, add a connection line
                    if (!found)
                    {
                        GerberLine connectionline = new GerberLine(endpoint, startpoint, false);
                        _lines.Insert(i, connectionline);
                    }
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
            double distance = double.MaxValue;

            // Linie am nächsten zum Startpunkt finden
            for (int i = 0; i < _lines.Count; i++)
            {
                double[] currentdist = _lines[i].getdistance(0, 0);
                if (currentdist[0] < distance)
                {
                    index = i;
                    distance = currentdist[0];
                }
            }

            // Startpunkt der Linie herausfinden, welcher näher am Startpunkt ist
            return _lines[index].getdistance(0, 0)[1] == 0 ? _lines[index]._startpoint : _lines[index]._endpoint;
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
            //Debug.WriteLine($"Index: {shortestdistindex}");
            return _lines[shortestdistindex];
        }


        /// <summary>
        /// Nächste Linie bekommen, wird der Reihe nach ausgegeben
        /// </summary>
        public GerberLine get_next_line()
        {
            int currentline_volatile = currentline;
            currentline++;

            if (currentline_volatile >= _lines.Count)
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
            if (_lines.Count > 1)
            {
                return (int)((double)currentline / _lines.Count * 100);
            }
            else
            {
                return 0;
            }
        }



        private void correctnegativeoffsets()
        {
            double dX = 0;
            double dY = 0;

            for (int i = 0; i < _lines.Count; i++)
            {
                GerberPoint offset = _lines[i].getnegativeoffset();

                double offsetx = offset.X;
                double offsety = offset.Y;

                if (offsetx < dX)
                {
                    dX = offsetx;
                }

                if (offsety < dY)
                {
                    dY = offsety;
                }
            }

            dX = Math.Abs(dX);
            dY = Math.Abs(dY);

            //Wenn ein Offset vorhanden ist
            if (dX > 0 || dY > 0)
            {
                Debug.WriteLine($"Offset wird korrigiert: X: {dX} Y: {dY}");
                for (int i = 0; i < _lines.Count; i++)
                {
                    //Offset anwenden
                    _lines[i].correctoffset(dX > 0 ? dX + 2 : 0, dY > 0 ? dY + 2 : 0);
                }
            }
        }


        private void movetopad()
        {
            double Xoffset = 100000;
            double Yoffset = 100000;

            //Offset auf maximal gesetzt, jetzt muss der kleiste Offset gefunden und korrigiert werden
            foreach (GerberLine line in _lines)
            {
                GerberPoint g = line.getpositiveoffset();
                Xoffset = g.X < Xoffset ? g.X : Xoffset;
                Yoffset = g.Y < Yoffset ? g.Y : Yoffset;
            }

            foreach (GerberLine l in _lines)
            {
                l.correctoffset(-Xoffset + 1, -Yoffset + 1);
            }
        }


        public string getlinelist_as_json()
        {
            return JsonSerializer.Serialize(_lines);
        }


        /*
        /// <summary>
        /// Linien in einem Canvas zeichnen
        /// </summary>
        public void drawlinestocanvas(Canvas cananas, int drawmultiplier)
        {
            cananas.Children.Clear();

            for (int i = 0; i < _lines.Count; i++)
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

                    l.X1 = line._startpoint.X * drawmultiplier;
                    l.Y1 = line._startpoint.Y * drawmultiplier;
                    l.X2 = line._endpoint.X * drawmultiplier;
                    l.Y2 = line._endpoint.Y * drawmultiplier;

                    cananas.Children.Add(l);


                    //Textblock am anfang der Linie
                    TextBlock tb = new TextBlock();
                    tb.Text = i + "";
                    tb.Foreground = Brushes.Blue;
                    tb.FontSize = 7;
                    Canvas.SetLeft(tb, l.X1 + 4); Canvas.SetTop(tb, l.Y1 + 2);
                    //Text transformieren, da das Canvas gespiegelt ist
                    TransformGroup transformGroup1 = new TransformGroup();
                    //Spiegeln
                    transformGroup1.Children.Add(new ScaleTransform(-1, 1));
                    //180° Drehen
                    transformGroup1.Children.Add(new RotateTransform(180));
                    tb.RenderTransform = transformGroup1;
                    tb.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);

                    cananas.Children.Add(tb);


                    //Beschreibung
                    TextBlock text = new TextBlock
                    {
                        Text = i + "",
                        Foreground = Brushes.Black,
                        FontSize = 10
                    };

                    //Text knapp neben die Mitte setzen
                    Canvas.SetLeft(text, (line._startpoint.X + line._endpoint.X) / 2 * drawmultiplier + 5);
                    Canvas.SetTop(text, (line._startpoint.Y + line._endpoint.Y) / 2 * drawmultiplier);

                    //Text transformieren, da das Canvas gespiegelt ist
                    TransformGroup transformGroup = new TransformGroup();
                    //Spiegeln
                    transformGroup.Children.Add(new ScaleTransform(-1, 1));
                    //180° Drehen
                    transformGroup.Children.Add(new RotateTransform(180));
                    text.RenderTransform = transformGroup;
                    text.RenderTransformOrigin = new System.Windows.Point(0.5, 0.5);

                    cananas.Children.Add(text);
                }
            }
        }
        */


    }


    public class GerberLine
    {
        //Klasse für Linien, die aus dem Gerberfile entnommen wurden

        public GerberPoint _startpoint { get; set; } = new GerberPoint();
        public GerberPoint _endpoint { get; set; } = new GerberPoint();

        //Boolean für Zeichnen und nicht Zeichnen
        public bool _paint { get; set; } = false;
        public bool _painted { get; set; } = false;


        //public GerberLine(string X_start, string Y_start, string X_end, string Y_end, bool paint)
        //{
        //    setcoordinates(X_start,Y_start, X_end, Y_end, paint);
        //}
        public GerberLine(GerberPoint startpoint_, GerberPoint endpoint_, bool paint = true)
        {
            _startpoint = startpoint_;
            _endpoint = endpoint_;
            _paint = paint;

            roundcoordinates();
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
                    roundcoordinates();
                }
            }
        }

        public void setcoordinates(double X_start, double Y_start, double X_end, double Y_end, bool paint)
        {
            _startpoint = new GerberPoint(X_start, Y_start);
            _endpoint = new GerberPoint(X_end, Y_end);
            _paint = paint;

            roundcoordinates();
        }


        public void switchstartend()
        {
            GerberPoint startpointold = _startpoint;
            GerberPoint endpointold = _endpoint;

            _startpoint = endpointold;
            _endpoint = startpointold;
        }


        /// <summary>
        /// Abstand zwischen dem Start / Endpunkt und einem Übergebenen Punkt berechnen
        /// </summary>
        /// <param name="X"></param>
        /// <param name="Y"></param>
        /// <returns>Abstand der Punkte als Double</returns>
        public double[] getdistance(double X, double Y)
        {
            double distancestartX = Math.Abs(_startpoint.X - X);
            double distancestartY = Math.Abs(_startpoint.Y - Y);

            double distanceendX = Math.Abs(_endpoint.X - X);
            double distanceendY = Math.Abs(_endpoint.Y - Y);

            //Abstand mithilfe des Satz von Pythagoras berechnen
            double distancestart = Math.Sqrt(distancestartX * distancestartX + distancestartY * distancestartY);
            double distanceend = Math.Sqrt(distanceendX * distanceendX + distanceendY * distanceendY);

            if (distancestart > distanceend)
            {
                return new double[] { distanceend, 1 };
            }
            else
            {
                return new double[] { distancestart, 0 };
            }
        }

        public void correctoffset(double dX, double dY)
        {
            _startpoint.X += dX;
            _startpoint.Y += dY;

            _endpoint.X += dX;
            _endpoint.Y += dY;

            roundcoordinates();
        }

        /// <summary>
        /// Maximaler negativer Offset
        /// </summary>
        /// <returns></returns>
        public GerberPoint getnegativeoffset()
        {
            double dXs, dXe, dYs, dYe = 0;


            dYs = _startpoint.Y < 0 ? _startpoint.Y : 0;
            dYe = _endpoint.Y < 0 ? _endpoint.Y : 0;

            dXs = _startpoint.X < 0 ? _startpoint.X : 0;
            dXe = _endpoint.X < 0 ? _endpoint.X : 0;

            return new GerberPoint(Math.MinMagnitude(dXs, dXe), Math.MinMagnitude(dYs, dYe));
        }

        private void roundcoordinates()
        {
            _startpoint.X = Math.Round(_startpoint.X, 2);
            _startpoint.Y = Math.Round(_startpoint.Y, 2);
            _endpoint.X = Math.Round(_endpoint.X, 2);
            _endpoint.Y = Math.Round(_endpoint.Y, 2);
        }

        public GerberPoint getpositiveoffset()
        {
            double dX = 0;
            double dY = 0;

            dX = _startpoint.X > _endpoint.X ? _endpoint.X : _startpoint.X;

            dY = _startpoint.Y > _endpoint.Y ? _endpoint.Y : _startpoint.Y;

            return new GerberPoint(dX, dY);
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

    //Gerberpoint klasse, weil Point nur in WPF verfügbar ist
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

        public GerberPoint(double[] xy)
        {
            X = xy[0];
            Y = xy[1];
        }


        public override string ToString()
        {
            return "X: " + X + ", Y: " + Y;
        }

        //Custom Override der vergleichfunktion, da zwei Punkte sonst nicht verglichen werden können
        public override bool Equals(object? obj)
        {

            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            GerberPoint vergleichspoint = (GerberPoint)obj;
            return vergleichspoint.X == X && vergleichspoint.Y == Y;
        }

        // Überschreiben der == Funktion
        public static bool operator ==(GerberPoint p1, GerberPoint p2)
        {
            if (ReferenceEquals(p1, p2))
                return true;
            if (p1 is null || p2 is null)
                return false;

            return p1.Equals(p2);
        }

        // Auch den != Operator
        public static bool operator !=(GerberPoint p1, GerberPoint p2)
        {
            return !(p1 == p2);
        }
    }


    public class GerberSettingsList
    {
        public List<GerberSetting> _settings { get; set; } = new List<GerberSetting>();

        public delegate void CallbackDelegate(string filecontent);
        string _callbackFileContent = "";

        private CallbackDelegate _onchangecallback;

        //Callback setzen
        public void setonchangecallback(CallbackDelegate callback, string callbackFileContent)
        {
            _onchangecallback = callback;
            _callbackFileContent = callbackFileContent;
        }


        public double getpadwidth()
        {
            return getsetting(SETTING.padwidth);
        }

        public void setpadwidth(double value)
        {
            setsetting(SETTING.padwidth, value);
        }

        public double getpolygoninfill()
        {
            return getsetting(SETTING.polygoninfill);
        }

        public void setpolygoninfill(double value)
        {
            setsetting(SETTING.polygoninfill, value);
        }


        public double getsetting(SETTING Setting)
        {
            return _settings.Find(x => x._type.Equals(Setting))?._value ?? 0.5;
        }


        public bool existssetting(SETTING Setting)
        {
            return _settings.Any(x => x._type.Equals(Setting));
        }

        public void setsetting(SETTING Setting, double value)
        {
            if (existssetting(Setting))
            {
                _settings.Find(x => x._type.Equals(Setting))._value = value;
            }
            else
            {
                _settings.Add(new GerberSetting(Setting, value));
            }
            Console.WriteLine("Setsetting");
            //Callback ausführen, es wurde etwas geändert
            _onchangecallback?.Invoke(_callbackFileContent);
        }
    }
    public enum SETTING
    {
        padwidth,
        polygoninfill,
        none
    }


    public class GerberSetting
    {
        public SETTING _type = SETTING.none;
        public double _value = 0;

        public GerberSetting(SETTING Setting, double Value)
        {
            _type = Setting;
            _value = Value;
        }
    }


    public class GerberPolygon
    {
        public List<GerberPoint> _poligoncornerpoints = new List<GerberPoint>();
        public double _stepsize { get; set; } = 1;

        public GerberPolygon(List<GerberPoint> polygoncorners, double stepsize = 10)
        {
            _poligoncornerpoints = polygoncorners;
            _stepsize = stepsize;
        }


        //folgender Code von ChatGPT mit prompt: 
        /*
         * i need to implement a function which takes edge points of a polygon in absolute coordinates x and y and fills the polygon with
         * straight lines, and not to paint outside of the polygon even if there are more than 4 edges into this class:
            public     class GerberPolygon
            {
                public List<GerberPoint> _poligoncornerpoints;

                public GerberPolygon(List<GerberPoint> polygoncorners)
                {
                    _poligoncornerpoints = polygoncorners;
                }

                public List<GerberLine> getgerberlines() { 
        
        
    
                }
            }

                        and the gerberline has this constructor:
                        public GerberLine(GerberPoint startpoint_, GerberPoint endpoint_, bool paint) paint for if there is a line or not, i only need to make the lines with paint
         * 
         * 
         */

        public List<GerberLine> getgerberlines(double lineDistance = 1)
        {
            List<GerberLine> gerberLines = new List<GerberLine>();


            // Find the bounding box of the polygon (min/max X and Y)
            double minX = _poligoncornerpoints.Min(p => p.X);
            double maxX = _poligoncornerpoints.Max(p => p.X);
            double minY = _poligoncornerpoints.Min(p => p.Y);
            double maxY = _poligoncornerpoints.Max(p => p.Y);

            // Horizontal lines
            for (double currentY = minY; currentY <= maxY; currentY += lineDistance)
            {
                List<double> intersections = GetIntersections(currentY);

                if (intersections.Count < 2)
                    continue;

                intersections.Sort();

                for (int i = 0; i < intersections.Count; i += 2)
                {
                    GerberPoint startPoint = new GerberPoint(intersections[i], currentY);
                    GerberPoint endPoint = new GerberPoint(intersections[i + 1], currentY);
                    gerberLines.Add(new GerberLine(startPoint, endPoint, true)); // true for paint
                }
            }

            // Vertical lines
            for (double currentX = minX; currentX <= maxX; currentX += lineDistance)
            {
                List<double> intersections = GetVerticalIntersections(currentX);

                if (intersections.Count < 2)
                    continue;

                intersections.Sort();

                for (int i = 0; i < intersections.Count; i += 2)
                {
                    GerberPoint startPoint = new GerberPoint(currentX, intersections[i]);
                    GerberPoint endPoint = new GerberPoint(currentX, intersections[i + 1]);
                    gerberLines.Add(new GerberLine(startPoint, endPoint, true)); // true for paint
                }
            }
            //gerberLines.AddRange(getoutline());

            return gerberLines;
        }

        // Get intersections of a horizontal line (scanline) with the polygon edges
        private List<double> GetIntersections(double y)
        {
            List<double> intersections = new List<double>();

            for (int i = 0; i < _poligoncornerpoints.Count; i++)
            {
                GerberPoint p1 = _poligoncornerpoints[i];
                GerberPoint p2 = _poligoncornerpoints[(i + 1) % _poligoncornerpoints.Count];

                if ((p1.Y <= y && p2.Y > y) || (p1.Y > y && p2.Y <= y))
                {
                    double x = p1.X + (y - p1.Y) * (p2.X - p1.X) / (p2.Y - p1.Y);
                    intersections.Add(x);
                }
            }

            return intersections;
        }

        // Get intersections of a vertical line with the polygon edges
        private List<double> GetVerticalIntersections(double x)
        {
            List<double> intersections = new List<double>();

            for (int i = 0; i < _poligoncornerpoints.Count; i++)
            {
                GerberPoint p1 = _poligoncornerpoints[i];
                GerberPoint p2 = _poligoncornerpoints[(i + 1) % _poligoncornerpoints.Count];

                if ((p1.X <= x && p2.X > x) || (p1.X > x && p2.X <= x))
                {
                    double y = p1.Y + (x - p1.X) * (p2.Y - p1.Y) / (p2.X - p1.X);
                    intersections.Add(y);
                }
            }

            return intersections;
        }

    }

}
