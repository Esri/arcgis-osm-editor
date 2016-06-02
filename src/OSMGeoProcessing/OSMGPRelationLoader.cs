// (c) Copyright Esri, 2010 - 2016
// This source is subject to the Apache 2.0 License.
// Please see http://www.apache.org/licenses/LICENSE-2.0.html for details.
// All other rights reserved.

using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.Geoprocessing;
using ESRI.ArcGIS.DataSourcesFile;
using System.Resources;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.OSM.OSMClassExtension;
using ESRI.ArcGIS.Display;

namespace ESRI.ArcGIS.OSM.GeoProcessing
{
    [Guid("1fdcb9ab-54b1-4592-ad00-e5a19e425ce8")]
    [ClassInterface(ClassInterfaceType.None)]
    [ProgId("OSMEditor.OSMGPRelationLoader")]
    public class OSMGPRelationLoader : ESRI.ArcGIS.Geoprocessing.IGPFunction2
    {


        string m_DisplayName = String.Empty;
        ResourceManager resourceManager = null;
        OSMGPFactory osmGPFactory = null;

        public OSMGPRelationLoader()
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
                    m_DisplayName = osmGPFactory.GetFunctionName(OSMGPFactory.m_RelationLoaderName).DisplayName;
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

            IGPParameter loadSuperRelationParameter = paramvalues.get_Element(1) as IGPParameter;
            IGPBoolean loadSuperRelationGPValue = gpUtilities3.UnpackGPValue(loadSuperRelationParameter) as IGPBoolean;

            IGPParameter osmSourceLineFeatureClassParameter = paramvalues.get_Element(2) as IGPParameter;
            IGPValue osmSourceLineFeatureClassGPValue = gpUtilities3.UnpackGPValue(osmSourceLineFeatureClassParameter) as IGPValue;

            IGPParameter osmSourcePolygonFeatureClassParameter = paramvalues.get_Element(3) as IGPParameter;
            IGPValue osmSourcePolygonFeatureClassGPValue = gpUtilities3.UnpackGPValue(osmSourcePolygonFeatureClassParameter) as IGPValue;


            IGPParameter osmTargetLineFeatureClassParameter = paramvalues.get_Element(6) as IGPParameter;
            IGPValue osmTargetLineFeatureClassGPValue = gpUtilities3.UnpackGPValue(osmTargetLineFeatureClassParameter) as IGPValue;

            IName workspaceName = gpUtilities3.CreateParentFromCatalogPath(osmTargetLineFeatureClassGPValue.GetAsText());
            IWorkspace2 lineFeatureWorkspace = workspaceName.Open() as IWorkspace2;

            string[] lineFCNameElements = osmTargetLineFeatureClassGPValue.GetAsText().Split(System.IO.Path.DirectorySeparatorChar);

            IFeatureClass osmLineFeatureClass = null;

            IGPParameter tagLineCollectionParameter = paramvalues.get_Element(4) as IGPParameter;
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
                osmLineFeatureClass = osmToolHelper.CreateSmallLineFeatureClass(lineFeatureWorkspace, 
                    lineFCNameElements[lineFCNameElements.Length - 1], storageKeyword, "", "", lineTagstoExtract);
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


            IGPParameter osmTargetPolygonFeatureClassParameter = paramvalues.get_Element(7) as IGPParameter;
            IGPValue osmTargetPolygonFeatureClassGPValue = gpUtilities3.UnpackGPValue(osmTargetPolygonFeatureClassParameter) as IGPValue;

            workspaceName = gpUtilities3.CreateParentFromCatalogPath(osmTargetPolygonFeatureClassGPValue.GetAsText());
            IWorkspace2 polygonFeatureWorkspace = workspaceName.Open() as IWorkspace2;

            string[] polygonFCNameElements = osmTargetPolygonFeatureClassGPValue.GetAsText().Split(System.IO.Path.DirectorySeparatorChar);

            IFeatureClass osmPolygonFeatureClass = null;

            IGPParameter tagPolygonCollectionParameter = paramvalues.get_Element(5) as IGPParameter;
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


            string[] gdbComponents = new string[polygonFCNameElements.Length - 1];
            System.Array.Copy(lineFCNameElements, gdbComponents, lineFCNameElements.Length - 1);
            string fileGDBLocation = String.Join(System.IO.Path.DirectorySeparatorChar.ToString(), gdbComponents);

            osmToolHelper.smallLoadOSMRelations(osmFileLocationString.GetAsText(), 
                osmSourceLineFeatureClassGPValue.GetAsText(), 
                osmSourcePolygonFeatureClassGPValue.GetAsText(),
                osmTargetLineFeatureClassGPValue.GetAsText(), 
                osmTargetPolygonFeatureClassGPValue.GetAsText(),
                lineTagstoExtract, polygonTagstoExtract, loadSuperRelationGPValue.Value);


        }

        public ESRI.ArcGIS.esriSystem.IName FullName
        {
            get
            {
                IName fullName = null;

                if (osmGPFactory != null)
                {
                    fullName = osmGPFactory.GetFunctionName(OSMGPFactory.m_RelationLoaderName) as IName;
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
                string metadafile = "osmgprelationloader.xml";

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
                return OSMGPFactory.m_RelationLoaderName;
            }
        }

        public ESRI.ArcGIS.esriSystem.IArray ParameterInfo
        {
            get
            {
                IArray parameters = new ArrayClass();

                // input osm file containing the relations
                IGPParameterEdit3 osmRelationFileParameter = new GPParameterClass() as IGPParameterEdit3;
                osmRelationFileParameter.DataType = new DEFileTypeClass();
                osmRelationFileParameter.Direction = esriGPParameterDirection.esriGPParameterDirectionInput;
                osmRelationFileParameter.DisplayName = resourceManager.GetString("GPTools_OSMGPRelationLoader_osmfile_desc");
                osmRelationFileParameter.Name = "in_osmRelationFile";
                osmRelationFileParameter.ParameterType = esriGPParameterType.esriGPParameterTypeRequired;

                // load super-relation parameter
                IGPParameterEdit3 loadSuperRelationParameter = new GPParameterClass() as IGPParameterEdit3;
                IGPCodedValueDomain loadSuperRelationDomain = new GPCodedValueDomainClass();

                IGPBoolean loadSuperRelationTrue = new GPBooleanClass();
                loadSuperRelationTrue.Value = true;
                IGPBoolean loadSuperRelationFalse = new GPBooleanClass();
                loadSuperRelationFalse.Value = false;

                loadSuperRelationDomain.AddCode((IGPValue)loadSuperRelationTrue, "LOAD_SUPER_RELATION");
                loadSuperRelationDomain.AddCode((IGPValue)loadSuperRelationFalse, "DO_NOT_LOAD_SUPER_RELATION");
                loadSuperRelationParameter.Domain = (IGPDomain)loadSuperRelationDomain;
                loadSuperRelationParameter.Value = (IGPValue)loadSuperRelationFalse;

                loadSuperRelationParameter.DataType = new GPBooleanTypeClass();
                loadSuperRelationParameter.Direction = esriGPParameterDirection.esriGPParameterDirectionInput;
                loadSuperRelationParameter.ParameterType = esriGPParameterType.esriGPParameterTypeOptional;
                loadSuperRelationParameter.DisplayName = resourceManager.GetString("GPTools_OSMGPRelationLoader_loadSuperRelations_desc");
                loadSuperRelationParameter.Name = "in_loadSuperRelation";


                IGPParameterEdit3 osmLinesParameter = new GPParameterClass() as IGPParameterEdit3;
                osmLinesParameter.DataType = new DEFeatureClassTypeClass();
                osmLinesParameter.Direction = esriGPParameterDirection.esriGPParameterDirectionInput;
                osmLinesParameter.ParameterType = esriGPParameterType.esriGPParameterTypeRequired;
                osmLinesParameter.DisplayName = resourceManager.GetString("GPTools_OSMGPRelationLoader_osmLines_desc");
                osmLinesParameter.Name = "in_SourceOSMLines";

                IGPFeatureClassDomain osmLineFeatureClassDomain = new GPFeatureClassDomainClass();
                osmLineFeatureClassDomain.AddFeatureType(esriFeatureType.esriFTSimple);
                osmLineFeatureClassDomain.AddType(ESRI.ArcGIS.Geometry.esriGeometryType.esriGeometryPolyline);
                osmLinesParameter.Domain = osmLineFeatureClassDomain as IGPDomain;

                IGPParameterEdit3 osmPolygonsParameter = new GPParameterClass() as IGPParameterEdit3;
                osmPolygonsParameter.DataType = new DEFeatureClassTypeClass();
                osmPolygonsParameter.Direction = esriGPParameterDirection.esriGPParameterDirectionInput;
                osmPolygonsParameter.ParameterType = esriGPParameterType.esriGPParameterTypeRequired;
                osmPolygonsParameter.DisplayName = resourceManager.GetString("GPTools_OSMGPRelationLoader_osmPolygons_desc");
                osmPolygonsParameter.Name = "in_SourceOSMPolygons";

                // field multi parameter
                IGPParameterEdit3 loadLineFieldsParameter = new GPParameterClass() as IGPParameterEdit3;

                IGPDataType fieldNameDataType = new GPStringTypeClass();
                IGPMultiValue fieldNameMultiValue = new GPMultiValueClass();
                fieldNameMultiValue.MemberDataType = fieldNameDataType;

                IGPMultiValueType fieldNameDataType2 = new GPMultiValueTypeClass();
                fieldNameDataType2.MemberDataType = fieldNameDataType;

                loadLineFieldsParameter.Name = "in_polyline_fieldNames";
                loadLineFieldsParameter.DisplayName = resourceManager.GetString("GPTools_OSMGPRelationLoader_lineFieldNames_desc");
                loadLineFieldsParameter.Category = resourceManager.GetString("GPTools_OSMGPMultiLoader_schemaCategory_desc");
                loadLineFieldsParameter.ParameterType = esriGPParameterType.esriGPParameterTypeOptional;
                loadLineFieldsParameter.Direction = esriGPParameterDirection.esriGPParameterDirectionInput;
                loadLineFieldsParameter.DataType = (IGPDataType)fieldNameDataType2;
                loadLineFieldsParameter.Value = (IGPValue)fieldNameMultiValue;

                IGPParameterEdit3 loadPolygonFieldsParameter = new GPParameterClass() as IGPParameterEdit3;
                loadPolygonFieldsParameter.Name = "in_polygon_fieldNames";
                loadPolygonFieldsParameter.DisplayName = resourceManager.GetString("GPTools_OSMGPRelationLoader_polygonFieldNames_desc");
                loadPolygonFieldsParameter.Category = resourceManager.GetString("GPTools_OSMGPMultiLoader_schemaCategory_desc");
                loadPolygonFieldsParameter.ParameterType = esriGPParameterType.esriGPParameterTypeOptional;
                loadPolygonFieldsParameter.Direction = esriGPParameterDirection.esriGPParameterDirectionInput;
                loadPolygonFieldsParameter.DataType = (IGPDataType)fieldNameDataType2;
                loadPolygonFieldsParameter.Value = (IGPValue)fieldNameMultiValue;

                IGPFeatureClassDomain osmPolygonFeatureClassDomain = new GPFeatureClassDomainClass();
                osmPolygonFeatureClassDomain.AddFeatureType(esriFeatureType.esriFTSimple);
                osmPolygonFeatureClassDomain.AddType(ESRI.ArcGIS.Geometry.esriGeometryType.esriGeometryPolygon);
                osmPolygonsParameter.Domain = osmPolygonFeatureClassDomain as IGPDomain;

                IGPParameterEdit3 osmOutLinesParameter = new GPParameterClass() as IGPParameterEdit3;
                osmOutLinesParameter.DataType = new DEFeatureClassTypeClass();
                osmOutLinesParameter.Direction = esriGPParameterDirection.esriGPParameterDirectionOutput;
                osmOutLinesParameter.ParameterType = esriGPParameterType.esriGPParameterTypeRequired;
                osmOutLinesParameter.DisplayName = resourceManager.GetString("GPTools_OSMGPRelationLoader_osmOutLines_desc");
                osmOutLinesParameter.Name = "out_osmLines";

                IGPFeatureClassDomain osmOutLinesFeatureClassDomain = new GPFeatureClassDomainClass();
                osmOutLinesFeatureClassDomain.AddFeatureType(esriFeatureType.esriFTSimple);
                osmOutLinesFeatureClassDomain.AddType(ESRI.ArcGIS.Geometry.esriGeometryType.esriGeometryPolyline);
                osmOutLinesParameter.Domain = osmOutLinesFeatureClassDomain as IGPDomain;


                IGPParameterEdit3 osmOutPolygonsParameter = new GPParameterClass() as IGPParameterEdit3;
                osmOutPolygonsParameter.DataType = new DEFeatureClassTypeClass();
                osmOutPolygonsParameter.Direction = esriGPParameterDirection.esriGPParameterDirectionOutput;
                osmOutPolygonsParameter.ParameterType = esriGPParameterType.esriGPParameterTypeRequired;
                osmOutPolygonsParameter.DisplayName = resourceManager.GetString("GPTools_OSMGPRelationLoader_osmOutPolygons_desc");
                osmOutPolygonsParameter.Name = "out_osmPolygons";

                IGPFeatureClassDomain osmOutPolygonFeatureClassDomain = new GPFeatureClassDomainClass();
                osmOutPolygonFeatureClassDomain.AddFeatureType(esriFeatureType.esriFTSimple);
                osmOutPolygonFeatureClassDomain.AddType(ESRI.ArcGIS.Geometry.esriGeometryType.esriGeometryPolygon);
                osmOutPolygonsParameter.Domain = osmOutPolygonFeatureClassDomain as IGPDomain;

                // add all the parameters into the info array
                parameters.Add(osmRelationFileParameter);
                parameters.Add(loadSuperRelationParameter);
                parameters.Add(osmLinesParameter);
                parameters.Add(osmPolygonsParameter);
                parameters.Add(loadLineFieldsParameter);
                parameters.Add(loadPolygonFieldsParameter);
                parameters.Add(osmOutLinesParameter);
                parameters.Add(osmOutPolygonsParameter);

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
