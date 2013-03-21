using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

namespace SyncUtilTask
{
    class Program
    {
        static void Main(string[] args)
        {
            string sRequestToMake = "http://localhost/osm/SyncNow";

            if (args.Length > 0)
                sRequestToMake = args[0];

            using (WebClient client = new WebClient())
            {
                string sResponse = client.DownloadString(sRequestToMake);

                Console.Write(sResponse);
            }
        }
    }
}
