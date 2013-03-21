using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using ESRI.ArcGIS.Server;
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.esriSystem;


using Microsoft.Win32;
using System.Net;
#if _WIN64
using ESRI.ArcGIS.GISClient;
#endif


namespace ServicePublisher.MXD
{
    public class MapService10_1
    {
#if _WIN64


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

        public void Start(string serverName, string serviceName, string sAgsAdminUser, string sAgsAdminPwd)
        {
            serviceName = CleanServiceName(serviceName);

            try
            {
                if (!ConnectAGS(serverName, sAgsAdminUser, sAgsAdminPwd)) throw new ApplicationException("Could not get server connection to " + serverName);
                soAdmin.StartConfiguration(serviceName, "MapServer");

                COMUtil.ReleaseObject(soAdmin);
                soAdmin = null;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public void Stop(string serverName, string serviceName, bool bDelete, string sAgsAdminUser, string sAgsAdminPwd)
        {
            serviceName = CleanServiceName(serviceName);
            try
            {
                if (!ConnectAGS(serverName, sAgsAdminUser, sAgsAdminPwd)) throw new ApplicationException("Could not get server connection to " + serverName);
                soAdmin.StopConfiguration(serviceName, "MapServer");
                if (bDelete == true)
                    soAdmin.DeleteConfiguration(serviceName, "MapServer");

                COMUtil.ReleaseObject(soAdmin);
                soAdmin = null;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public bool Exists(string serverName, string serviceName)
        {
            WebClient client = null;

            try
            {
                // Call rest to see if service exists
                client = new WebClient();
                string sResults = client.DownloadString("http://" + serverName + ":6080/arcgis/rest/services/" + serviceName + "/MapServer?f=pjson");
                return !sResults.Contains("code\": 500,");
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                client = null;
            }

        }

        private string CleanServiceName(string sName)
        {
            return sName.Replace(" ", "_");
        }

        public string PublishMapToServer(string sAgsConn, string sServiceName, string sMxd, string sOutputSdDir, string sPublishFolder, string sPythonScriptFile)
        {
            RegistryKey rk = null;
            try
            {
                // Get sPythonDir from registry
                string sPythonDir = @"C:\Python27\ArcGIS10.1\";
                object oPythonDir = @"C:\Python27\";

                string subkey = @"SOFTWARE\Wow6432Node\ESRI\Python10.1";
                rk = Registry.LocalMachine.OpenSubKey(subkey);
                if (rk != null)
                {
                    sPythonDir = rk.GetValue("PythonDir", oPythonDir).ToString();
                    sPythonDir = sPythonDir + "ArcGIS10.1\\";

                }
                else
                {
                    subkey = @"SOFTWARE\ESRI\Python10.1";
                    rk = Registry.LocalMachine.OpenSubKey(subkey);
                    if (rk != null)
                    {
                        sPythonDir = rk.GetValue("PythonDir", oPythonDir).ToString();
                        sPythonDir = sPythonDir + "ArcGIS10.1\\";
                    }
                }

                //string sScript = @"C:\Data\Tools\PublishMxd.py";
                string sScript = sPythonScriptFile;

                string sCommand = sPythonDir + "python.exe \"" + sScript + "\" \"" +
                                  sMxd + "\" \"" + sOutputSdDir + "\" \"" +
                                  sAgsConn + "\" \"" + sServiceName + "\" \"" +
                                  sPublishFolder + "\"";

                string sResults = ExecuteCommand(sCommand);

                if (sResults.Length < 2) sResults = string.Empty;

                return sResults;
            }
            catch (Exception ex)
            {
                throw ex;
            }
            finally
            {
                rk = null;
            }
        }

        private static bool ConnectAGS(string host, string sAgsAdminUser, string sAgsAdminPwd)
        {
            try
            {
                IPropertySet propertySet = new PropertySetClass();
                propertySet.SetProperty("url", host);
                propertySet.SetProperty("user", sAgsAdminUser);
                propertySet.SetProperty("password", sAgsAdminPwd);
                propertySet.SetProperty("ConnectionMode", esriAGSConnectionMode.esriAGSConnectionModePublisher);
                propertySet.SetProperty("ServerType", esriAGSServerType.esriAGSServerTypeDiscovery);

                IAGSServerConnectionName3 connectName = new AGSServerConnectionNameClass() as IAGSServerConnectionName3;
                connectName.ConnectionProperties = propertySet;

                IAGSServerConnectionAdmin agsAdmin = ((IName)connectName).Open() as IAGSServerConnectionAdmin;
                soAdmin = agsAdmin.ServerObjectAdmin as IServerObjectAdmin;
                return true;
            }
            catch (Exception exc)
            {
                Console.WriteLine("Error: Couldn't connect to AGSServer: {0}. Message: {1}", host, exc.Message);
                return false;
            }
        }

        private static string ExecuteCommand(object command)
        {
            try
            {
                // Create the ProcessStartInfo using "cmd" as the program to be run,
                // and "/c " as the parameters.
                // "/c" tells cmd that you want it to execute the command that follows,
                // then exit.
                System.Diagnostics.ProcessStartInfo procStartInfo = new
                    System.Diagnostics.ProcessStartInfo("cmd", "/c " + command);

                // The following commands are needed to redirect the standard output.
                // This means that it will be redirected to the Process.StandardOutput StreamReader.
                procStartInfo.RedirectStandardOutput = true;
                procStartInfo.RedirectStandardError = true;
                procStartInfo.UseShellExecute = false;

                // Do not create the black window.
                procStartInfo.CreateNoWindow = true;

                // Now you create a process, assign its ProcessStartInfo, and start it.
                System.Diagnostics.Process proc = new System.Diagnostics.Process();
                proc.StartInfo = procStartInfo;
                proc.Start();


                // Get the output into a string.
                string sErrorResult = proc.StandardError.ReadToEnd();
                string result = proc.StandardOutput.ReadToEnd();

                // Combine any errors and messages
                result += sErrorResult;

                // Display the command output.
                Console.WriteLine(result);

                return result;
            }
            catch (Exception objException)
            {
                Console.WriteLine(objException.Message);
                // Log the exception and errors.

                return "Error: " + objException.Message;
            }
        }
#endif
    }
}
