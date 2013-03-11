using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using System.IO;
using ESRI.ArcGIS.OSM.OSMClassExtension;
using System.Xml;
using System.Xml.Serialization;
using System.Runtime.InteropServices;

namespace ESRI.ArcGIS.OSM.GeoProcessing.OsmRest
{
    [ComVisible(false)]
    public class HttpUtils
    {
        public static void Post(HttpWebRequest webRequest, string sContent)
        {
            webRequest.Method = "POST";
            Stream requestStream = webRequest.GetRequestStream();
            StreamWriter mywriter = new StreamWriter(requestStream);

            mywriter.Write(sContent);
            mywriter.Close();
        }



        public static void Put(HttpWebRequest webRequest, string sContent)
        {
            webRequest.Method = "PUT";
            Stream requestStream = webRequest.GetRequestStream();
            StreamWriter mywriter = new StreamWriter(requestStream);

            mywriter.Write(sContent);
            mywriter.Close();
        }

        public static WebResponse Delete(string sUrl, string sContent, string sEncodedUsernameAndPassword, int iSeconds)
        {
            WebRequest delete = WebRequest.Create(sUrl);
            delete.Timeout = iSeconds * 1000;
            delete.Headers.Add("Authorization", "Basic " + sEncodedUsernameAndPassword);
            
            delete.Method = "DELETE";

            Stream requestStream = delete.GetRequestStream();
            StreamWriter stwriter = new StreamWriter(requestStream);

            stwriter.Write(sContent);
            stwriter.Close();

            WebResponse deleteResponse = null;
            deleteResponse = delete.GetResponse();
            
            stwriter.Close();
            if (deleteResponse != null)
                deleteResponse.Close();

            return deleteResponse;
        }

        public static string GetResponseContent(HttpWebResponse webResponse)
        {
            Stream responseStreamclient = webResponse.GetResponseStream();            
            StreamReader myreader = new StreamReader(responseStreamclient);
            string sResponseString = myreader.ReadToEnd();

            myreader.Close();

            return sResponseString;
        }

        public static diffResult GetResponse(HttpWebResponse webResponse)
        {
            diffResult diffResultResponse = null;

            try
            {
                Stream stream = webResponse.GetResponseStream();
                XmlSerializer serializer = new XmlSerializer(typeof(diffResult));

                XmlTextReader xmlReader = new XmlTextReader(stream);
                diffResultResponse = serializer.Deserialize(xmlReader) as diffResult;
                
                xmlReader.Close();
                stream.Close();
            }
            catch { }

            return diffResultResponse;
        }
    }
}
