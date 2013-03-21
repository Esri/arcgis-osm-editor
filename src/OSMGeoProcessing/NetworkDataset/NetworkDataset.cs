using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Runtime.InteropServices;
using ESRI.ArcGIS.Display;
using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.Geoprocessing;
using ESRI.ArcGIS.OSM.OSMClassExtension;

//TODO:
// - Add full set of Parameter types (currently only double, int and bool are supported)
// - Turns with multiple Via nodes are not supported

namespace ESRI.ArcGIS.OSM.GeoProcessing
{
    [ComVisible(false)]
    public class NetworkDataset : IDisposable
    {
        private static ResourceManager RESMGR = new ResourceManager(
            "ESRI.ArcGIS.OSM.GeoProcessing.OSMGPToolsStrings", Assembly.GetAssembly(typeof(NetworkDataset)));

        private NetworkDatasetXML _xml;
        private IDataset _osmDataset;
        private string _ndsName;
        private IGPMessages _messages;
        private ITrackCancel _trackCancel;

        private List<IEdgeFeatureSource> _edgeSources;
        private List<IJunctionFeatureSource> _junctionSources;
        private INetworkSource _turnSource;

        private List<IEvaluatedNetworkAttribute> _networkAttrs;
        private INetworkDataset _networkDataset;

        private string _dsPath;
        private string _osmLineName, _osmLinePath;
        private string _osmPointName, _osmPointPath;
        private IEnvelope _extent;
        private ISpatialReference _spatialReference;

        private RunTaskManager _taskManager;

        /// <summary>Lazy GPUtilities</summary>
        private IGPUtilities3 _gpUtil;
        private IGPUtilities3 GPUtil
        {
            get
            {
                if (_gpUtil == null)
                    _gpUtil = new GPUtilitiesClass();

                return _gpUtil;
            }
        }

        public NetworkDataset(string configXML, IDataset osmDataset, string ndsName, IGPMessages messages, ITrackCancel trackCancel)
        {
            _xml = new NetworkDatasetXML(configXML, RESMGR);

            _osmDataset = osmDataset;
            _ndsName = ndsName;
            _messages = messages;
            _trackCancel = trackCancel;

            IDataElement deOSM = GPUtil.MakeDataElementFromNameObject(_osmDataset.FullName);
            _dsPath = deOSM.CatalogPath;

            _osmLineName = _osmDataset.Name + "_osm_ln";
            _osmLinePath = _dsPath + "\\" + _osmLineName;
            _osmPointName = _osmDataset.Name + "_osm_pt";
            _osmPointPath = _dsPath + "\\" + _osmPointName;

            // Get the extent from the point feature class
            // NOTE: the feature dataset is not used for this because exceptions occur (SDE only)
            //       if a feature class was recently deleted from the feature dataset.
            IFeatureClass fcPoint = ((IFeatureWorkspace)_osmDataset.Workspace).OpenFeatureClass(_osmPointName);
            IGeoDataset gds = (IGeoDataset)fcPoint;
            _extent = gds.Extent;
            _spatialReference = gds.SpatialReference;
        }

        /// <summary>IDisposable - dispose of open assets</summary>
        public void Dispose()
        {
            _xml = null;
            _osmDataset = null;
            _messages = null;
            _trackCancel = null;

            if (_edgeSources != null)
            {
                foreach (var efs in _edgeSources)
                    ComReleaser.ReleaseCOMObject(efs);

                _edgeSources.Clear();
                _edgeSources = null;
            }

            if (_junctionSources != null)
            {
                foreach (var jfs in _junctionSources)
                    ComReleaser.ReleaseCOMObject(jfs);

                _junctionSources.Clear();
                _junctionSources = null;
            }

            ComReleaser.ReleaseCOMObject(_turnSource);
            _turnSource = null;

            if (_networkAttrs != null)
            {
                foreach (var na in _networkAttrs)
                    ComReleaser.ReleaseCOMObject(na);

                _networkAttrs.Clear();
                _networkAttrs = null;
            }

            ComReleaser.ReleaseCOMObject(_networkDataset);
            _networkDataset = null;
        }

        /// <summary>Checks to see if everything is ready for network dataset creation</summary>
        /// <remarks>
        /// - Checks that OSM Dataset is valid
        /// - Assumes that Network Analyst license check is already done (currently done by the GP tool)
        /// </remarks>
        public bool CanCreateNetworkDataset()
        {
            // Check OSM Dataset
            if (_osmDataset == null)
                throw new ArgumentNullException("OSM Dataset");

            IWorkspace2 ws2 = _osmDataset.Workspace as IWorkspace2;
            if (!ws2.get_NameExists(esriDatasetType.esriDTFeatureClass, _osmLineName))
                throw new ApplicationException(string.Format(RESMGR.GetString("GPTools_OSMGPCreateNetworkDataset_invalidOsmDataset"), _osmLineName));

            if (!ws2.get_NameExists(esriDatasetType.esriDTFeatureClass, _osmPointName))
                throw new ApplicationException(string.Format(RESMGR.GetString("GPTools_OSMGPCreateNetworkDataset_invalidOsmDataset"), _osmPointName));

            return true;
        }

        /// <summary>Creates a new network dataset in the current OSM dataset</summary>
        public void CreateNetworkDataset()
        {
            _edgeSources = new List<IEdgeFeatureSource>();
            _junctionSources = new List<IJunctionFeatureSource>();
            _turnSource = null;
            _networkAttrs = new List<IEvaluatedNetworkAttribute>();

            using (_taskManager = new RunTaskManager(_trackCancel ?? new CancelTrackerClass(), _messages))
            {
                _taskManager.ExecuteTask("GPTools_OSMGPCreateNetworkDataset_extractingEdges", ExtractEdgeFeatureClasses);
                _taskManager.ExecuteTask("GPTools_OSMGPCreateNetworkDataset_extractingJunctions", ExtractJunctionFeatureClasses);
                _taskManager.ExecuteTask("GPTools_OSMGPCreateNetworkDataset_assignConnectivity", AssignConnectivity);
                _taskManager.ExecuteTask("GPTools_OSMGPCreateNetworkDataset_extractingTurns", ExtractTurnRestrictions);
                _taskManager.ExecuteTask("GPTools_OSMGPCreateNetworkDataset_addingNetworkAttributes", AddNetworkAttributes);
                _taskManager.ExecuteTask("GPTools_OSMGPCreateNetworkDataset_creating", CreateBuildableNDS);
                _taskManager.ExecuteTask("GPTools_OSMGPCreateNetworkDataset_building", BuildNDS);
            }
        }

        /// <summary>Adds edge feature classes to the network dataset</summary>
        /// <remarks>
        /// - For each 'edge' element in the config file, adds a new EdgeFeatureSource to the NDS
        /// - A new feature class is extracted from *_OSM_LN using a filter specified in the config file
        /// </remarks>
        private void ExtractEdgeFeatureClasses()
        {
            IList<SourceFeatureClassInfo> edges = _xml.EdgeFeatureClasses().ToList();
            if ((edges == null) || (edges.Count == 0))
                return;

            ConvertRequiredTagsToAttributes(true);

            // Create the new feature class using the query filter from the config XML
            foreach (SourceFeatureClassInfo edge in edges)
            {
                string edgeClassName = GetFullClassName(edge.Name);
                SelectFeaturesToNewFeatureClass(_osmLinePath, _dsPath + "\\" + edgeClassName, edge.Query);

                INetworkSource edgeNetworkSource = new EdgeFeatureSourceClass();
                edgeNetworkSource.Name = edgeClassName;
                edgeNetworkSource.ElementType = esriNetworkElementType.esriNETEdge;

                IEdgeFeatureSource edgeFeatureSource = (IEdgeFeatureSource)edgeNetworkSource;
                edgeFeatureSource.UsesSubtypes = false;
                edgeFeatureSource.ClassConnectivityPolicy = (esriNetworkEdgeConnectivityPolicy)edge.ConnectPolicy;

                if (edge.StreetNameFields != null)
                {
                    // Create a StreetNameFields object and populate its settings for the Streets source.
                    IStreetNameFields streetNameFields = new StreetNameFieldsClass();
                    streetNameFields.Priority = 1;
                    streetNameFields.StreetNameFieldName = edge.StreetNameFields.StreetName;
                    streetNameFields.PrefixDirectionFieldName = edge.StreetNameFields.DirectionPrefix;
                    streetNameFields.SuffixDirectionFieldName = edge.StreetNameFields.DirectionSuffix;
                    streetNameFields.PrefixTypeFieldName = edge.StreetNameFields.TypePrefix;
                    streetNameFields.SuffixTypeFieldName = edge.StreetNameFields.TypeSuffix;

                    INetworkSourceDirections sourceDirections = new NetworkSourceDirectionsClass();
                    IArray streetNameFieldsArray = new ArrayClass();
                    streetNameFieldsArray.Add(streetNameFields);
                    sourceDirections.StreetNameFields = streetNameFieldsArray;
                    ((INetworkSource)edgeFeatureSource).NetworkSourceDirections = sourceDirections;
                }

                _edgeSources.Add(edgeFeatureSource);
            }
        }

        /// <summary>Adds junction feature classes to the network dataset</summary>
        /// <remarks>
        /// - For each 'junction' element in the config file, adds a new JunctionFeatureSource to the NDS
        /// - A new feature class is extracted from *_OSM_PT using a filter specified in the config file
        /// </remarks>
        private void ExtractJunctionFeatureClasses()
        {
            IList<SourceFeatureClassInfo> junctions = _xml.JunctionFeatureClasses().ToList();
            if ((junctions == null) || (junctions.Count == 0))
                return;

            ConvertRequiredTagsToAttributes(false);

            foreach (SourceFeatureClassInfo junction in junctions)
            {
                string juncClassName = GetFullClassName(junction.Name);
                SelectFeaturesToNewFeatureClass(_osmPointPath, _dsPath + "\\" + juncClassName, junction.Query);

                INetworkSource junctionNetworkSource = new JunctionFeatureSourceClass();
                junctionNetworkSource.Name = juncClassName;
                junctionNetworkSource.ElementType = esriNetworkElementType.esriNETJunction;

                IJunctionFeatureSource junctionFeatureSource = (IJunctionFeatureSource)junctionNetworkSource;
                junctionFeatureSource.UsesSubtypes = false;
                junctionFeatureSource.RemoveAllClassConnectivityGroups();
                junctionFeatureSource.ClassConnectivityPolicy = (esriNetworkJunctionConnectivityPolicy)junction.ConnectPolicy;

                _junctionSources.Add(junctionFeatureSource);
            }
        }

        /// <summary>Use AttributeSelector to create attributes for fields specified in the config XML</summary>
        private void ConvertRequiredTagsToAttributes(bool isEdge)
        {
            string featureClassName = ((isEdge) ? _osmLineName : _osmPointName);
            string featureClassPath = ((isEdge) ? _osmLinePath : _osmPointPath);

            using (ComReleaser cr = new ComReleaser())
            {
                IFeatureClass fc = ((IFeatureWorkspace)_osmDataset.Workspace).OpenFeatureClass(featureClassName);
                cr.ManageLifetime(fc);

                string[] requiredFieldNames = ((isEdge) ? _xml.EdgeFieldNames() : _xml.JunctionFieldNames())
                    .Where(fld => fc.FindField("osm_" + fld) == -1)
                    .ToArray();

                if (requiredFieldNames.Length > 0)
                {
                    IVariantArray paramArray = new VarArrayClass();
                    paramArray.Add(featureClassPath);
                    paramArray.Add(string.Join(";", requiredFieldNames));

                    _taskManager.ExecuteTool("OSMGPAttributeSelector_osmtools", paramArray);
                }
            }
        }

        /// <summary>Selects a subset of features to a new feature class</summary>
        private void SelectFeaturesToNewFeatureClass(string source, string target, string query)
        {
            try
            {
                IVariantArray paramArray = new VarArrayClass();
                paramArray.Add(source);
                paramArray.Add(target);
                paramArray.Add(query);

                _taskManager.ExecuteTool("Select_analysis", paramArray);
            }
            catch (COMException comex)
            {
                // When working with file geodatabase, assume E_FAIL is a locking issue
                if (_osmDataset.Workspace.Type == esriWorkspaceType.esriLocalDatabaseWorkspace)
                {
                    if ((uint)comex.ErrorCode == 0x80004005)
                    {
                        throw new ApplicationException(string.Format(RESMGR.GetString("GPTools_OSMGPCreateNetworkDataset_lockedTarget"), target));
                    }
                }

                throw;
            }
        }

        /// <summary>Assigns a class connectivity group for each edge / junction source in the xml defined groups</summary>
        private void AssignConnectivity()
        {
            int groupCount = 1;

            foreach (IList<string> group in _xml.ConnectivityGroups())
            {
                foreach (string source in group)
                {
                    string className = GetFullClassName(source);

                    IEdgeFeatureSource efs = _edgeSources.FirstOrDefault(src => ((INetworkSource)src).Name.Equals(className));
                    if (efs != null)
                    {
                        efs.ClassConnectivityGroup = groupCount;
                    }
                    else
                    {
                        IJunctionFeatureSource jfs = _junctionSources.FirstOrDefault(src => ((INetworkSource)src).Name.Equals(className));
                        if (jfs != null)
                            jfs.AddClassConnectivityGroup(groupCount);
                    }
                }

                ++groupCount;
            }
        }
        
        /// <summary>Adds turn restriction feature class to network dataset</summary>
        private void ExtractTurnRestrictions()
        {
            string turnFeatureClass = GetFullClassName(_xml.TurnFeatureClassName());
            if (string.IsNullOrEmpty(turnFeatureClass))
                return;

            INetworkDataset nds = null;

            try
            {
                nds = CreateTempNDS();
                ((INetworkBuild)nds).BuildNetwork(_extent);

                NetworkTurns nt = new NetworkTurns(turnFeatureClass, _osmDataset, nds);
                nt.TaskManager = _taskManager;
                nt.OsmPointFeatureClass = ((IFeatureWorkspace)_osmDataset.Workspace).OpenFeatureClass(_osmPointName);
                nt.OsmEdgeFeatureClasses = _edgeSources
                    .OfType<INetworkSource>()
                    .Select(ns => ((IFeatureWorkspace)_osmDataset.Workspace).OpenFeatureClass(ns.Name))
                    .ToList();

                IFeatureClass fc = ((IFeatureWorkspace)_osmDataset.Workspace).OpenFeatureClass(_osmLineName);
                nt.OsmExtVersion = fc.OSMExtensionVersion();

                _turnSource = nt.ExtractTurnRestrictions();
            }
            finally
            {
                if (nds != null)
                {
                    ((IDataset)nds).Delete();
                    ComReleaser.ReleaseCOMObject(nds);
                }
            }
        }

        /// <summary>Creates a temporary network dataset to use during turn feature creation</summary>
        private INetworkDataset CreateTempNDS()
        {
            const string TEMP_NDS_NAME = "TEMP_TURN_NDS";

            IDENetworkDataset2 deNDS = new DENetworkDatasetClass();
            deNDS.Buildable = true;

            ((IDataElement)deNDS).Name = TEMP_NDS_NAME;

            // Copy the feature dataset's extent and spatial reference to the network dataset
            IDEGeoDataset deGeoDataset = (IDEGeoDataset)deNDS;
            deGeoDataset.Extent = _extent;
            deGeoDataset.SpatialReference = _spatialReference;

            deNDS.ElevationModel = esriNetworkElevationModel.esriNEMNone;
            deNDS.SupportsTurns = true;

            IArray sources = new ArrayClass();
            foreach (INetworkSource ns in EnumerateNetworkSources())
                sources.Add(ns);

            deNDS.Sources = sources;

            // Get the feature dataset extension and create the network dataset from the data element.
            IFeatureDatasetExtension fdExtension = ((IFeatureDatasetExtensionContainer)_osmDataset).FindExtension(esriDatasetType.esriDTNetworkDataset);
            INetworkDataset nds = (INetworkDataset)((IDatasetContainer2)fdExtension).CreateDataset((IDEDataset)deNDS);
            if (nds == null)
                throw new ArgumentNullException("NetworkDataset");

            return nds;
        }

        /// <summary>Adds network attributes</summary>
        private void AddNetworkAttributes()
        {
            List<NetworkAttributeInfo> nais = _xml.NetworkAttributes().ToList();

            // Check for mixed script languages (VBScript / Python)
            var evalLanguages = nais
                .SelectMany(na => na.Evaluators.Select(ev => ev.ScriptLanguage))
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct();
            if (evalLanguages.Count() > 1)
                throw new ApplicationException(RESMGR.GetString("GPTools_OSMGPCreateNetworkDataset_mixedScriptLanguage"));

            foreach (NetworkAttributeInfo nai in nais)
            {
                // Create network attribute
                IEvaluatedNetworkAttribute netAttr = new EvaluatedNetworkAttributeClass()
                {
                    Name = nai.Name,
                    UsageType = nai.UsageType,
                    DataType = nai.DataType,
                    Units = nai.Units,
                    UseByDefault = nai.UseAsDefault
                };

                // Add Parameters
                AddNetworkParameters(nai, netAttr);

                // Add Evaluators
                AddEvaluators(nai, netAttr);

                // Assign default constant evaluator
                INetworkEvaluator constEval = new NetworkConstantEvaluatorClass() { ConstantValue = nai.DefaultValue };
                netAttr.set_DefaultEvaluator(esriNetworkElementType.esriNETEdge, constEval);
                netAttr.set_DefaultEvaluator(esriNetworkElementType.esriNETJunction, constEval);
                netAttr.set_DefaultEvaluator(esriNetworkElementType.esriNETTurn, constEval);

                _networkAttrs.Add(netAttr);
            }
        }

        /// <summary>Creates new network attribute parameters</summary>
        private static void AddNetworkParameters(NetworkAttributeInfo nai, IEvaluatedNetworkAttribute netAttr)
        {
            // Create parameters
            if ((nai.Parameters != null) && (nai.Parameters.Count > 0))
            {
                IArray naParams = new ArrayClass();

                foreach (ParameterInfo paramInfo in nai.Parameters)
                {
                    INetworkAttributeParameter nap = new NetworkAttributeParameterClass();
                    nap.Name = paramInfo.Name;

                    if (paramInfo.DefaultValue is double)
                        nap.VarType = (int)VarEnum.VT_R8;
                    else if (paramInfo.DefaultValue is bool)
                        nap.VarType = (int)VarEnum.VT_BOOL;
                    else if (paramInfo.DefaultValue is string)
                        nap.VarType = (int)VarEnum.VT_BSTR;

                    nap.DefaultValue = paramInfo.DefaultValue;

                    naParams.Add(nap);
                }

                ((INetworkAttribute3)netAttr).Parameters = naParams;
            }
        }

        /// <summary>Adds new Network Attribute Evaluators</summary>
        private void AddEvaluators(NetworkAttributeInfo nai, IEvaluatedNetworkAttribute netAttr)
        {
            foreach (EvaluatorInfo eval in nai.Evaluators)
            {
                INetworkEvaluator evaluator = null;

                if (eval.EvaluatorType == eEvaluatorType.Constant)
                {
                    evaluator = new NetworkConstantEvaluatorClass();
                    if (netAttr.DataType == esriNetworkAttributeDataType.esriNADTBoolean)
                        ((INetworkConstantEvaluator)evaluator).ConstantValue = eval.Expression.Equals(bool.TrueString, StringComparison.CurrentCultureIgnoreCase);
                    else if (netAttr.DataType == esriNetworkAttributeDataType.esriNADTDouble)
                        ((INetworkConstantEvaluator)evaluator).ConstantValue = Convert.ToDouble(eval.Expression);
                    else if (netAttr.DataType == esriNetworkAttributeDataType.esriNADTFloat)
                        ((INetworkConstantEvaluator)evaluator).ConstantValue = Convert.ToSingle(eval.Expression);
                    else if (netAttr.DataType == esriNetworkAttributeDataType.esriNADTInteger)
                        ((INetworkConstantEvaluator)evaluator).ConstantValue = Convert.ToInt32(eval.Expression);
                }
                else if (eval.EvaluatorType == eEvaluatorType.Script)
                {
                    evaluator = new NetworkScriptEvaluatorClass();

#if ARCGIS_10_0     // Handle Python script language added in ArcGIS 10.1
                    if (eval.ScriptLanguage != "VBScript")
                        throw new ApplicationException(RESMGR.GetString("GPTools_OSMGPCreateNetworkDataset_invalidScriptLanguage"));

                    INetworkScriptEvaluator scriptEvaluator = (INetworkScriptEvaluator)evaluator;
                    scriptEvaluator.SetExpression(eval.Expression, eval.Prelogic);
#else
                    INetworkScriptEvaluator2 scriptEvaluator = (INetworkScriptEvaluator2)evaluator;
                    scriptEvaluator.SetLanguage(eval.ScriptLanguage);
                    scriptEvaluator.SetExpression(eval.Expression, eval.Prelogic);
#endif
                }
                else if (eval.EvaluatorType == eEvaluatorType.Field)
                {
                    evaluator = new NetworkFieldEvaluatorClass();

#if ARCGIS_10_0     // Handle Python script language added in ArcGIS 10.1
                    if (eval.ScriptLanguage != "VBScript")
                        throw new ApplicationException(RESMGR.GetString("GPTools_OSMGPCreateNetworkDataset_invalidScriptLanguage"));

                    INetworkFieldEvaluator fieldEvaluator = (INetworkFieldEvaluator)evaluator;
                    fieldEvaluator.SetExpression(eval.Expression, eval.Prelogic);
#else
                    INetworkFieldEvaluator2 fieldEvaluator = (INetworkFieldEvaluator2)evaluator;
                    fieldEvaluator.SetLanguage(eval.ScriptLanguage);
                    fieldEvaluator.SetExpression(eval.Expression, eval.Prelogic);
#endif
                }

                INetworkSource source = EnumerateNetworkSources()
                    .FirstOrDefault(ns => ns.Name.Equals(GetFullClassName(eval.Source), StringComparison.CurrentCultureIgnoreCase));
                if (source != null)
                {
                    esriNetworkEdgeDirection direction = eval.Direction;
                    if (!(source is IEdgeFeatureSource))
                        direction = esriNetworkEdgeDirection.esriNEDNone;

                    netAttr.set_Evaluator(source, direction, evaluator);
                }
            }
        }

        /// <summary>Creates an new (unbuilt) Network Dataset</summary>
        private void CreateBuildableNDS()
        {
            IDENetworkDataset2 deNetworkDataset = new DENetworkDatasetClass();
            deNetworkDataset.Buildable = true;

            ((IDataElement)deNetworkDataset).Name = _ndsName;

            // Copy the feature dataset's extent and spatial reference to the network dataset
            IDEGeoDataset deGeoDataset = (IDEGeoDataset)deNetworkDataset;
            deGeoDataset.Extent = _extent;
            deGeoDataset.SpatialReference = _spatialReference;

            deNetworkDataset.ElevationModel = esriNetworkElevationModel.esriNEMNone;
            deNetworkDataset.SupportsTurns = true;

            // General Network Directions
            GeneralNetworkDirectionInfo dirInfo = _xml.GeneralNetworkDirections();
            if (dirInfo != null)
            {
                INetworkDirections netdir = new NetworkDirectionsClass();
                netdir.LengthAttributeName = dirInfo.LengthAttr;
                netdir.DefaultOutputLengthUnits = dirInfo.LengthUnits;
                netdir.RoadClassAttributeName = dirInfo.RoadClassAttr;
                netdir.TimeAttributeName = dirInfo.TimeAttr;

                deNetworkDataset.Directions = netdir;
            }

            IArray sources = new ArrayClass();
            foreach (INetworkSource ns in EnumerateNetworkSources())
                sources.Add(ns);

            IArray attrs = new ArrayClass();
            foreach (var na in _networkAttrs)
                attrs.Add(na);

            deNetworkDataset.Sources = sources;
            deNetworkDataset.Attributes = attrs;

            // Get the feature dataset extension and create the network dataset from the data element.
            IFeatureDatasetExtension fdExtension = ((IFeatureDatasetExtensionContainer)_osmDataset).FindExtension(esriDatasetType.esriDTNetworkDataset);
            _networkDataset = (INetworkDataset)((IDatasetContainer2)fdExtension).CreateDataset((IDEDataset)deNetworkDataset);
        }

        /// <summary>Builds the network dataset</summary>
        private void BuildNDS()
        {
            INetworkBuild networkBuild = (INetworkBuild)_networkDataset;
            networkBuild.BuildNetwork(_extent);
        }

        /// <summary>Enumerates a sequence of current INetworkSource objects</summary>
        private IEnumerable<INetworkSource> EnumerateNetworkSources()
        {
            foreach (INetworkSource ns in _edgeSources.OfType<INetworkSource>())
                yield return ns;

            foreach (INetworkSource ns in _junctionSources.OfType<INetworkSource>())
                yield return ns;

            if (_turnSource != null)
                yield return _turnSource;
        }

        /// <summary>Returns a new string with the given suffix appended to the dataset base name</summary>
        private string GetFullClassName(string suffix)
        {
            if (string.IsNullOrEmpty(suffix))
                return string.Empty;

            suffix.TrimStart('\\', '_');
            if (string.IsNullOrEmpty(suffix))
                return string.Empty;

            return _ndsName + "_" + suffix;
        }
    }
}
