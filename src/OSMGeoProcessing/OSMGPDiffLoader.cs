// (c) Copyright Esri, 2010 - 2016
// This source is subject to the Apache 2.0 License.
// Please see http://www.apache.org/licenses/LICENSE-2.0.html for details.
// All other rights reserved.

using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Resources;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geoprocessing;
using ESRI.ArcGIS.esriSystem;
using System.IO;
using System.Net;
using System.Linq;
using System.Text.RegularExpressions;
using System.IO.Compression;
using System.Xml.Serialization;
using ESRI.ArcGIS.OSM.OSMClassExtension;
using System.Globalization;
using ESRI.ArcGIS.Display;
using ESRI.ArcGIS.OSM.OSMUtilities;

namespace ESRI.ArcGIS.OSM.GeoProcessing
{
    [Guid("d7ee4012-2344-4f90-93c3-6c36a7c0aba4")]
    [ClassInterface(ClassInterfaceType.None)]
    [ProgId("OSMEditor.OSMGPDiffLoader")]
    public class OSMGPDiffLoader : ESRI.ArcGIS.Geoprocessing.IGPFunction2
    {
        string m_DisplayName = String.Empty;
        int in_downloadURLNumber, in_targetDatasetNumber, in_startSyncTimeNumber, in_updateinsideAOINumber, in_verboseLoggingNumber, out_targetDatasetNumber;
        ResourceManager resourceManager = null;
        ISpatialReference m_wgs84 = null;
        Dictionary<string, string> m_editorConfigurationSettings = null;
        OSMGPFactory osmGPFactory = null;
        OSMUtility _osmUtility = null;

        public OSMGPDiffLoader()
        {
            resourceManager = new ResourceManager("ESRI.ArcGIS.OSM.GeoProcessing.OSMGPToolsStrings", this.GetType().Assembly);
            osmGPFactory = new OSMGPFactory();
            _osmUtility = new OSMUtility();

            m_editorConfigurationSettings = OSMGPFactory.ReadOSMEditorSettings();

            ISpatialReferenceFactory spatialReferenceFactory = new SpatialReferenceEnvironmentClass() as ISpatialReferenceFactory;
            m_wgs84 = spatialReferenceFactory.CreateGeographicCoordinateSystem((int)esriSRGeoCSType.esriSRGeoCS_WGS1984) as ISpatialReference;

        }

        #region "IGPFunction2 Implementations"
        public ESRI.ArcGIS.esriSystem.UID DialogCLSID
        {
            get
            {
                return null;
            }
        }

        public string DisplayName
        {
            get
            {
                if (string.IsNullOrEmpty(m_DisplayName))
                {
                   m_DisplayName = osmGPFactory.GetFunctionName(OSMGPFactory.m_DiffLoaderName).DisplayName;
                }

                return m_DisplayName;
            }
        }

        private ESRI.ArcGIS.Geodatabase.IGPMessages _message;
        public string[] Logs
        {
            get
            {
                try
                {
                    string[] stemp = new string[_message.Count];
                    for (int i = 0; i < _message.Count; i++)
                    {
                        IGPMessage msg = _message.GetMessage(i);
                        stemp[i] = msg.Description;
                    }

                    return stemp;
                }
                catch { }

                return null;
            }
        }

        /// <summary>
        /// Diff tool
        /// </summary>
        /// <param name="paramvalues"></param>
        /// <param name="TrackCancel"></param>
        /// <param name="envMgr"></param>
        /// <param name="message"></param>
        public void Execute(IArray paramvalues, ITrackCancel TrackCancel, IGPEnvironmentManager envMgr, IGPMessages message)
        {
            _message = message;
            IGPUtilities3 gpUtilities3 = new GPUtilitiesClass() as IGPUtilities3;

            if (TrackCancel == null)
            {
                TrackCancel = new CancelTrackerClass();
            }

            string hourReplicateURL = "http://planet.openstreetmap.org/replication/hour/";
            string minuteReplicateURL = "http://planet.openstreetmap.org/replication/minute/";

            try
            {
                // load the descriptions from which to derive the domain values
                OSMDomains availableDomains = null;

                // Reading the XML document requires a FileStream.
                System.Xml.XmlTextReader reader = new System.Xml.XmlTextReader(m_editorConfigurationSettings["osmdomainsfilepath"]);

                System.Xml.Serialization.XmlSerializer domainSerializer = null;

                try
                {
                    domainSerializer = new XmlSerializer(typeof(OSMDomains));
                    availableDomains = domainSerializer.Deserialize(reader) as OSMDomains;
                }
                catch
                {
                }
                reader.Close();

                IGPParameter downloadURLParameter = paramvalues.get_Element(in_downloadURLNumber) as IGPParameter;
                IGPString downloadURLString = gpUtilities3.UnpackGPValue(downloadURLParameter) as IGPString;

                if (downloadURLString == null)
                {
                    message.AddError(120048, string.Format(resourceManager.GetString("GPTools_NullPointerParameterType"), downloadURLParameter.Name));
                    return;
                }

                hourReplicateURL = downloadURLString.Value + @"/hour/";
                minuteReplicateURL = downloadURLString.Value + @"/minute/";

                IGPParameter targetDatasetParameter = paramvalues.get_Element(in_targetDatasetNumber) as IGPParameter;
                IDEDataset2 targetDEDataset2 = gpUtilities3.UnpackGPValue(targetDatasetParameter) as IDEDataset2;

                if (targetDEDataset2 == null)
                {
                    message.AddError(120048, string.Format(resourceManager.GetString("GPTools_NullPointerParameterType"), targetDatasetParameter.Name));
                    return;
                }

                
                string targetDatasetName = ((IGPValue)targetDEDataset2).GetAsText();

                IDataElement targetDataElement = targetDEDataset2 as IDataElement;
                IDataset targetDataset = gpUtilities3.OpenDatasetFromLocation(targetDataElement.CatalogPath);

                string baseName = targetDataset.Name;

                DateTime syncStartDateTime = DateTime.MinValue;

                IGPParameter startSyncTimeParameter = paramvalues.get_Element(in_startSyncTimeNumber) as IGPParameter;
                IGPValue startSyncTimeGPValue = gpUtilities3.UnpackGPValue(startSyncTimeParameter);

                IGPParameter updateInsideAOIParameter = paramvalues.get_Element(in_updateinsideAOINumber) as IGPParameter;
                IGPBoolean updateInsideAOIGPBoolean = gpUtilities3.UnpackGPValue(updateInsideAOIParameter) as IGPBoolean;

                if (updateInsideAOIGPBoolean == null)
                {
                    message.AddError(120048, string.Format(resourceManager.GetString("GPTools_NullPointerParameterType"), updateInsideAOIParameter.Name));
                    return;
                }

                IGPParameter useVerboseLoggingParameter = paramvalues.get_Element(in_verboseLoggingNumber) as IGPParameter;
                IGPBoolean useVerboseLoggingGPBoolean = gpUtilities3.UnpackGPValue(useVerboseLoggingParameter) as IGPBoolean;

                if (useVerboseLoggingGPBoolean == null)
                {
                    message.AddError(120048, string.Format(resourceManager.GetString("GPTools_NullPointerParameterType"), useVerboseLoggingParameter.Name));
                    return;
                }

                // parse the given date time value
                if (startSyncTimeGPValue != null && startSyncTimeGPValue.IsEmpty() == false)
                {
                    syncStartDateTime = Convert.ToDateTime(startSyncTimeGPValue.GetAsText());
                }
                else
                {
                    syncStartDateTime = SyncState.RetrieveLastSyncTime(targetDatasetName);
                }

                message.AddMessage(resourceManager.GetString(""));

                // determine the current state of the server holding the diff files - the OpenStreetMap perception of time
                // it is assumed that the diff files are at the following location
                // hourly diffs at http://planet.openstreetmap.org/replication/hour/
                // minutes diffs at http://planet.openstreetmap.org/replication/minute/

                string urlAddition = String.Empty;

                # region load hour diff
                // determine the current latest files to establish a point in time for the hour diff and minute diff files
                string tempHourlyURL = hourReplicateURL;
                while (String.IsNullOrEmpty(urlAddition = GetMostCurrentFolder(tempHourlyURL)) == false)
                {
                    tempHourlyURL = tempHourlyURL + urlAddition;
                }

                string latestHourStateFile = GetMostCurrentSync(tempHourlyURL);

                int hourSequenceNumber = 0;
                DateTime hourSyncDateTime = DateTime.MinValue;
                GetLatestSyncInformation(tempHourlyURL + latestHourStateFile, out hourSequenceNumber, out hourSyncDateTime);

                // determine which hour diffs to download
                TimeSpan syncTimeSpan = hourSyncDateTime.Subtract(syncStartDateTime);

                // if there is a difference in synchronization of more than 1 hour let us the hour replicate file to bring the state under 1 hour
                // then we'll switch to the minutes
                if (syncTimeSpan.TotalHours > 1)
                {
                    // assemble a dictionary of download urls for the hour replicate files
                    int startHourSequence = hourSequenceNumber - (int)syncTimeSpan.TotalHours - 1;
                    if (startHourSequence < 1)
                        startHourSequence = 1;

                    Dictionary<string, DateTime> hourReplicateDictionary = new Dictionary<string, DateTime>();

                    for (int sequenceNumber = startHourSequence; sequenceNumber <= hourSequenceNumber; sequenceNumber++)
                    {
                        string hourURL = ConvertSequenceNumbertoURL(hourReplicateURL, sequenceNumber);
                        DateTime hourDateTime = hourSyncDateTime.AddHours(sequenceNumber - hourSequenceNumber);

                        hourReplicateDictionary.Add(hourURL, hourDateTime);
                    }

                    System.Xml.Serialization.XmlSerializer serializer = null;
                    serializer = new XmlSerializer(typeof(osmChange));

                    // get the capabilities from the server
                    HttpWebResponse httpResponse = null;

                    message.AddMessage(resourceManager.GetString("GPTools_OSMGPDownload_startingDownloadRequest"));

                    foreach (var item in hourReplicateDictionary)
                    {
                        // if difference between current hour and next url is more 1 hour continue
                        TimeSpan currentTimespan = DateTime.Now.ToUniversalTime().Subtract(item.Value);

                        if (currentTimespan.TotalHours > 1)
                        {
                            osmChange osmChangeObject = null;
                            try
                            {
                                HttpWebRequest httpClient = HttpWebRequest.Create(item.Key) as HttpWebRequest;
                                httpClient = OSMGPDownload.AssignProxyandCredentials(httpClient);
                                    try
                                    {
                                        // download the replicate file
                                        httpResponse = httpClient.GetResponse() as HttpWebResponse;                                        
                                    }
                                    catch (Exception ex)
                                    {
                                        message.AddWarning(String.Format(resourceManager.GetString("GPTools_OSMGPDiffLoader_downloadProblem"), item.Key));
                                        message.AddWarning(ex.Message);
                                        continue;
                                    }

                                using (MemoryStream memoryStream = new MemoryStream())
                                {
                                    using (GZipStream gzipStream = new GZipStream(httpResponse.GetResponseStream(), CompressionMode.Decompress))
                                    {
                                        //Copy the decompression stream into the output file.
                                        byte[] buffer = new byte[4096];
                                        int numRead;
                                        while ((numRead = gzipStream.Read(buffer, 0, buffer.Length)) != 0)
                                        {
                                            memoryStream.Write(buffer, 0, numRead);
                                        }
                                        // reposition the stream to the beginning
                                        memoryStream.Position = 0;

                                        // deserialize the content
                                        osmChangeObject = serializer.Deserialize(memoryStream) as osmChange;
                                    }
                                }

                                if (TrackCancel.Continue() == false)
                                {
                                    return;
                                }

                                if (osmChangeObject != null)
                                {
                                    message.AddMessage(String.Format(resourceManager.GetString("GPTools_OSMGPDiffLoader_processing_diff"), resourceManager.GetString("GPTools_OSMGPDiffLoader_hour"), item.Key.Substring(hourReplicateURL.Length)));

                                    DateTime startTime = DateTime.Now;

                                    // loop through the create/modify/delete nodes of the document
                                    parseDiffFile(osmChangeObject, targetDataset.Workspace, baseName, availableDomains, updateInsideAOIGPBoolean.Value, useVerboseLoggingGPBoolean.Value, TrackCancel, message);

                                    if (useVerboseLoggingGPBoolean.Value)
                                    {
                                        TimeSpan loadingTime = new TimeSpan(DateTime.Now.Ticks - startTime.Ticks);

                                        message.AddMessage(String.Format(resourceManager.GetString("GPTools_OSMGPDiffLoader_loadtime_message"), loadingTime.TotalMinutes));
                                    }

                                    osmChangeObject = null;
                                }

                                SyncState.StoreLastSyncTime(targetDatasetName, item.Value);
                            }
                            catch (Exception ex)
                            {
                                message.AddWarning(ex.Message);
                                message.AddWarning(ex.StackTrace);
                            }
                        }
                    }
                }
                #endregion

                # region load minute diffs
                // determine which minute diffs to download
                // determine the current latest files to establish a point in time for the hour diff and minute diff files
                urlAddition = String.Empty;
                string tempMinutelyURL = minuteReplicateURL;
                while (String.IsNullOrEmpty(urlAddition = GetMostCurrentFolder(tempMinutelyURL)) == false)
                {
                    tempMinutelyURL = tempMinutelyURL + urlAddition;
                }

                string latestMinuteStateFile = GetMostCurrentSync(tempMinutelyURL);

                int minuteSequenceNumber = 0;
                DateTime minuteSyncDateTime = DateTime.MinValue;
                GetLatestSyncInformation(tempMinutelyURL + latestMinuteStateFile, out minuteSequenceNumber, out minuteSyncDateTime);

                if (TrackCancel.Continue() == false)
                {
                    return;
                }

                syncStartDateTime = SyncState.RetrieveLastSyncTime(targetDatasetName);

                // determine which minute diffs to download
                syncTimeSpan = minuteSyncDateTime.Subtract(syncStartDateTime);

                // if there is a difference in synchronization of more than 1 hour let us the hour replicate file to bring the state under 1 hour
                // then we'll switch to the minutes
                if (syncTimeSpan.TotalMinutes > 10)
                {
                    // assemble a dictionary of download urls for the hour replicate files
                    int startMinuteSequence = minuteSequenceNumber - (int)syncTimeSpan.TotalMinutes;

                    Dictionary<string, DateTime> minuteReplicateDictionary = new Dictionary<string, DateTime>();

                    for (int sequenceNumber = startMinuteSequence; sequenceNumber <= minuteSequenceNumber; sequenceNumber++)
                    {
                        string minuteURL = ConvertSequenceNumbertoURL(minuteReplicateURL, sequenceNumber);
                        DateTime minuteDateTime = minuteSyncDateTime.AddMinutes(sequenceNumber - minuteSequenceNumber);

                        minuteReplicateDictionary.Add(minuteURL, minuteDateTime);
                    }

                    System.Xml.Serialization.XmlSerializer serializer = null;
                    serializer = new XmlSerializer(typeof(osmChange));

                    // get the capabilities from the server
                    HttpWebResponse httpResponse = null;

                    message.AddMessage(resourceManager.GetString("GPTools_OSMGPDownload_startingDownloadRequest"));

                    foreach (var item in minuteReplicateDictionary)
                    {
                        // if difference between current minute and next url is more then 10 minutes continue
                        TimeSpan currentTimespan = DateTime.Now.ToUniversalTime().Subtract(item.Value);

                        //message.AddMessage("now: " + DateTime.Now.ToUniversalTime() + " item.value: " + item.Value.ToString() + " timespan (min): " + currentTimespan.TotalMinutes);

                        if (currentTimespan.TotalMinutes > 10)
                        {
                            osmChange osmChangeObject = null;
                            try
                            {
                                HttpWebRequest httpClient = HttpWebRequest.Create(item.Key) as HttpWebRequest;
                                httpClient = OSMGPDownload.AssignProxyandCredentials(httpClient);

                                    try
                                    {
                                        // download the replicate file
                                        httpResponse = httpClient.GetResponse() as HttpWebResponse;
                                    }
                                    catch (Exception ex)
                                    {
                                        message.AddWarning(String.Format(resourceManager.GetString("GPTools_OSMGPDiffLoader_downloadProblem"), item.Key));
                                        message.AddWarning(ex.Message);
                                        continue;
                                    }

                                using (MemoryStream memoryStream = new MemoryStream())
                                {
                                    using (GZipStream gzipStream = new GZipStream(httpResponse.GetResponseStream(), CompressionMode.Decompress))
                                    {
                                        //Copy the decompression stream into the output file.
                                        byte[] buffer = new byte[4096];
                                        int numRead;
                                        while ((numRead = gzipStream.Read(buffer, 0, buffer.Length)) != 0)
                                        {
                                            memoryStream.Write(buffer, 0, numRead);
                                        }
                                        // reposition the stream to the beginning
                                        memoryStream.Position = 0;

                                        // deserialize the content
                                        osmChangeObject = serializer.Deserialize(memoryStream) as osmChange;
                                    }
                                }

                                if (TrackCancel.Continue() == false)
                                {
                                    return;
                                }

                                if (osmChangeObject != null)
                                {
                                    message.AddMessage(String.Format(resourceManager.GetString("GPTools_OSMGPDiffLoader_processing_diff"), resourceManager.GetString("GPTools_OSMGPDiffLoader_minute"), item.Key.Substring(minuteReplicateURL.Length)));
                                    DateTime startTime = DateTime.Now;

                                    // loop through the create/modify/delete nodes of the document
                                    parseDiffFile(osmChangeObject, targetDataset.Workspace, baseName, availableDomains, updateInsideAOIGPBoolean.Value, useVerboseLoggingGPBoolean.Value, TrackCancel, message);

                                    if (useVerboseLoggingGPBoolean.Value)
                                    {
                                        TimeSpan loadingTime = new TimeSpan(DateTime.Now.Ticks - startTime.Ticks);

                                        message.AddMessage(String.Format(resourceManager.GetString("GPTools_OSMGPDiffLoader_loadtime_message"), loadingTime.TotalMinutes));
                                    }

                                    osmChangeObject = null;
                                }

                                // update sync state file on disk
                                SyncState.StoreLastSyncTime(targetDatasetName, item.Value);
                            }
                            catch (Exception ex)
                            {
                                message.AddWarning(ex.Message);
                                message.AddWarning(ex.StackTrace);
                            }
                        }
                    }
                }
                #endregion
            }
            catch (Exception ex)
            {
                message.AddError(120008, ex.Message);
                message.AddError(120008, ex.StackTrace);
            }

            gpUtilities3.ReleaseInternals();
        }

        public ESRI.ArcGIS.esriSystem.IName FullName
        {
            get
            {
                IName fullName = null;

                if (osmGPFactory != null)
                {
                    fullName = osmGPFactory.GetFunctionName(OSMGPFactory.m_DiffLoaderName) as IName;
                }

                return fullName;
            }
        }

        public object GetRenderer(ESRI.ArcGIS.Geoprocessing.IGPParameter pParam)
        {
            return null;
        }

        public int HelpContext
        {
            get
            {
                return 0;
            }
        }

        public string HelpFile
        {
            get
            {
                return String.Empty;
            }
        }

        public bool IsLicensed()
        {
            return true;
        }

        public string MetadataFile
        {
            get
            {
                string metadafile = "osmgpdiffloader.xml";

                try
                {
                    string[] languageid = System.Threading.Thread.CurrentThread.CurrentUICulture.Name.Split("-".ToCharArray());

                    string ArcGISInstallationLocation = OSMGPFactory.GetArcGIS10InstallLocation();
                    string localizedMetaDataFileShort = ArcGISInstallationLocation + System.IO.Path.DirectorySeparatorChar.ToString() + "help" + System.IO.Path.DirectorySeparatorChar.ToString() + "gp" + System.IO.Path.DirectorySeparatorChar.ToString() + "osmgpdiffloader_" + languageid[0] + ".xml";
                    string localizedMetaDataFileLong = ArcGISInstallationLocation + System.IO.Path.DirectorySeparatorChar.ToString() + "help" + System.IO.Path.DirectorySeparatorChar.ToString() + "gp" + System.IO.Path.DirectorySeparatorChar.ToString() + "osmgpdiffloader_" + System.Threading.Thread.CurrentThread.CurrentUICulture.Name + ".xml";

                    if (System.IO.File.Exists(localizedMetaDataFileShort))
                    {
                        metadafile = localizedMetaDataFileShort;
                    }
                    else if (System.IO.File.Exists(localizedMetaDataFileLong))
                    {
                        metadafile = localizedMetaDataFileLong;
                    }
                }
                catch { }

                return metadafile;
            }
        }

        public string Name
        {
            get
            {
                return OSMGPFactory.m_DiffLoaderName;
            }
        }

        public ESRI.ArcGIS.esriSystem.IArray ParameterInfo
        {
            get
            {
                IArray parameters = new ArrayClass();

                // osm download URL (required)
                IGPParameterEdit3 downloadURL = new GPParameterClass() as IGPParameterEdit3;
                downloadURL.DataType = new GPStringTypeClass();
                downloadURL.Direction = esriGPParameterDirection.esriGPParameterDirectionInput;
                downloadURL.DisplayName = resourceManager.GetString("GPTools_OSMGPDiffLoader_downloadURL_desc");
                downloadURL.Name = "in_osmURL";
                downloadURL.ParameterType = esriGPParameterType.esriGPParameterTypeRequired;

                IGPString urlDownloadString = new GPStringClass();
                if (m_editorConfigurationSettings != null)
                {
                    if (m_editorConfigurationSettings.ContainsKey("osmdiffsurl"))
                    {
                        urlDownloadString.Value = m_editorConfigurationSettings["osmdiffsurl"];
                    }
                }
                downloadURL.Value = urlDownloadString as IGPValue;

                in_downloadURLNumber = 0;
                parameters.Add(downloadURL);

                // target dataset (required)
                IGPParameterEdit3 targetDataset = new GPParameterClass() as IGPParameterEdit3;
                targetDataset.DataType = new DEFeatureDatasetTypeClass();
                targetDataset.Direction = esriGPParameterDirection.esriGPParameterDirectionInput;
                targetDataset.ParameterType = esriGPParameterType.esriGPParameterTypeRequired;
                targetDataset.DisplayName = resourceManager.GetString("GPTools_OSMGPDiffLoader_targetDataset_desc");
                targetDataset.Name = "in_targetdataset";

                IGPDatasetDomain datasetDomain = new GPDatasetDomainClass();
                datasetDomain.AddType(esriDatasetType.esriDTFeatureDataset);

                targetDataset.Domain = datasetDomain as IGPDomain;


                in_targetDatasetNumber = 1;
                parameters.Add(targetDataset);

                // starting sync date
                IGPParameterEdit3 syncTimeString = new GPParameterClass() as IGPParameterEdit3;
                syncTimeString.DataType = new GPStringTypeClass();
                syncTimeString.Direction = esriGPParameterDirection.esriGPParameterDirectionInput;
                syncTimeString.ParameterType = esriGPParameterType.esriGPParameterTypeOptional;
                syncTimeString.DisplayName = resourceManager.GetString("GPTools_OSMGPDiffLoader_syncStartTime_desc");
                syncTimeString.Name = "in_syncstarttime";

                in_startSyncTimeNumber = 2;
                parameters.Add(syncTimeString);

                // option to where to apply the diffs 
                IGPParameterEdit3 insideAOIUpdate = new GPParameterClass() as IGPParameterEdit3;
                IGPCodedValueDomain insideAOIUpdateDomain = new GPCodedValueDomainClass();

                IGPBoolean insideAOITrue = new GPBooleanClass();
                // By changing this, the diff will apply outside of the world
                insideAOITrue.Value = true;
                IGPBoolean insideAOIFalse = new GPBooleanClass();
                insideAOIFalse.Value = false;

                insideAOIUpdateDomain.AddCode((IGPValue)insideAOITrue, "UPDATE_INSIDE_AOI");
                insideAOIUpdateDomain.AddCode((IGPValue)insideAOIFalse, "UPDATE_EVERYTHING");

                insideAOIUpdate.DataType = new GPBooleanTypeClass();
                insideAOIUpdate.Direction = esriGPParameterDirection.esriGPParameterDirectionInput;
                insideAOIUpdate.ParameterType = esriGPParameterType.esriGPParameterTypeRequired;
                insideAOIUpdate.DisplayName = resourceManager.GetString("GPTools_OSMGPDownload_updateinsideaoi_desc");
                insideAOIUpdate.Domain = (IGPDomain)insideAOIUpdateDomain;
                insideAOIUpdate.Value = (IGPValue)insideAOITrue;
                insideAOIUpdate.Name = "in_updateinsideAOI";

                in_updateinsideAOINumber = 3;
                parameters.Add(insideAOIUpdate);


                // option about the action logging
                IGPParameterEdit3 loggingDetails = new GPParameterClass() as IGPParameterEdit3;
                IGPCodedValueDomain loggingDetailsDomain = new GPCodedValueDomainClass();

                IGPBoolean loggingVerboseTrue = new GPBooleanClass();
                loggingVerboseTrue.Value = true;
                IGPBoolean loggingVerboseFalse = new GPBooleanClass();
                loggingVerboseFalse.Value = false;

                loggingDetailsDomain.AddCode((IGPValue)loggingVerboseTrue, "VERBOSE_LOGGING");
                loggingDetailsDomain.AddCode((IGPValue)loggingVerboseFalse, "NORMAL_LOGGING");

                loggingDetails.DataType = new GPBooleanTypeClass();
                loggingDetails.Direction = esriGPParameterDirection.esriGPParameterDirectionInput;
                loggingDetails.ParameterType = esriGPParameterType.esriGPParameterTypeRequired;
                loggingDetails.DisplayName = resourceManager.GetString("GPTools_OSMGPDownload_logginglevel_desc");
                loggingDetails.Domain = (IGPDomain)loggingDetailsDomain;
                loggingDetails.Value = (IGPValue)loggingVerboseFalse;
                loggingDetails.Name = "in_logginglevel";

                in_verboseLoggingNumber = 4;
                parameters.Add(loggingDetails);

                
                IGPParameterEdit3 outtargetDataset = new GPParameterClass() as IGPParameterEdit3;
                outtargetDataset.DataType = new DEFeatureDatasetTypeClass();
                outtargetDataset.Direction = esriGPParameterDirection.esriGPParameterDirectionOutput;
                outtargetDataset.AddDependency("in_targetdataset");
                outtargetDataset.ParameterType = esriGPParameterType.esriGPParameterTypeDerived;
                outtargetDataset.DisplayName = resourceManager.GetString("GPTools_OSMGPDiffLoader_outtargetDataset_desc");
                outtargetDataset.Name = "out_targetdataset";

                datasetDomain = new GPDatasetDomainClass();
                datasetDomain.AddType(esriDatasetType.esriDTFeatureDataset);

                outtargetDataset.Domain = datasetDomain as IGPDomain;

                out_targetDatasetNumber = 5;
                parameters.Add(outtargetDataset);


                return parameters;
            }
        }

        public void UpdateMessages(ESRI.ArcGIS.esriSystem.IArray paramvalues, ESRI.ArcGIS.Geoprocessing.IGPEnvironmentManager pEnvMgr, ESRI.ArcGIS.Geodatabase.IGPMessages Messages)
        {
            // if there is a time string, test if it can be parsed
            IGPUtilities3 gpUtilities3 = new GPUtilitiesClass();

            IGPParameter startSyncTimeParameter = paramvalues.get_Element(in_startSyncTimeNumber) as IGPParameter;
            IGPValue startSyncTimeGPValue = gpUtilities3.UnpackGPValue(startSyncTimeParameter);

            if (startSyncTimeGPValue != null)
            {
                if (startSyncTimeGPValue.IsEmpty() == false)
                {
                    DateTime syncStartDateTime = DateTime.MinValue;

                    try
                    {
                        syncStartDateTime = Convert.ToDateTime(startSyncTimeGPValue.GetAsText());
                    }
                    catch (Exception ex)
                    {
                        StringBuilder errorMessage = new StringBuilder();
                        errorMessage.AppendLine(resourceManager.GetString("GPTools_OSMGPDiffLoader_invalidTimeString"));
                        errorMessage.AppendLine(ex.Message);
                        Messages.ReplaceError(in_startSyncTimeNumber, -3, errorMessage.ToString());
                    }

                    // if the time string is before July 1st, 2004 flag it as illegal
                    if (syncStartDateTime.CompareTo(new DateTime(2004, 7, 1)) < 0)
                    {
                        Messages.ReplaceError(in_startSyncTimeNumber, -34, resourceManager.GetString("GPTools_OSMGPDiffLoader_invalidTime_beforeOSM"));
                    }
                }
            }

            // check if there is a valid url -- just a simple syntax check
            IGPParameter downloadURLParameter = paramvalues.get_Element(in_downloadURLNumber) as IGPParameter;
            IGPValue downloadURLGPValue = gpUtilities3.UnpackGPValue(downloadURLParameter);

            if (downloadURLGPValue.IsEmpty() == false)
            {
                Regex regex = new Regex(@"^((http:\/\/www\.)|(www\.)|(http:\/\/))[a-zA-Z0-9._-]+\.[a-zA-Z.]{2,5}");
                MatchCollection matches = regex.Matches(downloadURLGPValue.GetAsText());

                if (matches.Count < 1)
                {
                    Messages.ReplaceError(in_downloadURLNumber, -35, resourceManager.GetString("GPTools_OSMGPDiffLoader_malformed_downloadURL"));
                }
            }

            // check if the input target dataset is a valid OSM container based on the naming convention for the feature classes (_osm_pt, _osm_ln, _osm_ply)

            gpUtilities3.ReleaseInternals();
        }

        public void UpdateParameters(ESRI.ArcGIS.esriSystem.IArray paramvalues, ESRI.ArcGIS.Geoprocessing.IGPEnvironmentManager pEnvMgr)
        {
            // copy input dataset location to output dataset location
            IGPUtilities3 gpUtilities3 = new GPUtilitiesClass() as IGPUtilities3;

            IGPParameter intargetDatasetParameter = paramvalues.get_Element(in_targetDatasetNumber) as IGPParameter;
            IGPValue inTargetDatasetGPValue = gpUtilities3.UnpackGPValue(intargetDatasetParameter);

            IGPParameter outtargetDatasetParameter = paramvalues.get_Element(out_targetDatasetNumber) as IGPParameter;
            gpUtilities3.PackGPValue(inTargetDatasetGPValue, outtargetDatasetParameter);

            gpUtilities3.ReleaseInternals();
        }

        public ESRI.ArcGIS.Geodatabase.IGPMessages Validate(ESRI.ArcGIS.esriSystem.IArray paramvalues, bool updateValues, ESRI.ArcGIS.Geoprocessing.IGPEnvironmentManager envMgr)
        {
            return default(ESRI.ArcGIS.Geodatabase.IGPMessages);
        }
        #endregion

        private string GetDirectoryListingRegexForUrl(string url)
        {
            if (url.Contains("http://planet.openstreetmap.org/"))
            {
                return "<a href=\".*\">(?<name>.*/+)</a>";

            }
            throw new NotSupportedException();
        }

        private string GetStateFileListingRegexForUrl(string url)
        {
            if (url.Contains("http://planet.openstreetmap.org/"))
            {
                return "<a href=\".*\">(?<name>.*.txt)</a>";

            }
            throw new NotSupportedException();
        }

        private string GetMostCurrentFolder(string url)
        {
            string foundMostCurrentFolder = String.Empty;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    string html = reader.ReadToEnd();
                    Regex regex = new Regex(GetDirectoryListingRegexForUrl(url));
                    MatchCollection matches = regex.Matches(html);
                    if (matches.Count > 0)
                    {
                        foundMostCurrentFolder = matches[0].Groups["name"].ToString();
                    }
                }
            }

            return foundMostCurrentFolder;
        }

        private string GetMostCurrentSync(string url)
        {
            string foundMostCurrentSyncFile = String.Empty;
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    string html = reader.ReadToEnd();
                    Regex regex = new Regex(GetStateFileListingRegexForUrl(url));
                    MatchCollection matches = regex.Matches(html);
                    if (matches.Count > 0)
                    {
                        foundMostCurrentSyncFile = matches[0].Groups["name"].ToString();
                    }
                }
            }

            return foundMostCurrentSyncFile;
        }

        private void GetLatestSyncInformation(string url, out int sequenceNumber, out DateTime syncTimeStamp)
        {
            sequenceNumber = 0;
            syncTimeStamp = DateTime.MinValue;

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
            {
                using (StreamReader reader = new StreamReader(response.GetResponseStream()))
                {
                    string currentLine = null;
                    while ((currentLine = reader.ReadLine()) != null)
                    {
                        string [] lineElements = currentLine.Split("=".ToCharArray());

                        if (lineElements.Length == 2)
                        {
                            switch (lineElements[0])
                            {
                                case "sequenceNumber":
                                    try
                                    {
                                        sequenceNumber = Convert.ToInt32(lineElements[1]);
                                    }
                                    catch { }
                                    break;
                                case "timestamp":
                                    try
                                    {
                                        syncTimeStamp = Convert.ToDateTime(lineElements[1].Replace("\\",""));
                                    }
                                    catch { }
                                    break;
                                default:
                                    break;
                            }
                        }
                    }
                }
            }
        }

        private string ConvertSequenceNumbertoURL(string baseURL, int sequenceNumber)
        {
            string downloadURL = String.Empty;

            string formattedSequenceNumber = string.Format("{0:D9}", sequenceNumber);

            downloadURL = baseURL + formattedSequenceNumber.Substring(0,3) + "/" + formattedSequenceNumber.Substring(3,3) + "/" + formattedSequenceNumber.Substring(6,3) + ".osc.gz";

            return downloadURL;
        }

        private void parseDiffFile(osmChange osmChangeDocument, IWorkspace osmWorkspace, string baseName, OSMDomains availableDomains, bool restrictUpdatesToAOI, bool useVerboseLogging, ITrackCancel trackCancel, IGPMessages message)
        {
            OSMToolHelper osmToolHelper = new OSMToolHelper();

            if (osmChangeDocument == null)
            {
                throw new ArgumentNullException(GetName(new { osmChangeDocument }));
            }

            if (osmChangeDocument.Items == null)
            {
                return;
            }

            if (osmWorkspace == null)
            {
                throw new ArgumentNullException(GetName(new { osmWorkspace }));
            }

            IFeatureClass osmPointFeatureClass = null;
            IFeatureClass osmLineFeatureClass = null;
            IFeatureClass osmPolygonFeatureClass = null;

            try
            {
                osmPointFeatureClass = findFeatureClass(osmWorkspace, baseName, esriGeometryType.esriGeometryPoint);
                osmPointFeatureClass.DisableOSMChangeDetection();

                int internalExtensionVersion = osmPointFeatureClass.OSMExtensionVersion();

                IPropertySet pointFieldIndexes = new PropertySetClass();
                int osmPointTrackChangesFieldIndex = osmPointFeatureClass.FindField("osmTrackChanges");
                pointFieldIndexes.SetProperty("osmTrackChangesFieldIndex", osmPointTrackChangesFieldIndex);

                int osmUserPointFieldIndex = osmPointFeatureClass.FindField("osmuser");
                pointFieldIndexes.SetProperty("osmUserFieldIndex", osmUserPointFieldIndex);

                int osmUIDPointFieldIndex = osmPointFeatureClass.FindField("osmuid");
                pointFieldIndexes.SetProperty("osmUIDFieldIndex", osmUIDPointFieldIndex);

                int osmVisiblePointFieldIndex = osmPointFeatureClass.FindField("osmvisible");
                pointFieldIndexes.SetProperty("osmVisibleFieldIndex", osmVisiblePointFieldIndex);

                int osmVersionPointFieldIndex = osmPointFeatureClass.FindField("osmversion");
                pointFieldIndexes.SetProperty("osmVersionFieldIndex", osmVersionPointFieldIndex);

                int osmChangesetPointFieldIndex = osmPointFeatureClass.FindField("osmchangeset");
                pointFieldIndexes.SetProperty("osmChangeSetFieldIndex", osmChangesetPointFieldIndex);

                int osmTimeStampPointFieldIndex = osmPointFeatureClass.FindField("osmtimestamp");
                pointFieldIndexes.SetProperty("osmTimeStampFieldIndex", osmTimeStampPointFieldIndex);

                int osmMemberOfPointFieldIndex = osmPointFeatureClass.FindField("osmMemberOf");
                pointFieldIndexes.SetProperty("osmMemberOfFieldIndex", osmMemberOfPointFieldIndex);

                int osmSupportingElementPointFieldIndex = osmPointFeatureClass.FindField("osmSupportingElement");
                pointFieldIndexes.SetProperty("osmSupportingElementFieldIndex", osmSupportingElementPointFieldIndex);

                int osmWayRefCountFieldIndex = osmPointFeatureClass.FindField("wayRefCount");
                pointFieldIndexes.SetProperty("osmWayRefCountFieldIndex", osmWayRefCountFieldIndex);

                int tagCollectionPointFieldIndex = osmPointFeatureClass.FindField("osmTags");
                pointFieldIndexes.SetProperty("osmTagsFieldIndex", tagCollectionPointFieldIndex);

                int osmPointIDFieldIndex = osmPointFeatureClass.FindField("OSMID");
                pointFieldIndexes.SetProperty("osmIDFieldIndex", osmPointIDFieldIndex);

                Dictionary<string, int> osmPointDomainAttributeFieldIndices = new Dictionary<string, int>();
                foreach (var domains in availableDomains.domain)
                {
                    int currentFieldIndex = osmPointFeatureClass.FindField(domains.name);

                    if (currentFieldIndex != -1)
                    {
                        osmPointDomainAttributeFieldIndices.Add(domains.name, currentFieldIndex);
                    }
                }
                pointFieldIndexes.SetProperty("domainAttributesFieldIndex", osmPointDomainAttributeFieldIndices);


                osmLineFeatureClass = findFeatureClass(osmWorkspace, baseName, esriGeometryType.esriGeometryPolyline);
                osmLineFeatureClass.DisableOSMChangeDetection();

                IPropertySet polylineFieldIndexes = new PropertySetClass();

                int tagCollectionPolylineFieldIndex = osmLineFeatureClass.FindField("osmTags");
                polylineFieldIndexes.SetProperty("osmTagsFieldIndex", tagCollectionPolylineFieldIndex);

                int osmLineIDFieldIndex = osmLineFeatureClass.FindField("OSMID");
                polylineFieldIndexes.SetProperty("osmIDFieldIndex", osmLineIDFieldIndex);

                Dictionary<string, int> osmLineDomainAttributeFieldIndices = new Dictionary<string, int>();
                foreach (var domains in availableDomains.domain)
                {
                    int currentFieldIndex = osmLineFeatureClass.FindField(domains.name);

                    if (currentFieldIndex != -1)
                    {
                        osmLineDomainAttributeFieldIndices.Add(domains.name, currentFieldIndex);
                    }
                }
                polylineFieldIndexes.SetProperty("domainAttributesFieldIndex", osmLineDomainAttributeFieldIndices);

                int osmPolylineTrackChangesFieldIndex = osmLineFeatureClass.FindField("osmTrackChanges");
                polylineFieldIndexes.SetProperty("osmTrackChangesFieldIndex", osmPolylineTrackChangesFieldIndex);

                int osmUserPolylineFieldIndex = osmLineFeatureClass.FindField("osmuser");
                polylineFieldIndexes.SetProperty("osmUserFieldIndex", osmUserPolylineFieldIndex);

                int osmUIDPolylineFieldIndex = osmLineFeatureClass.FindField("osmuid");
                polylineFieldIndexes.SetProperty("osmUIDFieldIndex", osmUIDPolylineFieldIndex);

                int osmVisiblePolylineFieldIndex = osmLineFeatureClass.FindField("osmvisible");
                polylineFieldIndexes.SetProperty("osmVisibleFieldIndex", osmVisiblePolylineFieldIndex);

                int osmVersionPolylineFieldIndex = osmLineFeatureClass.FindField("osmversion");
                polylineFieldIndexes.SetProperty("osmVersionFieldIndex", osmVersionPolylineFieldIndex);

                int osmChangesetPolylineFieldIndex = osmLineFeatureClass.FindField("osmchangeset");
                polylineFieldIndexes.SetProperty("osmChangeSetFieldIndex", osmChangesetPolylineFieldIndex);

                int osmTimeStampPolylineFieldIndex = osmLineFeatureClass.FindField("osmtimestamp");
                polylineFieldIndexes.SetProperty("osmTimeStampFieldIndex", osmTimeStampPolylineFieldIndex);

                int osmMemberOfPolylineFieldIndex = osmLineFeatureClass.FindField("osmMemberOf");
                polylineFieldIndexes.SetProperty("osmMemberOfFieldIndex", osmMemberOfPolylineFieldIndex);

                int osmMembersPolylineFieldIndex = osmLineFeatureClass.FindField("osmMembers");
                polylineFieldIndexes.SetProperty("osmMembersFieldIndex", osmMembersPolylineFieldIndex);

                int osmSupportingElementPolylineFieldIndex = osmLineFeatureClass.FindField("osmSupportingElement");
                polylineFieldIndexes.SetProperty("osmSupportingElementFieldIndex", osmSupportingElementPolylineFieldIndex);


                osmPolygonFeatureClass = findFeatureClass(osmWorkspace, baseName, esriGeometryType.esriGeometryPolygon);
                osmPolygonFeatureClass.DisableOSMChangeDetection();

                IPropertySet polygonFieldIndexes = new PropertySetClass();

                int tagCollectionPolygonFieldIndex = osmPolygonFeatureClass.FindField("osmTags");
                polygonFieldIndexes.SetProperty("osmTagsFieldIndex", tagCollectionPolygonFieldIndex);

                int osmPolygonIDFieldIndex = osmPolygonFeatureClass.FindField("OSMID");
                polygonFieldIndexes.SetProperty("osmIDFieldIndex", osmPolygonIDFieldIndex);

                Dictionary<string, int> osmPolygonDomainAttributeFieldIndices = new Dictionary<string, int>();
                foreach (var domains in availableDomains.domain)
                {
                    int currentFieldIndex = osmPolygonFeatureClass.FindField(domains.name);

                    if (currentFieldIndex != -1)
                    {
                        osmPolygonDomainAttributeFieldIndices.Add(domains.name, currentFieldIndex);
                    }
                }
                polygonFieldIndexes.SetProperty("domainAttributesFieldIndex", osmPolygonDomainAttributeFieldIndices);

                int osmPolygonTrackChangesFieldIndex = osmPolygonFeatureClass.FindField("osmTrackChanges");
                polygonFieldIndexes.SetProperty("osmTrackChangesFieldIndex", osmPolygonTrackChangesFieldIndex);

                int osmUserPolygonFieldIndex = osmPolygonFeatureClass.FindField("osmuser");
                polygonFieldIndexes.SetProperty("osmUserFieldIndex", osmUserPolygonFieldIndex);

                int osmUIDPolygonFieldIndex = osmPolygonFeatureClass.FindField("osmuid");
                polygonFieldIndexes.SetProperty("osmUIDFieldIndex", osmUIDPolygonFieldIndex);

                int osmVisiblePolygonFieldIndex = osmPolygonFeatureClass.FindField("osmvisible");
                polygonFieldIndexes.SetProperty("osmVisibleFieldIndex", osmVisiblePolygonFieldIndex);

                int osmVersionPolygonFieldIndex = osmPolygonFeatureClass.FindField("osmversion");
                polygonFieldIndexes.SetProperty("osmVersionFieldIndex", osmVersionPolygonFieldIndex);

                int osmChangesetPolygonFieldIndex = osmPolygonFeatureClass.FindField("osmchangeset");
                polygonFieldIndexes.SetProperty("osmChangeSetFieldIndex", osmChangesetPolygonFieldIndex);

                int osmTimeStampPolygonFieldIndex = osmPolygonFeatureClass.FindField("osmtimestamp");
                polygonFieldIndexes.SetProperty("osmTimeStampFieldIndex", osmTimeStampPolygonFieldIndex);

                int osmMemberOfPolygonFieldIndex = osmPolygonFeatureClass.FindField("osmMemberOf");
                polygonFieldIndexes.SetProperty("osmMemberOfFieldIndex", osmMemberOfPolygonFieldIndex);

                int osmMembersPolygonFieldIndex = osmPolygonFeatureClass.FindField("osmMembers");
                polygonFieldIndexes.SetProperty("osmMembersFieldIndex", osmMembersPolygonFieldIndex);

                int osmSupportingElementPolygonFieldIndex = osmPolygonFeatureClass.FindField("osmSupportingElement");
                polygonFieldIndexes.SetProperty("osmSupportingElementFieldIndex", osmSupportingElementPolygonFieldIndex);

                ITable relationTable = findTable(osmWorkspace, baseName, "relation");

                IPropertySet relationFieldIndexes = new PropertySetClass();

                int osmRelationIDFieldIndex = relationTable.FindField("OSMID");
                relationFieldIndexes.SetProperty("osmIDFieldIndex", osmRelationIDFieldIndex);

                int tagCollectionRelationFieldIndex = relationTable.FindField("osmTags");
                relationFieldIndexes.SetProperty("osmTagsFieldIndex", tagCollectionRelationFieldIndex);

                int osmUserRelationFieldIndex = relationTable.FindField("osmuser");
                relationFieldIndexes.SetProperty("osmUserFieldIndex", osmUserRelationFieldIndex);

                int osmUIDRelationFieldIndex = relationTable.FindField("osmuid");
                relationFieldIndexes.SetProperty("osmUIDFieldIndex", osmUIDRelationFieldIndex);

                int osmVisibleRelationFieldIndex = relationTable.FindField("osmvisible");
                relationFieldIndexes.SetProperty("osmVisibleFieldIndex", osmVisibleRelationFieldIndex);

                int osmVersionRelationFieldIndex = relationTable.FindField("osmversion");
                relationFieldIndexes.SetProperty("osmVersionFieldIndex", osmVersionRelationFieldIndex);

                int osmChangesetRelationFieldIndex = relationTable.FindField("osmchangeset");
                relationFieldIndexes.SetProperty("osmChangeSetFieldIndex", osmChangesetRelationFieldIndex);

                int osmTimeStampRelationFieldIndex = relationTable.FindField("osmtimestamp");
                relationFieldIndexes.SetProperty("osmTimeStampFieldIndex", osmTimeStampRelationFieldIndex);

                int osmMemberOfRelationFieldIndex = relationTable.FindField("osmMemberOf");
                relationFieldIndexes.SetProperty("osmMemberOfFieldIndex", osmMemberOfRelationFieldIndex);

                int osmMembersRelationFieldIndex = relationTable.FindField("osmMembers");
                relationFieldIndexes.SetProperty("osmMembersFieldIndex", osmMembersRelationFieldIndex);

                int osmSupportingElementRelationFieldIndex = relationTable.FindField("osmSupportingElement");
                relationFieldIndexes.SetProperty("osmSupportingElementFieldIndex", osmSupportingElementRelationFieldIndex);

                int osmRelationTrackChangesFieldIndex = relationTable.FindField("osmTrackChanges");
                relationFieldIndexes.SetProperty("osmTrackChangesFieldIndex", osmRelationTrackChangesFieldIndex);

                // if the updates are restricted to be only recorded inside the AOI, determine the AOI geometry
                IGeometry updateFilterGeometry = null;

                if (restrictUpdatesToAOI == true)
                {
                    if (osmPointFeatureClass != null)
                    {
                        updateFilterGeometry = ((IGeoDataset)osmPointFeatureClass).Extent;
                    }
                }


                ITable revisionTable = findTable(osmWorkspace, baseName, "revision");

                int domainFieldLengthPoints = -1;
                int domainFieldLengthLines = -1;
                int domainFieldLengthPolygons = -1;

                foreach (domain currentDomain in availableDomains.domain)
                {
                    if (domainFieldLengthPoints == -1)
                    {
                        int domainFieldIndexPoints = osmPointFeatureClass.FindField(currentDomain.name);
                        if (domainFieldIndexPoints > -1)
                        {
                            domainFieldLengthPoints = osmPointFeatureClass.Fields.get_Field(domainFieldIndexPoints).Length;
                        }
                    }

                    if (domainFieldLengthLines == -1)
                    {
                        int domainFieldIndexLines = osmLineFeatureClass.FindField(currentDomain.name);
                        if (domainFieldIndexLines > -1)
                        {
                            domainFieldLengthLines = osmLineFeatureClass.Fields.get_Field(domainFieldIndexLines).Length;
                        }
                    }

                    if (domainFieldLengthPolygons == -1)
                    {
                        int domainFieldIndexPolygons = osmPolygonFeatureClass.FindField(currentDomain.name);
                        if (domainFieldIndexPolygons > -1)
                        {
                            domainFieldLengthPolygons = osmPolygonFeatureClass.Fields.get_Field(domainFieldIndexPolygons).Length;
                        }
                    }
                }

                // scan the revision table to capture local edits - don't redo the edits originating from this client
                Dictionary<string, int> localnodeRevisions = gatherLocalOSMEdits(revisionTable, "node");
                Dictionary<string, int> localwayRevisions = gatherLocalOSMEdits(revisionTable, "way");
                Dictionary<string, int> localrelationRevisions = gatherLocalOSMEdits(revisionTable, "relation");


                // create dictionaries for nodes, way, relations
                Dictionary<string, node> nodesToCreateOrModify = new Dictionary<string, node>();
                Dictionary<string, node> nodesToDelete = new Dictionary<string, node>();

                Dictionary<string, way> waysToCreateOrModify = new Dictionary<string, way>();
                Dictionary<string, way> waysToDelete = new Dictionary<string, way>();

                Dictionary<string, relation> relationsToCreateOrModify = new Dictionary<string, relation>();
                Dictionary<string, relation> relationsToDelete = new Dictionary<string, relation>();

                foreach (var xmlNode in osmChangeDocument.Items)
                {
                    if (xmlNode is create)
                    {
                        // determine what it is - then create it
                        create createXMLNode = xmlNode as create;

                        foreach (var createItem in createXMLNode.Items)
                        {
                            if (createItem is node)
                            {
                                node currentNode = createItem as node;
                                nodesToCreateOrModify.Add(currentNode.id, currentNode);
                            }
                            else if (createItem is way)
                            {
                                way currentWay = createItem as way;
                                waysToCreateOrModify.Add(currentWay.id, currentWay);
                            }
                            else if (createItem is relation)
                            {
                                relation currentRelation = createItem as relation;
                                relationsToCreateOrModify.Add(currentRelation.id, currentRelation);
                            }

                        }
                    }
                    else if (xmlNode is modify)
                    {
                        // determine what it is - then either update an existing item or add it to the dictionary
                        modify modifyXMLNode = xmlNode as modify;

                        foreach (var modifyItem in modifyXMLNode.Items)
                        {
                            if (modifyItem is node)
                            {
                                node currentNode = modifyItem as node;
                                if (nodesToCreateOrModify.ContainsKey(currentNode.id))
                                    nodesToCreateOrModify[currentNode.id] = currentNode;
                                else
                                    nodesToCreateOrModify.Add(currentNode.id, currentNode);
                            }
                            else if (modifyItem is way)
                            {
                                way currentWay = modifyItem as way;
                                if (waysToCreateOrModify.ContainsKey(currentWay.id))
                                    waysToCreateOrModify[currentWay.id] = currentWay;
                                else
                                    waysToCreateOrModify.Add(currentWay.id, currentWay);
                            }
                            else if (modifyItem is relation)
                            {
                                relation currentRelation = modifyItem as relation;
                                if (relationsToCreateOrModify.ContainsKey(currentRelation.id))
                                    relationsToCreateOrModify[currentRelation.id] = currentRelation;
                                else
                                    relationsToCreateOrModify.Add(currentRelation.id, currentRelation);
                            }
                        }
                    }
                    else if (xmlNode is delete)
                    {
                        delete deleteXMLNode = xmlNode as delete;

                        foreach (var deleteItem in deleteXMLNode.Items)
                        {
                            if (deleteItem is node)
                            {
                                node currentNode = deleteItem as node;
                                nodesToDelete.Add(currentNode.id, currentNode);
                            }
                            else if (deleteItem is way)
                            {
                                way currentWay = deleteItem as way;
                                waysToDelete.Add(currentWay.id, currentWay);
                            }
                            else if (deleteItem is relation)
                            {
                                relation currentRelation = deleteItem as relation;
                                relationsToDelete.Add(currentRelation.id, currentRelation);
                            }
                        }
                    }
                }

                using (ComReleaser comReleaser = new ComReleaser())
                {

                    if (trackCancel.Continue() == false)
                    {
                        return;
                    }

                    // let's deal with the nodes first

                    // since it is a new node we need to explicitly create it
                    manipulateNodes(nodesToCreateOrModify, "create_modify", updateFilterGeometry, osmPointFeatureClass, availableDomains, domainFieldLengthPoints, pointFieldIndexes, ref message, internalExtensionVersion, comReleaser, useVerboseLogging, ref trackCancel);

                    if (trackCancel.Continue() == false)
                    {
                        return;
                    }

                    if (useVerboseLogging == true)
                    {
                        message.AddMessage(String.Format(resourceManager.GetString("GPTools_OSMGPDiffLoader_createmodify_message"), nodesToCreateOrModify.Count, resourceManager.GetString("GPTools_OSM_nodes")));
                    }

                    manipulateNodes(nodesToDelete, "delete", updateFilterGeometry, osmPointFeatureClass, availableDomains, domainFieldLengthPoints, pointFieldIndexes, ref message, internalExtensionVersion, comReleaser, useVerboseLogging, ref trackCancel);

                    if (trackCancel.Continue() == false)
                    {
                        return;
                    }

                    if (useVerboseLogging == true)
                    {
                        message.AddMessage(String.Format(resourceManager.GetString("GPTools_OSMGPDiffLoader_delete_message"), nodesToDelete.Count, resourceManager.GetString("GPTools_OSM_nodes")));
                    }


                    manipulateWays(waysToCreateOrModify, "create_modify", updateFilterGeometry, osmPointFeatureClass, pointFieldIndexes, availableDomains, domainFieldLengthLines, osmLineFeatureClass, polylineFieldIndexes, domainFieldLengthPolygons, osmPolygonFeatureClass, polygonFieldIndexes, ref message, internalExtensionVersion, comReleaser, useVerboseLogging, ref trackCancel);

                    if (trackCancel.Continue() == false)
                    {
                        return;
                    }

                    if (useVerboseLogging == true)
                    {
                        message.AddMessage(String.Format(resourceManager.GetString("GPTools_OSMGPDiffLoader_createmodify_message"), waysToCreateOrModify.Count, resourceManager.GetString("GPTools_OSM_ways")));
                    }

                    manipulateWays(waysToDelete, "delete", updateFilterGeometry, osmPointFeatureClass, pointFieldIndexes, availableDomains, domainFieldLengthLines, osmLineFeatureClass, polylineFieldIndexes, domainFieldLengthPolygons, osmPolygonFeatureClass, polygonFieldIndexes, ref message, internalExtensionVersion, comReleaser, useVerboseLogging, ref trackCancel);

                    if (trackCancel.Continue() == false)
                    {
                        return;
                    }

                    if (useVerboseLogging == true)
                    {
                        message.AddMessage(String.Format(resourceManager.GetString("GPTools_OSMGPDiffLoader_delete_message"), waysToDelete.Count, resourceManager.GetString("GPTools_OSM_ways")));
                    }

                    manipulateRelations(relationsToCreateOrModify, "create_modify", updateFilterGeometry, relationTable, relationFieldIndexes, availableDomains, domainFieldLengthLines, osmLineFeatureClass, polylineFieldIndexes, domainFieldLengthPolygons, osmPolygonFeatureClass, polygonFieldIndexes, ref message, internalExtensionVersion, comReleaser, useVerboseLogging, ref trackCancel);

                    if (trackCancel.Continue() == false)
                    {
                        return;
                    }

                    if (useVerboseLogging == true)
                    {
                        message.AddMessage(String.Format(resourceManager.GetString("GPTools_OSMGPDiffLoader_createmodify_message"), relationsToCreateOrModify.Count, resourceManager.GetString("GPTools_OSM_relations")));
                    }

                    manipulateRelations(relationsToDelete, "delete", updateFilterGeometry, relationTable, relationFieldIndexes, availableDomains, domainFieldLengthLines, osmLineFeatureClass, polylineFieldIndexes, domainFieldLengthPolygons, osmPolygonFeatureClass, polygonFieldIndexes, ref message, internalExtensionVersion, comReleaser, useVerboseLogging, ref trackCancel);

                    if (useVerboseLogging == true)
                    {
                        message.AddMessage(String.Format(resourceManager.GetString("GPTools_OSMGPDiffLoader_delete_message"), relationsToDelete.Count, resourceManager.GetString("GPTools_relations")));
                    }

                }

            }
            catch { }
            finally
            {
                try
                {
                    osmPointFeatureClass.EnableOSMChangeDetection();
                    osmLineFeatureClass.EnableOSMChangeDetection();
                    osmPolygonFeatureClass.EnableOSMChangeDetection();
                }
                catch (Exception ex)
                {
                    message.AddWarning(ex.ToString());
                }
            }
        }


        private string GetName<T>(T item) where T : class
        {
            string returnName = String.Empty;

            var properties = typeof(T).GetProperties();

            if (properties.Length == 1)
            {
                returnName = properties[0].Name;
            }

            return returnName;
        }

        private void insertRelation(relation currentRelation, string action, IGeometry filterGeometry, esriGeometryType detectedGeometryType, IGeometry detectedGeometry, IFeatureClass osmPointFeatureClass, IPropertySet pointFieldIndexes, IFeatureClass osmLineFeatureClass, IPropertySet polylineFieldIndexes, IFeatureClass osmPolygonFeatureClass, IPropertySet polygonFieldIndexes, ITable relationTable, IPropertySet relationIndexes, ref IGPMessages message, int extensionVersion, ComReleaser comReleaser, bool verboseLogging)
        {
            IFeatureCursor updateCursor = null;
            ICursor updateRow = null;

            try
            {
                OSMToolHelper osmToolHelper = new OSMToolHelper();
                Dictionary<string, ESRI.ArcGIS.OSM.GeoProcessing.OSMToolHelper.osmItemReference> pointReferences = new Dictionary<string, ESRI.ArcGIS.OSM.GeoProcessing.OSMToolHelper.osmItemReference>();
                Dictionary<string, ESRI.ArcGIS.OSM.GeoProcessing.OSMToolHelper.osmItemReference> lineReferences = new Dictionary<string, ESRI.ArcGIS.OSM.GeoProcessing.OSMToolHelper.osmItemReference>();
                Dictionary<string, ESRI.ArcGIS.OSM.GeoProcessing.OSMToolHelper.osmItemReference> relationReferences = new Dictionary<string, ESRI.ArcGIS.OSM.GeoProcessing.OSMToolHelper.osmItemReference>();

                int osmSupportingElementPolygonFieldIndex = (int)polygonFieldIndexes.GetProperty("osmSupportingElementFieldIndex");
                int osmMemberOfPolygonFieldIndex = (int)polygonFieldIndexes.GetProperty("osmMemberOfFieldIndex");
                int osmPolygonTrackChangesFieldIndex = (int)polygonFieldIndexes.GetProperty("osmTrackChangesFieldIndex");
                int osmMembersPolygonFieldIndex = (int)polygonFieldIndexes.GetProperty("osmMembersFieldIndex");
                int tagCollectionPolygonFieldIndex = (int)polygonFieldIndexes.GetProperty("osmTagsFieldIndex");
                int osmUserPolygonFieldIndex = (int)polygonFieldIndexes.GetProperty("osmUserFieldIndex");
                int osmUIDPolygonFieldIndex = (int)polygonFieldIndexes.GetProperty("osmUIDFieldIndex");
                int osmVisiblePolygonFieldIndex = (int)polygonFieldIndexes.GetProperty("osmVisibleFieldIndex");
                int osmVersionPolygonFieldIndex = (int)polygonFieldIndexes.GetProperty("osmVersionFieldIndex");
                int osmChangesetPolygonFieldIndex = (int)polygonFieldIndexes.GetProperty("osmChangeSetFieldIndex");
                int osmTimeStampPolygonFieldIndex = (int)polygonFieldIndexes.GetProperty("osmTimeStampFieldIndex");
                int osmPolygonIDFieldIndex = (int)polygonFieldIndexes.GetProperty("osmIDFieldIndex");

                int osmSupportingElementPolylineFieldIndex = (int)polylineFieldIndexes.GetProperty("osmSupportingElementFieldIndex");
                int osmMemberOfPolylineFieldIndex = (int)polylineFieldIndexes.GetProperty("osmMemberOfFieldIndex");
                int tagCollectionPolylineFieldIndex = (int)polylineFieldIndexes.GetProperty("osmTagsFieldIndex");
                int osmMembersPolylineFieldIndex = (int)polylineFieldIndexes.GetProperty("osmMembersFieldIndex");
                int osmUserPolylineFieldIndex = (int)polylineFieldIndexes.GetProperty("osmUserFieldIndex");
                int osmUIDPolylineFieldIndex = (int)polylineFieldIndexes.GetProperty("osmUIDFieldIndex");
                int osmVisiblePolylineFieldIndex = (int)polylineFieldIndexes.GetProperty("osmVisibleFieldIndex");
                int osmVersionPolylineFieldIndex = (int)polylineFieldIndexes.GetProperty("osmVersionFieldIndex");
                int osmChangesetPolylineFieldIndex = (int)polylineFieldIndexes.GetProperty("osmChangeSetFieldIndex");
                int osmTimeStampPolylineFieldIndex = (int)polylineFieldIndexes.GetProperty("osmTimeStampFieldIndex");
                int osmLineIDFieldIndex = (int)polylineFieldIndexes.GetProperty("osmIDFieldIndex");
                int osmPolylineTrackChangesFieldIndex = (int)polylineFieldIndexes.GetProperty("osmTrackChangesFieldIndex");

                int tagCollectionRelationFieldIndex = (int)relationIndexes.GetProperty("osmTagsFieldIndex");
                int osmMembersRelationFieldIndex = (int)relationIndexes.GetProperty("osmMembersFieldIndex");
                int osmUserRelationFieldIndex = (int)relationIndexes.GetProperty("osmUserFieldIndex");
                int osmUIDRelationFieldIndex = (int)relationIndexes.GetProperty("osmUIDFieldIndex");
                int osmVisibleRelationFieldIndex = (int)relationIndexes.GetProperty("osmVisibleFieldIndex");
                int osmVersionRelationFieldIndex = (int)relationIndexes.GetProperty("osmVersionFieldIndex");
                int osmChangesetRelationFieldIndex = (int)relationIndexes.GetProperty("osmChangeSetFieldIndex");
                int osmTimeStampRelationFieldIndex = (int)relationIndexes.GetProperty("osmTimeStampFieldIndex");
                int osmRelationIDFieldIndex = (int)relationIndexes.GetProperty("osmIDFieldIndex");
                int osmRelationTrackChangesFieldIndex = (int)relationIndexes.GetProperty("osmTrackChangesFieldIndex");

                List<tag> relationTagList = new List<tag>();
                List<member> relationMemberList = new List<member>();
                List<string> wayList = new List<string>();

                // relations should always have items but just as a sanity check
                if (currentRelation.Items == null)
                {
                    return;
                }

                foreach (var item in currentRelation.Items)
                {
                    if (item is member)
                    {
                        member memberItem = item as member;
                        relationMemberList.Add(memberItem);

                        switch (memberItem.type)
                        {
                            case memberType.way:

                                wayList.Add(memberItem.@ref);

                                if (lineReferences.ContainsKey(memberItem.@ref))
                                {
                                    ESRI.ArcGIS.OSM.GeoProcessing.OSMToolHelper.osmItemReference lineItemReference = lineReferences[memberItem.@ref];
                                    lineItemReference.refCount = lineItemReference.refCount + 1;
                                    if (detectedGeometryType == esriGeometryType.esriGeometryPolygon)
                                    {
                                        lineItemReference.relations.Add(currentRelation.id + "_ply");
                                    }
                                    else if (detectedGeometryType == esriGeometryType.esriGeometryPolyline)
                                    {
                                        lineItemReference.relations.Add(currentRelation.id + "_ln");
                                    }
                                    else
                                    {
                                        lineItemReference.relations.Add(currentRelation.id + "_rel");
                                    }

                                    lineReferences[memberItem.@ref] = lineItemReference;
                                }
                                else
                                {
                                    ESRI.ArcGIS.OSM.GeoProcessing.OSMToolHelper.osmItemReference newlineItemReference = new ESRI.ArcGIS.OSM.GeoProcessing.OSMToolHelper.osmItemReference();
                                    newlineItemReference.refCount = 1;
                                    newlineItemReference.relations = new List<string>();
                                    if (detectedGeometryType == esriGeometryType.esriGeometryPolygon)
                                    {
                                        newlineItemReference.relations.Add(currentRelation.id + "_ply");
                                    }
                                    else if (detectedGeometryType == esriGeometryType.esriGeometryPolyline)
                                    {
                                        newlineItemReference.relations.Add(currentRelation.id + "_ln");
                                    }
                                    else
                                    {
                                        newlineItemReference.relations.Add(currentRelation.id + "_rel");
                                    }

                                    lineReferences.Add(memberItem.@ref, newlineItemReference);
                                }

                                break;
                            case memberType.node:

                                if (pointReferences.ContainsKey(memberItem.@ref) == true)
                                {
                                    ESRI.ArcGIS.OSM.GeoProcessing.OSMToolHelper.osmItemReference currentItemReference = pointReferences[memberItem.@ref];
                                    //currentItemReference.refCount = currentItemReference.refCount + 1;
                                    currentItemReference.relations.Add(currentRelation.id + "_rel");

                                    pointReferences[memberItem.@ref] = currentItemReference;
                                }
                                else
                                {
                                    ESRI.ArcGIS.OSM.GeoProcessing.OSMToolHelper.osmItemReference newItemReference = new ESRI.ArcGIS.OSM.GeoProcessing.OSMToolHelper.osmItemReference();
                                    newItemReference.refCount = 1;
                                    newItemReference.relations = new List<string>();
                                    newItemReference.relations.Add(currentRelation.id + "_rel");

                                    pointReferences.Add(memberItem.@ref, newItemReference);
                                }

                                break;
                            case memberType.relation:

                                if (relationReferences.ContainsKey(memberItem.@ref) == true)
                                {
                                    ESRI.ArcGIS.OSM.GeoProcessing.OSMToolHelper.osmItemReference relationItemReference = relationReferences[memberItem.@ref];
                                    relationItemReference.refCount = relationItemReference.refCount + 1;
                                    relationItemReference.relations.Add(currentRelation.id + "_rel");

                                    relationReferences[memberItem.@ref] = relationItemReference;
                                }
                                else
                                {
                                    ESRI.ArcGIS.OSM.GeoProcessing.OSMToolHelper.osmItemReference newRelationItemReference = new ESRI.ArcGIS.OSM.GeoProcessing.OSMToolHelper.osmItemReference();
                                    newRelationItemReference.refCount = 1;
                                    newRelationItemReference.relations = new List<string>();
                                    newRelationItemReference.relations.Add(currentRelation.id + "_rel");

                                    relationReferences.Add(memberItem.@ref, newRelationItemReference);
                                }
                                break;
                            default:
                                break;
                        }

                    }
                    else if (item is tag)
                    {
                        relationTagList.Add((tag)item);
                    }
                }


                // if there is a defined geometry type use it to generate a multipart geometry
                if (detectedGeometryType == esriGeometryType.esriGeometryPolygon)
                {
                    #region create multipart polygon geometry

                    ISpatialFilter osmIDQueryFilter = new SpatialFilterClass();
                    string sqlPolyOSMID = osmPolygonFeatureClass.SqlIdentifier("OSMID");
                    object missing = Type.Missing;

                    // use this as an indicator if a part of a relation was already modified 
                    // if that is the case then we cannot abort 
                    bool alreadyClearedFlag = false;

                    IFeature mpFeature = null;

                    if (action == "create")
                    {
                        //mpFeature = osmPolygonFeatureClass.CreateFeature();
                        updateCursor = osmPolygonFeatureClass.Insert(true);
                        comReleaser.ManageLifetime(updateCursor);
                        mpFeature = osmPolygonFeatureClass.CreateFeatureBuffer() as IFeature;
                    }
                    else if (action == "modify")
                    {
                        //mpFeature = getOSMRow((ITable)osmPolygonFeatureClass, currentRelation.id) as IFeature;
                        IQueryFilter queryFilter = new QueryFilterClass();
                        queryFilter.WhereClause = osmPolygonFeatureClass.WhereClauseByExtensionVersion(currentRelation.id, "OSMID", extensionVersion);
                        queryFilter.SubFields = String.Join(",", osmPolygonFeatureClass.ExtractRequiredFields());
                        comReleaser.ManageLifetime(queryFilter);
                        updateCursor = osmPolygonFeatureClass.Update(queryFilter, false);
                        comReleaser.ManageLifetime(updateCursor);
                        mpFeature = updateCursor.NextFeature();
                    }

                    // don't continue unless we have a feature instance we can populate
                    if (mpFeature == null)
                    {
                        message.AddWarning(String.Format(resourceManager.GetString("GPTools_OSMGPDiffLoader_relationskipped_nopolygon"), currentRelation.id));
                        return;
                    }

                    mpFeature.Shape = detectedGeometry;

                    if (osmMembersPolygonFieldIndex > -1)
                    {
                        _osmUtility.insertMembers(osmMembersPolygonFieldIndex, mpFeature, relationMemberList.ToArray());
                    }

                    if (tagCollectionPolygonFieldIndex > -1)
                    {
                        if (_osmUtility.DoesHaveKeys(currentRelation))
                        {
                            _osmUtility.insertOSMTags(tagCollectionPolygonFieldIndex, mpFeature, relationTagList.ToArray(), ((IDataset)osmPolygonFeatureClass).Workspace);
                        }
                        else
                        {
                            relationTagList = osmToolHelper.MergeTagsFromOuterPolygonToRelation(currentRelation, osmPolygonFeatureClass);
                        }
                    }

                    // store the administrative attributes
                    // user, uid, version, changeset, timestamp, visible
                    if (osmUserPolygonFieldIndex != -1)
                    {
                        if (!String.IsNullOrEmpty(currentRelation.user))
                        {
                            mpFeature.set_Value(osmUserPolygonFieldIndex, currentRelation.user);
                        }
                    }

                    if (osmUIDPolygonFieldIndex != -1)
                    {
                        if (!String.IsNullOrEmpty(currentRelation.uid))
                        {
                            mpFeature.set_Value(osmUIDPolygonFieldIndex, Convert.ToInt64(currentRelation.uid));
                        }
                    }

                    if (osmVisiblePolygonFieldIndex != -1)
                    {
                        if (String.IsNullOrEmpty(currentRelation.visible) == false)
                        {
                            mpFeature.set_Value(osmVisiblePolygonFieldIndex, currentRelation.visible.ToString());
                        }
                        else
                        {
                            mpFeature.set_Value(osmVisiblePolygonFieldIndex, "unknown");
                        }
                    }

                    if (osmVersionPolygonFieldIndex != -1)
                    {
                        if (!String.IsNullOrEmpty(currentRelation.version))
                        {
                            mpFeature.set_Value(osmVersionPolygonFieldIndex, Convert.ToInt32(currentRelation.version));
                        }
                    }

                    if (osmChangesetPolygonFieldIndex != -1)
                    {
                        if (!String.IsNullOrEmpty(currentRelation.changeset))
                        {
                            mpFeature.set_Value(osmChangesetPolygonFieldIndex, Convert.ToInt64(currentRelation.changeset));
                        }
                    }

                    if (osmTimeStampPolygonFieldIndex != -1)
                    {
                        if (!String.IsNullOrEmpty(currentRelation.timestamp))
                        {
                            mpFeature.set_Value(osmTimeStampPolygonFieldIndex, Convert.ToDateTime(currentRelation.timestamp));
                        }
                    }

                    if (osmPolygonIDFieldIndex != -1)
                    {
                        if (extensionVersion == 1)
                            mpFeature.set_Value(osmPolygonIDFieldIndex, Convert.ToInt32(currentRelation.id));
                        else if (extensionVersion == 2)
                            mpFeature.set_Value(osmPolygonIDFieldIndex, currentRelation.id);
                    }

                    if (osmSupportingElementPolygonFieldIndex > -1)
                    {
                        mpFeature.set_Value(osmSupportingElementPolygonFieldIndex, "no");
                    }

                    if (osmPolygonTrackChangesFieldIndex > -1)
                    {
                        mpFeature.set_Value(osmPolygonTrackChangesFieldIndex, 1);
                    }


                    switch (action)
                    {
                        case "create":
                            //mpFeature.Store();
                            updateCursor.InsertFeature((IFeatureBuffer)mpFeature);
                            break;
                        case "modify":
                            updateCursor.UpdateFeature(mpFeature);
                            break;
                        default:
                            break;
                    }

                    #endregion
                }
                else if (detectedGeometryType == esriGeometryType.esriGeometryPolyline)
                {
                    #region create multipart polyline geometry
                    IFeature mpFeature = null;

                    if (action == "create")
                    {
                        //mpFeature = osmLineFeatureClass.CreateFeature();
                        updateCursor = osmLineFeatureClass.Insert(true);
                        comReleaser.ManageLifetime(updateCursor);
                        mpFeature = osmLineFeatureClass.CreateFeatureBuffer() as IFeature;
                    }
                    else if (action == "modify")
                    {
                        //mpFeature = getOSMRow((ITable)osmLineFeatureClass, currentRelation.id) as IFeature;
                        IQueryFilter queryFilter = new QueryFilterClass();
                        queryFilter.WhereClause = osmLineFeatureClass.WhereClauseByExtensionVersion(currentRelation.id, "OSMID", extensionVersion);
                        queryFilter.SubFields = String.Join(",", osmLineFeatureClass.ExtractRequiredFields());
                        comReleaser.ManageLifetime(queryFilter);
                        updateCursor = osmLineFeatureClass.Update(queryFilter, false);
                        comReleaser.ManageLifetime(updateCursor);
                        mpFeature = updateCursor.NextFeature();
                    }

                    // don't continue unless we have a feature instance we can populate
                    if (mpFeature == null)
                    {
                        message.AddWarning(String.Format(resourceManager.GetString("GPTools_OSMGPDiffLoader_relationskipped_noline"), currentRelation.id));
                        return;
                    }

                    mpFeature.Shape = detectedGeometry;

                    if (tagCollectionPolylineFieldIndex > -1)
                    {
                        _osmUtility.insertOSMTags(tagCollectionPolylineFieldIndex, mpFeature, relationTagList.ToArray(), ((IDataset)osmLineFeatureClass).Workspace);
                    }

                    if (osmMembersPolylineFieldIndex > -1)
                    {
                        _osmUtility.insertMembers(osmMembersPolylineFieldIndex, mpFeature, relationMemberList.ToArray());
                    }

                    // store the administrative attributes
                    // user, uid, version, changeset, timestamp, visible
                    if (osmUserPolylineFieldIndex != -1)
                    {
                        if (!String.IsNullOrEmpty(currentRelation.user))
                        {
                            mpFeature.set_Value(osmUserPolylineFieldIndex, currentRelation.user);
                        }
                    }

                    if (osmUIDPolylineFieldIndex != -1)
                    {
                        if (!String.IsNullOrEmpty(currentRelation.uid))
                        {
                            mpFeature.set_Value(osmUIDPolylineFieldIndex, Convert.ToInt64(currentRelation.uid));
                        }
                    }

                    if (osmVisiblePolylineFieldIndex != -1)
                    {
                        if (String.IsNullOrEmpty(currentRelation.visible) == false)
                        {
                            mpFeature.set_Value(osmVisiblePolylineFieldIndex, currentRelation.visible.ToString());
                        }
                        else
                        {
                            mpFeature.set_Value(osmVisiblePolylineFieldIndex, "unknown");
                        }
                    }

                    if (osmVersionPolylineFieldIndex != -1)
                    {
                        if (!String.IsNullOrEmpty(currentRelation.version))
                        {
                            mpFeature.set_Value(osmVersionPolylineFieldIndex, Convert.ToInt32(currentRelation.version));
                        }
                    }

                    if (osmChangesetPolylineFieldIndex != -1)
                    {
                        if (!String.IsNullOrEmpty(currentRelation.changeset))
                        {
                            mpFeature.set_Value(osmChangesetPolylineFieldIndex, Convert.ToInt64(currentRelation.changeset));
                        }
                    }

                    if (osmTimeStampPolylineFieldIndex != -1)
                    {
                        if (!String.IsNullOrEmpty(currentRelation.timestamp))
                        {
                            mpFeature.set_Value(osmTimeStampPolylineFieldIndex, Convert.ToDateTime(currentRelation.timestamp));
                        }
                    }

                    if (osmLineIDFieldIndex != -1)
                    {
                        if (extensionVersion == 1)
                            mpFeature.set_Value(osmLineIDFieldIndex, Convert.ToInt32(currentRelation.id));
                        else if (extensionVersion == 2)
                            mpFeature.set_Value(osmLineIDFieldIndex, currentRelation.id);
                    }

                    if (osmSupportingElementPolylineFieldIndex > -1)
                    {
                        mpFeature.set_Value(osmSupportingElementPolylineFieldIndex, "no");
                    }

                    if (osmPolylineTrackChangesFieldIndex > -1)
                    {
                        mpFeature.set_Value(osmPolylineTrackChangesFieldIndex, 1);
                    }

                    switch (action)
                    {
                        case "create":
                            //mpFeature.Store();
                            updateCursor.InsertFeature((IFeatureBuffer)mpFeature);
                            break;
                        case "modify":
                            updateCursor.UpdateFeature(mpFeature);
                            break;
                        default:
                            break;
                    }


                    #endregion

                }
                else if (detectedGeometryType == esriGeometryType.esriGeometryPoint)
                {
#if DEBUG
                    System.Diagnostics.Debug.WriteLine("Relation #: ____: POINT!!!");
#endif

                }
                else
                // otherwise it is relation that needs to be dealt with separately
                {
                    IRow relationRow = null;
                    if (action == "create")
                    {
                        //relationRow = relationTable.CreateRow();
                        updateRow = relationTable.Insert(true);
                        comReleaser.ManageLifetime(updateRow);
                        relationRow = relationTable.CreateRowBuffer() as IRow;
                    }
                    else if (action == "modify")
                    {
                        //relationRow = getOSMRow(relationTable, currentRelation.id);
                        IQueryFilter queryFilter = new QueryFilterClass();
                        queryFilter.WhereClause = relationTable.WhereClauseByExtensionVersion(currentRelation.id, "OSMID", extensionVersion);
                        comReleaser.ManageLifetime(queryFilter);
                        updateRow = relationTable.Update(queryFilter, false);
                        comReleaser.ManageLifetime(updateRow);
                        relationRow = updateRow.NextRow();
                    }

                    if (relationRow == null)
                    {
                        if (verboseLogging)
                        {
                            message.AddWarning(String.Format(resourceManager.GetString("GPTools_OSMGPDiffLoader_relationskipped_norelation"), currentRelation.id));
                        }
                        return;
                    }

#if DEBUG
                    System.Diagnostics.Debug.WriteLine("Relation #: " + currentRelation.id + " :____: Kept as relation");
#endif

                    if (tagCollectionRelationFieldIndex != -1)
                    {
                        _osmUtility.insertOSMTags(tagCollectionRelationFieldIndex, relationRow, relationTagList.ToArray());
                    }

                    if (osmMembersRelationFieldIndex != -1)
                    {
                        _osmUtility.insertMembers(osmMembersRelationFieldIndex, relationRow, relationMemberList.ToArray());
                    }

                    // store the administrative attributes
                    // user, uid, version, changeset, timestamp, visible
                    if (osmUserRelationFieldIndex != -1)
                    {
                        if (!String.IsNullOrEmpty(currentRelation.user))
                        {
                            relationRow.set_Value(osmUserRelationFieldIndex, currentRelation.user);
                        }
                    }

                    if (osmUIDRelationFieldIndex != -1)
                    {
                        if (!String.IsNullOrEmpty(currentRelation.uid))
                        {
                            relationRow.set_Value(osmUIDRelationFieldIndex, Convert.ToInt64(currentRelation.uid));
                        }
                    }

                    if (osmVisibleRelationFieldIndex != -1)
                    {
                        if (currentRelation.visible != null)
                        {
                            relationRow.set_Value(osmVisibleRelationFieldIndex, currentRelation.visible.ToString());
                        }
                    }

                    if (osmVersionRelationFieldIndex != -1)
                    {
                        if (!String.IsNullOrEmpty(currentRelation.version))
                        {
                            relationRow.set_Value(osmVersionRelationFieldIndex, Convert.ToInt32(currentRelation.version));
                        }
                    }

                    if (osmChangesetRelationFieldIndex != -1)
                    {
                        if (!String.IsNullOrEmpty(currentRelation.changeset))
                        {
                            relationRow.set_Value(osmChangesetRelationFieldIndex, Convert.ToInt64(currentRelation.changeset));
                        }
                    }

                    if (osmTimeStampRelationFieldIndex != -1)
                    {
                        if (!String.IsNullOrEmpty(currentRelation.timestamp))
                        {
                            relationRow.set_Value(osmTimeStampRelationFieldIndex, Convert.ToDateTime(currentRelation.timestamp));
                        }
                    }

                    if (osmRelationIDFieldIndex != -1)
                    {
                        if (extensionVersion == 1)
                            relationRow.set_Value(osmRelationIDFieldIndex, Convert.ToInt32(currentRelation.id));
                        else if (extensionVersion == 2)
                            relationRow.set_Value(osmRelationIDFieldIndex, currentRelation.id);
                    }

                    if (osmRelationTrackChangesFieldIndex > -1)
                    {
                        relationRow.set_Value(osmRelationTrackChangesFieldIndex, 1);
                    }


                    switch (action)
                    {
                        case "create":
                            //relationRow.Store();
                            updateRow.InsertRow((IRowBuffer)relationRow);
                            break;
                        case "modify":
                            updateRow.UpdateRow(relationRow);
                            break;
                        default:
                            break;
                    }

                }
            }
            catch (Exception ex)
            {
                message.AddWarning(ex.Message);
                message.AddWarning(ex.StackTrace);
            }
            finally
            {
                if (updateCursor != null)
                    Marshal.ReleaseComObject(updateCursor);

                if (updateRow != null)
                    Marshal.ReleaseComObject(updateRow);
            }
        }

        private IRow getOSMRow(ITable searchTable,string osmID)
        {
            IRow foundRow = null;

            int extensionVersion = searchTable.CurrentExtensionVersion();

 	        using (ComReleaser comReleaser = new ComReleaser())
            {
                IQueryFilter queryFilter = new QueryFilterClass();
                queryFilter.WhereClause = searchTable.WhereClauseByExtensionVersion(osmID, "OSMID", extensionVersion);
                comReleaser.ManageLifetime(queryFilter);

                ICursor searchCursor = searchTable.Search(queryFilter, false);
                comReleaser.ManageLifetime(searchCursor);

                foundRow = searchCursor.NextRow();
            }

            return foundRow;
        }

        private void manipulateRelationFeatures(string action, Dictionary<string, KeyValuePair<relation, IGeometry>> elementsInAOI, IFeatureClass insertFeatureClass, IPropertySet relationFieldIndexes, OSMDomains availableDomains, int domainFieldLength, int extensionVersion, ComReleaser comReleaser, ref ITrackCancel cancelTracker, ref IGPMessages message, bool useVerboseLogging)
        {
            try
            {
                Dictionary<string, int> osmDomainAttributeFieldIndices = (Dictionary<string, int>)relationFieldIndexes.GetProperty("domainAttributesFieldIndex");
                int osmIDFieldIndex = (int)relationFieldIndexes.GetProperty("osmIDFieldIndex");
                int tagCollectionFieldIndex = (int)relationFieldIndexes.GetProperty("osmTagsFieldIndex");
                int osmSupportingElementFieldIndex = (int)relationFieldIndexes.GetProperty("osmSupportingElementFieldIndex");
                int osmUserFieldIndex = (int)relationFieldIndexes.GetProperty("osmUserFieldIndex");
                int osmUIDFieldIndex = (int)relationFieldIndexes.GetProperty("osmUIDFieldIndex");
                int osmVisibleFieldIndex = (int)relationFieldIndexes.GetProperty("osmVisibleFieldIndex");
                int osmVersionFieldIndex = (int)relationFieldIndexes.GetProperty("osmVersionFieldIndex");
                int osmChangesetFieldIndex = (int)relationFieldIndexes.GetProperty("osmChangeSetFieldIndex");
                int osmTimeStampFieldIndex = (int)relationFieldIndexes.GetProperty("osmTimeStampFieldIndex");
                int osmTrackChangesFieldIndex = (int)relationFieldIndexes.GetProperty("osmTrackChangesFieldIndex");
                int osmMembersFieldIndex = (int)relationFieldIndexes.GetProperty("osmMembersFieldIndex");

                IQueryFilter queryFilter = new QueryFilterClass();
                comReleaser.ManageLifetime(queryFilter);
                queryFilter.SubFields = String.Join(",", insertFeatureClass.ExtractRequiredFields());

                IFeature insertFeature = null;
                IFeatureCursor featureCursor = null;

                OSMToolHelper osmToolHelper = new OSMToolHelper();

                // delete the mentioned lines
                List<relation> relationsToWorkOn = elementsInAOI.Values.Select(kp => kp.Key).ToList();
                List<string> whereClauses = osmToolHelper.SplitOSMIDRequests(relationsToWorkOn, extensionVersion);
                string relationIDs = insertFeatureClass.SqlIdentifier("OSMID");

                switch (action)
                {
                    case "create_modify":
                        foreach (string whereClause in whereClauses)
                        {
                            if (cancelTracker.Continue() == false)
                                return;

                            queryFilter.WhereClause = relationIDs + " IN " + whereClause;

                            featureCursor = insertFeatureClass.Update(queryFilter, false);
                            comReleaser.ManageLifetime(featureCursor);
                            insertFeature = featureCursor.NextFeature();

                            while (insertFeature != null)
                            {
                                if (osmIDFieldIndex > -1)
                                {
                                    string elementID = insertFeature.get_Value(osmIDFieldIndex) as string;
                                    if (elementsInAOI.ContainsKey(elementID))
                                    {
                                        KeyValuePair<relation, IGeometry> currentRelation = elementsInAOI[elementID];

                                        manipulateRelationFeature(currentRelation.Key, ref insertFeature, availableDomains, domainFieldLength, extensionVersion, currentRelation.Value, osmIDFieldIndex, tagCollectionFieldIndex, osmDomainAttributeFieldIndices, osmSupportingElementFieldIndex, osmUserFieldIndex, osmUIDFieldIndex, osmVisibleFieldIndex, osmVersionFieldIndex, osmChangesetFieldIndex, osmTimeStampFieldIndex, osmTrackChangesFieldIndex, osmMembersFieldIndex);

                                        // remove the current node 
                                        elementsInAOI.Remove(elementID);

                                        featureCursor.UpdateFeature(insertFeature);

                                        if (useVerboseLogging)
                                        {
                                            message.AddMessage(String.Format(resourceManager.GetString("GPTools_OSMGPDiffLoader_modifymessage"), resourceManager.GetString("GPTools_OSM_relation"), currentRelation.Key.id));
                                        }
                                    }
                                }

                                insertFeature = featureCursor.NextFeature();
                            }
                        }

                        // at this point we should have only ways left that need to be created
                        featureCursor = insertFeatureClass.Insert(true);
                        comReleaser.ManageLifetime(featureCursor);

                        foreach (var item in elementsInAOI)
                        {
                            if (cancelTracker.Continue() == false)
                                return;


                            insertFeature = insertFeatureClass.CreateFeatureBuffer() as IFeature;

                            KeyValuePair<relation, IGeometry> currentRelation = item.Value;

                            manipulateRelationFeature(currentRelation.Key, ref insertFeature, availableDomains, domainFieldLength, extensionVersion, currentRelation.Value, osmIDFieldIndex, tagCollectionFieldIndex, osmDomainAttributeFieldIndices, osmSupportingElementFieldIndex, osmUserFieldIndex, osmUIDFieldIndex, osmVisibleFieldIndex, osmVersionFieldIndex, osmChangesetFieldIndex, osmTimeStampFieldIndex, osmTrackChangesFieldIndex, osmMembersFieldIndex);

                            featureCursor.InsertFeature((IFeatureBuffer)insertFeature);

                            if (useVerboseLogging)
                            {
                                message.AddMessage(String.Format(resourceManager.GetString("GPTools_OSMGPDiffLoader_createmessage"), resourceManager.GetString("GPTools_OSM_relation"), currentRelation.Key.id));
                            }
                        }

                        break;
                    case "delete":

                        IFeatureCursor deleteCursor = null;
                        IFeature deleteFeature = null;

                        queryFilter = new QueryFilterClass();
                        comReleaser.ManageLifetime(queryFilter);

                        foreach (string whereClause in whereClauses)
                        {
                            if (cancelTracker.Continue() == false)
                                return;

                            queryFilter.WhereClause = relationIDs + " IN " + whereClause;

                            deleteCursor = insertFeatureClass.Update(queryFilter, false);
                            deleteFeature = deleteCursor.NextFeature();

                            if (deleteFeature != null)
                            {
                                String elementID = String.Empty;
                                if (osmIDFieldIndex > -1)
                                {
                                    elementID = Convert.ToString(deleteFeature.get_Value(osmIDFieldIndex));
                                }

                                if (useVerboseLogging == true)
                                {
                                    message.AddMessage(String.Format(resourceManager.GetString("GPTools_OSMGPDiffLoader_deletemessage"), resourceManager.GetString("GPTools_OSM_relation"), elementID));
                                }

                                deleteFeature.Delete();

                                deleteFeature = deleteCursor.NextFeature();
                            }

                        }

                        break;
                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine(ex.Message);
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
#endif
            }

        }

        private void manipulateRelations(Dictionary<string, relation> relations, string action, IGeometry filterGeometry, ITable relationTable, IPropertySet relationFieldIndexes, OSMDomains availableDomains, int domainFieldLengthLines, IFeatureClass osmLineFeatureClass, IPropertySet polylineFieldIndexes, int domainFieldLengthPolygons, IFeatureClass osmPolygonFeatureClass, IPropertySet polygonFieldIndexes, ref IGPMessages message, int internalExtensionVersion, ComReleaser comReleaser, bool useVerboseLogging, ref ITrackCancel trackCancel)
        {

            Dictionary<string, KeyValuePair<relation, IGeometry>> linesInAOI = new Dictionary<string, KeyValuePair<relation, IGeometry>>();
            Dictionary<string, KeyValuePair<relation, IGeometry>> polygonsInAOI = new Dictionary<string, KeyValuePair<relation, IGeometry>>();
            Dictionary<string, relation> relationsInAOI = new Dictionary<string, relation>();

            try
            {
                OSMToolHelper osmToolHelper = new OSMToolHelper();
                IRelationalOperator relationalOperator = filterGeometry as IRelationalOperator;

                foreach (KeyValuePair<string, relation> item in relations)
                {
                    if (trackCancel.Continue() == false)
                        return;

                    // a relation can be multiple things, multipart polyline or polygon is one we would like to treat special
                    esriGeometryType detectedGeoType = osmToolHelper.determineRelationGeometryType(osmLineFeatureClass, osmPolygonFeatureClass, relationTable, item.Value);

                    IGeometry relationGeometry = null;
                    relationGeometry = extractGeometryFromOSMFeature(item.Value, detectedGeoType, osmLineFeatureClass, osmPolygonFeatureClass, internalExtensionVersion);

                    // check if geometry type and geometry are in sync
                    if (relationGeometry == null)
                        detectedGeoType = esriGeometryType.esriGeometryNull;

                    // if we have a filter geometry check if the newly created point is inside or outside the AOI 
                    if (filterGeometry != null)
                    {
                        if (relationGeometry == null)
                        {
                            relationsInAOI.Add(item.Key, item.Value);
                        }
                        else
                        {
                            relationGeometry.Project(filterGeometry.SpatialReference);

                            if (relationalOperator.Contains(relationGeometry))
                            {
                                // if the geometry is contained in the AOI - filterGeometry, let's keep if for further processing
                                if (detectedGeoType == esriGeometryType.esriGeometryLine)
                                    linesInAOI.Add(item.Key, new KeyValuePair<relation, IGeometry>(item.Value, relationGeometry));
                                else if (detectedGeoType == esriGeometryType.esriGeometryPolygon)
                                    polygonsInAOI.Add(item.Key, new KeyValuePair<relation, IGeometry>(item.Value, relationGeometry));
                                else
                                    relationsInAOI.Add(item.Key, item.Value);
                            }
                        }
                    }
                    else
                    {
                        // otherwise we collect everything
                        if (detectedGeoType == esriGeometryType.esriGeometryLine)
                            linesInAOI.Add(item.Key, new KeyValuePair<relation, IGeometry>(item.Value, relationGeometry));
                        else if (detectedGeoType == esriGeometryType.esriGeometryPolygon)
                            polygonsInAOI.Add(item.Key, new KeyValuePair<relation, IGeometry>(item.Value, relationGeometry));
                        else
                            relationsInAOI.Add(item.Key, item.Value);
                    }
                }

                //Dictionary<string, int> osmDomainAttributeFieldIndices = (Dictionary<string, int>)relationFieldIndexes.GetProperty("domainAttributesFieldIndex");
                int osmIDFieldIndex = (int)relationFieldIndexes.GetProperty("osmIDFieldIndex");
                int tagCollectionFieldIndex = (int)relationFieldIndexes.GetProperty("osmTagsFieldIndex");
                int osmSupportingElementFieldIndex = (int)relationFieldIndexes.GetProperty("osmSupportingElementFieldIndex");
                int osmUserFieldIndex = (int)relationFieldIndexes.GetProperty("osmUserFieldIndex");
                int osmUIDFieldIndex = (int)relationFieldIndexes.GetProperty("osmUIDFieldIndex");
                int osmVisibleFieldIndex = (int)relationFieldIndexes.GetProperty("osmVisibleFieldIndex");
                int osmVersionFieldIndex = (int)relationFieldIndexes.GetProperty("osmVersionFieldIndex");
                int osmChangesetFieldIndex = (int)relationFieldIndexes.GetProperty("osmChangeSetFieldIndex");
                int osmTimeStampFieldIndex = (int)relationFieldIndexes.GetProperty("osmTimeStampFieldIndex");
                int osmTrackChangesFieldIndex = (int)relationFieldIndexes.GetProperty("osmTrackChangesFieldIndex");
                int osmMembersFieldIndex = (int)relationFieldIndexes.GetProperty("osmMembersFieldIndex");

                IFeatureCursor featureCursor = null;
                IFeature editFeature = null;

                IQueryFilter lineQueryFilter = new QueryFilterClass();
                comReleaser.ManageLifetime(lineQueryFilter);
                lineQueryFilter.SubFields = String.Join(",", osmLineFeatureClass.ExtractRequiredFields());

                Dictionary<string, int> osmLineDomainAttributeFieldIndices = (Dictionary<string, int>)polylineFieldIndexes.GetProperty("domainAttributesFieldIndex");
                int osmLineIDFieldIndex = (int)polylineFieldIndexes.GetProperty("osmIDFieldIndex");
                int lineTagCollectionFieldIndex = (int)polylineFieldIndexes.GetProperty("osmTagsFieldIndex");
                int osmLineSupportingElementFieldIndex = (int)polylineFieldIndexes.GetProperty("osmSupportingElementFieldIndex");
                int osmLineUserFieldIndex = (int)polylineFieldIndexes.GetProperty("osmUserFieldIndex");
                int osmLineUIDFieldIndex = (int)polylineFieldIndexes.GetProperty("osmUIDFieldIndex");
                int osmLineVisibleFieldIndex = (int)polylineFieldIndexes.GetProperty("osmVisibleFieldIndex");
                int osmLineVersionFieldIndex = (int)polylineFieldIndexes.GetProperty("osmVersionFieldIndex");
                int osmLineChangesetFieldIndex = (int)polylineFieldIndexes.GetProperty("osmChangeSetFieldIndex");
                int osmLineTimeStampFieldIndex = (int)polylineFieldIndexes.GetProperty("osmTimeStampFieldIndex");
                int osmLineTrackChangesFieldIndex = (int)polylineFieldIndexes.GetProperty("osmTrackChangesFieldIndex");
                int osmLineMembersFieldIndex = (int)polylineFieldIndexes.GetProperty("osmMembersFieldIndex");


                List<relation> lineRelations = linesInAOI.Values.Select(kp => kp.Key).ToList();
                List<string> lineWhereClauses = osmToolHelper.SplitOSMIDRequests(lineRelations, internalExtensionVersion);
                string lineIDs = osmLineFeatureClass.SqlIdentifier("OSMID");

                IQueryFilter polygonQueryFilter = new QueryFilterClass();
                comReleaser.ManageLifetime(lineQueryFilter);
                polygonQueryFilter.SubFields = String.Join(",", osmLineFeatureClass.ExtractRequiredFields());

                Dictionary<string, int> osmPolyDomainAttributeFieldIndices = (Dictionary<string, int>)polygonFieldIndexes.GetProperty("domainAttributesFieldIndex");
                int osmPolyIDFieldIndex = (int)polygonFieldIndexes.GetProperty("osmIDFieldIndex");
                int polyTagCollectionFieldIndex = (int)polygonFieldIndexes.GetProperty("osmTagsFieldIndex");
                int osmPolySupportingElementFieldIndex = (int)polygonFieldIndexes.GetProperty("osmSupportingElementFieldIndex");
                int osmPolyUserFieldIndex = (int)polygonFieldIndexes.GetProperty("osmUserFieldIndex");
                int osmPolyUIDFieldIndex = (int)polygonFieldIndexes.GetProperty("osmUIDFieldIndex");
                int osmPolyVisibleFieldIndex = (int)polygonFieldIndexes.GetProperty("osmVisibleFieldIndex");
                int osmPolyVersionFieldIndex = (int)polygonFieldIndexes.GetProperty("osmVersionFieldIndex");
                int osmPolyChangesetFieldIndex = (int)polygonFieldIndexes.GetProperty("osmChangeSetFieldIndex");
                int osmPolyTimeStampFieldIndex = (int)polygonFieldIndexes.GetProperty("osmTimeStampFieldIndex");
                int osmPolyTrackChangesFieldIndex = (int)polygonFieldIndexes.GetProperty("osmTrackChangesFieldIndex");
                int osmPolyMembersFieldIndex = (int)polygonFieldIndexes.GetProperty("osmMembersFieldIndex");


                List<relation> polygonRelations = polygonsInAOI.Values.Select(kp => kp.Key).ToList();
                List<string> polygonWhereClauses = osmToolHelper.SplitOSMIDRequests(polygonRelations, internalExtensionVersion);
                string polygonIDs = osmPolygonFeatureClass.SqlIdentifier("OSMID");

                List<relation> relationsToWorkOn = relationsInAOI.Values.ToList();
                List<string> relationWhereClauses = osmToolHelper.SplitOSMIDRequests(relationsToWorkOn, internalExtensionVersion);
                string relationIDs = relationTable.SqlIdentifier("OSMID");


                switch (action)
                {
                    case "create_modify":
                        #region create - modify line relations
                        foreach (string whereClause in lineWhereClauses)
                        {
                            if (trackCancel.Continue() == false)
                                return;

                            lineQueryFilter.WhereClause = lineIDs + " IN " + whereClause;

                            featureCursor = osmLineFeatureClass.Update(lineQueryFilter, false);
                            comReleaser.ManageLifetime(featureCursor);
                            editFeature = featureCursor.NextFeature();

                            while (editFeature != null)
                            {
                                if (osmIDFieldIndex > -1)
                                {
                                    string elementID = editFeature.get_Value(osmIDFieldIndex) as string;
                                    if (linesInAOI.ContainsKey(elementID))
                                    {
                                        KeyValuePair<relation, IGeometry> currentElement = linesInAOI[elementID];

                                        manipulateRelationFeature(currentElement.Key, ref editFeature, availableDomains, domainFieldLengthLines, internalExtensionVersion, currentElement.Value, osmLineIDFieldIndex, lineTagCollectionFieldIndex, osmLineDomainAttributeFieldIndices, osmLineSupportingElementFieldIndex, osmLineUserFieldIndex, osmLineUIDFieldIndex, osmLineVisibleFieldIndex, osmLineVersionFieldIndex, osmLineChangesetFieldIndex, osmLineTimeStampFieldIndex, osmLineTrackChangesFieldIndex, osmLineMembersFieldIndex);

                                        //remove the current relation - line
                                        linesInAOI.Remove(elementID);

                                        featureCursor.UpdateFeature(editFeature);

                                        if (useVerboseLogging)
                                        {
                                            message.AddMessage(string.Format(resourceManager.GetString("GPTools_OSMGPDiffLoader_modifymessage"), resourceManager.GetString("GPTools_OSM_relation"), elementID));
                                        }
                                    }
                                }

                                editFeature = featureCursor.NextFeature();
                            }

                            // at this point we should have only relations (linear) left that need to be created
                            featureCursor = osmLineFeatureClass.Insert(true);
                            comReleaser.ManageLifetime(featureCursor);

                            foreach (var createLine in linesInAOI)
                            {
                                if (trackCancel.Continue() == false)
                                    return;

                                editFeature = osmLineFeatureClass.CreateFeatureBuffer() as IFeature;

                                KeyValuePair<relation, IGeometry> currentElement = createLine.Value;

                                manipulateRelationFeature(currentElement.Key, ref editFeature, availableDomains, domainFieldLengthLines, internalExtensionVersion, currentElement.Value, osmLineIDFieldIndex, lineTagCollectionFieldIndex, osmLineDomainAttributeFieldIndices, osmLineSupportingElementFieldIndex, osmLineUserFieldIndex, osmLineUIDFieldIndex, osmLineVisibleFieldIndex, osmLineVersionFieldIndex, osmLineChangesetFieldIndex, osmLineTimeStampFieldIndex, osmLineTrackChangesFieldIndex, osmLineMembersFieldIndex);

                                featureCursor.InsertFeature((IFeatureBuffer)editFeature);

                                if (useVerboseLogging)
                                {
                                    message.AddMessage(string.Format(resourceManager.GetString("GPTools_OSMGPDiffLoader_createmessage"), resourceManager.GetString("GPTools_OSM_relation"), currentElement.Key.id));
                                }

                            }
                        }
                        #endregion

                        #region create - modify polygon relations
                        foreach (string whereClause in polygonWhereClauses)
                        {
                            if (trackCancel.Continue() == false)
                                return;

                            lineQueryFilter.WhereClause = polygonIDs + " IN " + whereClause;

                            featureCursor = osmPolygonFeatureClass.Update(polygonQueryFilter, false);
                            comReleaser.ManageLifetime(featureCursor);
                            editFeature = featureCursor.NextFeature();

                            while (editFeature != null)
                            {
                                if (osmIDFieldIndex > -1)
                                {
                                    string elementID = editFeature.get_Value(osmIDFieldIndex) as string;
                                    if (polygonsInAOI.ContainsKey(elementID))
                                    {
                                        KeyValuePair<relation, IGeometry> currentElement = linesInAOI[elementID];

                                        manipulateRelationFeature(currentElement.Key, ref editFeature, availableDomains, domainFieldLengthLines, internalExtensionVersion, currentElement.Value, osmPolyIDFieldIndex, polyTagCollectionFieldIndex, osmPolyDomainAttributeFieldIndices, osmPolySupportingElementFieldIndex, osmPolyUserFieldIndex, osmPolyUIDFieldIndex, osmPolyVisibleFieldIndex, osmPolyVersionFieldIndex, osmPolyChangesetFieldIndex, osmPolyTimeStampFieldIndex, osmPolyTrackChangesFieldIndex, osmPolyMembersFieldIndex);

                                        //remove the current relation - line
                                        polygonsInAOI.Remove(elementID);

                                        featureCursor.UpdateFeature(editFeature);

                                        if (useVerboseLogging)
                                        {
                                            message.AddMessage(string.Format(resourceManager.GetString("GPTools_OSMGPDiffLoader_modifymessage"), resourceManager.GetString("GPTools_OSM_relation"), elementID));
                                        }
                                    }
                                }

                                editFeature = featureCursor.NextFeature();
                            }

                            // at this point we should have only relations (polygonal) left that need to be created
                            featureCursor = osmPolygonFeatureClass.Insert(true);
                            comReleaser.ManageLifetime(featureCursor);

                            foreach (var createPolygon in polygonsInAOI)
                            {
                                if (trackCancel.Continue() == false)
                                    return;

                                editFeature = osmPolygonFeatureClass.CreateFeatureBuffer() as IFeature;

                                KeyValuePair<relation, IGeometry> currentElement = createPolygon.Value;

                                manipulateRelationFeature(currentElement.Key, ref editFeature, availableDomains, domainFieldLengthLines, internalExtensionVersion, currentElement.Value, osmPolyIDFieldIndex, polyTagCollectionFieldIndex, osmPolyDomainAttributeFieldIndices, osmPolySupportingElementFieldIndex, osmPolyUserFieldIndex, osmPolyUIDFieldIndex, osmPolyVisibleFieldIndex, osmPolyVersionFieldIndex, osmPolyChangesetFieldIndex, osmPolyTimeStampFieldIndex, osmPolyTrackChangesFieldIndex, osmPolyMembersFieldIndex);

                                featureCursor.InsertFeature((IFeatureBuffer)editFeature);

                                if (useVerboseLogging)
                                {
                                    message.AddMessage(string.Format(resourceManager.GetString("GPTools_OSMGPDiffLoader_createmessage"), resourceManager.GetString("GPTools_OSM_relation"), currentElement.Key.id));
                                }
                            }
                        }
                        # endregion

                        #region create - modify relations
                        IQueryFilter rowQueryFilter = new QueryFilterClass();
                        comReleaser.ManageLifetime(rowQueryFilter);

                        IRow editRow = null;
                        ICursor rowCursor = null;

                        foreach (string whereClause in relationWhereClauses)
                        {
                            if (trackCancel.Continue() == false)
                                return;

                            rowQueryFilter.WhereClause = relationIDs + " IN " + whereClause;

                            rowCursor = relationTable.Update(rowQueryFilter, false);
                            comReleaser.ManageLifetime(rowCursor);
                            editRow = rowCursor.NextRow();

                            while (editRow != null)
                            {
                                if (osmIDFieldIndex > -1)
                                {
                                    string elementID = editRow.get_Value(osmIDFieldIndex) as string;
                                    if (relationsInAOI.ContainsKey(elementID))
                                    {
                                        relation currentRelation = relationsInAOI[elementID];

                                        manipulateRelationRow(currentRelation, ref editRow, internalExtensionVersion, osmIDFieldIndex, tagCollectionFieldIndex, osmSupportingElementFieldIndex, osmUserFieldIndex, osmUIDFieldIndex, osmVisibleFieldIndex, osmVersionFieldIndex, osmChangesetFieldIndex, osmTimeStampFieldIndex, osmMembersFieldIndex);

                                        // remove the current relation from the list
                                        relationsInAOI.Remove(elementID);

                                        rowCursor.UpdateRow(editRow);

                                        if (useVerboseLogging)
                                        {
                                            message.AddMessage(string.Format(resourceManager.GetString("GPTools_OSMGPDiffLoader_modifymessage"), resourceManager.GetString("GPTools_OSM_relation"), currentRelation.id));
                                        }
                                    }
                                }

                                editRow = rowCursor.NextRow();
                            }
                        }
                        #endregion

                        #region create - relations in table
                        // at this point we should have only relations left that need to be created
                        rowCursor = relationTable.Insert(true);
                        comReleaser.ManageLifetime(rowCursor);

                        foreach (var rel in relationsInAOI)
                        {
                            if (trackCancel.Continue() == false)
                                return;


                            editRow = relationTable.CreateRowBuffer() as IRow;

                            relation currentRelation = rel.Value;

                            manipulateRelationRow(currentRelation, ref editRow, internalExtensionVersion, osmIDFieldIndex, tagCollectionFieldIndex, osmSupportingElementFieldIndex, osmUserFieldIndex, osmUIDFieldIndex, osmVisibleFieldIndex, osmVersionFieldIndex, osmChangesetFieldIndex, osmTimeStampFieldIndex, osmMembersFieldIndex);

                            rowCursor.InsertRow((IRowBuffer)editRow);

                            if (useVerboseLogging)
                            {
                                message.AddMessage(string.Format(resourceManager.GetString("GPTools_OSMGPDiffLoader_createmessage"), resourceManager.GetString("GPTools_OSM_relation"), currentRelation.id));
                            }
                        }
                        #endregion

                        break;
                    case "delete":
                        ICursor deleteCursor = null;
                        IRow deleteRow = null;

                        IQueryFilter deleteQueryFilter = new QueryFilterClass();
                        comReleaser.ManageLifetime(deleteQueryFilter);



                        foreach (string whereClause in lineWhereClauses)
                        {
                            if (trackCancel.Continue() == false)
                                return;

                            deleteQueryFilter.WhereClause = lineIDs + " IN " + whereClause;

                            deleteCursor = ((ITable)osmLineFeatureClass).Update(deleteQueryFilter, false);
                            deleteRow = deleteCursor.NextRow();

                            while (deleteRow != null)
                            {
                                string relationID = String.Empty;

                                if (osmLineIDFieldIndex > -1)
                                {
                                    deleteRow.get_Value(osmLineIDFieldIndex);
                                }

                                deleteRow.Delete();

                                if (useVerboseLogging)
                                {
                                    message.AddMessage(String.Format(resourceManager.GetString("GPTools_OSMGPDiffLoader_deletemessage"), resourceManager.GetString("GPTools_OSM_relation"), relationID));
                                }

                                deleteRow = deleteCursor.NextRow();
                            }
                        }

                        foreach (string whereClause in polygonWhereClauses)
                        {
                            if (trackCancel.Continue() == false)
                                return;

                            deleteQueryFilter.WhereClause = polygonIDs + " IN " + whereClause;

                            deleteCursor = ((ITable)osmPolygonFeatureClass).Update(deleteQueryFilter, false);
                            deleteRow = deleteCursor.NextRow();

                            while (deleteRow != null)
                            {
                                string relationID = String.Empty;

                                if (osmLineIDFieldIndex > -1)
                                {
                                    deleteRow.get_Value(osmLineIDFieldIndex);
                                }

                                deleteRow.Delete();

                                if (useVerboseLogging)
                                {
                                    message.AddMessage(String.Format(resourceManager.GetString("GPTools_OSMGPDiffLoader_deletemessage"), resourceManager.GetString("GPTools_OSM_relation"), relationID));
                                }

                                deleteRow = deleteCursor.NextRow();
                            }
                        }

                        foreach (string whereClause in relationWhereClauses)
                        {
                            if (trackCancel.Continue() == false)
                                return;

                            deleteQueryFilter.WhereClause = relationIDs + " IN " + whereClause;

                            deleteCursor = relationTable.Update(deleteQueryFilter, false);
                            deleteRow = deleteCursor.NextRow();

                            while (deleteRow != null)
                            {
                                string relationID = String.Empty;

                                if (osmLineIDFieldIndex > -1)
                                {
                                    deleteRow.get_Value(osmLineIDFieldIndex);
                                }

                                deleteRow.Delete();

                                if (useVerboseLogging)
                                {
                                    message.AddMessage(String.Format(resourceManager.GetString("GPTools_OSMGPDiffLoader_deletemessage"), resourceManager.GetString("GPTools_OSM_relation"), relationID));
                                }

                                deleteRow = deleteCursor.NextRow();
                            }
                        }

                        break;
                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine(ex.Message);
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
#endif
            }
        }

        private void manipulateWays(Dictionary<string, way> ways, string action, IGeometry filterGeometry, IFeatureClass osmPointFeatureClass, IPropertySet pointFieldIndexes, OSMDomains availableDomains, int domainFieldLengthLines, IFeatureClass osmLineFeatureClass, IPropertySet polylineFieldIndexes, int domainFieldLengthPolygons, IFeatureClass osmPolygonFeatureClass, IPropertySet polygonFieldIndexes, ref IGPMessages message, int internalExtensionVersion, ComReleaser comReleaser, bool useVerboseLogging, ref ITrackCancel trackCancel)
        {
            Dictionary<string, KeyValuePair<way, IGeometry>> linesInAOI = new Dictionary<string, KeyValuePair<way, IGeometry>>();
            Dictionary<string, KeyValuePair<way, IGeometry>> polygonsInAOI = new Dictionary<string, KeyValuePair<way, IGeometry>>();

            try
            {
                OSMToolHelper osmToolHelper = new OSMToolHelper();

                IRelationalOperator relationalOperator = filterGeometry as IRelationalOperator;
                int osmPointIDFieldIndex = osmPointFeatureClass.FindField("OSMID");

                foreach (KeyValuePair<string, way> item in ways)
                {
                    if (trackCancel.Continue() == false)
                        return;

                    IGeometry wayGeometry = null;

                    bool isLine = OSMToolHelper.IsThisWayALine(item.Value);

#if DEBUG
                    DateTime startTime = DateTime.Now;
                    if (useVerboseLogging)
                    {
                        message.AddMessage(String.Format("extracting geometry for way id {0}", item.Value.id));
                    }
#endif

                    if (isLine)
                        wayGeometry = extractGeometryFromOSMFeature(item.Value, osmLineFeatureClass, osmPointFeatureClass, osmPointIDFieldIndex, internalExtensionVersion, comReleaser);
                    else
                        wayGeometry = extractGeometryFromOSMFeature(item.Value, osmPolygonFeatureClass, osmPointFeatureClass, osmPointIDFieldIndex, internalExtensionVersion, comReleaser);

#if DEBUG
                    if (useVerboseLogging)
                    {
                        message.AddMessage(String.Format("found geometry in {0} sec", (new TimeSpan(DateTime.Now.Ticks - startTime.Ticks)).TotalSeconds));
                    }
#endif

                    // we are unable to complete the geometry, so let's continue with the next way
                    if (wayGeometry == null)
                        continue;


                    // if we have a filter geometry check if the newly created point is inside or outside the AOI 
                    if (filterGeometry != null)
                    {
                        wayGeometry.Project(filterGeometry.SpatialReference);

                        if (relationalOperator.Contains(wayGeometry))
                        {
                            // if the geometry is contained in the AOI - filterGeometry, let's keep if for further processing
                            if (isLine)
                                linesInAOI.Add(item.Key, new KeyValuePair<way, IGeometry>(item.Value, wayGeometry));
                            else
                                polygonsInAOI.Add(item.Key, new KeyValuePair<way, IGeometry>(item.Value, wayGeometry));
                        }
                    }
                    else
                    {
                        if (isLine)
                            linesInAOI.Add(item.Key, new KeyValuePair<way, IGeometry>(item.Value, wayGeometry));
                        else
                            polygonsInAOI.Add(item.Key, new KeyValuePair<way, IGeometry>(item.Value, wayGeometry));
                    }
                }

                // if all the geometries are outside our area then there is nothing to do
                if (linesInAOI.Count == 0 & polygonsInAOI.Count == 0)
                    return;

                // work on the mentioned lines
                manipulateLinePolyFeatures(action, linesInAOI, osmLineFeatureClass, polylineFieldIndexes, availableDomains, domainFieldLengthLines, internalExtensionVersion, comReleaser, ref trackCancel, ref message, useVerboseLogging);

                // work on the mentioned polygons
                manipulateLinePolyFeatures(action, polygonsInAOI, osmPolygonFeatureClass, polygonFieldIndexes, availableDomains, domainFieldLengthPolygons, internalExtensionVersion, comReleaser, ref trackCancel, ref message, useVerboseLogging);

            }
            catch (Exception ex)
            {
                #if DEBUG
                System.Diagnostics.Debug.WriteLine(ex.Message);
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
                #endif
            }
        }


        private void manipulateLinePolyFeatures(string action, Dictionary<string, KeyValuePair<way, IGeometry>> elementsInAOI, IFeatureClass insertFeatureClass, IPropertySet wayFieldIndexes, OSMDomains availableDomains, int domainFieldLength, int extensionVersion, ComReleaser comReleaser, ref ITrackCancel cancelTracker, ref IGPMessages message, bool useVerboseLogging)
        {
            try
            {
                Dictionary<string, int> osmDomainAttributeFieldIndices = (Dictionary<string, int>)wayFieldIndexes.GetProperty("domainAttributesFieldIndex");
                int osmIDFieldIndex = (int)wayFieldIndexes.GetProperty("osmIDFieldIndex");
                int tagCollectionFieldIndex = (int)wayFieldIndexes.GetProperty("osmTagsFieldIndex");
                int osmSupportingElementFieldIndex = (int)wayFieldIndexes.GetProperty("osmSupportingElementFieldIndex");
                int osmUserFieldIndex = (int)wayFieldIndexes.GetProperty("osmUserFieldIndex");
                int osmUIDFieldIndex = (int)wayFieldIndexes.GetProperty("osmUIDFieldIndex");
                int osmVisibleFieldIndex = (int)wayFieldIndexes.GetProperty("osmVisibleFieldIndex");
                int osmVersionFieldIndex = (int)wayFieldIndexes.GetProperty("osmVersionFieldIndex");
                int osmChangesetFieldIndex = (int)wayFieldIndexes.GetProperty("osmChangeSetFieldIndex");
                int osmTimeStampFieldIndex = (int)wayFieldIndexes.GetProperty("osmTimeStampFieldIndex");
                int osmTrackChangesFieldIndex = (int)wayFieldIndexes.GetProperty("osmTrackChangesFieldIndex");

                IQueryFilter queryFilter = new QueryFilterClass();
                comReleaser.ManageLifetime(queryFilter);
                queryFilter.SubFields = String.Join(",", insertFeatureClass.ExtractRequiredFields());

                IFeature insertFeature = null;
                IFeatureCursor featureCursor = null;

                OSMToolHelper osmToolHelper = new OSMToolHelper();

                // delete the mentioned lines
                List<way> waysToWorkOn = elementsInAOI.Values.Select(kp => kp.Key).ToList();
                List<string> whereClauses = osmToolHelper.SplitOSMIDRequests(waysToWorkOn, extensionVersion);
                string featureIDs = insertFeatureClass.SqlIdentifier("OSMID");

                switch (action)
                {
                    case "create_modify":
                        foreach (string whereClause in whereClauses)
                        {
                            if (cancelTracker.Continue() == false)
                                return;

                            queryFilter.WhereClause = featureIDs + " IN " + whereClause;

                            featureCursor = insertFeatureClass.Update(queryFilter, false);
                            comReleaser.ManageLifetime(featureCursor);
                            insertFeature = featureCursor.NextFeature();

                            while (insertFeature != null)
                            {
                                if (osmIDFieldIndex > -1)
                                {
                                    string elementID = insertFeature.get_Value(osmIDFieldIndex) as string;
                                    if (elementsInAOI.ContainsKey(elementID))
                                    {
                                        KeyValuePair<way, IGeometry> currentWay = elementsInAOI[elementID];

                                        manipulateLinearWayFeature(currentWay.Key, ref insertFeature, availableDomains, domainFieldLength, extensionVersion, currentWay.Value, osmIDFieldIndex, tagCollectionFieldIndex, osmDomainAttributeFieldIndices, osmSupportingElementFieldIndex, osmUserFieldIndex, osmUIDFieldIndex, osmVisibleFieldIndex, osmVersionFieldIndex, osmChangesetFieldIndex, osmTimeStampFieldIndex, osmTrackChangesFieldIndex);

                                        // remove the current node 
                                        elementsInAOI.Remove(elementID);

                                        featureCursor.UpdateFeature(insertFeature);

                                        if (useVerboseLogging)
                                        {
                                            message.AddMessage(String.Format(resourceManager.GetString("GPTools_OSMGPDiffLoader_modifymessage"), resourceManager.GetString("GPTools_OSM_way"), currentWay.Key.id));
                                        }
                                    }
                                }

                                insertFeature = featureCursor.NextFeature();
                            }
                        }

                        // at this point we should have only ways left that need to be created
                        featureCursor = insertFeatureClass.Insert(true);
                        comReleaser.ManageLifetime(featureCursor);

                        foreach (var item in elementsInAOI)
                        {
                            if (cancelTracker.Continue() == false)
                                return;


                            insertFeature = insertFeatureClass.CreateFeatureBuffer() as IFeature;

                            KeyValuePair<way, IGeometry> currentWay = item.Value;

                            manipulateLinearWayFeature(currentWay.Key, ref insertFeature, availableDomains, domainFieldLength, extensionVersion, currentWay.Value, osmIDFieldIndex, tagCollectionFieldIndex, osmDomainAttributeFieldIndices, osmSupportingElementFieldIndex, osmUserFieldIndex, osmUIDFieldIndex, osmVisibleFieldIndex, osmVersionFieldIndex, osmChangesetFieldIndex, osmTimeStampFieldIndex, osmTrackChangesFieldIndex);

                            featureCursor.InsertFeature((IFeatureBuffer)insertFeature);

                            if (useVerboseLogging)
                            {
                                message.AddMessage(String.Format(resourceManager.GetString("GPTools_OSMGPDiffLoader_createmessage"), resourceManager.GetString("GPTools_OSM_way"), currentWay.Key.id));
                            }
                        }

                        break;
                    case "delete":

                        IFeatureCursor deleteCursor = null;
                        IFeature deleteFeature = null;

                        queryFilter = new QueryFilterClass();
                        comReleaser.ManageLifetime(queryFilter);

                        foreach (string whereClause in whereClauses)
                        {
                            if (cancelTracker.Continue() == false)
                                return;

                            queryFilter.WhereClause = featureIDs + " IN " + whereClause;

                            deleteCursor = insertFeatureClass.Update(queryFilter, false);
                            deleteFeature = deleteCursor.NextFeature();

                            if (deleteFeature != null)
                            {
                                string elementID = String.Empty;
                                if (osmIDFieldIndex > -1)
                                {
                                    elementID = Convert.ToString(deleteFeature.get_Value(osmIDFieldIndex));
                                }

                                if (useVerboseLogging == true)
                                {
                                    message.AddMessage(String.Format(resourceManager.GetString("GPTools_OSMGPDiffLoader_deletemessage"), resourceManager.GetString("GPTools_OSM_way"), elementID));
                                }

                                deleteFeature.Delete();

                                deleteFeature = deleteCursor.NextFeature();
                            }

                        }

                        break;
                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine(ex.Message);
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
#endif
            }
        }


        void insertWay(way currentWay, string action, IFeatureClass insertFeatureClass, IGeometry featureGeometry, IGeometry filterGeometry, IFeatureClass osmPointFeatureClass, IPropertySet pointFieldIndexes, OSMDomains availableDomains, int domainFieldLength, IPropertySet wayFieldIndexes, ref IGPMessages message, int extensionVersion, ComReleaser comReleaser)
        {
            IFeature newWayFeature = null;
            IFeatureCursor updateCursor = null;
            
            try
            {
                if (currentWay == null)
                {
                    throw new ArgumentNullException(GetName(currentWay));
                }

                if (String.IsNullOrEmpty(action))
                {
                    throw new ArgumentNullException(GetName(action));
                }

                if (insertFeatureClass == null)
                {
                    throw new ArgumentNullException(GetName(insertFeatureClass));
                }

                if (featureGeometry == null)
                {
                    throw new ArgumentNullException(GetName(featureGeometry));
                }

                if (osmPointFeatureClass == null)
                {
                    throw new ArgumentNullException(GetName(osmPointFeatureClass));
                }

                if (pointFieldIndexes == null)
                {
                    throw new ArgumentNullException(GetName(pointFieldIndexes));
                }

                if (availableDomains == null)
                {
                    throw new ArgumentNullException(GetName(availableDomains));
                }

                if (wayFieldIndexes == null)
                {
                    throw new ArgumentNullException(GetName(wayFieldIndexes));
                }

                if (message == null)
                {
                    throw new ArgumentNullException(GetName(message));
                }

                int osmPointIDFieldIndex = (int)pointFieldIndexes.GetProperty("osmIDFieldIndex");
                int osmWayRefCountFieldIndex = (int)pointFieldIndexes.GetProperty("osmWayRefCountFieldIndex");
                Dictionary<string, int> osmDomainAttributeFieldIndices = (Dictionary<string, int>)wayFieldIndexes.GetProperty("domainAttributesFieldIndex");
                int osmIDFieldIndex = (int)wayFieldIndexes.GetProperty("osmIDFieldIndex");
                int tagCollectionFieldIndex = (int)wayFieldIndexes.GetProperty("osmTagsFieldIndex");
                int osmSupportingElementFieldIndex = (int)wayFieldIndexes.GetProperty("osmSupportingElementFieldIndex");
                int osmUserFieldIndex = (int)wayFieldIndexes.GetProperty("osmUserFieldIndex");
                int osmUIDFieldIndex = (int)wayFieldIndexes.GetProperty("osmUIDFieldIndex");
                int osmVisibleFieldIndex = (int)wayFieldIndexes.GetProperty("osmVisibleFieldIndex");
                int osmVersionFieldIndex = (int)wayFieldIndexes.GetProperty("osmVersionFieldIndex");
                int osmChangesetFieldIndex = (int)wayFieldIndexes.GetProperty("osmChangeSetFieldIndex");
                int osmTimeStampFieldIndex = (int)wayFieldIndexes.GetProperty("osmTimeStampFieldIndex");
                int osmTrackChangesFieldIndex = (int)wayFieldIndexes.GetProperty("osmTrackChangesFieldIndex");

                if (filterGeometry != null)
                {
                    IRelationalOperator relationalOperator = filterGeometry as IRelationalOperator;
                    if (relationalOperator.Contains(featureGeometry) == false)
                    {
                        // in this case the insert geometry is not part of the current area of interest
                        // skip the insertion process
                        return;
                    }
                }

                switch (action)
                {
                    case "create":
                        //newWayFeature = insertFeatureClass.CreateFeature();
                        updateCursor = insertFeatureClass.Insert(true);
                        comReleaser.ManageLifetime(updateCursor);
                        newWayFeature = insertFeatureClass.CreateFeatureBuffer() as IFeature;
                        break;
                    case "modify":
                        //newWayFeature = getOSMRow((ITable)insertFeatureClass, currentWay.id) as IFeature;
                        IQueryFilter queryFilter = new QueryFilterClass();
                        queryFilter.WhereClause = insertFeatureClass.WhereClauseByExtensionVersion(currentWay.id, "OSMID", extensionVersion);
                        comReleaser.ManageLifetime(queryFilter);
                        updateCursor = insertFeatureClass.Update(queryFilter, false);
                        comReleaser.ManageLifetime(updateCursor);
                        newWayFeature = updateCursor.NextFeature();
                        break;
                    default:
                        break;
                }

                // sanity check 
                if (newWayFeature == null)
                {
                    if (insertFeatureClass.ShapeType == esriGeometryType.esriGeometryPolyline)
                    {
                        message.AddWarning(String.Format(resourceManager.GetString("GPTools_OSMGPDiffLoader_wayskipped_noline"), currentWay.id));
                    }
                    else if (insertFeatureClass.ShapeType == esriGeometryType.esriGeometryPolygon)
                    {
                        message.AddWarning(String.Format(resourceManager.GetString("GPTools_OSMGPDiffLoader_wayskipped_nopolygon"), currentWay.id));
                    }
                    return;
                }

                // assign the geometry to the either the new or the existing feature
                newWayFeature.Shape = featureGeometry;


                if (osmIDFieldIndex > -1)
                {
                    if (extensionVersion == 1)
                        newWayFeature.set_Value(osmIDFieldIndex, Convert.ToInt32(currentWay.id));
                    else if (extensionVersion == 2)
                        newWayFeature.set_Value(osmIDFieldIndex, Convert.ToString(currentWay.id));
                }

                if (currentWay.tag != null)
                {
                    foreach (domain domainItem in availableDomains.domain)
                    {
                        foreach (tag tagItem in currentWay.tag)
                        {
                            if (tagItem.k == domainItem.name)
                            {
                                if (osmDomainAttributeFieldIndices.ContainsKey(domainItem.name))
                                {
                                    if (tagItem.v.Length <= domainFieldLength)
                                    {
                                        newWayFeature.set_Value(osmDomainAttributeFieldIndices[domainItem.name], tagItem.v);
                                    }
                                }
                            }
                        }
                    }

                    if (tagCollectionFieldIndex != -1)
                    {
                        _osmUtility.insertOSMTags(tagCollectionFieldIndex, newWayFeature, currentWay.tag);
                    }
                }
                else
                {
                    // remember to null the domain attribute fields on the buffer as well
                    foreach (domain domainItem in availableDomains.domain)
                    {
                        if (osmDomainAttributeFieldIndices.ContainsKey(domainItem.name))
                        {
                            newWayFeature.set_Value(osmDomainAttributeFieldIndices[domainItem.name], DBNull.Value);
                        }
                    }

                    _osmUtility.insertOSMTags(tagCollectionFieldIndex, newWayFeature, new tag[0]);
                }

                // store the administrative attributes
                // user, uid, version, changeset, timestamp, visible
                if (!String.IsNullOrEmpty(currentWay.user))
                {
                    if (osmUserFieldIndex != -1)
                    {
                        newWayFeature.set_Value(osmUserFieldIndex, currentWay.user);
                    }
                }

                if (!String.IsNullOrEmpty(currentWay.uid))
                {
                    if (osmUIDFieldIndex != -1)
                    {
                        newWayFeature.set_Value(osmUIDFieldIndex, Convert.ToInt64(currentWay.uid));
                    }
                }

                if (osmVisibleFieldIndex != -1)
                {
                    newWayFeature.set_Value(osmVisibleFieldIndex, currentWay.visible.ToString());
                }

                if (!String.IsNullOrEmpty(currentWay.version))
                {
                    if (osmVersionFieldIndex != -1)
                    {
                        newWayFeature.set_Value(osmVersionFieldIndex, Convert.ToInt32(currentWay.version));
                    }
                }

                if (!String.IsNullOrEmpty(currentWay.changeset))
                {
                    if (osmChangesetFieldIndex != -1)
                    {
                        newWayFeature.set_Value(osmChangesetFieldIndex, Convert.ToInt64(currentWay.changeset));
                    }
                }

                if (!String.IsNullOrEmpty(currentWay.timestamp))
                {
                    if (osmTimeStampFieldIndex != -1)
                    {
                        newWayFeature.set_Value(osmTimeStampFieldIndex, Convert.ToDateTime(currentWay.timestamp));
                    }
                }

                if (osmSupportingElementFieldIndex > -1)
                {
                    newWayFeature.set_Value(osmSupportingElementFieldIndex, "no");
                }


                if (osmTrackChangesFieldIndex > -1)
                {
                    // indicate to bypass the OSM logging behavior
                    newWayFeature.set_Value(osmTrackChangesFieldIndex, 1);
                }


                switch (action)
                {
                    case "create":
                        //newWayFeature.Store();
                        updateCursor.InsertFeature((IFeatureBuffer)newWayFeature);
                        break;
                    case "modify":
                        updateCursor.UpdateFeature(newWayFeature);
                        break;
                    default:
                        break;
                }


            }
            catch (Exception ex)
            {
                message.AddWarning(ex.Message);
                message.AddWarning(ex.StackTrace);
            }
            finally
            {
                if (updateCursor != null)
                    Marshal.ReleaseComObject(updateCursor);

                if (newWayFeature != null)
                    Marshal.ReleaseComObject(newWayFeature);
            }
        }

        private static IGeometry extractGeometryFromOSMFeature(relation currentRelation, esriGeometryType detectedGeometry, IFeatureClass lineFeatureClass, IFeatureClass polygonFeatureClass, int extensionVersion)
        {
            IGeometry featureGeometry = null;
            ISegmentCollection relationGeometryCollection = null;
            OSMToolHelper osmToolHelper = new OSMToolHelper();

            if (detectedGeometry == esriGeometryType.esriGeometryPolygon)
            {
                IPolygon relationMPPolygon = new PolygonClass();
                relationMPPolygon.SpatialReference = ((IGeoDataset)polygonFeatureClass).SpatialReference;

                relationGeometryCollection = relationMPPolygon as ISegmentCollection;
            }
            else if (detectedGeometry == esriGeometryType.esriGeometryPolyline)
            {
                IPolyline relationMPPolyline = new PolylineClass();
                relationMPPolyline.SpatialReference = ((IGeoDataset)lineFeatureClass).SpatialReference;

                relationGeometryCollection = relationMPPolyline as ISegmentCollection;
            }
            else
            {
                return featureGeometry;
            }


            IQueryFilter osmIDQueryFilter = new QueryFilterClass();
            string sqlPolyOSMID = polygonFeatureClass.SqlIdentifier("OSMID");
            string sqlLineOSMID = lineFeatureClass.SqlIdentifier("OSMID");
            object missing = Type.Missing;

            foreach (var item in currentRelation.Items)
            {

                if (item is member)
                {
                    member memberItem = item as member;
                    switch (memberItem.type)
                    {
                        case memberType.way:
                        case memberType.relation:
                            bool isPolyline = true;

                            if (detectedGeometry == esriGeometryType.esriGeometryPolygon)
                            {
                                isPolyline = osmToolHelper.IsThisWayALine(memberItem.@ref, lineFeatureClass, sqlLineOSMID, polygonFeatureClass, sqlPolyOSMID);
                            }

                            if (isPolyline == true)
                            {
                                osmIDQueryFilter.WhereClause = ((ITable)lineFeatureClass).WhereClauseByExtensionVersion(memberItem.@ref, "OSMID", extensionVersion);
                                osmIDQueryFilter.SubFields = lineFeatureClass.OIDFieldName + "," + lineFeatureClass.ShapeFieldName;
                            }
                            else
                            {
                                osmIDQueryFilter.WhereClause = ((ITable)polygonFeatureClass).WhereClauseByExtensionVersion(memberItem.@ref, "OSMID", extensionVersion);
                                osmIDQueryFilter.SubFields = polygonFeatureClass.OIDFieldName + "," + polygonFeatureClass.ShapeFieldName;
                            }


                            using (ComReleaser relationComReleaser = new ComReleaser())
                            {
                                IFeatureCursor featureCursor = null;
                                if (isPolyline == true)
                                {
                                    featureCursor = lineFeatureClass.Search(osmIDQueryFilter, false);
                                }
                                else
                                {
                                    featureCursor = polygonFeatureClass.Search(osmIDQueryFilter, false);
                                }

                                relationComReleaser.ManageLifetime(featureCursor);

                                IFeature partFeature = featureCursor.NextFeature();

                                // set the appropriate field attribute to become invisible as a standalone features
                                if (partFeature != null)
                                {
                                    IGeometryCollection ringCollection = partFeature.Shape as IGeometryCollection;

                                    // test for available content in the geometry collection  
                                    if (ringCollection.GeometryCount > 0)
                                    {
                                        // test if we dealing with a valid geometry
                                        if (ringCollection.get_Geometry(0).IsEmpty == false)
                                        {
                                            // add it to the new geometry and mark the added geometry as a supporting element
                                            relationGeometryCollection.AddSegmentCollection((ISegmentCollection)ringCollection.get_Geometry(0));

                                        }
                                    }
                                }
                            }
                            break;
                        default:
                            break;
                    }
                }
            }

            return featureGeometry;
        }

        private static IGeometry extractGeometryFromOSMFeature(way currentWay, IFeatureClass insertFeatureClass, IFeatureClass osmPointFeatureClass, int osmPointIDFieldIndex, int extensionVersion, ComReleaser comReleaser)
        {
            IGeometry featureGeometry = null;
            IPointCollection wayPointCollection = null;
            IQueryFilter osmIDQueryFilter = new QueryFilterClass();
            IFeatureCursor searchPointCursor = null;
            OSMToolHelper osmToolHelper = new OSMToolHelper();


            if (currentWay == null)
                return featureGeometry;

            if (currentWay.nd == null)
                return featureGeometry;

            Regex regex = new Regex(@"\d");

            //try
            //{
                string sqlPointOSMID = osmPointFeatureClass.SqlIdentifier("OSMID");

                if (insertFeatureClass.ShapeType == esriGeometryType.esriGeometryPolyline)
                {
                    IPolyline wayPolyline = new PolylineClass();

                    IPointIDAware polylineIDAware = wayPolyline as IPointIDAware;
                    polylineIDAware.PointIDAware = true;

                    wayPointCollection = wayPolyline as IPointCollection;
                    for (int pointIndex = 0; pointIndex < currentWay.nd.Length; pointIndex++)
                    {
                        wayPointCollection.AddPoint(new PointClass());
                    }

                    List<string> idRequests = osmToolHelper.SplitOSMIDRequests(currentWay, extensionVersion);

                    // build a list of node ids we can use to determine the point index in the line geometry
                    // as well as a dictionary to determine the position in the list in case of duplicates nodes
                    Dictionary<string, int> nodePositionDictionary = new Dictionary<string, int>(currentWay.nd.Length);
                    List<string> nodeIDs = new List<string>(currentWay.nd.Length);

                    foreach (nd wayNode in currentWay.nd)
                    {
                        nodeIDs.Add(wayNode.@ref);

                        if (nodePositionDictionary.ContainsKey(wayNode.@ref) == false)
                        {
                            nodePositionDictionary.Add(wayNode.@ref, 0);
                        }
                    }

                    foreach (string request in idRequests)
                    {
                        string tempRequest = request;

                        osmIDQueryFilter.WhereClause = sqlPointOSMID + " IN " + request;
                        osmIDQueryFilter.SubFields = osmPointFeatureClass.OIDFieldName + ",OSMID," + osmPointFeatureClass.ShapeFieldName;

                        //using (ComReleaser comReleaser = new ComReleaser())
                        //{
                            searchPointCursor = osmPointFeatureClass.Search(osmIDQueryFilter, false);
                            comReleaser.ManageLifetime(searchPointCursor);

                            IFeature nodeFeature = searchPointCursor.NextFeature();

                            while (nodeFeature != null)
                            {
                                // determine the index of the point in with respect to the node position
                                if (osmPointIDFieldIndex > -1)
                                {
                                    string nodeOSMIDString = Convert.ToString(nodeFeature.get_Value(osmPointIDFieldIndex));
                                    int nodePositionIndex = nodeIDs.IndexOf(nodeOSMIDString, nodePositionDictionary[nodeOSMIDString]);

                                    // update the new position start search index
                                    if ((nodePositionIndex + 1) < nodeIDs.Count)
                                    {
                                        nodePositionDictionary[nodeOSMIDString] = nodePositionIndex + 1;
                                    }

                                    IPoint currentPoint = nodeFeature.Shape as IPoint;
                                    currentPoint.ID = nodeFeature.OID;

                                    wayPointCollection.UpdatePoint(nodePositionIndex, currentPoint);

                                    // remove the current osmid from the request string to indicate that we handled this feature
                                    tempRequest = tempRequest.Replace(nodeOSMIDString, "");

                                    nodeFeature = searchPointCursor.NextFeature();
                                }
                            }
                        //}

                        if (regex.IsMatch(tempRequest))
                        {
                            // if we still find a number in the request string then is way OSM feature contains a node that is currently not 
                            // in the point feature class
                            // if this is the case then let's abandon the geometry creation as opposed to creating invalid geometry due to missing nodes
                            return null;
                        }
                    }

                    featureGeometry = wayPolyline;
                }
                else
                {
                    IPolygon wayPolygon = new PolygonClass();
                    wayPolygon.SpatialReference = ((IGeoDataset)insertFeatureClass).SpatialReference;

                    IPointIDAware polygonIDAware = wayPolygon as IPointIDAware;
                    polygonIDAware.PointIDAware = true;

                    wayPointCollection = wayPolygon as IPointCollection;

                    // populate the point collection with the number of nodes
                    for (int pointIndex = 0; pointIndex < currentWay.nd.Length; pointIndex++)
                    {
                        wayPointCollection.AddPoint(new PointClass());
                    }

                    List<string> idRequests = osmToolHelper.SplitOSMIDRequests(currentWay, extensionVersion);

                    // build a list of node ids we can use to determine the point index in the line geometry
                    // as well as a dictionary to determine the position in the list in case of duplicates nodes
                    Dictionary<string, int> nodePositionDictionary = new Dictionary<string, int>(currentWay.nd.Length);
                    List<string> nodeIDs = new List<string>(currentWay.nd.Length);

                    foreach (nd wayNode in currentWay.nd)
                    {
                        nodeIDs.Add(wayNode.@ref);

                        if (nodePositionDictionary.ContainsKey(wayNode.@ref) == false)
                        {
                            nodePositionDictionary.Add(wayNode.@ref, 0);
                        }
                    }

                    foreach (string osmIDRequest in idRequests)
                    {
                        string tempRequest = osmIDRequest;

                        //using (ComReleaser innercomReleaser = new ComReleaser())
                        //{
                            osmIDQueryFilter.WhereClause = sqlPointOSMID + " IN " + osmIDRequest;
                            osmIDQueryFilter.SubFields = osmPointFeatureClass.OIDFieldName + ",OSMID," + osmPointFeatureClass.ShapeFieldName;

                            searchPointCursor = osmPointFeatureClass.Update(osmIDQueryFilter, false);
                            comReleaser.ManageLifetime(searchPointCursor);

                            IFeature nodeFeature = searchPointCursor.NextFeature();

                            while (nodeFeature != null)
                            {
                                if (osmPointIDFieldIndex > -1)
                                {
                                    // determine the index of the point in with respect to the node position
                                    string nodeOSMIDString = Convert.ToString(nodeFeature.get_Value(osmPointIDFieldIndex));
                                    int nodePositionIndex = nodeIDs.IndexOf(nodeOSMIDString, nodePositionDictionary[nodeOSMIDString]);

                                    // update the new position start search index
                                    if ((nodePositionIndex + 1) < nodeIDs.Count)
                                    {
                                        nodePositionDictionary[nodeOSMIDString] = nodePositionIndex + 1;
                                    }

                                    IPoint currentPoint = nodeFeature.Shape as IPoint;
                                    currentPoint.ID = nodeFeature.OID;

                                    wayPointCollection.UpdatePoint(nodePositionIndex, currentPoint);

                                    //// increase the reference counter
                                    //if (osmWayRefCountFieldIndex != -1)
                                    //{
                                    //    nodeFeature.set_Value(osmWayRefCountFieldIndex, ((int)nodeFeature.get_Value(osmWayRefCountFieldIndex)) + 1);

                                    //    searchPointCursor.UpdateFeature(nodeFeature);
                                    //}

                                    // remove the current osmid from the request string to indicate that we handled this feature
                                    tempRequest = tempRequest.Replace(nodeOSMIDString, "");
                                }

                                nodeFeature = searchPointCursor.NextFeature();
                            }
                        //}

                        if (regex.IsMatch(tempRequest))
                        {
                            // if we still find a number in the request string then is way OSM feature contains a node that is currently not 
                            // in the point feature class
                            // if this is the case then let's abandon the geometry creation as opposed to creating invalid geometry due to missing nodes
                            return null;
                        }

                    }

                    // remove the last point as OSM considers them to be coincident
                    wayPointCollection.RemovePoints(wayPointCollection.PointCount - 1, 1);
                    ((IPolygon)wayPointCollection).Close();


                    featureGeometry = wayPointCollection as IGeometry;
                }


//            }
//            catch (Exception ex) 
//            {
//#if DEBUG
//                System.Diagnostics.Debug.WriteLine(ex.Message);
//                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
//#endif
//            }

            return featureGeometry;
        }

        private Dictionary<string, int> gatherLocalOSMEdits(ITable revisionTable, string osmElementType)
        {
            Dictionary<string, int> capturedLocalEdits = new Dictionary<string, int>();

            try
            {
                int osmRevisionNewIDFieldIndex = revisionTable.FindField("osmnewid");
                int osmRevisionVersionFieldIndex = revisionTable.FindField("osmversion");

                IQueryFilter queryFilter = new QueryFilterClass();

                // node all the nodes 
                using (ComReleaser comReleaser = new ComReleaser())
                {
                    SQLFormatter sqlFormatter = new SQLFormatter(revisionTable);

                    queryFilter.WhereClause = sqlFormatter.SqlIdentifier("osmelementtype") + " = '" + osmElementType 
                        + "' AND NOT " + sqlFormatter.SqlIdentifier("osmnewid") + "IS NULL";
                    queryFilter.SubFields = "osmversion,osmnewid";

                    ICursor nodeCursor = revisionTable.Search(queryFilter, false);
                    comReleaser.ManageLifetime(nodeCursor);

                    IRow nodeRow = null;

                    while ((nodeRow = nodeCursor.NextRow()) != null)
                    {
                        if ((osmRevisionVersionFieldIndex > -1) && (osmRevisionNewIDFieldIndex > -1))
                        {
                            try
                            {
                                int rowVersion = Convert.ToInt32(nodeRow.get_Value(osmRevisionVersionFieldIndex));
                                string rowOSMID = Convert.ToString(nodeRow.get_Value(osmRevisionNewIDFieldIndex));

                                if (capturedLocalEdits.ContainsKey(rowOSMID))
                                {
                                    // the ID already exists make sure that we are recording the highest available version number
                                    if (capturedLocalEdits[rowOSMID] < rowVersion)
                                    {
                                        capturedLocalEdits[rowOSMID] = rowVersion;
                                    }
                                }
                                else
                                {
                                    // add a new entry of the given type in the dictionary
                                    capturedLocalEdits.Add(rowOSMID, rowVersion);
                                }
                            }
                            catch { }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
            }

            return capturedLocalEdits;
        }

        private ITable findTable(IWorkspace osmWorkspace, string baseName, string tableType)
        {
            ITable foundTable = null;

            try
            {
                IFeatureWorkspace osmFeatureWorkspace = osmWorkspace as IFeatureWorkspace;

                switch (tableType)
                {
                    case "revision":
                        string revisionTableName = baseName + "_osm_revision";
                        foundTable = osmFeatureWorkspace.OpenTable(revisionTableName);
                        break;
                    case "relation":
                        string relationTableName = baseName + "_osm_relation";
                        foundTable = osmFeatureWorkspace.OpenTable(relationTableName);
                        break;
                    default:
                        break;
                }
            }
            catch
            {
            }

            return foundTable;
        }

        private IFeatureClass findFeatureClass(IWorkspace osmWorkspace, string baseName, esriGeometryType fcGeometryType)
        {
            IFeatureClass foundFeatureClass = null;

            try
            {
                IFeatureWorkspace osmFeatureWorkspace = osmWorkspace as IFeatureWorkspace;
                IFeatureClassContainer osmFeatureClassContainer = osmFeatureWorkspace.OpenFeatureDataset(baseName) as IFeatureClassContainer; 

                switch (fcGeometryType)
                {
                    case esriGeometryType.esriGeometryAny:
                        break;
                    case esriGeometryType.esriGeometryBag:
                        break;
                    case esriGeometryType.esriGeometryBezier3Curve:
                        break;
                    case esriGeometryType.esriGeometryCircularArc:
                        break;
                    case esriGeometryType.esriGeometryEllipticArc:
                        break;
                    case esriGeometryType.esriGeometryEnvelope:
                        break;
                    case esriGeometryType.esriGeometryLine:
                        break;
                    case esriGeometryType.esriGeometryMultiPatch:
                        break;
                    case esriGeometryType.esriGeometryMultipoint:
                        break;
                    case esriGeometryType.esriGeometryNull:
                        break;
                    case esriGeometryType.esriGeometryPath:
                        break;
                    case esriGeometryType.esriGeometryPoint:
                        string pointFeatureClassName = baseName + "_osm_pt";
                        foundFeatureClass = osmFeatureClassContainer.get_ClassByName(pointFeatureClassName);

                        break;
                    case esriGeometryType.esriGeometryPolygon:
                        string polygonFeatureClassName = baseName + "_osm_ply";
                        foundFeatureClass = osmFeatureClassContainer.get_ClassByName(polygonFeatureClassName);
                        break;
                    case esriGeometryType.esriGeometryPolyline:
                        string polylineFeatureClassName = baseName + "_osm_ln";
                        foundFeatureClass = osmFeatureClassContainer.get_ClassByName(polylineFeatureClassName);

                        break;
                    case esriGeometryType.esriGeometryRay:
                        break;
                    case esriGeometryType.esriGeometryRing:
                        break;
                    case esriGeometryType.esriGeometrySphere:
                        break;
                    case esriGeometryType.esriGeometryTriangleFan:
                        break;
                    case esriGeometryType.esriGeometryTriangleStrip:
                        break;
                    case esriGeometryType.esriGeometryTriangles:
                        break;
                    default:
                        break;
                }
            }
            catch
            {
            }

            return foundFeatureClass;
        }

        private bool elementNeedsUpdate(object osmObject, Dictionary<string, int> localNodeEdits, Dictionary<string, int> localWayEdits, Dictionary<string, int> localRelationEdits)
        {
            // 
            bool needsUpdate = false;

            try
            {
                if (osmObject is node)
                {
                    node nodeToTest = osmObject as node;

                    string nodeID = nodeToTest.id;
                    int nodeVersion = Convert.ToInt32(nodeToTest.version);

                    if (localNodeEdits.ContainsKey(nodeID))
                    {
                        if (localNodeEdits[nodeID] < nodeVersion)
                        {
                            needsUpdate = true;
                        }

                        // ?? delete the entries for this ID from the revision table ??
                    }
                    else
                    {
                        needsUpdate = true;
                    }
                }
                else if (osmObject is way)
                {
                    way wayToTest = osmObject as way;

                    string wayID = wayToTest.id;
                    int wayVersion = Convert.ToInt32(wayToTest.version);

                    if (localWayEdits.ContainsKey(wayID))
                    {
                        if (localWayEdits[wayID] < wayVersion)
                        {
                            needsUpdate = true;
                        }

                        // ?? delete the entries for this ID from the revision table ??
                    }
                    else
                    {
                        needsUpdate = true;
                    }
                }
                else if (osmObject is relation)
                {
                    relation relationToTest = osmObject as relation;

                    string relationID = relationToTest.id;
                    int relationVersion = Convert.ToInt32(relationToTest.version);

                    if (localRelationEdits.ContainsKey(relationID))
                    {
                        if (localRelationEdits[relationID] < relationVersion)
                        {
                            needsUpdate = true;
                        }

                        // ?? delete the entries for this ID from the revision table ??
                    }
                    else
                    {
                        needsUpdate = true;
                    }

                }
            }
            
            catch { }
            return needsUpdate;
        }

        private void manipulateNodes(Dictionary<string, node> nodes, string action, IGeometry filterGeometry, IFeatureClass pointFeatureClass, OSMDomains availableDomains, int domainFieldLength, IPropertySet pointFieldIndexes, ref IGPMessages message, int extensionVersion, ComReleaser comReleaser, bool useVerboseLogging, ref ITrackCancel cancelTracker)
        {
            Dictionary<string, KeyValuePair<node, IPoint>> nodesInAOI = new Dictionary<string,KeyValuePair<node, IPoint>>();

            try
            {
                OSMToolHelper osmToolHelper = new OSMToolHelper();

                IRelationalOperator relationalOperator = filterGeometry as IRelationalOperator;

                foreach (KeyValuePair<string, node> item in nodes)
                {
                    if (cancelTracker.Continue() == false)
                        return;

                    IPoint pointGeometry = new PointClass();
                    pointGeometry.X = Convert.ToDouble(item.Value.lon, new CultureInfo("en-US"));
                    pointGeometry.Y = Convert.ToDouble(item.Value.lat, new CultureInfo("en-US"));
                    pointGeometry.SpatialReference = m_wgs84;

                    // if we have a filter geometry check if the newly created point is inside or outside the AOI 
                    if (filterGeometry != null)
                    {
                        pointGeometry.Project(filterGeometry.SpatialReference);

                        if (relationalOperator.Contains(pointGeometry))
                        {
                            // if the node is contained in the AOI - filterGeometry, let's keep if for further processing
                            nodesInAOI.Add(item.Key, new KeyValuePair<node, IPoint>(item.Value, pointGeometry));
                        }
                    }
                    else
                    {
                        nodesInAOI.Add(item.Key, new KeyValuePair<node, IPoint>(item.Value, pointGeometry));
                    }
                }

                // if all the nodes are outside our area then there is nothing to do
                if (nodesInAOI.Count == 0)
                    return;

                IFeature pointFeature = null;
                IFeatureCursor updateCursor = null;

                int osmPointIDFieldIndex = (int)pointFieldIndexes.GetProperty("osmIDFieldIndex");
                int tagCollectionPointFieldIndex = (int)pointFieldIndexes.GetProperty("osmTagsFieldIndex");
                Dictionary<string, int> osmPointDomainAttributeFieldIndices = (Dictionary<string, int>)pointFieldIndexes.GetProperty("domainAttributesFieldIndex");
                int osmSupportingElementPointFieldIndex = (int)pointFieldIndexes.GetProperty("osmSupportingElementFieldIndex");
                int osmWayRefCountFieldIndex = (int)pointFieldIndexes.GetProperty("osmWayRefCountFieldIndex");
                int osmUserPointFieldIndex = (int)pointFieldIndexes.GetProperty("osmUserFieldIndex");
                int osmUIDPointFieldIndex = (int)pointFieldIndexes.GetProperty("osmUIDFieldIndex");
                int osmVisiblePointFieldIndex = (int)pointFieldIndexes.GetProperty("osmVisibleFieldIndex");
                int osmVersionPointFieldIndex = (int)pointFieldIndexes.GetProperty("osmVersionFieldIndex");
                int osmChangesetPointFieldIndex = (int)pointFieldIndexes.GetProperty("osmChangeSetFieldIndex");
                int osmTimeStampPointFieldIndex = (int)pointFieldIndexes.GetProperty("osmTimeStampFieldIndex");
                int osmTrackChangesFieldIndex = (int)pointFieldIndexes.GetProperty("osmTrackChangesFieldIndex");

                string sqlPointOSMID = pointFeatureClass.SqlIdentifier("OSMID");

                switch (action)
                {
                    case "create_modify":
                        List<node> nodesToCheck = nodesInAOI.Values.Select(kp => kp.Key).ToList();
                        List<string> whereClauses = osmToolHelper.SplitOSMIDRequests(nodesToCheck, extensionVersion);

                        IQueryFilter queryFilter = new QueryFilterClass();
                        comReleaser.ManageLifetime(queryFilter);
                        queryFilter.SubFields = String.Join(",", pointFeatureClass.ExtractRequiredFields());

                        foreach (string whereClause in whereClauses)
                        {
                            if (cancelTracker.Continue() == false)
                                return;

                            queryFilter.WhereClause = sqlPointOSMID + " IN " + whereClause;
                            updateCursor = pointFeatureClass.Update(queryFilter, false);
                            comReleaser.ManageLifetime(updateCursor);
                            pointFeature = updateCursor.NextFeature();

                            while (pointFeature != null)
                            {
                                if (osmPointIDFieldIndex > -1)
                                {
                                    string nodeID = pointFeature.get_Value(osmPointIDFieldIndex) as string;
                                    if (nodesInAOI.ContainsKey(nodeID))
                                    {
                                        KeyValuePair<node, IPoint> currentNode = nodesInAOI[nodeID];

                                        manipulatePointFeature(currentNode.Key, ref pointFeature, availableDomains, domainFieldLength, extensionVersion, currentNode.Value, osmPointIDFieldIndex, tagCollectionPointFieldIndex, osmPointDomainAttributeFieldIndices, osmSupportingElementPointFieldIndex, osmWayRefCountFieldIndex, osmUserPointFieldIndex, osmUIDPointFieldIndex, osmVisiblePointFieldIndex, osmVersionPointFieldIndex, osmChangesetPointFieldIndex, osmTimeStampPointFieldIndex, osmTrackChangesFieldIndex);

                                        // remove the current node 
                                        nodesInAOI.Remove(nodeID);

                                        updateCursor.UpdateFeature(pointFeature);

                                        if (useVerboseLogging == true)
                                        {
                                            message.AddMessage(String.Format(resourceManager.GetString("GPTools_OSMGPDiffLoader_modifymessage"), resourceManager.GetString("GPTools_OSM_node"), currentNode.Key.id));
                                        }
                                    }
                                }

                                pointFeature = updateCursor.NextFeature();
                            }
                        }

                        // at this point we should have only points left that need to be created
                        updateCursor = pointFeatureClass.Insert(true);
                        comReleaser.ManageLifetime(updateCursor);

                        foreach (var item in nodesInAOI)
                        {
                            if (cancelTracker.Continue() == false)
                                return;


                            pointFeature = pointFeatureClass.CreateFeatureBuffer() as IFeature;

                            KeyValuePair<node, IPoint> currentNode = item.Value;

                            manipulatePointFeature(currentNode.Key, ref pointFeature, availableDomains, domainFieldLength, extensionVersion, currentNode.Value, osmPointIDFieldIndex, tagCollectionPointFieldIndex, osmPointDomainAttributeFieldIndices, osmSupportingElementPointFieldIndex, osmWayRefCountFieldIndex, osmUserPointFieldIndex, osmUIDPointFieldIndex, osmVisiblePointFieldIndex, osmVersionPointFieldIndex, osmChangesetPointFieldIndex, osmTimeStampPointFieldIndex, osmTrackChangesFieldIndex);

                            updateCursor.InsertFeature((IFeatureBuffer)pointFeature);

                            if (useVerboseLogging == true)
                            {
                                message.AddMessage(String.Format(resourceManager.GetString("GPTools_OSMGPDiffLoader_createmessage"), resourceManager.GetString("GPTools_OSM_node"), currentNode.Key.id));
                            }
                        }

                        break;
                    case "delete":

                        List<node> nodesToDelete = nodesInAOI.Values.Select(kp => kp.Key).ToList();
                        whereClauses = osmToolHelper.SplitOSMIDRequests(nodesToDelete, extensionVersion);


                        IFeatureCursor deleteCursor = null;
                        IFeature deleteFeature = null;

                        queryFilter = new QueryFilterClass();
                        comReleaser.ManageLifetime(queryFilter);

                        foreach (string whereClause in whereClauses)
                        {
                            if (cancelTracker.Continue() == false)
                                return;

                            queryFilter.WhereClause = sqlPointOSMID + " IN " + whereClause;

                            deleteCursor = pointFeatureClass.Update(queryFilter, false);
                            deleteFeature = deleteCursor.NextFeature();

                            while (deleteFeature != null)
                            {
                                if (cancelTracker.Continue() == false)
                                    return;

                                string elementID = String.Empty;
                                if (osmPointIDFieldIndex > -1)
                                {
                                    elementID = Convert.ToString(deleteFeature.get_Value(osmPointIDFieldIndex));
                                }

                                if (useVerboseLogging == true)
                                {
                                    message.AddMessage(String.Format(resourceManager.GetString("GPTools_OSMGPDiffLoader_deletemessage"), resourceManager.GetString("GPTools_OSM_node"), elementID));
                                }

                                deleteFeature.Delete();

                                deleteFeature = deleteCursor.NextFeature();
                            }
                        }
                        break;
                    default:
                        break;
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine(ex.Message);
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
#endif
            }
        }

        private void insertNode(node createNode, string action, IGeometry filterGeometry, IFeatureClass pointFeatureClass, OSMDomains availableDomains, int domainFieldLength, IPropertySet pointFieldIndexes, ref IGPMessages message, int extensionVersion, ComReleaser comReleaser)
        {
            try
            {
                OSMToolHelper osmToolHelper = new OSMToolHelper();

                IPoint pointGeometry = new PointClass();
                pointGeometry.X = Convert.ToDouble(createNode.lon, new CultureInfo("en-US"));
                pointGeometry.Y = Convert.ToDouble(createNode.lat, new CultureInfo("en-US"));
                pointGeometry.SpatialReference = m_wgs84;

                // if we have a filter geometry check if the newly created point is inside or outside the AOI 
                if (filterGeometry != null)
                {
                    pointGeometry.Project(filterGeometry.SpatialReference);

                    IRelationalOperator relationalOperator = filterGeometry as IRelationalOperator;

                    if (relationalOperator.Contains(pointGeometry) == false)
                    {
                        return;
                    }
                }

                IFeature pointFeature = null;
                IFeatureCursor updateCursor = null;

                switch (action)
                {
                    case "create":
                        updateCursor = pointFeatureClass.Insert(true);
                        comReleaser.ManageLifetime(updateCursor);
                        pointFeature = pointFeatureClass.CreateFeatureBuffer() as IFeature;
                        //pointFeature = pointFeatureClass.CreateFeature();
                        break;
                    case "modify":
                        IQueryFilter queryFilter = new QueryFilterClass();
                        queryFilter.WhereClause = pointFeatureClass.WhereClauseByExtensionVersion(createNode.id, "OSMID", extensionVersion);
                        queryFilter.SubFields = String.Join(",", pointFeatureClass.ExtractRequiredFields());
                        comReleaser.ManageLifetime(queryFilter);
                        updateCursor = pointFeatureClass.Update(queryFilter, false);
                        comReleaser.ManageLifetime(updateCursor);
                        pointFeature = updateCursor.NextFeature();
                        //pointFeature = getOSMRow((ITable)pointFeatureClass, createNode.id) as IFeature;
                        break;
                    default:
                        break;
                }
                if (pointFeature == null)
                {
                    return;
                }

                int osmPointIDFieldIndex = (int)pointFieldIndexes.GetProperty("osmIDFieldIndex");
                int tagCollectionPointFieldIndex = (int)pointFieldIndexes.GetProperty("osmTagsFieldIndex");
                Dictionary<string, int> osmPointDomainAttributeFieldIndices = (Dictionary<string, int>)pointFieldIndexes.GetProperty("domainAttributesFieldIndex");
                int osmSupportingElementPointFieldIndex = (int)pointFieldIndexes.GetProperty("osmSupportingElementFieldIndex");
                int osmWayRefCountFieldIndex = (int) pointFieldIndexes.GetProperty("osmWayRefCountFieldIndex");
                int osmUserPointFieldIndex = (int) pointFieldIndexes.GetProperty("osmUserFieldIndex");
                int osmUIDPointFieldIndex = (int) pointFieldIndexes.GetProperty("osmUIDFieldIndex");
                int osmVisiblePointFieldIndex = (int) pointFieldIndexes.GetProperty("osmVisibleFieldIndex");
                int osmVersionPointFieldIndex = (int) pointFieldIndexes.GetProperty("osmVersionFieldIndex");
                int osmChangesetPointFieldIndex = (int) pointFieldIndexes.GetProperty("osmChangeSetFieldIndex");
                int osmTimeStampPointFieldIndex = (int) pointFieldIndexes.GetProperty("osmTimeStampFieldIndex");
                int osmTrackChangesFieldIndex = (int)pointFieldIndexes.GetProperty("osmTrackChangesFieldIndex");

                manipulatePointFeature(createNode, ref pointFeature, availableDomains, domainFieldLength, extensionVersion, pointGeometry, osmPointIDFieldIndex, tagCollectionPointFieldIndex, osmPointDomainAttributeFieldIndices, osmSupportingElementPointFieldIndex, osmWayRefCountFieldIndex, osmUserPointFieldIndex, osmUIDPointFieldIndex, osmVisiblePointFieldIndex, osmVersionPointFieldIndex, osmChangesetPointFieldIndex, osmTimeStampPointFieldIndex, osmTrackChangesFieldIndex);

                switch (action)
                {
                    case "create":
                        updateCursor.InsertFeature((IFeatureBuffer)pointFeature);
                        break;
                    case "modify":
                        updateCursor.UpdateFeature(pointFeature);
                        break;
                    default:
                        break;
                }


                Marshal.ReleaseComObject(pointGeometry);
                Marshal.ReleaseComObject(pointFeature);
            }
            catch (Exception ex)
            {
                message.AddWarning(ex.Message + "___" + action);
                message.AddWarning(ex.StackTrace);
            }
        }

        private void manipulatePointFeature(node createNode, ref IFeature pointFeature, OSMDomains availableDomains, int domainFieldLength, int extensionVersion, IPoint pointGeometry, int osmPointIDFieldIndex, int tagCollectionPointFieldIndex, Dictionary<string, int> osmPointDomainAttributeFieldIndices, int osmSupportingElementPointFieldIndex, int osmWayRefCountFieldIndex, int osmUserPointFieldIndex, int osmUIDPointFieldIndex, int osmVisiblePointFieldIndex, int osmVersionPointFieldIndex, int osmChangesetPointFieldIndex, int osmTimeStampPointFieldIndex, int osmTrackChangesFieldIndex)
        {
            IPointIDAware idAware = pointGeometry as IPointIDAware;
            idAware.PointIDAware = true;


            pointGeometry.ID = pointFeature.OID;
            pointFeature.Shape = pointGeometry;

            if (osmPointIDFieldIndex > -1)
            {
                if (extensionVersion == 1)
                    pointFeature.set_Value(osmPointIDFieldIndex, Convert.ToInt32(createNode.id));
                else if (extensionVersion == 2)
                    pointFeature.set_Value(osmPointIDFieldIndex, createNode.id);
            }

            string isSupportingNode = "";
            if (_osmUtility.DoesHaveKeys(createNode.tag))
            {
                // if case it has tags I assume that the node presents an entity of it own,
                // hence it is not a supporting node in the context of supporting a way or relation
                isSupportingNode = "no";
            }
            else
            {
                // node has no tags -- at this point I assume that the absence of tags indicates that it is a supporting node
                // for a way or a relation
                isSupportingNode = "yes";

            }

            if (createNode.tag != null)
            {
                foreach (domain domainItem in availableDomains.domain)
                {
                    foreach (tag tagItem in createNode.tag)
                    {
                        if (tagItem.k.ToLower() == domainItem.name)
                        {
                            if (tagItem.v.Length <= domainFieldLength)
                            {
                                if (osmPointDomainAttributeFieldIndices.ContainsKey(tagItem.k))
                                {
                                    if (osmPointDomainAttributeFieldIndices[tagItem.k] > -1)
                                    {
                                        pointFeature.set_Value(osmPointDomainAttributeFieldIndices[tagItem.k], tagItem.v);
                                    }
                                }
                            }
                        }
                    }
                }

                if (tagCollectionPointFieldIndex > -1)
                {
                    _osmUtility.insertOSMTags(tagCollectionPointFieldIndex, pointFeature, createNode.tag);
                }
            }

            if (osmSupportingElementPointFieldIndex > -1)
            {
                pointFeature.set_Value(osmSupportingElementPointFieldIndex, isSupportingNode);
            }

            if (osmWayRefCountFieldIndex > -1)
            {
                pointFeature.set_Value(osmWayRefCountFieldIndex, 0);
            }

            // store the administrative attributes
            // user, uid, version, changeset, timestamp, visible
            if (osmUserPointFieldIndex > -1)
            {
                if (!String.IsNullOrEmpty(createNode.user))
                {
                    pointFeature.set_Value(osmUserPointFieldIndex, createNode.user);
                }
            }

            if (osmUIDPointFieldIndex > -1)
            {
                if (!String.IsNullOrEmpty(createNode.uid))
                {
                    pointFeature.set_Value(osmUIDPointFieldIndex, Convert.ToInt64(createNode.uid));
                }
            }

            if (osmVisiblePointFieldIndex > -1)
            {
                pointFeature.set_Value(osmVisiblePointFieldIndex, createNode.visible.ToString());
            }

            if (osmVersionPointFieldIndex > -1)
            {
                if (!String.IsNullOrEmpty(createNode.version))
                {
                    pointFeature.set_Value(osmVersionPointFieldIndex, Convert.ToInt32(createNode.version));
                }
            }

            if (osmChangesetPointFieldIndex > -1)
            {
                if (!String.IsNullOrEmpty(createNode.changeset))
                {
                    pointFeature.set_Value(osmChangesetPointFieldIndex, Convert.ToInt64(createNode.changeset));
                }
            }

            if (osmTimeStampPointFieldIndex > -1)
            {
                if (!String.IsNullOrEmpty(createNode.timestamp))
                {
                    pointFeature.set_Value(osmTimeStampPointFieldIndex, Convert.ToDateTime(createNode.timestamp));
                }
            }

            if (osmTrackChangesFieldIndex > -1)
            {
                // flag this feature not to be tracked by the class extension
                pointFeature.set_Value(osmTrackChangesFieldIndex, 1);
            }
        }

        private void manipulateLinearWayFeature(way createWay, ref IFeature editFeature, OSMDomains availableDomains, int domainFieldLength, int extensionVersion, IGeometry featureGeometry, int osmIDFieldIndex, int tagCollectionFieldIndex, Dictionary<string, int> osmDomainAttributeFieldIndices, int osmSupportingElementFieldIndex, int osmUserFieldIndex, int osmUIDFieldIndex, int osmVisibleFieldIndex, int osmVersionFieldIndex, int osmChangesetFieldIndex, int osmTimeStampFieldIndex, int osmTrackChangesFieldIndex)
        {
            try
            {
                editFeature.Shape = featureGeometry;

                if (osmIDFieldIndex > -1)
                {
                    if (extensionVersion == 1)
                        editFeature.set_Value(osmIDFieldIndex, Convert.ToInt32(createWay.id));
                    else if (extensionVersion == 2)
                        editFeature.set_Value(osmIDFieldIndex, createWay.id);
                }

                string isSupportingNode = "";
                if (_osmUtility.DoesHaveKeys(createWay.tag))
                {
                    // if case it has tags I assume that the node presents an entity of it own,
                    // hence it is not a supporting node in the context of supporting a way or relation
                    isSupportingNode = "no";
                }
                else
                {
                    // node has no tags -- at this point I assume that the absence of tags indicates that it is a supporting node
                    // for a way or a relation
                    isSupportingNode = "yes";

                }

                if (createWay.tag != null)
                {
                    foreach (domain domainItem in availableDomains.domain)
                    {
                        foreach (tag tagItem in createWay.tag)
                        {
                            if (tagItem.k.ToLower() == domainItem.name)
                            {
                                if (tagItem.v.Length <= domainFieldLength)
                                {
                                    if (osmDomainAttributeFieldIndices.ContainsKey(tagItem.k))
                                    {
                                        if (osmDomainAttributeFieldIndices[tagItem.k] > -1)
                                        {
                                            editFeature.set_Value(osmDomainAttributeFieldIndices[tagItem.k], tagItem.v);
                                        }
                                    }
                                }
                            }
                        }
                    }

                    if (tagCollectionFieldIndex > -1)
                    {
                        _osmUtility.insertOSMTags(tagCollectionFieldIndex, editFeature, createWay.tag);
                    }
                }

                if (osmSupportingElementFieldIndex > -1)
                {
                    editFeature.set_Value(osmSupportingElementFieldIndex, isSupportingNode);
                }

                // store the administrative attributes
                // user, uid, version, changeset, timestamp, visible
                if (osmUserFieldIndex > -1)
                {
                    if (!String.IsNullOrEmpty(createWay.user))
                    {
                        editFeature.set_Value(osmUserFieldIndex, createWay.user);
                    }
                }

                if (osmUIDFieldIndex > -1)
                {
                    if (!String.IsNullOrEmpty(createWay.uid))
                    {
                        editFeature.set_Value(osmUIDFieldIndex, Convert.ToInt64(createWay.uid));
                    }
                }

                if (osmVisibleFieldIndex > -1)
                {
                    editFeature.set_Value(osmVisibleFieldIndex, createWay.visible.ToString());
                }

                if (osmVersionFieldIndex > -1)
                {
                    if (!String.IsNullOrEmpty(createWay.version))
                    {
                        editFeature.set_Value(osmVersionFieldIndex, Convert.ToInt32(createWay.version));
                    }
                }

                if (osmChangesetFieldIndex > -1)
                {
                    if (!String.IsNullOrEmpty(createWay.changeset))
                    {
                        editFeature.set_Value(osmChangesetFieldIndex, Convert.ToInt64(createWay.changeset));
                    }
                }

                if (osmTimeStampFieldIndex > -1)
                {
                    if (!String.IsNullOrEmpty(createWay.timestamp))
                    {
                        editFeature.set_Value(osmTimeStampFieldIndex, Convert.ToDateTime(createWay.timestamp));
                    }
                }

                if (osmTrackChangesFieldIndex > -1)
                {
                    // flag this feature not to be tracked by the class extension
                    editFeature.set_Value(osmTrackChangesFieldIndex, 1);
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine(ex.Message);
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
#endif
            }
        }

        private void manipulateRelationFeature(relation createRelation, ref IFeature editFeature, OSMDomains availableDomains, int domainFieldLength, int extensionVersion, IGeometry featureGeometry, int osmIDFieldIndex, int tagCollectionFieldIndex, Dictionary<string, int> osmDomainAttributeFieldIndices, int osmSupportingElementFieldIndex, int osmUserFieldIndex, int osmUIDFieldIndex, int osmVisibleFieldIndex, int osmVersionFieldIndex, int osmChangesetFieldIndex, int osmTimeStampFieldIndex, int osmTrackChangesFieldIndex, int osmMembersFieldIndex)
        {
            // relations should always have items but just as a sanity check
            if (createRelation.Items == null)
            {
                return;
            }

            try
            {

                OSMToolHelper osmToolHelper = new OSMToolHelper();

                List<tag> relationTagList = new List<tag>();
                List<member> relationMemberList = new List<member>();

                // create a list of members and tags to be along in attribute fields for the multipart features
                foreach (var item in createRelation.Items)
                {
                    if (item is member)
                        relationMemberList.Add((member)item);
                    else if (item is tag)
                        relationTagList.Add((tag)item);
                }


                editFeature.Shape = featureGeometry;

                if (osmIDFieldIndex > -1)
                {
                    if (extensionVersion == 1)
                        editFeature.set_Value(osmIDFieldIndex, Convert.ToInt32(createRelation.id));
                    else if (extensionVersion == 2)
                        editFeature.set_Value(osmIDFieldIndex, createRelation.id);
                }

                string isSupportingNode = "";
                if (_osmUtility.DoesHaveKeys(createRelation))
                {
                    // if case it has tags I assume that the node presents an entity of it own,
                    // hence it is not a supporting node in the context of supporting a way or relation
                    isSupportingNode = "no";
                }
                else
                {
                    // node has no tags -- at this point I assume that the absence of tags indicates that it is a supporting node
                    // for a way or a relation
                    isSupportingNode = "yes";

                }

                if (relationTagList.Count > 0)
                {
                    foreach (domain domainItem in availableDomains.domain)
                    {
                        foreach (tag tagItem in relationTagList)
                        {
                            if (tagItem.k.ToLower() == domainItem.name)
                            {
                                if (tagItem.v.Length <= domainFieldLength)
                                {
                                    if (osmDomainAttributeFieldIndices.ContainsKey(tagItem.k))
                                    {
                                        if (osmDomainAttributeFieldIndices[tagItem.k] > -1)
                                        {
                                            editFeature.set_Value(osmDomainAttributeFieldIndices[tagItem.k], tagItem.v);
                                        }
                                    }
                                }
                            }
                        }
                    }

                    if (tagCollectionFieldIndex > -1)
                    {
                        // if the current relation doesn't have keys we merge the tags from the outer rings to the relation
                        if (!_osmUtility.DoesHaveKeys(createRelation))
                        {
                            IFeatureClass osmPolygonFeatureClass = editFeature.Table as IFeatureClass;
                            relationTagList = osmToolHelper.MergeTagsFromOuterPolygonToRelation(createRelation, osmPolygonFeatureClass);
                        }
                            

                        _osmUtility.insertOSMTags(tagCollectionFieldIndex, editFeature, relationTagList.ToArray());
                    }
                }
                else
                {
                    // if no tags exist we'll attempt to merge the tags from the outer rings to the relation
                    IFeatureClass osmPolygonFeatureClass = editFeature.Table as IFeatureClass;

                    if (osmPolygonFeatureClass != null)
                    {
                        relationTagList = osmToolHelper.MergeTagsFromOuterPolygonToRelation(createRelation, osmPolygonFeatureClass);

                        _osmUtility.insertOSMTags(tagCollectionFieldIndex, editFeature, relationTagList.ToArray());
                    }
                }

                if (osmSupportingElementFieldIndex > -1)
                {
                    editFeature.set_Value(osmSupportingElementFieldIndex, isSupportingNode);
                }

                if (osmMembersFieldIndex > -1)
                {
                    _osmUtility.insertMembers(osmMembersFieldIndex, editFeature, relationMemberList.ToArray());
                }

                // store the administrative attributes
                // user, uid, version, changeset, timestamp, visible
                if (osmUserFieldIndex > -1)
                {
                    if (!String.IsNullOrEmpty(createRelation.user))
                    {
                        editFeature.set_Value(osmUserFieldIndex, createRelation.user);
                    }
                }

                if (osmUIDFieldIndex > -1)
                {
                    if (!String.IsNullOrEmpty(createRelation.uid))
                    {
                        editFeature.set_Value(osmUIDFieldIndex, Convert.ToInt64(createRelation.uid));
                    }
                }

                if (osmVisibleFieldIndex > -1)
                {
                    editFeature.set_Value(osmVisibleFieldIndex, createRelation.visible.ToString());
                }

                if (osmVersionFieldIndex > -1)
                {
                    if (!String.IsNullOrEmpty(createRelation.version))
                    {
                        editFeature.set_Value(osmVersionFieldIndex, Convert.ToInt32(createRelation.version));
                    }
                }



                if (osmChangesetFieldIndex > -1)
                {
                    if (!String.IsNullOrEmpty(createRelation.changeset))
                    {
                        editFeature.set_Value(osmChangesetFieldIndex, Convert.ToInt64(createRelation.changeset));
                    }
                }

                if (osmTimeStampFieldIndex > -1)
                {
                    if (!String.IsNullOrEmpty(createRelation.timestamp))
                    {
                        editFeature.set_Value(osmTimeStampFieldIndex, Convert.ToDateTime(createRelation.timestamp));
                    }
                }

                if (osmTrackChangesFieldIndex > -1)
                {
                    // flag this feature not to be tracked by the class extension
                    editFeature.set_Value(osmTrackChangesFieldIndex, 1);
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine(ex.Message);
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
#endif
            }
        }

        private void manipulateRelationRow(relation createRelation, ref IRow editRow, int extensionVersion, int osmIDFieldIndex, int tagCollectionFieldIndex, int osmSupportingElementFieldIndex, int osmUserFieldIndex, int osmUIDFieldIndex, int osmVisibleFieldIndex, int osmVersionFieldIndex, int osmChangesetFieldIndex, int osmTimeStampFieldIndex, int osmMembersFieldIndex)
        {
            // relations should always have items but just as a sanity check
            if (createRelation.Items == null)
            {
                return;
            }

            try
            {

                OSMToolHelper osmToolHelper = new OSMToolHelper();

                List<tag> relationTagList = new List<tag>();
                List<member> relationMemberList = new List<member>();

                // create a list of members and tags to be along in attribute fields for the multipart features
                foreach (var item in createRelation.Items)
                {
                    if (item is member)
                        relationMemberList.Add((member)item);
                    else if (item is tag)
                        relationTagList.Add((tag)item);
                }


                if (osmIDFieldIndex > -1)
                {
                    if (extensionVersion == 1)
                        editRow.set_Value(osmIDFieldIndex, Convert.ToInt32(createRelation.id));
                    else if (extensionVersion == 2)
                        editRow.set_Value(osmIDFieldIndex, createRelation.id);
                }

                if (relationTagList.Count > 0)
                {

                    if (tagCollectionFieldIndex > -1)
                    {
                        _osmUtility.insertOSMTags(tagCollectionFieldIndex, editRow, relationTagList.ToArray());
                    }
                }

                if (osmMembersFieldIndex > -1)
                {
                    _osmUtility.insertMembers(osmMembersFieldIndex, editRow, relationMemberList.ToArray());
                }

                // store the administrative attributes
                // user, uid, version, changeset, timestamp, visible
                if (osmUserFieldIndex > -1)
                {
                    if (!String.IsNullOrEmpty(createRelation.user))
                    {
                        editRow.set_Value(osmUserFieldIndex, createRelation.user);
                    }
                }

                if (osmUIDFieldIndex > -1)
                {
                    if (!String.IsNullOrEmpty(createRelation.uid))
                    {
                        editRow.set_Value(osmUIDFieldIndex, Convert.ToInt64(createRelation.uid));
                    }
                }

                if (osmVisibleFieldIndex > -1)
                {
                    if (!String.IsNullOrEmpty(createRelation.visible))
                    {
                        editRow.set_Value(osmVisibleFieldIndex, createRelation.visible.ToString());
                    }
                }

                if (osmVersionFieldIndex > -1)
                {
                    if (!String.IsNullOrEmpty(createRelation.version))
                    {
                        editRow.set_Value(osmVersionFieldIndex, Convert.ToInt32(createRelation.version));
                    }
                }

                if (osmChangesetFieldIndex > -1)
                {
                    if (!String.IsNullOrEmpty(createRelation.changeset))
                    {
                        editRow.set_Value(osmChangesetFieldIndex, Convert.ToInt64(createRelation.changeset));
                    }
                }

                if (osmTimeStampFieldIndex > -1)
                {
                    if (!String.IsNullOrEmpty(createRelation.timestamp))
                    {
                        editRow.set_Value(osmTimeStampFieldIndex, Convert.ToDateTime(createRelation.timestamp));
                    }
                }

            }
            catch (Exception ex)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine(ex.Message);
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
#endif
            }
        }

    }
}
