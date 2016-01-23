// (c) Copyright Esri, 2010 - 2016
// This source is subject to the Apache 2.0 License.
// Please see http://www.apache.org/licenses/LICENSE-2.0.html for details.
// All other rights reserved.


using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Runtime.InteropServices;
using System.Resources;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geoprocessing;
using ESRI.ArcGIS.esriSystem;

using System.Xml;
using System.Xml.Serialization;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.DataSourcesGDB;
using ESRI.ArcGIS.DataSourcesFile;
using System.IO;
using System.Reflection;
using System.Globalization;
using System.Xml.Linq;
using Microsoft.Win32;
using ESRI.ArcGIS.Display;
using ESRI.ArcGIS.OSM.OSMClassExtension;
using System.Net;
using ESRI.ArcGIS.OSM.OSMUtilities;

namespace ESRI.ArcGIS.OSM.GeoProcessing
{
    [Guid("b1653cbd-4023-4640-a48c-5ac8d21b4a71")]
    [ClassInterface(ClassInterfaceType.None)]
    [ComVisible(true)]
    [ProgId("OSMEditor.OSMGPDownload")]
    public class OSMGPDownload : ESRI.ArcGIS.Geoprocessing.IGPFunction2
    {
        string m_DisplayName = String.Empty;
        int in_downloadURLNumber, in_downloadExtentNumber, out_targetDatasetNumber, in_includeReferencesNumber, out_osmPointsNumber, out_osmLinesNumber, out_osmPolygonsNumber, out_RevTableNumber;
        ResourceManager resourceManager = null;
        OSMGPFactory osmGPFactory = null;

        Dictionary<string, string> m_editorConfigurationSettings = null;
        api m_osmAPICapabilities = null;

        public OSMGPDownload()
        {
            resourceManager = new ResourceManager("ESRI.ArcGIS.OSM.GeoProcessing.OSMGPToolsStrings", this.GetType().Assembly);
            osmGPFactory = new OSMGPFactory();

            m_editorConfigurationSettings = OSMGPFactory.ReadOSMEditorSettings();

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
                if (String.IsNullOrEmpty(m_DisplayName))
                {
                    m_DisplayName = osmGPFactory.GetFunctionName(OSMGPFactory.m_DownloadDataName).DisplayName;
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

        public void Execute(ESRI.ArcGIS.esriSystem.IArray paramvalues, ESRI.ArcGIS.esriSystem.ITrackCancel TrackCancel, ESRI.ArcGIS.Geoprocessing.IGPEnvironmentManager envMgr, ESRI.ArcGIS.Geodatabase.IGPMessages message)
        {
            _message = message;

            IFeatureClass osmPointFeatureClass = null;
            IFeatureClass osmLineFeatureClass = null;
            IFeatureClass osmPolygonFeatureClass = null;

            try
            {
                DateTime syncTime = DateTime.Now;

                IGPUtilities3 gpUtilities3 = new GPUtilitiesClass();

                if (TrackCancel == null)
                {
                    TrackCancel = new CancelTrackerClass();
                }

                IGPParameter baseURLParameter = paramvalues.get_Element(in_downloadURLNumber) as IGPParameter;
                IGPString baseURLString = gpUtilities3.UnpackGPValue(baseURLParameter) as IGPString;

                if (baseURLString == null)
                {
                    message.AddError(120048, string.Format(resourceManager.GetString("GPTools_NullPointerParameterType"), baseURLParameter.Name));
                }

                IGPParameter downloadExtentParameter = paramvalues.get_Element(in_downloadExtentNumber) as IGPParameter;
                IGPValue downloadExtentGPValue = gpUtilities3.UnpackGPValue(downloadExtentParameter);

                esriGPExtentEnum gpExtent;
                IEnvelope downloadEnvelope = gpUtilities3.GetExtent(downloadExtentGPValue, out gpExtent);

                IGPParameter includeAllReferences = paramvalues.get_Element(in_includeReferencesNumber) as IGPParameter;
                IGPBoolean includeAllReferencesGPValue = gpUtilities3.UnpackGPValue(includeAllReferences) as IGPBoolean;

                if (includeAllReferencesGPValue == null)
                {
                    message.AddError(120048, string.Format(resourceManager.GetString("GPTools_NullPointerParameterType"), includeAllReferences.Name));
                }

                IEnvelope newExtent = null;

                ISpatialReferenceFactory spatialReferenceFactory = new SpatialReferenceEnvironmentClass() as ISpatialReferenceFactory;
                ISpatialReference wgs84 = spatialReferenceFactory.CreateGeographicCoordinateSystem((int)esriSRGeoCSType.esriSRGeoCS_WGS1984) as ISpatialReference;

                // this determines the spatial reference as defined from the gp environment settings and the initial wgs84 SR
                ISpatialReference downloadSpatialReference = gpUtilities3.GetGPSpRefEnv(envMgr, wgs84, newExtent, 0, 0, 0, 0, null);

                downloadEnvelope.Project(wgs84);

                Marshal.ReleaseComObject(wgs84);
                Marshal.ReleaseComObject(spatialReferenceFactory);

                HttpWebRequest httpClient;
                System.Xml.Serialization.XmlSerializer serializer = null;
                serializer = new XmlSerializer(typeof(osm));

                // get the capabilities from the server
                HttpWebResponse httpResponse = null;

                api apiCapabilities = null;
                CultureInfo enUSCultureInfo = new CultureInfo("en-US");

#if DEBUG
                Console.WriteLine("Debbuging");
                message.AddMessage("Debugging...");
#endif

                message.AddMessage(resourceManager.GetString("GPTools_OSMGPDownload_startingDownloadRequest"));

                try
                {
                    httpClient = HttpWebRequest.Create(baseURLString.Value + "/api/capabilities") as HttpWebRequest;
                    httpClient = AssignProxyandCredentials(httpClient);

                    httpResponse = httpClient.GetResponse() as HttpWebResponse;

                    osm osmCapabilities = null;

                    Stream stream = httpResponse.GetResponseStream();

                    XmlTextReader xmlReader = new XmlTextReader(stream);
                    osmCapabilities = serializer.Deserialize(xmlReader) as osm;
                    xmlReader.Close();

                    apiCapabilities = osmCapabilities.Items[0] as api;

                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                    message.AddError(120009, ex.Message);

                    if (ex is WebException)
                    {
                        WebException webException = ex as WebException;
                        string serverErrorMessage = webException.Response.Headers["Error"];
                        if (!String.IsNullOrEmpty(serverErrorMessage))
                        {
                            message.AddError(120009, serverErrorMessage);
                        }
                    }

                }
                finally
                {
                    if (httpResponse != null)
                    {
                        httpResponse.Close();
                    }
                    httpClient = null;
                }

                if (apiCapabilities != null)
                {
                    // check for the extent
                    double roiArea = ((IArea)downloadEnvelope).Area;
                    double capabilitiyArea = Convert.ToDouble(apiCapabilities.area.maximum, new CultureInfo("en-US"));

                    if (roiArea > capabilitiyArea)
                    {
                        message.AddAbort(resourceManager.GetString("GPTools_OSMGPDownload_exceedDownloadROI"));
                        return;
                    }
                }

                // check for user interruption
                if (TrackCancel.Continue() == false)
                {
                    return;
                }

                // list containing either only one document for a single bbox request or multiple if relation references need to be resolved
                List<string> downloadedOSMDocuments = new List<string>();

                string requestURL = baseURLString.Value + "/api/0.6/map?bbox=" + downloadEnvelope.XMin.ToString("f5", enUSCultureInfo) + "," + downloadEnvelope.YMin.ToString("f5", enUSCultureInfo) + "," + downloadEnvelope.XMax.ToString("f5", enUSCultureInfo) + "," + downloadEnvelope.YMax.ToString("f5", enUSCultureInfo);
                string osmMasterDocument = downloadOSMDocument(ref message, requestURL, apiCapabilities);

                // check if the initial request was successfull
                // it might have failed at this point because too many nodes were requested or because of something else
                if (String.IsNullOrEmpty(osmMasterDocument))
                {
                    message.AddAbort(resourceManager.GetString("GPTools_OSMGPDownload_noValidOSMResponse"));
                    return;
                }

                // add the "master document" ) original bbox request to the list
                downloadedOSMDocuments.Add(osmMasterDocument);

                if (includeAllReferencesGPValue.Value)
                {
                    List<string> nodeList = new List<string>();
                    List<string> wayList = new List<string>();
                    List<string> relationList = new List<string>();
                    // check for user interruption
                    if (TrackCancel.Continue() == false)
                    {
                        return;
                    }

                    parseOSMDocument(osmMasterDocument, ref message, ref nodeList, ref wayList, ref relationList, ref downloadedOSMDocuments, baseURLString.Value, apiCapabilities);
                }

                string metadataAbstract = resourceManager.GetString("GPTools_OSMGPDownload_metadata_abstract");
                string metadataPurpose = resourceManager.GetString("GPTools_OSMGPDownload_metadata_purpose");

                IGPParameter targetDatasetParameter = paramvalues.get_Element(out_targetDatasetNumber) as IGPParameter;
                IDEDataset2 targetDEDataset2 = gpUtilities3.UnpackGPValue(targetDatasetParameter) as IDEDataset2;
                IGPValue targetDatasetGPValue = gpUtilities3.UnpackGPValue(targetDatasetParameter);
                string targetDatasetName = ((IGPValue)targetDEDataset2).GetAsText();

                IDataElement targetDataElement = targetDEDataset2 as IDataElement;
                IDataset targetDataset = gpUtilities3.OpenDatasetFromLocation(targetDataElement.CatalogPath);

                IName parentName = null;

                try
                {
                    parentName = gpUtilities3.CreateParentFromCatalogPath(targetDataElement.CatalogPath);
                }
                catch
                {
                    message.AddError(120033, resourceManager.GetString("GPTools_OSMGPFileReader_unable_to_create_fd"));
                    return;
                }

                // test if the feature classes already exists, 
                // if they do and the environments settings are such that an overwrite is not allowed we need to abort at this point
                IGeoProcessorSettings gpSettings = (IGeoProcessorSettings)envMgr;
                if (gpSettings.OverwriteOutput == true)
                {
                }

                else
                {
                    if (gpUtilities3.Exists((IGPValue)targetDEDataset2) == true)
                    {
                        message.AddError(120010, String.Format(resourceManager.GetString("GPTools_OSMGPDownload_basenamealreadyexists"), targetDataElement.Name));
                        return;
                    }
                }

                string Container = "";
                IDEUtilities deUtilities = new DEUtilitiesClass();
                deUtilities.ParseContainer(targetDataElement.CatalogPath, ref Container);

                IFeatureWorkspace featureWorkspace = gpUtilities3.OpenFromString(Container) as IFeatureWorkspace;

                if (featureWorkspace == null)
                {
                    message.AddError(120011, String.Format(resourceManager.GetString("GPTools_OSMGPDownload_nofeatureworkspace"), Container));
                    return;
                }

                // load the descriptions from which to derive the domain values
                OSMDomains availableDomains = null;

                System.Xml.XmlTextReader reader = null;
                try
                {
                    if (File.Exists(m_editorConfigurationSettings["osmdomainsfilepath"]))
                    {
                        reader = new System.Xml.XmlTextReader(m_editorConfigurationSettings["osmdomainsfilepath"]);
                    }
                }
                // If is in the server and hasn't been install all the configuration files
                catch
                {
                    if (File.Exists(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetAssembly(typeof(OSMGPDownload)).Location), "osm_domains.xml")))
                    {
                        reader = new System.Xml.XmlTextReader(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetAssembly(typeof(OSMGPDownload)).Location), "osm_domains.xml"));
                    }
                }

                if (reader == null)
                {
                    message.AddError(120012, resourceManager.GetString("GPTools_OSMGPDownload_NoDomainConfigFile"));
                    return;
                }

                try
                {
                    serializer = new XmlSerializer(typeof(OSMDomains));
                    availableDomains = serializer.Deserialize(reader) as OSMDomains;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                    System.Diagnostics.Debug.WriteLine(ex.StackTrace);
                    message.AddError(120013, ex.Message);
                    return;
                }

                #region define and add domains to the workspace
                // we are using domains to guide the edit templates in the editor for ArcGIS desktop
                Dictionary<string, IDomain> codedValueDomains = new Dictionary<string, IDomain>();

                foreach (var domain in availableDomains.domain)
                {
                    ICodedValueDomain pointCodedValueDomain = new CodedValueDomainClass();
                    ((IDomain)pointCodedValueDomain).Name = domain.name + "_pt";
                    ((IDomain)pointCodedValueDomain).FieldType = esriFieldType.esriFieldTypeString;

                    ICodedValueDomain lineCodedValueDomain = new CodedValueDomainClass();
                    ((IDomain)lineCodedValueDomain).Name = domain.name + "_ln";
                    ((IDomain)lineCodedValueDomain).FieldType = esriFieldType.esriFieldTypeString;

                    ICodedValueDomain polygonCodedValueDomain = new CodedValueDomainClass();
                    ((IDomain)polygonCodedValueDomain).Name = domain.name + "_ply";
                    ((IDomain)polygonCodedValueDomain).FieldType = esriFieldType.esriFieldTypeString;

                    for (int i = 0; i < domain.domainvalue.Length; i++)
                    {
                        for (int domainGeometryIndex = 0; domainGeometryIndex < domain.domainvalue[i].geometrytype.Length; domainGeometryIndex++)
                        {
                            switch (domain.domainvalue[i].geometrytype[domainGeometryIndex])
                            {
                                case geometrytype.point:
                                    pointCodedValueDomain.AddCode(domain.domainvalue[i].value, domain.domainvalue[i].value);
                                    break;
                                case geometrytype.line:
                                    lineCodedValueDomain.AddCode(domain.domainvalue[i].value, domain.domainvalue[i].value);
                                    break;
                                case geometrytype.polygon:
                                    polygonCodedValueDomain.AddCode(domain.domainvalue[i].value, domain.domainvalue[i].value);
                                    break;
                                default:
                                    break;
                            }
                        }
                    }

                    // add the domain tables to the domains collection
                    codedValueDomains.Add(((IDomain)pointCodedValueDomain).Name, (IDomain)pointCodedValueDomain);
                    codedValueDomains.Add(((IDomain)lineCodedValueDomain).Name, (IDomain)lineCodedValueDomain);
                    codedValueDomains.Add(((IDomain)polygonCodedValueDomain).Name, (IDomain)polygonCodedValueDomain);
                }

                IWorkspaceDomains workspaceDomain = featureWorkspace as IWorkspaceDomains;
                foreach (var domain in codedValueDomains.Values)
                {
                    IDomain testDomain = null;
                    try
                    {
                        testDomain = workspaceDomain.get_DomainByName(domain.Name);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(ex.Message);
                        System.Diagnostics.Debug.WriteLine(ex.StackTrace);
                    }

                    if (testDomain == null)
                    {
                        workspaceDomain.AddDomain(domain);
                    }
                }
                #endregion

                IGPEnvironment configKeyword = OSMToolHelper.getEnvironment(envMgr, "configKeyword");
                IGPString gpString = null;
                if (configKeyword != null)
                    gpString = configKeyword.Value as IGPString;

                string storageKeyword = String.Empty;

                if (gpString != null)
                {
                    storageKeyword = gpString.Value;
                }

                IFeatureDataset targetFeatureDataset = null;
                if (gpUtilities3.Exists((IGPValue)targetDEDataset2))
                {
                    targetFeatureDataset = gpUtilities3.OpenDataset((IGPValue)targetDEDataset2) as IFeatureDataset;
                }
                else
                {
                    targetFeatureDataset = featureWorkspace.CreateFeatureDataset(targetDataElement.Name, downloadSpatialReference);
                }


                ESRI.ArcGIS.esriSystem.UID osmClassExtensionUID = new ESRI.ArcGIS.esriSystem.UIDClass();
                //GUID for the OSM feature class extension
                osmClassExtensionUID.Value = "{65CA4847-8661-45eb-8E1E-B2985CA17C78}";


                downloadSpatialReference = ((IGeoDataset)targetFeatureDataset).SpatialReference;
                OSMToolHelper osmToolHelper = new OSMToolHelper();

                #region create point/line/polygon feature classes and tables
                // points
                try
                {
                    osmPointFeatureClass = osmToolHelper.CreatePointFeatureClass((IWorkspace2)featureWorkspace, targetFeatureDataset, targetDataElement.Name + "_osm_pt", null, null, osmClassExtensionUID, storageKeyword, availableDomains, metadataAbstract, metadataPurpose);
                }
                catch (Exception ex)
                {
                    message.AddError(120014, String.Format(resourceManager.GetString("GPTools_OSMGPDownload_nullpointfeatureclass"), ex.Message));
                    return;
                }

                if (osmPointFeatureClass == null)
                {
                    return;
                }

                // change the property set of the osm class extension to skip any change detection during the initial data load
                osmPointFeatureClass.RemoveOSMClassExtension();

                // lines
                try
                {
                    osmLineFeatureClass = osmToolHelper.CreateLineFeatureClass((IWorkspace2)featureWorkspace, targetFeatureDataset, targetDataElement.Name + "_osm_ln", null, null, osmClassExtensionUID, storageKeyword, availableDomains, metadataAbstract, metadataPurpose);
                }
                catch (Exception ex)
                {
                    message.AddError(120015, String.Format(resourceManager.GetString("GPTools_OSMGPDownload_nulllinefeatureclass"), ex.Message));
                    return;
                }

                if (osmLineFeatureClass == null)
                {
                    return;
                }

                // change the property set of the osm class extension to skip any change detection during the initial data load
                osmLineFeatureClass.RemoveOSMClassExtension();

                // polygons
                try
                {
                    osmPolygonFeatureClass = osmToolHelper.CreatePolygonFeatureClass((IWorkspace2)featureWorkspace, targetFeatureDataset, targetDataElement.Name + "_osm_ply", null, null, osmClassExtensionUID, storageKeyword, availableDomains, metadataAbstract, metadataPurpose);
                }
                catch (Exception ex)
                {
                    message.AddError(120016, String.Format(resourceManager.GetString("GPTools_OSMGPDownload_nullpolygonfeatureclass"), ex.Message));
                    return;
                }

                if (osmPolygonFeatureClass == null)
                {
                    return;
                }

                // change the property set of the osm class extension to skip any change detection during the initial data load
                osmPolygonFeatureClass.RemoveOSMClassExtension();

                // relation table
                ITable relationTable = null;

                try
                {
                    relationTable = osmToolHelper.CreateRelationTable((IWorkspace2)featureWorkspace, targetDataElement.Name + "_osm_relation", null, storageKeyword, metadataAbstract, metadataPurpose);
                }
                catch (Exception ex)
                {
                    message.AddError(120017, String.Format(resourceManager.GetString("GPTools_OSMGPDownload_nullrelationtable"), ex.Message));
                    return;
                }

                if (relationTable == null)
                {
                    return;
                }

                // revision table 
                ITable revisionTable = null;

                try
                {
                    revisionTable = osmToolHelper.CreateRevisionTable((IWorkspace2)featureWorkspace, targetDataElement.Name + "_osm_revision", null, storageKeyword);
                }
                catch (Exception ex)
                {
                    message.AddError(120018, String.Format(resourceManager.GetString("GPTools_OSMGPDownload_nullrelationtable"), ex.Message));
                    return;
                }

                if (revisionTable == null)
                {
                    return;
                }

                // check for user interruption
                if (TrackCancel.Continue() == false)
                {
                    return;
                }
                #endregion

                #region clean any existing data from loading targets
                ESRI.ArcGIS.Geoprocessing.IGeoProcessor2 gp = new ESRI.ArcGIS.Geoprocessing.GeoProcessorClass();
                IGeoProcessorResult gpResult = new GeoProcessorResultClass();

                try
                {
                    IVariantArray truncateParameters = new VarArrayClass();
                    truncateParameters.Add(((IWorkspace)featureWorkspace).PathName + "\\" + targetDataElement.Name + "\\" + targetDataElement.Name + "_osm_pt");
                    gpResult = gp.Execute("TruncateTable_management", truncateParameters, TrackCancel);

                    truncateParameters = new VarArrayClass();
                    truncateParameters.Add(((IWorkspace)featureWorkspace).PathName + "\\" + targetDataElement.Name + "\\" + targetDataElement.Name + "_osm_ln");
                    gpResult = gp.Execute("TruncateTable_management", truncateParameters, TrackCancel);

                    truncateParameters = new VarArrayClass();
                    truncateParameters.Add(((IWorkspace)featureWorkspace).PathName + "\\" + targetDataElement.Name + "\\" + targetDataElement.Name + "_osm_ply");
                    gpResult = gp.Execute("TruncateTable_management", truncateParameters, TrackCancel);

                    truncateParameters = new VarArrayClass();
                    truncateParameters.Add(((IWorkspace)featureWorkspace).PathName + "\\" + targetDataElement.Name + "_osm_relation");
                    gpResult = gp.Execute("TruncateTable_management", truncateParameters, TrackCancel);

                    truncateParameters = new VarArrayClass();
                    truncateParameters.Add(((IWorkspace)featureWorkspace).PathName + "\\" + targetDataElement.Name + "_osm_revision");
                    gpResult = gp.Execute("TruncateTable_management", truncateParameters, TrackCancel);
                }
                catch (Exception ex)
                {
                    message.AddWarning(ex.Message);
                }
                #endregion

                Dictionary<string, OSMToolHelper.simplePointRef> osmNodeDictionary = null;

                foreach (string osmDownloadDocument in downloadedOSMDocuments.Reverse<string>())
                {
                    long nodeCapacity = 0;
                    long wayCapacity = 0;
                    long relationCapacity = 0;

                    message.AddMessage(resourceManager.GetString("GPTools_OSMGPFileReader_countingNodes"));

                    osmToolHelper.countOSMStuff(osmDownloadDocument, ref nodeCapacity, ref wayCapacity, ref relationCapacity, ref TrackCancel);
                    message.AddMessage(String.Format(resourceManager.GetString("GPTools_OSMGPFileReader_countedElements"), nodeCapacity, wayCapacity, relationCapacity));

                    if (osmNodeDictionary == null)
                        osmNodeDictionary = new Dictionary<string, OSMToolHelper.simplePointRef>(Convert.ToInt32(nodeCapacity));

                    #region load points
                    osmToolHelper.loadOSMNodes(osmDownloadDocument, ref TrackCancel, ref message, targetDatasetGPValue, osmPointFeatureClass, false, false, Convert.ToInt32(nodeCapacity), ref osmNodeDictionary, featureWorkspace, downloadSpatialReference, availableDomains, false);
                    #endregion

                    if (TrackCancel.Continue() == false)
                    {
                        return;
                    }

                    #region load ways
                    if (wayCapacity > 0)
                    {
                        List<string> missingWays = null;
                        missingWays = osmToolHelper.loadOSMWays(osmDownloadDocument, ref TrackCancel, ref message, targetDatasetGPValue, osmPointFeatureClass, osmLineFeatureClass, osmPolygonFeatureClass, false, false, Convert.ToInt32(wayCapacity), ref osmNodeDictionary, featureWorkspace, downloadSpatialReference, availableDomains, false);
                    }
                    #endregion

                    if (TrackCancel.Continue() == false)
                    {
                        return;
                    }

                    # region for conserve memory condition, update refcount
                    int refCounterFieldIndex = osmPointFeatureClass.Fields.FindField("wayRefCount");
                    if (refCounterFieldIndex > -1)
                    {
                        foreach (var refNode in osmNodeDictionary)
                        {
                            try
                            {
                                IFeature updateFeature = osmPointFeatureClass.GetFeature(refNode.Value.pointObjectID);

                                int refCount = refNode.Value.RefCounter;
                                if (refCount == 0)
                                {
                                    refCount = 1;
                                }

                                updateFeature.set_Value(refCounterFieldIndex, refCount);
                                updateFeature.Store();
                            }
                            catch { }
                        }
                    }

                    #endregion

                    // check for user interruption
                    if (TrackCancel.Continue() == false)
                    {
                        return;
                    }
                    ESRI.ArcGIS.Geoprocessor.Geoprocessor geoProcessor = new ESRI.ArcGIS.Geoprocessor.Geoprocessor();

                    #region for local geodatabases enforce spatial integrity
                    bool storedOriginal = geoProcessor.AddOutputsToMap;
                    geoProcessor.AddOutputsToMap = false;

                    try
                    {
                        if (osmLineFeatureClass != null)
                        {
                            if (((IDataset)osmLineFeatureClass).Workspace.Type == esriWorkspaceType.esriLocalDatabaseWorkspace)
                            {
                                IVariantArray lineRepairParameters = new VarArrayClass();
                                lineRepairParameters.Add(((IWorkspace)featureWorkspace).PathName + "\\" + targetDataElement.Name + "\\" + targetDataElement.Name + "_osm_ln");
                                lineRepairParameters.Add("DELETE_NULL");

                                IGeoProcessorResult2 gpResults = gp.Execute("RepairGeometry_management", lineRepairParameters, TrackCancel) as IGeoProcessorResult2;
                                message.AddMessages(gpResults.GetResultMessages());
                            }
                        }

                        if (osmPolygonFeatureClass != null)
                        {
                            if (((IDataset)osmPolygonFeatureClass).Workspace.Type == esriWorkspaceType.esriLocalDatabaseWorkspace)
                            {
                                IVariantArray polygonRepairParameters = new VarArrayClass();
                                polygonRepairParameters.Add(((IWorkspace)featureWorkspace).PathName + "\\" + targetDataElement.Name + "\\" + targetDataElement.Name + "_osm_ply");
                                polygonRepairParameters.Add("DELETE_NULL");

                                IGeoProcessorResult2 gpResults = gp.Execute("RepairGeometry_management", polygonRepairParameters, TrackCancel) as IGeoProcessorResult2;
                                message.AddMessages(gpResults.GetResultMessages());
                            }
                        }
                    }
                    catch
                    {
                        message.AddWarning(resourceManager.GetString("GPTools_OSMGPDownload_repairgeometryfailure"));
                    }
                    geoProcessor.AddOutputsToMap = storedOriginal;
                    #endregion



                    #region load relations
                    if (relationCapacity > 0)
                    {
                        List<string> missingRelations = null;
                        missingRelations = osmToolHelper.loadOSMRelations(osmDownloadDocument, ref TrackCancel, ref message, targetDatasetGPValue, osmPointFeatureClass, osmLineFeatureClass, osmPolygonFeatureClass, Convert.ToInt32(relationCapacity), relationTable, availableDomains, false, false);
                    }
                    #endregion
                }

                #region update the references counts and member lists for nodes
                message.AddMessage(resourceManager.GetString("GPTools_OSMGPFileReader_updatereferences"));
                IFeatureCursor pointUpdateCursor = null;

                using (SchemaLockManager ptLockManager = new SchemaLockManager(osmPointFeatureClass as ITable))
                {
                    using (ComReleaser comReleaser = new ComReleaser())
                    {
                        int updateCount = 0;
                        pointUpdateCursor = osmPointFeatureClass.Update(null, false);
                        updateCount = ((ITable)osmPointFeatureClass).RowCount(null);

                        IStepProgressor stepProgressor = TrackCancel as IStepProgressor;

                        if (stepProgressor != null)
                        {
                            stepProgressor.MinRange = 0;
                            stepProgressor.MaxRange = updateCount;
                            stepProgressor.Position = 0;
                            stepProgressor.Message = resourceManager.GetString("GPTools_OSMGPFileReader_updatepointrefcount");
                            stepProgressor.StepValue = 1;
                            stepProgressor.Show();
                        }

                        comReleaser.ManageLifetime(pointUpdateCursor);

                        IFeature pointFeature = pointUpdateCursor.NextFeature();

                        int osmPointIDFieldIndex = osmPointFeatureClass.FindField("OSMID");
                        int osmWayRefCountFieldIndex = osmPointFeatureClass.FindField("wayRefCount");
                        int positionCounter = 0;
                        while (pointFeature != null)
                        {
                            positionCounter++;
                            string nodeID = Convert.ToString(pointFeature.get_Value(osmPointIDFieldIndex));

                            // let get the reference counter from the internal node dictionary
                            if (osmNodeDictionary[nodeID].RefCounter == 0)
                            {
                                pointFeature.set_Value(osmWayRefCountFieldIndex, 1);
                            }
                            else
                            {
                                pointFeature.set_Value(osmWayRefCountFieldIndex, osmNodeDictionary[nodeID].RefCounter);
                            }

                            pointUpdateCursor.UpdateFeature(pointFeature);

                            if (pointFeature != null)
                                Marshal.ReleaseComObject(pointFeature);

                            pointFeature = pointUpdateCursor.NextFeature();

                            if (stepProgressor != null)
                            {
                                stepProgressor.Position = positionCounter;
                            }
                        }

                        if (stepProgressor != null)
                        {
                            stepProgressor.Hide();
                        }
                    }
                }
                #endregion

                // clean all the downloaded OSM files
                foreach (string osmFile in downloadedOSMDocuments)
                {
                    if (File.Exists(osmFile))
                    {
                        try
                        {
                            File.Delete(osmFile);
                        }
                        catch { }
                    }
                }

                SyncState.StoreLastSyncTime(targetDatasetName, syncTime);

                gpUtilities3 = new GPUtilitiesClass() as IGPUtilities3;

                // repackage the feature class into their respective gp values
                IGPParameter pointFeatureClassParameter = paramvalues.get_Element(out_osmPointsNumber) as IGPParameter;
                IGPValue pointFeatureClassPackGPValue = gpUtilities3.UnpackGPValue(pointFeatureClassParameter);
                gpUtilities3.PackGPValue(pointFeatureClassPackGPValue, pointFeatureClassParameter);

                IGPParameter lineFeatureClassParameter = paramvalues.get_Element(out_osmLinesNumber) as IGPParameter;
                IGPValue lineFeatureClassPackGPValue = gpUtilities3.UnpackGPValue(lineFeatureClassParameter);
                gpUtilities3.PackGPValue(lineFeatureClassPackGPValue, lineFeatureClassParameter);

                IGPParameter polygonFeatureClassParameter = paramvalues.get_Element(out_osmPolygonsNumber) as IGPParameter;
                IGPValue polygon1FeatureClassPackGPValue = gpUtilities3.UnpackGPValue(polygonFeatureClassParameter);
                gpUtilities3.PackGPValue(polygon1FeatureClassPackGPValue, polygonFeatureClassParameter);

                gpUtilities3.ReleaseInternals();
                Marshal.ReleaseComObject(gpUtilities3);

                Marshal.ReleaseComObject(baseURLString);
                Marshal.ReleaseComObject(downloadExtentGPValue);
                Marshal.ReleaseComObject(downloadEnvelope);
                Marshal.ReleaseComObject(includeAllReferences);
                Marshal.ReleaseComObject(downloadSpatialReference);

                if (osmToolHelper != null)
                    osmToolHelper = null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
                message.AddError(120019, ex.Message);
            }
            finally
            {
                try
                {
                    if (osmPointFeatureClass != null)
                    {
                        osmPointFeatureClass.ApplyOSMClassExtension();
                        Marshal.ReleaseComObject(osmPointFeatureClass);
                    }

                    if (osmLineFeatureClass != null)
                    {
                        osmLineFeatureClass.ApplyOSMClassExtension();
                        Marshal.ReleaseComObject(osmLineFeatureClass);
                    }

                    if (osmPolygonFeatureClass != null)
                    {
                        osmPolygonFeatureClass.ApplyOSMClassExtension();
                        Marshal.ReleaseComObject(osmPolygonFeatureClass);
                    }
                }
                catch (Exception ex)
                {
                    message.AddError(120020, ex.ToString());
                }
            }
        }

        private string downloadOSMDocument(ref IGPMessages message, string requestURL, api apiCapabilities)
        {
            string osmDownloadDocument = String.Empty;
            HttpWebResponse httpResponse = null;

            try
            {

                // OSM does not understand URL encoded query parameters
                HttpWebRequest httpClient = HttpWebRequest.Create(requestURL) as HttpWebRequest;
                httpClient = AssignProxyandCredentials(httpClient);

                // read the timeout parameter
                int secondsToTimeout = Convert.ToInt32(apiCapabilities.timeout.seconds);
                httpClient.Timeout = secondsToTimeout * 1000;

                httpResponse = httpClient.GetResponse() as HttpWebResponse;

                osmDownloadDocument = System.IO.Path.GetTempFileName();

                using (System.IO.FileStream fileStream = new System.IO.FileStream(osmDownloadDocument, FileMode.Append, FileAccess.Write))
                {
                    using (StreamReader streamReader = new StreamReader(httpResponse.GetResponseStream()))
                    {
                        UTF8Encoding encoding = new UTF8Encoding();

                        byte[] byteBuffer = encoding.GetBytes(streamReader.ReadToEnd());
                        fileStream.Write(byteBuffer, 0, byteBuffer.Length);
                    }
                }

            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
                message.AddError(120009, ex.Message);

                if (ex is WebException)
                {
                    WebException webException = ex as WebException;
                    if (webException != null)
                    {
                        if (webException.Response != null)
                        {
                            string serverErrorMessage = webException.Response.Headers["Error"];
                            if (!String.IsNullOrEmpty(serverErrorMessage))
                            {
                                message.AddError(120009, serverErrorMessage);
                            }
                        }
                    }
                }
            }
            finally
            {

                if (httpResponse != null)
                {
                    httpResponse.Close();
                }
            }
            return osmDownloadDocument;
        }

        static public HttpWebRequest AssignProxyandCredentials(HttpWebRequest httpClient)
        {
            if (httpClient == null)
                return httpClient;

            // read the network proxy settings from the IE settings - this includes proxy settings (static, dynamic) and credentials
            httpClient.UseDefaultCredentials = true;

            // user name and password are coming from registry settings written by ArcCatalog
            IProxyServerInfo2 proxyServerInfo = new ProxyServerInfoClass() as IProxyServerInfo2;

            if (proxyServerInfo == null)
                return httpClient;

            if (proxyServerInfo.Enabled)
                httpClient.Proxy.Credentials = new NetworkCredential(proxyServerInfo.UserName, proxyServerInfo.Password);

            return httpClient;
        }

        /// <summary>
        /// Checks if the baseURL can be used to retrieve the OSM server capabilities. All exceptions will be rethrown to the caller.
        /// </summary>
        /// <param name="baseURL"></param>
        /// <returns></returns>
        static public api CheckValidServerURL(string baseURL)
        {
            osm osmCapabilities = null;
            api apiCapabilities = null;

            // get the capabilities from the server
            HttpWebResponse httpResponse = null;
            HttpWebRequest httpClient = HttpWebRequest.Create(baseURL + "/api/capabilities") as HttpWebRequest;
            httpClient = AssignProxyandCredentials(httpClient);

            httpClient.Timeout = 10000;
            System.Xml.Serialization.XmlSerializer serializer = null;
            serializer = new XmlSerializer(typeof(osm));

            httpResponse = httpClient.GetResponse() as HttpWebResponse;

            Stream stream = httpResponse.GetResponseStream();

            XmlTextReader xmlReader = new XmlTextReader(stream);
            osmCapabilities = serializer.Deserialize(xmlReader) as osm;
            xmlReader.Close();

            apiCapabilities = osmCapabilities.Items[0] as api;


            return apiCapabilities;
        }

        private string retrieveAdditionalRelations(string osmID, ref ITrackCancel TrackCancel, ESRI.ArcGIS.Geodatabase.IGPMessages message, IGPString baseURLString, HttpWebRequest httpClient, ref HttpWebResponse httpResponse)
        {
            string osmDocumentLocation = String.Empty;

            try
            {
                if (TrackCancel.Continue() == false)
                {
                    return osmDocumentLocation;
                }

                httpClient = HttpWebRequest.Create(baseURLString.Value + "/api/0.6/relation/" + osmID + "/full") as HttpWebRequest;
                httpClient = AssignProxyandCredentials(httpClient);

                httpResponse = httpClient.GetResponse() as HttpWebResponse;

                osmDocumentLocation = System.IO.Path.GetTempFileName();

                using (System.IO.FileStream fileStream = new System.IO.FileStream(osmDocumentLocation, FileMode.Append, FileAccess.Write))
                {
                    using (StreamReader streamReader = new StreamReader(httpResponse.GetResponseStream()))
                    {
                        UTF8Encoding encoding = new UTF8Encoding();

                        byte[] byteBuffer = encoding.GetBytes(streamReader.ReadToEnd());
                        fileStream.Write(byteBuffer, 0, byteBuffer.Length);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
                message.AddWarning(ex.Message);

                if (ex is WebException)
                {
                    WebException webException = ex as WebException;
                    string serverErrorMessage = webException.Response.Headers["Error"];
                    if (!String.IsNullOrEmpty(serverErrorMessage))
                    {
                        message.AddWarning(serverErrorMessage);
                    }
                }

            }

            return osmDocumentLocation;
        }



        public ESRI.ArcGIS.esriSystem.IName FullName
        {
            get
            {
                IName fullName = null;

                if (osmGPFactory != null)
                {
                    fullName = osmGPFactory.GetFunctionName(OSMGPFactory.m_DownloadDataName) as IName;
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
                return "";
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
                string metadafile = "osmgpdownload.xml";

                try
                {
                    string[] languageid = System.Threading.Thread.CurrentThread.CurrentUICulture.Name.Split("-".ToCharArray());

                    string ArcGISInstallationLocation = OSMGPFactory.GetArcGIS10InstallLocation();
                    string localizedMetaDataFileShort = ArcGISInstallationLocation + System.IO.Path.DirectorySeparatorChar.ToString() + "help" + System.IO.Path.DirectorySeparatorChar.ToString() + "gp" + System.IO.Path.DirectorySeparatorChar.ToString() + "osmgpdownload_" + languageid[0] + ".xml";
                    string localizedMetaDataFileLong = ArcGISInstallationLocation + System.IO.Path.DirectorySeparatorChar.ToString() + "help" + System.IO.Path.DirectorySeparatorChar.ToString() + "gp" + System.IO.Path.DirectorySeparatorChar.ToString() + "osmgpdownload_" + System.Threading.Thread.CurrentThread.CurrentUICulture.Name + ".xml";

                    if (System.IO.File.Exists(localizedMetaDataFileShort))
                    {
                        metadafile = localizedMetaDataFileShort;
                    }
                    else if (System.IO.File.Exists(localizedMetaDataFileLong))
                    {
                        metadafile = localizedMetaDataFileLong;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                    System.Diagnostics.Debug.WriteLine(ex.StackTrace);
                }

                return metadafile;
            }
        }

        public string Name
        {
            get
            {
                return OSMGPFactory.m_DownloadDataName;
            }
        }

        public ESRI.ArcGIS.esriSystem.IArray ParameterInfo
        {
            get
            {
                //
                IArray parameterArray = new ArrayClass();

                // osm download URL (required)
                IGPParameterEdit3 downloadURL = new GPParameterClass() as IGPParameterEdit3;
                downloadURL.DataType = new GPStringTypeClass();
                downloadURL.Direction = esriGPParameterDirection.esriGPParameterDirectionInput;
                downloadURL.DisplayName = resourceManager.GetString("GPTools_OSMGPDownload_downloadURL_desc");
                downloadURL.Name = "in_osmURL";
                downloadURL.ParameterType = esriGPParameterType.esriGPParameterTypeRequired;

                IGPString urlDownloadString = new GPStringClass();
                if (m_editorConfigurationSettings != null)
                {
                    if (m_editorConfigurationSettings.ContainsKey("osmbaseurl"))
                    {
                        urlDownloadString.Value = m_editorConfigurationSettings["osmbaseurl"];
                    }
                }
                downloadURL.Value = urlDownloadString as IGPValue;


                // download extent (required)
                IGPParameterEdit3 downloadExtent = new GPParameterClass() as IGPParameterEdit3;
                downloadExtent.DataType = new GPExtentTypeClass();
                downloadExtent.Direction = esriGPParameterDirection.esriGPParameterDirectionInput;
                downloadExtent.DisplayName = resourceManager.GetString("GPTools_OSMGPDownload_downloadExtent_desc");
                downloadExtent.Name = "in_downloadextent";
                downloadExtent.ParameterType = esriGPParameterType.esriGPParameterTypeRequired;

                // target dataset (required)
                IGPParameterEdit3 targetDataset = new GPParameterClass() as IGPParameterEdit3;
                targetDataset.DataType = new DEFeatureDatasetTypeClass();
                targetDataset.Direction = esriGPParameterDirection.esriGPParameterDirectionOutput;
                targetDataset.DisplayName = resourceManager.GetString("GPTools_OSMGPDownload_targetDataset_desc");
                targetDataset.Name = "out_targetdataset";

                IGPDatasetDomain datasetDomain = new GPDatasetDomainClass();
                datasetDomain.AddType(esriDatasetType.esriDTFeatureDataset);

                targetDataset.Domain = datasetDomain as IGPDomain;

                // option to resolve all references included relations
                IGPParameterEdit3 includeReferences = new GPParameterClass() as IGPParameterEdit3;
                IGPCodedValueDomain includeReferenceDomain = new GPCodedValueDomainClass();

                IGPBoolean includeTrue = new GPBooleanClass();
                includeTrue.Value = true;
                IGPBoolean includeFalse = new GPBooleanClass();
                includeFalse.Value = false;

                includeReferenceDomain.AddCode((IGPValue)includeTrue, "INCLUDE_REFERENCES");
                includeReferenceDomain.AddCode((IGPValue)includeFalse, "DO_NOT_INCLUDE_REFERENCES");
                includeReferences.Domain = (IGPDomain)includeReferenceDomain;
                includeReferences.Value = (IGPValue)includeFalse;

                includeReferences.DataType = new GPBooleanTypeClass();
                includeReferences.Direction = esriGPParameterDirection.esriGPParameterDirectionInput;
                includeReferences.ParameterType = esriGPParameterType.esriGPParameterTypeOptional;
                includeReferences.DisplayName = resourceManager.GetString("GPTools_OSMGPDownload_includeReferences_desc");
                includeReferences.Name = "in_includereferences";

                IGPParameterEdit3 osmPoints = new GPParameterClass() as IGPParameterEdit3;
                osmPoints.DataType = new DEFeatureClassTypeClass();
                osmPoints.Direction = esriGPParameterDirection.esriGPParameterDirectionOutput;
                osmPoints.ParameterType = esriGPParameterType.esriGPParameterTypeRequired;
                osmPoints.Enabled = false;
                osmPoints.Category = resourceManager.GetString("GPTools_OSMGPDownload_outputCategory");
                osmPoints.DisplayName = resourceManager.GetString("GPTools_OSMGPDownload_osmPoints_desc");
                osmPoints.Name = "out_osmPoints";

                IGPFeatureClassDomain osmPointFeatureClassDomain = new GPFeatureClassDomainClass();
                osmPointFeatureClassDomain.AddFeatureType(esriFeatureType.esriFTSimple);
                osmPointFeatureClassDomain.AddType(ESRI.ArcGIS.Geometry.esriGeometryType.esriGeometryPoint);
                osmPoints.Domain = osmPointFeatureClassDomain as IGPDomain;

                IGPParameterEdit3 osmLines = new GPParameterClass() as IGPParameterEdit3;
                osmLines.DataType = new DEFeatureClassTypeClass();
                osmLines.Direction = esriGPParameterDirection.esriGPParameterDirectionOutput;
                osmLines.ParameterType = esriGPParameterType.esriGPParameterTypeRequired;
                osmLines.Category = resourceManager.GetString("GPTools_OSMGPDownload_outputCategory");
                osmLines.Enabled = false;
                osmLines.DisplayName = resourceManager.GetString("GPTools_OSMGPDownload_osmLine_desc");
                osmLines.Name = "out_osmLines";

                IGPFeatureClassDomain osmPolylineFeatureClassDomain = new GPFeatureClassDomainClass();
                osmPolylineFeatureClassDomain.AddFeatureType(esriFeatureType.esriFTSimple);
                osmPolylineFeatureClassDomain.AddType(ESRI.ArcGIS.Geometry.esriGeometryType.esriGeometryPolyline);
                osmLines.Domain = osmPolylineFeatureClassDomain as IGPDomain;

                IGPParameterEdit3 osmPolygons = new GPParameterClass() as IGPParameterEdit3;
                osmPolygons.DataType = new DEFeatureClassTypeClass();
                osmPolygons.Direction = esriGPParameterDirection.esriGPParameterDirectionOutput;
                osmPolygons.ParameterType = esriGPParameterType.esriGPParameterTypeRequired;
                osmPolygons.Category = resourceManager.GetString("GPTools_OSMGPDownload_outputCategory");
                osmPolygons.Enabled = false;
                osmPolygons.DisplayName = resourceManager.GetString("GPTools_OSMGPDownload_osmPolygon_desc");
                osmPolygons.Name = "out_osmPolygons";

                IGPFeatureClassDomain osmPolygonFeatureClassDomain = new GPFeatureClassDomainClass();
                osmPolygonFeatureClassDomain.AddFeatureType(esriFeatureType.esriFTSimple);
                osmPolygonFeatureClassDomain.AddType(ESRI.ArcGIS.Geometry.esriGeometryType.esriGeometryPolygon);
                osmPolygons.Domain = osmPolygonFeatureClassDomain as IGPDomain;


                parameterArray.Add(downloadURL);
                in_downloadURLNumber = 0;

                parameterArray.Add(downloadExtent);
                in_downloadExtentNumber = 1;

                parameterArray.Add(includeReferences);
                in_includeReferencesNumber = 2;

                parameterArray.Add(targetDataset);
                out_targetDatasetNumber = 3;

                parameterArray.Add(osmPoints);
                out_osmPointsNumber = 4;

                parameterArray.Add(osmLines);
                out_osmLinesNumber = 5;

                parameterArray.Add(osmPolygons);
                out_osmPolygonsNumber = 6;

                return parameterArray;
            }
        }

        public void UpdateMessages(ESRI.ArcGIS.esriSystem.IArray paramvalues, ESRI.ArcGIS.Geoprocessing.IGPEnvironmentManager pEnvMgr, ESRI.ArcGIS.Geodatabase.IGPMessages Messages)
        {
            IGPUtilities3 gpUtilities3 = new GPUtilitiesClass();

            // check for a valid download url
            IGPParameter downloadURLParameter = paramvalues.get_Element(in_downloadURLNumber) as IGPParameter;

            if (downloadURLParameter.HasBeenValidated == false)
            {
                IGPString downloadURLGPString = downloadURLParameter.Value as IGPString;


                if (downloadURLGPString != null)
                {
                    if (String.IsNullOrEmpty(downloadURLGPString.Value) == false)
                    {
                        try
                        {
                            Uri downloadURI = new Uri(downloadURLGPString.Value);


                            // attempt a download request from the given URL to get the server capabilities
                            m_osmAPICapabilities = CheckValidServerURL(downloadURLGPString.Value);

                            // if we can construct a valid URI  class then we are accepting the value and store it in the user settings as well
                            if (m_editorConfigurationSettings != null)
                            {
                                if (m_editorConfigurationSettings.ContainsKey("osmbaseurl"))
                                {
                                    m_editorConfigurationSettings["osmbaseurl"] = downloadURLGPString.Value;
                                }
                                else
                                {
                                    m_editorConfigurationSettings.Add("osmbaseurl", downloadURLGPString.Value);
                                }

                                OSMGPFactory.StoreOSMEditorSettings(m_editorConfigurationSettings);
                            }
                        }
                        catch (Exception ex)
                        {
                            StringBuilder errorMessage = new StringBuilder();
                            errorMessage.AppendLine(resourceManager.GetString("GPTools_OSMGPDownload_invaliddownloadurl"));
                            errorMessage.AppendLine(ex.Message);
                            Messages.ReplaceError(in_downloadURLNumber, -3, errorMessage.ToString());
                            m_osmAPICapabilities = null;
                        }
                    }
                }
            }


            if (m_osmAPICapabilities == null)
            {
                return;
            }

            // check for extent
            IGPParameter downloadExtentParameter = paramvalues.get_Element(in_downloadExtentNumber) as IGPParameter;

            if (downloadExtentParameter.HasBeenValidated == false)
            {
                IGPValue downloadExtent = gpUtilities3.UnpackGPValue(downloadExtentParameter);

                if (downloadExtent != null)
                {
                    esriGPExtentEnum gpExtent;
                    IEnvelope downloadEnvelope = gpUtilities3.GetExtent(downloadExtent, out gpExtent);

                    if (downloadEnvelope == null)
                        return;

                    if (downloadEnvelope.IsEmpty == true)
                        return;

                    ISpatialReferenceFactory spatialReferenceFactory = new SpatialReferenceEnvironmentClass() as ISpatialReferenceFactory;
                    ISpatialReference wgs84 = spatialReferenceFactory.CreateGeographicCoordinateSystem((int)esriSRGeoCSType.esriSRGeoCS_WGS1984) as ISpatialReference;

                    downloadEnvelope.Project(wgs84);

                    Marshal.ReleaseComObject(wgs84);
                    Marshal.ReleaseComObject(spatialReferenceFactory);

                    IArea downloadArea = downloadEnvelope as IArea;
                    double maximumAcceptableOSMArea = Convert.ToDouble(m_osmAPICapabilities.area.maximum, new CultureInfo("en-US"));

                    if (downloadArea.Area > maximumAcceptableOSMArea)
                    {
                        Messages.ReplaceError(in_downloadExtentNumber, -3, resourceManager.GetString("GPTools_OSMGPDownload_exceedDownloadROI"));
                    }
                }
            }

            // check for valid geodatabase path
            // if the user is pointing to a valid directory on disk, flag it as an error
            IGPParameter targetDatasetParameter = paramvalues.get_Element(out_targetDatasetNumber) as IGPParameter;
            IGPValue targetDatasetGPValue = gpUtilities3.UnpackGPValue(targetDatasetParameter);

            if (targetDatasetGPValue == null)
            {
                Messages.ReplaceError(out_targetDatasetNumber, -98, string.Format(resourceManager.GetString("GPTools_NullPointerParameterType"), targetDatasetParameter.Name));
            }


            if (targetDatasetGPValue.IsEmpty() == false)
            {
                if (System.IO.Directory.Exists(targetDatasetGPValue.GetAsText()))
                {
                    Messages.ReplaceError(out_targetDatasetNumber, -4, resourceManager.GetString("GPTools_OSMGPDownload_directory_is_not_target_dataset"));
                }
            }

            // check one of the output feature classes for version compatibility
            IGPParameter pointFeatureClassParameter = paramvalues.get_Element(out_osmPointsNumber) as IGPParameter;
            IDEFeatureClass pointDEFeatureClass = gpUtilities3.UnpackGPValue(pointFeatureClassParameter) as IDEFeatureClass;

            if (pointDEFeatureClass != null)
            {
                if (((IGPValue)pointDEFeatureClass).IsEmpty() == false)
                {
                    if (gpUtilities3.Exists((IGPValue)pointDEFeatureClass))
                    {
                        IFeatureClass ptfc = gpUtilities3.Open(gpUtilities3.UnpackGPValue(pointFeatureClassParameter)) as IFeatureClass;
                        IPropertySet osmExtensionPropertySet = ptfc.ExtensionProperties;

                        if (osmExtensionPropertySet == null)
                        {
                            Messages.ReplaceError(out_targetDatasetNumber, -5, string.Format(resourceManager.GetString("GPTools_IncompatibleExtensionVersion"), 1, OSMClassExtensionManager.Version));
                            Messages.ReplaceError(out_osmPointsNumber, -5, string.Format(resourceManager.GetString("GPTools_IncompatibleExtensionVersion"), 1, OSMClassExtensionManager.Version));
                            Messages.ReplaceError(out_osmLinesNumber, -5, string.Format(resourceManager.GetString("GPTools_IncompatibleExtensionVersion"), 1, OSMClassExtensionManager.Version));
                            Messages.ReplaceError(out_osmPolygonsNumber, -5, string.Format(resourceManager.GetString("GPTools_IncompatibleExtensionVersion"), 1, OSMClassExtensionManager.Version));
                        }
                        else
                        {
                            try
                            {
                                int extensionVersion = Convert.ToInt32(osmExtensionPropertySet.GetProperty("VERSION"));

                                object names;
                                object values;
                                osmExtensionPropertySet.GetAllProperties(out names, out values);

                                if (extensionVersion != OSMClassExtensionManager.Version)
                                {
                                    Messages.ReplaceError(out_targetDatasetNumber, -5, string.Format(resourceManager.GetString("GPTools_IncompatibleExtensionVersion"), extensionVersion, OSMClassExtensionManager.Version));
                                    Messages.ReplaceError(out_osmPointsNumber, -5, string.Format(resourceManager.GetString("GPTools_IncompatibleExtensionVersion"), extensionVersion, OSMClassExtensionManager.Version));
                                    Messages.ReplaceError(out_osmLinesNumber, -5, string.Format(resourceManager.GetString("GPTools_IncompatibleExtensionVersion"), extensionVersion, OSMClassExtensionManager.Version));
                                    Messages.ReplaceError(out_osmPolygonsNumber, -5, string.Format(resourceManager.GetString("GPTools_IncompatibleExtensionVersion"), extensionVersion, OSMClassExtensionManager.Version));
                                }
                            }
                            catch
                            {
                                Messages.ReplaceError(out_targetDatasetNumber, -5, string.Format(resourceManager.GetString("GPTools_IncompatibleExtensionVersion"), 1, OSMClassExtensionManager.Version));
                                Messages.ReplaceError(out_osmPointsNumber, -5, string.Format(resourceManager.GetString("GPTools_IncompatibleExtensionVersion"), 1, OSMClassExtensionManager.Version));
                                Messages.ReplaceError(out_osmLinesNumber, -5, string.Format(resourceManager.GetString("GPTools_IncompatibleExtensionVersion"), 1, OSMClassExtensionManager.Version));
                                Messages.ReplaceError(out_osmPolygonsNumber, -5, string.Format(resourceManager.GetString("GPTools_IncompatibleExtensionVersion"), 1, OSMClassExtensionManager.Version));
                            }
                        }
                    }
                }
            }
        }

        public void UpdateParameters(ESRI.ArcGIS.esriSystem.IArray paramvalues, ESRI.ArcGIS.Geoprocessing.IGPEnvironmentManager pEnvMgr)
        {
            IGPUtilities3 gpUtilities3 = new GPUtilitiesClass();

            IGPParameter targetDatasetParameter = paramvalues.get_Element(out_targetDatasetNumber) as IGPParameter;
            IGPValue targetDatasetGPValue = gpUtilities3.UnpackGPValue(targetDatasetParameter);

            if (targetDatasetGPValue == null)
                return;

            if (targetDatasetGPValue.GetAsText().Length == 0)
            {
                return;
            }

            IDEFeatureDataset targetDEFeatureDataset = targetDatasetGPValue as IDEFeatureDataset;

            if (targetDEFeatureDataset == null)
                return;

            IDataElement dataElement = targetDEFeatureDataset as IDataElement;

            if (dataElement == null)
                return;

            string nameOfPointFeatureClass = dataElement.GetBaseName() + "_osm_pt";
            string nameOfLineFeatureClass = dataElement.GetBaseName() + "_osm_ln";
            string nameOfPolygonFeatureClass = dataElement.GetBaseName() + "_osm_ply";

            string outpointsPath = dataElement.CatalogPath + System.IO.Path.DirectorySeparatorChar + nameOfPointFeatureClass;
            IGPParameter outPointsFeatureClassParameter = paramvalues.get_Element(out_osmPointsNumber) as IGPParameter;
            IGPValue outPointsFeatureClass = gpUtilities3.UnpackGPValue(outPointsFeatureClassParameter);
            outPointsFeatureClass.SetAsText(outpointsPath);
            gpUtilities3.PackGPValue(outPointsFeatureClass, outPointsFeatureClassParameter);

            string outlinesPath = dataElement.CatalogPath + System.IO.Path.DirectorySeparatorChar + nameOfLineFeatureClass;
            IGPParameter outLinesFeatureClassParameter = paramvalues.get_Element(out_osmLinesNumber) as IGPParameter;
            IGPValue outLinesFeatureClass = gpUtilities3.UnpackGPValue(outLinesFeatureClassParameter);
            outLinesFeatureClass.SetAsText(outlinesPath);
            gpUtilities3.PackGPValue(outLinesFeatureClass, outLinesFeatureClassParameter);

            string outpolygonsPath = dataElement.CatalogPath + System.IO.Path.DirectorySeparatorChar + nameOfPolygonFeatureClass;
            IGPParameter outPolygonFeatureClassParameter = paramvalues.get_Element(out_osmPolygonsNumber) as IGPParameter;
            IGPValue outPolygonFeatureClass = gpUtilities3.UnpackGPValue(outPolygonFeatureClassParameter);
            outPolygonFeatureClass.SetAsText(outpolygonsPath);
            gpUtilities3.PackGPValue(outPolygonFeatureClass, outPolygonFeatureClassParameter);

        }

        public ESRI.ArcGIS.Geodatabase.IGPMessages Validate(ESRI.ArcGIS.esriSystem.IArray paramvalues, bool updateValues, ESRI.ArcGIS.Geoprocessing.IGPEnvironmentManager envMgr)
        {
            return default(ESRI.ArcGIS.Geodatabase.IGPMessages);
        }
        #endregion

        private void parseOSMDocument(string osmFileLocation, ref IGPMessages message, ref List<string> nodeList, ref List<string> wayList, ref List<string> relationList, ref List<string> downloadedDocuments, string baseURL, api apiCapabilities)
        {
            try
            {
                XmlSerializer nodeSerializer = new XmlSerializer(typeof(node));
                XmlSerializer waySerializer = new XmlSerializer(typeof(way));
                XmlSerializer relationSerializer = new XmlSerializer(typeof(relation));

                string requestURL = String.Empty;

                using (System.Xml.XmlReader osmFileXmlReader = System.Xml.XmlReader.Create(osmFileLocation))
                {
                    osmFileXmlReader.MoveToContent();

                    while (osmFileXmlReader.Read())
                    {
                        if (osmFileXmlReader.IsStartElement())
                        {
                            if (osmFileXmlReader.Name == "node")
                            {
                                string currentNodeString = osmFileXmlReader.ReadOuterXml();
                                // turn the xml node representation into a node class representation
                                node currentNode = nodeSerializer.Deserialize(new System.IO.StringReader(currentNodeString)) as node;
                                if (!nodeList.Contains(currentNode.id))
                                {
                                    nodeList.Add(currentNode.id);
                                }
                            }
                            else if (osmFileXmlReader.Name == "way")
                            {
                                string currentWayString = osmFileXmlReader.ReadOuterXml();
                                // turn the xml way representation into a way class representation
                                way currentWay = waySerializer.Deserialize(new System.IO.StringReader(currentWayString)) as way;
                                if (!wayList.Contains(currentWay.id))
                                {
                                    wayList.Add(currentWay.id);
                                }

                                //if (currentWay.nd != null)
                                //{
                                //    foreach (nd wayNd in currentWay.nd)
                                //    {
                                //        if (!nodeList.Contains(wayNd.@ref))
                                //        {
                                //            string resolveMessage = string.Format(resourceManager.GetString("GPTools_OSMGPDownload_resolveid"), resourceManager.GetString("GPTools_OSM_node"), wayNd.@ref);
                                //            message.AddMessage(resolveMessage);
                                //            requestURL = baseURL + "/api/0.6/node/" + wayNd.@ref + "/full";
                                //            string nodeDownloadDocument = downloadOSMDocument(ref message, requestURL, apiCapabilities);
                                //            parseOSMDocument(nodeDownloadDocument, ref message, ref nodeList, ref wayList, ref relationList, ref downloadedDocuments, baseURL, apiCapabilities);
                                //            downloadedDocuments.Insert(1, nodeDownloadDocument);
                                //        }
                                //    }
                                //}
                            }
                            else if (osmFileXmlReader.Name == "relation")
                            {
                                string currentRelationString = osmFileXmlReader.ReadOuterXml();
                                // turn the xml way representation into a way class representation
                                relation currentRelation = relationSerializer.Deserialize(new System.IO.StringReader(currentRelationString)) as relation;
                                if (!relationList.Contains(currentRelation.id))
                                {
                                    relationList.Add(currentRelation.id);
                                }

                                foreach (object relationItem in currentRelation.Items)
                                {
                                    if (relationItem is member)
                                    {
                                        //if (((member)relationItem).type == memberType.node)
                                        //{
                                        //    string memberNodeID = ((member)relationItem).@ref;
                                        //    if (!nodeList.Contains(memberNodeID))
                                        //    {
                                        //        string resolveMessage = string.Format(resourceManager.GetString("GPTools_OSMGPDownload_resolveid"), resourceManager.GetString("GPTools_OSM_node"), memberNodeID);
                                        //        message.AddMessage(resolveMessage);
                                        //        requestURL = baseURL + "/api/0.6/node/" + memberNodeID + "/full";
                                        //        string nodeDownloadDocument = downloadOSMDocument(ref message, requestURL, apiCapabilities);
                                        //        parseOSMDocument(nodeDownloadDocument, ref message, ref nodeList, ref wayList, ref relationList, ref downloadedDocuments, baseURL, apiCapabilities);
                                        //        downloadedDocuments.Insert(1, nodeDownloadDocument);
                                        //    }
                                        //}
                                        if (((member)relationItem).type == memberType.way)
                                        {
                                            string memberWayID = ((member)relationItem).@ref;
                                            if (!wayList.Contains(memberWayID))
                                            {
                                                string resolveMessage = string.Format(resourceManager.GetString("GPTools_OSMGPDownload_resolveid"), resourceManager.GetString("GPTools_OSM_way"), memberWayID);
                                                message.AddMessage(resolveMessage);
                                                requestURL = baseURL + "/api/0.6/way/" + memberWayID + "/full";
                                                string wayDownloadDocument = downloadOSMDocument(ref message, requestURL, apiCapabilities);
                                                if (!String.IsNullOrEmpty(wayDownloadDocument))
                                                {
                                                    parseOSMDocument(wayDownloadDocument, ref message, ref nodeList, ref wayList, ref relationList, ref downloadedDocuments, baseURL, apiCapabilities);
                                                    downloadedDocuments.Insert(1, wayDownloadDocument);
                                                }
                                            }
                                        }
                                        else if (((member)relationItem).type == memberType.relation)
                                        {
                                            string memberRelationID = ((member)relationItem).@ref;
                                            if (!wayList.Contains(memberRelationID))
                                            {
                                                string resolveMessage = string.Format(resourceManager.GetString("GPTools_OSMGPDownload_resolveid"), resourceManager.GetString("GPTools_OSM_relation"), memberRelationID);
                                                message.AddMessage(resolveMessage);
                                                requestURL = baseURL + "/api/0.6/relation/" + memberRelationID + "/full";
                                                string relationDownloadDocument = downloadOSMDocument(ref message, requestURL, apiCapabilities);
                                                if (!String.IsNullOrEmpty(relationDownloadDocument))
                                                {
                                                    parseOSMDocument(relationDownloadDocument, ref message, ref nodeList, ref wayList, ref relationList, ref downloadedDocuments, baseURL, apiCapabilities);
                                                    downloadedDocuments.Insert(1, relationDownloadDocument);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    osmFileXmlReader.Close();
                }
            }
            catch (Exception ex)
            {
                message.AddError(120051, ex.Message);
            }
        }

    }
}