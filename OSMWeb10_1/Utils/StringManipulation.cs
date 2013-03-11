using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace OSMWeb.Utils
{
    public class StringManipulation
    {
        static public string EncodeTo64(string toEncode)
        {

            byte[] toEncodeAsBytes

                  = System.Text.ASCIIEncoding.ASCII.GetBytes(toEncode);

            string returnValue

                  = System.Convert.ToBase64String(toEncodeAsBytes);

            return returnValue;

        }

    }
}