using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;

namespace ServicePublisher.MXD
{
    class COMUtil
    {

        public static void ReleaseObject(object objectContext)
        {
            if (objectContext != null)
                if (Marshal.IsComObject(objectContext))
                    while (Marshal.ReleaseComObject(objectContext) > 0)
                        continue;
        }
    }
}
