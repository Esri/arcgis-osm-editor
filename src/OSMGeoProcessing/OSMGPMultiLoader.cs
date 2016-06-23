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
using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.Geoprocessing;
using ESRI.ArcGIS.DataSourcesFile;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.OSM.OSMClassExtension;
using ESRI.ArcGIS.Display;
using System.Text.RegularExpressions;

namespace ESRI.ArcGIS.OSM.GeoProcessing
{
    [Guid("ce7a2734-2414-4893-b4b2-88a78dde5527")]
    [ClassInterface(ClassInterfaceType.None)]
    [ProgId("OSMEditor.OSMGPMultiLoader")]
    public class OSMGPMultiLoader : ESRI.ArcGIS.Geoprocessing.IGPFunction2
    {
        string m_DisplayName = String.Empty;
        ResourceManager resourceManager = null;
        OSMGPFactory osmGPFactory = null;


        int in_osmFileNumber, out_osmPointsNumber, out_osmLinesNumber, out_osmPolygonsNumber, 
            in_deleteSupportNodesNumber, in_deleteOSMSourceFileNumber, in_pointFieldNamesNumber, 
            in_lineFieldNamesNumber, in_polygonFieldNamesNumber;
        Dictionary<string, string> m_editorConfigurationSettings = null;


        public OSMGPMultiLoader()
        {
            osmGPFactory = new OSMGPFactory();
            resourceManager = new ResourceManager("ESRI.ArcGIS.OSM.GeoProcessing.OSMGPToolsStrings", this.GetType().Assembly);

            m_editorConfigurationSettings = OSMGPFactory.ReadOSMEditorSettings();
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
                    m_DisplayName = osmGPFactory.GetFunctionName(OSMGPFactory.m_MultiLoaderName).DisplayName;
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
                    message.AddError(120029, String.Format(resourceManager.GetString("GPTools_OSMGPMultiLoader_problemaccessingfile"), ex.Message));
                    return;
                }

                if (osmFileExists == false)
                {
                    message.AddError(120030, String.Format(resourceManager.GetString("GPTools_OSMGPFileReader_osmfiledoesnotexist"), osmFileLocationString.GetAsText()));
                    return;
                }

                // determine the number of threads to be used
                IGPEnvironment parallelProcessingFactorEnvironment = OSMToolHelper.getEnvironment(envMgr, "parallelProcessingFactor");
                IGPString parallelProcessingFactorString = parallelProcessingFactorEnvironment.Value as IGPString;

                // the default value is to use half the cores for additional threads - I am aware that we are comparing apples and oranges but I need a number
                int numberOfThreads = Convert.ToInt32(System.Environment.ProcessorCount / 2);

                if (!(parallelProcessingFactorEnvironment.Value.IsEmpty()))
                {
                    if (!Int32.TryParse(parallelProcessingFactorString.Value, out numberOfThreads))
                    {
                        // this case we have a percent string
                        string resultString = Regex.Match(parallelProcessingFactorString.Value, @"\d+").Value;
                        numberOfThreads = Convert.ToInt32(Double.Parse(resultString) / 100 * System.Environment.ProcessorCount);
                    }
                }

                // tread the special case of 0
                if (numberOfThreads <= 0)
                    numberOfThreads = 1;

                IGPEnvironment configKeyword = OSMToolHelper.getEnvironment(envMgr, "configKeyword");
                IGPString gpString = configKeyword.Value as IGPString;

                string storageKeyword = String.Empty;

                if (gpString != null)
                {
                    storageKeyword = gpString.Value;
                }

                // determine the temp folder to be use for the intermediate files
                IGPEnvironment scratchWorkspaceEnvironment = OSMToolHelper.getEnvironment(envMgr, "scratchWorkspace");
                IDEWorkspace deWorkspace = scratchWorkspaceEnvironment.Value as IDEWorkspace;
                String scratchWorkspaceFolder = String.Empty;

                if (deWorkspace != null)
                {
                    if (scratchWorkspaceEnvironment.Value.IsEmpty())
                    {
                        scratchWorkspaceFolder = (new System.IO.FileInfo(osmFileLocationString.GetAsText())).DirectoryName;
                    }
                    else
                    {
                        if (deWorkspace.WorkspaceType == esriWorkspaceType.esriRemoteDatabaseWorkspace)
                        {
                            scratchWorkspaceFolder = System.IO.Path.GetTempPath();
                        }
                        else if (deWorkspace.WorkspaceType == esriWorkspaceType.esriFileSystemWorkspace)
                        {
                            scratchWorkspaceFolder = ((IDataElement)deWorkspace).CatalogPath;
                        }
                        else
                        {
                            scratchWorkspaceFolder = (new System.IO.FileInfo(((IDataElement)deWorkspace).CatalogPath)).DirectoryName;
                        }
                    }
                }
                else
                {
                    scratchWorkspaceFolder = (new System.IO.FileInfo(osmFileLocationString.GetAsText())).DirectoryName;
                }

                string metadataAbstract = resourceManager.GetString("GPTools_OSMGPFileReader_metadata_abstract");
                string metadataPurpose = resourceManager.GetString("GPTools_OSMGPFileReader_metadata_purpose");

                IGPParameter osmPointsFeatureClassParameter = paramvalues.get_Element(out_osmPointsNumber) as IGPParameter;
                IGPValue osmPointsFeatureClassGPValue = gpUtilities3.UnpackGPValue(osmPointsFeatureClassParameter) as IGPValue;

                IGPParameter osmLineFeatureClassParameter = paramvalues.get_Element(out_osmLinesNumber) as IGPParameter;
                IGPValue osmLineFeatureClassGPValue = gpUtilities3.UnpackGPValue(osmLineFeatureClassParameter) as IGPValue;

                IGPParameter osmPolygonFeatureClassParameter = paramvalues.get_Element(out_osmPolygonsNumber) as IGPParameter;
                IGPValue osmPolygonFeatureClassGPValue = gpUtilities3.UnpackGPValue(osmPolygonFeatureClassParameter) as IGPValue;

                List<string> fcTargets = new List<string>() { osmPointsFeatureClassGPValue.GetAsText(), 
                    osmLineFeatureClassGPValue.GetAsText(), osmPolygonFeatureClassGPValue.GetAsText() };
                IEnumerable<string> uniqueFeatureClassTargets = fcTargets.Distinct();

                if (uniqueFeatureClassTargets.Count() != 3)
                {
                    message.AddError(120201, String.Format(resourceManager.GetString("GPTools_OSMGPRelationLoader_not_unique_fc_names")));
                    return;
                }

                // determine the number of nodes, ways and relation in the OSM file
                long nodeCapacity = 0;
                long wayCapacity = 0;
                long relationCapacity = 0;

                // this assume a clean, tidy XML file - if this is not the case, there will by sync issues later on
                osmToolHelper.countOSMStuffFast(osmFileLocationString.GetAsText(), ref nodeCapacity, ref wayCapacity, ref relationCapacity, ref TrackCancel);

                if (nodeCapacity == 0 && wayCapacity == 0 && relationCapacity == 0)
                {
                    return;
                }

                message.AddMessage(String.Format(resourceManager.GetString("GPTools_OSMGPMultiLoader_countedElements"), nodeCapacity, wayCapacity, relationCapacity));


                string pointFCName = osmPointsFeatureClassGPValue.GetAsText();
                string[] pointFCNameElements = pointFCName.Split(System.IO.Path.DirectorySeparatorChar);

                IName workspaceName = null;
                try
                {
                    workspaceName = gpUtilities3.CreateParentFromCatalogPath(pointFCName);

                    if (workspaceName is IDatasetName)
                    {
                        message.AddError(120200, String.Format(resourceManager.GetString("GPTools_OSMGPMultiLoader_fc_only"),
                            pointFCNameElements[pointFCNameElements.Length - 1]));
                        return;
                    }
                }
                catch (Exception ex)
                {
                    message.AddError(120200, String.Format(resourceManager.GetString("GPTools_OSMGPMultiLoader_fc_only"), 
                        pointFCNameElements[pointFCNameElements.Length - 1]));
                    return;
                }

                IWorkspace2 pointFeatureWorkspace = workspaceName.Open() as IWorkspace2;

                IGPParameter tagPointCollectionParameter = paramvalues.get_Element(in_pointFieldNamesNumber) as IGPParameter;
                IGPMultiValue tagPointCollectionGPValue = gpUtilities3.UnpackGPValue(tagPointCollectionParameter) as IGPMultiValue;

                List<String> pointTagstoExtract = null;

                if (tagPointCollectionGPValue.Count > 0)
                {
                    pointTagstoExtract = new List<string>();

                    for (int valueIndex = 0; valueIndex < tagPointCollectionGPValue.Count; valueIndex++)
                    {
                        string nameOfTag = tagPointCollectionGPValue.get_Value(valueIndex).GetAsText();

                        pointTagstoExtract.Add(nameOfTag);
                    }
                }
                else
                {
                    pointTagstoExtract = OSMToolHelper.OSMSmallFeatureClassFields();
                }

                // points
                try
                {
                    osmPointFeatureClass = osmToolHelper.CreateSmallPointFeatureClass(pointFeatureWorkspace, 
                        pointFCNameElements[pointFCNameElements.Length - 1], storageKeyword, metadataAbstract,
                        metadataPurpose, pointTagstoExtract);
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

                int osmPointIDFieldIndex = osmPointFeatureClass.FindField("OSMID");
                Dictionary<string, int> mainPointAttributeFieldIndices = new Dictionary<string, int>();
                
                foreach (string fieldName in OSMToolHelper.OSMSmallFeatureClassFields())
                {
                    int currentFieldIndex = osmPointFeatureClass.FindField(fieldName);

                    if (currentFieldIndex != -1)
                    {
                        mainPointAttributeFieldIndices.Add(fieldName, currentFieldIndex);
                    }
                }

                int tagCollectionPointFieldIndex = osmPointFeatureClass.FindField("osmTags");
                int osmSupportingElementPointFieldIndex = osmPointFeatureClass.FindField("osmSupportingElement");

                string lineFCName = osmLineFeatureClassGPValue.GetAsText();
                string[] lineFCNameElements = lineFCName.Split(System.IO.Path.DirectorySeparatorChar);

                IName lineWorkspaceName = null;
                try
                {
                    lineWorkspaceName = gpUtilities3.CreateParentFromCatalogPath(lineFCName);
                }
                catch (Exception ex)
                {
                    message.AddError(120200, String.Format(resourceManager.GetString("GPTools_OSMGPMultiLoader_fc_only"),
                        lineFCNameElements[lineFCNameElements.Length - 1]));
                    return;
                }
                IWorkspace2 lineFeatureWorkspace = lineWorkspaceName.Open() as IWorkspace2;

                IGPParameter tagLineCollectionParameter = paramvalues.get_Element(in_lineFieldNamesNumber) as IGPParameter;
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
                    osmLineFeatureClass = osmToolHelper.CreateSmallLineFeatureClass(lineFeatureWorkspace, lineFCNameElements[lineFCNameElements.Length -1],
                        storageKeyword, metadataAbstract, metadataPurpose, lineTagstoExtract);
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

                int osmLineIDFieldIndex = osmLineFeatureClass.FindField("OSMID");

                Dictionary<string, int> mainLineAttributeFieldIndices = new Dictionary<string, int>();
                foreach (string fieldName in OSMToolHelper.OSMSmallFeatureClassFields())
                {
                    int currentFieldIndex = osmLineFeatureClass.FindField(fieldName);

                    if (currentFieldIndex != -1)
                    {
                        mainLineAttributeFieldIndices.Add(fieldName, currentFieldIndex);
                    }
                }

                int tagCollectionPolylineFieldIndex = osmLineFeatureClass.FindField("osmTags");
                int osmSupportingElementPolylineFieldIndex = osmLineFeatureClass.FindField("osmSupportingElement");

                string polygonFCName = osmPolygonFeatureClassGPValue.GetAsText();
                string[] polygonFCNameElements = polygonFCName.Split(System.IO.Path.DirectorySeparatorChar);

                IName polygonWorkspaceName = null;

                try
                {
                    polygonWorkspaceName = gpUtilities3.CreateParentFromCatalogPath(polygonFCName);
                }
                catch (Exception ex)
                {
                    message.AddError(120200, String.Format(resourceManager.GetString("GPTools_OSMGPMultiLoader_fc_only"),
                        polygonFCNameElements[polygonFCNameElements.Length - 1]));
                    return;
                }

                IWorkspace2 polygonFeatureWorkspace = polygonWorkspaceName.Open() as IWorkspace2;

                IGPParameter tagPolygonCollectionParameter = paramvalues.get_Element(in_polygonFieldNamesNumber) as IGPParameter;
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
                        polygonFCNameElements[polygonFCNameElements.Length -1], storageKeyword, metadataAbstract, 
                        metadataPurpose, polygonTagstoExtract);
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

                int osmPolygonIDFieldIndex = osmPolygonFeatureClass.FindField("OSMID");

                Dictionary<string, int> mainPolygonAttributeFieldIndices = new Dictionary<string, int>();
                foreach (var fieldName in OSMToolHelper.OSMSmallFeatureClassFields())
                {
                    int currentFieldIndex = osmPolygonFeatureClass.FindField(fieldName);

                    if (currentFieldIndex != -1)
                    {
                        mainPolygonAttributeFieldIndices.Add(fieldName, currentFieldIndex);
                    }
                }

                int tagCollectionPolygonFieldIndex = osmPolygonFeatureClass.FindField("osmTags");
                int osmSupportingElementPolygonFieldIndex = osmPolygonFeatureClass.FindField("osmSupportingElement");

                ComReleaser.ReleaseCOMObject(osmPointFeatureClass);
                osmPointFeatureClass = null;

                ComReleaser.ReleaseCOMObject(osmLineFeatureClass);
                osmLineFeatureClass = null;

                ComReleaser.ReleaseCOMObject(osmPolygonFeatureClass);
                osmPolygonFeatureClass = null;


                List<string> nodeOSMFileNames = new List<string>(numberOfThreads);
                List<string> nodeGDBFileNames = new List<string>(numberOfThreads);

                List<string> wayOSMFileNames = new List<string>(numberOfThreads);
                List<string> wayGDBFileNames = new List<string>(numberOfThreads);

                List<string> relationOSMFileNames = new List<string>(numberOfThreads);
                List<string> relationGDBFileNames = new List<string>(numberOfThreads);

                if (TrackCancel.Continue() == false)
                {
                    return;
                }

                // split the original OSM xml file into smaller pieces for the python processes
                osmToolHelper.splitOSMFile(osmFileLocationString.GetAsText(), scratchWorkspaceFolder, nodeCapacity, wayCapacity, relationCapacity, numberOfThreads,
                    out nodeOSMFileNames, out nodeGDBFileNames, out wayOSMFileNames, out wayGDBFileNames, out relationOSMFileNames, out relationGDBFileNames);

                IGPParameter deleteSourceOSMFileParameter = paramvalues.get_Element(in_deleteOSMSourceFileNumber) as IGPParameter;
                IGPBoolean deleteSourceOSMFileGPValue = gpUtilities3.UnpackGPValue(deleteSourceOSMFileParameter) as IGPBoolean;

                if (deleteSourceOSMFileGPValue.Value)
                {
                    try
                    {
                        System.IO.File.Delete(osmFileLocationString.GetAsText());
                    }
                    catch (Exception ex)
                    {
                        message.AddWarning(ex.Message);
                    }
                }

                if (TrackCancel.Continue() == false)
                {
                    return;
                }

                if (nodeOSMFileNames.Count == 0)
                {
                    nodeOSMFileNames.Add(osmFileLocationString.GetAsText());
                    nodeGDBFileNames.Add(((IWorkspace)pointFeatureWorkspace).PathName);

                    wayOSMFileNames.Add(osmFileLocationString.GetAsText());
                    wayGDBFileNames.Add(((IWorkspace)lineFeatureWorkspace).PathName);

                    relationOSMFileNames.Add(osmFileLocationString.GetAsText());
                    relationGDBFileNames.Add(((IWorkspace)polygonFeatureWorkspace).PathName);
                }
                else if (nodeOSMFileNames.Count == 1)
                {
                    nodeGDBFileNames[0] = ((IWorkspace)pointFeatureWorkspace).PathName;
                    wayGDBFileNames[0] = ((IWorkspace)lineFeatureWorkspace).PathName;
                    relationGDBFileNames[0] = ((IWorkspace)polygonFeatureWorkspace).PathName;
                }
                else
                {
                    // for the nodes let's load one of the parts directly into the target file geodatabase
                    nodeGDBFileNames[0] = ((IWorkspace)pointFeatureWorkspace).PathName;
                }

                // define variables helping to invoke core tools for data management
                IGeoProcessorResult2 gpResults2 = null;
                IGeoProcessor2 geoProcessor = new GeoProcessorClass();

                IGPParameter deleteSupportingNodesParameter = paramvalues.get_Element(in_deleteSupportNodesNumber) as IGPParameter;
                IGPBoolean deleteSupportingNodesGPValue = gpUtilities3.UnpackGPValue(deleteSupportingNodesParameter) as IGPBoolean;

                #region load points
                osmToolHelper.loadOSMNodes(nodeOSMFileNames, nodeGDBFileNames, pointFCNameElements[pointFCNameElements.Length - 1],
                    pointFCName, pointTagstoExtract, deleteSupportingNodesGPValue.Value, ref message, ref TrackCancel);
                #endregion

                if (TrackCancel.Continue() == false)
                {
                    return;
                }

                #region load ways
                osmToolHelper.loadOSMWays(wayOSMFileNames, pointFCName, lineFCName,
                    polygonFCName, wayGDBFileNames, lineFCNameElements[lineFCNameElements.Length - 1], 
                    polygonFCNameElements[polygonFCNameElements.Length - 1], lineTagstoExtract, polygonTagstoExtract, ref message,  ref TrackCancel);
                #endregion

                #region for local geodatabases enforce spatial integrity

                bool storedOriginalLocal = geoProcessor.AddOutputsToMap;
                geoProcessor.AddOutputsToMap = false;

                try
                {
                    osmLineFeatureClass = ((IFeatureWorkspace)lineFeatureWorkspace).OpenFeatureClass(lineFCNameElements[lineFCNameElements.Length - 1]);

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
                }
                catch { }
                finally
                {
                    ComReleaser.ReleaseCOMObject(osmLineFeatureClass);
                }


                try
                {
                    osmPolygonFeatureClass = ((IFeatureWorkspace)polygonFeatureWorkspace).OpenFeatureClass(polygonFCNameElements[polygonFCNameElements.Length - 1]);

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
                }
                catch { }
                finally
                {
                    ComReleaser.ReleaseCOMObject(osmPolygonFeatureClass);
                }

                geoProcessor.AddOutputsToMap = storedOriginalLocal;

                #endregion

                if (TrackCancel.Continue() == false)
                {
                    return;
                }

                #region load relations
                osmToolHelper.loadOSMRelations(relationOSMFileNames, lineFCName, polygonFCName,
                    lineFCName, polygonFCName, relationGDBFileNames, lineTagstoExtract, 
                    polygonTagstoExtract, ref message, ref TrackCancel);
                #endregion

                // check for user interruption
                if (TrackCancel.Continue() == false)
                {
                    return;
                }

                #region for local geodatabases enforce spatial integrity
                //storedOriginalLocal = geoProcessor.AddOutputsToMap;
                //geoProcessor.AddOutputsToMap = false;

                //try
                //{
                //    osmLineFeatureClass = ((IFeatureWorkspace)lineFeatureWorkspace).OpenFeatureClass(lineFCNameElements[lineFCNameElements.Length - 1]);

                //    if (osmLineFeatureClass != null)
                //    {

                //        if (((IDataset)osmLineFeatureClass).Workspace.Type == esriWorkspaceType.esriLocalDatabaseWorkspace)
                //        {
                //            gpUtilities3 = new GPUtilitiesClass() as IGPUtilities3;

                //            IGPParameter outLinesParameter = paramvalues.get_Element(out_osmLinesNumber) as IGPParameter;
                //            IGPValue lineFeatureClass = gpUtilities3.UnpackGPValue(outLinesParameter);

                //            DataManagementTools.RepairGeometry repairlineGeometry = new DataManagementTools.RepairGeometry(osmLineFeatureClass);

                //            IVariantArray repairGeometryParameterArray = new VarArrayClass();
                //            repairGeometryParameterArray.Add(lineFeatureClass.GetAsText());
                //            repairGeometryParameterArray.Add("DELETE_NULL");

                //            gpResults2 = geoProcessor.Execute(repairlineGeometry.ToolName, repairGeometryParameterArray, TrackCancel) as IGeoProcessorResult2;
                //            message.AddMessages(gpResults2.GetResultMessages());

                //            ComReleaser.ReleaseCOMObject(gpUtilities3);
                //        }
                //    }
                //}
                //catch { }
                //finally
                //{
                //    ComReleaser.ReleaseCOMObject(osmLineFeatureClass);
                //}


                //try
                //{
                //    osmPolygonFeatureClass = ((IFeatureWorkspace)polygonFeatureWorkspace).OpenFeatureClass(polygonFCNameElements[polygonFCNameElements.Length - 1]);

                //    if (osmPolygonFeatureClass != null)
                //    {
                //        if (((IDataset)osmPolygonFeatureClass).Workspace.Type == esriWorkspaceType.esriLocalDatabaseWorkspace)
                //        {
                //            gpUtilities3 = new GPUtilitiesClass() as IGPUtilities3;

                //            IGPParameter outPolygonParameter = paramvalues.get_Element(out_osmPolygonsNumber) as IGPParameter;
                //            IGPValue polygonFeatureClass = gpUtilities3.UnpackGPValue(outPolygonParameter);

                //            DataManagementTools.RepairGeometry repairpolygonGeometry = new DataManagementTools.RepairGeometry(osmPolygonFeatureClass);

                //            IVariantArray repairGeometryParameterArray = new VarArrayClass();
                //            repairGeometryParameterArray.Add(polygonFeatureClass.GetAsText());
                //            repairGeometryParameterArray.Add("DELETE_NULL");

                //            gpResults2 = geoProcessor.Execute(repairpolygonGeometry.ToolName, repairGeometryParameterArray, TrackCancel) as IGeoProcessorResult2;
                //            message.AddMessages(gpResults2.GetResultMessages());

                //            ComReleaser.ReleaseCOMObject(gpUtilities3);
                //        }
                //    }
                //}
                //catch { }
                //finally
                //{
                //    ComReleaser.ReleaseCOMObject(osmPolygonFeatureClass);
                //}

                //geoProcessor.AddOutputsToMap = storedOriginalLocal;

                #endregion

                if (TrackCancel.Continue() == false)
                {
                    return;
                }


                if (deleteSupportingNodesGPValue.Value)
                {
                    message.AddMessage(String.Format(resourceManager.GetString("GPTools_OSMGPMultiLoader_remove_supportNodes")));

                    storedOriginalLocal = geoProcessor.AddOutputsToMap;
                    geoProcessor.AddOutputsToMap = false;

                    gpUtilities3 = new GPUtilitiesClass() as IGPUtilities3;
                    workspaceName = gpUtilities3.CreateParentFromCatalogPath(pointFCName);
                    pointFeatureWorkspace = workspaceName.Open() as IWorkspace2;

                    // create a layer file to select the points that have attributes
                    osmPointFeatureClass = ((IFeatureWorkspace)pointFeatureWorkspace).OpenFeatureClass(pointFCNameElements[pointFCNameElements.Length - 1]);

                    IVariantArray makeFeatureLayerParameterArray = new VarArrayClass();
                    makeFeatureLayerParameterArray.Add(pointFCName);

                    string tempLayerFile = System.IO.Path.GetTempFileName();
                    makeFeatureLayerParameterArray.Add(tempLayerFile);
                    makeFeatureLayerParameterArray.Add(String.Format("{0} = 'no'", osmPointFeatureClass.SqlIdentifier("osmSupportingElement")));

                    geoProcessor.Execute("MakeFeatureLayer_management", makeFeatureLayerParameterArray, TrackCancel);

                    // copy the features into its own feature class
                    IVariantArray copyFeatureParametersArray = new VarArrayClass();
                    copyFeatureParametersArray.Add(tempLayerFile);

                    string tempFeatureClass = String.Join("\\", new string[] { 
                        ((IWorkspace)pointFeatureWorkspace).PathName, "t_" + pointFCNameElements[pointFCNameElements.Length - 1] });
                    copyFeatureParametersArray.Add(tempFeatureClass);

                    geoProcessor.Execute("CopyFeatures_management", copyFeatureParametersArray, TrackCancel);

                    // delete the temp file
                    System.IO.File.Delete(tempLayerFile);

                    // delete the original feature class
                    IVariantArray deleteParameterArray = new VarArrayClass();
                    deleteParameterArray.Add(osmPointsFeatureClassGPValue.GetAsText());

                    geoProcessor.Execute("Delete_management", deleteParameterArray, TrackCancel);

                    // rename the temp feature class back to the original
                    IVariantArray renameParameterArray = new VarArrayClass();
                    renameParameterArray.Add(tempFeatureClass);
                    renameParameterArray.Add(osmPointsFeatureClassGPValue.GetAsText());

                    geoProcessor.Execute("Rename_management", renameParameterArray, TrackCancel);

                    geoProcessor.AddOutputsToMap = storedOriginalLocal;

                    ComReleaser.ReleaseCOMObject(pointFeatureWorkspace);
                    ComReleaser.ReleaseCOMObject(workspaceName);
                    ComReleaser.ReleaseCOMObject(osmPointFeatureClass);
                    ComReleaser.ReleaseCOMObject(geoProcessor);
                }

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

                ComReleaser.ReleaseCOMObject(osmFileLocationString);

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
                    fullName = osmGPFactory.GetFunctionName(OSMGPFactory.m_MultiLoaderName) as IName;
                }

                return fullName;
            }
        }

        public object GetRenderer(ESRI.ArcGIS.Geoprocessing.IGPParameter pParam)
        {
            return default(object);
        }

        public int HelpContext
        {
            get
            {
                return default(int);
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
            return true;
        }

        public string MetadataFile
        {
            get
            {
                string metadafile = "osmgpmultiloader.xml";

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
                return OSMGPFactory.m_MultiLoaderName;
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
                osmLoadFile.DisplayName = resourceManager.GetString("GPTools_OSMGPMultiLoader_osmfile_desc");
                osmLoadFile.Name = "in_osmFile";
                osmLoadFile.ParameterType = esriGPParameterType.esriGPParameterTypeRequired;

                IGPParameterEdit3 osmPoints = new GPParameterClass() as IGPParameterEdit3;
                osmPoints.DataType = new DEFeatureClassTypeClass();
                osmPoints.Direction = esriGPParameterDirection.esriGPParameterDirectionOutput;
                osmPoints.ParameterType = esriGPParameterType.esriGPParameterTypeRequired;
                osmPoints.DisplayName = resourceManager.GetString("GPTools_OSMGPMultiLoader_osmPoints_desc");
                osmPoints.Name = "out_osmPoints";

                IGPFeatureClassDomain osmPointFeatureClassDomain = new GPFeatureClassDomainClass();
                osmPointFeatureClassDomain.AddFeatureType(esriFeatureType.esriFTSimple);
                osmPointFeatureClassDomain.AddType(ESRI.ArcGIS.Geometry.esriGeometryType.esriGeometryPoint);
                osmPoints.Domain = osmPointFeatureClassDomain as IGPDomain;

                IGPParameterEdit3 osmLines = new GPParameterClass() as IGPParameterEdit3;
                osmLines.DataType = new DEFeatureClassTypeClass();
                osmLines.Direction = esriGPParameterDirection.esriGPParameterDirectionOutput;
                osmLines.ParameterType = esriGPParameterType.esriGPParameterTypeRequired;
                osmLines.DisplayName = resourceManager.GetString("GPTools_OSMGPMultiLoader_osmLine_desc");
                osmLines.Name = "out_osmLines";

                IGPFeatureClassDomain osmPolylineFeatureClassDomain = new GPFeatureClassDomainClass();
                osmPolylineFeatureClassDomain.AddFeatureType(esriFeatureType.esriFTSimple);
                osmPolylineFeatureClassDomain.AddType(ESRI.ArcGIS.Geometry.esriGeometryType.esriGeometryPolyline);
                osmLines.Domain = osmPolylineFeatureClassDomain as IGPDomain;

                IGPParameterEdit3 osmPolygons = new GPParameterClass() as IGPParameterEdit3;
                osmPolygons.DataType = new DEFeatureClassTypeClass();
                osmPolygons.Direction = esriGPParameterDirection.esriGPParameterDirectionOutput;
                osmPolygons.ParameterType = esriGPParameterType.esriGPParameterTypeRequired;
                osmPolygons.DisplayName = resourceManager.GetString("GPTools_OSMGPMultiLoader_osmPolygon_desc");
                osmPolygons.Name = "out_osmPolygons";

                IGPFeatureClassDomain osmPolygonFeatureClassDomain = new GPFeatureClassDomainClass();
                osmPolygonFeatureClassDomain.AddFeatureType(esriFeatureType.esriFTSimple);
                osmPolygonFeatureClassDomain.AddType(ESRI.ArcGIS.Geometry.esriGeometryType.esriGeometryPolygon);
                osmPolygons.Domain = osmPolygonFeatureClassDomain as IGPDomain;

                IGPParameterEdit3 deleteOSMSourceFileParameter = new GPParameterClass() as IGPParameterEdit3;
                IGPCodedValueDomain deleteOSMSourceFileDomain = new GPCodedValueDomainClass();

                IGPBoolean deleteOSMSourceFileTrue = new GPBooleanClass();
                deleteOSMSourceFileTrue.Value = true;
                IGPBoolean deleteOSMSourceFileFalse = new GPBooleanClass();
                deleteOSMSourceFileFalse.Value = false;

                deleteOSMSourceFileDomain.AddCode((IGPValue)deleteOSMSourceFileTrue, "DELETE_OSM_FILE");
                deleteOSMSourceFileDomain.AddCode((IGPValue)deleteOSMSourceFileFalse, "DO_NOT_DELETE_OSM_FILE");
                deleteOSMSourceFileParameter.Domain = (IGPDomain)deleteOSMSourceFileDomain;
                deleteOSMSourceFileParameter.Value = (IGPValue)deleteOSMSourceFileFalse;

                deleteOSMSourceFileParameter.DataType = new GPBooleanTypeClass();
                deleteOSMSourceFileParameter.Direction = esriGPParameterDirection.esriGPParameterDirectionInput;
                deleteOSMSourceFileParameter.ParameterType = esriGPParameterType.esriGPParameterTypeOptional;
                deleteOSMSourceFileParameter.DisplayName = resourceManager.GetString("GPTools_OSMGPMultiLoader_deleteOSMSource_desc");
                deleteOSMSourceFileParameter.Name = "in_deleteOSMSourceFile";


                IGPParameterEdit3 deleteSupportNodesParameter = new GPParameterClass() as IGPParameterEdit3;
                IGPCodedValueDomain deleteSupportNodesDomain = new GPCodedValueDomainClass();

                IGPBoolean deleteSupportNodesTrue = new GPBooleanClass();
                deleteSupportNodesTrue.Value = true;
                IGPBoolean deleteSupportNodesFalse = new GPBooleanClass();
                deleteSupportNodesFalse.Value = false;

                deleteSupportNodesDomain.AddCode((IGPValue)deleteSupportNodesTrue, "DELETE_NODES");
                deleteSupportNodesDomain.AddCode((IGPValue)deleteSupportNodesFalse, "DO_NOT_DELETE_NODES");
                deleteSupportNodesParameter.Domain = (IGPDomain)deleteSupportNodesDomain;
                deleteSupportNodesParameter.Value = (IGPValue)deleteSupportNodesFalse;

                deleteSupportNodesParameter.DataType = new GPBooleanTypeClass();
                deleteSupportNodesParameter.Direction = esriGPParameterDirection.esriGPParameterDirectionInput;
                deleteSupportNodesParameter.ParameterType = esriGPParameterType.esriGPParameterTypeOptional;
                deleteSupportNodesParameter.DisplayName = resourceManager.GetString("GPTools_OSMGPMultiLoader_deleteNodes_desc");
                deleteSupportNodesParameter.Name = "in_deleteSupportNodes";

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

                IGPParameterEdit3 loadPointFieldsParameter = new GPParameterClass() as IGPParameterEdit3;
                loadPointFieldsParameter.Name = "in_point_fieldNames";
                loadPointFieldsParameter.DisplayName = resourceManager.GetString("GPTools_OSMGPNodeLoader_fieldNames_desc");
                loadPointFieldsParameter.Category = resourceManager.GetString("GPTools_OSMGPMultiLoader_schemaCategory_desc");
                loadPointFieldsParameter.ParameterType = esriGPParameterType.esriGPParameterTypeOptional;
                loadPointFieldsParameter.Direction = esriGPParameterDirection.esriGPParameterDirectionInput;
                loadPointFieldsParameter.DataType = (IGPDataType)fieldNameDataType2;
                loadPointFieldsParameter.Value = (IGPValue)fieldNameMultiValue;

                parameterArray.Add(osmLoadFile);
                in_osmFileNumber = 0;

                parameterArray.Add(loadPointFieldsParameter);
                in_pointFieldNamesNumber = 1;

                parameterArray.Add(loadLineFieldsParameter);
                in_lineFieldNamesNumber = 2;

                parameterArray.Add(loadPolygonFieldsParameter);
                in_polygonFieldNamesNumber = 3;

                parameterArray.Add(deleteSupportNodesParameter);
                in_deleteSupportNodesNumber = 4;

                parameterArray.Add(deleteOSMSourceFileParameter);
                in_deleteOSMSourceFileNumber = 5;

                parameterArray.Add(osmPoints);
                out_osmPointsNumber = 6;

                parameterArray.Add(osmLines);
                out_osmLinesNumber = 7;

                parameterArray.Add(osmPolygons);
                out_osmPolygonsNumber = 8;

                return parameterArray;
            }
        }

        public void UpdateMessages(ESRI.ArcGIS.esriSystem.IArray paramvalues, ESRI.ArcGIS.Geoprocessing.IGPEnvironmentManager pEnvMgr, ESRI.ArcGIS.Geodatabase.IGPMessages Messages)
        {
            //IGPUtilities3 gpUtilities3 = new GPUtilitiesClass();

            //IGPParameter targetDatasetParameter = paramvalues.get_Element(out_targetDatasetNumber) as IGPParameter;
            //try
            //{
            //    gpUtilities3.QualifyOutputDataElement(gpUtilities3.UnpackGPValue(targetDatasetParameter));
            //}
            //catch
            //{
            //    Messages.ReplaceError(out_targetDatasetNumber, -2, resourceManager.GetString("GPTools_OSMGPFileReader_targetDataset_notexist"));
            //}

            //// check for valid geodatabase path
            //// if the user is pointing to a valid directory on disk, flag it as an error
            //IGPValue targetDatasetGPValue = gpUtilities3.UnpackGPValue(targetDatasetParameter);

            //if (targetDatasetGPValue.IsEmpty() == false)
            //{
            //    if (System.IO.Directory.Exists(targetDatasetGPValue.GetAsText()))
            //    {
            //        Messages.ReplaceError(out_targetDatasetNumber, -4, resourceManager.GetString("GPTools_OSMGPDownload_directory_is_not_target_dataset"));
            //    }
            //}

            //gpUtilities3.ReleaseInternals();

            //if (gpUtilities3 != null)
            //    ComReleaser.ReleaseCOMObject(gpUtilities3);

        }

        public void UpdateParameters(ESRI.ArcGIS.esriSystem.IArray paramvalues, ESRI.ArcGIS.Geoprocessing.IGPEnvironmentManager pEnvMgr)
        {
            //IGPUtilities3 gpUtilities3 = new GPUtilitiesClass();

            //IGPParameter targetDatasetParameter = paramvalues.get_Element(out_targetDatasetNumber) as IGPParameter;
            //IGPValue targetDatasetGPValue = gpUtilities3.UnpackGPValue(targetDatasetParameter);

            //IDEFeatureDataset targetDEFeatureDataset = targetDatasetGPValue as IDEFeatureDataset;

            //IGPParameter3 outPointsFeatureClassParameter = null;
            //IGPValue outPointsFeatureClass = null;
            //string outpointsPath = String.Empty;

            //IGPParameter3 outLinesFeatureClassParameter = null;
            //IGPValue outLinesFeatureClass = null;
            //string outlinesPath = String.Empty;

            //IGPParameter3 outPolygonFeatureClassParameter = null;
            //IGPValue outPolygonFeatureClass = null;
            //string outpolygonsPath = String.Empty;

            //if (((IGPValue)targetDEFeatureDataset).GetAsText().Length != 0)
            //{
            //    IDataElement dataElement = targetDEFeatureDataset as IDataElement;
            //    try
            //    {
            //        gpUtilities3.QualifyOutputDataElement(gpUtilities3.UnpackGPValue(targetDatasetParameter));
            //    }
            //    catch
            //    {
            //        return;
            //    }

            //    string nameOfPointFeatureClass = dataElement.GetBaseName() + "_osm_pt";
            //    string nameOfLineFeatureClass = dataElement.GetBaseName() + "_osm_ln";
            //    string nameOfPolygonFeatureClass = dataElement.GetBaseName() + "_osm_ply";


            //    try
            //    {
            //        outpointsPath = dataElement.CatalogPath + System.IO.Path.DirectorySeparatorChar + nameOfPointFeatureClass;
            //    }
            //    catch (Exception ex)
            //    {
            //        System.Diagnostics.Debug.WriteLine(ex.Message);
            //    }
            //    outPointsFeatureClassParameter = paramvalues.get_Element(out_osmPointsNumber) as IGPParameter3;
            //    outPointsFeatureClass = gpUtilities3.UnpackGPValue(outPointsFeatureClassParameter);

            //    outlinesPath = dataElement.CatalogPath + System.IO.Path.DirectorySeparatorChar + nameOfLineFeatureClass;
            //    outLinesFeatureClassParameter = paramvalues.get_Element(out_osmLinesNumber) as IGPParameter3;
            //    outLinesFeatureClass = gpUtilities3.UnpackGPValue(outLinesFeatureClassParameter);

            //    outpolygonsPath = dataElement.CatalogPath + System.IO.Path.DirectorySeparatorChar + nameOfPolygonFeatureClass;
            //    outPolygonFeatureClassParameter = paramvalues.get_Element(out_osmPolygonsNumber) as IGPParameter3;
            //    outPolygonFeatureClass = gpUtilities3.UnpackGPValue(outPolygonFeatureClassParameter);
            //}

            //if (outPointsFeatureClassParameter != null)
            //{
            //    outPointsFeatureClass.SetAsText(outpointsPath);
            //    gpUtilities3.PackGPValue(outPointsFeatureClass, outPointsFeatureClassParameter);
            //}

            //if (outLinesFeatureClassParameter != null)
            //{
            //    outLinesFeatureClass.SetAsText(outlinesPath);
            //    gpUtilities3.PackGPValue(outLinesFeatureClass, outLinesFeatureClassParameter);
            //}

            //if (outPolygonFeatureClassParameter != null)
            //{
            //    outPolygonFeatureClass.SetAsText(outpolygonsPath);
            //    gpUtilities3.PackGPValue(outPolygonFeatureClass, outPolygonFeatureClassParameter);
            //}

            //gpUtilities3.ReleaseInternals();

            //if (gpUtilities3 != null)
            //    ComReleaser.ReleaseCOMObject(gpUtilities3);
        }

        public ESRI.ArcGIS.Geodatabase.IGPMessages Validate(ESRI.ArcGIS.esriSystem.IArray paramvalues, bool updateValues, ESRI.ArcGIS.Geoprocessing.IGPEnvironmentManager envMgr)
        {
            return default(ESRI.ArcGIS.Geodatabase.IGPMessages);
        }
        #endregion

    }
}
