using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ServicePublisher.MXD
{
    public class MxdManager
    {
        private string _sPathToMXD;
        private string _ArcGISServer;
        private string _sServiceFolder;
        private string _sNewDocument;
        private string _sMxdTemplate;

        private const string CHANGEDATASOURCE_PY = "ChangeDatasource.py";
        private const string CHECKFORDATA_PY = "CheckForData.py";

        public MxdManager()
        {
            _sMxdTemplate = "";
        }

        public MxdManager(string sMxdTemplate)
        {
            _sMxdTemplate = sMxdTemplate;
        }

        public bool CreateMxd(string sMxdTemplate,
                              string sPathToMXD,
                              string ArcGISServer,
                              string sMxdFile,
                              string sDBConn,
                              string sDataSet,
                              string sPythonScriptDir,
                              bool bSde)
        {
            if (sMxdTemplate.Length > 0) _sMxdTemplate = sMxdTemplate;

            _sPathToMXD = sPathToMXD;
            _ArcGISServer = ArcGISServer;
            RunPython pRunPython = null;

            try
            {
                // TODO:
                // Verify we have dataset, feature classes, and records
                // Set Args for python
                string sArgs = "\"" + Path.Combine(sPythonScriptDir, CHECKFORDATA_PY) + "\" \"" + sDBConn + "\" \"" + sDataSet + "\"";

                // Run CheckForData python script
                bool bOk = false;
                pRunPython = new RunPython();
                bOk = pRunPython.RunScript(sArgs);

                if (!(bOk)) throw new ApplicationException("Error: Data check failed for " + sMxdFile + Environment.NewLine + pRunPython.ErrOutput);

                // Check if Mxd already exists
                if (File.Exists(sMxdFile)) throw new ApplicationException("Error: " + sMxdFile + " already exists!");
                
                // Create a Mxd from Template
                if (!(PrivateCreateMxd(sMxdFile))) throw new ApplicationException("Error creating Mxd file from template");

                // Set Args for python
                sArgs = "\"" + Path.Combine(sPythonScriptDir, CHANGEDATASOURCE_PY) + "\" \"" + _sNewDocument + "\" \"" + sDBConn + "\" \"" + sDataSet + "\" " + bSde;

                // Run ChangeDatasource python script
                bOk = false;
                pRunPython = new RunPython();
                bOk = pRunPython.RunScript(sArgs);

                if (!(bOk)) throw new ApplicationException("Error changing datasources in " + sMxdFile + Environment.NewLine + pRunPython.ErrOutput);
                
                return true;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

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

            try
            {
                MapService10_1 pService = new MapService10_1();

                // Check if MapService already exists
                if (pService.Exists(_ArcGISServer, sServiceName))
                {
                    return "Service " + sServiceName + " already exists.";
                }

                // Publish Map
                sPubResults = pService.PublishMapToServer(sAgsConnFile,
                                                          sServiceName,
                                                          _sNewDocument,
                                                          sMxdOutputDir,
                                                          "",
                                                          sPythonScriptFile);

                // If results begin with "Error:" return now
                if (sPubResults.StartsWith("Error: "))
                {
                    return sPubResults;
                }

                // Return URL
                return "http://" + _ArcGISServer + ":6080/arcgis/rest/services/" + sServiceName + "/FeatureServer";
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }


        //public bool UnPublishMap(string sServer, string sServiceName, string sServerURL, string sPathToMxd, string sServiceFolder, string sAgsUser, string sAgsPwd, string sPythonScriptFile)
        //{
        //    MapService10_1 pService = null;
        //    string sResults = string.Empty;

        //    try
        //    {
        //        pService = new MapService10_1();

        //        sResults = pService.UnPublish(sServer, sServerURL, sServiceName, sServiceFolder, sAgsUser, sAgsPwd, sPythonScriptFile);

        //        if (sResults.StartsWith("Error: "))
        //        {
        //            return false;
        //        }

        //        // Delete MXD
        //        DeleteMxd(sPathToMxd, sServiceName);

        //        // Check if MXD was deleted
        //        if (File.Exists(sPathToMxd + "\\" + sServiceName + ".mxd")) return false;

        //        return true;
        //    }
        //    catch (Exception ex)
        //    {
        //        throw ex;
        //    }
        //    finally
        //    {
        //        pService = null;
        //    }
        //}

        private bool PrivateCreateMxd(string sPathToMXDToSave)
        {
            try
            {
                //Copy the MXD
                System.IO.File.Copy(_sMxdTemplate, sPathToMXDToSave, true);

                _sNewDocument = sPathToMXDToSave;

                if (System.IO.File.Exists(_sNewDocument))
                {
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                throw ex;
            }
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
                catch (Exception ex)
                {
                    throw ex;
                }
            }

            // Delete SD file if exists.
            sToDelete = sPathToMXD + "\\" + sMapServiceName + ".sd";

            if (File.Exists(sToDelete))
            {
                try
                {
                    System.IO.File.Delete(sToDelete);
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }

        }        

        public void DeleteFeatureDataset(string sFeatureDataset, string sConnectionFile, string sPythonScriptDir)
        {
            RunPython pRunPython = null;

            // Set PythonScript
            string sDeleteDataPyScript = Path.Combine(sPythonScriptDir, "");

            // Set Args for python
            string sArgs = "\"" + sDeleteDataPyScript + "\" \"" + sConnectionFile + "\" \"" + sFeatureDataset + "\"";

            // Run DeleteData python script
            bool bOk = false;
            pRunPython = new RunPython();
            bOk = pRunPython.RunScript(sArgs);

            if (!(bOk)) throw new ApplicationException("Error removing dataset " + sFeatureDataset + Environment.NewLine + pRunPython.ErrOutput);            
        }        

    }

}
