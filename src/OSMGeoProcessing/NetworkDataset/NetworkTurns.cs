using System;
using System.Collections.Generic;
using System.Linq;
using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.Geoprocessing;
using ESRI.ArcGIS.OSM.OSMClassExtension;

namespace ESRI.ArcGIS.OSM.GeoProcessing
{
    public class NetworkTurns
    {
        private string _turnClassName;
        private IDataset _osmDataset;
        private INetworkDataset _networkDataset;

        private string _dsPath;

        public IFeatureClass OsmPointFeatureClass { get; set; }
        public List<IFeatureClass> OsmEdgeFeatureClasses { get; set; }
        public int OsmExtVersion { get; set; }
        public RunTaskManager TaskManager { get; set; }
        
        public NetworkTurns(string turnClassName, IDataset osmDataset, INetworkDataset nds)
        {
            _turnClassName = turnClassName;
            _osmDataset = osmDataset;
            _networkDataset = nds;

            IGPUtilities gpUtil = new GPUtilitiesClass();
            IDataElement de = gpUtil.MakeDataElementFromNameObject(osmDataset.FullName);
            _dsPath = de.CatalogPath;
        }

        /// <summary>Adds turn restriction feature class to network dataset</summary>
        public INetworkSource ExtractTurnRestrictions()
        {
            CreateNetworkTurnFeatureClass();
            TaskManager.ExecuteTask("GPTools_OSMGPCreateNetworkDataset_populatingTurns", PopulateTurnsFromRelations);

            // Create a TurnFeatureSource object and point it to the new turns feature class.
            INetworkSource turnNetworkSource = new TurnFeatureSourceClass();
            turnNetworkSource.Name = _turnClassName;
            turnNetworkSource.ElementType = esriNetworkElementType.esriNETTurn;

            return turnNetworkSource;
        }

        private void CreateNetworkTurnFeatureClass()
        {
            IVariantArray paramArray = new VarArrayClass();
            paramArray.Add(_dsPath);
            paramArray.Add(_turnClassName);
            paramArray.Add(2);

            TaskManager.ExecuteTool("CreateTurnFeatureClass_na", paramArray);

            paramArray.RemoveAll();
            paramArray.Add(_dsPath + "\\" + _turnClassName);
            paramArray.Add("RestrictionType");
            paramArray.Add("TEXT");
            paramArray.Add("");
            paramArray.Add("");
            paramArray.Add(50);
            paramArray.Add("");
            paramArray.Add("NULLABLE");

            TaskManager.ExecuteTool("AddField_management", paramArray);
        }

        private void PopulateTurnsFromRelations()
        {
            IFeatureWorkspace fws = (IFeatureWorkspace)_osmDataset.Workspace;

            TurnFeatureClassWrapper fcTurn = new TurnFeatureClassWrapper();
            fcTurn.OpenTurnFeatureClass(fws, _turnClassName);

            // Add NDS Turn features to the turn feature class
            foreach (OSMTurnInfo osmTurn in EnumerateOsmTurnRestrictions(fws))
            {
                TaskManager.CheckCancel();

                if (osmTurn.TurnType.StartsWith("NO", StringComparison.CurrentCultureIgnoreCase))
                    CreateTurnFeature_NO(fcTurn, osmTurn);
                else if (osmTurn.TurnType.StartsWith("ONLY", StringComparison.CurrentCultureIgnoreCase))
                    CreateTurnFeature_ONLY(fcTurn, osmTurn);
            }
        }

        private IEnumerable<OSMTurnInfo> EnumerateOsmTurnRestrictions(IFeatureWorkspace fws)
        {
            OSMUtility osmUtility = new OSMUtility();
            ITable tableRelation = fws.OpenTable(_osmDataset.Name + "_osm_relation");
            try
            {
                TaskManager.StartProgress(tableRelation.RowCount(null));

                using (ComReleaser cr = new ComReleaser())
                {
                    ICursor cursor = tableRelation.Search(null, false);
                    cr.ManageLifetime(cursor);

                    int idxTags = cursor.FindField("osmTags");
                    int idxMembers = cursor.FindField("osmMembers");

                    IRow row = null;
                    while ((row = cursor.NextRow()) != null)
                    {
                        tag[] tags = osmUtility.retrieveOSMTags(row, idxTags, _osmDataset.Workspace);
                        var tagsRestriction = tags.Where(t => t.k.Equals("restriction", StringComparison.CurrentCultureIgnoreCase));

                        foreach (tag tagRestrict in tagsRestriction)
                        {
                            OSMTurnInfo turn = new OSMTurnInfo();

                            turn.TurnType = tagRestrict.v;

                            foreach (member m in osmUtility.retrieveMembers(row, idxMembers))
                            {
                                if (m.role.Equals("from", StringComparison.CurrentCultureIgnoreCase))
                                    turn.From = m;
                                else if (m.role.Equals("to", StringComparison.CurrentCultureIgnoreCase))
                                    turn.To = m;
                                else if (m.role.Equals("via", StringComparison.CurrentCultureIgnoreCase))
                                    turn.Via = m;
                            }

                            if (turn.HasValidMembers())
                            {
                                turn.FromFeature = FindEdgeFeature(turn.From.@ref);
                                turn.ToFeature = FindEdgeFeature(turn.To.@ref);
                                turn.ViaFeature = FindJunctionFeature(turn.Via.@ref);

                                if (turn.HasValidFeatures())
                                    yield return turn;
                            }
                        }

                        TaskManager.StepProgress();
                    }
                }
            }
            finally
            {
                TaskManager.EndProgress();
            }
        }

        private IFeature FindEdgeFeature(string osmid)
        {
            List<IFeatureClass> fcEdges = OsmEdgeFeatureClasses;

            foreach (IFeatureClass fcEdge in fcEdges)
            {
                IQueryFilter filter = new QueryFilterClass();
                filter.SubFields = string.Join(",", new string[] { fcEdge.OIDFieldName, fcEdge.ShapeFieldName, "OSMID" });
                filter.WhereClause = fcEdge.WhereClauseByExtensionVersion(osmid, "OSMID", OsmExtVersion);

                using (ComReleaser cr = new ComReleaser())
                {
                    IFeatureCursor cursor = fcEdge.Search(filter, false);
                    cr.ManageLifetime(cursor);

                    IFeature feature = cursor.NextFeature();
                    if (feature != null)
                        return feature;
                }
            }

            return null;
        }

        private IFeature FindJunctionFeature(string osmid)
        {
            IFeatureClass fcPoint = OsmPointFeatureClass;

            IQueryFilter filter = new QueryFilterClass();
            filter.SubFields = string.Join(",", new string[] { fcPoint.OIDFieldName, fcPoint.ShapeFieldName, "OSMID" });
            filter.WhereClause = fcPoint.WhereClauseByExtensionVersion(osmid, "OSMID", OsmExtVersion); 

            using (ComReleaser cr = new ComReleaser())
            {
                IFeatureCursor cursor = fcPoint.Search(filter, false);
                cr.ManageLifetime(cursor);

                IFeature feature = cursor.NextFeature();
                if (feature != null)
                    return feature;
            }

            return null;
        }

        private void CreateTurnFeature_NO(TurnFeatureClassWrapper turnFCW, OSMTurnInfo osmTurn)
        {
            IFeature turn = turnFCW.TurnFeatureClass.CreateFeature();

            IPoint ptVia = osmTurn.ViaFeature.Shape as IPoint;
            IPolyline lineFrom = osmTurn.FromFeature.Shape as IPolyline;
            IPolyline lineTo = osmTurn.ToFeature.Shape as IPolyline;

            // Create Turn Shape
            bool edge1End, edge2End;
            double posFrom, posTo;
            IPoint ptStart = GetTurnEndpoint(lineFrom, ptVia, out edge1End, out posFrom);
            IPoint ptEnd = GetTurnEndpoint(lineTo, ptVia, out edge2End, out posTo);
            turn.Shape = CreateTurnGeometry(ptStart, ptVia, ptEnd, lineFrom.SpatialReference);

            // Attributes (Edge1)
            turn.set_Value(turnFCW.idxEdge1End, (edge1End) ? "Y" : "N");
            turn.set_Value(turnFCW.idxEdge1FCID, osmTurn.FromFeature.Class.ObjectClassID);
            turn.set_Value(turnFCW.idxEdge1FID, osmTurn.FromFeature.OID);
            turn.set_Value(turnFCW.idxEdge1Pos, posFrom);

            // Attributes (Edge2)
            turn.set_Value(turnFCW.idxEdge2FCID, osmTurn.ToFeature.Class.ObjectClassID);
            turn.set_Value(turnFCW.idxEdge2FID, osmTurn.ToFeature.OID);
            turn.set_Value(turnFCW.idxEdge2Pos, posTo);

            // Restriction Type
            turn.set_Value(turnFCW.idxRestrict, osmTurn.TurnType);

            turn.Store();
        }

        private void CreateTurnFeature_ONLY(TurnFeatureClassWrapper turnFCW, OSMTurnInfo osmTurn)
        {
            IPoint ptVia = osmTurn.ViaFeature.Shape as IPoint;
            IPolyline lineFrom = osmTurn.FromFeature.Shape as IPolyline;

            bool edge1End, edge2End;
            double posFrom, posTo;
            IPoint ptStart = GetTurnEndpoint(lineFrom, ptVia, out edge1End, out posFrom);

            foreach (INetworkEdge edge in EnumerateAdjacentTurnEdges(osmTurn, edge1End))
            {
                IFeature turn = turnFCW.TurnFeatureClass.CreateFeature();

                INetworkSource srcTo = _networkDataset.get_SourceByID(edge.SourceID);
                IFeatureClass fcTo = ((IFeatureClassContainer)_networkDataset).get_ClassByName(srcTo.Name);
                IFeature featureTo = fcTo.GetFeature(edge.OID);
                IPolyline lineTo = featureTo.Shape as IPolyline;

                // Create Turn Shape
                IPoint ptEnd = GetTurnEndpoint(lineTo, ptVia, out edge2End, out posTo);
                turn.Shape = CreateTurnGeometry(ptStart, ptVia, ptEnd, lineFrom.SpatialReference);

                // Attributes (Edge1)
                turn.set_Value(turnFCW.idxEdge1End, (edge1End) ? "Y" : "N");
                turn.set_Value(turnFCW.idxEdge1FCID, osmTurn.FromFeature.Class.ObjectClassID);
                turn.set_Value(turnFCW.idxEdge1FID, osmTurn.FromFeature.OID);
                turn.set_Value(turnFCW.idxEdge1Pos, posFrom);

                // Attributes (Edge2)
                turn.set_Value(turnFCW.idxEdge2FCID, featureTo.Class.ObjectClassID);
                turn.set_Value(turnFCW.idxEdge2FID, featureTo.OID);
                turn.set_Value(turnFCW.idxEdge2Pos, posTo);

                // Restriction Type
                turn.set_Value(turnFCW.idxRestrict, "NO");

                turn.Store();
            }
        }

        /// <summary>Gets the end point of a turn edge</summary>
        /// <remarks>
        /// - The Via node of a turn is assumed to be at the From or To point of the edge
        /// </remarks>
        private static IPoint GetTurnEndpoint(IPolyline line, IPoint ptVia, out bool edgeEndAtToPoint, out double position)
        {
            IPoint point = null;

            edgeEndAtToPoint = false;
            position = 0.0;

            if ((ptVia.X == line.FromPoint.X) && (ptVia.Y == line.FromPoint.Y))
            {
                point = ((IPointCollection)line).get_Point(1);
                ISegment segment = ((ISegmentCollection)line).get_Segment(0);
                position = segment.Length / line.Length;
            }
            else
            {
                edgeEndAtToPoint = true;

                IPointCollection pc = (IPointCollection)line;
                point = pc.get_Point(pc.PointCount - 2);

                ISegmentCollection sc = line as ISegmentCollection;
                ISegment segment = sc.get_Segment(sc.SegmentCount - 1);

                position = (line.Length - segment.Length) / line.Length;
            }

            return point;
        }

        private static IGeometry CreateTurnGeometry(IPoint ptStart, IPoint ptVia, IPoint ptEnd, ISpatialReference sr)
        {
            IPolyline lineTurn = new PolylineClass();
            lineTurn.SpatialReference = sr;

            IPointCollection pcTurn = lineTurn as IPointCollection;
            pcTurn.AddPoint(ptStart);
            pcTurn.AddPoint(ptVia);
            pcTurn.AddPoint(ptEnd);

            return (IGeometry)lineTurn;
        }

        private IEnumerable<INetworkEdge> EnumerateAdjacentTurnEdges(OSMTurnInfo osmTurn, bool useToJunction)
        {
            INetworkQuery query = (INetworkQuery)_networkDataset;

            // get turn FROM-edge
            INetworkSource source = _networkDataset.get_SourceByName(((IDataset)osmTurn.FromFeature.Class).Name);
            IEnumNetworkElement enumNetElements = query.get_EdgesByPosition(source.ID, osmTurn.FromFeature.OID, 0.0, false);
            INetworkEdge edgeFrom = enumNetElements.Next() as INetworkEdge;

            // get the FROM-edge Junctions
            INetworkJunction fromJunction = query.CreateNetworkElement(esriNetworkElementType.esriNETJunction) as INetworkJunction;
            INetworkJunction toJunction = query.CreateNetworkElement(esriNetworkElementType.esriNETJunction) as INetworkJunction;
            edgeFrom.QueryJunctions(fromJunction, toJunction);

            // Get adjacent edges from the turn center junction
            INetworkJunction junction = ((useToJunction) ? toJunction : fromJunction);
            for (int n = 0; n < junction.EdgeCount; ++n)
            {
                INetworkEdge edge = query.CreateNetworkElement(esriNetworkElementType.esriNETEdge) as INetworkEdge;
                junction.QueryEdge(n, true, edge);

                if ((edge.OID == osmTurn.FromFeature.OID) || (edge.OID == osmTurn.ToFeature.OID))
                    continue;

                yield return edge;
            }
        }
    }

    internal class TurnFeatureClassWrapper
    {
        public IFeatureClass TurnFeatureClass { get; set; }

        public int idxEdge1End;
        public int idxEdge1FCID;
        public int idxEdge1FID;
        public int idxEdge1Pos;
        public int idxEdge2FCID;
        public int idxEdge2FID;
        public int idxEdge2Pos;
        public int idxRestrict;

        public TurnFeatureClassWrapper()
        {
        }

        public void OpenTurnFeatureClass(IFeatureWorkspace fws, string turnClassName)
        {
            TurnFeatureClass = fws.OpenFeatureClass(turnClassName);

            idxEdge1End = TurnFeatureClass.FindField("Edge1End");
            idxEdge1FCID = TurnFeatureClass.FindField("Edge1FCID");
            idxEdge1FID = TurnFeatureClass.FindField("Edge1FID");
            idxEdge1Pos = TurnFeatureClass.FindField("Edge1Pos");
            idxEdge2FCID = TurnFeatureClass.FindField("Edge2FCID");
            idxEdge2FID = TurnFeatureClass.FindField("Edge2FID");
            idxEdge2Pos = TurnFeatureClass.FindField("Edge2Pos");
            idxRestrict = TurnFeatureClass.FindField("RestrictionType");
        }
    }

    internal class OSMTurnInfo
    {
        public member From { get; set; }
        public member To { get; set; }
        public member Via { get; set; }

        public IFeature FromFeature { get; set; }
        public IFeature ToFeature { get; set; }
        public IFeature ViaFeature { get; set; }

        public string TurnType { get; set; }

        public OSMTurnInfo()
        {
            From = To = Via = null;
            FromFeature = ToFeature = ViaFeature = null;
        }

        public bool HasValidMembers()
        {
            return (From != null && To != null && Via != null);
        }

        public bool HasValidFeatures()
        {
            return ((FromFeature != null && FromFeature.Shape != null && !FromFeature.Shape.IsEmpty)
                && (ToFeature != null && ToFeature.Shape != null && !ToFeature.Shape.IsEmpty)
                && (ViaFeature != null && ViaFeature.Shape != null && !ViaFeature.Shape.IsEmpty));
        }

        public bool IsValid()
        {
            return (HasValidMembers() && HasValidFeatures());
        }
    }
}
