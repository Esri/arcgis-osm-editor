using System;
using System.Collections.Generic;
using System.Text;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.Server;
using ESRI.ArcGIS.ADF.Connection;
using System.Runtime.InteropServices;

namespace ServicePublisher.GeoSpatial
{
    public class FeatureClassManager
    {        
        private SdeServerInfo _sdeServerInfo = null;
        protected IServerContext _serverContext = null;
        protected IServerObjectManager _SOM = null;
        private string _serverName;
        private string _mapService;
        private string _sContextMapServer;
        private string _sContextMapService;

        
        public FeatureClassManager(SdeServerInfo sdeServerInfo, string serverName, string mapService, string sContextMapServer, string sContextMapService)
        {
            _serverName = serverName;
            _mapService = mapService.Replace(" ", "_");
            _sContextMapServer = sContextMapServer;
            _sContextMapService = sContextMapService;
            Init(sdeServerInfo, null);
        }

        
        private void Init(SdeServerInfo sdeServerInfo, IServerContext context)
        {
            Init();
            _sdeServerInfo = sdeServerInfo;
            
            _serverContext = GetServerContext(_mapService);        
        }

        public void Release()
        {

            if (_serverContext != null)
            {
                _serverContext.ReleaseContext();
                Util.ReleaseCOMObject(_serverContext);   
            }
            Util.ReleaseCOMObject(_SOM);
        }

        protected void Init()
        {
            
        }

       
        private IFeatureClass GetFeatureClassInsideDataset(string featureClassName)
        {
            IWorkspaceFactory2 factory=null;
            if (_serverContext != null)
                factory = (IWorkspaceFactory2)_serverContext.CreateObject("esriDataSourcesGDB.SDEWorkspaceFactory");
            //else
            //    factory = new ESRI.ArcGIS.DataSourcesGDB.SdeWorkspaceFactoryClass();
            //TODO COMMENTED ABOVE TO ALLOW COMPILE
            IWorkspace ipWks = null;
            IFeatureClass ipFeatClass = null;
            IFeatureDataset ipFeatDS = null;
            IEnumDataset ipEnumSubDataset = null;
            IEnumDataset ipEnumDataset = null;
            IFeatureWorkspace fws = null; 

            try
            {
                IPropertySet2 propSet=null;
                if ( _serverContext != null )
                    propSet = (IPropertySet2)_serverContext.CreateObject("esriSystem.PropertySet");
                //else
                //    propSet =  new ESRI.ArcGIS.esriSystem.PropertySetClass();
                //TODO COMMENTED ABOVE TO ALLOW COMPILE
                propSet.SetProperty("SERVER", _sdeServerInfo.server);
                propSet.SetProperty("INSTANCE", _sdeServerInfo.instance);
                propSet.SetProperty("DATABASE", _sdeServerInfo.database);
                propSet.SetProperty("USER", _sdeServerInfo.user);
                propSet.SetProperty("PASSWORD", _sdeServerInfo.password);
                propSet.SetProperty("VERSION", _sdeServerInfo.version);

                ipWks = factory.Open(propSet, 0);
                fws = (IFeatureWorkspace)ipWks;
                //try and open it
                if (ipWks != null)
                {   
                    // Now get all the feature classes included in the datasets
                    ipEnumDataset = ipWks.get_Datasets(esriDatasetType.esriDTFeatureDataset);
                    ipEnumDataset.Reset();
                    ipFeatDS = ipEnumDataset.Next() as IFeatureDataset;
                    while (ipFeatDS != null)
                    {
                        ipEnumSubDataset = ipFeatDS.Subsets; //.get_Datasets(esriDatasetType.esriDTFeatureClass);
                        ipEnumSubDataset.Reset();
                        ipFeatClass = ipEnumSubDataset.Next() as IFeatureClass;
                        while (ipFeatClass != null)
                        {
                            if (ipFeatClass.AliasName.ToUpper().Contains(featureClassName.ToUpper()))
                            {
                                return (ipFeatClass);
                            }
                            ipFeatClass = ipEnumSubDataset.Next() as IFeatureClass;
                        }
                        ipFeatDS = ipEnumDataset.Next() as IFeatureDataset;
                        ipEnumSubDataset = null;
                    }

                    ipEnumDataset = null;
                }
            }
            catch (System.Runtime.InteropServices.COMException edd)
            {  
                ipFeatClass = null;
                throw edd;
            }
            finally
            {
                Util.ReleaseCOMObject(fws);
                Util.ReleaseCOMObject(ipEnumSubDataset);
                Util.ReleaseCOMObject(ipEnumDataset);
                Util.ReleaseCOMObject(factory);               
                
            }

            return null;
            
        }

        //search outside datasets
        protected IFeatureClass GetFeatureClass(string featureClassName)
        {
            IWorkspaceFactory2 factory=null;
            if (_serverContext != null)
                factory = (IWorkspaceFactory2)_serverContext.CreateObject("esriDataSourcesGDB.SDEWorkspaceFactory");
            
            IWorkspace ipWks = null;
            IFeatureClass ipFeatClass = null;
            IFeatureClass ipFeatDS = null;
            IFeatureClass ipEnumSubDataset = null;
            IEnumDataset ipEnumDataset = null;
            IFeatureWorkspace fws = null; 

            try
            {
                IPropertySet2 propSet=null;
                if (_serverContext != null)
                    propSet = (IPropertySet2)_serverContext.CreateObject("esriSystem.PropertySet");
               
                propSet.SetProperty("SERVER", _sdeServerInfo.server);
                propSet.SetProperty("INSTANCE", _sdeServerInfo.instance);
                propSet.SetProperty("DATABASE", _sdeServerInfo.database);
                propSet.SetProperty("USER", _sdeServerInfo.user);
                propSet.SetProperty("PASSWORD", _sdeServerInfo.password);
                propSet.SetProperty("VERSION", _sdeServerInfo.version);

                ipWks = factory.Open(propSet, 0);
                fws = (IFeatureWorkspace)ipWks;

                //try and open it
                if (ipWks != null)
                {

                    ipEnumDataset = ipWks.get_Datasets(esriDatasetType.esriDTFeatureClass);
                    ipEnumDataset.Reset();
                    ipFeatDS = ipEnumDataset.Next() as IFeatureClass;
                    while (ipFeatDS != null)
                    {
                        ipEnumSubDataset = ipFeatDS;
                        ipFeatClass = ipEnumSubDataset;
                        while (ipFeatClass != null)
                        {
                            if (ipFeatClass.AliasName.ToUpper().Contains(featureClassName.ToUpper()))
                            {
                                return (ipFeatClass);
                            }
                            ipFeatClass = ipEnumDataset.Next() as IFeatureClass;
                        }
                        ipFeatDS = ipEnumDataset.Next() as IFeatureClass;
                        ipEnumSubDataset = null;
                    }

                    ipEnumDataset = null;
                }
            }
            catch (System.Runtime.InteropServices.COMException edd)
            {
                //exception is coming from Arcobjects...
                ipFeatClass = null;
                throw edd;
            }
            finally
            {
                Util.ReleaseCOMObject(fws);
                Util.ReleaseCOMObject(ipEnumSubDataset);
                Util.ReleaseCOMObject(ipEnumDataset);
                Util.ReleaseCOMObject(factory);               

            }
            return GetFeatureClassInsideDataset(featureClassName);
        }


        private IServerContext GetServerContext(string sMapServiceName)
        {
            // *** Using Web ADF Common API           
          ESRI.ArcGIS.ADF.Connection.AGS.AGSServerConnection gisconnection = new ESRI.ArcGIS.ADF.Connection.AGS.AGSServerConnection();
            gisconnection.Host = _serverName;

            gisconnection.Connect();

            _SOM = gisconnection.ServerObjectManager;

            // *** Change the map server object name as needed 
            int counter = 0;
            IServerContext mapContext = null;
            while ((mapContext == null) && (counter < 3))
            {
                try
                {
                    mapContext = _SOM.CreateServerContext(sMapServiceName, "MapServer");
                }
                catch
                {

                }
                counter += 1;
            }

            if (mapContext == null)
            {
                try
                {
                    if ( _sContextMapService != null )
                        mapContext = _SOM.CreateServerContext(_sContextMapService, "MapServer");
                }
                catch
                {

                }
            }

            return mapContext;
        }
    }
}
