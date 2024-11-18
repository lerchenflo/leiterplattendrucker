using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

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
            Console.WriteLine("Getresponse" + requesturl);
            //Je nach anfrage verschiedene Files zurückgeben
            string contentType = "";
            int statuscode = 200;

            

            string projectDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", ".."));
            string finalFilepath = Path.Combine(projectDir, "Website", requesturl);
            Console.WriteLine(finalFilepath);

            string body = loadfile(finalFilepath);
            if (body == string.Empty)
            {
                //Wenn ein falsches File angefordert wird, wird ein fehlercode zurückgegeben
                //statuscode = (int)HttpStatusCode.NotFound;
            }
            //Zurückgeben des Files und des Contenttypes mit Statuscode als Tuple
            return (body, contentType, statuscode);
        }



        
        private string loadfile(string path)
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
                Console.WriteLine("file " + path);
                return "";
            }
            
        }

    }
}
