using System.Net;
using System.Runtime.InteropServices;
using System.Text.Unicode;
using static System.Net.Mime.MediaTypeNames;

namespace Leiterplattendrucker_V1
{
    public class Website
    {
        public Website()
        {

        }

        /// <summary>
        /// Response für einen aufgerufenen URL bekommen
        /// </summary>
        /// <param name="requesturl"></param>
        /// <returns>Body, Content-Type, Statuscode</returns>
        public (string, string, int) getResponse(string requesturl)
        {
            //Je nach anfrage verschiedene Files zurückgeben


            //Bekannte urls manuell ändern
            if (requesturl == "/")
            {
                requesturl = "/index.html";
            }

            Console.WriteLine("\n\nRequesturl: " + requesturl);
            int statuscode = 200;

            string finalFilepath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Website", requesturl.Substring(1, requesturl.Length-1));
            
            finalFilepath = finalFilepath.Replace("/", "\\");
            //Console.WriteLine("Relativpfad: " + finalFilepath);

            string body = loadfile(finalFilepath);
            
            if (body == string.Empty)
            {
                Console.WriteLine("Website: Fehler - Kein File geladen");
                //Wenn ein falsches File angefordert wird, wird ein fehlercode zurückgegeben
                statuscode = (int)HttpStatusCode.NotFound;
            }
            //Zurückgeben des Files und des Contenttypes mit Statuscode als Tuple
            string contentType = getcontenttype(finalFilepath);

            Console.WriteLine("Content - Type: " + contentType);
            //Console.WriteLine("Body: " + body);

            return (body, contentType, statuscode);
        }




        private string loadfile(string path)
        {
            //Bilder werden anders behandelt
            if (Path.GetExtension(path) == ".ico")
            {
                Console.WriteLine("Bild angefordert");
                byte[] icoBytes = File.ReadAllBytes(path);
                
                return Convert.ToBase64String(icoBytes);
            }

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

        private string getcontenttype(string filePath)
        {
            string extension = Path.GetExtension(filePath).ToLower();
            return extension switch
            {
                ".html" => "text / html; charset=utf-8",
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
