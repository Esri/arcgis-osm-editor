using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Resources;
using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.Geoprocessing;
using ESRI.ArcGIS.DataSourcesFile;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Display;

namespace ESRI.ArcGIS.OSM.GeoProcessing
{
    [Guid("a89bdb78-8abf-45c7-9a44-c4b876fa5d6d")]
    [ClassInterface(ClassInterfaceType.None)]
    [ProgId("OSMEditor.OSMGPPointLoader")]
    public class OSMGPNodeLoader : ESRI.ArcGIS.Geoprocessing.IGPFunction2
    {
        string m_DisplayName = String.Empty;
        ResourceManager resourceManager = null;
        OSMGPFactory osmGPFactory = null;

        public OSMGPNodeLoader()
        {
            resourceManager = new ResourceManager("ESRI.ArcGIS.OSM.GeoProcessing.OSMGPToolsStrings", this.GetType().Assembly);

            osmGPFactory = new OSMGPFactory();
        }

        #region "IGPFunction2 Implementations"
        public ESRI.ArcGIS.esriSystem.UID DialogCLSID
        {
            get
            {
                return default(ESRI.ArcGIS.esriSystem.UID);
            }
        }

        public string DisplayName
        {
            get
            {
                if (String.IsNullOrEmpty(m_DisplayName))
                {
                    m_DisplayName = osmGPFactory.GetFunctionName(OSMGPFactory.m_NodeLoaderName).DisplayName;
                }

                return m_DisplayName;
            }
        }

        public void Execute(ESRI.ArcGIS.esriSystem.IArray paramvalues, ESRI.ArcGIS.esriSystem.ITrackCancel TrackCancel, ESRI.ArcGIS.Geoprocessing.IGPEnvironmentManager envMgr, ESRI.ArcGIS.Geodatabase.IGPMessages message)
        {
            IGPUtilities3 gpUtilities3 = new GPUtilitiesClass();
            OSMToolHelper osmToolHelper = new OSMToolHelper();

            if (TrackCancel == null)
            {
                TrackCancel = new CancelTrackerClass();
            }

            IGPEnvironment configKeyword = OSMToolHelper.getEnvironment(envMgr, "configKeyword");
            IGPString gpString = configKeyword.Value as IGPString;

            string storageKeyword = String.Empty;

            if (gpString != null)
            {
                storageKeyword = gpString.Value;
            }

            IGPParameter osmFileParameter = paramvalues.get_Element(0) as IGPParameter;
            IGPValue osmFileLocationString = gpUtilities3.UnpackGPValue(osmFileParameter) as IGPValue;


            IGPParameter tagCollectionParameter = paramvalues.get_Element(1) as IGPParameter;
            IGPMultiValue tagCollectionGPValue = gpUtilities3.UnpackGPValue(tagCollectionParameter) as IGPMultiValue;

            List<String> tagstoExtract = null;

            if (tagCollectionGPValue.Count > 0)
            {
                tagstoExtract = new List<string>();

                for (int valueIndex = 0; valueIndex < tagCollectionGPValue.Count; valueIndex++)
                {
                    string nameOfTag = tagCollectionGPValue.get_Value(valueIndex).GetAsText();

                    tagstoExtract.Add(nameOfTag);
                }
            }
            else
            {
                tagstoExtract = OSMToolHelper.OSMSmallFeatureClassFields();
            }


            IGPParameter osmPointsFeatureClassParameter = paramvalues.get_Element(2) as IGPParameter;
            IGPValue osmPointsFeatureClassGPValue = gpUtilities3.UnpackGPValue(osmPointsFeatureClassParameter) as IGPValue;

            IName workspaceName = gpUtilities3.CreateParentFromCatalogPath(osmPointsFeatureClassGPValue.GetAsText());
            IWorkspace2 pointFeatureWorkspace = workspaceName.Open() as IWorkspace2;

            string[] pointFCNameElements = osmPointsFeatureClassGPValue.GetAsText().Split(System.IO.Path.DirectorySeparatorChar);

            IFeatureClass osmPointFeatureClass = null;

            // points
            try
            {
                osmPointFeatureClass = osmToolHelper.CreateSmallPointFeatureClass(pointFeatureWorkspace, pointFCNameElements[pointFCNameElements.Length - 1], storageKeyword, "", "", tagstoExtract);
            }
            catch (Exception ex)
            {
                message.AddError(120035, String.Format(resourceManager.GetString("GPTools_OSMGPDownload_nullpointfeatureclass"), ex.Message));
                return;
            }

            if (osmPointFeatureClass == null)
            {
                return;
            }

            string[] gdbComponents = new string[pointFCNameElements.Length - 1];
            System.Array.Copy(pointFCNameElements, gdbComponents, pointFCNameElements.Length - 1);
            string fileGDBLocation = String.Join(System.IO.Path.DirectorySeparatorChar.ToString(), gdbComponents);
            osmToolHelper.smallLoadOSMNode(osmFileLocationString.GetAsText(), fileGDBLocation, pointFCNameElements[pointFCNameElements.Length - 1], tagstoExtract);

        }

        public ESRI.ArcGIS.esriSystem.IName FullName
        {
            get
            {
                IName fullName = null;

                if (osmGPFactory != null)
                {
                    fullName = osmGPFactory.GetFunctionName(OSMGPFactory.m_NodeLoaderName) as IName;
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
                string metadafile = "osmgpnodeloader.xml";

                try
                {
                    string[] languageid = System.Threading.Thread.CurrentThread.CurrentUICulture.Name.Split("-".ToCharArray());

                    string ArcGISInstallationLocation = OSMGPFactory.GetArcGIS10InstallLocation();
                    string localizedMetaDataFileShort = ArcGISInstallationLocation + System.IO.Path.DirectorySeparatorChar.ToString() + "help" + System.IO.Path.DirectorySeparatorChar.ToString() + "gp" + System.IO.Path.DirectorySeparatorChar.ToString() + "osmgpfileloader_" + languageid[0] + ".xml";
                    string localizedMetaDataFileLong = ArcGISInstallationLocation + System.IO.Path.DirectorySeparatorChar.ToString() + "help" + System.IO.Path.DirectorySeparatorChar.ToString() + "gp" + System.IO.Path.DirectorySeparatorChar.ToString() + "osmgpfileloader_" + System.Threading.Thread.CurrentThread.CurrentUICulture.Name + ".xml";

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
                return OSMGPFactory.m_NodeLoaderName;
            }
        }

        public ESRI.ArcGIS.esriSystem.IArray ParameterInfo
        {
            get
            {
                IArray parameters = new ArrayClass();

                // input osm file containing the nodes
                IGPParameterEdit3 osmNodeFile = new GPParameterClass() as IGPParameterEdit3;
                osmNodeFile.DataType = new DEFileTypeClass();
                osmNodeFile.Direction = esriGPParameterDirection.esriGPParameterDirectionInput;
                osmNodeFile.DisplayName = resourceManager.GetString("GPTools_OSMGPNodeLoader_osmfile_desc");
                osmNodeFile.Name = "in_osmNodeFile";
                osmNodeFile.ParameterType = esriGPParameterType.esriGPParameterTypeRequired;

                // field multi parameter
                IGPParameterEdit3 loadFieldsParameter = new GPParameterClass() as IGPParameterEdit3;

                IGPDataType fieldNameDataType = new GPStringTypeClass();
                IGPMultiValue fieldNameMultiValue = new GPMultiValueClass();
                fieldNameMultiValue.MemberDataType = fieldNameDataType;

                IGPMultiValueType fieldNameDataType2 = new GPMultiValueTypeClass();
                fieldNameDataType2.MemberDataType = fieldNameDataType;

                loadFieldsParameter.Name = "in_fieldNames";
                loadFieldsParameter.DisplayName = resourceManager.GetString("GPTools_OSMGPNodeLoader_fieldNames_desc");
                loadFieldsParameter.Category = resourceManager.GetString("GPTools_OSMGPMultiLoader_schemaCategory_desc");
                loadFieldsParameter.ParameterType = esriGPParameterType.esriGPParameterTypeOptional;
                loadFieldsParameter.Direction = esriGPParameterDirection.esriGPParameterDirectionInput;
                loadFieldsParameter.DataType = (IGPDataType)fieldNameDataType2;
                loadFieldsParameter.Value = (IGPValue)fieldNameMultiValue;

                IGPParameterEdit3 osmPoints = new GPParameterClass() as IGPParameterEdit3;
                osmPoints.DataType = new DEFeatureClassTypeClass();
                osmPoints.Direction = esriGPParameterDirection.esriGPParameterDirectionOutput;
                osmPoints.ParameterType = esriGPParameterType.esriGPParameterTypeRequired;
                osmPoints.DisplayName = resourceManager.GetString("GPTools_OSMGPNodeLoader_osmPoints_desc");
                osmPoints.Name = "out_osmNodePoints";

                IGPFeatureClassDomain osmPointFeatureClassDomain = new GPFeatureClassDomainClass();
                osmPointFeatureClassDomain.AddFeatureType(esriFeatureType.esriFTSimple);
                osmPointFeatureClassDomain.AddType(ESRI.ArcGIS.Geometry.esriGeometryType.esriGeometryPoint);
                osmPoints.Domain = osmPointFeatureClassDomain as IGPDomain;

                parameters.Add(osmNodeFile);

                parameters.Add(loadFieldsParameter);

                parameters.Add(osmPoints);

                return parameters;
            }
        }

        public void UpdateMessages(ESRI.ArcGIS.esriSystem.IArray paramvalues, ESRI.ArcGIS.Geoprocessing.IGPEnvironmentManager pEnvMgr, ESRI.ArcGIS.Geodatabase.IGPMessages Messages)
        {
        }

        public void UpdateParameters(ESRI.ArcGIS.esriSystem.IArray paramvalues, ESRI.ArcGIS.Geoprocessing.IGPEnvironmentManager pEnvMgr)
        {
        }

        public ESRI.ArcGIS.Geodatabase.IGPMessages Validate(ESRI.ArcGIS.esriSystem.IArray paramvalues, bool updateValues, ESRI.ArcGIS.Geoprocessing.IGPEnvironmentManager envMgr)
        {
            return default(ESRI.ArcGIS.Geodatabase.IGPMessages);
        }
        #endregion

    }
}
