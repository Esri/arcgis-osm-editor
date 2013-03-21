using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;
using ESRI.ArcGIS.ADF;
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.DataSourcesGDB;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.Geoprocessing;
using ESRI.ArcGIS.esriSystem;





namespace ServicePublisher.MXD
{
    public class MxdManager
    {
        private string _sPathToMXD;
        private string _ArcGISServer;
        private string _sTable;
        private string _sServiceFolder;

        private IMapDocument _pMapDocument;
        private string _sNewDocument;
        private string _sMxdTemplate;

        public MxdManager()
        {
            _sMxdTemplate = "";
        }

        private static LicenseInitializer m_AOLicenseInitializer;
        public bool IsInitialized;
        public MxdManager(string sMxdTemplate)
        {
            // Initialize License 
            m_AOLicenseInitializer = new LicenseInitializer();
            IsInitialized = m_AOLicenseInitializer.InitializeApplication(new ESRI.ArcGIS.esriSystem.esriLicenseProductCode[] { ESRI.ArcGIS.esriSystem.esriLicenseProductCode.esriLicenseProductCodeArcServer },
            new ESRI.ArcGIS.esriSystem.esriLicenseExtensionCode[] { });

            if (IsInitialized == false)
            {
                esriLicenseStatus licenseStatus = esriLicenseStatus.esriLicenseUnavailable;
                IAoInitialize m_AoInitialize = new AoInitialize();
#if _WIN64
                licenseStatus = m_AoInitialize.Initialize(esriLicenseProductCode.esriLicenseProductCodeAdvanced);
#else
                licenseStatus = m_AoInitialize.Initialize(esriLicenseProductCode.esriLicenseProductCodeArcInfo);
#endif
                if (licenseStatus != esriLicenseStatus.esriLicenseNotInitialized)
                    IsInitialized = true;
            }

            _sMxdTemplate = sMxdTemplate;

        }

        public bool CreateMxd(string sMxdTemplate,
                              string sPathToMXD,
                              string ArcGISServer,
                              string sMxdFile,
                              string sDBConn,
                              string sDataSet,
                              bool bSde)
        {
            if (sMxdTemplate.Length > 0) _sMxdTemplate = sMxdTemplate;

            _sPathToMXD = sPathToMXD;
            _ArcGISServer = ArcGISServer;
            ESRI.ArcGIS.Carto.IMap pMap = null;
            IFeatureClass pOldFC = null;
            string fcName = String.Empty;
            string sSuffix = String.Empty;

            IWorkspaceFactory2 wsf = null;
            IWorkspace2 ws2 = null;
            IFeatureWorkspace fws = null;
            IWorkspace ws = null;

            try
            {
                if (bSde)
                {
                    // Get WS for SDE
                    ws = ArcSdeWorkspaceFromFile(sDBConn);
                }
                else
                {
                    // Get WS from file GDB.   
                    wsf = new FileGDBWorkspaceFactoryClass() as IWorkspaceFactory2;
                    //if locks on gdb only path is passed in 
                    string fileGdb = sDBConn.Contains(".gdb") ? sDBConn : sDBConn;

                    if (wsf.IsWorkspace(fileGdb))
                    {
                        ws = wsf.OpenFromFile(fileGdb, 0);
                    }
                }

                if (ws == null)
                {
                    return false;
                }

                // Check if Mxd already exists
                if (File.Exists(sMxdFile))
                {
                    return false;
                }

                // Create a Mxd from Overlays Template
                pMap = PrivateCreateMxd(sMxdFile);

                ws2 = (IWorkspace2)ws;
                fws = (IFeatureWorkspace)ws;

                // Loop through all layers in MXD and repoint data source to OverlayGDB Features
                IEnumLayer pEnumLayer = pMap.get_Layers(null, true);
                pEnumLayer.Reset();
                ILayer pLayer = pEnumLayer.Next();
                while (pLayer != null)
                {
                    if (!(pLayer is IFeatureLayer))
                    {

                        pLayer = pEnumLayer.Next();
                        continue;
                    }

                    // Cast pLayer to featurelayer
                    IFeatureLayer pMapFeatureLayer = (IFeatureLayer)pLayer;
                    pOldFC = pMapFeatureLayer.FeatureClass;

                    if (pOldFC == null)
                    {
                        pLayer = pEnumLayer.Next();
                        continue;
                    }

                    // Get FC name
                    IDataset pDS = (IDataset)pOldFC;
                    fcName = pDS.Name;
                    
                    // Feature Class: <Dataset>_osm_pt, <Dataset>_osm_ln, <Dataset>_osm_ply
                    sSuffix = fcName.Substring(fcName.IndexOf("_osm_"));

                    if (String.IsNullOrEmpty(sSuffix)) continue;

                    // Check if feature class exists in GDB
                    if (ws2.get_NameExists(esriDatasetType.esriDTFeatureClass, sDataSet + sSuffix))
                    {
                        // Get feature class
                        IFeatureClass ipFC = fws.OpenFeatureClass(sDataSet + sSuffix);
                        IFeatureLayer ipFL = (IFeatureLayer)pLayer;

                        // Create IMapAdmin2 from pMap
                        IMapAdmin2 pMapAdmin2 = (IMapAdmin2)pMap;

                        // Change FeatureClass of layer to FC in FGDB
                        ipFL.FeatureClass = ipFC;
                        pMapAdmin2.FireChangeFeatureClass(pOldFC, ipFC);

                        COMUtil.ReleaseObject(ipFC);
                        ipFC = null;

                        COMUtil.ReleaseObject(ipFL);
                        ipFL = null;
                    }
                    else
                    {
                        // Remove layer from map
                        pMap.DeleteLayer(pLayer);
                    }

                    pLayer = pEnumLayer.Next();
                }

                SaveMXD(sMxdFile, pMap);

                return true;
            }
            catch (System.Runtime.InteropServices.COMException cx)
            {
                throw cx;
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                COMUtil.ReleaseObject(pOldFC);
                COMUtil.ReleaseObject(fws);
                COMUtil.ReleaseObject(ws2);
                COMUtil.ReleaseObject(ws);
                COMUtil.ReleaseObject(pMap);
                COMUtil.ReleaseObject(wsf);
                pOldFC = null;
                fws = null;
                ws2 = null;
                ws = null;
                wsf = null;
                pMap = null;
                _pMapDocument = null;

                //Do not make any call to ArcObjects after ShutDownApplication()
                if (m_AOLicenseInitializer != null) m_AOLicenseInitializer.ShutdownApplication();
                m_AOLicenseInitializer = null;

            }
        }

        public string PublishMap(string sServiceName,
                                 string sServer,
                                 string sMxd,
                                 bool bSde,
                                 int minInstances,
                                 int maxInstances,
                                 int waitTimeout,
                                 int usageTimeout,
                                 int idleTimeout)
        {
            if (sServer != null)
                _ArcGISServer = sServer;

            if (sMxd != null)
                _sNewDocument = sMxd;

            if (sServiceName != null)
                _sTable = sServiceName;

            try
            {
                MapService service = new MapService();

                // Check if MapService already exists
                if (service.Exists(_ArcGISServer, _sTable))
                {
                    return "Service " + _sTable + " already exists.";
                }

                service.Create(_ArcGISServer,
                                _sTable,
                                _sNewDocument,
                                true,
                                true,
                                "C:\\arcgisserver\\arcgisoutput",
                                "http://" + _ArcGISServer + "/arcgisoutput",
                                minInstances,
                                maxInstances,
                                waitTimeout,
                                usageTimeout,
                                idleTimeout,
                                bSde);

                service.Start(_ArcGISServer, _sTable);
            }
            catch (Exception ex)
            {
                throw ex;
            }

            return "http://" + _ArcGISServer + "/arcgis/rest/services/" + _sTable + "/FeatureServer";
        }

#if _WIN64
        public string PublishMap10_1(string sServiceName,
                                 string sServer,
                                 string sMxd,                                 
                                 string sMxdOutputDir,
                                 string sAgsConnFile,
                                 string sPythonScriptFile)
        {
            string sPubResults = string.Empty;

            if (sServer != null)
                _ArcGISServer = sServer;

            if (sMxd != null)
                _sNewDocument = sMxd;

            if (sServiceName != null)
                _sTable = sServiceName;

            try
            {
                

                MapService10_1 pService = new MapService10_1();

                // Check if MapService already exists
                if (pService.Exists(_ArcGISServer, _sTable))
                {
                    return "Service " + _sTable + " already exists.";
                }

                // Publish Map
                sPubResults = pService.PublishMapToServer(sAgsConnFile,
                                                          sServiceName,
                                                          _sNewDocument,
                                                          sMxdOutputDir,
                                                          "",
                                                          sPythonScriptFile);
                // If Not empty then return now
                if (!(string.IsNullOrEmpty(sPubResults)))
                {
                    return sPubResults;
                }

                // Return URL
                return "http://" + _ArcGISServer + ":6080/arcgis/rest/services/" + _sTable + "/FeatureServer";

            }
            catch (Exception ex)
            {
                throw ex;
            }
            
        }
#endif

        public bool UnPublishMap(string sServiceName, string sServer, string sPathToMxd, string sServiceFolder, string sOverLayGDB)
        {
            if (sServer != null)
                _ArcGISServer = sServer;

            if (sPathToMxd != null)
                _sNewDocument = sPathToMxd + "\\" + sServiceName + ".mxd";

            if (sServiceName != null)
                _sTable = sServiceName;

            if (sServiceFolder != null)
            {
                _sTable = sServiceFolder + "/" + _sTable;
            }

            try
            {
                MapService service = new MapService();

                // Check if MapService already exists
                if (service.Exists(_ArcGISServer, _sTable))
                {
                    // Stop and Delete MapService
                    service.Stop(_ArcGISServer, _sTable, true);
                }

                // Check if service got removed
                if (service.Exists(_ArcGISServer, _sTable))
                {
                    return false;
                }

                // Delete MXD
                DeleteMxd(sPathToMxd, sServiceName);

                // Check if MXD was deleted
                if (File.Exists(sPathToMxd + "\\" + sServiceName + ".mxd")) return false;

                return true;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private ESRI.ArcGIS.Carto.IMap PrivateCreateMxd(string sPathToMXDToSave)
        {

            try
            {

                _pMapDocument = new MapDocumentClass();

                //Copy the MXD
                System.IO.File.Copy(_sMxdTemplate, sPathToMXDToSave, true);

                _sNewDocument = sPathToMXDToSave;

                if (_pMapDocument.get_IsMapDocument(_sNewDocument))
                {
                    _pMapDocument.Open(_sNewDocument, null);
                    IMap pMap;
                    pMap = _pMapDocument.get_Map(0);
                    return pMap;
                }

                return null;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private void SaveMXD(string sName, ESRI.ArcGIS.Carto.IMap map)
        {
            
            try
            {
                _pMapDocument.Save(true, false);
            }
            catch
            {
                _pMapDocument.Save(false, false);

            }
            _pMapDocument.Close();
        }

        public void DeleteMxd(string sPathToMXD, string sMapServiceName)
        {
            string sToDelete = sPathToMXD + "\\" + sMapServiceName + ".mxd";

            if (File.Exists(sToDelete))
            {
                try
                {
                    System.IO.File.Delete(sToDelete);
                }
                catch (Exception et)
                {
                    throw et;
                }
            }

        }

        private string CreateFGDBFromTemplate(string templateGDB, string overlayGDB)
        {
            bool bOK = false;
            string resultOverlayGDB = "";
            string fileName;
            string filePath;
            try
            {
                // Create resultOverlayGDB file name
                FileInfo fiOverlayGDB = new FileInfo(overlayGDB);
                fileName = fiOverlayGDB.Name;
                filePath = fiOverlayGDB.DirectoryName;
                resultOverlayGDB = System.IO.Path.Combine(filePath, "Result_" + fileName);
                bOK = CopyDirectory(templateGDB, resultOverlayGDB, true);

                if (!(bOK)) return null;

                return resultOverlayGDB;
            }
            catch (Exception ex)
            {
                throw ex;
            }

        }

        private void SetDataFrameExtents(IFeatureClass pFC)
        {
            IPageLayout pPageLayout;
            IGraphicsContainer pGraphContainer = null;
            IMapFrame pMapFrame;
            IElement pElement;
            IActiveView pActiveView;
            IGeoDataset pIGeoDataSet = null;

            try
            {
                pPageLayout = _pMapDocument.PageLayout;
                pGraphContainer = (IGraphicsContainer)pPageLayout;
                pActiveView = (IActiveView)pPageLayout;

                pGraphContainer.Reset();
                pElement = pGraphContainer.Next();

                while (!(pElement == null))
                {
                    if (pElement is IMapFrame)
                    {
                        pMapFrame = (IMapFrame)pElement;
                        pIGeoDataSet = pFC as IGeoDataset;

                        pMapFrame.ExtentType = esriExtentTypeEnum.esriExtentBounds;
                        pMapFrame.MapBounds = pIGeoDataSet.Extent;
                        pActiveView.Refresh();
                        return;

                    }
                    pElement = pGraphContainer.Next();
                }

            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                COMUtil.ReleaseObject(pGraphContainer);
            }
        }

        public static bool CopyDirectory(string SourcePath, string DestPath, bool Overwrite = false)
        {
            DirectoryInfo SourceDir = default(DirectoryInfo);
            DirectoryInfo DestDir = default(DirectoryInfo);
            bool bOk = true;
            // the source directory must exist, otherwise throw an exception
            try
            {
                SourceDir = new DirectoryInfo(SourcePath);
                DestDir = new DirectoryInfo(DestPath);
                if (SourceDir.Exists)
                {
                    // if destination SubDir's parent SubDir does not exist throw an exception
                    if (!DestDir.Parent.Exists)
                    {
                        throw new DirectoryNotFoundException("Destination directory does not exist: " + DestDir.Parent.FullName);
                    }

                    if (!DestDir.Exists)
                    {
                        DestDir.Create();
                    }
                    // copy all the files of the current directory
                    //FileInfo ChildFile = default(FileInfo);
                    foreach (FileInfo ChildFile in SourceDir.GetFiles())
                    {
                        if (Overwrite)
                        {
                            ChildFile.CopyTo(System.IO.Path.Combine(DestDir.FullName, ChildFile.Name), true);
                        }
                        else
                        {
                            // if Overwrite = false, copy the file only if it does not exist
                            if (!File.Exists(System.IO.Path.Combine(DestDir.FullName, ChildFile.Name)))
                            {
                                ChildFile.CopyTo(System.IO.Path.Combine(DestDir.FullName, ChildFile.Name), false);
                            }
                        }
                    }

                    // copy all the sub-directories by recursively calling this same routine
                    //DirectoryInfo SubDir = default(DirectoryInfo);
                    foreach (DirectoryInfo SubDir in SourceDir.GetDirectories())
                    {
                        CopyDirectory(SubDir.FullName, System.IO.Path.Combine(DestDir.FullName, SubDir.Name), Overwrite);
                    }
                }
                else
                {
                    throw new DirectoryNotFoundException("Source directory does not exist: " + SourceDir.FullName);
                }
                bOk = true;
            }
            catch
            {
                bOk = false;
            }
            return bOk;
        }

        public string DeleteGdb(string sPathToGdb)
        {

            DirectoryInfo pGDB;
            FileInfo[] fiFileList;

            try
            {
                // Get DI object and get file collection to see if there are any locks. 
                if (!(Directory.Exists(sPathToGdb)))
                {
                    return "FGDB " + sPathToGdb + " did not exist.";
                }

                pGDB = new DirectoryInfo(sPathToGdb);
                if (!(pGDB == null))
                {
                    fiFileList = pGDB.GetFiles("*.lock");

                    if (fiFileList.Length > 0)
                    {
                        return "Error in MxdManger.DeleteGdb. Schema locks were found on " + sPathToGdb;
                    }

                    fiFileList = null;

                    // Delete FGDB.
                    try
                    {
                        pGDB.Delete(true);
                    }
                    catch (Exception et)
                    {
                        return "Error in MxdManger.DeleteGdb. Error deleting file geodatabase: " + et.Message + " StackTrace: " + et.StackTrace;
                    }

                    return "FGDB " + sPathToGdb + " was successfully removed.";
                }

                return "FGDB " + sPathToGdb + " did not exist.";
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                pGDB = null;
                fiFileList = null;
            }
        }

        public void DeleteFeatureDataset(string sFeatureDataset, string sConnectionFile)
        {
          IWorkspace pWorkspace = null;
          IEnumDataset pEnumDataset = null;
          IDataset pDataset = null;

          try
          {
            pWorkspace = ArcSdeWorkspaceFromFile(sConnectionFile);                     
            
            pEnumDataset = pWorkspace.get_Datasets(esriDatasetType.esriDTAny);
            pDataset = pEnumDataset.Next();

            while (pDataset != null)
            {
              if (pDataset.Name.Contains(sFeatureDataset))
              {
                if (pDataset.CanDelete())
                {
                  pDataset.Delete();
                }
                else
                {
                  
                  throw new ApplicationException("Cannot delete dataset " + pDataset.Name);
                }
              }

              pDataset = pEnumDataset.Next();
            }
          }
          catch (Exception ex)
          {
            throw ex;
          }
          finally
          {
            COMUtil.ReleaseObject(pDataset);
            COMUtil.ReleaseObject(pEnumDataset);
            COMUtil.ReleaseObject(pWorkspace);            
          }
        }

        public static IWorkspace ArcSdeWorkspaceFromFile(String connectionFile)
        {
            Type factoryType = Type.GetTypeFromProgID(
                "esriDataSourcesGDB.SdeWorkspaceFactory");
            IWorkspaceFactory workspaceFactory = (IWorkspaceFactory)Activator.CreateInstance
                (factoryType);
            return workspaceFactory.OpenFromFile(connectionFile, 0);
        }

       
    }


}
