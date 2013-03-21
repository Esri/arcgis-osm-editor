using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using ESRI.ArcGIS.Server;
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.esriSystem;
//using ESRI.ArcGIS.GISClient;

using Microsoft.Win32;


namespace ServicePublisher.MXD
{
    public class MapService
    {
        private static IServerObjectAdmin soAdmin = null;

        public void Start(string serverName, string serviceName)
        {
            serviceName = CleanServiceName(serviceName);

            try
            {
                IGISServerConnection pGISServerConnection = new GISServerConnection();
                pGISServerConnection.Connect(serverName);

                IServerObjectAdmin pServerObjectAdmin = pGISServerConnection.ServerObjectAdmin;
                pServerObjectAdmin.StartConfiguration(serviceName, "MapServer");

                COMUtil.ReleaseObject(pServerObjectAdmin);
                COMUtil.ReleaseObject(pGISServerConnection);
            }
            catch (Exception ex)
            {
                
                throw ex;
            }
        }

        public void Stop(string serverName, string serviceName, bool bDelete)
        {
          IGISServerConnection pGISServerConnection = null;
          IServerObjectAdmin pServerObjectAdmin = null;

          try
          {
            serviceName = CleanServiceName(serviceName);

            pGISServerConnection = new GISServerConnection();
            pGISServerConnection.Connect(serverName);

            pServerObjectAdmin = pGISServerConnection.ServerObjectAdmin;
            pServerObjectAdmin.StopConfiguration(serviceName, "MapServer");
            if (bDelete == true)
              pServerObjectAdmin.DeleteConfiguration(serviceName, "MapServer");            
          }
          catch (Exception ex)
          {
            
            throw ex;
          }
          finally
          {
            COMUtil.ReleaseObject(pServerObjectAdmin);
            COMUtil.ReleaseObject(pGISServerConnection);
          }
        }

        public int NumberOfServices(string serverName)
        {
            int iCount = 0;

            IGISServerConnection2 pGISServerConnection = new GISServerConnection() as IGISServerConnection2;
            pGISServerConnection.Connect(serverName);

            IServerObjectAdmin4 pServerObjectAdmin = pGISServerConnection.ServerObjectAdmin as IServerObjectAdmin4;

            IEnumServerObjectConfiguration objConfigs = pServerObjectAdmin.GetConfigurations();
            IServerObjectConfiguration3 icfg = objConfigs.Next() as IServerObjectConfiguration3;
            while (icfg != null)
            {
                string stemp = icfg.Name.ToUpper();
                iCount++;

                icfg = objConfigs.Next() as IServerObjectConfiguration3;
            }

            COMUtil.ReleaseObject(icfg);
            COMUtil.ReleaseObject(objConfigs);
            COMUtil.ReleaseObject(pServerObjectAdmin);
            COMUtil.ReleaseObject(pGISServerConnection);

            return iCount;
        }

        public bool Exists(string serverName, string serviceName)
        {
            IGISServerConnection2 pGISServerConnection = new GISServerConnection() as IGISServerConnection2;
            pGISServerConnection.Connect(serverName);
            IServerObjectConfiguration3 icfg = null;
            IEnumServerObjectConfiguration objConfigs = null;
            IServerObjectAdmin4 pServerObjectAdmin = pGISServerConnection.ServerObjectAdmin as IServerObjectAdmin4;
            try
            {
                // Get Configurations from the server
                objConfigs = pServerObjectAdmin.GetConfigurations();

                icfg = objConfigs.Next() as IServerObjectConfiguration3;

                while (icfg != null)
                {
                    if (icfg.Name.ToUpper() == serviceName.ToUpper()) return true;
                    icfg = objConfigs.Next() as IServerObjectConfiguration3;
                }

                return false;
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                COMUtil.ReleaseObject(icfg);
                COMUtil.ReleaseObject(objConfigs);
                COMUtil.ReleaseObject(pServerObjectAdmin);
                COMUtil.ReleaseObject(pGISServerConnection);
            }
        }

        private string CleanServiceName(string sName)
        {
            return sName.Replace(" ", "_");
        }

        public void Create(string serverName, string serviceName, string sFilePath, bool replaceExisting, bool isPooled, string outputDir, string virtualOutputDir, int minInstances, int maxInstances, int waitTimeout, int usageTimeout, int idleTimeout, bool bSde)
        {
            IGISServerConnection2 pGISServerConnection = null;
            IServerObjectAdmin3 pServerObjectAdmin = null;
            IServerObjectConfiguration3 pConfiguration = null;

            try
            {
                pGISServerConnection = new GISServerConnection() as IGISServerConnection2;
                pGISServerConnection.Connect(serverName);

                pServerObjectAdmin = pGISServerConnection.ServerObjectAdmin as IServerObjectAdmin3;

                // create the new configuration
                pConfiguration = pServerObjectAdmin.CreateConfiguration() as IServerObjectConfiguration3;

                pConfiguration.Name = serviceName;
                pConfiguration.TypeName = "MapServer";

                //Convert filepath to a local connection
                sFilePath = sFilePath.ToUpper().Replace("C$\\", "");

                IPropertySet pProps = pConfiguration.Properties;
                pProps.SetProperty("FilePath", sFilePath);
                pProps.SetProperty("OutputDir", outputDir);
                pProps.SetProperty("VirtualOutputDir", virtualOutputDir);

                pProps.SetProperty("SupportedImageReturnTypes", "URL");
                pProps.SetProperty("MaxImageHeight", "2048");
                pProps.SetProperty("MaxRecordCount", "500");
                pProps.SetProperty("MaxBufferCount", "100");
                pProps.SetProperty("MaxImageWidth", "2048");

                pProps.SetProperty("IgnoreCache", "false");
                pProps.SetProperty("ClientCachingAllowed", "true");

                // Tell AGS not to keep an active schema lock on the datasets for this map service
                pProps.SetProperty("SchemaLockingEnabled", "false");

                // Test to enable mobile at publish
                pProps.SetProperty("MobileDataAccess", "true");

                IPropertySet pPropsWeb = pConfiguration.Info;
                pPropsWeb.SetProperty("WebEnabled", "true");
                pPropsWeb.SetProperty("WebCapabilities", "Map,Query,Data");

                pConfiguration.IsPooled = isPooled;
                pConfiguration.MinInstances = minInstances;
                pConfiguration.MaxInstances = maxInstances;

                pConfiguration.WaitTimeout = waitTimeout;
                pConfiguration.UsageTimeout = usageTimeout;
                pConfiguration.IdleTimeout = idleTimeout;
                pConfiguration.Description = serviceName;

                //  Set KML extension properties
                pConfiguration.set_ExtensionEnabled("KMLServer", true);

                IPropertySet pPropKML = pConfiguration.get_ExtensionInfo("KMLServer");
                pPropKML.SetProperty("WebEnabled", "true");
                pPropKML.SetProperty("WebCapabilities", "Vectors");
                pConfiguration.set_ExtensionInfo("KMLServer", pPropKML);

                IPropertySet pExtensionPropKML = pConfiguration.get_ExtensionProperties("KMLServer");
                pExtensionPropKML.SetProperty("ImageSize", "1024");
                pExtensionPropKML.SetProperty("FeatureLimit", "1000000");
                pExtensionPropKML.SetProperty("Dpi", "96");

                pExtensionPropKML.SetProperty("MinRefreshPeriod", "30");
                pExtensionPropKML.SetProperty("UseDefaultSnippets", "false");

                // Set FeatureAccess extention         
                if (bSde)
                {
                    pConfiguration.set_ExtensionEnabled("FeatureServer", true);
                    IPropertySet pPropFeatureAccess = pConfiguration.get_ExtensionInfo("FeatureServer");
                    pPropFeatureAccess.SetProperty("WebEnabled", "true");
                    pPropFeatureAccess.SetProperty("Query", "true");
                    pPropFeatureAccess.SetProperty("Editing", "true");
                    pPropFeatureAccess.SetProperty("WebCapabilities", "Editing,Query");
                }

                try
                {

                    pServerObjectAdmin.AddConfiguration(pConfiguration);
                    pServerObjectAdmin.StartConfiguration(pConfiguration.Name, pConfiguration.TypeName);
                }
                catch (Exception)
                {
                    throw new ApplicationException("Service " + serviceName + " already exists");
                }
            }
            catch (Exception ex)
            {
                throw new ApplicationException("Error in MapService.Create: " + ex.Message + " Stack trace: " + ex.StackTrace);
            }
            finally
            {
                COMUtil.ReleaseObject(pConfiguration);
                COMUtil.ReleaseObject(pServerObjectAdmin);
                COMUtil.ReleaseObject(pGISServerConnection);
            }
        }
       
    }
}
