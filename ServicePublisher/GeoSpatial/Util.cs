using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;

namespace ServicePublisher.GeoSpatial
{
    public class Util
    {
        /// <summary>
        /// This function needs to be called to release all the com objects create within the context
        /// </summary>
        /// <param name="o"></param> 
        public static void ReleaseCOMObject(object o)
        {

            if (o != null)

                if (Marshal.IsComObject(o))

                    while (Marshal.ReleaseComObject(o) > 0)

                        continue;

        }

    }
}
