using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Serialization;
using System.Xml;
using System.Net;
using System.IO;
using System.Runtime.InteropServices;

namespace ESRI.ArcGIS.OSM.GeoProcessing.OsmRest
{
    [ComVisible(false)]
    public class SerializeUtils
    {
        public static string CreateXmlSerializable(object objectToConvert,
            XmlSerializer serializer, System.Text.Encoding encoder, string sTypeOutput)
        {
            string serializedString = String.Empty;

            if (objectToConvert != null)
            {

                System.IO.MemoryStream memoryStream = new System.IO.MemoryStream();
                if (serializer == null)
                {
                    serializer = new System.Xml.Serialization.XmlSerializer(objectToConvert.GetType());
                }
                System.Xml.XmlTextWriter xmlTextWriter = new System.Xml.XmlTextWriter(memoryStream, encoder);

                serializer.Serialize(xmlTextWriter, objectToConvert);
                memoryStream = (System.IO.MemoryStream)xmlTextWriter.BaseStream;

                serializedString = new UTF8Encoding().GetString(memoryStream.ToArray());
            }
         
            return serializedString;
        }
    }
}
