// (c) Copyright Esri, 2010 - 2016
// This source is subject to the Apache 2.0 License.
// Please see http://www.apache.org/licenses/LICENSE-2.0.html for details.
// All other rights reserved.

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
using ESRI.ArcGIS.OSM.OSMClassExtension;

namespace ESRI.ArcGIS.OSM.GeoProcessing
{
    [Guid("f1c4f01d-bf2c-4f2b-b8fa-8b9b701ee160")]
    [ClassInterface(ClassInterfaceType.None)]
    [ProgId("OSMEditor.OSMGPWayLoader")]
    public class OSMGPWayLoader : ESRI.ArcGIS.Geoprocessing.IGPFunction2
    {

        string m_DisplayName = String.Empty;
        ResourceManager resourceManager = null;
        OSMGPFactory osmGPFactory = null;

        public OSMGPWayLoader()
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
                    m_DisplayName = osmGPFactory.GetFunctionName(OSMGPFactory.m_WayLoaderName).DisplayName;
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

            string storageKeyword = String.Empty;

            IGPEnvironment configKeyword = OSMToolHelper.getEnvironment(envMgr, "configKeyword");
            if (configKeyword != null)
            {
                IGPString gpString = configKeyword.Value as IGPString;

                if (gpString != null)
                {
                    storageKeyword = gpString.Value;
                }
            }

            IGPParameter osmFileParameter = paramvalues.get_Element(0) as IGPParameter;
            IGPValue osmFileLocationString = gpUtilities3.UnpackGPValue(osmFileParameter) as IGPValue;

            IGPParameter osmLineFeatureClassParameter = paramvalues.get_Element(4) as IGPParameter;
            IGPValue osmLineFeatureClassGPValue = gpUtilities3.UnpackGPValue(osmLineFeatureClassParameter) as IGPValue;

            string sdfg = osmLineFeatureClassGPValue.GetAsText();

            IName workspaceName = gpUtilities3.CreateParentFromCatalogPath(osmLineFeatureClassGPValue.GetAsText());
            IWorkspace2 lineFeatureWorkspace = workspaceName.Open() as IWorkspace2;

            string[] lineFCNameElements = osmLineFeatureClassGPValue.GetAsText().Split(System.IO.Path.DirectorySeparatorChar);
            string[] lineGDBComponents = new string[lineFCNameElements.Length - 1];
            System.Array.Copy(lineFCNameElements, lineGDBComponents, lineFCNameElements.Length - 1);
            string lineFileGDBLocation = String.Join(System.IO.Path.DirectorySeparatorChar.ToString(), lineGDBComponents);

            IFeatureClass osmLineFeatureClass = null;


            IGPParameter tagLineCollectionParameter = paramvalues.get_Element(2) as IGPParameter;
            IGPMultiValue tagLineCollectionGPValue = gpUtilities3.UnpackGPValue(tagLineCollectionParameter) as IGPMultiValue;

            List<String> lineTagstoExtract = null;

            if (tagLineCollectionGPValue.Count > 0)
            {
                lineTagstoExtract = new List<string>();

                for (int valueIndex = 0; valueIndex < tagLineCollectionGPValue.Count; valueIndex++)
                {
                    string nameOfTag = tagLineCollectionGPValue.get_Value(valueIndex).GetAsText();

                    lineTagstoExtract.Add(nameOfTag);
                }
            }
            else
            {
                lineTagstoExtract = OSMToolHelper.OSMSmallFeatureClassFields();
            }


            // lines
            try
            {
                osmLineFeatureClass = osmToolHelper.CreateSmallLineFeatureClass(lineFeatureWorkspace, lineFCNameElements[lineFCNameElements.Length - 1], storageKeyword, "", "", lineTagstoExtract);
            }
            catch (Exception ex)
            {
                message.AddError(120035, String.Format(resourceManager.GetString("GPTools_OSMGPDownload_nullpointfeatureclass"), ex.Message));
                return;
            }

            if (osmLineFeatureClass == null)
            {
                return;
            }


            IGPParameter osmPolygonFeatureClassParameter = paramvalues.get_Element(5) as IGPParameter;
            IGPValue osmPolygonFeatureClassGPValue = gpUtilities3.UnpackGPValue(osmPolygonFeatureClassParameter) as IGPValue;

            workspaceName = gpUtilities3.CreateParentFromCatalogPath(osmPolygonFeatureClassGPValue.GetAsText());
            IWorkspace2 polygonFeatureWorkspace = workspaceName.Open() as IWorkspace2;

            string[] polygonFCNameElements = osmPolygonFeatureClassGPValue.GetAsText().Split(System.IO.Path.DirectorySeparatorChar);
            string[] polygonGDBComponents = new string[polygonFCNameElements.Length - 1];
            System.Array.Copy(polygonFCNameElements, polygonGDBComponents, polygonFCNameElements.Length - 1);
            string polygonFileGDBLocation = String.Join(System.IO.Path.DirectorySeparatorChar.ToString(), polygonGDBComponents);

            IFeatureClass osmPolygonFeatureClass = null;

            IGPParameter tagPolygonCollectionParameter = paramvalues.get_Element(3) as IGPParameter;
            IGPMultiValue tagPolygonCollectionGPValue = gpUtilities3.UnpackGPValue(tagPolygonCollectionParameter) as IGPMultiValue;

            List<String> polygonTagstoExtract = null;

            if (tagPolygonCollectionGPValue.Count > 0)
            {
                polygonTagstoExtract = new List<string>();

                for (int valueIndex = 0; valueIndex < tagPolygonCollectionGPValue.Count; valueIndex++)
                {
                    string nameOfTag = tagPolygonCollectionGPValue.get_Value(valueIndex).GetAsText();

                    polygonTagstoExtract.Add(nameOfTag);
                }
            }
            else
            {
                polygonTagstoExtract = OSMToolHelper.OSMSmallFeatureClassFields();
            }


            // polygons
            try
            {
                osmPolygonFeatureClass = osmToolHelper.CreateSmallPolygonFeatureClass(polygonFeatureWorkspace, 
                    polygonFCNameElements[polygonFCNameElements.Length - 1], storageKeyword, "", "", polygonTagstoExtract);
            }
            catch (Exception ex)
            {
                message.AddError(120035, String.Format(resourceManager.GetString("GPTools_OSMGPDownload_nullpointfeatureclass"), ex.Message));
                return;
            }

            if (osmPolygonFeatureClass == null)
            {
                return;
            }

            ComReleaser.ReleaseCOMObject(osmPolygonFeatureClass);
            ComReleaser.ReleaseCOMObject(osmLineFeatureClass);

            IGPParameter osmPointFeatureClassParameter = paramvalues.get_Element(1) as IGPParameter;
            IGPValue osmPointFeatureClassGPValue = gpUtilities3.UnpackGPValue(osmPointFeatureClassParameter) as IGPValue;

            osmToolHelper.smallLoadOSMWay(osmFileLocationString.GetAsText(), osmPointFeatureClassGPValue.GetAsText(), lineFileGDBLocation, 
                lineFCNameElements[lineFCNameElements.Length - 1], polygonFileGDBLocation, polygonFCNameElements[polygonFCNameElements.Length - 1], 
                lineTagstoExtract, polygonTagstoExtract);
        }

        public ESRI.ArcGIS.esriSystem.IName FullName
        {
            get
            {
                IName fullName = null;

                if (osmGPFactory != null)
                {
                    fullName = osmGPFactory.GetFunctionName(OSMGPFactory.m_WayLoaderName) as IName;
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
                string metadafile = "osmgpwayloader.xml";

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
                return OSMGPFactory.m_WayLoaderName;
            }
        }

        public ESRI.ArcGIS.esriSystem.IArray ParameterInfo
        {
            get
            {
                IArray parameters = new ArrayClass();

                // input osm file containing the ways
                IGPParameterEdit3 osmWayFileParameter = new GPParameterClass() as IGPParameterEdit3;
                osmWayFileParameter.DataType = new DEFileTypeClass();
                osmWayFileParameter.Direction = esriGPParameterDirection.esriGPParameterDirectionInput;
                osmWayFileParameter.DisplayName = resourceManager.GetString("GPTools_OSMGPWayLoader_osmfile_desc");
                osmWayFileParameter.Name = "in_osmWayFile";
                osmWayFileParameter.ParameterType = esriGPParameterType.esriGPParameterTypeRequired;

                // input feature class containing the points
                IGPParameterEdit3 osmPointFeatureClassParameter = new GPParameterClass() as IGPParameterEdit3;
                osmPointFeatureClassParameter.DataType = new DEFeatureClassTypeClass();
                osmPointFeatureClassParameter.Direction = esriGPParameterDirection.esriGPParameterDirectionInput;
                osmPointFeatureClassParameter.DisplayName = resourceManager.GetString("GPTools_OSMGPWayLoader_osmpointFeatureClass_desc");
                osmPointFeatureClassParameter.Name = "in_osmPointFC";
                osmPointFeatureClassParameter.ParameterType = esriGPParameterType.esriGPParameterTypeRequired;


                IGPFeatureClassDomain osmPointFeatureClassDomain = new GPFeatureClassDomainClass();
                osmPointFeatureClassDomain.AddFeatureType(esriFeatureType.esriFTSimple);
                osmPointFeatureClassDomain.AddType(ESRI.ArcGIS.Geometry.esriGeometryType.esriGeometryPoint);
                osmPointFeatureClassParameter.Domain = osmPointFeatureClassDomain as IGPDomain;


                // field multi parameter
                IGPParameterEdit3 loadLineFieldsParameter = new GPParameterClass() as IGPParameterEdit3;

                IGPDataType fieldNameDataType = new GPStringTypeClass();
                IGPMultiValue fieldNameMultiValue = new GPMultiValueClass();
                fieldNameMultiValue.MemberDataType = fieldNameDataType;

                IGPMultiValueType fieldNameDataType2 = new GPMultiValueTypeClass();
                fieldNameDataType2.MemberDataType = fieldNameDataType;

                loadLineFieldsParameter.Name = "in_polyline_fieldNames";
                loadLineFieldsParameter.DisplayName = resourceManager.GetString("GPTools_OSMGPWayLoader_lineFieldNames_desc");
                loadLineFieldsParameter.Category = resourceManager.GetString("GPTools_OSMGPMultiLoader_schemaCategory_desc");
                loadLineFieldsParameter.ParameterType = esriGPParameterType.esriGPParameterTypeOptional;
                loadLineFieldsParameter.Direction = esriGPParameterDirection.esriGPParameterDirectionInput;
                loadLineFieldsParameter.DataType = (IGPDataType)fieldNameDataType2;
                loadLineFieldsParameter.Value = (IGPValue)fieldNameMultiValue;

                IGPParameterEdit3 loadPolygonFieldsParameter = new GPParameterClass() as IGPParameterEdit3;
                loadPolygonFieldsParameter.Name = "in_polygon_fieldNames";
                loadPolygonFieldsParameter.DisplayName = resourceManager.GetString("GPTools_OSMGPWayLoader_polygonFieldNames_desc");
                loadPolygonFieldsParameter.Category = resourceManager.GetString("GPTools_OSMGPMultiLoader_schemaCategory_desc");
                loadPolygonFieldsParameter.ParameterType = esriGPParameterType.esriGPParameterTypeOptional;
                loadPolygonFieldsParameter.Direction = esriGPParameterDirection.esriGPParameterDirectionInput;
                loadPolygonFieldsParameter.DataType = (IGPDataType)fieldNameDataType2;
                loadPolygonFieldsParameter.Value = (IGPValue)fieldNameMultiValue;


                IGPParameterEdit3 osmLinesParameter = new GPParameterClass() as IGPParameterEdit3;
                osmLinesParameter.DataType = new DEFeatureClassTypeClass();
                osmLinesParameter.Direction = esriGPParameterDirection.esriGPParameterDirectionOutput;
                osmLinesParameter.ParameterType = esriGPParameterType.esriGPParameterTypeRequired;
                osmLinesParameter.DisplayName = resourceManager.GetString("GPTools_OSMGPWayLoader_osmLines_desc");
                osmLinesParameter.Name = "out_osmWayLines";

                IGPFeatureClassDomain osmLineFeatureClassDomain = new GPFeatureClassDomainClass();
                osmLineFeatureClassDomain.AddFeatureType(esriFeatureType.esriFTSimple);
                osmLineFeatureClassDomain.AddType(ESRI.ArcGIS.Geometry.esriGeometryType.esriGeometryPolyline);
                osmLinesParameter.Domain = osmLineFeatureClassDomain as IGPDomain;

                IGPParameterEdit3 osmPolygonsParameter = new GPParameterClass() as IGPParameterEdit3;
                osmPolygonsParameter.DataType = new DEFeatureClassTypeClass();
                osmPolygonsParameter.Direction = esriGPParameterDirection.esriGPParameterDirectionOutput;
                osmPolygonsParameter.ParameterType = esriGPParameterType.esriGPParameterTypeRequired;
                osmPolygonsParameter.DisplayName = resourceManager.GetString("GPTools_OSMGPWayLoader_osmPolygons_desc");
                osmPolygonsParameter.Name = "out_osmWayPolygons";

                IGPFeatureClassDomain osmPolygonFeatureClassDomain = new GPFeatureClassDomainClass();
                osmPolygonFeatureClassDomain.AddFeatureType(esriFeatureType.esriFTSimple);
                osmPolygonFeatureClassDomain.AddType(ESRI.ArcGIS.Geometry.esriGeometryType.esriGeometryPolygon);
                osmPolygonsParameter.Domain = osmPolygonFeatureClassDomain as IGPDomain;

                parameters.Add(osmWayFileParameter);

                parameters.Add(osmPointFeatureClassParameter);

                parameters.Add(loadLineFieldsParameter);

                parameters.Add(loadPolygonFieldsParameter);

                parameters.Add(osmLinesParameter);

                parameters.Add(osmPolygonsParameter);

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
