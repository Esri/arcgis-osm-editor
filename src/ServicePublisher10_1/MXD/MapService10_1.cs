using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.Win32;
using System.Net;

namespace ServicePublisher.MXD
{
    public class MapService10_1
    {

       
        
        public string UnPublish(string sServer, string sServerURL, string sServiceName, string sServiceFolder, string sAgsAdminUser, string sAgsAdminPwd, string sPythonScriptFile)
        {
            //Python Args: -u http://robertb:6080/arcgis -n arcgis -p arcgis -s EmergencyTempWork_1 -f
            RunPython pRunPython = null;

            try
            {
                // Check is service exists
                if (Exists(sServer, sServiceName, sServiceFolder))
                {
                    // Set Args for python
                    string sArgs = "\"" + sPythonScriptFile + "\" -u " + sServerURL + " " +
                                    "-n " + sAgsAdminUser + " -p " + sAgsAdminPwd + " " +
                                    "-s \"" + sServiceName + "\" -f \"" + sServiceFolder + "\"";

                    // Run UnPublish python script
                    bool bOk = false;
                    pRunPython = new RunPython();
                    bOk = pRunPython.RunScript(sArgs);

                    if (bOk)
                    {
                        return pRunPython.StdOutput;
                    }
                    else
                    {
                        return "Error: " + pRunPython.ErrOutput;
                    }
                }

                return "Service " + sServiceName + " does not exist!";
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public bool Exists(string serverName, string serviceName)
        {
            try
            {
                return Exists(serverName, serviceName, "");
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public bool Exists(string serverName, string serviceName, string serviceFolder)
        {
            WebClient client = null;
            try
            {
                // Set serviceFolder
                if (!(string.IsNullOrEmpty(serviceFolder)))
                {
                    serviceFolder = serviceFolder + "/";
                }

                // Call rest to see if service exists
                client = new WebClient();
                string sResults = client.DownloadString("http://" + serverName + ":6080/arcgis/rest/services/" + serviceFolder + serviceName + "/MapServer?f=pjson");
                if (!sResults.Contains("code\": 500,") && !sResults.Contains("code\": 400,")) return true;
                return false;
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
            RunPython pRunPython = null;

            try
            {
                // Set Args for python
                string sArgs = "\"" + sPythonScriptFile + "\" \"" + sMxd + "\" \"" +
                                sOutputSdDir + "\" \"" + sAgsConn + "\" \"" +
                                sServiceName + "\" \"" + sPublishFolder + "\"";

                // Run PublishMxd python script
                bool bOk = false;
                pRunPython = new RunPython();
                bOk = pRunPython.RunScript(sArgs);

                if (bOk)
                {
                    return pRunPython.StdOutput;
                }
                else
                {
                    return "Error: " + pRunPython.ErrOutput;
                }
            }
            catch (Exception ex)
            {
                throw ex;
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

    }
}
