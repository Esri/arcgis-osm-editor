// (c) Copyright ESRI, 2010 - 2012
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using System.Runtime.InteropServices;
using System.Resources;
using ESRI.ArcGIS.Geoprocessing;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.esriSystem;
using System.Xml.Serialization;
using ESRI.ArcGIS.DataSourcesFile;
using System.Globalization;
using System.Collections;
using ESRI.ArcGIS.Display;
using ESRI.ArcGIS.OSM.OSMClassExtension;
using ESRI.ArcGIS.OSM.OSMUtilities;

namespace ESRI.ArcGIS.OSM.GeoProcessing
{
    [Guid("86211e63-2dbb-4d56-95ca-25b3d2a0a0ac")]
    [ClassInterface(ClassInterfaceType.None)]
    [ProgId("OSMEditor.OSMGPFileLoader")]
    public class OSMGPFileLoader : ESRI.ArcGIS.Geoprocessing.IGPFunction2
    {

        Dictionary<string, OSMToolHelper.simplePointRef> osmNodeDictionary = null; 

        string m_DisplayName = String.Empty;
        int in_osmFileNumber, in_conserveMemoryNumber, out_targetDatasetNumber, out_osmPointsNumber, out_osmLinesNumber, out_osmPolygonsNumber, out_RelationTableNumber, out_RevTableNumber;
        ResourceManager resourceManager = null;
        OSMGPFactory osmGPFactory = null;
        Dictionary<string, string> m_editorConfigurationSettings = null;

        public OSMGPFileLoader()
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
                    m_DisplayName = osmGPFactory.GetFunctionName(OSMGPFactory.m_FileLoaderName).DisplayName;
                }

                return m_DisplayName;
            }
        }

        public void Execute(ESRI.ArcGIS.esriSystem.IArray paramvalues, ESRI.ArcGIS.esriSystem.ITrackCancel TrackCancel, ESRI.ArcGIS.Geoprocessing.IGPEnvironmentManager envMgr, ESRI.ArcGIS.Geodatabase.IGPMessages message)
        {
            IFeatureClass osmPointFeatureClass = null;
            IFeatureClass osmLineFeatureClass = null;
            IFeatureClass osmPolygonFeatureClass = null;
            OSMToolHelper osmToolHelper = null;

            try
            {
                DateTime syncTime = DateTime.Now;

                IGPUtilities3 gpUtilities3 = new GPUtilitiesClass();
                osmToolHelper = new OSMToolHelper();

                if (TrackCancel == null)
                {
                    TrackCancel = new CancelTrackerClass();
                }

                IGPParameter osmFileParameter = paramvalues.get_Element(in_osmFileNumber) as IGPParameter;
                IGPValue osmFileLocationString = gpUtilities3.UnpackGPValue(osmFileParameter) as IGPValue;

                // ensure that the specified file does exist
                bool osmFileExists = false;

                try
                {
                    osmFileExists = System.IO.File.Exists(osmFileLocationString.GetAsText());
                }
                catch (Exception ex)
                {
                    message.AddError(120029, String.Format(resourceManager.GetString("GPTools_OSMGPFileReader_problemaccessingfile"), ex.Message));
                    return;
                }

                if (osmFileExists == false)
                {
                    message.AddError(120030, String.Format(resourceManager.GetString("GPTools_OSMGPFileReader_osmfiledoesnotexist"), osmFileLocationString.GetAsText()));
                    return;
                }

                IGPParameter conserveMemoryParameter = paramvalues.get_Element(in_conserveMemoryNumber) as IGPParameter;
                IGPBoolean conserveMemoryGPValue = gpUtilities3.UnpackGPValue(conserveMemoryParameter) as IGPBoolean;

                if (conserveMemoryGPValue == null)
                {
                    message.AddError(120031, string.Format(resourceManager.GetString("GPTools_NullPointerParameterType"), conserveMemoryParameter.Name));
                    return;
                }

                message.AddMessage(resourceManager.GetString("GPTools_OSMGPFileReader_countingNodes"));
                int nodeCapacity = 0;
                int wayCapacity = 0;
                int relationCapacity = 0;

                osmToolHelper.countOSMStuff(osmFileLocationString.GetAsText(), ref nodeCapacity, ref wayCapacity, ref relationCapacity, ref TrackCancel);

                if (conserveMemoryGPValue.Value == false)
                {
                    osmNodeDictionary = new Dictionary<string, OSMToolHelper.simplePointRef>(nodeCapacity);
                }

                message.AddMessage(String.Format(resourceManager.GetString("GPTools_OSMGPFileReader_countedElements"), nodeCapacity, wayCapacity, relationCapacity));

                // prepare the feature dataset and classes
                IGPParameter targetDatasetParameter = paramvalues.get_Element(out_targetDatasetNumber) as IGPParameter;
                IGPValue targetDatasetGPValue = gpUtilities3.UnpackGPValue(targetDatasetParameter);
                IDEDataset2 targetDEDataset2 = targetDatasetGPValue as IDEDataset2;

                if (targetDEDataset2 == null)
                {
                    message.AddError(120048, string.Format(resourceManager.GetString("GPTools_NullPointerParameterType"), targetDatasetParameter.Name));
                    return;
                }

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
                        message.AddError(120032, String.Format(resourceManager.GetString("GPTools_OSMGPFileReader_basenamealreadyexists"), targetDataElement.Name));
                        return;
                    }
                }

                // load the descriptions from which to derive the domain values
                OSMDomains availableDomains = null;

                // Reading the XML document requires a FileStream.
                System.Xml.XmlTextReader reader = null;
                string xmlDomainFile = "";
                m_editorConfigurationSettings.TryGetValue("osmdomainsfilepath", out xmlDomainFile);

                if (System.IO.File.Exists(xmlDomainFile))
                {
                    reader = new System.Xml.XmlTextReader(xmlDomainFile);
                }

                if (reader == null)
                {
                    message.AddError(120033, resourceManager.GetString("GPTools_OSMGPDownload_NoDomainConfigFile"));
                    return;
                }

                System.Xml.Serialization.XmlSerializer domainSerializer = null;

                try
                {
                    domainSerializer = new XmlSerializer(typeof(OSMDomains));
                    availableDomains = domainSerializer.Deserialize(reader) as OSMDomains;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                    System.Diagnostics.Debug.WriteLine(ex.StackTrace);
                    message.AddError(120034, ex.Message);
                    return;
                }
                reader.Close();

                message.AddMessage(resourceManager.GetString("GPTools_preparedb"));

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

                IWorkspaceDomains workspaceDomain = null;
                IFeatureWorkspace featureWorkspace = null;
                // if the target dataset already exists we can go ahead and QI to it directly
                if (targetDataset != null)
                {
                    workspaceDomain = targetDataset.Workspace as IWorkspaceDomains;
                    featureWorkspace = targetDataset.Workspace as IFeatureWorkspace;
                }
                else
                {
                    // in case it doesn't exist yet we will open the parent (the workspace - geodatabase- itself) and 
                    // use it as a reference to create the feature dataset and the feature classes in it.
                    IWorkspace newWorkspace = ((IName)parentName).Open() as IWorkspace;
                    workspaceDomain = newWorkspace as IWorkspaceDomains;
                    featureWorkspace = newWorkspace as IFeatureWorkspace;
                }

                foreach (var domain in codedValueDomains.Values)
                {
                    IDomain testDomain = null;
                    try
                    {
                        testDomain = workspaceDomain.get_DomainByName(domain.Name);
                    }
                    catch { }

                    if (testDomain == null)
                    {
                        workspaceDomain.AddDomain(domain);
                    }
                }

                // this determines the spatial reference as defined from the gp environment settings and the initial wgs84 SR
                ISpatialReferenceFactory spatialReferenceFactory = new SpatialReferenceEnvironmentClass() as ISpatialReferenceFactory;
                ISpatialReference wgs84 = spatialReferenceFactory.CreateGeographicCoordinateSystem((int)esriSRGeoCSType.esriSRGeoCS_WGS1984) as ISpatialReference;

                ISpatialReference downloadSpatialReference = gpUtilities3.GetGPSpRefEnv(envMgr, wgs84, null, 0, 0, 0, 0, null);

                Marshal.ReleaseComObject(wgs84);
                Marshal.ReleaseComObject(spatialReferenceFactory);

                IGPEnvironment configKeyword = OSMGPDownload.getEnvironment(envMgr, "configKeyword");
                IGPString gpString = configKeyword.Value as IGPString;

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


                downloadSpatialReference = ((IGeoDataset)targetFeatureDataset).SpatialReference;

                string metadataAbstract = resourceManager.GetString("GPTools_OSMGPFileReader_metadata_abstract");
                string metadataPurpose = resourceManager.GetString("GPTools_OSMGPFileReader_metadata_purpose");

                // assign the custom class extension for use with the OSM feature inspector
                UID osmClassUID = new UIDClass();
                osmClassUID.Value = "{65CA4847-8661-45eb-8E1E-B2985CA17C78}";

                // points
                try
                {
                    osmPointFeatureClass = osmToolHelper.CreatePointFeatureClass((IWorkspace2)featureWorkspace, targetFeatureDataset, targetDataElement.Name + "_osm_pt", null, null, null, storageKeyword, availableDomains, metadataAbstract, metadataPurpose);
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

                // change the property set of the osm class extension to skip any change detection during the initial data load
                osmPointFeatureClass.RemoveOSMClassExtension();

                int osmPointIDFieldIndex = osmPointFeatureClass.FindField("OSMID");
                Dictionary<string, int> osmPointDomainAttributeFieldIndices = new Dictionary<string, int>();
                foreach (var domains in availableDomains.domain)
                {
                    int currentFieldIndex = osmPointFeatureClass.FindField(domains.name);

                    if (currentFieldIndex != -1)
                    {
                        osmPointDomainAttributeFieldIndices.Add(domains.name, currentFieldIndex);
                    }
                }
                int tagCollectionPointFieldIndex = osmPointFeatureClass.FindField("osmTags");
                int osmUserPointFieldIndex = osmPointFeatureClass.FindField("osmuser");
                int osmUIDPointFieldIndex = osmPointFeatureClass.FindField("osmuid");
                int osmVisiblePointFieldIndex = osmPointFeatureClass.FindField("osmvisible");
                int osmVersionPointFieldIndex = osmPointFeatureClass.FindField("osmversion");
                int osmChangesetPointFieldIndex = osmPointFeatureClass.FindField("osmchangeset");
                int osmTimeStampPointFieldIndex = osmPointFeatureClass.FindField("osmtimestamp");
                int osmMemberOfPointFieldIndex = osmPointFeatureClass.FindField("osmMemberOf");
                int osmSupportingElementPointFieldIndex = osmPointFeatureClass.FindField("osmSupportingElement");
                int osmWayRefCountFieldIndex = osmPointFeatureClass.FindField("wayRefCount");


                // lines
                try
                {
                    osmLineFeatureClass = osmToolHelper.CreateLineFeatureClass((IWorkspace2)featureWorkspace, targetFeatureDataset, targetDataElement.Name + "_osm_ln", null, null, null, storageKeyword, availableDomains, metadataAbstract, metadataPurpose);
                }
                catch (Exception ex)
                {
                    message.AddError(120036, String.Format(resourceManager.GetString("GPTools_OSMGPDownload_nulllinefeatureclass"), ex.Message));
                    return;
                }

                if (osmLineFeatureClass == null)
                {
                    return;
                }

                // change the property set of the osm class extension to skip any change detection during the initial data load
                osmLineFeatureClass.RemoveOSMClassExtension();

                int osmLineIDFieldIndex = osmLineFeatureClass.FindField("OSMID");
                Dictionary<string, int> osmLineDomainAttributeFieldIndices = new Dictionary<string, int>();
                foreach (var domains in availableDomains.domain)
                {
                    int currentFieldIndex = osmLineFeatureClass.FindField(domains.name);

                    if (currentFieldIndex != -1)
                    {
                        osmLineDomainAttributeFieldIndices.Add(domains.name, currentFieldIndex);
                    }
                }
                int tagCollectionPolylineFieldIndex = osmLineFeatureClass.FindField("osmTags");
                int osmUserPolylineFieldIndex = osmLineFeatureClass.FindField("osmuser");
                int osmUIDPolylineFieldIndex = osmLineFeatureClass.FindField("osmuid");
                int osmVisiblePolylineFieldIndex = osmLineFeatureClass.FindField("osmvisible");
                int osmVersionPolylineFieldIndex = osmLineFeatureClass.FindField("osmversion");
                int osmChangesetPolylineFieldIndex = osmLineFeatureClass.FindField("osmchangeset");
                int osmTimeStampPolylineFieldIndex = osmLineFeatureClass.FindField("osmtimestamp");
                int osmMemberOfPolylineFieldIndex = osmLineFeatureClass.FindField("osmMemberOf");
                int osmMembersPolylineFieldIndex = osmLineFeatureClass.FindField("osmMembers");
                int osmSupportingElementPolylineFieldIndex = osmLineFeatureClass.FindField("osmSupportingElement");


                // polygons
                try
                {
                    osmPolygonFeatureClass = osmToolHelper.CreatePolygonFeatureClass((IWorkspace2)featureWorkspace, targetFeatureDataset, targetDataElement.Name + "_osm_ply", null, null, null, storageKeyword, availableDomains, metadataAbstract, metadataPurpose);
                }
                catch (Exception ex)
                {
                    message.AddError(120037, String.Format(resourceManager.GetString("GPTools_OSMGPDownload_nullpolygonfeatureclass"), ex.Message));
                    return;
                }

                if (osmPolygonFeatureClass == null)
                {
                    return;
                }

                // change the property set of the osm class extension to skip any change detection during the initial data load
                osmPolygonFeatureClass.RemoveOSMClassExtension();

                int osmPolygonIDFieldIndex = osmPolygonFeatureClass.FindField("OSMID");
                Dictionary<string, int> osmPolygonDomainAttributeFieldIndices = new Dictionary<string, int>();
                foreach (var domains in availableDomains.domain)
                {
                    int currentFieldIndex = osmPolygonFeatureClass.FindField(domains.name);

                    if (currentFieldIndex != -1)
                    {
                        osmPolygonDomainAttributeFieldIndices.Add(domains.name, currentFieldIndex);
                    }
                }
                int tagCollectionPolygonFieldIndex = osmPolygonFeatureClass.FindField("osmTags");
                int osmUserPolygonFieldIndex = osmPolygonFeatureClass.FindField("osmuser");
                int osmUIDPolygonFieldIndex = osmPolygonFeatureClass.FindField("osmuid");
                int osmVisiblePolygonFieldIndex = osmPolygonFeatureClass.FindField("osmvisible");
                int osmVersionPolygonFieldIndex = osmPolygonFeatureClass.FindField("osmversion");
                int osmChangesetPolygonFieldIndex = osmPolygonFeatureClass.FindField("osmchangeset");
                int osmTimeStampPolygonFieldIndex = osmPolygonFeatureClass.FindField("osmtimestamp");
                int osmMemberOfPolygonFieldIndex = osmPolygonFeatureClass.FindField("osmMemberOf");
                int osmMembersPolygonFieldIndex = osmPolygonFeatureClass.FindField("osmMembers");
                int osmSupportingElementPolygonFieldIndex = osmPolygonFeatureClass.FindField("osmSupportingElement");


                // relation table
                ITable relationTable = null;

                try
                {
                    relationTable = osmToolHelper.CreateRelationTable((IWorkspace2)featureWorkspace, targetDataElement.Name + "_osm_relation", null, storageKeyword, metadataAbstract, metadataPurpose);
                }
                catch (Exception ex)
                {
                    message.AddError(120038, String.Format(resourceManager.GetString("GPTools_OSMGPDownload_nullrelationtable"), ex.Message));
                    return;
                }

                if (relationTable == null)
                {
                    return;
                }

                int osmRelationIDFieldIndex = relationTable.FindField("OSMID");
                int tagCollectionRelationFieldIndex = relationTable.FindField("osmTags");
                int osmUserRelationFieldIndex = relationTable.FindField("osmuser");
                int osmUIDRelationFieldIndex = relationTable.FindField("osmuid");
                int osmVisibleRelationFieldIndex = relationTable.FindField("osmvisible");
                int osmVersionRelationFieldIndex = relationTable.FindField("osmversion");
                int osmChangesetRelationFieldIndex = relationTable.FindField("osmchangeset");
                int osmTimeStampRelationFieldIndex = relationTable.FindField("osmtimestamp");
                int osmMemberOfRelationFieldIndex = relationTable.FindField("osmMemberOf");
                int osmMembersRelationFieldIndex = relationTable.FindField("osmMembers");
                int osmSupportingElementRelationFieldIndex = relationTable.FindField("osmSupportingElement");

                // revision table 
                ITable revisionTable = null;

                try
                {
                    revisionTable = osmToolHelper.CreateRevisionTable((IWorkspace2)featureWorkspace, targetDataElement.Name + "_osm_revision", null, storageKeyword);
                }
                catch (Exception ex)
                {
                    message.AddError(120039, String.Format(resourceManager.GetString("GPTools_OSMGPDownload_nullrevisiontable"), ex.Message));
                    return;
                }

                if (revisionTable == null)
                {
                    return;
                }


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


                //// check for user interruption
                //if (TrackCancel.Continue() == false)
                //{
                //    message.AddAbort(resourceManager.GetString("GPTools_toolabort"));
                //    return;
                //}

                //IFeatureCursor deleteCursor = null;
                //using (ComReleaser comReleaser = new ComReleaser())
                //{
                //    // let's make sure that we clean out any old data that might have existed in the feature classes
                //    deleteCursor = osmPointFeatureClass.Update(null, false);
                //    comReleaser.ManageLifetime(deleteCursor);

                //    for (IFeature feature = deleteCursor.NextFeature(); feature != null; feature = deleteCursor.NextFeature())
                //    {
                //        feature.Delete();

                //        // check for user interruption
                //        if (TrackCancel.Continue() == false)
                //        {
                //            message.AddAbort(resourceManager.GetString("GPTools_toolabort"));
                //            return;
                //        }

                //    }
                //}

                //using (ComReleaser comReleaser = new ComReleaser())
                //{
                //    deleteCursor = osmLineFeatureClass.Update(null, false);
                //    comReleaser.ManageLifetime(deleteCursor);

                //    for (IFeature feature = deleteCursor.NextFeature(); feature != null; feature = deleteCursor.NextFeature())
                //    {
                //        feature.Delete();

                //        // check for user interruption
                //        if (TrackCancel.Continue() == false)
                //        {
                //            message.AddAbort(resourceManager.GetString("GPTools_toolabort"));
                //            return;
                //        }
                //    }
                //}

                //using (ComReleaser comReleaser = new ComReleaser())
                //{
                //    deleteCursor = osmPolygonFeatureClass.Update(null, false);
                //    comReleaser.ManageLifetime(deleteCursor);

                //    for (IFeature feature = deleteCursor.NextFeature(); feature != null; feature = deleteCursor.NextFeature())
                //    {
                //        feature.Delete();

                //        // check for user interruption
                //        if (TrackCancel.Continue() == false)
                //        {
                //            message.AddAbort(resourceManager.GetString("GPTools_toolabort"));
                //            return;
                //        }
                //    }
                //}

                //ICursor tableCursor = null;
                //using (ComReleaser comReleaser = new ComReleaser())
                //{
                //    tableCursor = relationTable.Update(null, false);
                //    comReleaser.ManageLifetime(tableCursor);

                //    for (IRow row = tableCursor.NextRow(); row != null; row = tableCursor.NextRow())
                //    {
                //        row.Delete();

                //        // check for user interruption
                //        if (TrackCancel.Continue() == false)
                //        {
                //            message.AddAbort(resourceManager.GetString("GPTools_toolabort"));
                //            return;
                //        }
                //    }
                //}

                //using (ComReleaser comReleaser = new ComReleaser())
                //{
                //    tableCursor = revisionTable.Update(null, false);
                //    comReleaser.ManageLifetime(tableCursor);

                //    for (IRow row = tableCursor.NextRow(); row != null; row = tableCursor.NextRow())
                //    {
                //        row.Delete();

                //        // check for user interruption
                //        if (TrackCancel.Continue() == false)
                //        {
                //            message.AddAbort(resourceManager.GetString("GPTools_toolabort"));
                //            return;
                //        }
                //    }
                //}

                // define variables helping to invoke core tools for data management
                IGeoProcessorResult2 gpResults2 = null;

                IGeoProcessor2 geoProcessor = new GeoProcessorClass();

                #region load points
                osmToolHelper.loadOSMNodes(osmFileLocationString.GetAsText(), ref TrackCancel, ref message, targetDatasetGPValue, osmPointFeatureClass, conserveMemoryGPValue.Value, false, nodeCapacity, ref osmNodeDictionary, featureWorkspace, downloadSpatialReference, availableDomains, false);
                #endregion


                if (TrackCancel.Continue() == false)
                {
                    return;
                }

                #region load ways
                List<string> missingWays = osmToolHelper.loadOSMWays(osmFileLocationString.GetAsText(), ref TrackCancel, ref message, targetDatasetGPValue, osmPointFeatureClass, osmLineFeatureClass, osmPolygonFeatureClass, conserveMemoryGPValue.Value, false, wayCapacity, ref osmNodeDictionary, featureWorkspace, downloadSpatialReference, availableDomains, false);
                #endregion

                if (downloadSpatialReference != null)
                    Marshal.ReleaseComObject(downloadSpatialReference);

                #region for local geodatabases enforce spatial integrity

                bool storedOriginalLocal = geoProcessor.AddOutputsToMap;
                geoProcessor.AddOutputsToMap = false;

                if (osmLineFeatureClass != null)
                {
                    if (((IDataset)osmLineFeatureClass).Workspace.Type == esriWorkspaceType.esriLocalDatabaseWorkspace)
                    {
                        gpUtilities3 = new GPUtilitiesClass() as IGPUtilities3;

                        IGPParameter outLinesParameter = paramvalues.get_Element(out_osmLinesNumber) as IGPParameter;
                        IGPValue lineFeatureClass = gpUtilities3.UnpackGPValue(outLinesParameter);

                        DataManagementTools.RepairGeometry repairlineGeometry = new DataManagementTools.RepairGeometry(osmLineFeatureClass);

                        IVariantArray repairGeometryParameterArray = new VarArrayClass();
                        repairGeometryParameterArray.Add(lineFeatureClass.GetAsText());
                        repairGeometryParameterArray.Add("DELETE_NULL");

                        gpResults2 = geoProcessor.Execute(repairlineGeometry.ToolName, repairGeometryParameterArray, TrackCancel) as IGeoProcessorResult2;
                        message.AddMessages(gpResults2.GetResultMessages());

                        ComReleaser.ReleaseCOMObject(gpUtilities3);
                    }
                }

                if (osmPolygonFeatureClass != null)
                {
                    if (((IDataset)osmPolygonFeatureClass).Workspace.Type == esriWorkspaceType.esriLocalDatabaseWorkspace)
                    {
                        gpUtilities3 = new GPUtilitiesClass() as IGPUtilities3;

                        IGPParameter outPolygonParameter = paramvalues.get_Element(out_osmPolygonsNumber) as IGPParameter;
                        IGPValue polygonFeatureClass = gpUtilities3.UnpackGPValue(outPolygonParameter);

                        DataManagementTools.RepairGeometry repairpolygonGeometry = new DataManagementTools.RepairGeometry(osmPolygonFeatureClass);

                        IVariantArray repairGeometryParameterArray = new VarArrayClass();
                        repairGeometryParameterArray.Add(polygonFeatureClass.GetAsText());
                        repairGeometryParameterArray.Add("DELETE_NULL");

                        gpResults2 = geoProcessor.Execute(repairpolygonGeometry.ToolName, repairGeometryParameterArray, TrackCancel) as IGeoProcessorResult2;
                        message.AddMessages(gpResults2.GetResultMessages());

                        ComReleaser.ReleaseCOMObject(gpUtilities3);
                    }
                }

                geoProcessor.AddOutputsToMap = storedOriginalLocal;

                #endregion


                if (TrackCancel.Continue() == false)
                {
                    return;
                }

                #region load relations
                List<string> missingRelations = osmToolHelper.loadOSMRelations(osmFileLocationString.GetAsText(), ref TrackCancel, ref message, targetDatasetGPValue, osmPointFeatureClass, osmLineFeatureClass, osmPolygonFeatureClass, relationCapacity, relationTable, availableDomains, false, false);
                #endregion

                // check for user interruption
                if (TrackCancel.Continue() == false)
                {
                    return;
                }

                //storedOriginalLocal = geoProcessor.AddOutputsToMap;
                //try
                //{
                //    geoProcessor.AddOutputsToMap = false;

                //    // add indexes for revisions
                //    //IGPValue revisionTableGPValue = gpUtilities3.MakeGPValueFromObject(revisionTable);
                //    string revisionTableString = targetDatasetGPValue.GetAsText() + System.IO.Path.DirectorySeparatorChar + ((IDataset)revisionTable).BrowseName;
                //    IVariantArray parameterArrary2 = osmToolHelper.CreateAddIndexParameterArray(revisionTableString, "osmoldid;osmnewid", "osmID_IDX", "", "");
                //    gpResults2 = geoProcessor.Execute("AddIndex_management", parameterArrary2, TrackCancel) as IGeoProcessorResult2;

                //    message.AddMessages(gpResults2.GetResultMessages());
                //}
                //catch (Exception ex)
                //{
                //    message.AddWarning(ex.Message);
                //}
                //finally
                //{
                //    geoProcessor.AddOutputsToMap = storedOriginalLocal;
                //}


                #region update the references counts and member lists for nodes

                message.AddMessage(resourceManager.GetString("GPTools_OSMGPFileReader_updatereferences"));
                IFeatureCursor pointUpdateCursor = null;

                IQueryFilter pointQueryFilter = new QueryFilterClass();

                // adjust of number of all other reference counter from 0 to 1
                if (conserveMemoryGPValue.Value == true)
                {
                    pointQueryFilter.WhereClause = osmPointFeatureClass.SqlIdentifier("wayRefCount") + " = 0";
                    pointQueryFilter.SubFields = osmPointFeatureClass.OIDFieldName + ",wayRefCount";
                }

                using (SchemaLockManager ptLockManager = new SchemaLockManager(osmPointFeatureClass as ITable))
                {
                    using (ComReleaser comReleaser = new ComReleaser())
                    {
                        int updateCount = 0;
                        if (conserveMemoryGPValue.Value == true)
                        {
                            pointUpdateCursor = osmPointFeatureClass.Update(pointQueryFilter, false);
                            updateCount = ((ITable)osmPointFeatureClass).RowCount(pointQueryFilter);
                        }
                        else
                        {
                            pointUpdateCursor = osmPointFeatureClass.Update(null, false);
                            updateCount = ((ITable)osmPointFeatureClass).RowCount(null);
                        }

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

                        int positionCounter = 0;
                        while (pointFeature != null)
                        {
                            positionCounter++;
                            string nodeID = Convert.ToString(pointFeature.get_Value(osmPointIDFieldIndex));

                            if (conserveMemoryGPValue.Value == false)
                            {
                                // let get the reference counter from the internal node dictionary
                                if (osmNodeDictionary[nodeID].RefCounter == 0)
                                {
                                    pointFeature.set_Value(osmWayRefCountFieldIndex, 1);
                                }
                                else
                                {
                                    pointFeature.set_Value(osmWayRefCountFieldIndex, osmNodeDictionary[nodeID].RefCounter);
                                }
                            }
                            else
                            {
                                // in the case of memory conservation let's go change the 0s to 1s
                                pointFeature.set_Value(osmWayRefCountFieldIndex, 1);
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

                        Marshal.ReleaseComObject(pointQueryFilter);
                    }
                }

                #endregion

                if (osmNodeDictionary != null)
                {
                    // clear outstanding resources potentially holding points
                    osmNodeDictionary = null;
                    System.GC.Collect(2, GCCollectionMode.Forced);
                }

                if (missingRelations.Count > 0)
                {
                    missingRelations.Clear();
                    missingRelations = null;
                }

                if (missingWays.Count > 0)
                {
                    missingWays.Clear();
                    missingWays = null;
                }

                SyncState.StoreLastSyncTime(targetDatasetName, syncTime);

                gpUtilities3 = new GPUtilitiesClass() as IGPUtilities3;

                // repackage the feature class into their respective gp values
                IGPParameter pointFeatureClassParameter = paramvalues.get_Element(out_osmPointsNumber) as IGPParameter;
                IGPValue pointFeatureClassGPValue = gpUtilities3.UnpackGPValue(pointFeatureClassParameter);
                gpUtilities3.PackGPValue(pointFeatureClassGPValue, pointFeatureClassParameter);

                IGPParameter lineFeatureClassParameter = paramvalues.get_Element(out_osmLinesNumber) as IGPParameter;
                IGPValue line1FeatureClassGPValue = gpUtilities3.UnpackGPValue(lineFeatureClassParameter);
                gpUtilities3.PackGPValue(line1FeatureClassGPValue, lineFeatureClassParameter);

                IGPParameter polygonFeatureClassParameter = paramvalues.get_Element(out_osmPolygonsNumber) as IGPParameter;
                IGPValue polygon1FeatureClassGPValue = gpUtilities3.UnpackGPValue(polygonFeatureClassParameter);
                gpUtilities3.PackGPValue(polygon1FeatureClassGPValue, polygonFeatureClassParameter);

                ComReleaser.ReleaseCOMObject(relationTable);
                ComReleaser.ReleaseCOMObject(revisionTable);

                ComReleaser.ReleaseCOMObject(targetFeatureDataset);
                ComReleaser.ReleaseCOMObject(featureWorkspace);

                ComReleaser.ReleaseCOMObject(osmFileLocationString);
                ComReleaser.ReleaseCOMObject(conserveMemoryGPValue);
                ComReleaser.ReleaseCOMObject(targetDataset);

                gpUtilities3.ReleaseInternals();
                ComReleaser.ReleaseCOMObject(gpUtilities3);
            }
            catch (Exception ex)
            {
                message.AddError(120055, ex.Message);
                message.AddError(120055, ex.StackTrace);
            }
            finally
            {
                try
                {
                    if (osmPointFeatureClass != null)
                    {
                        osmPointFeatureClass.ApplyOSMClassExtension();
                        ComReleaser.ReleaseCOMObject(osmPointFeatureClass);
                    }

                    if (osmLineFeatureClass != null)
                    {
                        osmLineFeatureClass.ApplyOSMClassExtension();
                        ComReleaser.ReleaseCOMObject(osmLineFeatureClass);
                    }

                    if (osmPolygonFeatureClass != null)
                    {
                        osmPolygonFeatureClass.ApplyOSMClassExtension();
                        ComReleaser.ReleaseCOMObject(osmPolygonFeatureClass);
                    }

                    osmToolHelper = null;

                    System.GC.Collect();
                    System.GC.WaitForPendingFinalizers();
                }
                catch (Exception ex)
                {
                    message.AddError(120056, ex.ToString());
                }
            }
        }

        public ESRI.ArcGIS.esriSystem.IName FullName
        {
            get
            {
                IName fullName = null;

                if (osmGPFactory != null)
                {
                    fullName = osmGPFactory.GetFunctionName(OSMGPFactory.m_FileLoaderName) as IName;
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
                string metadafile = "osmgpfileloader.xml";

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
                return OSMGPFactory.m_FileLoaderName;
            }
        }

        public ESRI.ArcGIS.esriSystem.IArray ParameterInfo
        {
            get
            {
                //
                IArray parameterArray = new ArrayClass();

                // osm file to load (required)
                IGPParameterEdit3 osmLoadFile = new GPParameterClass() as IGPParameterEdit3;
                osmLoadFile.DataType = new DEFileTypeClass();
                osmLoadFile.Direction = esriGPParameterDirection.esriGPParameterDirectionInput;
                osmLoadFile.DisplayName = resourceManager.GetString("GPTools_OSMGPFileReader_osmfile_desc");
                osmLoadFile.Name = "in_osmFile";
                osmLoadFile.ParameterType = esriGPParameterType.esriGPParameterTypeRequired;

                IGPParameterEdit3 conserveMemory = new GPParameterClass() as IGPParameterEdit3;
                IGPCodedValueDomain conserveMemoryDomain = new GPCodedValueDomainClass();

                IGPBoolean conserveTrue = new GPBooleanClass();
                conserveTrue.Value = true;
                IGPBoolean conserveFalse = new GPBooleanClass();
                conserveFalse.Value = false;

                conserveMemoryDomain.AddCode((IGPValue)conserveTrue, "CONSERVE_MEMORY");
                conserveMemoryDomain.AddCode((IGPValue)conserveFalse, "DO_NOT_CONSERVE_MEMORY");
                conserveMemory.Domain = (IGPDomain)conserveMemoryDomain;
                conserveMemory.Value = (IGPValue)conserveTrue;

                conserveMemory.DataType = new GPBooleanTypeClass();
                conserveMemory.Direction = esriGPParameterDirection.esriGPParameterDirectionInput;
                conserveMemory.DisplayName = resourceManager.GetString("GPTools_OSMGPFileReader_conserveMemory_desc");
                conserveMemory.Name = "in_conserveMemory";

                // target dataset (required)
                IGPParameterEdit3 targetDataset = new GPParameterClass() as IGPParameterEdit3;
                targetDataset.DataType = new DEFeatureDatasetTypeClass();
                targetDataset.Direction = esriGPParameterDirection.esriGPParameterDirectionOutput;
                targetDataset.DisplayName = resourceManager.GetString("GPTools_OSMGPFileReader_targetDataset_desc");
                targetDataset.Name = "out_targetdataset";

                IGPDatasetDomain datasetDomain = new GPDatasetDomainClass();
                datasetDomain.AddType(esriDatasetType.esriDTFeatureDataset);

                targetDataset.Domain = datasetDomain as IGPDomain;

                IGPParameterEdit3 osmPoints = new GPParameterClass() as IGPParameterEdit3;
                osmPoints.DataType = new DEFeatureClassTypeClass();
                osmPoints.Direction = esriGPParameterDirection.esriGPParameterDirectionOutput;
                osmPoints.ParameterType = esriGPParameterType.esriGPParameterTypeRequired;
                osmPoints.Enabled = false;
                osmPoints.Category = resourceManager.GetString("GPTools_OSMGPFileReader_outputCategory");
                osmPoints.DisplayName = resourceManager.GetString("GPTools_OSMGPFileReader_osmPoints_desc");
                osmPoints.Name = "out_osmPoints";

                IGPFeatureClassDomain osmPointFeatureClassDomain = new GPFeatureClassDomainClass();
                osmPointFeatureClassDomain.AddFeatureType(esriFeatureType.esriFTSimple);
                osmPointFeatureClassDomain.AddType(ESRI.ArcGIS.Geometry.esriGeometryType.esriGeometryPoint);
                osmPoints.Domain = osmPointFeatureClassDomain as IGPDomain;

                IGPParameterEdit3 osmLines = new GPParameterClass() as IGPParameterEdit3;
                osmLines.DataType = new DEFeatureClassTypeClass();
                osmLines.Direction = esriGPParameterDirection.esriGPParameterDirectionOutput;
                osmLines.ParameterType = esriGPParameterType.esriGPParameterTypeRequired;
                osmLines.Category = resourceManager.GetString("GPTools_OSMGPFileReader_outputCategory");
                osmLines.Enabled = false;
                osmLines.DisplayName = resourceManager.GetString("GPTools_OSMGPFileReader_osmLine_desc");
                osmLines.Name = "out_osmLines";

                IGPFeatureClassDomain osmPolylineFeatureClassDomain = new GPFeatureClassDomainClass();
                osmPolylineFeatureClassDomain.AddFeatureType(esriFeatureType.esriFTSimple);
                osmPolylineFeatureClassDomain.AddType(ESRI.ArcGIS.Geometry.esriGeometryType.esriGeometryPolyline);
                osmLines.Domain = osmPolylineFeatureClassDomain as IGPDomain;

                IGPParameterEdit3 osmPolygons = new GPParameterClass() as IGPParameterEdit3;
                osmPolygons.DataType = new DEFeatureClassTypeClass();
                osmPolygons.Direction = esriGPParameterDirection.esriGPParameterDirectionOutput;
                osmPolygons.ParameterType = esriGPParameterType.esriGPParameterTypeRequired;
                osmPolygons.Category = resourceManager.GetString("GPTools_OSMGPFileReader_outputCategory");
                osmPolygons.Enabled = false;
                osmPolygons.DisplayName = resourceManager.GetString("GPTools_OSMGPFileReader_osmPolygon_desc");
                osmPolygons.Name = "out_osmPolygons";

                IGPFeatureClassDomain osmPolygonFeatureClassDomain = new GPFeatureClassDomainClass();
                osmPolygonFeatureClassDomain.AddFeatureType(esriFeatureType.esriFTSimple);
                osmPolygonFeatureClassDomain.AddType(ESRI.ArcGIS.Geometry.esriGeometryType.esriGeometryPolygon);
                osmPolygons.Domain = osmPolygonFeatureClassDomain as IGPDomain;


                parameterArray.Add(osmLoadFile);
                in_osmFileNumber = 0;

                parameterArray.Add(conserveMemory);
                in_conserveMemoryNumber = 1;

                parameterArray.Add(targetDataset);
                out_targetDatasetNumber = 2;

                parameterArray.Add(osmPoints);
                out_osmPointsNumber = 3;

                parameterArray.Add(osmLines);
                out_osmLinesNumber = 4;

                parameterArray.Add(osmPolygons);
                out_osmPolygonsNumber = 5;

                return parameterArray;
            }
        }

        public void UpdateMessages(ESRI.ArcGIS.esriSystem.IArray paramvalues, ESRI.ArcGIS.Geoprocessing.IGPEnvironmentManager pEnvMgr, ESRI.ArcGIS.Geodatabase.IGPMessages Messages)
        {
            IGPUtilities3 gpUtilities3 = new GPUtilitiesClass();

            IGPParameter targetDatasetParameter = paramvalues.get_Element(out_targetDatasetNumber) as IGPParameter;
            try
            {
                gpUtilities3.QualifyOutputDataElement(gpUtilities3.UnpackGPValue(targetDatasetParameter));
            }
            catch
            {
                Messages.ReplaceError(out_targetDatasetNumber, -2, resourceManager.GetString("GPTools_OSMGPFileReader_targetDataset_notexist"));
            }

            // check for valid geodatabase path
            // if the user is pointing to a valid directory on disk, flag it as an error
            IGPValue targetDatasetGPValue = gpUtilities3.UnpackGPValue(targetDatasetParameter);

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

            gpUtilities3.ReleaseInternals();

            if (gpUtilities3 != null)
                ComReleaser.ReleaseCOMObject(gpUtilities3);

        }

        public void UpdateParameters(ESRI.ArcGIS.esriSystem.IArray paramvalues, ESRI.ArcGIS.Geoprocessing.IGPEnvironmentManager pEnvMgr)
        {
            IGPUtilities3 gpUtilities3 = new GPUtilitiesClass();

            IGPParameter targetDatasetParameter = paramvalues.get_Element(out_targetDatasetNumber) as IGPParameter;
            IGPValue targetDatasetGPValue = gpUtilities3.UnpackGPValue(targetDatasetParameter);

            IDEFeatureDataset targetDEFeatureDataset = targetDatasetGPValue as IDEFeatureDataset;


            if (((IGPValue)targetDEFeatureDataset).GetAsText().Length == 0)
            {
                return;
            }

            IDataElement dataElement = targetDEFeatureDataset as IDataElement;
            try
            {
                gpUtilities3.QualifyOutputDataElement(gpUtilities3.UnpackGPValue(targetDatasetParameter));
            }
            catch
            {
                return;
            }

            string nameOfPointFeatureClass = dataElement.GetBaseName() + "_osm_pt";
            string nameOfLineFeatureClass = dataElement.GetBaseName() + "_osm_ln";
            string nameOfPolygonFeatureClass = dataElement.GetBaseName() + "_osm_ply";

            string outpointsPath = null;
            try
            {
                outpointsPath = dataElement.CatalogPath + System.IO.Path.DirectorySeparatorChar + nameOfPointFeatureClass;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
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

            gpUtilities3.ReleaseInternals();

            if (gpUtilities3 != null)
                ComReleaser.ReleaseCOMObject(gpUtilities3);

        }

        public ESRI.ArcGIS.Geodatabase.IGPMessages Validate(ESRI.ArcGIS.esriSystem.IArray paramvalues, bool updateValues, ESRI.ArcGIS.Geoprocessing.IGPEnvironmentManager envMgr)
        {
            return default(ESRI.ArcGIS.Geodatabase.IGPMessages);
        }
        #endregion


    }
}
