// (c) Copyright Esri, 2010 - 2016
// This source is subject to the Apache 2.0 License.
// Please see http://www.apache.org/licenses/LICENSE-2.0.html for details.
// All other rights reserved.

using System;
using System.Resources;
using System.Runtime.InteropServices;
using ESRI.ArcGIS.DataSourcesFile;
using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geoprocessing;
using ESRI.ArcGIS.OSM.OSMClassExtension;

namespace ESRI.ArcGIS.OSM.GeoProcessing
{
    [Guid("780d241f-47a9-47dc-b981-3f51849c1949")]
    [ClassInterface(ClassInterfaceType.None)]
    [ProgId("OSMEditor.OSMGPCreateNetworkDataset")]
    public class OSMGPCreateNetworkDataset : IGPFunction2
    {
        private const string _defaultNetworkDatasetName = "nd";

        private string m_DisplayName = "Create OSM Network Dataset";
        private int in_osmFeatureDataset, in_NetworkConfigurationFile, out_NetworkDataset;
        private ResourceManager resourceManager = null;
        private OSMGPFactory osmGPFactory = null;

        public OSMGPCreateNetworkDataset()
        {
            osmGPFactory = new OSMGPFactory();
            resourceManager = new ResourceManager("ESRI.ArcGIS.OSM.GeoProcessing.OSMGPToolsStrings", this.GetType().Assembly);
        }

        #region "IGPFunction2 Implementations"
        public UID DialogCLSID
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
                if (String.IsNullOrEmpty(m_DisplayName))
                {
                    m_DisplayName = osmGPFactory.GetFunctionName(OSMGPFactory.m_CreateNetworkDatasetName).DisplayName;
                }

                return m_DisplayName;
            }
        }

        public void Execute(IArray paramvalues, ITrackCancel TrackCancel, IGPEnvironmentManager envMgr, IGPMessages message)
        {
            IAoInitialize aoInitialize = new AoInitializeClass();
            esriLicenseStatus naStatus = esriLicenseStatus.esriLicenseUnavailable;

            IGPUtilities2 gpUtil = null;
            IDataset osmDataset = null;

            try
            {
                if (!aoInitialize.IsExtensionCheckedOut(esriLicenseExtensionCode.esriLicenseExtensionCodeNetwork))
                    naStatus = aoInitialize.CheckOutExtension(esriLicenseExtensionCode.esriLicenseExtensionCodeNetwork);

                gpUtil = new GPUtilitiesClass();

                // OSM Dataset Param
                IGPParameter osmDatasetParam = paramvalues.get_Element(in_osmFeatureDataset) as IGPParameter;
                IDEDataset2 osmDEDataset = gpUtil.UnpackGPValue(osmDatasetParam) as IDEDataset2;
                if (osmDEDataset == null)
                {
                    message.AddError(120048, string.Format(resourceManager.GetString("GPTools_NullPointerParameterType"), osmDatasetParam.Name));
                    return;
                }

                osmDataset = gpUtil.OpenDatasetFromLocation(((IDataElement)osmDEDataset).CatalogPath) as IDataset;

                // Network Config File Param
                IGPParameter osmNetConfigParam = paramvalues.get_Element(in_NetworkConfigurationFile) as IGPParameter;
                IGPValue osmNetConfigFile = gpUtil.UnpackGPValue(osmNetConfigParam) as IGPValue;
                if ((osmNetConfigFile == null) || (string.IsNullOrEmpty(osmNetConfigFile.GetAsText())))
                {
                    message.AddError(120048, string.Format(resourceManager.GetString("GPTools_NullPointerParameterType"), osmNetConfigParam.Name));
                    return;
                }

                // Target Network Dataset Param
                IGPParameter ndsParam = paramvalues.get_Element(out_NetworkDataset) as IGPParameter;
                IDataElement deNDS = gpUtil.UnpackGPValue(ndsParam) as IDataElement;
                if (deNDS == null)
                {
                    message.AddError(120048, string.Format(resourceManager.GetString("GPTools_NullPointerParameterType"), ndsParam.Name));
                    return;
                }

                // Create Network Dataset
                using (NetworkDataset nd = new NetworkDataset(osmNetConfigFile.GetAsText(), osmDataset, deNDS.Name, message, TrackCancel))
                {
                    if (nd.CanCreateNetworkDataset())
                        nd.CreateNetworkDataset();
                }
            }
            catch (UserCancelException ex)
            {
                message.AddWarning(ex.Message);
            }
            catch (Exception ex)
            {
                message.AddError(120008, ex.Message);
#if DEBUG
                message.AddError(120008, ex.StackTrace);
#endif
            }
            finally
            {
                if (osmDataset != null)
                    ComReleaser.ReleaseCOMObject(osmDataset);

                if (naStatus == esriLicenseStatus.esriLicenseCheckedOut)
                    aoInitialize.CheckInExtension(esriLicenseExtensionCode.esriLicenseExtensionCodeNetwork);

                if (gpUtil != null)
                    ComReleaser.ReleaseCOMObject(gpUtil);

                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
        }

        public IName FullName
        {
            get
            {
                IName fullName = null;

                if (osmGPFactory != null)
                {
                    fullName = osmGPFactory.GetFunctionName(OSMGPFactory.m_CreateNetworkDatasetName) as IName;
                }

                return fullName;
            }
        }

        public object GetRenderer(IGPParameter pParam)
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
                return default(string);
            }
        }

        public bool IsLicensed()
        {
            // check for a Network Analyst license
            IAoInitialize aoInitialize = new AoInitializeClass();
            ILicenseInformation licInfo = (ILicenseInformation)aoInitialize;

            esriLicenseStatus licenseStatus = aoInitialize.IsExtensionCodeAvailable(aoInitialize.InitializedProduct(), esriLicenseExtensionCode.esriLicenseExtensionCodeNetwork);

            bool isLicensed = false;

            if (licenseStatus != esriLicenseStatus.esriLicenseUnavailable)
            {
                isLicensed = true;
            }

            return isLicensed;
        }

        public string MetadataFile
        {
            get
            {
                string metadafile = "osmgpcreatenetworkdataset.xml";

                try
                {
                    string[] languageid = System.Threading.Thread.CurrentThread.CurrentUICulture.Name.Split("-".ToCharArray());

                    string ArcGISInstallationLocation = OSMGPFactory.GetArcGIS10InstallLocation();
                    string localizedMetaDataFileShort = ArcGISInstallationLocation + System.IO.Path.DirectorySeparatorChar.ToString() + "help" + System.IO.Path.DirectorySeparatorChar.ToString() + "gp" + System.IO.Path.DirectorySeparatorChar.ToString() + "osmgpcreatenetworkdataset_" + languageid[0] + ".xml";
                    string localizedMetaDataFileLong = ArcGISInstallationLocation + System.IO.Path.DirectorySeparatorChar.ToString() + "help" + System.IO.Path.DirectorySeparatorChar.ToString() + "gp" + System.IO.Path.DirectorySeparatorChar.ToString() + "osmgpcreatenetworkdataset_" + System.Threading.Thread.CurrentThread.CurrentUICulture.Name + ".xml";

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
                return OSMGPFactory.m_CreateNetworkDatasetName;
            }
        }

        public IArray ParameterInfo
        {
            get
            {
                IArray parameterArray = new ArrayClass();

                // input osm feature dataset
                IGPParameterEdit3 inputOSMFeatureDataset = new GPParameterClass() as IGPParameterEdit3;
                inputOSMFeatureDataset.DataType = new DEFeatureDatasetTypeClass();
                inputOSMFeatureDataset.Direction = esriGPParameterDirection.esriGPParameterDirectionInput;
                inputOSMFeatureDataset.DisplayName = resourceManager.GetString("GPTools_OSMGPCreateNetworkDataset_inputOSMDataset_displayname");
                inputOSMFeatureDataset.Name = "in_osmfeaturedataset";
                inputOSMFeatureDataset.ParameterType = esriGPParameterType.esriGPParameterTypeRequired;
                in_osmFeatureDataset = 0;
                parameterArray.Add(inputOSMFeatureDataset);

                // input configurationfile
                IGPParameterEdit3 inputNetworkConfiurationFile = new GPParameterClass() as IGPParameterEdit3;
                inputNetworkConfiurationFile.DataType = new DEFileTypeClass();
                inputNetworkConfiurationFile.Direction = esriGPParameterDirection.esriGPParameterDirectionInput;
                inputNetworkConfiurationFile.DisplayName = resourceManager.GetString("GPTools_OSMGPCreateNetworkDataset_inputConfigFile_displayname");
                inputNetworkConfiurationFile.Name = "in_networkconfigfile";
                inputNetworkConfiurationFile.ParameterType = esriGPParameterType.esriGPParameterTypeRequired;
                in_NetworkConfigurationFile = 1;
                parameterArray.Add(inputNetworkConfiurationFile);

                // the final network dataset
                IGPParameterEdit3 outputNetworkDataset = new GPParameterClass() as IGPParameterEdit3;
                outputNetworkDataset.DataType = new DENetworkDatasetTypeClass();
                outputNetworkDataset.Direction = esriGPParameterDirection.esriGPParameterDirectionOutput;
                outputNetworkDataset.DisplayName = resourceManager.GetString("GPTools_OSMGPCreateNetworkDataset_outputNetworkDataset_displayname");
                outputNetworkDataset.Name = "out_networkdataset";
                outputNetworkDataset.ParameterType = esriGPParameterType.esriGPParameterTypeRequired;
                out_NetworkDataset = 2;
                parameterArray.Add(outputNetworkDataset);

                return parameterArray;
            }
        }

        public void UpdateMessages(IArray paramvalues, IGPEnvironmentManager pEnvMgr, IGPMessages Messages)
        {
        }

        public void UpdateParameters(IArray paramvalues, IGPEnvironmentManager pEnvMgr)
        {
            IGPUtilities2 gpUtil = null;

            try
            {
                gpUtil = new GPUtilitiesClass();

                IGPParameter targetDatasetParameter = paramvalues.get_Element(in_osmFeatureDataset) as IGPParameter;
                IDataElement dataElement = gpUtil.UnpackGPValue(targetDatasetParameter) as IDataElement;
                string osmDatasetPath = dataElement.CatalogPath;

                IGPParameter gppNetworkDataset = paramvalues.get_Element(out_NetworkDataset) as IGPParameter;
                IGPValue gpvNetworkDataset = gpUtil.UnpackGPValue(gppNetworkDataset);
                string ndsPath = gpvNetworkDataset.GetAsText();

                string ndsDir = string.Empty;
                if (!string.IsNullOrEmpty(ndsPath))
                    ndsDir = System.IO.Path.GetDirectoryName(ndsPath);

                if (!ndsDir.Equals(osmDatasetPath))
                {
                    string ndsName = System.IO.Path.GetFileName(ndsPath);
                    if (string.IsNullOrEmpty(ndsName))
                        ndsName = _defaultNetworkDatasetName;

                    ndsName = System.IO.Path.GetFileName(osmDatasetPath) + "_" + ndsName;
                    gpvNetworkDataset.SetAsText(System.IO.Path.Combine(osmDatasetPath, ndsName));
                    gpUtil.PackGPValue(gpvNetworkDataset, gppNetworkDataset);
                }
            }
            finally
            {
                if (gpUtil != null)
                    ComReleaser.ReleaseCOMObject(gpUtil);
            }
        }

        public IGPMessages Validate(IArray paramvalues, bool updateValues, IGPEnvironmentManager envMgr)
        {
            return null;
        }
        #endregion
    }
}
