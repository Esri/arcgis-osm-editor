using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ESRI.ArcGIS.OSM.OSMClassExtension;

namespace OSMClassExtensionTest
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void SampleTest()
        {
            OpenStreetMapClassExtension extension = new OpenStreetMapClassExtension();
            int result = extension.supportingElementFieldIndex;
            
            Assert.AreEqual(result, 1);
        }
    }
}
