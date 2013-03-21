using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace ServicePublisher.MXD
{
    
    public class RunPython
    {
        private string _stdOutput;
        private string _errOutput;

        public string StdOutput
        {
            get
            {
                return _stdOutput;
            }

        }

        public string ErrOutput
        {
            get
            {
                return _errOutput;
            }
        }
        public bool RunScript(string sArguments)
        {
            return RunPyScript(sArguments, 30);
        }

        public bool RunPyScript(string sArguments, int iTimeOutMinutes)
        {

            System.Diagnostics.ProcessStartInfo procStartInfo = null;
            System.Diagnostics.Process proc = null;
            RegistryKey rk = null;
            string sCommand = string.Empty;
            try
            {
                // Get sPythonDir from registry
                string sPythonDir = @"C:\Python27\ArcGIS10.1\";

                string subkey = @"SOFTWARE\Python\PythonCore\2.7\InstallPath";
                string value64 = string.Empty; 
                string value32 = string.Empty; 
                RegistryKey localKey = RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, RegistryView.Registry64);
                localKey = localKey.OpenSubKey(subkey);
                if (localKey != null)
                {
                    sPythonDir = localKey.GetValue("").ToString();
                }
                else
                {
                    RegistryKey localKey32 = RegistryKey.OpenBaseKey(Microsoft.Win32.RegistryHive.LocalMachine, RegistryView.Registry32);
                    localKey32 = localKey32.OpenSubKey(subkey);
                    if (localKey32 != null)
                    {
                        sPythonDir = localKey32.GetValue("").ToString();
                    }

                }

                sCommand = Path.Combine(sPythonDir, "python.exe") + " " + sArguments;                
                // Create the ProcessStartInfo using "cmd" as the program to be run,
                // and "/c " as the parameters.
                // "/c" tells cmd that you want it to execute the command that follows,
                // then exit.
                procStartInfo = new System.Diagnostics.ProcessStartInfo("cmd", "/c " + sCommand);

                // The following commands are needed to redirect the standard output.
                // This means that it will be redirected to the Process.StandardOutput StreamReader.
                procStartInfo.RedirectStandardOutput = true;
                procStartInfo.RedirectStandardError = true;
                procStartInfo.UseShellExecute = false;

                // Do not create the black window.
                procStartInfo.CreateNoWindow = true;

                // Now you create a process, assign its ProcessStartInfo, and start it.
                proc = new System.Diagnostics.Process();
                proc.StartInfo = procStartInfo;
                proc.Start();
                proc.WaitForExit(iTimeOutMinutes * 60 * 1000);


                // Get the output into a string.
                _errOutput = proc.StandardError.ReadToEnd();
                _stdOutput = proc.StandardOutput.ReadToEnd().TrimEnd('\r', '\n');               

                int exitCode = proc.ExitCode;
                Console.WriteLine("Exit Code: {0}", proc.ExitCode);

                if (exitCode != 0 && (!(string.IsNullOrEmpty(_errOutput))))
                {
                    _errOutput = "Error in RunPyScript! Command: " + sCommand + Environment.NewLine + _errOutput;
                    return false;
                }

                return true;
            }
            catch (Exception objException)
            {
                Console.WriteLine(objException.Message);
                // Log the exception and errors.
                _errOutput = "Error in RunPyScript! Command: " + sCommand + Environment.NewLine + objException.Message;
                return false;
            }
            finally
            {
                if (!(proc == null))
                {
                    if (!(proc.HasExited))
                    {
                        proc.Kill();
                    }
                    proc = null;
                }
                rk = null;
            }
        }

    }
}
