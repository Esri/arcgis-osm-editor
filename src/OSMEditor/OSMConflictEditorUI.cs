// (c) Copyright Esri, 2010 - 2016
// This source is subject to the Apache 2.0 License.
// Please see http://www.apache.org/licenses/LICENSE-2.0.html for details.
// All other rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.esriSystem;
using System.Resources;
using ESRI.ArcGIS.DataSourcesGDB;
using ESRI.ArcGIS.Geometry;
using System.Globalization;
using System.Xml.Serialization;
using System.Web;

using System.Runtime.InteropServices;
using ESRI.ArcGIS.Display;
using ESRI.ArcGIS.ArcMapUI;
using ESRI.ArcGIS.Controls;
using System.Net;
using System.IO;
using System.Xml;
using ESRI.ArcGIS.OSM.GeoProcessing;
using ESRI.ArcGIS.Editor;
using ESRI.ArcGIS.ADF;
using ESRI.ArcGIS.OSM.OSMClassExtension;

namespace ESRI.ArcGIS.OSM.Editor
{
    [ComVisible(false)]
    [ProgId("OSMEditor.OSMConflictEditorUI")]
    [Guid("20A630E5-3131-4A43-AAF7-7682146D0CCD")]
    public partial class OSMConflictEditorUI : Form
    {
        private IMap m_currentMap = null;
        private List<ITable> m_revisionTables = null;
        private Dictionary<string, ITable> m_allRevisionTables = new Dictionary<string, ITable>();
        private ResourceManager resourceManager = null;
        private IFeatureClass m_osmPointsFC = null;
        private IFeatureClass m_osmLinesFC = null;
        private IFeatureClass m_osmPolygonFC = null;
        private IDocumentDefaultSymbols m_documentDefaultSymbols = null;
        private IEditor m_editor = null;
        private string m_osmBaseURL = String.Empty;
        private TreeNode m_rightClickTreeNode = null;
        private OSMUtility _osmUtility = null;

        public OSMConflictEditorUI()
        {
            InitializeComponent();

            resourceManager = new ResourceManager("ESRI.ArcGIS.OSM.Editor.OSMFeatureInspectorStrings", this.GetType().Assembly);
            _osmUtility = new OSMUtility();

            this.OSMAttributes.HeaderText = resourceManager.GetString("OSMEditor_ConflictEditor_osmattributes_header");
            this.LocalOSMFeature.HeaderText = resourceManager.GetString("OSMEditor_ConflictEditor_localOSMFeature_header");
            this.ServerOSMFeature.HeaderText = resourceManager.GetString("OSMEditor_ConflictEditor_serverOSMFeature_header");
        }

        public IMap FocusMap
        {
            set
            {
                m_currentMap = value;
            }
        }

        public string OSMBaseURL
        {
            set
            {
                m_osmBaseURL = value;
            }
        }

        public IDocumentDefaultSymbols defaultSymbols
        {
            set
            {
                m_documentDefaultSymbols = value;
            }
        }

        public List<ITable> revisionTables
        {
            set
            {
                m_revisionTables = value;
            }
        }

        public IEditor Editor
        {
            set
            {
                m_editor = value;
            }
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // attempting to load the revision tables from the map
            if (m_currentMap != null)
            {
                FindTablesInsideCurrentMap();
            }

            // and/or use the provided list of tables of revision
            if (m_revisionTables != null)
            {
                for (int m_revionTabIndex = 0; m_revionTabIndex < m_revisionTables.Count; m_revionTabIndex++)
                {
                    IDataset tableDataset = m_revisionTables[m_revionTabIndex] as IDataset;

                    if (m_allRevisionTables.ContainsKey(tableDataset.Name) == false)
                    {
                        m_allRevisionTables.Add(tableDataset.Name, m_revisionTables[m_revionTabIndex]);
                    }
                }
            }

            // warn the user that no revision info is available
            if (m_allRevisionTables.Count == 0)
            {
                MessageBox.Show(resourceManager.GetString("OSMEditor_ConflictEditor_norevisionTables"), resourceManager.GetString("OSMEditor_ConflictEditor_norevisionTables_title"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // prepare the map controls
            for (int layerIndex = 0; layerIndex < axMapControl1.LayerCount; layerIndex++)
            {
                axMapControl1.DeleteLayer(layerIndex);
            }

            for (int layerIndex = 0; layerIndex < axMapControl2.LayerCount; layerIndex++)
            {
                axMapControl2.DeleteLayer(layerIndex);
            }

            if (m_currentMap != null)
            {
                IEnumLayer enumLayer = m_currentMap.get_Layers(null, false);

                ILayer basemapLayer = enumLayer.Next();

                while (basemapLayer != null)
                {
                    if (basemapLayer.Valid == true)
                    {
                        if (basemapLayer is IBasemapLayer)
                        {
                            axMapControl1.Map.AddLayer(basemapLayer);
                            axMapControl2.Map.AddLayer(basemapLayer);
                        }
                    }

                    basemapLayer = enumLayer.Next();
                }
            }

            axMapControl1.OnExtentUpdated += new IMapControlEvents2_Ax_OnExtentUpdatedEventHandler(mapControlOnExtentUpdated);
            axMapControl2.OnExtentUpdated += new IMapControlEvents2_Ax_OnExtentUpdatedEventHandler(mapControlOnExtentUpdated);

            m_osmPointsFC = CreatePointFeatureClass("osm_points", null, null);
            m_osmLinesFC = CreateLineFeatureClass("osm_lines", null, null);
            m_osmPolygonFC = CreatePolygonFeatureClass("osm_polygons", null, null);

            errorTreeView.Nodes.Add(resourceManager.GetString("OSMEditor_ConflictEditor_prepareCollectData"));
            errorTreeView.Refresh();

            // prepare the tree view
            errorTreeView.BeginUpdate();

            errorTreeView.Nodes.Clear();
            int firstLevelNodeIndex = 0;

            foreach (ITable revisionTable in m_allRevisionTables.Values)
            {
                errorTreeView.Nodes.Add(new TreeNode(((IDataset)revisionTable).Name));

                // Find the elements with a conflict
                int osmIDFieldIndex = revisionTable.Fields.FindField("osmoldid");
                int sourceFCNameFieldIndex = revisionTable.Fields.FindField("sourcefcname");
                int errorMessageFieldIndex = revisionTable.Fields.FindField("osmerrormessage");
                int elementTypeFieldIndex = revisionTable.Fields.FindField("osmelementtype");
                int errorStatusCodeFieldIndex = revisionTable.Fields.FindField("osmstatuscode");

                IQueryFilter searchFilter = new QueryFilterClass();
                searchFilter.WhereClause = SqlFormatterExt.SqlIdentifier(revisionTable, "osmstatuscode") + " <> 200";

                using (ESRI.ArcGIS.OSM.OSMClassExtension.ComReleaser comReleaser = new ESRI.ArcGIS.OSM.OSMClassExtension.ComReleaser())
                {
                    IFeatureWorkspace featureWorkspace = ((IDataset)revisionTable).Workspace as IFeatureWorkspace;
                    comReleaser.ManageLifetime(featureWorkspace);

                    ICursor searchCursor = revisionTable.Search(searchFilter, false);
                    comReleaser.ManageLifetime(searchCursor);

                    IRow errorRow = searchCursor.NextRow();
                    comReleaser.ManageLifetime(errorRow);

                    while (errorRow != null)
                    {
                        string osmElementType = errorRow.get_Value(elementTypeFieldIndex).ToString();
                        string osmIDString = errorRow.get_Value(osmIDFieldIndex).ToString();

                        RequestOSMServerData(osmElementType, osmIDString, m_osmPointsFC, m_osmLinesFC, m_osmPolygonFC);

                        string osmTypeSuffix = RetrieveOSMTypeAddendum(errorRow, sourceFCNameFieldIndex);

                        string errorMessage = errorRow.get_Value(errorMessageFieldIndex) as string;

                        TreeNode errorNode = new TreeNode("(" + osmIDString + ", " + osmTypeSuffix + ")");

                        if (String.IsNullOrEmpty(errorMessage) == false)
                        {
                            errorNode.ToolTipText = errorMessage;
                        }

                        int errorStatusCode = -1;
                        if (errorStatusCodeFieldIndex != -1)
                        {
                            errorStatusCode = (int) errorRow.get_Value(errorStatusCodeFieldIndex);
                        }

                        conflictTag errorNodeConflictTag = new conflictTag(errorRow.OID, osmIDString, errorStatusCode, errorRow.get_Value(sourceFCNameFieldIndex).ToString());

                        errorNode.Tag = errorNodeConflictTag;
                        errorNode.ContextMenuStrip = CreateErrorContextMenu(errorStatusCode, "");

                        errorTreeView.Nodes[firstLevelNodeIndex].Nodes.Add(errorNode);

                        errorTreeView.Nodes[firstLevelNodeIndex].ExpandAll();

                        errorRow = searchCursor.NextRow();
                        comReleaser.ManageLifetime(errorRow);
                    }
                }

                firstLevelNodeIndex = firstLevelNodeIndex + 1;
            }

            errorTreeView.EndUpdate();

            // clean the remaining UI components
            UpdateDataGridViewColumn(1, Color.White);
            UpdateDataGridViewColumn(2, Color.White);
            dataGridView2.Rows.Clear();
            dataGridView2.Invalidate();

            DeleteAllGraphicElements();
        }

        private void DeleteAllGraphicElements()
        {
            IMapControl4 mapControl = axMapControl1.Object as IMapControl4;
            IGraphicsContainer graphicsContainer = mapControl.Map.BasicGraphicsLayer as IGraphicsContainer;

            if (graphicsContainer != null)
            {
                graphicsContainer.DeleteAllElements();
            }

            mapControl.Refresh(esriViewDrawPhase.esriViewGraphics, null, null);
            
            mapControl = axMapControl2.Object as IMapControl4;
            graphicsContainer = mapControl.Map.BasicGraphicsLayer as IGraphicsContainer;

            if (graphicsContainer != null)
            {
                graphicsContainer.DeleteAllElements();
            }

            mapControl.Refresh(esriViewDrawPhase.esriViewGraphics, null, null);
        }

        void mapControlOnExtentUpdated(object sender, IMapControlEvents2_OnExtentUpdatedEvent e)
        {
            AxMapControl axControl= sender as AxMapControl;

            if (axControl == null)
            {
                return;
            }

            IMapControl4 mapControl4 = axControl.Object as IMapControl4;

            if (mapControl4 == null)
            {
                return;
            }

            IGraphicsContainer basicGraphicsContainer = mapControl4.Map.BasicGraphicsLayer as IGraphicsContainer;

            basicGraphicsContainer.Reset();
            IElement currentElement = basicGraphicsContainer.Next();

            IEnvelope extentEnvelope = e.newEnvelope as IEnvelope;
            IDisplayTransformation displayTransformation = e.displayTransformation as IDisplayTransformation;

            tagRECT displayRectangle = displayTransformation.get_DeviceFrame();

            while (currentElement != null)
            {
                if (((IElementProperties)currentElement).Name == "DeleteMarker")
                {
                    IPoint deleteMarkerLocation = new PointClass();
                    deleteMarkerLocation.SpatialReference = mapControl4.SpatialReference;

                    deleteMarkerLocation = displayTransformation.ToMapPoint(displayRectangle.left + 45, displayRectangle.bottom - 45);

                    currentElement.Geometry = deleteMarkerLocation;
                }
                else if (((IElementProperties)currentElement).Name == "AcceptMarker")
                {
                    IPoint acceptMarkerLocation = new PointClass();
                    acceptMarkerLocation.SpatialReference = mapControl4.SpatialReference;

                    acceptMarkerLocation = displayTransformation.ToMapPoint(displayRectangle.left + 45, displayRectangle.bottom - 45);

                    currentElement.Geometry = acceptMarkerLocation;
                }

                currentElement = basicGraphicsContainer.Next();
            }

            mapControl4.Refresh(esriViewDrawPhase.esriViewGraphics, null, null);
        }

        private void RequestOSMServerData(string osmElementType, string osmIDString, IFeatureClass osmPointsFC, IFeatureClass osmLinesFC, IFeatureClass osmPolygonFC)
        {
            osm osmDocument = null;
            System.Xml.Serialization.XmlSerializer serializer = new XmlSerializer(typeof(osm));

            HttpWebRequest httpClient = null;
            try
            {
                string sRequestUrl = "";
                if (osmElementType == "node")
                    sRequestUrl = m_osmBaseURL + "/api/0.6/" + osmElementType + "/" + osmIDString;
                else
                    sRequestUrl = m_osmBaseURL + "/api/0.6/" + osmElementType + "/" + osmIDString + "/full";

                httpClient = HttpWebRequest.Create(sRequestUrl) as HttpWebRequest;
                httpClient = OSMGPDownload.AssignProxyandCredentials(httpClient);

                using (HttpWebResponse httpResponse = httpClient.GetResponse() as HttpWebResponse)
                {
                    Stream stream = httpResponse.GetResponseStream();

                    XmlTextReader xmlTextReader = new XmlTextReader(stream);
                    osmDocument = serializer.Deserialize(xmlTextReader) as osm;

                    if (osmElementType.Equals("node"))
                    {
                        NodeToFeatureClass(osmDocument, osmPointsFC);
                    }
                    else if (osmElementType.Equals("way"))
                    {
                        WayToFeatureClass(osmDocument, osmLinesFC, osmPolygonFC);
                    }
                    else if (osmElementType.Equals("relation"))
                    {
                    }

                    stream.Close();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }

        private void FillDataGridView(IRow localFeature, IRow serverFeature)
        {
            dataGridView2.Rows.Clear();

            int localosmIDFieldIndex = localFeature.Fields.FindField("OSMID");
            int serverosmIDFieldIndex = serverFeature.Fields.FindField("OSMID");

            int localosmTagsFieldIndex = localFeature.Fields.FindField("osmTags");
            int serverosmTagsFieldIndex = serverFeature.Fields.FindField("osmTags");

            int localosmVisibleFieldIndex = localFeature.Fields.FindField("osmVisible");
            int serverosmVisibleFieldIndex = serverFeature.Fields.FindField("osmVisible");

            int localOSMVersionFieldIndex = localFeature.Fields.FindField("osmversion");
            int serverOSMVersionFieldIndex = serverFeature.Fields.FindField("osmversion");

            int localOSMChangesetFieldIndex = localFeature.Fields.FindField("osmchangeset");
            int serverOSMChangesetFieldIndex = serverFeature.Fields.FindField("osmchangeset");

            int localOSMTimeStampFieldIndex = localFeature.Fields.FindField("osmtimestamp");
            int serverOSMTimeStampFieldIndex = serverFeature.Fields.FindField("osmtimestamp");

            if (localosmIDFieldIndex != -1 && serverosmIDFieldIndex != -1)
            {
                insertLocalServerRow("OSM ID", localFeature.get_Value(localosmIDFieldIndex), serverFeature.get_Value(serverosmIDFieldIndex));
            }

            if (localosmVisibleFieldIndex != -1 && serverosmVisibleFieldIndex != -1)
            {
                insertLocalServerRow("OSM Visibility", localFeature.get_Value(localosmVisibleFieldIndex), serverFeature.get_Value(serverosmVisibleFieldIndex));
            }

            if (localOSMVersionFieldIndex != -1 && serverOSMVersionFieldIndex != -1)
            {
                insertLocalServerRow("OSM Version", localFeature.get_Value(localOSMVersionFieldIndex), serverFeature.get_Value(serverOSMVersionFieldIndex));
            }

            if (localOSMChangesetFieldIndex != -1 && serverOSMChangesetFieldIndex != -1)
            {
                insertLocalServerRow("OSM Changeset", localFeature.get_Value(localOSMChangesetFieldIndex), serverFeature.get_Value(serverOSMChangesetFieldIndex));
            }

            if (localOSMTimeStampFieldIndex != -1 && serverOSMTimeStampFieldIndex != -1)
            {
                insertLocalServerRow("TimeStamp", localFeature.get_Value(localOSMTimeStampFieldIndex), serverFeature.get_Value(serverOSMTimeStampFieldIndex));
            }

            if (localosmTagsFieldIndex != -1 && serverosmTagsFieldIndex != -1)
            {
                tag[] localOSMTags = null;

                IObject localObject = localFeature as IObject;
                if (localObject != null)
                {
                    IDataset localObjectDataset = localObject.Class as IDataset;
                    if (localObjectDataset != null)
                    {
                        localOSMTags = _osmUtility.retrieveOSMTags(localFeature, localosmTagsFieldIndex, localObjectDataset.Workspace);
                    }
                }
                else
                {
                    localOSMTags = _osmUtility.retrieveOSMTags(localFeature, localosmTagsFieldIndex, null);
                }

                tag[] serverOSMTags = _osmUtility.retrieveOSMTags(serverFeature, serverosmTagsFieldIndex, null);

                Dictionary<string, tag> localTagDictionary = new Dictionary<string, tag>();

                 if (localOSMTags != null)
                 {
                     for (int tagIndex = 0; tagIndex < localOSMTags.Length; tagIndex++)
                     {
                         if (localTagDictionary.ContainsKey(localOSMTags[tagIndex].k) == false)
                         {
                             localTagDictionary.Add(localOSMTags[tagIndex].k, localOSMTags[tagIndex]);
                         }
                     }
                 }

                 Dictionary<string, tag> serverTagDictionary = new Dictionary<string, tag>();

                 if (serverOSMTags != null)
                 {
                     for (int tagIndex = 0; tagIndex < serverOSMTags.Length; tagIndex++)
                     {
                         if (serverTagDictionary.ContainsKey(serverOSMTags[tagIndex].k) == false)
                         {
                             serverTagDictionary.Add(serverOSMTags[tagIndex].k, serverOSMTags[tagIndex]);
                         }
                     }
                 }

                 var unionedKeys = localTagDictionary.Keys.Union(serverTagDictionary.Keys);

                 foreach (string currentKey in unionedKeys)
                 {
                     object localValue = null;
                     if (localTagDictionary.ContainsKey(currentKey))
                     {
                         localValue = localTagDictionary[currentKey].v;
                     }

                     object serverValue = null;
                     if (serverTagDictionary.ContainsKey(currentKey))
                     {
                         serverValue = serverTagDictionary[currentKey].v;
                     }

                     insertLocalServerRow(currentKey, localValue, serverValue);
                 }
            }

            dataGridView2.ClearSelection();
        }

        private bool DoesHaveKeys(object osmDocumentItem)
        {
            bool doesHaveKeys = false;

            try
            {
                if (osmDocumentItem is node)
                {
                    node currentNode = osmDocumentItem as node;

                    if (currentNode.tag != null)
                    {
                        foreach (tag nodeTag in currentNode.tag)
                        {
                            if (nodeTag.k.ToLower().Equals("created_by"))
                            {
                            }
                            else
                            {
                                doesHaveKeys = true;
                                break;
                            }
                        }
                    }
                }
                else if (osmDocumentItem is way)
                {
                    way currentway = osmDocumentItem as way;

                    if (currentway.tag != null)
                    {
                        foreach (tag waytag in currentway.tag)
                        {
                            if (waytag.k.ToLower().Equals("area") && waytag.v.ToLower().Equals("yes"))
                            {

                            }
                            else if (waytag.k.ToLower().Equals("created_by"))
                            {
                            }
                            else
                            {
                                doesHaveKeys = true;
                                break;
                            }
                        }
                    }
                }
                else if (osmDocumentItem is relation)
                {
                    relation currentRelation = osmDocumentItem as relation;

                    foreach (var relationItem in currentRelation.Items)
                    {
                        if (relationItem is tag)
                        {
                            doesHaveKeys = true;
                            break;
                        }
                    }
                }
            }
            catch
            {
            }

            return doesHaveKeys;
        }

        private void NodeToFeatureClass(osm osmDocument, IFeatureClass pointFeatureClass)
        {
            // initial safety check -- consider throwing an exception instead
            if (osmDocument == null)
            {
                return;
            }

            if (osmDocument.Items == null)
            {
                return;
            }

            if (pointFeatureClass == null)
            {
                return;
            }

            // 
            int tagCollectionPointFieldIndex = pointFeatureClass.FindField("osmTags");
            int osmUserPointFieldIndex = pointFeatureClass.FindField("osmuser");
            int osmUIDPointFieldIndex = pointFeatureClass.FindField("osmuid");
            int osmVisiblePointFieldIndex = pointFeatureClass.FindField("osmvisible");
            int osmVersionPointFieldIndex = pointFeatureClass.FindField("osmversion");
            int osmChangesetPointFieldIndex = pointFeatureClass.FindField("osmchangeset");
            int osmTimeStampPointFieldIndex = pointFeatureClass.FindField("osmtimestamp");
            int osmIDPointFieldIndex = pointFeatureClass.FindField("OSMID");

            ISpatialReferenceFactory srFactory = new SpatialReferenceEnvironmentClass();
            ISpatialReference wgs84 = srFactory.CreateGeographicCoordinateSystem((int)esriSRGeoCSType.esriSRGeoCS_WGS1984) as ISpatialReference;

            foreach (var documentItem in osmDocument.Items)
            {
                if (documentItem is node)
                {
                   // if (DoesHaveKeys(documentItem))
                   // {
                        node currentNode = documentItem as node;

                        IFeature newPointFeature = pointFeatureClass.CreateFeature();

                        IPoint pointGeometry = new PointClass();
                        pointGeometry.X = Convert.ToDouble(currentNode.lon, new CultureInfo("en-US"));
                        pointGeometry.Y = Convert.ToDouble(currentNode.lat, new CultureInfo("en-US"));
                        pointGeometry.SpatialReference = wgs84;

                        newPointFeature.Shape = pointGeometry;

                        if (osmUserPointFieldIndex != -1)
                        {
                        newPointFeature.set_Value(osmUserPointFieldIndex, currentNode.user);
                        }
                        if (osmUIDPointFieldIndex != -1)
                        {
                            newPointFeature.set_Value(osmUIDPointFieldIndex, currentNode.uid);
                        }
                        if (osmVisiblePointFieldIndex != -1)
                        {
                            newPointFeature.set_Value(osmVisiblePointFieldIndex, currentNode.visible);
                        }
                        if (osmVersionPointFieldIndex != -1)
                        {
                            newPointFeature.set_Value(osmVersionPointFieldIndex, Convert.ToInt32(currentNode.version));
                        }
                        if (osmChangesetPointFieldIndex != -1)
                        {
                            newPointFeature.set_Value(osmChangesetPointFieldIndex, Convert.ToInt32(currentNode.changeset));
                        }
                        if (osmTimeStampPointFieldIndex != -1)
                        {
                            newPointFeature.set_Value(osmTimeStampPointFieldIndex, Convert.ToDateTime(currentNode.timestamp));
                        }
                        if (osmIDPointFieldIndex != -1)
                        {
                            newPointFeature.set_Value(osmIDPointFieldIndex, Convert.ToInt32(currentNode.id));
                        }

                        _osmUtility.insertOSMTags(tagCollectionPointFieldIndex, newPointFeature, currentNode.tag);

                        newPointFeature.Store();
                 //   }
                }
            }
        }

        private void WayToFeatureClass(osm osmDocument, IFeatureClass lineFeatureClass, IFeatureClass polygonFeatureClass)
        {
            // initial safety check -- consider throwing an exception instead
            if (osmDocument == null)
            {
                return;
            }

            if (osmDocument.Items == null)
            {
                return;
            }

            int osmTagsLineFieldIndex = -1;
            int osmUserLineFieldIndex = -1;
            int osmUIDLineFieldIndex = -1;
            int osmVisibleLineFieldIndex = -1;
            int osmVersionLineFieldIndex = -1;
            int osmChangesetLineFieldIndex = -1;
            int osmTimeStampLineFieldIndex = -1;
            int osmIDLineFieldIndex = -1;

            int osmTagsPolygonFieldIndex = -1;
            int osmUserPolygonFieldIndex = -1;
            int osmUIDPolygonFieldIndex = -1;
            int osmVisiblePolygonFieldIndex = -1;
            int osmVersionPolygonFieldIndex = -1;
            int osmChangesetPolygonFieldIndex = -1;
            int osmTimeStampPolygonFieldIndex = -1;
            int osmIDPolygonFieldIndex = -1;

            if (lineFeatureClass != null)
            {
                // 
                osmTagsLineFieldIndex = lineFeatureClass.FindField("osmTags");
                osmUserLineFieldIndex = lineFeatureClass.FindField("osmuser");
                osmUIDLineFieldIndex = lineFeatureClass.FindField("osmuid");
                osmVisibleLineFieldIndex = lineFeatureClass.FindField("osmvisible");
                osmVersionLineFieldIndex = lineFeatureClass.FindField("osmversion");
                osmChangesetLineFieldIndex = lineFeatureClass.FindField("osmchangeset");
                osmTimeStampLineFieldIndex = lineFeatureClass.FindField("osmtimestamp");
                osmIDLineFieldIndex = lineFeatureClass.FindField("OSMID");
            }

            if (polygonFeatureClass != null)
            {
                osmTagsPolygonFieldIndex = polygonFeatureClass.FindField("osmTags");
                osmUserPolygonFieldIndex = polygonFeatureClass.FindField("osmuser");
                osmUIDPolygonFieldIndex = polygonFeatureClass.FindField("osmuid");
                osmVisiblePolygonFieldIndex = polygonFeatureClass.FindField("osmvisible");
                osmVersionPolygonFieldIndex = polygonFeatureClass.FindField("osmversion");
                osmChangesetPolygonFieldIndex = polygonFeatureClass.FindField("osmchangeset");
                osmTimeStampPolygonFieldIndex = polygonFeatureClass.FindField("osmtimestamp");
                osmIDPolygonFieldIndex = polygonFeatureClass.FindField("OSMID");
            }

            ISpatialReferenceFactory srFactory = new SpatialReferenceEnvironmentClass();
            ISpatialReference wgs84 = srFactory.CreateGeographicCoordinateSystem((int)esriSRGeoCSType.esriSRGeoCS_WGS1984) as ISpatialReference;


            Dictionary<string, IPoint> nodeCollection = new Dictionary<string, IPoint>();
            foreach (var documentItem in osmDocument.Items)
            {
                if (documentItem is ESRI.ArcGIS.OSM.OSMClassExtension.node)
                {
                    ESRI.ArcGIS.OSM.OSMClassExtension.node currentNode = documentItem as ESRI.ArcGIS.OSM.OSMClassExtension.node;

                    if (nodeCollection.ContainsKey(currentNode.id) == false)
                    {
                        IPoint newNodePoint = new PointClass();
                        newNodePoint.X = Convert.ToDouble(currentNode.lon, new CultureInfo("en-US"));
                        newNodePoint.Y = Convert.ToDouble(currentNode.lat, new CultureInfo("en-US"));
                        newNodePoint.SpatialReference = wgs84;

                        IPointIDAware idAware = newNodePoint as IPointIDAware;
                        idAware.PointIDAware = true;
                        newNodePoint.ID = Convert.ToInt32(currentNode.id);

                        nodeCollection.Add(currentNode.id, newNodePoint);
                    }
                }
            }

            int osmTagsFieldIndex = -1;
            int osmUserFieldIndex = -1;
            int osmUIDFieldIndex = -1;
            int osmVisibleFieldIndex = -1;
            int osmVersionFieldIndex = -1;
            int osmChangesetFieldIndex = -1;
            int osmTimeStampFieldIndex = -1;
            int osmIDFieldIndex = -1;

            foreach (var documentItem in osmDocument.Items)
            {
                if (documentItem is ESRI.ArcGIS.OSM.OSMClassExtension.way)
                {
                    ESRI.ArcGIS.OSM.OSMClassExtension.way currentWay = documentItem as ESRI.ArcGIS.OSM.OSMClassExtension.way;
                    IFeatureClass targetFeatureClass = null;

                    if (OSMToolHelper.IsThisWayALine(currentWay))
                    {
                        targetFeatureClass = lineFeatureClass;
                        osmTagsFieldIndex = osmTagsLineFieldIndex;
                        osmUserFieldIndex = osmUserLineFieldIndex;
                        osmUIDFieldIndex = osmUIDLineFieldIndex;
                        osmVisibleFieldIndex = osmVisibleLineFieldIndex;
                        osmVersionFieldIndex = osmVersionLineFieldIndex;
                        osmChangesetFieldIndex = osmChangesetLineFieldIndex;
                        osmTimeStampFieldIndex = osmTimeStampLineFieldIndex;
                        osmIDFieldIndex = osmIDLineFieldIndex;
                    }
                    else
                    {
                        targetFeatureClass = polygonFeatureClass;
                        osmTagsFieldIndex = osmTagsPolygonFieldIndex;
                        osmUserFieldIndex = osmUserPolygonFieldIndex;
                        osmUIDFieldIndex = osmUIDPolygonFieldIndex;
                        osmVisibleFieldIndex = osmVisiblePolygonFieldIndex;
                        osmVersionFieldIndex = osmVersionPolygonFieldIndex;
                        osmChangesetFieldIndex = osmChangesetPolygonFieldIndex;
                        osmTimeStampFieldIndex = osmChangesetPolygonFieldIndex;
                        osmIDFieldIndex = osmIDPolygonFieldIndex;
                    }


                    if (targetFeatureClass != null)
                    {
                        IFeature newWayFeature = targetFeatureClass.CreateFeature();

                        IPointCollection pointCollection = null;
                        if (targetFeatureClass.ShapeType == esriGeometryType.esriGeometryPolyline)
                        {
                            pointCollection = new PolylineClass() as IPointCollection;
                        }
                        else
                        {
                            pointCollection = new PolygonClass() as IPointCollection;
                        }

                        IPointIDAware topGeometryIDAware = pointCollection as IPointIDAware;
                        topGeometryIDAware.PointIDAware = true;

                        foreach (var wayNode in currentWay.nd)
                        {
                            if (nodeCollection.ContainsKey(wayNode.@ref))
                            {
                                pointCollection.AddPoint(nodeCollection[wayNode.@ref]);
                            }
                        }

                        newWayFeature.Shape = pointCollection as IGeometry;

                        if (osmUserFieldIndex != -1)
                        {
                            newWayFeature.set_Value(osmUserFieldIndex, currentWay.user);
                        }
                        if (osmUIDFieldIndex != -1)
                        {
                            newWayFeature.set_Value(osmUIDFieldIndex, currentWay.uid);
                        }
                        if (osmVisibleFieldIndex != -1)
                        {
                            newWayFeature.set_Value(osmVisibleFieldIndex, currentWay.visible.ToString());
                        }
                        if (osmVersionFieldIndex != -1)
                        {
                            newWayFeature.set_Value(osmVersionFieldIndex, Convert.ToInt32(currentWay.version));
                        }
                        if (osmChangesetFieldIndex != -1)
                        {
                            newWayFeature.set_Value(osmChangesetFieldIndex, Convert.ToInt32(currentWay.changeset));
                        }
                        if (osmTimeStampFieldIndex != -1)
                        {
                            newWayFeature.set_Value(osmTimeStampFieldIndex, Convert.ToDateTime(currentWay.timestamp));
                        }
                        if (osmIDFieldIndex != -1)
                        {
                            newWayFeature.set_Value(osmIDFieldIndex, Convert.ToInt32(currentWay.id));
                        }

                        _osmUtility.insertOSMTags(osmTagsFieldIndex, newWayFeature, currentWay.tag);

                        newWayFeature.Store();
                    }

                }
            }
        }

        private void insertLocalServerRow(string rowLabel, object localValue, object serverValue)
        {
            DataGridViewRow newGridViewRow = new DataGridViewRow();
            DataGridViewTextBoxCell label = new DataGridViewTextBoxCell();
            label.Value = rowLabel;

            DataGridViewTextBoxCell localCell = new DataGridViewTextBoxCell();
            if (localValue != null)
            {
                localCell.Value = localValue;
            }

            DataGridViewTextBoxCell serverCell = new DataGridViewTextBoxCell();
            if (serverValue != null)
            {
                serverCell.Value = serverValue;
            }

            newGridViewRow.Cells.Add(label);
            newGridViewRow.Cells.Add(localCell);
            newGridViewRow.Cells.Add(serverCell);

            dataGridView2.Rows.Add(newGridViewRow);

        }

        private string RetrieveOSMTypeAddendum(IRow errorRow, int sourceFeatureClassNameIndex)
        {
            string osmTypeString = resourceManager.GetString("OSMEditor_ConflictEditor_osmtypedescription_unk");

            string fcsourcename = errorRow.get_Value(sourceFeatureClassNameIndex).ToString();

            if (fcsourcename.Contains("_osm_pt") == true)
            {
                osmTypeString = resourceManager.GetString("OSMEditor_ConflictEditor_osmtypedescription_pt"); ;
            }
            else if (fcsourcename.Contains("_osm_ln") == true)
            {
                osmTypeString = resourceManager.GetString("OSMEditor_ConflictEditor_osmtypedescription_ln"); ;
            }
            else if (fcsourcename.Contains("_osm_ply") == true)
            {
                osmTypeString = resourceManager.GetString("OSMEditor_ConflictEditor_osmtypedescription_ply"); ;
            }
            else if (fcsourcename.Contains("_osm_relation") == true)
            {
                osmTypeString = resourceManager.GetString("OSMEditor_ConflictEditor_osmtypedescription_relation"); ;
            }

            return osmTypeString;
        }

        private void FindTablesInsideCurrentMap()
        {
            if (m_currentMap == null)
            {
                return;
            }


            // let's get all standalone tables
            IStandaloneTableCollection tableCollection = m_currentMap as IStandaloneTableCollection;

            if (tableCollection == null)
            {
                return;
            }

            for (int tableIndex = 0; tableIndex < tableCollection.StandaloneTableCount; tableIndex++)
            {
                IStandaloneTable currentStandaloneTable = tableCollection.get_StandaloneTable(tableIndex);

                if (currentStandaloneTable.Name.Contains("_osm_revision") == true)
                {
                    if (m_allRevisionTables.ContainsKey(currentStandaloneTable.Name) == false)
                    {
                        if (currentStandaloneTable.Valid == true)
                        {
                            m_allRevisionTables.Add(currentStandaloneTable.Name, currentStandaloneTable.Table);
                        }
                    }
                }
            }

            // as well as the standalone revision tables "linked (by name)" to the loaded feature classes
            UID geofeatureLayerCLSID = new UIDClass();
            geofeatureLayerCLSID.Value = "{E156D7E5-22AF-11D3-9F99-00C04F6BC78E}";
            IEnumLayer enumLayer = m_currentMap.get_Layers(geofeatureLayerCLSID, true);

            enumLayer.Reset();

            IGeoFeatureLayer geoFeatureLayer = enumLayer.Next() as IGeoFeatureLayer;

            while (geoFeatureLayer != null)
            {
                if (geoFeatureLayer.Valid == true)
                {
                    string fcAliasName = ((IDataset)geoFeatureLayer.FeatureClass).Name;
                    int baseNameIndex = fcAliasName.IndexOf("_osm_");

                    if (baseNameIndex > -1)
                    {
                        string baseName = fcAliasName.Substring(0, baseNameIndex);
                        string tableName = baseName + "_osm_revision";

                        if (m_allRevisionTables.ContainsKey(tableName) == false)
                        {
                            IFeatureWorkspace featureWorkspace = ((IDataset)geoFeatureLayer.FeatureClass).Workspace as IFeatureWorkspace;

                            ITable revisionTable = null;
                            try
                            {
                                revisionTable = featureWorkspace.OpenTable(tableName);
                            }
                            catch
                            {

                            }

                            if (revisionTable != null)
                            {
                                m_allRevisionTables.Add(tableName, revisionTable);
                            }
                        }
                    }

                    geoFeatureLayer = enumLayer.Next() as IGeoFeatureLayer;
                }
            }
        }

        private ESRI.ArcGIS.Geodatabase.IFeatureClass CreatePointFeatureClass(System.String featureClassName, ESRI.ArcGIS.Geodatabase.IFields fields, ESRI.ArcGIS.esriSystem.UID CLSID)
        {
            if (featureClassName == "") return null; // name was not passed in 

            IWorkspaceFactory workspaceFactory = new InMemoryWorkspaceFactoryClass();
            // Create a new in-memory workspace. This returns a name object.
            IWorkspaceName workspaceName = workspaceFactory.Create(null, "OSMPointsWorkspace", null, 0);
            IName name = (IName)workspaceName;

            // Open the workspace through the name object.
            IWorkspace workspace = (IWorkspace)name.Open();

            ESRI.ArcGIS.Geodatabase.IFeatureClass featureClass = null;
            ESRI.ArcGIS.Geodatabase.IFeatureWorkspace featureWorkspace = (ESRI.ArcGIS.Geodatabase.IFeatureWorkspace)workspace; // Explicit Cast

            // assign the class id value if not assigned
            if (CLSID == null)
            {
                CLSID = new ESRI.ArcGIS.esriSystem.UIDClass();
                CLSID.Value = "esriGeoDatabase.Feature";
            }

            ESRI.ArcGIS.Geodatabase.IObjectClassDescription objectClassDescription = new ESRI.ArcGIS.Geodatabase.FeatureClassDescriptionClass();

            // if a fields collection is not passed in then supply our own
            if (fields == null)
            {
                // create the fields using the required fields method
                fields = objectClassDescription.RequiredFields;
                ESRI.ArcGIS.Geodatabase.IFieldsEdit fieldsEdit = (ESRI.ArcGIS.Geodatabase.IFieldsEdit)fields; // Explicit Cast

                // add the OSM ID field
                IFieldEdit osmIDField = new FieldClass() as IFieldEdit;
                osmIDField.Name_2 = "OSMID";
                osmIDField.Type_2 = esriFieldType.esriFieldTypeString;
                osmIDField.Length_2 = 20;
                fieldsEdit.AddField((IField)osmIDField);

                // add the field for the tag cloud for all other tag/value pairs
                IFieldEdit osmXmlTagsField = new FieldClass() as IFieldEdit;
                osmXmlTagsField.Name_2 = "osmTags";
                    osmXmlTagsField.Type_2 = esriFieldType.esriFieldTypeBlob;
                fieldsEdit.AddField((IField)osmXmlTagsField);

                // user, uid, visible, version, changeset, timestamp
                IFieldEdit osmuserField = new FieldClass() as IFieldEdit;
                osmuserField.Name_2 = "osmuser";
                osmuserField.Type_2 = esriFieldType.esriFieldTypeString;
                osmuserField.Length_2 = 100;
                fieldsEdit.AddField((IField) osmuserField);

                IFieldEdit osmuidField = new FieldClass() as IFieldEdit;
                osmuidField.Name_2 = "osmuid";
                osmuidField.Type_2 = esriFieldType.esriFieldTypeInteger;
                fieldsEdit.AddField((IField)osmuidField);

                IFieldEdit osmvisibleField = new FieldClass() as IFieldEdit;
                osmvisibleField.Name_2 = "osmvisible";
                osmvisibleField.Type_2 = esriFieldType.esriFieldTypeString;
                osmvisibleField.Length_2 = 20;
                fieldsEdit.AddField((IField)osmvisibleField);

                IFieldEdit osmversionField = new FieldClass() as IFieldEdit;
                osmversionField.Name_2 = "osmversion";
                osmversionField.Type_2 = esriFieldType.esriFieldTypeSmallInteger;
                fieldsEdit.AddField((IField)osmversionField);

                IFieldEdit osmchangesetField = new FieldClass() as IFieldEdit;
                osmchangesetField.Name_2 = "osmchangeset";
                osmchangesetField.Type_2 = esriFieldType.esriFieldTypeInteger;
                fieldsEdit.AddField((IField)osmchangesetField);

                IFieldEdit osmtimestampField = new FieldClass() as IFieldEdit;
                osmtimestampField.Name_2 = "osmtimestamp";
                osmtimestampField.Type_2 = esriFieldType.esriFieldTypeDate;
                fieldsEdit.AddField((IField)osmtimestampField);

                IFieldEdit osmrelationIDField = new FieldClass() as IFieldEdit;
                osmrelationIDField.Name_2 = "osmMemberOf";
                    osmrelationIDField.Type_2 = esriFieldType.esriFieldTypeBlob;
                fieldsEdit.AddField((IField)osmrelationIDField);

                IFieldEdit hasOSMTagsField = new FieldClass() as IFieldEdit;
                hasOSMTagsField.Name_2 = "hasOSMTags";

                IFieldEdit osmSupportingElementField = new FieldClass() as IFieldEdit;
                osmSupportingElementField.Name_2 = "osmSupportingElement";
                osmSupportingElementField.Type_2 = esriFieldType.esriFieldTypeString;
                osmSupportingElementField.Length_2 = 5;
                fieldsEdit.AddField((IField) osmSupportingElementField);

                IFieldEdit wayRefCountField = new FieldClass() as IFieldEdit;
                wayRefCountField.Name_2 = "wayRefCount";
                wayRefCountField.Type_2 = esriFieldType.esriFieldTypeInteger;
                wayRefCountField.DefaultValue_2 = 0;
                fieldsEdit.AddField((IField)wayRefCountField);


                fields = (ESRI.ArcGIS.Geodatabase.IFields)fieldsEdit; // Explicit Cast
            }

            System.String strShapeField = "";

            // locate the shape field
            for (int j = 0; j < fields.FieldCount; j++)
            {
                if (fields.get_Field(j).Type == ESRI.ArcGIS.Geodatabase.esriFieldType.esriFieldTypeGeometry)
                {
                    strShapeField = fields.get_Field(j).Name;

                    // redefine geometry type

                    IFieldEdit shapeField = fields.get_Field(j) as IFieldEdit;
                    IGeometryDefEdit geometryDef = new GeometryDefClass() as IGeometryDefEdit;
                    geometryDef.GeometryType_2 = esriGeometryType.esriGeometryPoint;
                    geometryDef.HasZ_2 = false;
                    geometryDef.HasM_2 = false;
                    geometryDef.GridCount_2 = 1;
                    geometryDef.set_GridSize(0, 1);

                    ISpatialReferenceFactory spatialRefFactory = new SpatialReferenceEnvironmentClass();
                    ISpatialReference wgs84 = spatialRefFactory.CreateGeographicCoordinateSystem((int) esriSRGeoCSType.esriSRGeoCS_WGS1984) as ISpatialReference;
                    geometryDef.SpatialReference_2 = wgs84;

                    shapeField.GeometryDef_2 = (IGeometryDef)geometryDef;

                    break;
                }
            }

            // Use IFieldChecker to create a validated fields collection.
            ESRI.ArcGIS.Geodatabase.IFieldChecker fieldChecker = new ESRI.ArcGIS.Geodatabase.FieldCheckerClass();
            ESRI.ArcGIS.Geodatabase.IEnumFieldError enumFieldError = null;
            ESRI.ArcGIS.Geodatabase.IFields validatedFields = null;
            fieldChecker.ValidateWorkspace = (ESRI.ArcGIS.Geodatabase.IWorkspace)workspace;
            fieldChecker.Validate(fields, out enumFieldError, out validatedFields);

            // The enumFieldError enumerator can be inspected at this point to determine 
            // which fields were modified during validation.


            // finally create and return the feature class
                try
                {
                    featureClass = featureWorkspace.CreateFeatureClass(featureClassName, validatedFields, CLSID, null, ESRI.ArcGIS.Geodatabase.esriFeatureType.esriFTSimple, strShapeField, "");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                }

            return featureClass;
        }

        public ESRI.ArcGIS.Geodatabase.IFeatureClass CreateLineFeatureClass(System.String featureClassName, ESRI.ArcGIS.Geodatabase.IFields fields, ESRI.ArcGIS.esriSystem.UID CLSID)
        {
            if (featureClassName == "") return null; // name was not passed in 

            IWorkspaceFactory workspaceFactory = new InMemoryWorkspaceFactoryClass();
            // Create a new in-memory workspace. This returns a name object.
            IWorkspaceName workspaceName = workspaceFactory.Create(null, "OSMLineWorkspace", null, 0);
            IName name = (IName)workspaceName;

            // Open the workspace through the name object.
            IWorkspace workspace = (IWorkspace)name.Open();


            ESRI.ArcGIS.Geodatabase.IFeatureClass featureClass;
            ESRI.ArcGIS.Geodatabase.IFeatureWorkspace featureWorkspace = (ESRI.ArcGIS.Geodatabase.IFeatureWorkspace)workspace; // Explicit Cast

            // assign the class id value if not assigned
            if (CLSID == null)
            {
                CLSID = new ESRI.ArcGIS.esriSystem.UIDClass();
                CLSID.Value = "esriGeoDatabase.Feature";
            }

            ESRI.ArcGIS.Geodatabase.IObjectClassDescription objectClassDescription = new ESRI.ArcGIS.Geodatabase.FeatureClassDescriptionClass();

            // if a fields collection is not passed in then supply our own
            if (fields == null)
            {
                // create the fields using the required fields method
                fields = objectClassDescription.RequiredFields;
                ESRI.ArcGIS.Geodatabase.IFieldsEdit fieldsEdit = (ESRI.ArcGIS.Geodatabase.IFieldsEdit)fields; // Explicit Cast

                // add the OSM ID field
                IFieldEdit osmIDField = new FieldClass() as IFieldEdit;
                osmIDField.Name_2 = "OSMID";
                osmIDField.Type_2 = esriFieldType.esriFieldTypeString;
                osmIDField.Length_2 = 20;
                fieldsEdit.AddField((IField)osmIDField);

                // user, uid, visible, version, changeset, timestamp
                IFieldEdit osmuserField = new FieldClass() as IFieldEdit;
                osmuserField.Name_2 = "osmuser";
                osmuserField.Type_2 = esriFieldType.esriFieldTypeString;
                osmuserField.Length_2 = 100;
                fieldsEdit.AddField((IField)osmuserField);

                IFieldEdit osmuidField = new FieldClass() as IFieldEdit;
                osmuidField.Name_2 = "osmuid";
                osmuidField.Type_2 = esriFieldType.esriFieldTypeInteger;
                fieldsEdit.AddField((IField)osmuidField);

                IFieldEdit osmvisibleField = new FieldClass() as IFieldEdit;
                osmvisibleField.Name_2 = "osmvisible";
                osmvisibleField.Type_2 = esriFieldType.esriFieldTypeString;
                osmvisibleField.Length_2 = 20;
                fieldsEdit.AddField((IField)osmvisibleField);

                IFieldEdit osmversionField = new FieldClass() as IFieldEdit;
                osmversionField.Name_2 = "osmversion";
                osmversionField.Type_2 = esriFieldType.esriFieldTypeSmallInteger;
                fieldsEdit.AddField((IField)osmversionField);

                IFieldEdit osmchangesetField = new FieldClass() as IFieldEdit;
                osmchangesetField.Name_2 = "osmchangeset";
                osmchangesetField.Type_2 = esriFieldType.esriFieldTypeInteger;
                fieldsEdit.AddField((IField)osmchangesetField);

                IFieldEdit osmtimestampField = new FieldClass() as IFieldEdit;
                osmtimestampField.Name_2 = "osmtimestamp";
                osmtimestampField.Type_2 = esriFieldType.esriFieldTypeDate;
                fieldsEdit.AddField((IField)osmtimestampField);

                IFieldEdit osmrelationIDField = new FieldClass() as IFieldEdit;
                osmrelationIDField.Name_2 = "osmMemberOf";
                    osmrelationIDField.Type_2 = esriFieldType.esriFieldTypeBlob;
                fieldsEdit.AddField((IField)osmrelationIDField);

                // add the field for the tag cloud for all other tag/value pairs
                IFieldEdit osmXmlTagsField = new FieldClass() as IFieldEdit;
                osmXmlTagsField.Name_2 = "osmTags";
                //if (((IWorkspace)workspace).Type == esriWorkspaceType.esriLocalDatabaseWorkspace)
                //{
                    osmXmlTagsField.Type_2 = esriFieldType.esriFieldTypeBlob;
                //}
                //else
                //{
                //    osmXmlTagsField.Type_2 = esriFieldType.esriFieldTypeXML;
                //}
                fieldsEdit.AddField((IField)osmXmlTagsField);

                IFieldEdit osmSupportingElementField = new FieldClass() as IFieldEdit;
                osmSupportingElementField.Name_2 = "osmSupportingElement";
                osmSupportingElementField.Type_2 = esriFieldType.esriFieldTypeString;
                osmSupportingElementField.Length_2 = 5;
                osmSupportingElementField.DefaultValue_2 = "no";
                fieldsEdit.AddField((IField)osmSupportingElementField);

                fields = (ESRI.ArcGIS.Geodatabase.IFields)fieldsEdit; // Explicit Cast
            }

            System.String strShapeField = "";

            // locate the shape field
            for (int j = 0; j < fields.FieldCount; j++)
            {
                if (fields.get_Field(j).Type == ESRI.ArcGIS.Geodatabase.esriFieldType.esriFieldTypeGeometry)
                {
                    strShapeField = fields.get_Field(j).Name;

                    // redefine geometry type

                    IFieldEdit shapeField = fields.get_Field(j) as IFieldEdit;
                    IGeometryDefEdit geometryDef = new GeometryDefClass() as IGeometryDefEdit;
                    geometryDef.GeometryType_2 = esriGeometryType.esriGeometryPolyline;
                    geometryDef.HasZ_2 = false;
                    geometryDef.HasM_2 = false;
                    geometryDef.GridCount_2 = 3;
                    geometryDef.set_GridSize(0, 0);
                    geometryDef.set_GridSize(1, 0);
                    geometryDef.set_GridSize(2, 0);

                    ISpatialReferenceFactory srFactory = new SpatialReferenceEnvironmentClass();
                    ISpatialReference wgs84 = srFactory.CreateGeographicCoordinateSystem((int) esriSRGeoCSType.esriSRGeoCS_WGS1984);
                    geometryDef.SpatialReference_2 = wgs84;

                    shapeField.GeometryDef_2 = (IGeometryDef)geometryDef;

                    break;
                }
            }

            // Use IFieldChecker to create a validated fields collection.
            ESRI.ArcGIS.Geodatabase.IFieldChecker fieldChecker = new ESRI.ArcGIS.Geodatabase.FieldCheckerClass();
            ESRI.ArcGIS.Geodatabase.IEnumFieldError enumFieldError = null;
            ESRI.ArcGIS.Geodatabase.IFields validatedFields = null;
            fieldChecker.ValidateWorkspace = (ESRI.ArcGIS.Geodatabase.IWorkspace)workspace;
            fieldChecker.Validate(fields, out enumFieldError, out validatedFields);

            // The enumFieldError enumerator can be inspected at this point to determine 
            // which fields were modified during validation.


            // finally create and return the feature class
            featureClass = featureWorkspace.CreateFeatureClass(featureClassName, validatedFields, CLSID, null, ESRI.ArcGIS.Geodatabase.esriFeatureType.esriFTSimple, strShapeField, "");
            
            return featureClass;
        }

        public ESRI.ArcGIS.Geodatabase.IFeatureClass CreatePolygonFeatureClass(System.String featureClassName, ESRI.ArcGIS.Geodatabase.IFields fields, ESRI.ArcGIS.esriSystem.UID CLSID)
        {
            if (featureClassName == "") return null; // name was not passed in 

            IWorkspaceFactory workspaceFactory = new InMemoryWorkspaceFactoryClass();
            // Create a new in-memory workspace. This returns a name object.
            IWorkspaceName workspaceName = workspaceFactory.Create(null, "OSMPolygonWorkspace", null, 0);
            IName name = (IName)workspaceName;

            // Open the workspace through the name object.
            IWorkspace workspace = (IWorkspace)name.Open();

            ESRI.ArcGIS.Geodatabase.IFeatureClass featureClass = null;
            ESRI.ArcGIS.Geodatabase.IFeatureWorkspace featureWorkspace = (ESRI.ArcGIS.Geodatabase.IFeatureWorkspace)workspace; // Explicit Cast

            // assign the class id value if not assigned
            if (CLSID == null)
            {
                CLSID = new ESRI.ArcGIS.esriSystem.UIDClass();
                CLSID.Value = "esriGeoDatabase.Feature";
            }

            ESRI.ArcGIS.Geodatabase.IObjectClassDescription objectClassDescription = new ESRI.ArcGIS.Geodatabase.FeatureClassDescriptionClass();

            // if a fields collection is not passed in then supply our own
            if (fields == null)
            {
                // create the fields using the required fields method
                fields = objectClassDescription.RequiredFields;
                ESRI.ArcGIS.Geodatabase.IFieldsEdit fieldsEdit = (ESRI.ArcGIS.Geodatabase.IFieldsEdit)fields; // Explicit Cast

                // add the OSM ID field
                IFieldEdit osmIDField = new FieldClass() as IFieldEdit;
                osmIDField.Name_2 = "OSMID";
                osmIDField.Type_2 = esriFieldType.esriFieldTypeString;
                osmIDField.Length_2 = 20;
                fieldsEdit.AddField((IField)osmIDField);

                // random text field to store additional tags
                IFieldEdit tempTextField = new FieldClass() as IFieldEdit;
                tempTextField.Name_2 = "TagContainer";
                tempTextField.Type_2 = esriFieldType.esriFieldTypeString;
                tempTextField.Length_2 = 255;
                fieldsEdit.AddField((IField)tempTextField);

                // add the field for the tag cloud for all other tag/value pairs
                IFieldEdit osmXmlTagsField = new FieldClass() as IFieldEdit;
                osmXmlTagsField.Name_2 = "osmTags";
                    osmXmlTagsField.Type_2 = esriFieldType.esriFieldTypeBlob;
                fieldsEdit.AddField((IField)osmXmlTagsField);

                // user, uid, visible, version, changeset, timestamp
                IFieldEdit osmuserField = new FieldClass() as IFieldEdit;
                osmuserField.Name_2 = "osmuser";
                osmuserField.Type_2 = esriFieldType.esriFieldTypeString;
                osmuserField.Length_2 = 100;
                fieldsEdit.AddField((IField)osmuserField);

                IFieldEdit osmuidField = new FieldClass() as IFieldEdit;
                osmuidField.Name_2 = "osmuid";
                osmuidField.Type_2 = esriFieldType.esriFieldTypeInteger;
                fieldsEdit.AddField((IField)osmuidField);

                IFieldEdit osmvisibleField = new FieldClass() as IFieldEdit;
                osmvisibleField.Name_2 = "osmvisible";
                osmvisibleField.Type_2 = esriFieldType.esriFieldTypeString;
                osmvisibleField.Length_2 = 20;
                fieldsEdit.AddField((IField)osmvisibleField);

                IFieldEdit osmversionField = new FieldClass() as IFieldEdit;
                osmversionField.Name_2 = "osmversion";
                osmversionField.Type_2 = esriFieldType.esriFieldTypeSmallInteger;
                fieldsEdit.AddField((IField)osmversionField);

                IFieldEdit osmchangesetField = new FieldClass() as IFieldEdit;
                osmchangesetField.Name_2 = "osmchangeset";
                osmchangesetField.Type_2 = esriFieldType.esriFieldTypeInteger;
                fieldsEdit.AddField((IField)osmchangesetField);

                IFieldEdit osmtimestampField = new FieldClass() as IFieldEdit;
                osmtimestampField.Name_2 = "osmtimestamp";
                osmtimestampField.Type_2 = esriFieldType.esriFieldTypeDate;
                fieldsEdit.AddField((IField)osmtimestampField);

                IFieldEdit osmrelationIDField = new FieldClass() as IFieldEdit;
                osmrelationIDField.Name_2 = "osmMemberOf";
                    osmrelationIDField.Type_2 = esriFieldType.esriFieldTypeBlob;
                fieldsEdit.AddField((IField)osmrelationIDField);

                IFieldEdit osmSupportingElementField = new FieldClass() as IFieldEdit;
                osmSupportingElementField.Name_2 = "osmSupportingElement";
                osmSupportingElementField.Type_2 = esriFieldType.esriFieldTypeString;
                osmSupportingElementField.Length_2 = 5;
                osmSupportingElementField.DefaultValue_2 = "no";
                fieldsEdit.AddField((IField)osmSupportingElementField);


                fields = (ESRI.ArcGIS.Geodatabase.IFields)fieldsEdit; // Explicit Cast
            }

            System.String strShapeField = "";

            // locate the shape field
            for (int j = 0; j < fields.FieldCount; j++)
            {
                if (fields.get_Field(j).Type == ESRI.ArcGIS.Geodatabase.esriFieldType.esriFieldTypeGeometry)
                {
                    strShapeField = fields.get_Field(j).Name;

                    // redefine geometry type

                    IFieldEdit shapeField = fields.get_Field(j) as IFieldEdit;
                    IGeometryDefEdit geometryDef = new GeometryDefClass() as IGeometryDefEdit;
                    geometryDef.GeometryType_2 = esriGeometryType.esriGeometryPolygon;
                    geometryDef.HasZ_2 = false;
                    geometryDef.HasM_2 = false;
                    geometryDef.GridCount_2 = 1;
                    geometryDef.set_GridSize(0, 0);

                    ISpatialReferenceFactory srFactory = new SpatialReferenceEnvironmentClass();
                    ISpatialReference wgs84 = srFactory.CreateGeographicCoordinateSystem((int)esriSRGeoCSType.esriSRGeoCS_WGS1984);
                    geometryDef.SpatialReference_2 = wgs84;

                    shapeField.GeometryDef_2 = (IGeometryDef)geometryDef;

                    break;
                }
            }

            // Use IFieldChecker to create a validated fields collection.
            ESRI.ArcGIS.Geodatabase.IFieldChecker fieldChecker = new ESRI.ArcGIS.Geodatabase.FieldCheckerClass();
            ESRI.ArcGIS.Geodatabase.IEnumFieldError enumFieldError = null;
            ESRI.ArcGIS.Geodatabase.IFields validatedFields = null;
            fieldChecker.ValidateWorkspace = (ESRI.ArcGIS.Geodatabase.IWorkspace)workspace;
            fieldChecker.Validate(fields, out enumFieldError, out validatedFields);

            // The enumFieldError enumerator can be inspected at this point to determine 
            // which fields were modified during validation.


            // finally create and return the feature class
            featureClass = featureWorkspace.CreateFeatureClass(featureClassName, validatedFields, CLSID, null, ESRI.ArcGIS.Geodatabase.esriFeatureType.esriFTSimple, strShapeField, "");
            
            return featureClass;
        }

        private ContextMenuStrip CreateErrorContextMenu(int errorCode, string changeAction)
        {
            ContextMenuStrip errorContextMenu = new ContextMenuStrip();
            errorContextMenu.AutoSize = true;
            errorContextMenu.ShowImageMargin = false;
            errorContextMenu.ShowCheckMargin = true;
            errorContextMenu.ItemClicked += new ToolStripItemClickedEventHandler(errorContextMenu_ItemClicked);

            ToolStripSeparator seperatorItem = new ToolStripSeparator();

            
            switch (errorCode)
            {
                case 400:
                    break;
                case 404:
                    ToolStripItem error404deletelocal = new ToolStripMenuItem(resourceManager.GetString("OSMEditor_ConflictEditor_error404_deletelocal"));
                    error404deletelocal.AutoSize = true;
                    error404deletelocal.Tag = conflictTag.osmConflictResolution.osmDeleteLocalFeature;
                    errorContextMenu.Items.Add(error404deletelocal);
                    break;
                case 409:
                    ToolStripItem error409_keepLocal = new ToolStripMenuItem(resourceManager.GetString("OSMEditor_ConflictEditor_error409_keepLocalGeometryAttributes"));
                    error409_keepLocal.AutoSize = true;
                    error409_keepLocal.Tag = conflictTag.osmConflictResolution.osmUpdateLocalVersionNumber;
                    errorContextMenu.Items.Add(error409_keepLocal);

                    ToolStripItem error409_keepLocalGeometryServerAttributes = new ToolStripMenuItem(resourceManager.GetString("OSMEditor_ConflictEditor_error409_keepLocalGeometryServerAttributes"));
                    error409_keepLocalGeometryServerAttributes.AutoSize = true;
                    error409_keepLocalGeometryServerAttributes.Tag = conflictTag.osmConflictResolution.osmUpdateLocalGeometryServerAttributes;
                    errorContextMenu.Items.Add(error409_keepLocalGeometryServerAttributes);

                    ToolStripItem error409_keepLocalAttributesServerGeometry = new ToolStripMenuItem(resourceManager.GetString("OSMEditor_ConflictEditor_error409_keepLocalAttributesServerGeometry"));
                    error409_keepLocalAttributesServerGeometry.AutoSize = true;
                    error409_keepLocalAttributesServerGeometry.Tag = conflictTag.osmConflictResolution.osmUpdateServerGeometryLocalAttributes;
                    errorContextMenu.Items.Add(error409_keepLocalAttributesServerGeometry);

                    ToolStripItem error409_keepServerGeometryServerAttributes = new ToolStripMenuItem(resourceManager.GetString("OSMEditor_ConflictEditor_error409_keepServerGeometryServerAttributes"));
                    error409_keepServerGeometryServerAttributes.AutoSize = true;
                    error409_keepServerGeometryServerAttributes.Tag = conflictTag.osmConflictResolution.osmUpdateServerGeometryServerAttributes;
                    errorContextMenu.Items.Add(error409_keepServerGeometryServerAttributes);

                    break;
                case 412:
                    if (changeAction == "delete")
                    {
                    }
                    break;
                case 410:
                    ToolStripItem error410 = new ToolStripMenuItem(resourceManager.GetString("OSMEditor_ConflictEditor_error410_deletelocal"));
                    error410.AutoSize = true;
                    error410.Tag = conflictTag.osmConflictResolution.osmDeleteLocalFeature;
                    errorContextMenu.Items.Add(error410);
                    break;
                default:
                    ToolStripItem errorUnkown = new ToolStripMenuItem(resourceManager.GetString("OSMEditor_ConflictEditor_error_UnknownSolution"));
                    errorUnkown.AutoSize = true;
                    errorUnkown.Tag = conflictTag.osmConflictResolution.osmNoResolution;
                    errorContextMenu.Items.Add(errorUnkown);

                    break;
            }

            if (errorContextMenu.Items.Count > 0)
            {
                errorContextMenu.Items.Add(new ToolStripSeparator());
            }

            ToolStripItem clearRevisionStatusItem = new ToolStripMenuItem(resourceManager.GetString("OSMEditor_ConflictEditor_error_clearRevisionStatus"));
            clearRevisionStatusItem.AutoSize = true;
            clearRevisionStatusItem.Tag = conflictTag.osmConflictResolution.osmClearRevisionStatus;
            errorContextMenu.Items.Add(clearRevisionStatusItem);

            errorContextMenu.Items.Add(new ToolStripSeparator());

            ToolStripItem deleteRevisionItem = new ToolStripMenuItem(resourceManager.GetString("OSMEditor_ConflictEditor_error_removeRevisionIncident"));
            deleteRevisionItem.AutoSize = true;
            deleteRevisionItem.Tag = conflictTag.osmConflictResolution.osmRemoveRevisionIncident;
            errorContextMenu.Items.Add(deleteRevisionItem);

            return errorContextMenu;
        }

        void errorContextMenu_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            ToolStripMenuItem clickedToolStripMenuItem = e.ClickedItem as ToolStripMenuItem;

            if (clickedToolStripMenuItem == null)
            {
                return;
            }

            if (clickedToolStripMenuItem.Checked == false)
            {
                foreach (ToolStripItem menuItem in clickedToolStripMenuItem.Owner.Items)
                {
                    if (menuItem is ToolStripMenuItem)
                    {
                        if (clickedToolStripMenuItem.Equals(menuItem) == false && menuItem != null)
                        {
                            ((ToolStripMenuItem)menuItem).Checked = false;
                        }
                    }
                }
            }

            clickedToolStripMenuItem.Checked = !clickedToolStripMenuItem.Checked;


            conflictTag.osmConflictResolution resolution = conflictTag.osmConflictResolution.osmNoResolution;

            // update the selected resolution in the node of the reported errors
            if (clickedToolStripMenuItem.Checked == true && m_rightClickTreeNode != null)
            {
                resolution = (conflictTag.osmConflictResolution)clickedToolStripMenuItem.Tag;
            }

            if (m_rightClickTreeNode != null)
            {
                conflictTag currentNodeConflictTag = m_rightClickTreeNode.Tag as conflictTag;

                currentNodeConflictTag.DeterminedResolution = (conflictTag.osmConflictResolution)clickedToolStripMenuItem.Tag;

                errorTreeView.Nodes[m_rightClickTreeNode.Parent.Index].Nodes[m_rightClickTreeNode.Index].Tag = currentNodeConflictTag;

                m_rightClickTreeNode = null;
            }


            UpdateConflictUIBasedOnSelectedResolution(resolution);
        }

        private void UpdateConflictUIBasedOnSelectedResolution(conflictTag.osmConflictResolution selectedConflictResolution)
        {

            switch (selectedConflictResolution)
            {
                case conflictTag.osmConflictResolution.osmNoResolution:
                    UpdateDataGridViewColumn(1, Color.White);
                    UpdateDataGridViewColumn(2, Color.White);
                    DeleteAllMarkerElements();
                    break;
                case conflictTag.osmConflictResolution.osmUpdateLocalVersionNumber:
                    UpdateDataGridViewColumn(2, Color.FromArgb(255, 192, 192));
                    UpdateDataGridViewColumn(1, Color.FromArgb(192, 255, 192));
                    DeleteAllMarkerElements();
                    UpdateMapControlWithMarker((IMapControl4)axMapControl1.Object, "AcceptMarker");
                    UpdateMapControlWithMarker((IMapControl4)axMapControl2.Object, "DeleteMarker");
                    break;
                case conflictTag.osmConflictResolution.osmUpdateLocalGeometryServerAttributes:
                    UpdateDataGridViewColumn(1, Color.FromArgb(255, 192, 192));
                    UpdateDataGridViewColumn(2, Color.FromArgb(192, 255, 192));
                    DeleteAllMarkerElements();
                    UpdateMapControlWithMarker((IMapControl4)axMapControl1.Object, "AcceptMarker");
                    UpdateMapControlWithMarker((IMapControl4)axMapControl2.Object, "DeleteMarker");
                    break;
                case conflictTag.osmConflictResolution.osmUpdateServerGeometryLocalAttributes:
                    UpdateDataGridViewColumn(2, Color.FromArgb(255, 192, 192));
                    UpdateDataGridViewColumn(1, Color.FromArgb(192, 255, 192));
                    DeleteAllMarkerElements();
                    UpdateMapControlWithMarker((IMapControl4)axMapControl2.Object, "AcceptMarker");
                    UpdateMapControlWithMarker((IMapControl4)axMapControl1.Object, "DeleteMarker");
                    break;
                case conflictTag.osmConflictResolution.osmUpdateServerGeometryServerAttributes:
                    UpdateDataGridViewColumn(1, Color.FromArgb(255, 192, 192));
                    UpdateDataGridViewColumn(2, Color.FromArgb(192, 255, 192));
                    DeleteAllMarkerElements();
                    UpdateMapControlWithMarker((IMapControl4)axMapControl2.Object, "AcceptMarker");
                    UpdateMapControlWithMarker((IMapControl4)axMapControl1.Object, "DeleteMarker");
                    break;
                case conflictTag.osmConflictResolution.osmDeleteLocalFeature:
                    UpdateDataGridViewColumn(2, Color.White);
                    UpdateDataGridViewColumn(1, Color.FromArgb(255, 192, 192));
                    DeleteAllMarkerElements();
                    UpdateMapControlWithMarker((IMapControl4)axMapControl1.Object, "DeleteMarker");
                    break;
                case conflictTag.osmConflictResolution.osmChangeUpdateToCreate:
                    UpdateDataGridViewColumn(1, Color.White);
                    UpdateDataGridViewColumn(2, Color.White);
                    DeleteAllMarkerElements();
                    break;
                case conflictTag.osmConflictResolution.osmClearRevisionStatus:
                    UpdateDataGridViewColumn(1, Color.White);
                    UpdateDataGridViewColumn(2, Color.White);
                    DeleteAllMarkerElements();
                    break;
                case conflictTag.osmConflictResolution.osmRemoveRevisionIncident:
                    UpdateDataGridViewColumn(1, Color.White);
                    UpdateDataGridViewColumn(2, Color.White);
                    DeleteAllMarkerElements();
                    break;
                default:
                    break;
            }
        }

        private void DeleteAllMarkerElements()
        {
            IGraphicsContainer graphicsContainer = ((IMapControl4)axMapControl1.Object).Map.BasicGraphicsLayer as IGraphicsContainer;

            graphicsContainer.Reset();
            IElement currentElement = graphicsContainer.Next();

            if (currentElement != null)
            {
                if (((IElementProperties)currentElement).Name.Contains("Marker"))
                {
                    graphicsContainer.DeleteElement(currentElement);
                }

                currentElement = graphicsContainer.Next();
            }

            ((IMapControl4)axMapControl1.Object).Refresh(esriViewDrawPhase.esriViewGraphics);

            graphicsContainer = ((IMapControl4)axMapControl2.Object).Map.BasicGraphicsLayer as IGraphicsContainer;

            graphicsContainer.Reset();
            currentElement = graphicsContainer.Next();

            if (currentElement != null)
            {
                if (((IElementProperties)currentElement).Name.Contains("Marker"))
                {
                    graphicsContainer.DeleteElement(currentElement);
                }

                currentElement = graphicsContainer.Next();
            }

            ((IMapControl4)axMapControl2.Object).Refresh(esriViewDrawPhase.esriViewGraphics);
        }

        private void UpdateDataGridViewColumn(int columnIndex, Color newBackgroundColor)
        {
            if (columnIndex < 0 || columnIndex > dataGridView2.Columns.Count - 1)
            {
                throw new ArgumentOutOfRangeException("columnIndex");
            }

            dataGridView2.Columns[columnIndex].DefaultCellStyle.BackColor = newBackgroundColor;

            dataGridView2.InvalidateColumn(columnIndex);
        }

        private void UpdateDataGridViewRow(int rowIndex, Color newBackgroundColor)
        {
            if (rowIndex < 0 || rowIndex > dataGridView2.Rows.Count - 1)
            {
                throw new ArgumentOutOfRangeException("rowIndex");
            }

            dataGridView2.Rows[rowIndex].DefaultCellStyle.BackColor = newBackgroundColor;

            dataGridView2.InvalidateRow(rowIndex);
        }


        private void UpdateMapControlWithMarker(IMapControl4 updateMapControl, string action)
        {
            IRgbColor redRgbColor = new RgbColorClass();
            redRgbColor.Red = 255;
            redRgbColor.Green = 0;
            redRgbColor.Blue = 0;

            IRgbColor yellowRgbColor = new RgbColorClass();
            yellowRgbColor.Red = 255;
            yellowRgbColor.Green = 255;
            yellowRgbColor.Blue = 115;

            IRgbColor blackRgbcolor = new RgbColorClass();
            blackRgbcolor.Red = 0;
            blackRgbcolor.Green = 0;
            blackRgbcolor.Blue = 0;

            IRgbColor greenRgbColor = new RgbColorClass();
            greenRgbColor.Red = 0;
            greenRgbColor.Green = 255;
            greenRgbColor.Blue = 0;

            IEnvelope extentEnvelope = updateMapControl.Extent;
            IGraphicsContainer graphicsContainer = updateMapControl.Map.BasicGraphicsLayer as IGraphicsContainer;

            if (action == "DeleteMarker")
            {
                DeleteElement(updateMapControl, action);
            }
            else if (action == "AcceptMarker")
            {
                DeleteElement(updateMapControl, action);
            }

            IMultiLayerMarkerSymbol multiLayerMarkerSymbol = new MultiLayerMarkerSymbolClass();

            stdole.IFontDisp esriDefaultMarkerFont = new stdole.StdFontClass() as stdole.IFontDisp;
            esriDefaultMarkerFont.Name = "ESRI Default Marker";
            esriDefaultMarkerFont.Size = Convert.ToDecimal(48);

            IPoint markerLocation = new PointClass();
            markerLocation.SpatialReference = updateMapControl.SpatialReference;

            markerLocation.X = extentEnvelope.LowerLeft.X + extentEnvelope.Width / 10;
            markerLocation.Y = extentEnvelope.LowerLeft.Y + extentEnvelope.Height / (10 * (extentEnvelope.Height / extentEnvelope.Width));

            IMarkerElement markerElement = new MarkerElementClass();
            ((IElement)markerElement).Geometry = markerLocation;

            ISimpleMarkerSymbol backgroundMarkerSymbol = new SimpleMarkerSymbolClass();
            backgroundMarkerSymbol.Color = (IColor)yellowRgbColor;
            backgroundMarkerSymbol.Size = 45;
            backgroundMarkerSymbol.Style = esriSimpleMarkerStyle.esriSMSSquare;
            backgroundMarkerSymbol.Outline = true;
            backgroundMarkerSymbol.OutlineColor = blackRgbcolor;
            backgroundMarkerSymbol.OutlineSize = 2;

            multiLayerMarkerSymbol.AddLayer((IMarkerSymbol)backgroundMarkerSymbol);

            if (action == "DeleteMarker")
            {
                ICharacterMarkerSymbol deleteMarkerSymbol = new CharacterMarkerSymbolClass();
                deleteMarkerSymbol.Font = esriDefaultMarkerFont;
                deleteMarkerSymbol.CharacterIndex = 68;
                deleteMarkerSymbol.Color = redRgbColor;
                deleteMarkerSymbol.Size = 48;

                multiLayerMarkerSymbol.AddLayer(deleteMarkerSymbol);

                ((IElementProperties)markerElement).Name = "DeleteMarker";

            }
            else if (action == "AcceptMarker")
            {
                ICharacterMarkerSymbol acceptMarkerSymbol = new CharacterMarkerSymbolClass();

                acceptMarkerSymbol.Font = esriDefaultMarkerFont;
                acceptMarkerSymbol.CharacterIndex = 105;
                acceptMarkerSymbol.Color = greenRgbColor;
                acceptMarkerSymbol.Size = 48;

                multiLayerMarkerSymbol.AddLayer(acceptMarkerSymbol);

                ((IElementProperties)markerElement).Name = "AcceptMarker";
            }

            markerElement.Symbol = (IMarkerSymbol)multiLayerMarkerSymbol;

            graphicsContainer.AddElement((IElement)markerElement, 99);

            updateMapControl.Refresh(esriViewDrawPhase.esriViewGraphics, null, null);
        }


        private void DeleteElement(IMapControl4 updateMapControl, string action)
        {
            IGraphicsContainer graphicsContainer = updateMapControl.Map.BasicGraphicsLayer as IGraphicsContainer;

            graphicsContainer.Reset();
            IElement currentElement = graphicsContainer.Next();

            while (currentElement != null)
            {
                if (((IElementProperties)currentElement).Name.Equals(action))
                {
                    graphicsContainer.DeleteElement(currentElement);
                }

                currentElement = graphicsContainer.Next();
            }
        } 

        private void treeView1_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            TreeNode clickedTreeNode = e.Node;

            // left mouse click to populate grid and maps
            if (e.Button == System.Windows.Forms.MouseButtons.Left)
            {
                IRow localOSMRow = null;
                IRow serverOSMRow = null;

                ResolveNodeTag(clickedTreeNode, out localOSMRow, out serverOSMRow);

                if (localOSMRow == null || serverOSMRow == null)
                {
                    return;
                }

                FillDataGridView(localOSMRow, serverOSMRow);

                IFeature localOSMFeature = localOSMRow as IFeature;
                IFeature serverOSMFeature = serverOSMRow as IFeature;

                PaintFeaturesToMapControl(localOSMFeature, serverOSMFeature);
            }
            // right mouse click to show context menu and solutions
            else if (e.Button == System.Windows.Forms.MouseButtons.Right)
            {
                m_rightClickTreeNode = e.Node;
                errorTreeView.SelectedNode = e.Node;
            }
        }

        private void PaintFeaturesToMapControl(IFeature localOSMFeature, IFeature serverOSMFeature)
        {
            if (localOSMFeature != null)
            {
                IRgbColor greenColor = new RgbColorClass();
                greenColor.Green = 255;
                greenColor.Red = 0;
                greenColor.Blue = 0;

                IMapControl4 localMapControl = axMapControl1.Object as IMapControl4;

                IGeometry localGeometry = localOSMFeature.Shape;
                localGeometry.Project(localMapControl.SpatialReference);

                IElement localOSMElement = OSMGeometryToGraphicElement(localGeometry, greenColor);

                IGraphicsContainer basicGraphicsContainer = axMapControl1.Map.BasicGraphicsLayer as IGraphicsContainer;

                basicGraphicsContainer.DeleteAllElements();

                basicGraphicsContainer.AddElement(localOSMElement, 0);

                if (localOSMFeature.Shape.GeometryType == esriGeometryType.esriGeometryPoint)
                {
                    localMapControl.CenterAt((IPoint)localGeometry);
                    localMapControl.MapScale = 500;
                }
                else
                {
                    IEnvelope featureEnvelope = localOSMFeature.Shape.Envelope;
                    featureEnvelope.Project(localMapControl.SpatialReference);

                    featureEnvelope.Expand(1.1, 1.1, true);

                    localMapControl.Extent = featureEnvelope;
                }

                localMapControl.Refresh(esriViewDrawPhase.esriViewGraphics, null, null);

            }

            if (serverOSMFeature != null)
            {
                IRgbColor redColor = new RgbColorClass();
                redColor.Green = 0;
                redColor.Red = 255;
                redColor.Blue = 0;

                IMapControl4 serverMapControl = axMapControl2.Object as IMapControl4;

                IGeometry serverGeometry = serverOSMFeature.Shape as IGeometry;
                serverGeometry.Project(serverMapControl.SpatialReference);

                IElement serverOSMElement = OSMGeometryToGraphicElement(serverGeometry, redColor);

                IGraphicsContainer basicGraphicsContainer = axMapControl2.Map.BasicGraphicsLayer as IGraphicsContainer;

                basicGraphicsContainer.DeleteAllElements();

                basicGraphicsContainer.AddElement(serverOSMElement, 0);

                if (serverOSMFeature.Shape.GeometryType == esriGeometryType.esriGeometryPoint)
                {
                    serverMapControl.CenterAt((IPoint)serverGeometry);
                    serverMapControl.MapScale = 500;
                }
                else
                {
                    IEnvelope featureEnvelope = serverOSMFeature.Shape.Envelope;
                    featureEnvelope.Project(serverMapControl.SpatialReference);

                    featureEnvelope.Expand(1.1, 1.1, true);

                    serverMapControl.Extent = featureEnvelope;
                }

                serverMapControl.Refresh(esriViewDrawPhase.esriViewGraphics, null, null);
            }
        }

        private IElement OSMGeometryToGraphicElement(IGeometry featureGeometry, IColor featureColor)
        {
            IElement graphicElement = null;

            switch (featureGeometry.GeometryType)
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
                    graphicElement = new MarkerElementClass() as IElement;

                    graphicElement.Geometry = featureGeometry;


                    IMarkerSymbol markerSymbol = null;
                    if (m_documentDefaultSymbols == null)
                    {
                        // create a symbology for a point geometry
                        ISimpleMarkerSymbol simpleMarkerSymbol = new SimpleMarkerSymbolClass();
                        simpleMarkerSymbol.Color = featureColor;
                        simpleMarkerSymbol.Style = esriSimpleMarkerStyle.esriSMSCircle;
                        simpleMarkerSymbol.Size = 18;

                        markerSymbol = simpleMarkerSymbol;
                    }
                    else
                    {
                        markerSymbol = m_documentDefaultSymbols.MarkerSymbol;
                        markerSymbol.Color = featureColor;
                    }

                    ((IMarkerElement)graphicElement).Symbol = markerSymbol;

                    break;
                case esriGeometryType.esriGeometryPolygon:
                    graphicElement = new PolygonElementClass() as IElement;

                    graphicElement.Geometry = featureGeometry;

                    // 
                    IFillSymbol fillSymbol = null;
                    if (m_documentDefaultSymbols == null)
                    {
                        ISimpleFillSymbol simpleFillSymbol = new SimpleFillSymbolClass();
                        simpleFillSymbol.Color = featureColor;

                        ILineSymbol simpleLineSymbol = new SimpleLineSymbolClass();
                        simpleLineSymbol.Color = featureColor;
                        simpleLineSymbol.Width = 1;
                        simpleFillSymbol.Outline = simpleLineSymbol;
                        simpleFillSymbol.Style = esriSimpleFillStyle.esriSFSSolid;

                        fillSymbol = simpleFillSymbol;
                    }
                    else
                    {
                        fillSymbol = m_documentDefaultSymbols.FillSymbol;
                        fillSymbol.Color = featureColor;

                    }
                    ((IFillShapeElement)graphicElement).Symbol = fillSymbol;

                    break;
                case esriGeometryType.esriGeometryPolyline:
                    graphicElement = new LineElementClass() as IElement;

                    graphicElement.Geometry = featureGeometry;

                    ILineSymbol lineSymbol = null;
                    if (m_documentDefaultSymbols == null)
                    {
                        lineSymbol = new SimpleLineSymbolClass();
                        lineSymbol.Color = featureColor;
                        lineSymbol.Width = 3;
                    }
                    else
                    {
                        lineSymbol = m_documentDefaultSymbols.LineSymbol;
                        lineSymbol.Color = featureColor;
                    }

                    ((ILineElement)graphicElement).Symbol = lineSymbol;

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

            return graphicElement;
        }

        private void ResolveNodeTag(TreeNode clickedTreeNode, out IRow localOSMFeature, out IRow serverOSMFeature)
        {
            conflictTag nodeConflictTag = clickedTreeNode.Tag as conflictTag;

            if (nodeConflictTag == null)
            {
                localOSMFeature = null;
                serverOSMFeature = null;
                return;
            }

            ITable currentRevisionTable = m_allRevisionTables[clickedTreeNode.Parent.Text];

            IFeatureWorkspace featureWorkspace = ((IDataset)currentRevisionTable).Workspace as IFeatureWorkspace;
            IFeatureClass localFeatureClass = featureWorkspace.OpenFeatureClass(nodeConflictTag.SourceFeatureClassName);

            IQueryFilter localQueryFilter = new QueryFilterClass();
            localQueryFilter.WhereClause = localFeatureClass.WhereClauseByExtensionVersion(nodeConflictTag.OSMID, "OSMID",localFeatureClass.OSMExtensionVersion());

            using (ESRI.ArcGIS.OSM.OSMClassExtension.ComReleaser comReleaser = new ESRI.ArcGIS.OSM.OSMClassExtension.ComReleaser())
            {

                IFeatureCursor localSearchCursor = localFeatureClass.Search(localQueryFilter, false);
                comReleaser.ManageLifetime(localSearchCursor);

                IFeature localFeature = localSearchCursor.NextFeature();
                localOSMFeature = localFeature as IRow;

            }

            IQueryFilter serverQueryFilter = new QueryFilterClass();
            serverQueryFilter.WhereClause = m_osmPointsFC.WhereClauseByExtensionVersion(nodeConflictTag.OSMID, "OSMID", m_osmPointsFC.OSMExtensionVersion());

            IFeatureCursor serverSearchCursor = null;
            IFeature serverFeature = null;

            using (ESRI.ArcGIS.OSM.OSMClassExtension.ComReleaser comReleaser = new ESRI.ArcGIS.OSM.OSMClassExtension.ComReleaser())
            {
                if (nodeConflictTag.SourceFeatureClassName.Contains("_osm_pt"))
                {
                    serverSearchCursor = m_osmPointsFC.Search(serverQueryFilter, false);
                    comReleaser.ManageLifetime(serverSearchCursor);
                }
                else if (nodeConflictTag.SourceFeatureClassName.Contains("_osm_ln"))
                {
                    serverSearchCursor = m_osmLinesFC.Search(serverQueryFilter, false);
                    comReleaser.ManageLifetime(serverSearchCursor);
                }
                else if (nodeConflictTag.SourceFeatureClassName.Contains("_osm_ply"))
                {
                    serverSearchCursor = m_osmPolygonFC.Search(serverQueryFilter, false);
                    comReleaser.ManageLifetime(serverSearchCursor);
                }
                else if (nodeConflictTag.SourceFeatureClassName.Contains("_osm_relation"))
                {
                }

                if (serverSearchCursor != null)
                {
                    serverFeature = serverSearchCursor.NextFeature();
                }
            }

            serverOSMFeature = serverFeature as IRow;
        }

        private void ClearRevisionErrors(IRow currentRow, int statusIndex, int statusCodeIndex, int errorMessageIndex)
        {
            if (currentRow == null)
            {
                return;
            }

            if (statusIndex != -1)
            {
                currentRow.set_Value(statusIndex, System.DBNull.Value);
            }
            if (statusCodeIndex != -1)
            {
                currentRow.set_Value(statusCodeIndex, System.DBNull.Value);
            }
            if (errorMessageIndex != -1)
            {
                currentRow.set_Value(errorMessageIndex, System.DBNull.Value);
            }
        }

        private void OSMConflictEditorUI_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (errorTreeView == null)
            {
                return;
            }

            if (errorTreeView.Nodes == null)
            {
                return;
            }

            foreach (TreeNode revisionErrorNode in errorTreeView.Nodes)
            {
                ITable revisionTable = m_allRevisionTables[revisionErrorNode.Text];

                int revisionOSMActionFieldIndex = revisionTable.Fields.FindField("osmaction");
                int revisionOSMElementTypeFieldIndex = revisionTable.Fields.FindField("osmelementtype");
                int revisionOSMSourceFCNameFieldIndex = revisionTable.Fields.FindField("sourcefcname");
                int revisionOSMStatusFieldIndex = revisionTable.Fields.FindField("osmstatus");
                int revisionOSMStatusCodeFieldIndex = revisionTable.Fields.FindField("osmstatuscode");
                int revisionOSMErrorMessageFieldIndex = revisionTable.Fields.FindField("osmerrormessage");
                int revisionOSMOldIDFieldIndex = revisionTable.Fields.FindField("osmoldid");
                int revisionOSMVersionFieldIndex = revisionTable.Fields.FindField("osmversion");

                int localVersionFieldIndex = -1;
                int serverVersionFieldIndex = -1;

                int localChangesetFieldIndex = -1;
                int serverChangesetFieldIndex = -1;

                int localOSMTagsFieldIndex = -1;
                int serverOSMTagsFieldIndex = -1;


                IRow localRow = null;
                IRow serverRow = null;
                IRow updateRow = null;

                IFeature localFeature = null;
                IFeature serverFeature = null;

                foreach (TreeNode errorNode in revisionErrorNode.Nodes)
                {
                    conflictTag errorNodeConflictTag = errorNode.Tag as conflictTag;

                    switch (errorNodeConflictTag.DeterminedResolution)
                    {
                        case conflictTag.osmConflictResolution.osmNoResolution:
                            break;
                        case conflictTag.osmConflictResolution.osmUpdateLocalVersionNumber:
                            try
                            {
                                ResolveNodeTag(errorNode, out localRow, out serverRow);

                                if (serverRow == null || localRow == null)
                                {
                                    continue;
                                }

                                if (m_editor != null)
                                {
                                    m_editor.StartOperation();
                                }

                                localVersionFieldIndex = localRow.Fields.FindField("osmversion");
                                serverVersionFieldIndex = serverRow.Fields.FindField("osmversion");

                                localChangesetFieldIndex = localRow.Fields.FindField("osmchangeset");
                                serverChangesetFieldIndex = serverRow.Fields.FindField("osmchangeset");

                                if (localVersionFieldIndex != -1 && serverVersionFieldIndex != -1)
                                {
                                    localRow.set_Value(localVersionFieldIndex, serverRow.get_Value(serverVersionFieldIndex));
                                }

                                if (localChangesetFieldIndex != -1 && serverChangesetFieldIndex != -1)
                                {
                                    localRow.set_Value(localChangesetFieldIndex, serverRow.get_Value(serverChangesetFieldIndex));
                                }

                                localRow.Store();

                                updateRow = revisionTable.GetRow(errorNodeConflictTag.RevisionOID);
                                ClearRevisionErrors(updateRow, revisionOSMStatusFieldIndex, revisionOSMStatusCodeFieldIndex, revisionOSMErrorMessageFieldIndex);

                                if (revisionOSMVersionFieldIndex != -1 && serverVersionFieldIndex != -1)
                                {
                                    updateRow.set_Value(revisionOSMVersionFieldIndex, serverRow.get_Value(serverVersionFieldIndex));
                                }

                                updateRow.Store();

                                if (m_editor != null)
                                {
                                    if (!String.IsNullOrEmpty(errorNode.ToolTipText))
                                    {
                                        m_editor.StopOperation(errorNode.ToolTipText);
                                    }
                                    else
                                    {
                                        m_editor.StopOperation(resourceManager.GetString("OSMEditor_ConflictEditor_genericConflictOpMessage"));
                                    }
                                }
                            }
                            catch 
                            {
                                if (m_editor != null)
                                {
                                    m_editor.AbortOperation();
                                }
                            }
                            break;
                        case conflictTag.osmConflictResolution.osmUpdateLocalGeometryServerAttributes:
                            try
                            {
                                ResolveNodeTag(errorNode, out localRow, out serverRow);

                                if (serverRow == null || localRow == null)
                                {
                                    continue;
                                }

                                if (m_editor != null)
                                {
                                    m_editor.StartOperation();
                                }

                                localOSMTagsFieldIndex = localRow.Fields.FindField("osmTags");
                                serverOSMTagsFieldIndex = serverRow.Fields.FindField("osmTags");

                                ESRI.ArcGIS.OSM.OSMClassExtension.tag[] serverTags = null;

                                if (serverOSMTagsFieldIndex != -1)
                                {
                                    serverTags = _osmUtility.retrieveOSMTags(serverRow, serverOSMTagsFieldIndex, null);
                                }

                                if (localOSMTagsFieldIndex != -1)
                                {
                                    _osmUtility.insertOSMTags(localOSMTagsFieldIndex, localRow, serverTags, ((IDataset)revisionTable).Workspace);
                                }

                                localVersionFieldIndex = localRow.Fields.FindField("osmversion");
                                serverVersionFieldIndex = serverRow.Fields.FindField("osmversion");

                                localChangesetFieldIndex = localRow.Fields.FindField("osmchangeset");
                                serverChangesetFieldIndex = serverRow.Fields.FindField("osmchangeset");

                                if (localVersionFieldIndex != -1 && serverVersionFieldIndex != -1)
                                {
                                    localRow.set_Value(localVersionFieldIndex, serverRow.get_Value(serverVersionFieldIndex));
                                }

                                if (localChangesetFieldIndex != -1 && serverChangesetFieldIndex != -1)
                                {
                                    localRow.set_Value(localChangesetFieldIndex, serverRow.get_Value(serverChangesetFieldIndex));
                                }

                                localRow.Store();

                                updateRow = revisionTable.GetRow(errorNodeConflictTag.RevisionOID);
                                ClearRevisionErrors(updateRow, revisionOSMStatusFieldIndex, revisionOSMStatusCodeFieldIndex, revisionOSMErrorMessageFieldIndex);

                                if (revisionOSMVersionFieldIndex != -1 && serverVersionFieldIndex != -1)
                                {
                                    updateRow.set_Value(revisionOSMVersionFieldIndex, serverRow.get_Value(serverVersionFieldIndex));
                                }

                                updateRow.Store();

                                if (m_editor != null)
                                {
                                    if (!String.IsNullOrEmpty(errorNode.ToolTipText))
                                    {
                                        m_editor.StopOperation(errorNode.ToolTipText);
                                    }
                                    else
                                    {
                                        m_editor.StopOperation(resourceManager.GetString("OSMEditor_ConflictEditor_genericConflictOpMessage"));
                                    }
                                }
                            }
                            catch 
                            {
                                if (m_editor != null)
                                {
                                    m_editor.AbortOperation();
                                }
                            }
                            break;
                        case conflictTag.osmConflictResolution.osmUpdateServerGeometryLocalAttributes:
                            try
                            {
                                ResolveNodeTag(errorNode, out localRow, out serverRow);

                                if (serverRow == null || localRow == null)
                                {
                                    continue;
                                }

                                if (m_editor != null)
                                {
                                    m_editor.StartOperation();
                                }

                                localFeature = localRow as IFeature;
                                serverFeature = serverRow as IFeature;

                                localFeature.Shape = serverFeature.ShapeCopy;

                                localVersionFieldIndex = localRow.Fields.FindField("osmversion");
                                serverVersionFieldIndex = serverRow.Fields.FindField("osmversion");

                                localChangesetFieldIndex = localRow.Fields.FindField("osmchangeset");
                                serverChangesetFieldIndex = serverRow.Fields.FindField("osmchangeset");

                                if (localVersionFieldIndex != -1 && serverVersionFieldIndex != -1)
                                {
                                    localRow.set_Value(localVersionFieldIndex, serverRow.get_Value(serverVersionFieldIndex));
                                }

                                if (localChangesetFieldIndex != -1 && serverChangesetFieldIndex != -1)
                                {
                                    localRow.set_Value(localChangesetFieldIndex, serverRow.get_Value(serverChangesetFieldIndex));
                                }

                                localRow.Store();

                                updateRow = revisionTable.GetRow(errorNodeConflictTag.RevisionOID);
                                ClearRevisionErrors(updateRow, revisionOSMStatusFieldIndex, revisionOSMStatusCodeFieldIndex, revisionOSMErrorMessageFieldIndex);
                                updateRow.Store();

                                if (m_editor != null)
                                {
                                    if (!String.IsNullOrEmpty(errorNode.ToolTipText))
                                    {
                                        m_editor.StopOperation(errorNode.ToolTipText);
                                    }
                                    else
                                    {
                                        m_editor.StopOperation(resourceManager.GetString("OSMEditor_ConflictEditor_genericConflictOpMessage"));
                                    }
                                }
                            }
                            catch 
                            {
                                if (m_editor != null)
                                {
                                    m_editor.AbortOperation();
                                }
                            }
                            break;
                        case conflictTag.osmConflictResolution.osmUpdateServerGeometryServerAttributes:
                            try
                            {
                                ResolveNodeTag(errorNode, out localRow, out serverRow);

                                if (serverRow == null || localRow == null)
                                {
                                    continue;
                                }

                                if (m_editor != null)
                                {
                                    m_editor.StartOperation();
                                }

                                localOSMTagsFieldIndex = localRow.Fields.FindField("osmTags");
                                serverOSMTagsFieldIndex = serverRow.Fields.FindField("osmTags");

                                ESRI.ArcGIS.OSM.OSMClassExtension.tag[] serverTags = null;

                                if (serverOSMTagsFieldIndex != -1)
                                {
                                    serverTags = _osmUtility.retrieveOSMTags(serverRow, serverOSMTagsFieldIndex, null);
                                }

                                if (localOSMTagsFieldIndex != -1)
                                {
                                    _osmUtility.insertOSMTags(localOSMTagsFieldIndex, localRow, serverTags, ((IDataset)revisionTable).Workspace);
                                }

                                localFeature = localRow as IFeature;
                                serverFeature = serverRow as IFeature;

                                localFeature.Shape = serverFeature.ShapeCopy;

                                localVersionFieldIndex = localRow.Fields.FindField("osmversion");
                                serverVersionFieldIndex = serverRow.Fields.FindField("osmversion");

                                localChangesetFieldIndex = localRow.Fields.FindField("osmchangeset");
                                serverChangesetFieldIndex = serverRow.Fields.FindField("osmchangeset");

                                if (localVersionFieldIndex != -1 && serverVersionFieldIndex != -1)
                                {
                                    localRow.set_Value(localVersionFieldIndex, serverRow.get_Value(serverVersionFieldIndex));
                                }

                                if (localChangesetFieldIndex != -1 && serverChangesetFieldIndex != -1)
                                {
                                    localRow.set_Value(localChangesetFieldIndex, serverRow.get_Value(serverChangesetFieldIndex));
                                }

                                localRow.Store();

                                updateRow = revisionTable.GetRow(errorNodeConflictTag.RevisionOID);
                                ClearRevisionErrors(updateRow, revisionOSMStatusFieldIndex, revisionOSMStatusCodeFieldIndex, revisionOSMErrorMessageFieldIndex);

                                if (revisionOSMVersionFieldIndex != -1 && serverVersionFieldIndex != -1)
                                {
                                    updateRow.set_Value(revisionOSMVersionFieldIndex, serverRow.get_Value(serverVersionFieldIndex));
                                }

                                updateRow.Store();

                                if (m_editor != null)
                                {
                                    if (!String.IsNullOrEmpty(errorNode.ToolTipText))
                                    {
                                        m_editor.StopOperation(errorNode.ToolTipText);
                                    }
                                    else
                                    {
                                        m_editor.StopOperation(resourceManager.GetString("OSMEditor_ConflictEditor_genericConflictOpMessage"));
                                    }
                                }
                            }
                            catch
                            {
                                if (m_editor != null)
                                {
                                    m_editor.AbortOperation();
                                }
                            }

                            break;
                        case conflictTag.osmConflictResolution.osmDeleteLocalFeature:
                            try
                            {
                                if (m_editor != null)
                                {
                                    m_editor.StartOperation();
                                }

                                IFeatureWorkspace featureWorkspace = ((IDataset)revisionTable).Workspace as IFeatureWorkspace;
                                IFeatureClass localFeatureClass = featureWorkspace.OpenFeatureClass(errorNodeConflictTag.SourceFeatureClassName);

                                IQueryFilter localQueryFilter = new QueryFilterClass();
                                localQueryFilter.WhereClause = localFeatureClass.WhereClauseByExtensionVersion(errorNodeConflictTag.OSMID, "OSMID", localFeatureClass.OSMExtensionVersion());

                                using (ESRI.ArcGIS.OSM.OSMClassExtension.ComReleaser comReleaser = new ESRI.ArcGIS.OSM.OSMClassExtension.ComReleaser())
                                {
                                    IFeatureCursor localSearchCursor = localFeatureClass.Search(localQueryFilter, false);
                                    comReleaser.ManageLifetime(localSearchCursor);

                                    localFeature = localSearchCursor.NextFeature();

                                    while (localFeature != null)
                                    {
                                        localFeature.Delete();

                                        localFeature = localSearchCursor.NextFeature();
                                    }
                                }

                                updateRow = revisionTable.GetRow(errorNodeConflictTag.RevisionOID);
                                updateRow.Delete();

                                if (m_editor != null)
                                {
                                    if (!String.IsNullOrEmpty(errorNode.ToolTipText))
                                    {
                                        m_editor.StopOperation(errorNode.ToolTipText);
                                    }
                                    else
                                    {
                                        m_editor.StopOperation(resourceManager.GetString("OSMEditor_ConflictEditor_genericConflictOpMessage"));
                                    }
                                }
                            }
                            catch 
                            {
                                if (m_editor != null)
                                {
                                    m_editor.AbortOperation();
                                }
                            }

                            break;
                        case conflictTag.osmConflictResolution.osmChangeUpdateToCreate:
                            try
                            {
                                if (m_editor != null)
                                {
                                    m_editor.StartOperation();
                                }

                                updateRow = revisionTable.GetRow(errorNodeConflictTag.RevisionOID);

                                ClearRevisionErrors(updateRow, revisionOSMStatusFieldIndex, revisionOSMStatusCodeFieldIndex, revisionOSMErrorMessageFieldIndex);

                                if (revisionOSMActionFieldIndex != -1)
                                {
                                    updateRow.set_Value(revisionOSMActionFieldIndex, "create");
                                }

                                updateRow.Store();

                                if (m_editor != null)
                                {
                                    if (!String.IsNullOrEmpty(errorNode.ToolTipText))
                                    {
                                        m_editor.StopOperation(errorNode.ToolTipText);
                                    }
                                    else
                                    {
                                        m_editor.StopOperation(resourceManager.GetString("OSMEditor_ConflictEditor_genericConflictOpMessage"));
                                    }
                                }
                            }
                            catch
                            {
                                if (m_editor != null)
                                {
                                    m_editor.AbortOperation();
                                }
                            }
                            break;
                        case conflictTag.osmConflictResolution.osmClearRevisionStatus:
                            try
                            {
                                if (m_editor != null)
                                {
                                    m_editor.StartOperation();
                                }

                                updateRow = revisionTable.GetRow(errorNodeConflictTag.RevisionOID);

                                ClearRevisionErrors(updateRow, revisionOSMStatusFieldIndex, revisionOSMStatusCodeFieldIndex, revisionOSMErrorMessageFieldIndex);

                                updateRow.Store();

                                if (m_editor != null)
                                {
                                    if (!String.IsNullOrEmpty(errorNode.ToolTipText))
                                    {
                                        m_editor.StopOperation(errorNode.ToolTipText);
                                    }
                                    else
                                    {
                                        m_editor.StopOperation(resourceManager.GetString("OSMEditor_ConflictEditor_genericConflictOpMessage"));
                                    }
                                }
                            }
                            catch 
                            {
                                if (m_editor != null)
                                {
                                    m_editor.AbortOperation();
                                }
                            }
                            break;
                        case conflictTag.osmConflictResolution.osmRemoveRevisionIncident:
                            try
                            {
                                if (m_editor != null)
                                {
                                    m_editor.StartOperation();
                                }

                                IRow rowToDelete = revisionTable.GetRow(errorNodeConflictTag.RevisionOID);
                                rowToDelete.Delete();

                                if (m_editor != null)
                                {
                                    if (!String.IsNullOrEmpty(errorNode.ToolTipText))
                                    {
                                        m_editor.StopOperation(errorNode.ToolTipText);
                                    }
                                    else
                                    {
                                        m_editor.StopOperation(resourceManager.GetString("OSMEditor_ConflictEditor_genericConflictOpMessage"));
                                    }
                                }
                            }
                            catch 
                            {
                                if (m_editor != null)
                                {
                                    m_editor.AbortOperation();
                                }
                            }
                            break;
                        default:
                            break;
                    }
                }
            }

            foreach (KeyValuePair<string, ITable> table in m_allRevisionTables)
            {
                // explicitly release the tables 
                if (table.Value != null)
                {
                    Marshal.ReleaseComObject(table.Value);
                }
            }

            //finally clear the table itself
            m_allRevisionTables.Clear();

        }
    }

    [ComVisible(false)]
    public class conflictTag
    {
        private string m_osmID = String.Empty;
        private int m_errorCode = -1;
        private string m_sourceFeatureClassName = String.Empty;
        private osmConflictResolution m_determinedConflictResolution = osmConflictResolution.osmNoResolution;
        private int m_revisionOID = -1;

        public enum osmConflictResolution
        {
            osmNoResolution,
            osmUpdateLocalVersionNumber,
            osmUpdateLocalGeometryServerAttributes,
            osmUpdateServerGeometryLocalAttributes,
            osmUpdateServerGeometryServerAttributes,
            osmDeleteLocalFeature,
            osmChangeUpdateToCreate,
            osmClearRevisionStatus,
            osmRemoveRevisionIncident
        }

        public conflictTag()
        {
        }

        public conflictTag(int revisionOID, string OSMID, int ErrorCode, string SourceFeatureClassName)
        {
            m_revisionOID = revisionOID;
            m_osmID = OSMID;
            m_errorCode = ErrorCode;
            m_sourceFeatureClassName = SourceFeatureClassName;
        }
        public conflictTag(int revisionOID, string OSMID, int ErrorCode, string SourceFeatureClassName, osmConflictResolution DeterminedResolution)
        {
            m_revisionOID = revisionOID;
            m_osmID = OSMID;
            m_errorCode = ErrorCode;
            m_sourceFeatureClassName = SourceFeatureClassName;
            m_determinedConflictResolution = DeterminedResolution;
        }


        public int RevisionOID
        {
            get
            {
                return m_revisionOID;
            }
            set
            {
                m_revisionOID = value;
            }
        }

        public string OSMID
        {
            get
            {
                return m_osmID;
            }
            set
            {
                m_osmID = value;
            }
        }

        public int ErrorCode
        {
            get
            {
                return m_errorCode;
            }
            set
            {
                m_errorCode = value;
            }
        }

        public string SourceFeatureClassName
        {
            get
            {
                return m_sourceFeatureClassName;
            }
            set
            {
                m_sourceFeatureClassName = value;
            }
        }

        public osmConflictResolution DeterminedResolution
        {
            get
            {
                return m_determinedConflictResolution;
            }
            set
            {
                m_determinedConflictResolution = value;
            }
        }
    }




}
