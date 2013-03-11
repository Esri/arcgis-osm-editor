// (c) Copyright ESRI, 2010 - 2012
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Collections;
using System.Runtime.InteropServices;

namespace ESRI.ArcGIS.OSM.OSMClassExtension
{
    [Serializable]
    public class ComReleaser : IDisposable
    {
        private ArrayList m_array;
        public ComReleaser()
        {
            this.m_array = ArrayList.Synchronized(new ArrayList());
        }

        ~ComReleaser()
        {
            this.Dispose(false);
        }

        public void ManageLifetime(object o)
        {
            this.m_array.Add(o);
        }
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }
        protected virtual void Dispose(bool disposing)
        {
            int count = this.m_array.Count;
            for (int i = 0; i < count; i++)
            {
                if (this.m_array[i] != null && Marshal.IsComObject(this.m_array[i]))
                {
                    while (Marshal.ReleaseComObject(this.m_array[i]) > 0)
                    {
                    }
                }
            }
            if (disposing)
            {
                this.m_array = null;
            }
        }

        public static void ReleaseCOMObject(object o)
        {
            if (o != null && Marshal.IsComObject(o))
            {
                while (Marshal.ReleaseComObject(o) > 0)
                {
                }
            }
        }
    }
}
