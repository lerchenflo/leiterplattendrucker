using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Unicode;
using static System.Net.Mime.MediaTypeNames;

namespace Leiterplattendrucker_V1
{
    public static class Website
    {

        /// <summary>
        /// Response für einen aufgerufenen URL zurückgeben
        /// </summary>
        /// <param name="requesturl"></param>
        /// <returns>Body, Content-Type, Statuscode</returns>
        public static (byte[], string, int, bool) getResponse(string requesturl)
        {
            //Je nach anfrage verschiedene Files zurückgeben
            bool picture = false;

            //Beim aufrufen der Website ohne Verzeichnis wird die index.html zurückgegeben
            if (requesturl == "/")
            {
                requesturl = "/index.html";
            }
            //Favicon muss seperat verändert werden
            else if (requesturl == "favicon.ico")
            {
                requesturl = "/favicon.ico";
            }
            

            //Standardmäßig ist der Statuscode auf 200 (OK)
            int statuscode = (int)HttpStatusCode.OK;

            //Ins unterverzeichnis der gerade ausgeführten Instanz navigieren
            string finalFilepath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Website", requesturl.Substring(1, requesturl.Length-1));


            //Bei Windows den Dateipfad ändern
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                finalFilepath = finalFilepath.Replace("/", "\\");
            }

            byte[] body;
            if (finalFilepath.EndsWith(".ico"))
            {
                body = File.ReadAllBytes(finalFilepath);
                picture = true;
            }
            else
            {
                //Den HTML / CSS / Javascript Code aus dem File auslesen
                body = Encoding.UTF8.GetBytes(loadfile(finalFilepath));
            }

            //Console.WriteLine(finalFilepath);

            //Falls das File nicht gefunden wurde / Leer ist
            if (Encoding.UTF8.GetString(body) == string.Empty)
            {
                Console.WriteLine("Website: Fehler - Kein File geladen");
                //Wenn ein falsches File angefordert wird, wird ein fehlercode zurückgegeben
                statuscode = (int)HttpStatusCode.NotFound;
                body = new byte[0];
            }

            //Den contenttype aus dem Pfad auslesen
            string contentType = getcontenttype(finalFilepath);


            //Zurückgeben des Files und des Contenttypes mit Statuscode als Tuple
            return (body, contentType, statuscode, picture);
        }

        /// <summary>
        /// File aus Pfad auslesen
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string loadfile(string path)
        {
            
            try
            {
                StreamReader sr = new StreamReader(path);
                string body = sr.ReadToEnd();
                sr.Close();
                return body;
            }
            catch (FileNotFoundException)
            {
                Console.WriteLine("File nicht gefunden: " + path);
                return "";
            }
        }

        /// <summary>
        /// Korrespondierenden Contenttype aus Dateipfad zurückgeben
        /// </summary>
        /// <param name="filePath"></param>
        /// <returns></returns>
        private static string getcontenttype(string filePath)
        {
            //Extension aus dem Dateipfad extrahieren
            string extension = Path.GetExtension(filePath).ToLower();

            //Korrespondierenden Contenttype zurückgeben
            return extension switch
            {
                ".html" => "text/html; charset=utf-8",
                ".css" => "text/css",
                ".js" => "application/javascript",
                ".png" => "image/png",
                ".jpg" => "image/jpeg",
                ".gif" => "image/gif",
                ".svg" => "image/svg+xml",
                ".ico" => "image/x-icon",
                _ => "application/octet-stream",
            };
        }

    }
}
