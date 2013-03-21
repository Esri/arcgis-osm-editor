using System;
using ESRI.ArcGIS;

namespace ServicePublisher.MXD
{
  internal partial class LicenseInitializer
  {
    public LicenseInitializer()
    {
      ResolveBindingEvent += new EventHandler(BindingArcGISRuntime);
    }

    void BindingArcGISRuntime(object sender, EventArgs e)
    {
     
      if (!RuntimeManager.Bind(ProductCode.Server))
      {
          
        // Failed to bind, announce and force exit
        Console.WriteLine("Invalid ArcGIS runtime binding. Application will shut down.");
        System.Environment.Exit(0);
      }
    }
  }
}