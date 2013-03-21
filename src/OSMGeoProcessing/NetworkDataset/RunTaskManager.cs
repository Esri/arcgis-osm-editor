using System;
using System.Collections.Generic;
using System.Reflection;
using System.Resources;
using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geoprocessing;
using ESRI.ArcGIS.OSM.OSMClassExtension;

namespace ESRI.ArcGIS.OSM.GeoProcessing
{
    public class RunTaskManager : IDisposable
    {
        private static ResourceManager RESMGR = new ResourceManager(
            "ESRI.ArcGIS.OSM.GeoProcessing.OSMGPToolsStrings", Assembly.GetAssembly(typeof(UserCancelException)));

        private ITrackCancel _trackCancel;
        private IGPMessages _messages;
        private List<string> _taskMessages;
        private int _stepCancelCheckThreshold;

        /// <summary>TrackCancel Step Progressor</summary>
        public IStepProgressor StepProgressor { get; private set;  }

        /// <summary>Lazy loaded GeoProcessor object</summary>
        private IGeoProcessor2 _gp;
        public IGeoProcessor2 GP
        {
            get
            {
                if (_gp == null)
                {
                    _gp = new GeoProcessorClass();
                    _gp.OverwriteOutput = true;
                    _gp.AddOutputsToMap = false;
                    _gp.AddToResults = false;

                    LoadCustomToolbox();
                }

                return _gp;
            }
        }

        public RunTaskManager(ITrackCancel trackCancel, IGPMessages messages)
        {
            if (trackCancel == null)
                throw new ArgumentNullException("TrackCancel");
            if (messages == null)
                throw new ArgumentNullException("Messages");

            _trackCancel = trackCancel;
            _messages = messages;
            _taskMessages = new List<string>();

            StepProgressor = _trackCancel as IStepProgressor;
        }

        /// <summary>IDisposable::Dispose - clean up GP if possible</summary>
        public void Dispose()
        {
            if (_gp != null)
            {
                IGPUtilities3 gpUtil = new GPUtilitiesClass();
                gpUtil.ClearInMemoryWorkspace();
                gpUtil.ReleaseInternals();
                gpUtil.RemoveInternalData();
                gpUtil.RemoveInternalValues();

                _gp.ResetEnvironments();
                ComReleaser.ReleaseCOMObject(_gp);
                _gp = null;
            }
        }

        /// <summary>Executes a non-GP method with user cancel exception handling</summary>
        public void ExecuteTask(string messageName, Action task)
        {
            CheckCancel();

            try
            {
                StartTaskMessage(RESMGR.GetString(messageName));
                task();
            }
            catch
            {
                CheckCancel();
                throw;
            }
            finally
            {
                EndTaskMessage();
            }
        }

        /// <summary>Executes a GP tool with user cancel exception handling</summary>
        public IGeoProcessorResult ExecuteTool(string tool, IVariantArray paramArray, string messageName = "")
        {
            CheckCancel();

            try
            {
                string message = string.Empty;
                if (!string.IsNullOrEmpty(messageName))
                    message = RESMGR.GetString(messageName);
                if (string.IsNullOrEmpty(message))
                    message = tool;

                StartTaskMessage(message);
                return GP.Execute(tool, paramArray, _trackCancel);
            }
            catch
            {
                CheckCancel();
                throw;
            }
            finally
            {
                EndTaskMessage();
            }
        }

        /// <summary>Throws a cancel exception if the user cancels the current process</summary>
        public void CheckCancel()
        {
            if (_trackCancel.Continue() == false)
                throw new UserCancelException(RESMGR.GetString("GPTools_OSMGPCreateNetworkDataset_userCancelException"));
        }

        /// <summary>Start TrackCancel progress bar</summary>
        public void StartProgress(int maxRange)
        {
            if (StepProgressor == null)
                return;

            StepProgressor.MinRange = 0;
            StepProgressor.MaxRange = maxRange;
            StepProgressor.Position = 0;
            StepProgressor.StepValue = 1;
            StepProgressor.Show();

            _stepCancelCheckThreshold = Math.Max(1, maxRange / 100);
        }

        /// <summary>Steps TrackCancel progress</summary>
        public void StepProgress()
        {
            if (StepProgressor == null)
                return;

            ++StepProgressor.Position;

            if (StepProgressor.Position % _stepCancelCheckThreshold == 0)
                CheckCancel();
        }

        /// <summary>Ends TrackCancel progress meter</summary>
        public void EndProgress()
        {
            if (StepProgressor == null)
                return;

            StepProgressor.Hide();
        }

        /// <summary>Sets a UI message for the current process</summary>
        private void StartTaskMessage(string message)
        {
            if (_trackCancel.Progressor != null)
                _trackCancel.Progressor.Message = message;

            IGPMessage msg = new GPMessageClass();
            msg.Description = string.Empty;
            msg.Type = esriGPMessageType.esriGPMessageTypeProcessDefinition;
            _messages.Add(msg);

            IGPMessage msgStart = new GPMessageClass();
            msgStart.Description = message;
            msgStart.Type = esriGPMessageType.esriGPMessageTypeProcessStart;
            _messages.Add(msgStart); 
            
            _taskMessages.Add(message);
        }

        /// <summary>Resets the previous UI message</summary>
        private void EndTaskMessage()
        {
            if (_taskMessages.Count > 0)
                _taskMessages.RemoveAt(_taskMessages.Count - 1);

            if (_trackCancel.Progressor != null)
            {
                if (_taskMessages.Count > 0)
                    _trackCancel.Progressor.Message = _taskMessages[_taskMessages.Count - 1];
                else
                    _trackCancel.Progressor.Message = string.Empty;
            }

            IGPMessage msg = new GPMessageClass();
            msg.Description = string.Empty;
            msg.Type = esriGPMessageType.esriGPMessageTypeProcessStop;
            _messages.Add(msg);
        }

        /// <summary>Loads the custom OSM toolbox to the geoprocessor</summary>
        private void LoadCustomToolbox()
        {
            const string osmToolbox = @"ArcToolbox\Toolboxes\OpenStreetMap Toolbox.tbx";

            string toolboxPath = System.IO.Path.Combine(OSMGPFactory.GetArcGIS10InstallLocation(), osmToolbox);

            if (System.IO.File.Exists(toolboxPath))
            {
                IGpEnumList list = _gp.ListToolboxes("osmtools");
                if (string.IsNullOrEmpty(list.Next()))
                    _gp.AddToolbox(toolboxPath);
            }
        }
    }

    /// <summary>Exception to indicate user cancel</summary>
    internal class UserCancelException : Exception
    {
        public UserCancelException(string message)
            : base(message)
        {
        }
    }
}
