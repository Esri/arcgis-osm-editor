using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ServicePublisher
{
  public class Services
  {
      private const string UNPUBLISHSVC_PY = "UnPublishSvc.py";
      private const string REMOVEDATA_PY = "DeleteData.py";

    public void DeleteService(string sArcGisServer,
                                string sAgsInstance,
                                string sConfigID,
                                string sFeatureDataset,
                                string sServiceName,
                                string sMxdOutput,
                                string sSDEConnection,
                                string sAgsUser,
                                string sAgsPwd,
                                string sPythonScriptDir)
    {
      // Check if service name starts with http://
      if (sServiceName.IndexOf("http://") > 1)
      {
        throw new ApplicationException("Service name did not begin with the expected http://! ServiceName: " + sServiceName);
      }

      // Parse out service name
      if (sServiceName.Contains("/"))
      {
        sServiceName = sServiceName.Substring(0, (sServiceName.LastIndexOf("/")));
        sServiceName = sServiceName.Substring(sServiceName.LastIndexOf("/") + 1);
      }

        // Set AgsURL
        string sAgsUrl = "http://" + sArcGisServer + ":6080/" + sAgsInstance;
      // Stop and Unpublish
      ServicePublisher.MXD.MapService10_1 mapService = null;
      mapService = new MXD.MapService10_1();
      mapService.UnPublish(sArcGisServer, sAgsUrl, sServiceName, "", sAgsUser, sAgsPwd, System.IO.Path.Combine(sPythonScriptDir, UNPUBLISHSVC_PY));

      // Make sure MXD is deleted
      ServicePublisher.MXD.MxdManager mxdManager = null;
      mxdManager = new MXD.MxdManager();
      mxdManager.DeleteMxd(sMxdOutput, sServiceName);

      // Delete the DataSet from the connection as well
      mxdManager.DeleteFeatureDataset(sFeatureDataset, sSDEConnection, System.IO.Path.Combine(sPythonScriptDir, REMOVEDATA_PY));

    }

    public string CreateService10_1(string sConnectionToSDEorGDB,
                              string sArcGisServer,
                              string sServiceName,
                              string sMxdTemplate,
                              bool bSde,
                              string MxdOutput,
                              string agsConnection,
                              string sPythonScriptDir)
    {

      ServicePublisher.MXD.PublishMXD pPublishMxd;
      try
      {
        //Use mxd on file
        // Template found at c:\inetput\wwwroot\osm\Mxds\OSMTemplate.mxd
        //OSMTemplate.mxd in source control

        string sDataSet = sConnectionToSDEorGDB.Substring(sConnectionToSDEorGDB.LastIndexOf("\\") + 1);

        if (!(bSde))
        {
          sConnectionToSDEorGDB = sConnectionToSDEorGDB.Substring(0, sConnectionToSDEorGDB.Length - (sConnectionToSDEorGDB.Length - sConnectionToSDEorGDB.LastIndexOf("\\") + 1));
        }
        else
        {
          sConnectionToSDEorGDB = sConnectionToSDEorGDB.Substring(0, sConnectionToSDEorGDB.Length - (sConnectionToSDEorGDB.Length - sConnectionToSDEorGDB.LastIndexOf("\\")));
        }

        string sMxdOutputDir = MxdOutput;

        
        pPublishMxd = new MXD.PublishMXD(sMxdTemplate, sArcGisServer, sServiceName, sConnectionToSDEorGDB, sDataSet, sMxdOutputDir, bSde);
        return pPublishMxd.Publish(agsConnection, sPythonScriptDir);
      }
      catch (Exception ex)
      {
        throw ex;
      }
      finally
      {
        pPublishMxd = null;
      }
    }

    public string CreateService(string sConnectionToSDEorGDB,
                                string sArcGisServer,
                                string sServiceName,
                                string sMxdTemplate,
                                bool bSde,
                                string MxdOutput)
    {
      ServicePublisher.MXD.PublishMXD pPublishMxd;
      try
      {
        //Use mxd on file
        // Template found at c:\inetput\wwwroot\osm\Mxds\OSMTemplate.mxd
        //OSMTemplate.mxd in source control

        string sDataSet = sConnectionToSDEorGDB.Substring(sConnectionToSDEorGDB.LastIndexOf("\\") + 1);

        if (!(bSde))
        {
          sConnectionToSDEorGDB = sConnectionToSDEorGDB.Substring(0, sConnectionToSDEorGDB.Length - (sConnectionToSDEorGDB.Length - sConnectionToSDEorGDB.LastIndexOf("\\") + 1));
        }
        else
        {
          sConnectionToSDEorGDB = sConnectionToSDEorGDB.Substring(0, sConnectionToSDEorGDB.Length - (sConnectionToSDEorGDB.Length - sConnectionToSDEorGDB.LastIndexOf("\\")));
        }

        string sMxdOutputDir = MxdOutput;

        
        pPublishMxd = new MXD.PublishMXD(sMxdTemplate, sArcGisServer, sServiceName, sConnectionToSDEorGDB, sDataSet, sMxdOutputDir, bSde);
        return pPublishMxd.Publish("","");
      }
      catch (Exception ex)
      {
        throw ex;
      }
      finally
      {
        pPublishMxd = null;
      }
    }
  }
}
