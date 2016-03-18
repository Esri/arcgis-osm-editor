// (c) Copyright Esri, 2010 - 2016
// This source is subject to the Apache 2.0 License.
// Please see http://www.apache.org/licenses/LICENSE-2.0.html for details.
// All other rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ESRI.ArcGIS.Geometry;
using System.Resources;
using ESRI.ArcGIS.Geodatabase;
using System.Runtime.InteropServices;
using ESRI.ArcGIS.esriSystem;
using System.IO;
using System.Xml;
using System.Reflection;
using System.Globalization;
using System.Windows.Forms;
using ESRI.ArcGIS.Display;
using ESRI.ArcGIS.Carto;
using Microsoft.Win32;


#if _WIN64
namespace ESRI.ArcGIS.Editor
{
    [ComVisible(false)]
    [TypeLibType(256)]
    [Guid("014EE841-A498-11D1-846B-0000F875B9C6")]
    [InterfaceType(1)]
    public interface IObjectInspector
    {
        // Summary:
        //     The window handle for the inspector.
        [ComAliasName("ESRI.ArcGIS.esriSystem.OLE_HANDLE")]
        [DispId(1610678272)]
        int HWND { get; }

        // Summary:
        //     Clear the inspector before inspecting another object.
        void Clear();
        //
        // Summary:
        //     Copies the values from srcRow to the row being edited.
        void Copy(IRow srcRow);
        //
        // Summary:
        //     Inspects the properties of the features.
        void Inspect(IEnumRow objects, IEditor Editor);
    }

    // Summary:
    //     Provides access to members that enumerate rows in sequence.
    [TypeLibType(256)]
    [Guid("014EE840-A498-11D1-846B-0000F875B9C6")]
    [InterfaceType(1)]
    public interface IEnumRow
    {
        // Summary:
        //     The number of rows.
        [DispId(1610678272)]
        int Count { get; }

        // Summary:
        //     Retrieves the next row in the sequence.
        IRow Next();
        //
        // Summary:
        //     Resets the enumeration sequence to the beginning.
        void Reset();
    }


    // Summary:
    //     Provides access to a task that receives notification when the sketch is complete.
    [TypeLibType(256)]
    [Guid("6D3A6F62-9115-11D1-8461-0000F875B9C6")]
    [InterfaceType(1)]
    public interface IEditTask
    {
        // Summary:
        //     The name of the edit task.
        [DispId(1610678272)]
        string Name { get; }

        // Summary:
        //     Called by the editor when the task becomes active.
        void Activate(IEditor Editor, IEditTask oldTask);
        //
        // Summary:
        //     Called by the editor when the task becomes inactive.
        void Deactivate();
        //
        // Summary:
        //     Notifies the task that the edit sketch has been deleted.
        void OnDeleteSketch();
        //
        // Summary:
        //     Notifies the task that the edit sketch is complete.
        void OnFinishSketch();
    }


    // Summary:
    //     Indicates whether editing is happening or not.
    [Guid("929C8DC0-A0E0-11D2-8526-0000F875B9C6")]
    public enum esriEditState
    {
        // Summary:
        //     Not editing.
        esriStateNotEditing = 0,
        //
        // Summary:
        //     Editing.
        esriStateEditing = 1,
        //
        // Summary:
        //     Editing, but the map is out of focus.
        esriStateEditingUnfocused = 2,
    }

    // Summary:
    //     Provides access to members that control the behavior of the editor.
    [TypeLibType(256)]
    [InterfaceType(1)]
    [Guid("2866E6B0-C00B-11D0-802B-0000F8037368")]
    public interface IEditor
    {
        // Summary:
        //     The current edit task.
        [DispId(1610678293)]
        IEditTask CurrentTask { get; set; }
        //
        // Summary:
        //     Reference to the current display.
        [DispId(1610678275)]
        IScreenDisplay Display { get; }
        //
        // Summary:
        //     The selected features which are editable.
        [DispId(1610678288)]
        IEnumFeature EditSelection { get; }
        //
        // Summary:
        //     The editor's current edit state.
        [DispId(1610678273)]
        esriEditState EditState { get; }
        //
        // Summary:
        //     Reference to the workspace being edited.
        [DispId(1610678276)]
        IWorkspace EditWorkspace { get; }
        //
        // Summary:
        //     The last known location of the mouse.
        [DispId(1610678299)]
        IPoint Location { get; }
        //
        // Summary:
        //     Reference to the map being edited.
        [DispId(1610678274)]
        IMap Map { get; }
        //
        // Summary:
        //     Reference to the parent application.
        [DispId(1610678272)]
        object Parent { get; }
        //
        // Summary:
        //     Reference to the editor's scratch workspace.
        [DispId(1610678277)]
        IWorkspace ScratchWorkspace { get; }
        //
        // Summary:
        //     The selection anchor point.
        [DispId(1610678298)]
        IAnchorPoint SelectionAnchor { get; }
        //
        // Summary:
        //     The number of selected features which are editable.
        [DispId(1610678289)]
        int SelectionCount { get; }
        //
        // Summary:
        //     The number of edit tasks.
        [DispId(1610678291)]
        int TaskCount { get; }

        // Summary:
        //     Aborts an edit operation.
        void AbortOperation();
        //
        // Summary:
        //     Creates a geometry using the point and the current search tolerance.
        IGeometry CreateSearchShape(IPoint point);
        //
        // Summary:
        //     Used to batch operations together and minimize notifications.
        void DelayEvents(bool delay);
        //
        // Summary:
        //     Enable/disable the undo/redo capabilities.
        void EnableUndoRedo(bool Enabled);
        IExtension FindExtension(UID extensionID);
        IEditTask get_Task(int index);
        //
        // Summary:
        //     Indicates whether edits have been made during the session.
        bool HasEdits();
        //
        // Summary:
        //     Draws the editor's snapping agent.
        void InvertAgent(IPoint loc, int hdc);
        //
        // Summary:
        //     Redo an edit operation.
        void RedoOperation();
        //
        // Summary:
        //     Searches the edit selection using the given location.
        IEnumFeature SearchSelection(IPoint point);
        //
        // Summary:
        //     Starts an edit session.
        void StartEditing(IWorkspace workspace);
        //
        // Summary:
        //     Starts an edit operation.
        void StartOperation();
        //
        // Summary:
        //     Stops an edit session.
        void StopEditing(bool saveChanges);
        //
        // Summary:
        //     Stops an edit operation.
        void StopOperation(string menuText);
        //
        // Summary:
        //     Undo an edit operation.
        void UndoOperation();
    }
}
#endif


namespace ESRI.ArcGIS.OSM.OSMClassExtension
{
    [Guid("65CA4847-8661-45eb-8E1E-B2985CA17C78")]
    [ClassInterface(ClassInterfaceType.None)]
    [ProgId("OSMEditor.OSMClassExtension")]
    [ComVisible(true)]
    public class OpenStreetMapClassExtension : ESRI.ArcGIS.Geodatabase.IClassExtension, ESRI.ArcGIS.Geodatabase.IFeatureClassExtension, IObjectClassEvents, IObjectClassInfo2, IOSMClassExtension, ESRI.ArcGIS.Editor.IObjectInspector
    {

        #region COM Registration Function(s)
        [ComRegisterFunction()]
        [ComVisible(false)]
        static void RegisterFunction(Type registerType)
        {
            // Required for ArcGIS Component Category Registrar support
            ArcGISCategoryRegistration(registerType);
        }

        [ComUnregisterFunction()]
        [ComVisible(false)]
        static void UnregisterFunction(Type registerType)
        {
            // Required for ArcGIS Component Category Registrar support
            ArcGISCategoryUnregistration(registerType);
        }

        #region ArcGIS Component Category Registrar generated code
        /// <summary>
        /// Required method for ArcGIS Component Category registration -
        /// Do not modify the contents of this method with the code editor.
        /// </summary>
        private static void ArcGISCategoryRegistration(Type registerType)
        {
            string regKey = string.Format("HKEY_CLASSES_ROOT\\CLSID\\{{{0}}}", registerType.GUID);
            Registry.ClassesRoot.CreateSubKey(regKey.Substring(18) + "\\Implemented Categories\\{D4E2A322-5D59-11D2-89FD-006097AFF44E}");

        }
        /// <summary>
        /// Required method for ArcGIS Component Category unregistration -
        /// Do not modify the contents of this method with the code editor.
        /// </summary>
        private static void ArcGISCategoryUnregistration(Type registerType)
        {
            string regKey = string.Format("HKEY_CLASSES_ROOT\\CLSID\\{{{0}}}", registerType.GUID);
            Registry.ClassesRoot.DeleteSubKey(regKey.Substring(18) + "\\Implemented Categories\\{D4E2A322-5D59-11D2-89FD-006097AFF44E}");

        }

        #endregion
        #endregion
        private OSMUtility _osmUtility = null;

        public OpenStreetMapClassExtension()
        {
            try
            {
                resourceManager = new ResourceManager("ESRI.ArcGIS.OSM.OSMClassExtension.OSMClassExtensionStrings", this.GetType().Assembly);

                _osmUtility = new OSMUtility();

                // attempt to instantiate the feature inspector
                // get the implementing COM object 
                if ((m_Inspector == null) && (ESRI.ArcGIS.RuntimeManager.ActiveRuntime.Product == ProductCode.Desktop))
                {
                    Type oType = Type.GetTypeFromProgID("OSMEditor.OSMFeatureInspectorUI");
                    if (oType != null)
                    {
                        object something = Activator.CreateInstance(oType);
                        m_Inspector = something as ESRI.ArcGIS.Editor.IObjectInspector;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
            }
        }


        #region IClassExtension Members

        // Temporary index for newly created OSM IDs
        //  - shared between point / line and polygon feature classes for each unique osm dataset
        //  - by convention: use then decrement
        private long _temporaryIndex = 0;
        private string _revisionTableName;

        private static Dictionary<string, long> _tempIndicies;
        private static Dictionary<string, long> TempIndicies
        {
            get
            {
                if (_tempIndicies == null)
                    _tempIndicies = new Dictionary<string, long>();
                return _tempIndicies;
            }
        }

        private ISpatialReference m_wgs84 = null;
        private int _ExtensionVersion = 1;
        private ResourceManager resourceManager = null;
        private bool m_bypassOSMChangeDetection = false;
        private IClass m_baseClass = null;
        private ESRI.ArcGIS.Editor.IObjectInspector m_Inspector = null;

        public void Init(ESRI.ArcGIS.Geodatabase.IClassHelper ClassHelper, ESRI.ArcGIS.esriSystem.IPropertySet ExtensionProperties)
        {
            try
            {
                ISpatialReferenceFactory spatialReferenceFactory = new SpatialReferenceEnvironmentClass() as ISpatialReferenceFactory;
                m_wgs84 = spatialReferenceFactory.CreateGeographicCoordinateSystem((int)esriSRGeoCSType.esriSRGeoCS_WGS1984) as ISpatialReference;

                m_baseClass = ClassHelper.Class;

                // Save the path to the revision table (used to track temp indexes)
                IDataset ds = (IDataset)m_baseClass;
                _revisionTableName = "Empty";
                string className = ds.Name;
                int osmDelimiterPosition = className.IndexOf("_osm_");
                if (osmDelimiterPosition >= 0)
                {
                    string baseName = className.Substring(0, osmDelimiterPosition);
                    _revisionTableName = System.IO.Path.Combine(ds.Workspace.PathName, baseName + "_osm_revision");
                }

                if (ExtensionProperties == null)
                    _ExtensionVersion = 1;
                else
                {
                    try
                    {
                        _ExtensionVersion = Convert.ToInt32(ExtensionProperties.GetProperty("VERSION"));
                    }
                    catch
                    {
                        _ExtensionVersion = 1;
                    }
                }


            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
            }
        }

        public void Shutdown()
        {
            // general release of resources


        }

        #endregion

        #region IObjectClassEvents Members

        void IObjectClassEvents.OnChange(IObject obj)
        {
            // if the class extension is set to ignore the change dection, let's stop right now
            if (m_bypassOSMChangeDetection)
            {
                return;
            }

            HandleFeatureChanges(obj);
        }

        void IObjectClassEvents.OnCreate(IObject obj)
        {
            // if the class extension is set to ignore the change dection, let's stop right now
            if (m_bypassOSMChangeDetection)
            {
                return;
            }

            HandleFeatureAdds(obj);
        }

        void IObjectClassEvents.OnDelete(IObject obj)
        {
            // if the class extension is set to ignore the change dection, let's stop right now
            if (m_bypassOSMChangeDetection)
            {
                return;
            }

            HandleFeatureDeletes(obj);
        }

        #endregion

        #region Worker Methods

        public void HandleFeatureDeletes(IObject deleteObject)
        {
            IFeature deleteFeature = deleteObject as IFeature;

            if (deleteFeature == null)
            {
                return;
            }

            IFeatureClass currentObjectFeatureClass = deleteFeature.Class as IFeatureClass;
            if (currentObjectFeatureClass == null)
                return;

            // check if the current feature class being edited is acutally an OpenStreetMap feature class
            // all other feature class should not be touched by this extension
            UID osmFeatureClassExtensionCLSID = currentObjectFeatureClass.EXTCLSID;

            // this could be the case if there is no feature class extension, this is very unlikely as the fc extension itself triggers this code
            // hence it has to exist
            if (osmFeatureClassExtensionCLSID == null)
            {
                return;
            }

            if (osmFeatureClassExtensionCLSID.Value.ToString().Equals("{65CA4847-8661-45eb-8E1E-B2985CA17C78}", StringComparison.InvariantCultureIgnoreCase) == false)
            {
                return;
            }

            // find/retrieve the osm logging/revision table
            ITable revisionTable = findRevisionTable(deleteFeature);

            // Set revision table from cache or calculate from revision table
            SetTemporaryIndex(deleteObject, revisionTable);

            // find/retrieve the table containing the relations
            ITable relationTable = findRelationTable(deleteFeature);

            int wayRefCountFieldIndex = -1;
            int osmIDFieldIndex = -1;
            int supportingElementFieldIndex = -1;
            int osmTagsFieldIndex = -1;

            wayRefCountFieldIndex = deleteFeature.Fields.FindField("wayRefCount");
            osmIDFieldIndex = deleteFeature.Fields.FindField("OSMID");
            supportingElementFieldIndex = deleteFeature.Fields.FindField("osmSupportingElement");
            osmTagsFieldIndex = deleteFeature.Fields.FindField("osmTags");

            int osmVersionFieldIndex = deleteFeature.Fields.FindField("osmversion");
            int osmChangeSetFieldIndex = deleteFeature.Fields.FindField("osmchangeset");
            int osmMemberOfFieldIndex = deleteFeature.Fields.FindField("osmMemberOf");
            int osmMembersFieldIndex = deleteFeature.Fields.FindField("osmMembers");


            //bool trackChanges = true;
            int osmTrackChangesFieldIndex = currentObjectFeatureClass.FindField("osmTrackChanges");

            //if (osmTrackChangesFieldIndex > -1)
            //{
            //    try
            //    {
            //        int trackChangesIndicator = Convert.ToInt32(deleteFeature.get_Value(osmTrackChangesFieldIndex));

            //        if (trackChangesIndicator != 0)
            //        {
            //            trackChanges = false;
            //        }
            //    }
            //    catch { }
            //}

            int osmVersion = -1;
            int osmChangeSet = -1;
            int relationVersion = -1;

            if (deleteFeature.Shape == null)
            {
                return;
            }

            if (deleteFeature.Shape.GeometryType == esriGeometryType.esriGeometryPoint)
            {
                // flag as delete and demote the enitity to supporting node
                int wayRefCount = 0;
                if (wayRefCountFieldIndex > 0)
                {
                    wayRefCount = ReadAttributeValueAsInt((IRow)deleteFeature, wayRefCountFieldIndex) ?? -1;
                }

                if (wayRefCount > 1)
                {
                    // delete the osm tags if node is no longer a stand-alone feature
                    if (osmTagsFieldIndex > -1)
                    {
                        if (osmTagsAreStillValid(deleteFeature))
                        {
                        }
                        else
                        {
                            deleteFeature.set_Value(osmTagsFieldIndex, DBNull.Value);

                            if (supportingElementFieldIndex > -1)
                            {
                                deleteFeature.set_Value(supportingElementFieldIndex, "yes");
                            }
                        }
                    }

                    // decrease the reference count by one
                    deleteFeature.set_Value(wayRefCountFieldIndex, wayRefCount - 1);

                    // test if the feature is part of a relation
                    // if it is then we make sure that we modify the relation(s) to which the feature belongs
                    if (osmMemberOfFieldIndex > -1)
                    {
                        List<string> relationIDs = _osmUtility.retrieveIsMemberOf((IRow)deleteFeature, osmMemberOfFieldIndex);

                        Dictionary<string, string> relationIDsAndTypes = _osmUtility.parseIsMemberOfList(relationIDs);

                        foreach (var relationIDandType in relationIDsAndTypes)
                        {
                            if (relationIDandType.Value.Equals("rel"))
                            {
                                bool IdRemoved = RemoveOSMIDfromRelation(relationTable, Convert.ToInt64(relationIDandType.Key), memberType.node, Convert.ToInt64(deleteFeature.get_Value(osmIDFieldIndex)), out relationVersion, out osmChangeSet);
                                if (m_bypassOSMChangeDetection == false)
                                {
                                    if (IdRemoved)
                                    {
                                        LogOSMAction(revisionTable, "modify", "relation", ((IDataset)relationTable).Name, Convert.ToInt32(relationIDandType.Key), relationVersion, osmChangeSet, null);
                                    }
                                }
                            }
                            else if (relationIDandType.Value.Equals("ln"))
                            {
                            }
                            else if (relationIDandType.Value.Equals("ply"))
                            {
                            }
                        }
                    }
                }
                else
                {
                    long osmID = ReadAttributeValueAsLong(deleteFeature, osmIDFieldIndex) ?? _temporaryIndex;
                    osmVersion = ReadAttributeValueAsInt(deleteFeature, osmVersionFieldIndex) ?? -1;
                    osmChangeSet = -1;// ReadAttributeValueAsInt(deleteFeature, osmChangeSetFieldIndex) ?? -1;

                    if (m_bypassOSMChangeDetection == false)
                    {
                        LogOSMAction(revisionTable, "delete", "node", ((IDataset)deleteFeature.Class).Name, osmID, osmVersion, osmChangeSet, (IPoint)deleteFeature.Shape);
                    }

                    // test if the feature is part of a relation
                    // if it is then we make sure that we modify the relation(s) to which the feature belongs
                    if (osmMemberOfFieldIndex > -1)
                    {
                        List<string> relationIDs = _osmUtility.retrieveIsMemberOf((IRow)deleteFeature, osmMemberOfFieldIndex);

                        Dictionary<string, string> relationIDsAndTypes = _osmUtility.parseIsMemberOfList(relationIDs);

                        foreach (var relationIDandType in relationIDsAndTypes)
                        {
                            if (relationIDandType.Value.Equals("rel"))
                            {
                                RemoveOSMIDfromRelation(relationTable, Convert.ToInt64(relationIDandType.Key), memberType.node, Convert.ToInt64(deleteFeature.get_Value(osmIDFieldIndex)), out relationVersion, out osmChangeSet);
                                if (m_bypassOSMChangeDetection == false)
                                {
                                    LogOSMAction(revisionTable, "modify", "relation", ((IDataset)relationTable).Name, Convert.ToInt32(relationIDandType.Key), relationVersion, osmChangeSet, null);
                                }
                            }
                            else if (relationIDandType.Value.Equals("ln"))
                            {
                            }
                            else if (relationIDandType.Value.Equals("ply"))
                            {
                            }
                        }
                    }
                }

                // we have completed the actions due to point deletion - return
                return;
            }

            // process supporting nodes for ways / polygons
            IFeatureClass pointFeatureClass = findMatchingFeatureClass(deleteFeature, esriGeometryType.esriGeometryPoint);
            wayRefCountFieldIndex = pointFeatureClass.Fields.FindField("wayRefCount");

            foreach (IFeature nodeFeature in PointFeaturesFromWayOrPoly(deleteFeature, pointFeatureClass))
            {
                try
                {
                    // flag as delete and demote the enitity to supporting node
                    int wayRefCount = 0;
                    if (wayRefCountFieldIndex > 0)
                    {
                        wayRefCount = ReadAttributeValueAsInt(nodeFeature, wayRefCountFieldIndex) ?? 0;
                    }

                    if (wayRefCount > 1)
                    {
                        // reduce the reference counter by 1 node, since we are deleting the feature and the vertices
                        if (wayRefCountFieldIndex > -1)
                        {
                            nodeFeature.set_Value(wayRefCountFieldIndex, wayRefCount - 1);
                        }

                        // persist the update into the node 
                        nodeFeature.Store();
                    }
                    else
                    {
                        // remove the node feature altogether
                        nodeFeature.Delete();
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Error deleting supporting node: " + ex.ToString());
                }
            }

            // 
            osmTagsFieldIndex = deleteFeature.Fields.FindField("osmTags");
            osmChangeSetFieldIndex = deleteFeature.Fields.FindField("osmchangeset");
            osmIDFieldIndex = deleteFeature.Fields.FindField("OSMID");
            long deleteFeatureOSMID = ReadAttributeValueAsLong(deleteFeature, osmIDFieldIndex) ?? -1;
            osmVersionFieldIndex = deleteFeature.Fields.FindField("osmversion");

            osmVersion = ReadAttributeValueAsInt(deleteFeature, osmVersionFieldIndex) ?? -1;
            osmChangeSet = -1; // ReadAttributeValueAsInt(deleteFeature, osmChangeSetFieldIndex) ?? -1;

            IGeometryCollection deleteGeometryCollection = deleteFeature.Shape as IGeometryCollection;
            if (deleteGeometryCollection.GeometryCount > 1)
            {
                // flag members of the relation/multi-part geometry for deletion
                if (osmMembersFieldIndex > -1)
                {
                    member[] relationMembers = _osmUtility.retrieveMembers(deleteFeature, osmMembersFieldIndex);

                    if (relationMembers != null)
                    {
                        ITable deleteFeatureClass = (ITable)deleteFeature.Class;
                        string sqlDeleteOSMID = deleteFeatureClass.SqlIdentifier("OSMID");

                        using (ComReleaser comReleaser = new ComReleaser())
                        {
                            foreach (member currentosmRelationMember in relationMembers)
                            {
                                IRow currentRow = null;
                                IQueryFilter queryfilter = new QueryFilterClass();
                                queryfilter.WhereClause = deleteFeatureClass.WhereClauseByExtensionVersion(currentosmRelationMember.@ref, "OSMID", _ExtensionVersion);

                                // since we are only dealing with homogeneous geometry types  - releations with only polygons or only polylines
                                ICursor relationMemberCursor = deleteFeatureClass.Search(queryfilter, false);
                                comReleaser.ManageLifetime(relationMemberCursor);

                                currentRow = relationMemberCursor.NextRow();

                                if (currentRow != null)
                                {
                                    // delete the member representation of the multipart polygon
                                    currentRow.Delete();
                                }
                            }
                        }
                    }
                }

                if (m_bypassOSMChangeDetection == false)
                {
                    // if we have a multi-part geometry then we are dealing with a relation type in the OSM world
                    LogOSMAction(revisionTable, "delete", "relation", ((IDataset)deleteFeature.Class).Name, deleteFeatureOSMID, osmVersion, osmChangeSet, null);
                }
            }
            else
            {
                if (m_bypassOSMChangeDetection == false)
                {
                    LogOSMAction(revisionTable, "delete", "way", ((IDataset)deleteFeature.Class).Name, deleteFeatureOSMID, osmVersion, osmChangeSet, null);
                }
            }

            // test if the feature is part of a relation
            // if it is then we make sure that we modify the relation(s) to which the feature belongs
            if (osmMemberOfFieldIndex > -1)
            {
                List<string> relationIDs = _osmUtility.retrieveIsMemberOf((IRow)deleteFeature, osmMemberOfFieldIndex);

                Dictionary<string, string> relationIDsAndTypes = _osmUtility.parseIsMemberOfList(relationIDs);

                foreach (var relationIDandType in relationIDsAndTypes)
                {
                    if (relationIDandType.Value.Equals("rel"))
                    {
                        bool IdRemoved = RemoveOSMIDfromRelation(relationTable, Convert.ToInt64(relationIDandType.Key), memberType.way, Convert.ToInt64(deleteFeature.get_Value(osmIDFieldIndex)), out relationVersion, out osmChangeSet);
                        if (m_bypassOSMChangeDetection == false)
                        {
                            // if something was indeed removed we need to log the event
                            if (IdRemoved)
                            {
                                LogOSMAction(revisionTable, "modify", "relation", ((IDataset)relationTable).Name, Convert.ToInt32(relationIDandType.Key), relationVersion, osmChangeSet, null);
                            }
                        }
                    }
                    else if (relationIDandType.Value.Equals("ln"))
                    {
                        bool IdRemoved = RemoveOSMIDfromRelation(relationTable, Convert.ToInt64(relationIDandType.Key), memberType.way, Convert.ToInt64(deleteFeature.get_Value(osmIDFieldIndex)), out relationVersion, out osmChangeSet);
                        if (m_bypassOSMChangeDetection == false)
                        {
                            // if something was indeed removed we need to log the event
                            if (IdRemoved)
                            {
                                LogOSMAction((ITable)currentObjectFeatureClass, "modify", "way", ((IDataset)currentObjectFeatureClass).Name, Convert.ToInt32(relationIDandType.Key), relationVersion, osmChangeSet, null);
                            }
                        }
                    }
                    else if (relationIDandType.Value.Equals("ply"))
                    {
                        bool IdRemoved = RemoveOSMIDfromRelation(relationTable, Convert.ToInt64(relationIDandType.Key), memberType.way, Convert.ToInt64(deleteFeature.get_Value(osmIDFieldIndex)), out relationVersion, out osmChangeSet);
                        if (m_bypassOSMChangeDetection == false)
                        {
                            // if something was indeed removed we need to log the event
                            if (IdRemoved)
                            {
                                LogOSMAction((ITable)currentObjectFeatureClass, "modify", "way", ((IDataset)currentObjectFeatureClass).Name, Convert.ToInt32(relationIDandType.Key), relationVersion, osmChangeSet, null);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>Return a sequence of node features associated with the given osm line or polygon</summary>
        private IEnumerable<IFeature> PointFeaturesFromWayOrPoly(IFeature wayFeature, IFeatureClass pointFeatureClass)
        {
            IPointCollection pointCollection = wayFeature.Shape as IPointCollection;
            if ((pointCollection == null) || (pointFeatureClass == null))
                yield break;

            IQueryFilter filter = new QueryFilterClass();

            for (int idx = 0; idx < pointCollection.PointCount; ++idx)
            {
                int pointOID = pointCollection.get_Point(idx).ID;

                // Query for the feature by OSMID or ObjectID in the point feature class
                if (_ExtensionVersion == 1)
                {
                    filter.WhereClause = pointFeatureClass.WhereClauseByExtensionVersion(pointOID, "OSMID", _ExtensionVersion);
                }
                else if (_ExtensionVersion == 2)
                {
                    filter.WhereClause = string.Format("{0} = {1}", pointFeatureClass.OIDFieldName, pointOID);
                }

                using (ComReleaser comReleaser = new ComReleaser())
                {
                    IFeature pointFeature = null;

                    try
                    {
                        IFeatureCursor searchCursor = pointFeatureClass.Search(filter, false);
                        comReleaser.ManageLifetime(searchCursor);
                        pointFeature = searchCursor.NextFeature();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(
                            string.Format("Node ID Search (filter: {0}) Failed: {1}" + filter.WhereClause, ex.ToString()));
                    }

                    if (pointFeature != null)
                        yield return pointFeature;
                }
            }
        }

        private bool osmTagsAreStillValid(IFeature deleteFeature)
        {
            bool tagsAreStillValid = true;

            try
            {
                // see if there is a line feature that the deletion point
                IFeatureClass lineFeatureClass = findMatchingFeatureClass(deleteFeature, esriGeometryType.esriGeometryPolyline);

                ISpatialFilter spatialFilter = new SpatialFilterClass();
                spatialFilter.Geometry = deleteFeature.Shape;
                spatialFilter.SpatialRel = esriSpatialRelEnum.esriSpatialRelTouches;
                spatialFilter.GeometryField = lineFeatureClass.ShapeFieldName;

                using (ComReleaser comReleaser = new ComReleaser())
                {
                    IFeatureCursor searchCursor = lineFeatureClass.Search(spatialFilter, false);
                    comReleaser.ManageLifetime(searchCursor);
                    IFeature foundFeature = searchCursor.NextFeature();

                    if (foundFeature != null)
                    {
                        return true;
                    }
                }

                IFeatureClass polygonFeatureClass = findMatchingFeatureClass(deleteFeature, esriGeometryType.esriGeometryPolygon);

                using (ComReleaser comReleaser = new ComReleaser())
                {
                    IFeatureCursor searchCursor = polygonFeatureClass.Search(spatialFilter, false);
                    comReleaser.ManageLifetime(searchCursor);
                    IFeature foundFeature = searchCursor.NextFeature();

                    if (foundFeature != null)
                    {
                        return true;
                    }
                }

                ComReleaser.ReleaseCOMObject(spatialFilter);

                tagsAreStillValid = false;
            }
            catch { }

            return tagsAreStillValid;
        }

        private bool RemoveOSMIDfromRelation(ITable relationTable, long relationID, memberType deleteMemberType, long idToDelete, out int relationVersion, out int relationChangeset)
        {
            int osmIDFieldIndex = relationTable.Fields.FindField("OSMID");
            int membersFieldIndex = relationTable.Fields.FindField("osmMembers");
            int versionFieldIndex = relationTable.Fields.FindField("osmVersion");
            int changesetFieldIndex = relationTable.Fields.FindField("osmchangeset");
            bool somethingHasBeenRemoved = false;

            relationVersion = -1;
            relationChangeset = -1;

            IQueryFilter queryFilter = new QueryFilter();
            queryFilter.WhereClause = relationTable.WhereClauseByExtensionVersion(relationID, "OSMID", _ExtensionVersion);

            using (ComReleaser comReleaser = new ComReleaser())
            {
                ICursor tableCursor = relationTable.Search(queryFilter, false);
                comReleaser.ManageLifetime(tableCursor);

                IRow relationRow = tableCursor.NextRow();

                if (relationRow != null)
                {
                    member[] relationMembers = _osmUtility.retrieveMembers(relationRow, membersFieldIndex);

                    List<member> updatedMembers = new List<member>();

                    for (int memberIndex = 0; memberIndex < relationMembers.Length; memberIndex++)
                    {
                        if (relationMembers[memberIndex].type == deleteMemberType)
                        {
                            if (relationMembers[memberIndex].@ref.Equals(idToDelete.ToString()))
                            {
                                somethingHasBeenRemoved = true;
                            }
                            else
                            {
                                updatedMembers.Add(relationMembers[memberIndex]);
                            }
                        }
                    }

                    _osmUtility.insertMembers(membersFieldIndex, relationRow, updatedMembers.ToArray());

                    if (versionFieldIndex != -1)
                    {
                        relationVersion = Convert.ToInt32(relationRow.get_Value(versionFieldIndex));
                    }

                    if (changesetFieldIndex != -1)
                    {
                        relationChangeset = Convert.ToInt32(relationRow.get_Value(changesetFieldIndex));
                    }

                }
            }

            return somethingHasBeenRemoved;
        }

        public void HandleFeatureChanges(IObject changedObject)
        {
            IFeature changeFeature = changedObject as IFeature;

            if (changeFeature == null)
            {
                return;
            }

            IFeatureClass currentObjectFeatureClass = changeFeature.Class as IFeatureClass;
            if (currentObjectFeatureClass == null)
                return;

            // check if the current feature class being edited is acutally an OpenStreetMap feature class
            // all other feature class should not be touched by this extension
            UID osmFeatureClassExtensionCLSID = currentObjectFeatureClass.EXTCLSID;

            // this could be the case if there is no feature class extension, this is very unlikely as the fc extension itself triggers this code
            // hence it has to exist
            if (osmFeatureClassExtensionCLSID == null)
            {
                return;
            }

            if (osmFeatureClassExtensionCLSID.Value.ToString().Equals("{65CA4847-8661-45eb-8E1E-B2985CA17C78}", StringComparison.InvariantCultureIgnoreCase) == false)
            {
                return;
            }

            // find/retrieve the instance of the logging/revision table 
            ITable revisionTable = findRevisionTable(changeFeature);

            // Set revision table from cache or calculate from revision table
            SetTemporaryIndex(changedObject, revisionTable);

            bool trackChanges = true;
            int osmTrackChangesFieldIndex = currentObjectFeatureClass.FindField("osmTrackChanges");

            try
            {
                // if the feature geometry has changed make sure the matching point do exist in the _osm_pt feature class
                // as well as that the geometry is tested for coincident nodes
                if (((IFeatureChanges)changeFeature).ShapeChanged)
                {
                    IGeometry oldGeometry = ((IFeatureChanges)changeFeature).OriginalShape;

#if DEBUG
                    // debug code
                    #region ID tests
                    if (oldGeometry.GeometryType != esriGeometryType.esriGeometryPoint)
                    {
                        IEnumVertex enum1 = ((IPointCollection)oldGeometry).EnumVertices as IEnumVertex;

                        if (enum1 != null)
                        {
                            IPoint p1 = null;
                            int partIndex1 = -1;
                            int vertexIndex1 = -1;

                            enum1.Next(out p1, out partIndex1, out vertexIndex1);

                            while (p1 != null)
                            {
                                enum1.Next(out p1, out partIndex1, out vertexIndex1);
                            }
                        }
                    }
                    #endregion
#endif


                    IGeometry currentGeometry = changeFeature.Shape;

#if DEBUG
                    // debug code
                    #region ID tests
                    if (currentGeometry.GeometryType != esriGeometryType.esriGeometryPoint)
                    {
                        IEnumVertex enum2 = ((IPointCollection)currentGeometry).EnumVertices as IEnumVertex;

                        if (enum2 != null)
                        {
                            IPoint p2 = null;
                            int partIndex2 = -1;
                            int vertexIndex2 = -1;

                            enum2.Next(out p2, out partIndex2, out vertexIndex2);

                            while (p2 != null)
                            {
                                enum2.Next(out p2, out partIndex2, out vertexIndex2);
                            }
                        }
                    }
                    #endregion
#endif


                    IRelationalOperator relationalOperator = currentGeometry as IRelationalOperator;

                    bool equalPointCount = true;
                    if (currentGeometry is IPointCollection)
                    {
                        equalPointCount = ((IPointCollection)currentGeometry).PointCount != ((IPointCollection)oldGeometry).PointCount;
                    }

                    if ((relationalOperator.Equals(oldGeometry) == false) || equalPointCount)
                    {
                        // in case we are dealing with a point feature check if the point was updated to coincide with another node
                        // if this is the case then the new node merges with the existing node and its attributes
                        if (changeFeature.Shape.GeometryType == esriGeometryType.esriGeometryPoint)
                        {
                            // check if there are connected polygon or lines features
                            //CheckforMovingAwayFromFeature(changeFeature, trackChanges);

                            UpdateMergeNode(changeFeature, revisionTable);
                        }
                        else
                        {
                            // find the point feature class that contains the nodes
                            IFeatureClass pointFeatureClass = null;
                            pointFeatureClass = findMatchingFeatureClass(changeFeature, esriGeometryType.esriGeometryPoint);

                            if (pointFeatureClass == null)
                            {
                                System.Diagnostics.Debug.WriteLine("unable to locate point (osm node) feature class. defering updates to stop edit operation.");
                                return;
                            }


                            // handle the special case of more than 2000 vertices 
                            // and multipart entities
                            object MissingValue = Missing.Value;

                            // lines of nodes larger than 2000 are split into multiple features
                            IPointCollection pointCollection = null;
                            IGeometryCollection changedGeometryCollection = null;

                            pointCollection = changeFeature.Shape as IPointCollection;

                            // in case of a merge operation the new geometry is empty and old one contains the updates
                            // if this is actually the case then something is terribly wrong and we shouldn't continue at this point
                            // (this seems to happen occasionally - not expected and not quite reproducible)
                            if (pointCollection.PointCount == 0)
                            {
                                throw new ArgumentOutOfRangeException("changedObject", resourceManager.GetString("OSMClassExtension_emptyGeometry"));
                                //pointCollection = ((IFeatureChanges)changeFeature).OriginalShape as IPointCollection;
                            }

                            changedGeometryCollection = changeFeature.Shape as IGeometryCollection;

                            if (changedGeometryCollection.GeometryCount == 0)
                            {
                                changedGeometryCollection = ((IFeatureChanges)changeFeature).OriginalShape as IGeometryCollection;
                            }

                            int osmChangeFeatureIDFieldIndex = currentObjectFeatureClass.FindField("OSMID");
                            int osmChangeFeatureVersionFieldIndex = currentObjectFeatureClass.FindField("osmversion");
                            int osmChangeFeatureSupportElementFieldIndex = currentObjectFeatureClass.FindField("osmSupportingElement");
                            int osmChangeFeatureIsMemberOfFieldIndex = currentObjectFeatureClass.FindField("osmMemberOf");
                            int osmChangeFeatureTimeStampFieldIndex = currentObjectFeatureClass.FindField("osmtimestamp");

                            DecrementTemporaryIndex();

                            // depending on the incoming geometry type loop through all the points and make sure that they are put into chunks of 
                            // 2000 nodes/points
                            pointCollection = createOSMRelationNodeClusters("change", changeFeature, osmChangeFeatureIDFieldIndex, osmChangeFeatureVersionFieldIndex, osmChangeFeatureSupportElementFieldIndex, osmChangeFeatureIsMemberOfFieldIndex, osmChangeFeatureTimeStampFieldIndex, osmTrackChangesFieldIndex, trackChanges);

                            // otherwise we have to do some node matching
                            // - create if node doesn't exist yet
                            // - update if the node/vertex already existed
                            // - delete if the node/vertex was removed
                            IPointIDAware topGeometryIDAware = pointCollection as IPointIDAware;

                            if (topGeometryIDAware != null)
                            {
                                topGeometryIDAware.PointIDAware = true;
                            }

                            int pointSupportElementFieldIndex = pointFeatureClass.FindField("osmSupportingElement");
                            int pointosmIDFieldIndex = pointFeatureClass.FindField("osmID");
                            int pointwayRefCountFieldIndex = pointFeatureClass.FindField("wayRefCount");
                            int pointVersionFieldIndex = pointFeatureClass.FindField("osmVersion");
                            int pointisMemberOfFieldIndex = pointFeatureClass.FindField("osmMemberOf");
                            int pointTrackChangesFieldIndex = pointFeatureClass.FindField("osmTrackChanges");

                            long changeRowOSMID = -1;
                            if (osmChangeFeatureIDFieldIndex > -1)
                            {
                                changeRowOSMID = Convert.ToInt64(changedObject.get_Value(osmChangeFeatureIDFieldIndex));
                            }

                            IGeometryCollection changeGeometryCollection = pointCollection as IGeometryCollection;

                            // loop through all vertices and check for coincident and new points
                            pointCollection = checkAllPoints(changeFeature, pointFeatureClass, "modify", osmChangeFeatureIDFieldIndex, pointCollection, trackChanges);

                            changeFeature.Shape = (IGeometry)pointCollection;

                            // now let's check the old geometry if we need to delete a node
                            List<int> nowMissingNodes = findDeletedNodeIDs(((IFeatureChanges)changeFeature).OriginalShape, changeFeature.Shape);

                            // no deleted nodes were detected
                            // we are done at this point
                            if (nowMissingNodes.Count > 0)
                            {
                                string sqlPointOSMID = pointFeatureClass.SqlIdentifier("OSMID");

                                foreach (int deleteNodeID in nowMissingNodes)
                                {
                                    IQueryFilter queryfilter = new QueryFilter();
                                    IFeature foundFeature = null;
                                    if (_ExtensionVersion == 1)
                                    {
                                        queryfilter.WhereClause = pointFeatureClass.WhereClauseByExtensionVersion(deleteNodeID, "OSMID", _ExtensionVersion);

                                        IFeatureCursor searchCursor = pointFeatureClass.Search(queryfilter, false);
                                        foundFeature = searchCursor.NextFeature();
                                    }
                                    else if (_ExtensionVersion == 2)
                                    {
                                        try
                                        {
                                            foundFeature = pointFeatureClass.GetFeature(deleteNodeID);
                                        }
                                        catch
                                        {
                                            foundFeature = null;
                                        }
                                    }

                                    if (foundFeature != null)
                                    {
                                        int wayRefCount = 0;

                                        if (pointwayRefCountFieldIndex > -1)
                                        {
                                            wayRefCount = Convert.ToInt32(foundFeature.get_Value(pointwayRefCountFieldIndex));
                                        }


                                        if (wayRefCount > 1)
                                        {
                                            foundFeature.set_Value(pointwayRefCountFieldIndex, wayRefCount - 1);

                                            //if (pointTrackChangesFieldIndex > -1)
                                            //{
                                            //    if (trackChanges == false)
                                            //    {
                                            //        foundFeature.set_Value(pointTrackChangesFieldIndex, 1);
                                            //    }
                                            //}

                                            foundFeature.Store();
                                        }
                                        else
                                        {
                                            // delete the vertex from the parent feature as well
                                            List<string> isMembersOf = _osmUtility.retrieveIsMemberOf(foundFeature, pointisMemberOfFieldIndex);

                                            Dictionary<string, string> isMembersOfIDsAndTypes = _osmUtility.parseIsMemberOfList(isMembersOf);

                                            #region use isMemberOf info to determine relationship to parent
                                            IFeatureClass fc = (IFeatureClass)changedObject.Class;
                                            string sqlOSMID = fc.SqlIdentifier("OSMID");

                                            foreach (string currentParentID in isMembersOfIDsAndTypes.Keys)
                                            {
                                                using (ComReleaser comReleaser = new ComReleaser())
                                                {
                                                    IQueryFilter parentIDFilter = new QueryFilterClass();
                                                    parentIDFilter.WhereClause = sqlOSMID + " = " + currentParentID;

                                                    IFeatureCursor parentUpdateFeatureCursor = fc.Search(parentIDFilter, false);
                                                    comReleaser.ManageLifetime(parentUpdateFeatureCursor);

                                                    IFeature currentParentFeature = parentUpdateFeatureCursor.NextFeature();

                                                    // for each of the found parents, loop through and remove the point
                                                    while (currentParentFeature != null)
                                                    {
                                                        bool geometryChanged = false;
                                                        IPointCollection parentPointCollection = currentParentFeature.Shape as IPointCollection;

                                                        if (parentPointCollection != null)
                                                        {
                                                            for (int parentPointIndex = 0; parentPointIndex < parentPointCollection.PointCount; parentPointIndex++)
                                                            {
                                                                IPoint currentTestPoint = parentPointCollection.get_Point(parentPointIndex);

                                                                if (currentTestPoint.ID.Equals(deleteNodeID))
                                                                {
                                                                    geometryChanged = true;
                                                                    parentPointCollection.RemovePoints(parentPointIndex, 1);
                                                                }
                                                            }
                                                        }

                                                        if (geometryChanged)
                                                        {
                                                            currentParentFeature.Shape = parentPointCollection as IGeometry;

                                                            //if (trackChanges == false)
                                                            //{
                                                            //    if (osmTrackChangesFieldIndex > -1)
                                                            //    {
                                                            //        currentParentFeature.set_Value(osmTrackChangesFieldIndex, 0);
                                                            //    }
                                                            //}

                                                            // persist the changes back to the database
                                                            currentParentFeature.Store();
                                                        }

                                                        currentParentFeature = parentUpdateFeatureCursor.NextFeature();
                                                    }
                                                }
                                            }
                                            #endregion

                                            foundFeature.Delete();
                                        }
                                    }
                                } // for each of the missing nodes
                            } // missing nodes count larger than 0 
                        }  // geometry type test
                    } // IRelationOp::Equal test
                    else
                    {
                        // This means the shape is only different by vertex IDs or some other metadata
                        //   so reset the shape to the original geometry to get back the IDs
                        changeFeature.Shape = oldGeometry;
                    }
                } // shape.changed
            }
            catch { }

            int currentFeatureOSMIDIndex = changeFeature.Fields.FindField("OSMID");
            int currentFeatureVersionIndex = changeFeature.Fields.FindField("osmversion");
            long currentFeatureOSMID = 0;
            if (currentFeatureOSMIDIndex > -1)
            {
                currentFeatureOSMID = ReadAttributeValueAsLong(changeFeature, currentFeatureOSMIDIndex) ?? 0;

                // If the OSMID is null, we're probably hitting a premature feature service attribute edit
                //   so reset the OSMID to the original value
                if (currentFeatureOSMID == 0)
                {
                    object objID = ((IRowChanges)changeFeature).get_OriginalValue(currentFeatureOSMIDIndex);
                    if ((objID != null) && (objID != DBNull.Value))
                    {
                        long origOSMID = Convert.ToInt64(objID);
                        if (origOSMID != 0)
                            changeFeature.set_Value(currentFeatureOSMIDIndex, origOSMID);
                    }
                }
            }

            int currentFeatureVersion = -1;
            if (currentFeatureVersionIndex > -1)
            {
                currentFeatureVersion = ReadAttributeValueAsInt(changeFeature, currentFeatureVersionIndex) ?? -1;
            }

            if (m_bypassOSMChangeDetection == false)
            {
                // if the ids of the orginal and the changed feature geometry are the same and no attributes have changed then we don't need to
                // issue a change request against the OSM server
                if (hasOSMRelevantChanges((IRowChanges)changeFeature))
                {
                    LogOSMAction(revisionTable, "modify", determineOSMTypeByClassNameAndGeometry(((IDataset)changeFeature.Class).Name, changeFeature.Shape), ((IDataset)changeFeature.Class).Name, currentFeatureOSMID, currentFeatureVersion, -1, null);
                }
            }
        }

        /// <summary>
        /// check to see if a point was initially coincident with a polygon or line geometry 
        /// -- create needed vertices to fully represent the line or polygon
        /// </summary>
        /// <param name="changeFeature"></param>
        //private void CheckforMovingAwayFromFeature(IFeature changeFeature, bool trackChanges)
        //{

        //    // if the feature is not supposed to tracked then we are either handling the loading of diff files
        //    // since diff files have explictly expressed knowledge of the changes for OpenStreetMap there is no need to 
        //    // detect effects on connected entities as the diff itself will tell us
        //    if (trackChanges == false)
        //        return;

        //    IFeatureClass lineFeatureClass = findMatchingFeatureClass(changeFeature, esriGeometryType.esriGeometryPolyline);


        //    // the original shape is provided in the current map projection which might be different from the feature class projection
        //    // hence the re-project
        //    IPoint oldPointGeometry = ((IFeatureChanges)changeFeature).OriginalShape as IPoint;
        //    oldPointGeometry.Project(changeFeature.Shape.SpatialReference);

        //    ISpatialFilter searchSpatialFilter = new SpatialFilterClass();
        //    searchSpatialFilter.Geometry = oldPointGeometry;
        //    searchSpatialFilter.SpatialRel = esriSpatialRelEnum.esriSpatialRelIntersects;
        //    searchSpatialFilter.GeometryField = lineFeatureClass.ShapeFieldName;

        //    int supportElementFieldIndex = changeFeature.Fields.FindField("osmSupportingElement");
        //    int osmIDFieldIndex = changeFeature.Fields.FindField("OSMID");
        //    int wayRefCountFieldIndex = changeFeature.Fields.FindField("wayRefCount");
        //    int osmVersionFieldIndex = changeFeature.Fields.FindField("osmversion");
        //    int trackChangesFieldIndex = changeFeature.Fields.FindField("osmTrackChanges");

        //    int lineTrackChangesFieldIndex = lineFeatureClass.Fields.FindField("osmTrackChanges");

        //    // find lines that were coincident with the previous point location
        //    using (ComReleaser comReleaser = new ComReleaser())
        //    {
        //        IFeatureCursor lineSearchCursor = lineFeatureClass.Search(searchSpatialFilter, false);
        //        comReleaser.ManageLifetime(lineSearchCursor);

        //        IFeature foundFeature = lineSearchCursor.NextFeature();

        //        // if we indeed find such a line geometry, ensure that we add a new supporting vertex in the position of the vertex (point/node)
        //        // that was just changed
        //        if (foundFeature != null)
        //        {
        //            IPointCollection pointCollection = foundFeature.Shape as IPointCollection;
        //            IRelationalOperator relationOperator = oldPointGeometry as IRelationalOperator;
        //            IFeatureClass pointFeatureClass = changeFeature.Class as IFeatureClass;

        //            for (int pointIndex = 0; pointIndex < pointCollection.PointCount; pointIndex++)
        //            {
        //                if (relationOperator.Equals((IGeometry)pointCollection.get_Point(pointIndex)))
        //                {
        //                    IFeature newSupportPointFeature = pointFeatureClass.CreateFeature();

        //                    IPoint newPoint = ((IClone)pointCollection.get_Point(pointIndex)).Clone() as IPoint;

        //                    IPointIDAware newPointIDAware = newPoint as IPointIDAware;
        //                    newPointIDAware.PointIDAware = true;

        //                    if (_ExtensionVersion == 1)
        //                        newPoint.ID = Convert.ToInt32(_TemporaryIndex);
        //                    else if (_ExtensionVersion == 2)
        //                        newPoint.ID = newSupportPointFeature.OID;

        //                    // remember to update the geometry of the line itself
        //                    pointCollection.UpdatePoint(pointIndex, newPoint);

        //                    newSupportPointFeature.Shape = newPoint;

        //                    if (supportElementFieldIndex > -1)
        //                    {
        //                        newSupportPointFeature.set_Value(supportElementFieldIndex, "yes");
        //                    }
        //                    if (osmIDFieldIndex > -1)
        //                    {
        //                        if (_ExtensionVersion == 1)
        //                            newSupportPointFeature.set_Value(osmIDFieldIndex, Convert.ToInt32(_TemporaryIndex));
        //                        else if (_ExtensionVersion == 2)
        //                            newSupportPointFeature.set_Value(osmIDFieldIndex, Convert.ToString(_TemporaryIndex));
        //                    }

        //                    if (wayRefCountFieldIndex > -1)
        //                    {
        //                        newSupportPointFeature.set_Value(wayRefCountFieldIndex, 1);
        //                    }

        //                    if (osmVersionFieldIndex > -1)
        //                    {
        //                        newSupportPointFeature.set_Value(osmVersionFieldIndex, 1);
        //                    }

        //                    _TemporaryIndex = _TemporaryIndex - 1;

        //                    //if (trackChangesFieldIndex > -1)
        //                    //{
        //                    //    if (trackChanges == false)
        //                    //    {
        //                    //        newSupportPointFeature.set_Value(trackChangesFieldIndex, 1);
        //                    //    }
        //                    //}

        //                    // save the newly created point
        //                    newSupportPointFeature.Store();

        //                    //if (trackChanges == false)
        //                    //{
        //                    //    if (lineTrackChangesFieldIndex > -1)
        //                    //    {
        //                    //        foundFeature.set_Value(lineTrackChangesFieldIndex, 1);
        //                    //    }
        //                    //}

        //                    // save the updated line geometry
        //                    foundFeature.Shape = pointCollection as IGeometry;
        //                    foundFeature.Store();

        //                }
        //            }

        //            foundFeature = lineSearchCursor.NextFeature();
        //        }
        //    }

        //    IFeatureClass polygonFeatureClass = findMatchingFeatureClass(changeFeature, esriGeometryType.esriGeometryPolygon);
        //    int polygonTrackChangesFieldIndex = polygonFeatureClass.Fields.FindField("osmTrackChanges");

        //    ISpatialFilter searchPolygonSpatialFilter = new SpatialFilterClass();
        //    searchPolygonSpatialFilter.Geometry = oldPointGeometry;
        //    searchPolygonSpatialFilter.SpatialRel = esriSpatialRelEnum.esriSpatialRelTouches;
        //    searchPolygonSpatialFilter.GeometryField = polygonFeatureClass.ShapeFieldName;

        //    using (ComReleaser comReleaser = new ComReleaser())
        //    {
        //        IFeatureCursor polygonSearchCursor = polygonFeatureClass.Search(searchSpatialFilter, false);
        //        comReleaser.ManageLifetime(polygonSearchCursor);

        //        IFeature foundFeature = polygonSearchCursor.NextFeature();

        //        if (foundFeature != null)
        //        {
        //            IPointCollection pointCollection = foundFeature.Shape as IPointCollection;

        //            IRelationalOperator relationOperator = oldPointGeometry as IRelationalOperator;
        //            IFeatureClass pointFeatureClass = changeFeature.Class as IFeatureClass;

        //            for (int pointIndex = 0; pointIndex < pointCollection.PointCount; pointIndex++)
        //            {
        //                if (relationOperator.Equals((IGeometry)pointCollection.get_Point(pointIndex)))
        //                {
        //                    IFeature newSupportPointFeature = pointFeatureClass.CreateFeature();

        //                    IPoint newPoint = ((IClone)pointCollection.get_Point(pointIndex)).Clone() as IPoint;

        //                    IPointIDAware newPointIDAware = newPoint as IPointIDAware;
        //                    newPointIDAware.PointIDAware = true;

        //                    if (_ExtensionVersion == 1)
        //                        newPoint.ID = Convert.ToInt32(_TemporaryIndex);
        //                    else if (_ExtensionVersion == 2)
        //                        newPoint.ID = newSupportPointFeature.OID;


        //                    pointCollection.UpdatePoint(pointIndex, newPoint);

        //                    newSupportPointFeature.Shape = newPoint;

        //                    if (supportElementFieldIndex > -1)
        //                    {
        //                        newSupportPointFeature.set_Value(supportElementFieldIndex, "yes");
        //                    }
        //                    if (osmIDFieldIndex > -1)
        //                    {
        //                        if (_ExtensionVersion == 1)
        //                            newSupportPointFeature.set_Value(osmIDFieldIndex, Convert.ToInt32(_TemporaryIndex));
        //                        else if (_ExtensionVersion == 2)
        //                            newSupportPointFeature.set_Value(osmIDFieldIndex, Convert.ToString(_TemporaryIndex));
        //                    }

        //                    if (wayRefCountFieldIndex > -1)
        //                    {
        //                        newSupportPointFeature.set_Value(wayRefCountFieldIndex, 1);
        //                    }

        //                    if (osmVersionFieldIndex > -1)
        //                    {
        //                        newSupportPointFeature.set_Value(osmVersionFieldIndex, 1);
        //                    }

        //                    _TemporaryIndex = _TemporaryIndex - 1;

        //                    //if (trackChangesFieldIndex > -1)
        //                    //{
        //                    //    if (trackChanges == false)
        //                    //    {
        //                    //        newSupportPointFeature.set_Value(trackChangesFieldIndex, 1);
        //                    //    }
        //                    //}

        //                    // save the newly created point
        //                    newSupportPointFeature.Store();

        //                    //if (trackChanges == false)
        //                    //{
        //                    //    if (polygonTrackChangesFieldIndex > -1)
        //                    //    {
        //                    //        foundFeature.set_Value(polygonTrackChangesFieldIndex, 1);
        //                    //    }
        //                    //}

        //                    // save the changes to the polygon geometry
        //                    foundFeature.Shape = pointCollection as IGeometry;
        //                    foundFeature.Store();

        //                }
        //            }

        //            foundFeature = polygonSearchCursor.NextFeature();
        //        }
        //    }
        //}

        private string ReadAttributeValueAsString(IRow row, int fieldIndex, string defaultValue = null)
        {
            string attributeValue = String.Empty;

            if (String.IsNullOrEmpty(defaultValue) == false)
            {
                attributeValue = defaultValue;
            }

            if (row == null)
            {
                return attributeValue;
            }

            if (fieldIndex == -1)
            {
                return attributeValue;
            }

            try
            {
                attributeValue = Convert.ToString(row.get_Value(fieldIndex));
            }
            catch { }

            return attributeValue;
        }

        private long? ReadAttributeValueAsLong(IRow row, int fieldIndex)
        {
            long? attributeValue = null;

            if (row == null)
            {
                return attributeValue;
            }

            if (fieldIndex == -1)
            {
                return attributeValue;
            }

            try
            {
                attributeValue = Convert.ToInt64(row.get_Value(fieldIndex));
            }
            catch { }

            return attributeValue;
        }

        private int? ReadAttributeValueAsInt(IRow row, int fieldIndex)
        {
            int? attributeValue = null;

            if (row == null)
            {
                return attributeValue;
            }

            if (fieldIndex == -1)
            {
                return attributeValue;
            }

            try
            {
                attributeValue = Convert.ToInt32(row.get_Value(fieldIndex));
            }
            catch { }

            return attributeValue;
        }

        private double? ReadAttributeValueAsDouble(IRow row, int fieldIndex)
        {
            double? attributeValue = null;

            if (row == null)
            {
                return attributeValue;
            }

            if (fieldIndex == -1)
            {
                return attributeValue;
            }

            try
            {
                attributeValue = Convert.ToDouble(row.get_Value(fieldIndex));
            }
            catch { }

            return attributeValue;
        }

        private DateTime? ReadAttributeValueAsDateTime(IRow row, int fieldIndex)
        {
            DateTime? attributeValue = null;

            if (row == null)
            {
                return attributeValue;
            }

            if (fieldIndex == -1)
            {
                return attributeValue;
            }

            try
            {
                attributeValue = Convert.ToDateTime(row.get_Value(fieldIndex));
            }
            catch { }

            return attributeValue;
        }


        [Obsolete("Determining the OSM type by feature class alone has been deprecated. Please use determineOSMTypeByClassNameAndGeometry instead.")]
        private string determineOSMTypeByClassName(string featureClassName)
        {
            string osmTypeString = "";

            if (String.IsNullOrEmpty(featureClassName))
            {
                return osmTypeString;
            }

            if (featureClassName.Contains("_osm_pt"))
            {
                osmTypeString = "node";
            }
            else if (featureClassName.Contains("_osm_relation"))
            {
                osmTypeString = "relation";
            }
            else
            {
                osmTypeString = "way";
            }

            return osmTypeString;
        }

        private string determineOSMTypeByClassNameAndGeometry(string featureClassName, IGeometry geometryToTest)
        {
            string osmTypeString = "";

            if (String.IsNullOrEmpty(featureClassName))
            {
                return osmTypeString;
            }

            if (geometryToTest == null)
            {
                return osmTypeString;
            }

            if (geometryToTest.IsEmpty)
            {
                return osmTypeString;
            }

            if (featureClassName.Contains("_osm_pt"))
            {
                osmTypeString = "node";
            }
            else if (featureClassName.Contains("_osm_relation"))
            {
                osmTypeString = "relation";
            }
            else
            {
                osmTypeString = "way";
            }

            IGeometryCollection geometryCollection = geometryToTest as IGeometryCollection;

            if (geometryCollection != null)
            {
                if (geometryCollection.GeometryCount > 1)
                {
                    osmTypeString = "relation";
                }
            }

            return osmTypeString;
        }

        private bool hasOSMRelevantChanges(IRowChanges inputRowChanges)
        {
            bool equalityCheck = false;
            bool attributesChanged = false;
            int geometryCount = 1;

            try
            {
                if (inputRowChanges == null)
                    return equalityCheck;

                List<int> sourceIDList = null;
                List<int> differenceIDList = null;

                for (int attributeIndex = 0; attributeIndex < ((IRow)inputRowChanges).Fields.FieldCount; attributeIndex++)
                {
                    if (inputRowChanges.get_ValueChanged(attributeIndex) == true)
                    {
                        if (((IRow)inputRowChanges).Fields.get_Field(attributeIndex).Type == esriFieldType.esriFieldTypeGeometry)
                        {
                            sourceIDList = new List<int>();
                            differenceIDList = new List<int>();

                            IPoint sourcePoint = new PointClass();
                            int sourcePartIndex = -1;
                            int sourceVertexIndex = -1;

                            IGeometry oldGeometry = inputRowChanges.get_OriginalValue(attributeIndex) as IGeometry;
                            IPointCollection oldPointCollection = oldGeometry as IPointCollection;

                            // if the geometry doesn't support IPointCollection, then don't worry about it
                            if (oldPointCollection == null)
                                continue;

                            IEnumVertex sourceVertexCollection = oldPointCollection.EnumVertices as IEnumVertex;

                            sourceVertexCollection.QueryNext(sourcePoint, out sourcePartIndex, out sourceVertexIndex);

                            while (sourceVertexIndex > -1)
                            {
                                sourceIDList.Add(sourcePoint.ID);
                                sourceVertexCollection.QueryNext(sourcePoint, out sourcePartIndex, out sourceVertexIndex);
                            }

                            IEnumVertex differenceVertexCollection = ((IPointCollection)((IRow)inputRowChanges).get_Value(attributeIndex)).EnumVertices as IEnumVertex;
                            IPoint differencePoint = new PointClass();
                            int differencePartIndex = -1;
                            int differenceVertexIndex = -1;

                            differenceVertexCollection.QueryNext(differencePoint, out differencePartIndex, out differenceVertexIndex);

                            while (differenceVertexIndex > -1)
                            {
                                differenceIDList.Add(differencePoint.ID);
                                differenceVertexCollection.QueryNext(differencePoint, out differencePartIndex, out differenceVertexIndex);
                            }

                            IGeometryCollection geometryCollection = ((IFeature)inputRowChanges).Shape as IGeometryCollection;

                            if (geometryCollection != null)
                            {
                                geometryCount = geometryCollection.GeometryCount;
                            }
                        }
                        else if (((IRow)inputRowChanges).Fields.get_Field(attributeIndex).Name.Equals("osmTags"))
                        {
                            attributesChanged = true;
                        }
                        //else if (((IRow)inputRowChanges).Fields.get_Field(attributeIndex).Name.Contains("osm_"))
                        //{
                        //    attributesChanged = true;
                        //}
                    }
                }

                // in case the attributes are not changed  - check for the "equality" of the geometries based on the IDs
                // equal means that there are the same number Ids in the same sequence
                if (attributesChanged == false)
                {
                    if (geometryCount == 1)
                    {
                        if ((sourceIDList != null) && differenceIDList != null)
                        {
                            equalityCheck = sourceIDList.SequenceEqual(differenceIDList);
                        }
                        else
                        {
                            // in this case we don't have a check in place for the geometry 
                            // so we are assuming that they are equal
                            equalityCheck = true;
                        }
                    }
                    else
                    {
                        // check the equality of members
                        equalityCheck = true;
                    }
                }
            }
            catch
            {
            }

            bool doChangesExist = true;

            if (equalityCheck == true && attributesChanged == false)
            {
                doChangesExist = false;
            }

            return doChangesExist;
        }

        private List<int> findDeletedNodeIDs(IGeometry originalGeometry, IGeometry updatedGeometry)
        {
            List<int> deletedNodeIDs = new List<int>();

            try
            {
                IPointCollection originalPointCollection = originalGeometry as IPointCollection;
                IPointCollection updatedPointcollection = updatedGeometry as IPointCollection;

                for (int originalIndex = 0; originalIndex < originalPointCollection.PointCount; originalIndex++)
                {
                    bool foundIndex = false;

                    for (int updatedIndex = 0; updatedIndex < updatedPointcollection.PointCount; updatedIndex++)
                    {
                        if (originalPointCollection.get_Point(originalIndex).ID == updatedPointcollection.get_Point(updatedIndex).ID)
                        {
                            foundIndex = true;
                            break;
                        }
                    }

                    if (foundIndex == false)
                    {
                        deletedNodeIDs.Add(originalPointCollection.get_Point(originalIndex).ID);
                    }
                }
            }

            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
            }

            return deletedNodeIDs;
        }


        /// <summary>Sets the temporary index for pre-commited osmids</summary>
        /// <remarks>Reads the lowest available value from the given revision table</remarks>
        private void SetTemporaryIndex(IObject testObject, ITable revisionTable)
        {
            _temporaryIndex = 0;

            TempIndicies.TryGetValue(_revisionTableName, out _temporaryIndex);
            if (_temporaryIndex >= 0)
            {
                if (_ExtensionVersion == 1)
                    _temporaryIndex = determineMinTemporaryOSMID(testObject, _temporaryIndex);
                else if (_ExtensionVersion == 2)
                    _temporaryIndex = determineMinTemporaryOSMID(revisionTable, _temporaryIndex);

                TempIndicies[_revisionTableName] = _temporaryIndex;
            }
        }

        private void DecrementTemporaryIndex()
        {
            --_temporaryIndex;
            TempIndicies[_revisionTableName] = _temporaryIndex;
        }

        /// <summary>
        /// Determines the new temporary ID for a newly created feature. This should be consistent throughout the 
        /// point, line, and polygon feature class. Relations and revisions are checked as well - even though for version 1.1 there is currently
        /// no editing experience for those entities. This might change for later versions.
        /// </summary>
        /// <param name="testObject">Object to be created. It is used to determine the location of the feature classes.</param>
        /// <param name="currentID">Currently known lowest ID.</param>
        /// <returns>The returned new ID is guaranteed to be at least one less than the currentID.</returns>
        private long determineMinTemporaryOSMID(IObject testObject, long currentID)
        {
            long newTempID = 0;
            int catchCounter = 0;

            try
            {
                int featureClassBasePosition = ((IDataset)testObject.Class).Name.IndexOf("_osm_");
                if (featureClassBasePosition > -1)
                {
                    string featureClassBaseName = ((IDataset)testObject.Class).Name.Substring(0, featureClassBasePosition);
                    IFeatureWorkspace currentFeatureWorkSpace = ((IDataset)testObject.Class).Workspace as IFeatureWorkspace;
                    IFeatureClass pointFeatureClass = currentFeatureWorkSpace.OpenFeatureClass(featureClassBaseName + "_osm_pt");
                    newTempID = getLowestOSMID(currentID, ref catchCounter, (ITable)pointFeatureClass, "OSMID");

                    IFeatureClass lineFeatureClass = currentFeatureWorkSpace.OpenFeatureClass(featureClassBaseName + "_osm_ln");
                    newTempID = getLowestOSMID(newTempID, ref catchCounter, (ITable)lineFeatureClass, "OSMID");

                    IFeatureClass polygonFeatureClass = currentFeatureWorkSpace.OpenFeatureClass(featureClassBaseName + "_osm_ply");
                    newTempID = getLowestOSMID(newTempID, ref catchCounter, (ITable)polygonFeatureClass, "OSMID");

                    ITable relationTable = currentFeatureWorkSpace.OpenTable(featureClassBaseName + "_osm_relation");
                    newTempID = getLowestOSMID(newTempID, ref catchCounter, relationTable, "OSMID");

                    ITable revisionTable = currentFeatureWorkSpace.OpenTable(featureClassBaseName + "_osm_revision");
                    newTempID = getLowestOSMID(newTempID, ref catchCounter, revisionTable, "osmoldid");
                }
            }
            catch { }
            finally
            {
                newTempID = newTempID - 1 - catchCounter;
            }

            return newTempID;
        }

        private long determineMinTemporaryOSMID(ITable revisionTable, long currentID)
        {
            long newTempID = 0;
            int catchCounter = 0;

            try
            {
                newTempID = getLowestOSMID(ref catchCounter, revisionTable, "osmoldid");
            }
            catch { }
            finally
            {
                newTempID = newTempID - 1 - catchCounter;
            }

            return newTempID;
        }

        private long getLowestOSMID(ref int catchCounter, ITable inputTable, string fieldName)
        {
            int osmIDFieldIndex = inputTable.Fields.FindField(fieldName);
            long newTempID = 0;

            if (osmIDFieldIndex > -1)
            {
                using (ComReleaser comReleaser = new ComReleaser())
                {
                    ICursor searchCursor = inputTable.Search(null, true);
                    comReleaser.ManageLifetime(searchCursor);

                    long currentID = 10000;

                    IRow row = searchCursor.NextRow();

                    while (row != null)
                    {
                        try
                        {
                            object osmIDValue = row.get_Value(osmIDFieldIndex);
                            if (osmIDValue != DBNull.Value)
                            {
                                currentID = Convert.ToInt64(row.get_Value(osmIDFieldIndex));
                            }

                            newTempID = Math.Min(newTempID, currentID);
                            row = searchCursor.NextRow();
                        }
                        catch
                        {
                            catchCounter = catchCounter + 1;
                        }
                    }
                }
            }

            return newTempID;
        }

        private long getLowestOSMID(long currentID, ref int catchCounter, ITable inputTable, string fieldName)
        {
            int osmIDFieldIndex = inputTable.Fields.FindField(fieldName);
            long newTempID = -1;

            if (osmIDFieldIndex > -1)
            {
                using (ComReleaser comReleaser = new ComReleaser())
                {
                    IQueryFilter queryFilter = new QueryFilterClass();
                    queryFilter.WhereClause = inputTable.SqlIdentifier(fieldName) + " < " + currentID.ToString();
                    queryFilter.AddField(fieldName);

                    ICursor searchCursor = inputTable.Search(queryFilter, true);
                    comReleaser.ManageLifetime(searchCursor);

                    IRow row = searchCursor.NextRow();

                    while (row != null)
                    {
                        try
                        {
                            newTempID = ReadAttributeValueAsLong(row, osmIDFieldIndex) ?? -1;

                            currentID = Math.Min(newTempID, currentID);
                            row = searchCursor.NextRow();
                        }
                        catch
                        {
                            catchCounter = catchCounter + 1;
                        }
                    }
                }
            }

            return currentID;
        }

        private void LogOSMAction(ITable revisionTable, string osmAction, string osmElementType, string sourceFCName, long elementID, int osmVersion, int osmChangeSet, IPoint deletePoint)
        {
            int revActionFieldIndex = revisionTable.Fields.FindField("osmaction");
            int revElementTypeFieldIndex = revisionTable.Fields.FindField("osmelementtype");
            int revFCNameFieldIndex = revisionTable.Fields.FindField("sourcefcname");
            int revOldIdFieldIndex = revisionTable.Fields.FindField("osmoldid");
            int revNewIdFieldIndex = revisionTable.Fields.FindField("osmnewid");
            int revVersionFieldIndex = revisionTable.Fields.FindField("osmversion");
            int revStatusFieldIndex = revisionTable.Fields.FindField("osmstatus");
            int revChangeSetFieldIndex = revisionTable.Fields.FindField("osmchangeset");
            int revLatitudeFieldIndex = revisionTable.Fields.FindField("osmlat");
            int revLongitudeFieldIndex = revisionTable.Fields.FindField("osmlon");

            // if the osm version is -1 then don't log the event
            // this is a case of an intermediate create/merge/delete action
            if (osmVersion < 0)
            {
                return;
            }

            IQueryFilter queryFilter = new QueryFilterClass();

            if ((revOldIdFieldIndex > -1) && (revNewIdFieldIndex > -1))
            {
                SQLFormatter sqlFormatter = new SQLFormatter(revisionTable);

                // if there is a "create" action then we don't need to add another update (or create)
                queryFilter.WhereClause = revisionTable.WhereClauseByExtensionVersion(elementID, "osmoldid", _ExtensionVersion) +
                    " AND " + sqlFormatter.SqlIdentifier("osmnewid") + " IS NULL";

                using (ComReleaser comReleaser = new ComReleaser())
                {
                    ICursor searchCursor = revisionTable.Search(queryFilter, false);
                    comReleaser.ManageLifetime(searchCursor);

                    IRow searchRow = searchCursor.NextRow();

                    if (searchRow != null)
                    {
                        string revisionElementType = ReadAttributeValueAsString(searchRow, revElementTypeFieldIndex) ?? String.Empty;

                        if (osmAction.Equals("delete") && elementID < 0 && osmElementType.Equals(revisionElementType))
                        {
                            // if there is a delete request for a feature that hasn't been update yet (osmID < 0)
                            // then delete the request altogether
                            searchRow.Delete();
                        }
                        else if (osmAction.Equals("delete") && elementID > 0)
                        {
                            // if the feature has already existed with an update request 
                            // then change the existing request to delete
                            searchRow.set_Value(revActionFieldIndex, "delete");
                            searchRow.set_Value(revStatusFieldIndex, DBNull.Value);
                            searchRow.set_Value(revChangeSetFieldIndex, osmChangeSet);

                            searchRow.Store();
                        }

                        //if (revisionTable != null)
                        //{
                        //    ISchemaLock revisionSchemaLock = revisionTable as ISchemaLock;

                        //    if (revisionSchemaLock != null)
                        //    {
                        //        revisionSchemaLock.ChangeSchemaLock(esriSchemaLock.esriSharedSchemaLock);
                        //    }
                        //}

                        return;
                    }
                }
            }

            // at this point this a delete request for some that the server doesn't even know about yet - hence no tracking required
            if (osmAction.Equals("delete") && elementID < 0)
            {
                //if (revisionTable != null)
                //{
                //    ISchemaLock revisionSchemaLock = revisionTable as ISchemaLock;

                //    if (revisionSchemaLock != null)
                //    {
                //        revisionSchemaLock.ChangeSchemaLock(esriSchemaLock.esriSharedSchemaLock);
                //    }
                //}

                return;
            }

            IRow row = revisionTable.CreateRow();
            if (revActionFieldIndex > -1)
            {
                row.set_Value(revActionFieldIndex, osmAction);
            }
            if (revElementTypeFieldIndex > -1)
            {
                row.set_Value(revElementTypeFieldIndex, osmElementType);
            }
            if (revFCNameFieldIndex > -1)
            {
                row.set_Value(revFCNameFieldIndex, sourceFCName);
            }
            if (revOldIdFieldIndex > -1)
            {
                if (_ExtensionVersion == 1)
                    row.set_Value(revOldIdFieldIndex, Convert.ToInt32(elementID));
                else if (_ExtensionVersion == 2)
                    row.set_Value(revOldIdFieldIndex, Convert.ToString(elementID));
            }

            row.set_Value(revChangeSetFieldIndex, osmChangeSet);

            if (osmAction.Equals("delete") && osmElementType.Equals("node", StringComparison.InvariantCultureIgnoreCase))
            {
                if (deletePoint != null)
                {
                    deletePoint.Project(m_wgs84);
                    row.set_Value(revLatitudeFieldIndex, deletePoint.Y);
                    row.set_Value(revLongitudeFieldIndex, deletePoint.X);
                }
            }


            if (revVersionFieldIndex > -1)
            {
                row.set_Value(revVersionFieldIndex, osmVersion);
            }

            //change a potential modify into a delete
            /////

            row.Store();
        }

        /// <summary>
        /// This routine will find and open the matching revision table for the input object. <para>The revision table is used to capture and store
        /// the editing deltas from the geodatabase edits and is the foundation for the deltas submitted back to the OpenStreetMap server instance.</para>
        /// </summary>
        /// <param name="inspectionFeature">Feature/Object to be changed.</param>
        /// <returns>Matching revision table for the input feature/object. Use this table instance to log the changes for synchronization with the
        /// OpenStreetMap server instance.</returns>
        private ITable findRevisionTable(IObject inspectionFeature)
        {
            ITable revisionTable = null;

            try
            {
                string inspectionFeatureClassName = ((IDataset)inspectionFeature.Class).Name;

                int osmDelimiterPosition = inspectionFeatureClassName.IndexOf("_osm_");
                string featureClassBaseName = inspectionFeatureClassName.Substring(0, osmDelimiterPosition);

                IFeatureWorkspace featureWorkspace = ((IDataset)inspectionFeature.Class).Workspace as IFeatureWorkspace;
                revisionTable = featureWorkspace.OpenTable(featureClassBaseName + "_osm_revision");
            }
            catch
            { }

            return revisionTable;
        }

        private ITable findRelationTable(IFeature inspectionFeature)
        {
            ITable relationTable = null;

            try
            {
                string inspectionFeatureClassName = ((IDataset)inspectionFeature.Class).Name;

                int osmDelimiterPosition = inspectionFeatureClassName.IndexOf("_osm_");
                string featureClassBaseName = inspectionFeatureClassName.Substring(0, osmDelimiterPosition);

                IFeatureWorkspace featureWorkspace = ((IDataset)inspectionFeature.Class).Workspace as IFeatureWorkspace;
                relationTable = featureWorkspace.OpenTable(featureClassBaseName + "_osm_relation");
            }
            catch { }

            return relationTable;
        }

        private void UpdateMergeNode(IFeature targetFeature, ITable revisionTable)
        {
            if (targetFeature == null)
            {
                throw new ArgumentNullException("targetFeature");
            }

            if (targetFeature.Shape.IsEmpty == true)
            {
                return;
            }

            if (revisionTable == null)
            {
                throw new ArgumentNullException("revisionTable");
            }

            // TODO: this routine needs to be checked if an existing point feature snaps on top of another point feature......
            // a merge and a delete need to happen - so far they are not

            int osmIDFieldIndex = targetFeature.Fields.FindField("OSMID");
            int supportElementFieldIndex = targetFeature.Fields.FindField("osmSupportingElement");
            int wayRefCountFieldIndex = targetFeature.Fields.FindField("wayRefCount");
            int osmVersionFieldIndex = targetFeature.Fields.FindField("osmversion");
            int osmTrackFeatureChangesFieldIndex = targetFeature.Fields.FindField("osmTrackChanges");

            int revActionFieldIndex = revisionTable.Fields.FindField("osmaction");
            int revElementTypeFieldIndex = revisionTable.Fields.FindField("osmelementtype");
            int revFCNameFieldIndex = revisionTable.Fields.FindField("sourcefcname");
            int revOldIdFieldIndex = revisionTable.Fields.FindField("osmoldid");

            //bool trackFeatureChanges = true;

            if (_ExtensionVersion == 2)
            {
                // check if the geometry ID and the ObjectID are the same
                IPoint pointGeometry = targetFeature.Shape as IPoint;

                if (pointGeometry != null)
                {
                    bool geometryChanged = false;
                    IPointIDAware pointIDAware = pointGeometry as IPointIDAware;

                    if (pointIDAware.PointIDAware)
                    {
                        if (pointGeometry.ID != targetFeature.OID)
                        {
                            pointGeometry.ID = targetFeature.OID;
                            geometryChanged = true;
                        }
                    }
                    else
                    {
                        pointIDAware.PointIDAware = true;
                        pointGeometry.ID = targetFeature.OID;
                        geometryChanged = true;
                    }

                    if (geometryChanged)
                        targetFeature.Shape = pointGeometry;
                }
            }


            //if (osmTrackFeatureChangesFieldIndex > -1)
            //{
            //    try
            //    {
            //        int trackIndicator = Convert.ToInt32(targetFeature.get_Value(osmTrackFeatureChangesFieldIndex));

            //        if (trackIndicator != 0)
            //        {
            //            trackFeatureChanges = false;
            //        }
            //    }
            //    catch { }
            //}

            object osmIDValue = null;

            using (ComReleaser comReleaser = new ComReleaser())
            {
                osmIDValue = targetFeature.get_Value(osmIDFieldIndex);

                long osmID = 0;
                int osmVersion = 0;

                if (osmIDValue == DBNull.Value)
                {
                    assignBasicMetadata(targetFeature, osmIDFieldIndex, wayRefCountFieldIndex, osmVersionFieldIndex, out osmID, out osmVersion);

                    if (m_bypassOSMChangeDetection == false)
                    {
                        LogOSMAction(revisionTable, "create", "node", ((IDataset)targetFeature.Class).Name, osmID, osmVersion, -1, null);
                    }

                    //if (osmTrackFeatureChangesFieldIndex > -1)
                    //{
                    //    targetFeature.set_Value(osmTrackFeatureChangesFieldIndex, 0);
                    //}
                    return;
                }
                else
                {
                    osmID = Convert.ToInt64(osmIDValue);
                }

                IFeatureClass fcTarget = (IFeatureClass)targetFeature.Class;

                // let's first check if a node with the same osmID already exists
                IQueryFilter osmIDQueryFilter = new QueryFilterClass();
                osmIDQueryFilter.WhereClause = fcTarget.WhereClauseByExtensionVersion(osmID, "OSMID", _ExtensionVersion);

                IFeatureCursor osmIDCursor = fcTarget.Search(osmIDQueryFilter, false);
                comReleaser.ManageLifetime(osmIDCursor);

                IFeature matchingOSMIDFeature = osmIDCursor.NextFeature();
                if (matchingOSMIDFeature == null)
                {
                    assignBasicMetadata(targetFeature, osmIDFieldIndex, wayRefCountFieldIndex, osmVersionFieldIndex, out osmID, out osmVersion);

                    if (m_bypassOSMChangeDetection == false)
                    {
                        LogOSMAction(revisionTable, "create", "node", ((IDataset)targetFeature.Class).Name, osmID, osmVersion, -1, null);
                    }
                }
                else
                {
                    ISpatialFilter spatialFilter = new SpatialFilterClass();

                    IBufferConstruction bc = new BufferConstructionClass();
                    IGeometry buffer = bc.Buffer(targetFeature.Shape, ((ISpatialReferenceTolerance)targetFeature.Shape.SpatialReference).XYTolerance * 1.1);

                    spatialFilter.SearchOrder = esriSearchOrder.esriSearchOrderSpatial;
                    spatialFilter.Geometry = buffer.Envelope;
                    spatialFilter.GeometryField = ((IFeatureClass)targetFeature.Class).ShapeFieldName;
                    spatialFilter.SpatialRel = esriSpatialRelEnum.esriSpatialRelContains;

                    // we are looking for coincident points with a different ID
                    if (osmIDFieldIndex > -1)
                    {
                        if (osmIDValue == DBNull.Value)
                        {
                            spatialFilter.WhereClause = fcTarget.SqlIdentifier("OSMID") + " IS NOT NULL";
                        }
                        else
                        {
                            if (_ExtensionVersion == 1)
                                spatialFilter.WhereClause = fcTarget.SqlIdentifier("OSMID") + " <> " + Convert.ToInt32(osmIDValue).ToString();
                            else if (_ExtensionVersion == 2)
                                spatialFilter.WhereClause = fcTarget.SqlIdentifier("OSMID") + " <> '" + Convert.ToString(osmIDValue) + "'";
                        }
                    }

                    IFeatureCursor searchCursor = fcTarget.Search(spatialFilter, false);
                    comReleaser.ManageLifetime(searchCursor);

                    IFeature foundFeature = searchCursor.NextFeature();

                    if (foundFeature != null)
                    {
                        foundFeature = mergeOSMTags(foundFeature, targetFeature, true);
                        assignBasicMetadata(foundFeature, osmIDFieldIndex, wayRefCountFieldIndex, osmVersionFieldIndex, out osmID, out osmVersion);

                        if (supportElementFieldIndex > -1)
                        {
                            foundFeature.set_Value(supportElementFieldIndex, targetFeature.get_Value(supportElementFieldIndex));
                        }

                        //if (trackFeatureChanges == false)
                        //{
                        //    if (osmTrackFeatureChangesFieldIndex > -1)
                        //    {
                        //        foundFeature.set_Value(osmTrackFeatureChangesFieldIndex, 1);
                        //    }
                        //}

                        foundFeature.Store();

                        if (m_bypassOSMChangeDetection == false)
                        {
                            LogOSMAction(revisionTable, "modify", "node", ((IDataset)foundFeature.Class).Name, osmID, osmVersion, -1, null);
                        }

                        foundFeature = searchCursor.NextFeature();

                        targetFeature.Delete();
                    }
                    else
                    // this is the case when we have a OSM id assigned because it is handed to us from a line or polygon but the node 
                    // doesn't exist yet. We just need to log the node creation itself - maybe based on store or insert cursor
                    {
                        osmIDValue = targetFeature.get_Value(osmIDFieldIndex);
                        if (osmIDValue == DBNull.Value)
                        {
                            assignBasicMetadata(targetFeature, osmIDFieldIndex, wayRefCountFieldIndex, osmVersionFieldIndex, out osmID, out osmVersion);

                            // if a point feature has been explictly created it has to be an OSM feature
                            if (supportElementFieldIndex > -1)
                            {
                                targetFeature.set_Value(supportElementFieldIndex, "no");
                            }

                            if (m_bypassOSMChangeDetection == false)
                            {
                                LogOSMAction(revisionTable, "create", "node", ((IDataset)targetFeature.Class).Name, _temporaryIndex, 1, -1, null);
                            }

                            //if (osmTrackFeatureChangesFieldIndex > -1)
                            //{
                            //    targetFeature.set_Value(osmTrackFeatureChangesFieldIndex, 0);
                            //}

                            DecrementTemporaryIndex();
                        }
                        else
                        {
                            assignBasicMetadata(targetFeature, osmIDFieldIndex, wayRefCountFieldIndex, osmVersionFieldIndex, out osmID, out osmVersion);

                            if (m_bypassOSMChangeDetection == false)
                            {
                                if (osmID < 0)
                                {
                                    LogOSMAction(revisionTable, "create", "node", ((IDataset)targetFeature.Class).Name, osmID, osmVersion, -1, null);
                                }
                                else
                                {
                                    // the point geometry will change no matter what -- at this point we need to test further if the point has actually moved
                                    IFeatureChanges fcc = targetFeature as IFeatureChanges;
                                    if (fcc != null)
                                    {
                                        if (fcc.ShapeChanged)
                                        {
                                            IBufferConstruction pc = new BufferConstructionClass();
                                            // exapnd the xy tolerance by 50% for point to be considered equal
                                            IGeometry testBuffer = null;
                                            if (targetFeature.Shape.SpatialReference is IGeographicCoordinateSystem)
                                            {
                                                testBuffer = pc.Buffer(targetFeature.Shape, ((ISpatialReferenceTolerance)targetFeature.Shape.SpatialReference).XYTolerance * 1000);
                                            }
                                            else
                                            {
                                                testBuffer = pc.Buffer(targetFeature.Shape, ((ISpatialReferenceTolerance)targetFeature.Shape.SpatialReference).XYTolerance * 1.1);
                                            }

                                            bool equalPoint = ((IRelationalOperator2)testBuffer).ContainsEx(fcc.OriginalShape, esriSpatialRelationExEnum.esriSpatialRelationExProper);

                                            if (equalPoint == false)
                                            {
                                                LogOSMAction(revisionTable, "modify", "node", ((IDataset)targetFeature.Class).Name, osmID, osmVersion, -1, null);
                                            }
                                        }
                                    }
                                }
                            }

                            //if (osmTrackFeatureChangesFieldIndex > -1)
                            //{
                            //    targetFeature.set_Value(osmTrackFeatureChangesFieldIndex, 0);
                            //}
                        }
                    }
                }
            }
        }

        private void assignBasicMetadata(IFeature pointFeature, int osmIDFieldIndex, int wayRefCountFieldIndex, int osmVersionFieldIndex, out long osmID, out int osmVersion)
        {
            osmID = -1;
            if (osmIDFieldIndex > -1)
            {
                object osmIDValue = pointFeature.get_Value(osmIDFieldIndex);

                if (osmIDValue != DBNull.Value)
                {
                    osmID = Convert.ToInt64(osmIDValue);
                }
                else
                {
                    osmID = _temporaryIndex;
                    DecrementTemporaryIndex();
                    if (_ExtensionVersion == 1)
                        pointFeature.set_Value(osmIDFieldIndex, Convert.ToInt32(osmID));
                    else if (_ExtensionVersion == 2)
                        pointFeature.set_Value(osmIDFieldIndex, Convert.ToString(osmID));
                }
            }

            osmVersion = 1;
            if (osmVersionFieldIndex > -1)
            {
                object osmVersionValue = pointFeature.get_Value(osmVersionFieldIndex);
                if (osmVersionValue != DBNull.Value)
                {
                    osmVersion = Convert.ToInt32(osmVersionValue);
                }
                else if (osmVersionValue == DBNull.Value)
                    pointFeature.set_Value(osmVersionFieldIndex, osmVersion);
            }

            // initialize the ref counter value in case this hasn't happened yet
            if (wayRefCountFieldIndex > -1)
            {
                object refCountValue = pointFeature.get_Value(wayRefCountFieldIndex);
                int refCounter = 0;
                if (refCountValue != DBNull.Value)
                {
                    refCounter = Convert.ToInt32(refCountValue);
                    if (refCounter == 0)
                    {
                        pointFeature.set_Value(wayRefCountFieldIndex, 1);
                    }
                }
                else if (refCountValue == DBNull.Value)
                {
                    refCounter = refCounter + 1;
                    pointFeature.set_Value(wayRefCountFieldIndex, refCounter);
                }
            }
        }

        /// <summary>
        /// Combine the OSM tags from the source and the merge feature. This is usually the case when nodes are coincident and only one geometry 
        /// is needed in the OSM universe. 
        /// </summary>
        /// <param name="source">Source feature/Object.</param>
        /// <param name="mergeFeature">Target feature/object.</param>
        /// <param name="favorMergeFeature"> If set to true the merge feature tags will be kept, otherwise the source overwrites the merge feature.</param>
        /// <returns>The source feature with the union of tags.</returns>
        private IFeature mergeOSMTags(IFeature source, IFeature mergeFeature, bool favorMergeFeature)
        {
            if (mergeFeature == null)
            {
                if (source != null)
                {
                    mergeFeature = source;
                    return mergeFeature;
                }
                else
                {
                    return null;
                }
            }

            // rehydrate the tag from the container field
            Dictionary<string, tag> tagList = new Dictionary<string, tag>();
            tag[] sourceTags = null;

            int tagCollectionFieldIndex = source.Fields.FindField("osmTags");
            if (tagCollectionFieldIndex > -1)
            {
                sourceTags = _osmUtility.retrieveOSMTags(source, tagCollectionFieldIndex, ((IDataset)source.Class).Workspace);
            }

            tag[] targetTags = null;

            tagCollectionFieldIndex = mergeFeature.Fields.FindField("osmTags");
            if (tagCollectionFieldIndex > -1)
            {
                targetTags = _osmUtility.retrieveOSMTags(mergeFeature, tagCollectionFieldIndex, ((IDataset)source.Class).Workspace);

                if (targetTags != null)
                {
                    foreach (tag targetTag in targetTags)
                    {
                        tagList.Add(targetTag.k, targetTag);
                    }
                }
            }

            // let's do the merge of the source into target
            if (sourceTags != null)
            {
                foreach (tag sourceTag in sourceTags)
                {
                    // if the source already exists in the target then make the choice which one to keep
                    if (tagList.ContainsKey(sourceTag.k))
                    {
                        if (favorMergeFeature == false)
                        {
                            tagList.Remove(sourceTag.k);
                            tagList.Add(sourceTag.k, sourceTag);
                        }
                    }
                    // otherwise add it into the collection of targets
                    else
                    {
                        tagList.Add(sourceTag.k, sourceTag);
                    }
                }
            }

            if (tagList.Count > 0)
            {
                _osmUtility.insertOSMTags(tagCollectionFieldIndex, source, tagList.Values.ToArray(), ((IDataset)mergeFeature.Class).Workspace);
            }


            return source;
        }

        public void HandleFeatureAdds(IObject source)
        {
            IFeature newlyCreatedFeature = source as IFeature;

            try
            {
                // only feature classes are currently handled, tables are not yet supported
                if (newlyCreatedFeature == null)
                {
                    return;
                }

                IPoint currentPointt = newlyCreatedFeature.Shape as IPoint;



                IFeatureClass currentObjectFeatureClass = newlyCreatedFeature.Class as IFeatureClass;

                if (currentObjectFeatureClass == null)
                    return;

                // check if the current feature class being edited is acutally an OpenStreetMap feature class
                // all other feature classes should not be touched by this extension
                UID osmFeatureClassExtensionCLSID = currentObjectFeatureClass.EXTCLSID;

                // this could be the case if there is no feature class extension, this is very unlikely as the fc extension itself triggers this code
                // hence it has to exist
                if (osmFeatureClassExtensionCLSID == null)
                {
                    return;
                }

                if (osmFeatureClassExtensionCLSID.Value.ToString().Equals("{65CA4847-8661-45eb-8E1E-B2985CA17C78}", StringComparison.InvariantCultureIgnoreCase) == false)
                {
                    return;
                }

                int osmNewFeatureIDFieldIndex = currentObjectFeatureClass.FindField("OSMID");
                int osmNewFeatureVersionFieldIndex = currentObjectFeatureClass.FindField("osmversion");
                int osmNewFeatureSupportElementFieldIndex = currentObjectFeatureClass.FindField("osmSupportingElement");
                int osmNewFeatureIsMemberOfFieldIndex = currentObjectFeatureClass.FindField("osmMemberOf");
                int osmNewFeatureTimeStampFieldIndex = currentObjectFeatureClass.FindField("osmtimestamp");
                int osmTrackChangesFieldIndex = currentObjectFeatureClass.FindField("osmTrackchanges");

                // find the revision table for logging the changes.
                ITable revisionTable = findRevisionTable(newlyCreatedFeature);

                // Set revision table from cache or calculate from revision table
                SetTemporaryIndex(source, revisionTable);

                int revActionFieldIndex = revisionTable.Fields.FindField("osmaction");
                int revElementTypeFieldIndex = revisionTable.Fields.FindField("osmelementtype");
                int revFCNameFieldIndex = revisionTable.Fields.FindField("sourcefcname");
                int revOldIdFieldIndex = revisionTable.Fields.FindField("osmoldid"); ;

                // if the feature is a point treat it separately
                if (newlyCreatedFeature.Shape.GeometryType == esriGeometryType.esriGeometryPoint)
                {
                    UpdateMergeNode(newlyCreatedFeature, revisionTable);
                }
                else
                {
                    IFeatureClass pointFeatureClass = null;

                    // look for a matching OSM node feature class in the current map document
                    // otherwise open the node feature class directly
                    pointFeatureClass = findMatchingFeatureClass(newlyCreatedFeature, esriGeometryType.esriGeometryPoint);

                    if (pointFeatureClass == null)
                    {
                        System.Diagnostics.Debug.WriteLine("unable to locate point (osm node) feature class in adds adjustment");
                        return;
                    }

                    // loop through the nodes if a vertex already exists as an existing point (maybe due to point snap)
                    // then assign the already existing ID to it,
                    // if not then create a matching supporting node in the point feature class
                    IPointCollection pointCollection = newlyCreatedFeature.Shape as IPointCollection;

                    // make the top level geometry ID aware
                    IPointIDAware topGeometryIDAware = pointCollection as IPointIDAware;
                    if (topGeometryIDAware != null)
                    {
                        topGeometryIDAware.PointIDAware = true;
                    }

                    bool trackChanges = true;

                    //if (osmTrackChangesFieldIndex > -1)
                    //{
                    //    try
                    //    {
                    //        int trackChangesIndicator = Convert.ToInt32(newlyCreatedFeature.get_Value(osmTrackChangesFieldIndex));

                    //        if (trackChangesIndicator != 0)
                    //        {
                    //            trackChanges = false;
                    //        }
                    //    }
                    //    catch { }
                    //}


                    long featureOSMID = -1;

                    // set some initial metadata on the newly created feature
                    if (osmNewFeatureIDFieldIndex > -1)
                    {
                        // check if the feature needs a new ID or if it is created by a top-level geometry (in the case of multi-parts)
                        object osmIDValue = newlyCreatedFeature.get_Value(osmNewFeatureIDFieldIndex);

                        if (osmIDValue == DBNull.Value)
                        {
                            featureOSMID = _temporaryIndex;
                            DecrementTemporaryIndex();

                            if (_ExtensionVersion == 1)
                                newlyCreatedFeature.set_Value(osmNewFeatureIDFieldIndex, Convert.ToInt32(featureOSMID));
                            else if (_ExtensionVersion == 2)
                                newlyCreatedFeature.set_Value(osmNewFeatureIDFieldIndex, Convert.ToString(featureOSMID));
                        }
                        else
                        {
                            featureOSMID = Convert.ToInt64(osmIDValue);

                            // if the code in instructed to create a new feature with a negative OSMID then something else duplicating a new feature with
                            // existing attributes - that is not allowed by definition hence to decrement the temporary index by 1 to indicate a new feature
                            if (featureOSMID < 0)
                            {
                                featureOSMID = _temporaryIndex;
                                DecrementTemporaryIndex();
                            }
                        }
                    }

                    if (osmNewFeatureVersionFieldIndex > -1)
                    {
                        newlyCreatedFeature.set_Value(osmNewFeatureVersionFieldIndex, 1);
                    }


                    IGeometryCollection existingGeometryCollection = pointCollection as IGeometryCollection;

                    // ensure that the incoming geometry are conforming to the node limit of OSM as well as multi-part geometries being prepared to be 
                    // represented as relations
                    pointCollection = createOSMRelationNodeClusters("add", newlyCreatedFeature, osmNewFeatureIDFieldIndex, osmNewFeatureVersionFieldIndex, osmNewFeatureSupportElementFieldIndex, osmNewFeatureIsMemberOfFieldIndex, osmNewFeatureTimeStampFieldIndex, osmTrackChangesFieldIndex, trackChanges);


                    // we will need a new geometry container
                    IGeometryCollection updatedGeometryCollection = null;
                    if (pointCollection is IPolyline)
                    {
                        updatedGeometryCollection = new PolylineClass();
                    }
                    else if (pointCollection is IPolygon)
                    {
                        updatedGeometryCollection = new PolygonClass();
                    }


                    if (updatedGeometryCollection != null)
                    {
                        IPointIDAware pointIDAware = updatedGeometryCollection as IPointIDAware;

                        if (pointIDAware != null)
                        {
                            pointIDAware.PointIDAware = true;
                        }
                    }

                    object missingValue = Missing.Value;

                    // let's loop through all geometry parts  
                    for (int geometryIndex = 0; geometryIndex < existingGeometryCollection.GeometryCount; geometryIndex++)
                    {
                        IPointCollection geometryPartPointCollection = existingGeometryCollection.get_Geometry(geometryIndex) as IPointCollection;

                        // check all points if they are coincident with already existing points or if they need to be created  
                        geometryPartPointCollection = checkAllPoints(newlyCreatedFeature, pointFeatureClass, "create", osmNewFeatureIDFieldIndex, geometryPartPointCollection, trackChanges);

                        updatedGeometryCollection.AddGeometry((IGeometry)geometryPartPointCollection, ref missingValue, ref missingValue);
                    }

                    // ensure that the changes and adjustments are persisted back to the geometry
                    newlyCreatedFeature.Shape = updatedGeometryCollection as IGeometry;

                    // ensure that if we create a multi-part polygon geometry that there is acutally a tag to indicate such (multipolygon)
                    if ((pointCollection is IPolygon) && (updatedGeometryCollection.GeometryCount > 1))
                    {
                        int osmTagFieldIndex = newlyCreatedFeature.Fields.FindField("osmTags");

                        bool tagAlreadyExits = false;
                        if (osmTagFieldIndex > -1)
                        {
                            tag[] existingTags = _osmUtility.retrieveOSMTags(newlyCreatedFeature, osmTagFieldIndex, null);

                            List<tag> featureTags = null;
                            if (existingTags != null)
                                featureTags = new List<tag>(_osmUtility.retrieveOSMTags(newlyCreatedFeature, osmTagFieldIndex, null));
                            else
                                featureTags = new List<tag>();

                            foreach (tag currentTag in featureTags)
                            {
                                if (currentTag.k.ToUpper().Equals("TYPE") == true)
                                {
                                    tagAlreadyExits = true;
                                }
                            }
                            if (tagAlreadyExits == false)
                            {
                                tag multipolygonTag = new tag();
                                multipolygonTag.k = "type";
                                multipolygonTag.v = "multipolygon";

                                featureTags.Add(multipolygonTag);

                                _osmUtility.insertOSMTags(osmTagFieldIndex, newlyCreatedFeature, featureTags.ToArray());
                            }
                        }
                    }

                    if (m_bypassOSMChangeDetection == false)
                    {
                        if (((IGeometryCollection)pointCollection).GeometryCount > 1)
                        {
                            LogOSMAction(revisionTable, "create", "relation", ((IDataset)newlyCreatedFeature.Class).Name, featureOSMID, 1, -1, null);
                        }
                        else
                        {
                            LogOSMAction(revisionTable, "create", "way", ((IDataset)newlyCreatedFeature.Class).Name, featureOSMID, 1, -1, null);
                        }
                    }

                    // change the indicator back to track everything
                    //if (osmTrackChangesFieldIndex > -1)
                    //{
                    //    newlyCreatedFeature.set_Value(osmTrackChangesFieldIndex, 0);
                    //}
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
            }
        }

        private IPointCollection checkAllPoints(IFeature newlyCreatedFeature, IFeatureClass pointFeatureClass, string action, int osmNewFeatureIDFieldIndex, IPointCollection partPointCollection, bool trackChanges)
        {
            try
            {
                // add this point we should get all the vertices with a new ID back
                IEnumVertex enumVertex = partPointCollection.EnumVertices;
                //List<int> addIDs = detectPartAdds(((IFeatureChanges)newlyCreatedFeature).OriginalShape, partPointCollection as IGeometry);
                List<long> addIDs = detectPartAdds(null, partPointCollection as IGeometry);

                //check if we have the case of a coincident point
                // - in case we do have a coincident point then update the ID with the existing ID and increase the ref counter
                // - in case we don't have a point then let's store a new supporting node 

                int pointSupportElementFieldIndex = pointFeatureClass.FindField("osmSupportingElement");
                int pointOSMIDFieldIndex = pointFeatureClass.FindField("OSMID");
                int pointwayRefCountFieldIndex = pointFeatureClass.FindField("wayRefCount");
                int pointVersionFieldIndex = pointFeatureClass.FindField("osmversion");
                int pointisMemberOfFieldIndex = pointFeatureClass.FindField("osmMemberOf");
                int pointTrackChangesFieldIndex = pointFeatureClass.FindField("osmTrackChanges");

                string sqlOSMID = pointFeatureClass.SqlIdentifier("OSMID");

                IQueryFilter nodeQueryFilter = new QueryFilterClass();

                enumVertex.Reset();

                IPoint currentPoint = null;
                int partIndex = -1;
                int vertexIndex = -1;
                int addVertexCounter = 0;

                enumVertex.Next(out currentPoint, out partIndex, out vertexIndex);

                // for polygon and line features loop through all the points and determine if they are new or coincident points
                while (currentPoint != null)
                {
                    if (enumVertex.IsLastInPart() && newlyCreatedFeature.Shape is IPolygon)
                    {
                        // skip the last vertex for a part in a polygon as it is supposed to be coincident with the first node
                    }
                    else
                    {
                        currentPoint.Project(((IGeoDataset)pointFeatureClass).SpatialReference);

                        // to find the coincident point let's run an intersection operation with the existing point feature class
                        ISpatialFilter pointSearchFilter = new SpatialFilterClass();

                        // Original code from Thomas to see if there a duplicate node
                        IBufferConstruction bc = new BufferConstructionClass();
                        // exapnd the xy tolerance by 50% for point to be considered equal
                        IGeometry buffer = null;
                        if (currentPoint.SpatialReference is IGeographicCoordinateSystem)
                        {
                            buffer = bc.Buffer(currentPoint, ((ISpatialReferenceTolerance)currentPoint.SpatialReference).XYTolerance * 1000);
                        }
                        else
                        {
                            buffer = bc.Buffer(currentPoint, ((ISpatialReferenceTolerance)currentPoint.SpatialReference).XYTolerance * 1.1);
                        }

                        ((IPolygon)buffer).SimplifyPreserveFromTo();

                        pointSearchFilter.Geometry = buffer.Envelope;
                        pointSearchFilter.SearchOrder = esriSearchOrder.esriSearchOrderSpatial;
                        pointSearchFilter.GeometryField = pointFeatureClass.ShapeFieldName;
                        pointSearchFilter.SpatialRel = esriSpatialRelEnum.esriSpatialRelContains;

                        using (ComReleaser comReleaser = new ComReleaser())
                        {

                            IFeatureCursor updateCursor = pointFeatureClass.Search(pointSearchFilter, false);
                            comReleaser.ManageLifetime(updateCursor);

                            IFeature foundFeature = updateCursor.NextFeature();

                            // if we find an already existing point/node feature then are dealing with coincident geometries
                            // and we need to adjustment the vertex ID to reference the already existing one
                            if (foundFeature != null)
                            {
                                long searchOSMID = 0;
                                if (osmNewFeatureIDFieldIndex > -1)
                                {
                                    searchOSMID = Convert.ToInt64(foundFeature.get_Value(osmNewFeatureIDFieldIndex));
                                }

                                // if we found a match, please ensure that it is not the node (by id) itself
                                // this can happen when we store the top level geometry first, and then add the individual parts second
                                bool comparison = false;
                                if (_ExtensionVersion == 1)
                                    comparison = currentPoint.ID != searchOSMID;
                                else if (_ExtensionVersion == 2)
                                    comparison = currentPoint.ID != foundFeature.OID;

                                if (comparison)
                                {
                                    partPointCollection.UpdatePoint(vertexIndex, (IPoint)foundFeature.Shape);

                                    if (_ExtensionVersion == 1)
                                    {
                                        currentPoint.ID = (int)searchOSMID;
                                        enumVertex.put_ID((int)searchOSMID);
                                    }
                                    else if (_ExtensionVersion == 2)
                                    {
                                        currentPoint.ID = foundFeature.OID;
                                        enumVertex.put_ID(foundFeature.OID);
                                    }

                                    // update the ref count on the node
                                    if (pointwayRefCountFieldIndex > -1)
                                    {
                                        int currentRefCount = Convert.ToInt32(foundFeature.get_Value(pointwayRefCountFieldIndex));

                                        currentRefCount = currentRefCount + 1;
                                        foundFeature.set_Value(pointwayRefCountFieldIndex, currentRefCount);
                                    }

                                    //if (trackChanges == false)
                                    //{
                                    //    if (pointTrackChangesFieldIndex > -1)
                                    //    {
                                    //        foundFeature.set_Value(pointTrackChangesFieldIndex, 1);
                                    //    }
                                    //}

                                    foundFeature.Store();
                                }

                                else if (!comparison && action.ToLower(CultureInfo.InvariantCulture).Equals("create"))
                                {
                                    // for split the new higher-order geometry contains the (existing) nodes but it will also
                                    // be deleted later-on due to the split behavior
                                    // update the ref count on the node
                                    if (pointwayRefCountFieldIndex > -1)
                                    {
                                        int currentRefCount = Convert.ToInt32(foundFeature.get_Value(pointwayRefCountFieldIndex));

                                        currentRefCount = currentRefCount + 1;
                                        foundFeature.set_Value(pointwayRefCountFieldIndex, currentRefCount);

                                        foundFeature.Store();
                                    }
                                }

                                addVertexCounter = addVertexCounter + 1;
                            }
                            else
                            {
                                // then we have a previously existing node // let's just track the move
                                if (currentPoint.ID > 0)
                                {
                                    IFeature existingNodeFeature = null;

                                    if (_ExtensionVersion == 1)
                                    {
                                        nodeQueryFilter.WhereClause = pointFeatureClass.WhereClauseByExtensionVersion(currentPoint.ID, "OSMID", _ExtensionVersion);
                                        IFeatureCursor nodeUpdateCursor = pointFeatureClass.Search(nodeQueryFilter, false);
                                        comReleaser.ManageLifetime(nodeUpdateCursor);

                                        existingNodeFeature = nodeUpdateCursor.NextFeature();
                                    }
                                    else if (_ExtensionVersion == 2)
                                    {
                                        try
                                        {
                                            existingNodeFeature = pointFeatureClass.GetFeature(currentPoint.ID);
                                        }
                                        catch
                                        {
                                            existingNodeFeature = null;
                                        }
                                    }

                                    if (existingNodeFeature != null)
                                    {
                                        if (action.ToLower(CultureInfo.InvariantCulture).Equals("create"))
                                        {
                                            // update the ref count on the node
                                            if (pointwayRefCountFieldIndex > -1)
                                            {
                                                int currentRefCount = Convert.ToInt32(existingNodeFeature.get_Value(pointwayRefCountFieldIndex));

                                                currentRefCount = currentRefCount + 1;
                                                existingNodeFeature.set_Value(pointwayRefCountFieldIndex, currentRefCount);
                                            }
                                        }

                                        // -------
                                        // if the ref counter on the node is larger than 1 it means that some other entity is using the same node
                                        // in this case we create a new node for the parent feature and decrease the ref counter on the found feature
                                        if (pointwayRefCountFieldIndex > -1)
                                        {
                                            int currentRefCount = Convert.ToInt32(existingNodeFeature.get_Value(pointwayRefCountFieldIndex));

                                            if (currentRefCount > 1)
                                            {
                                                IBufferConstruction pc = new BufferConstructionClass();
                                                // exapnd the xy tolerance by 50% for point to be considered equal
                                                IGeometry testBuffer = null;
                                                if (currentPoint.SpatialReference is IGeographicCoordinateSystem)
                                                {
                                                    testBuffer = pc.Buffer(currentPoint, ((ISpatialReferenceTolerance)currentPoint.SpatialReference).XYTolerance * 1000);
                                                }
                                                else
                                                {
                                                    testBuffer = pc.Buffer(currentPoint, ((ISpatialReferenceTolerance)currentPoint.SpatialReference).XYTolerance * 1.5);
                                                }

                                                bool equalPoint = ((IRelationalOperator2)testBuffer).ContainsEx(existingNodeFeature.Shape, esriSpatialRelationExEnum.esriSpatialRelationExProper);

                                                if (equalPoint == false)
                                                {

                                                    IFeature newSupportFeature = pointFeatureClass.CreateFeature();

                                                    IPoint newSupportPoint = ((IClone)currentPoint).Clone() as IPoint;
                                                    newSupportPoint.ID = newSupportFeature.OID;

                                                    newSupportFeature.Shape = newSupportPoint;

                                                    // the current point needs a new ID
                                                    currentPoint.ID = newSupportFeature.OID;
                                                    enumVertex.put_ID(newSupportFeature.OID);

                                                    if (pointSupportElementFieldIndex > -1)
                                                        newSupportFeature.set_Value(pointSupportElementFieldIndex, "yes");

                                                    if (pointOSMIDFieldIndex > -1)
                                                    {
                                                        if (_ExtensionVersion == 1)
                                                            newSupportFeature.set_Value(pointOSMIDFieldIndex, Convert.ToInt32(_temporaryIndex));
                                                        else if (_ExtensionVersion == 2)
                                                            newSupportFeature.set_Value(pointOSMIDFieldIndex, Convert.ToString(_temporaryIndex));

                                                        DecrementTemporaryIndex();
                                                    }

                                                    if (pointVersionFieldIndex > -1)
                                                    {
                                                        newSupportFeature.set_Value(pointVersionFieldIndex, 1);
                                                    }

                                                    //if (trackChanges == false)
                                                    //{
                                                    //    if (pointTrackChangesFieldIndex > -1)
                                                    //    {
                                                    //        newSupportFeature.set_Value(pointTrackChangesFieldIndex, 1);
                                                    //    }
                                                    //}

                                                    newSupportFeature.Store();

                                                    // the replaced vertices will be recognized as deletes later on 
                                                    // and then the ref counter will be adjusted approppriately
                                                }
                                            }
                                            else
                                            {
                                                // assign the updated geometry to the existing node
                                                existingNodeFeature.Shape = ((IClone)currentPoint).Clone() as IPoint;
                                            }
                                        }

                                        //if (trackChanges == false)
                                        //{
                                        //    if (pointTrackChangesFieldIndex > -1)
                                        //    {
                                        //        existingNodeFeature.set_Value(pointTrackChangesFieldIndex, 1);
                                        //    }
                                        //}

                                        // persist the changes for the point
                                        existingNodeFeature.Store();
                                    }
                                    else
                                    {
                                        // now here is the checking for the special case due to a merge operation
                                        // a merge operation is defined by a delete and an add - this is problematic for the OSM data structure
                                        // as we need to keep track of the way (polygon/polyline) as well as the points
                                        // the core editor doesn't know about this relationship and hence after the first delete request, all the nodes
                                        // are deleted as well - 
                                        // since this is a special case and we are "only" deleting supporting nodes/points I hope that most users would accept
                                        // that we are recreating the points from scratch
                                        // if this procedure turns out to be an unacceptable no-no then we need to block the merge operation with a more succinct 
                                        // error message
                                        if (currentPoint.ID > -1)
                                        {
                                            // we are populating the add IDs as we go - but as opposed to adding the currently used one (for which we deleted the OSM metadata info)
                                            // we pick a new ID
                                            addIDs.Add(_temporaryIndex);
                                            DecrementTemporaryIndex();
                                        }

                                        // in this case we have a new point/node geometry
                                        // - create a matching node in the node feature class and log the creation in the revision table

                                        IFeature newSupportingPointFeature = pointFeatureClass.CreateFeature();

                                        // add the id to the local point copy as well
                                        if (_ExtensionVersion == 1)
                                        {
                                            enumVertex.put_ID(Convert.ToInt32(addIDs[addVertexCounter]));
                                            currentPoint.ID = Convert.ToInt32(addIDs[addVertexCounter]);
                                        }
                                        else if (_ExtensionVersion == 2)
                                        {
                                            enumVertex.put_ID(newSupportingPointFeature.OID);
                                            currentPoint.ID = newSupportingPointFeature.OID;
                                        }

                                        newSupportingPointFeature.Shape = currentPoint;

                                        if (pointSupportElementFieldIndex > -1)
                                        {
                                            newSupportingPointFeature.set_Value(pointSupportElementFieldIndex, "yes");
                                        }

                                        if (pointOSMIDFieldIndex > -1)
                                        {
                                            if (_ExtensionVersion == 1)
                                                newSupportingPointFeature.set_Value(pointOSMIDFieldIndex, Convert.ToInt32(addIDs[addVertexCounter]));
                                            else if (_ExtensionVersion == 2)
                                                newSupportingPointFeature.set_Value(pointOSMIDFieldIndex, Convert.ToString(addIDs[addVertexCounter]));
                                        }

                                        if (pointVersionFieldIndex > -1)
                                        {
                                            newSupportingPointFeature.set_Value(pointVersionFieldIndex, 1);
                                        }

                                        //if (trackChanges == false)
                                        //{
                                        //    if (pointTrackChangesFieldIndex > -1)
                                        //    {
                                        //        newSupportingPointFeature.set_Value(pointTrackChangesFieldIndex, 1);
                                        //    }
                                        //}

                                        newSupportingPointFeature.Store();
                                    }

                                    addVertexCounter = addVertexCounter + 1;
                                }
                                else
                                {
                                    IFeature newSupportingPointFeature = pointFeatureClass.CreateFeature();

                                    // in this case we have a new point/node geometry
                                    // - create a matching node in the node feature class and log the creation in the revision table

                                    if (_ExtensionVersion == 1)
                                        enumVertex.put_ID(Convert.ToInt32(addIDs[addVertexCounter]));
                                    else if (_ExtensionVersion == 2)
                                    {
                                        enumVertex.put_ID(newSupportingPointFeature.OID);
                                        currentPoint.ID = newSupportingPointFeature.OID;
                                    }

                                    newSupportingPointFeature.Shape = currentPoint;

                                    if (pointSupportElementFieldIndex > -1)
                                    {
                                        newSupportingPointFeature.set_Value(pointSupportElementFieldIndex, "yes");
                                    }

                                    if (pointOSMIDFieldIndex > -1)
                                    {
                                        if (_ExtensionVersion == 1)
                                            newSupportingPointFeature.set_Value(pointOSMIDFieldIndex, Convert.ToInt32(addIDs[addVertexCounter]));
                                        else if (_ExtensionVersion == 2)
                                            newSupportingPointFeature.set_Value(pointOSMIDFieldIndex, Convert.ToString(addIDs[addVertexCounter]));
                                    }

                                    if (pointwayRefCountFieldIndex > -1)
                                    {
                                        newSupportingPointFeature.set_Value(pointwayRefCountFieldIndex, 1);
                                    }

                                    if (pointVersionFieldIndex > -1)
                                    {
                                        newSupportingPointFeature.set_Value(pointVersionFieldIndex, 1);
                                    }

                                    //if (trackChanges == false)
                                    //{
                                    //    if (pointTrackChangesFieldIndex > -1)
                                    //    {
                                    //        newSupportingPointFeature.set_Value(pointTrackChangesFieldIndex, 1);
                                    //    }
                                    //}

                                    newSupportingPointFeature.Store();

                                    addVertexCounter = addVertexCounter + 1;
                                }
                            }
                        }
                    }

                    enumVertex.Next(out currentPoint, out partIndex, out vertexIndex);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
            }

            return partPointCollection;
        }

        private IPointCollection createOSMRelationNodeClusters(string action, IFeature workingFeature, int osmIDFieldIndex, int versionFieldIndex, int osmSupportElementFieldIndex, int osmIsMemberOfFieldIndex, int osmTimeStampFieldIndex, int osmTrackChangesFieldIndex, bool trackChanges)
        {
            // handle the special case of more than 2000 vertices 
            // and multipart entities
            object MissingValue = Missing.Value;
            List<member> relationMemberList = new List<member>();

            IPointCollection pointCollection = workingFeature.Shape as IPointCollection;
            IFeatureChanges workingFeatureChanges = workingFeature as IFeatureChanges;

            if (pointCollection.PointCount == 0)
            {
                pointCollection = workingFeatureChanges.OriginalShape as IPointCollection;
            }

            IGeometryCollection existingGeometryCollection = pointCollection as IGeometryCollection;

            IPointCollection originalPointCollection = null;
            if (workingFeatureChanges.OriginalShape == null)
            {
                originalPointCollection = pointCollection;
            }
            else
            {
                originalPointCollection = workingFeatureChanges.OriginalShape as IPointCollection;
                if (workingFeatureChanges.ShapeChanged == false && action.Equals("change"))
                {
                    return pointCollection;
                }
            }
            IGeometryCollection originalGeometryCollection = originalPointCollection as IGeometryCollection;

            int workingFeatureMembersFieldIndex = workingFeature.Fields.FindField("osmMembers");
            member[] currentMembers = null;

            if (existingGeometryCollection.GeometryCount > 1)
            {
                if (workingFeatureMembersFieldIndex > -1)
                {
                    currentMembers = _osmUtility.retrieveMembers(workingFeature, workingFeatureMembersFieldIndex);
                }
            }

            // lines of nodes larger than 2000 are split into multiple features
            if (workingFeature.Shape.GeometryType == esriGeometryType.esriGeometryPolyline)
            {
                if (existingGeometryCollection.GeometryCount > 1 || pointCollection.PointCount > 2000)
                {
                    // ensure that the newly generated polyline has the same spatial reference as the "old" one
                    IGeometryCollection newLineGeometry = new PolylineClass() as IGeometryCollection;
                    ((IPolyline)newLineGeometry).SpatialReference = ((IPolyline)pointCollection).SpatialReference;
                    IPointIDAware newLinePointIDAware = newLineGeometry as IPointIDAware;
                    newLinePointIDAware.PointIDAware = true;

                    // create mulit-part polyline
                    member newLineMember = null;
                    for (int linePartIndex = 0; linePartIndex < existingGeometryCollection.GeometryCount; linePartIndex++)
                    {
                        IPath currentPath = existingGeometryCollection.get_Geometry(linePartIndex) as IPath;

                        IPointCollection pathPointCollection = currentPath as IPointCollection;
                        if (pathPointCollection.PointCount > 2000)
                        {
                            IPointCollection newPathPointCollection = new PathClass() as IPointCollection;
                            ((IPath)newPathPointCollection).SpatialReference = ((IPolyline)pointCollection).SpatialReference;

                            for (int pointIndex = 0; pointIndex < pathPointCollection.PointCount; pointIndex++)
                            {
                                // break the line point collection into chunks of 2000 points (waynodes) -- 
                                // a number provided by the OSM v0.6 API
                                if ((pointIndex % 2000) == 0)
                                {
                                    newLineGeometry.AddGeometry((IPath)newPathPointCollection, ref MissingValue, ref MissingValue);

                                    // since we need to keep feature level OSM metadata around, we need to be sure to add a support object
                                    newLineMember = new member();
                                    newLineMember.@ref = Convert.ToString(_temporaryIndex);
                                    DecrementTemporaryIndex();
                                    newLineMember.role = String.Empty;
                                    newLineMember.type = memberType.way;

                                    relationMemberList.Add(newLineMember);

                                    storeSupportingGeometry((IGeometry)newPathPointCollection, (IFeatureClass)workingFeature.Class, Convert.ToInt64(newLineMember.@ref), Convert.ToInt64(workingFeature.get_Value(osmIDFieldIndex)), osmIDFieldIndex, versionFieldIndex, osmSupportElementFieldIndex, osmIsMemberOfFieldIndex, osmTimeStampFieldIndex, osmTrackChangesFieldIndex, trackChanges);

                                    newPathPointCollection = new PathClass() as IPointCollection;
                                    ((IPath)newPathPointCollection).SpatialReference = ((IPolyline)pointCollection).SpatialReference;

                                }

                                newPathPointCollection.AddPoint(pathPointCollection.get_Point(pointIndex), ref MissingValue, ref MissingValue);

                            }

                            newLineGeometry.AddGeometry((IPath)newPathPointCollection, ref MissingValue, ref MissingValue);

                            // since we need to keep feature level OSM metadata around, we need to be sure to add a support object
                            newLineMember = new member();
                            newLineMember.@ref = Convert.ToString(_temporaryIndex);
                            DecrementTemporaryIndex();
                            newLineMember.role = String.Empty;
                            newLineMember.type = memberType.way;

                            relationMemberList.Add(newLineMember);

                            storeSupportingGeometry((IGeometry)newPathPointCollection, (IFeatureClass)workingFeature.Class, Convert.ToInt64(newLineMember.@ref), Convert.ToInt64(workingFeature.get_Value(osmIDFieldIndex)), osmIDFieldIndex, versionFieldIndex, osmSupportElementFieldIndex, osmIsMemberOfFieldIndex, osmTimeStampFieldIndex, osmTrackChangesFieldIndex, trackChanges);

                        }
                        else
                        {
                            if (action.Equals("change"))
                            {
                                int partIndex = findPartIndex(originalGeometryCollection, currentPath);

                                if (partIndex > -1)
                                {
                                    relationMemberList.Add(currentMembers[partIndex]);
                                }
                            }
                            else
                            {
                                newLineGeometry.AddGeometry(currentPath, ref MissingValue, ref MissingValue);

                                newLineMember = new member();
                                newLineMember.@ref = Convert.ToString(_temporaryIndex);
                                DecrementTemporaryIndex();
                                newLineMember.role = String.Empty;
                                newLineMember.type = memberType.way;

                                relationMemberList.Add(newLineMember);

                                storeSupportingGeometry((IGeometry)currentPath, (IFeatureClass)workingFeature.Class, Convert.ToInt64(newLineMember.@ref), Convert.ToInt64(workingFeature.get_Value(osmIDFieldIndex)), osmIDFieldIndex, versionFieldIndex, osmSupportElementFieldIndex, osmIsMemberOfFieldIndex, osmTimeStampFieldIndex, osmTrackChangesFieldIndex, trackChanges);
                            }
                        }
                    }

                    // make sure that all the newly added geometries are recognized and the geometry caches are invalidated
                    newLineGeometry.GeometriesChanged();
                    // assign the newly created line geometry to the existing point collection pointer
                    pointCollection = newLineGeometry as IPointCollection;
                }
            }
            else if (workingFeature.Shape.GeometryType == esriGeometryType.esriGeometryPolygon)
            {
                IGeometryCollection existingPolygonGeometryCollection = pointCollection as IGeometryCollection;

                if (existingPolygonGeometryCollection.GeometryCount > 1)
                {
                    IPolygon4 polyPointCollection = pointCollection as IPolygon4;
                    polyPointCollection.SimplifyPreserveFromTo();

                    // each ring (way) element can't have more than 2000 nodes
                    IGeometryCollection outerRingGeometryCollection = polyPointCollection.ExteriorRingBag as IGeometryCollection;

                    for (int outerRingIndex = 0; outerRingIndex < outerRingGeometryCollection.GeometryCount; outerRingIndex++)
                    {
                        IGeometryCollection innerRingGeometryCollection = polyPointCollection.get_InteriorRingBag((IRing)outerRingGeometryCollection.get_Geometry(outerRingIndex)) as IGeometryCollection;

                        for (int innerRingIndex = 0; innerRingIndex < innerRingGeometryCollection.GeometryCount; innerRingIndex++)
                        {
                            IPointCollection innerRingPointCollection = innerRingGeometryCollection.get_Geometry(innerRingIndex) as IPointCollection;

                            if (innerRingPointCollection.PointCount > 2000)
                            {
                                string errorMessage = String.Format(resourceManager.GetString("OSMClassExtension_FeatureInspector_pointnumber_exceeeded_in_ring"), 2000, innerRingIndex);
                                throw new ArgumentException(errorMessage);
                            }

                            if (action.Equals("change"))
                            {
                                int partIndex = findPartIndex(originalGeometryCollection, innerRingGeometryCollection.get_Geometry(innerRingIndex));

                                if (partIndex > -1)
                                {
                                    relationMemberList.Add(currentMembers[partIndex]);
                                }
                                else
                                {
                                    member newHoleMember = new member();
                                    newHoleMember.@ref = Convert.ToString(_temporaryIndex);
                                    DecrementTemporaryIndex();
                                    newHoleMember.role = "inner";
                                    newHoleMember.type = memberType.way;

                                    relationMemberList.Add(newHoleMember);

                                    storeSupportingGeometry(innerRingGeometryCollection.get_Geometry(innerRingIndex), (IFeatureClass)workingFeature.Class, Convert.ToInt32(newHoleMember.@ref), (int)workingFeature.get_Value(osmIDFieldIndex), osmIDFieldIndex, versionFieldIndex, osmSupportElementFieldIndex, osmIsMemberOfFieldIndex, osmTimeStampFieldIndex, osmTrackChangesFieldIndex, trackChanges);
                                }
                            }
                            else
                            {
                                member newHoleMember = new member();
                                newHoleMember.@ref = Convert.ToString(_temporaryIndex);
                                DecrementTemporaryIndex();
                                newHoleMember.role = "inner";
                                newHoleMember.type = memberType.way;

                                relationMemberList.Add(newHoleMember);

                                storeSupportingGeometry(innerRingGeometryCollection.get_Geometry(innerRingIndex), (IFeatureClass)workingFeature.Class, Convert.ToInt32(newHoleMember.@ref), (int)workingFeature.get_Value(osmIDFieldIndex), osmIDFieldIndex, versionFieldIndex, osmSupportElementFieldIndex, osmIsMemberOfFieldIndex, osmTimeStampFieldIndex, osmTrackChangesFieldIndex, trackChanges);
                            }
                        }

                        IPointCollection ringPointCollection = outerRingGeometryCollection.get_Geometry(outerRingIndex) as IPointCollection;

                        if (ringPointCollection.PointCount > 2000)
                        {
                            string errorMessage = String.Format(resourceManager.GetString("OSMClassExtension_FeatureInspector_pointnumber_exceeeded_in_ring"), 2000, outerRingIndex);
                            throw new ArgumentException(errorMessage);
                        }

                        if (action.Equals("change"))
                        {
                            int partIndex = findPartIndex(originalGeometryCollection, outerRingGeometryCollection.get_Geometry(outerRingIndex));

                            if (partIndex > -1)
                            {
                                relationMemberList.Add(currentMembers[partIndex]);
                            }
                            else
                            {
                                member newPolygonMember = new member();
                                newPolygonMember.@ref = Convert.ToString(_temporaryIndex);
                                DecrementTemporaryIndex();
                                newPolygonMember.role = "outer";
                                newPolygonMember.type = memberType.way;

                                relationMemberList.Add(newPolygonMember);

                                storeSupportingGeometry(outerRingGeometryCollection.get_Geometry(outerRingIndex), (IFeatureClass)workingFeature.Class, Convert.ToInt32(newPolygonMember.@ref), (int)workingFeature.get_Value(osmIDFieldIndex), osmIDFieldIndex, versionFieldIndex, osmSupportElementFieldIndex, osmIsMemberOfFieldIndex, osmTimeStampFieldIndex, osmTrackChangesFieldIndex, trackChanges);
                            }
                        }
                        else
                        {
                            member newPolygonMember = new member();
                            newPolygonMember.@ref = Convert.ToString(_temporaryIndex);
                            DecrementTemporaryIndex();
                            newPolygonMember.role = "outer";
                            newPolygonMember.type = memberType.way;

                            relationMemberList.Add(newPolygonMember);

                            storeSupportingGeometry(outerRingGeometryCollection.get_Geometry(outerRingIndex), (IFeatureClass)workingFeature.Class, Convert.ToInt64(newPolygonMember.@ref), Convert.ToInt64(workingFeature.get_Value(osmIDFieldIndex)), osmIDFieldIndex, versionFieldIndex, osmSupportElementFieldIndex, osmIsMemberOfFieldIndex, osmTimeStampFieldIndex, osmTrackChangesFieldIndex, trackChanges);
                        }
                    }
                }

                if (existingGeometryCollection.GeometryCount > 1)
                {
                    if (workingFeatureMembersFieldIndex > -1)
                    {
                        _osmUtility.insertMembers(workingFeatureMembersFieldIndex, workingFeature, relationMemberList.ToArray());
                    }
                }
            }
            return pointCollection;
        }

        private int findPartIndex(IGeometryCollection geometryCollection, IGeometry testGeometry)
        {
            int foundPartIndex = -1;

            if (geometryCollection == null)
            {
                return foundPartIndex;
            }

            if (testGeometry == null)
            {
                return foundPartIndex;
            }

            try
            {
                for (int geometryIndex = 0; geometryIndex < geometryCollection.GeometryCount; geometryIndex++)
                {
                    IRelationalOperator relationalOperator = testGeometry as IRelationalOperator;

                    if (relationalOperator.Equals(geometryCollection.get_Geometry(geometryIndex)))
                    {
                        foundPartIndex = geometryIndex;
                        break;
                    }
                }
            }
            catch
            {
            }

            return foundPartIndex;
        }

        private List<long> detectPartAdds(IGeometry originalGeometry, IGeometry updatedGeometry)
        {
            List<long> addedIndexes = new List<long>();

            try
            {
                IEnumVertex originalEnumVertex = null;

                if (originalGeometry != null)
                {
                    originalEnumVertex = ((IPointCollection)originalGeometry).EnumVertices;
                }

                IEnumVertex updatedEnumVertex = null;

                if (updatedGeometry != null)
                {
                    updatedEnumVertex = ((IPointCollection)updatedGeometry).EnumVertices;
                }

                if (originalEnumVertex == null && updatedGeometry != null)
                {
                    addedIndexes = createIDList(updatedGeometry, updatedEnumVertex);

                }
                else if (originalGeometry != null && updatedGeometry != null)
                {
                    List<long> updatedIDList = createIDList(updatedGeometry, updatedEnumVertex);

                    List<long> originalIDList = createIDList(originalGeometry, originalEnumVertex);

                    // create a difference query
                    // positive numbers mean the vertices were removed and negative numbers mean that vertices were added
                    IEnumerable<long> addedQuery = updatedIDList.Except(originalIDList);

                    // store the relative complement findings in a list
                    addedIndexes = addedQuery.ToList();
                }
            }
            catch
            {
            }

            return addedIndexes;
        }

        /// <summary>
        /// returns the ids of all vertices as a list
        /// </summary>
        /// <param name="inputGeometry">needs to be of type IRing or IPath</param>
        /// <param name="enumVertex"></param>
        /// <returns></returns>
        private List<long> createIDList(IGeometry inputGeometry, IEnumVertex enumVertex)
        {
            IPoint currentPoint = new PointClass();
            int partIndex = -1;
            int vertexIndex = -1;
            List<long> iDList = new List<long>();

            if (inputGeometry == null)
            {
                throw new ArgumentNullException("inputGeometry");
            }

            if (enumVertex == null)
            {
                throw new ArgumentNullException("enumVertex");
            }

            enumVertex.Reset();

            enumVertex.QueryNext(currentPoint, out partIndex, out vertexIndex);

            while (vertexIndex > -1)
            {
                if (inputGeometry is IPolygon)
                {
                    if (enumVertex.IsLastInPart())
                    {
                        // ignore the last point in the part in ring as it is supposed to be coincident with the first point
                        // iDList.Add(iDList[0]);
                    }
                    else
                    {
                        // test if the point has an ID already assigned to it
                        if (currentPoint.ID == 0)
                        {
                            // if not then give it a temporary id
                            enumVertex.put_ID(Convert.ToInt32(_temporaryIndex));
                            iDList.Add(_temporaryIndex);

                            DecrementTemporaryIndex();
                        }
                        else
                        {
                            iDList.Add(currentPoint.ID);
                        }
                    }
                }
                else
                {
                    // test if the point has an ID already assigned to it
                    if (currentPoint.ID == 0)
                    {
                        // if not then give it a temporary id
                        enumVertex.put_ID(Convert.ToInt32(_temporaryIndex));
                        iDList.Add(_temporaryIndex);

                        DecrementTemporaryIndex();
                    }
                    else
                    {
                        iDList.Add(currentPoint.ID);
                    }
                }

                enumVertex.QueryNext(currentPoint, out partIndex, out vertexIndex);
            }

            return iDList;
        }

        //private List<int> detectPartChanges(IGeometry originalGeometry, IGeometry updatedGeometry)
        //{
        //    List<int> changedIndexes = new List<int>();

        //    try
        //    {
        //        IPointCollection originalPointCollection = originalGeometry as IPointCollection;
        //        IPointCollection updatedPointCollection = updatedGeometry as IPointCollection;

        //        IEnumVertex originalEnumVertex = originalPointCollection.EnumVertices;
        //        IEnumVertex updatedEnumVertex = updatedPointCollection.EnumVertices;

        //        List<long> updatedIDList = createIDList(updatedGeometry, updatedEnumVertex);
        //        List<long> originalIDList = createIDList(originalGeometry, originalEnumVertex);


        //        // get a list of IDs that both parts have in common
        //        IEnumerable<long> unionQuery = originalIDList.Union(updatedIDList);

        //        foreach (int vertexID in unionQuery)
        //        {
        //            IPoint originalPoint = originalPointCollection.get_Point(originalIDList.IndexOf(vertexID));
        //            IPoint updatedPoint = updatedPointCollection.get_Point(updatedIDList.IndexOf(vertexID));

        //            IRelationalOperator relationalOperator = originalPoint as IRelationalOperator;
        //            if (relationalOperator.Equals(updatedPoint))
        //            {
        //                // if the points are the same then we are not interested
        //            }
        //            else
        //            {
        //                // log the id if the points are not equal
        //                changedIndexes.Add(vertexID);
        //            }
        //        }
        //    }
        //    catch
        //    {
        //    }

        //    return changedIndexes;
        //}

        //private List<int> detectPartDeletes(IGeometry originalGeometry, IGeometry updatedGeometry)
        //{
        //    List<long> deletedIndexes = new List<long>();

        //    try
        //    {
        //        deletedIndexes = detectPartAdds(updatedGeometry, originalGeometry);
        //    }
        //    catch
        //    {
        //    }

        //    return deletedIndexes;
        //}

        private void storeSupportingGeometry(IGeometry supportingGeometry, IFeatureClass storeFeatureClass, long osmIdentifier, long parentOSMID, int osmIDFieldIndex, int osmVersionFieldIndex, int osmSupportElementFieldIndex, int osmIsMemberOfFieldIndex, int osmTimeStampFieldIndex, int osmTrackChangesIndex, bool trackChanges)
        {
            if (supportingGeometry == null)
                return;

            if (supportingGeometry.IsEmpty)
                return;

            if (storeFeatureClass == null)
                return;

            IFeature newSupportFeature = storeFeatureClass.CreateFeature();

            if (osmIDFieldIndex > -1)
            {
                if (_ExtensionVersion == 1)
                    newSupportFeature.set_Value(osmIDFieldIndex, Convert.ToInt32(osmIdentifier));
                else if (_ExtensionVersion == 2)
                    newSupportFeature.set_Value(osmIDFieldIndex, Convert.ToString(osmIdentifier));
            }

            if (osmVersionFieldIndex > -1)
            {
                newSupportFeature.set_Value(osmVersionFieldIndex, 1);
            }

            //if (osmTimeStampFieldIndex > -1)
            //{
            //    newSupportFeature.set_Value(osmTimeStampFieldIndex, DateTime.UtcNow);
            //}

            if (osmSupportElementFieldIndex > -1)
            {
                newSupportFeature.set_Value(osmSupportElementFieldIndex, "yes");
            }

            List<string> isMemberOfList = new List<string>();
            if (supportingGeometry is IRing)
            {
                isMemberOfList.Add(Convert.ToString(parentOSMID) + "_ply");
            }
            else if (supportingGeometry is IPath)
            {
                isMemberOfList.Add(Convert.ToString(parentOSMID) + "_ln");
            }
            else
            {
                isMemberOfList.Add(Convert.ToString(osmIdentifier));
            }

            if (osmIsMemberOfFieldIndex > -1)
            {
                _osmUtility.insertIsMemberOf(osmIsMemberOfFieldIndex, isMemberOfList, newSupportFeature);
            }

            //if (osmTrackChangesIndex > -1)
            //{
            //    if (trackChanges == false)
            //    {
            //        newSupportFeature.set_Value(osmTrackChangesIndex, 1);
            //    }
            //}

            // ensure that the new top level geometry has a spatial reference and has the awareness to deal with point ids
            IGeometryCollection newSupportGeometry = null;

            object missingValue = Missing.Value;

            // wrap the lower level geometry types into the higher level types
            if (supportingGeometry is IRing)
            {
                newSupportGeometry = new PolygonClass() as IGeometryCollection;
                ((IGeometry)newSupportGeometry).SpatialReference = supportingGeometry.SpatialReference;
                IPointIDAware pointIDAware = newSupportGeometry as IPointIDAware;
                pointIDAware.PointIDAware = true;

                newSupportGeometry.AddGeometry(supportingGeometry, ref missingValue, ref missingValue);
            }
            else if (supportingGeometry is IPath)
            {
                newSupportGeometry = new PolylineClass() as IGeometryCollection;
                ((IGeometry)newSupportGeometry).SpatialReference = supportingGeometry.SpatialReference;
                IPointIDAware pointIDAware = newSupportGeometry as IPointIDAware;
                pointIDAware.PointIDAware = true;

                newSupportGeometry.AddGeometry(supportingGeometry, ref missingValue, ref missingValue);
            }

            if (newSupportGeometry != null)
            {
                ITopologicalOperator2 topologicalOperator = newSupportGeometry as ITopologicalOperator2;

                // topologicalOperator.IsKnownSimple_2 = false;
                // topologicalOperator.Simplify();

                newSupportFeature.Shape = newSupportGeometry as IGeometry;

                newSupportFeature.Store();
            }
        }


        /// <summary>
        /// Finds the corresponding point feature class for the input feature. Since OpenStreetMap uses a node based feature notion other top
        /// level geometries (lines, polygons) need to track/create/update their vertices in the corresponding point feature class.
        /// </summary>
        /// <param name="inFeature">input feature. will be used to find the matching feature class in the same workspace.</param>
        /// <param name="typeToFind">supported geometry types. Currently those are esriGeometryPoint, esriGeometryPolygon, esriGeometryPolyline.</param>
        /// <returns>Corresponding feature class of the given type if it exists.</returns>
        public static IFeatureClass findMatchingFeatureClass(IFeature inFeature, esriGeometryType typeToFind)
        {
            IFeatureClass foundFeatureClass = null;

            try
            {
                // open it directly from the workspace
                IDataset osmDataset = inFeature.Class as IDataset;
                IFeatureWorkspace osmWorkspace = osmDataset.Workspace as IFeatureWorkspace;

                string inputFeatureClassName = ((IDataset)inFeature.Class).Name;

                int createFeatureOSMSuffix = inputFeatureClassName.IndexOf("_osm_");
                string osmFeatureClassBase = inputFeatureClassName.Substring(0, createFeatureOSMSuffix);

                switch (typeToFind)
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
                        foundFeatureClass = osmWorkspace.OpenFeatureClass(osmFeatureClassBase + "_osm_pt");
                        break;
                    case esriGeometryType.esriGeometryPolygon:
                        foundFeatureClass = osmWorkspace.OpenFeatureClass(osmFeatureClassBase + "_osm_ply");
                        break;
                    case esriGeometryType.esriGeometryPolyline:
                        foundFeatureClass = osmWorkspace.OpenFeatureClass(osmFeatureClassBase + "_osm_ln");
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
            }
            catch { }

            return foundFeatureClass;
        }



        #endregion

        #region IObjectClassInfo2 Members

        bool IObjectClassInfo2.CanBypassEditSession()
        {
            return true;
        }

        bool IObjectClassInfo2.CanBypassStoreMethod()
        {
            // we need to return false as we need track all changes, done through gp, inside or outside an edit session, engine, desktop, and server
            return m_bypassOSMChangeDetection;
        }

        #endregion

        public int supportingElementFieldIndex { get; set; }

        #region IOSMClassExtension Members

        public bool CanBypassChangeDetection
        {
            get
            {
                return m_bypassOSMChangeDetection;
            }
            set
            {
                m_bypassOSMChangeDetection = value;
            }
        }

        #endregion

        #region IObjectInspector Members

        public int HWND
        {
            get
            {
                if (m_Inspector != null)
                {
                    return (m_Inspector.HWND);
                }
                else
                    return 0;
            }
        }

        public void Clear()
        {
            if (m_Inspector != null)
            {
                m_Inspector.Clear();
            }
        }

        public void Copy(IRow row)
        {
            if (m_Inspector != null)
            {
                m_Inspector.Copy(row);
            }
        }

        public void Inspect(ArcGIS.Editor.IEnumRow enumRow, ArcGIS.Editor.IEditor editor)
        {
            if (m_Inspector != null)
            {
                m_Inspector.Inspect(enumRow, editor);
            }
        }

        #endregion

    }

    [ComVisible(true)]
    [Guid("85BF3D93-2461-46F3-8115-E162031BDC91")]
    public interface IOSMClassExtension
    {
        bool CanBypassChangeDetection { get; set; }
    }

    [ComVisible(false)]
    public class TagKeyComparer : IEqualityComparer<ESRI.ArcGIS.OSM.OSMClassExtension.tag>
    {
        #region IEqualityComparer<tag> Members

        public bool Equals(ESRI.ArcGIS.OSM.OSMClassExtension.tag x, ESRI.ArcGIS.OSM.OSMClassExtension.tag y)
        {
            bool tagsAreEqual = false;

            if (x.k.Equals(y.k, StringComparison.InvariantCulture))
            {
                tagsAreEqual = true;
            }

            return tagsAreEqual;

        }

        public int GetHashCode(ESRI.ArcGIS.OSM.OSMClassExtension.tag obj)
        {
            return obj.k.GetHashCode();
        }

        #endregion
    }

    [ComVisible(false)]
    public class TagKeyValueComparer : IEqualityComparer<ESRI.ArcGIS.OSM.OSMClassExtension.tag>
    {
        #region IEqualityComparer<tag> Members

        public bool Equals(ESRI.ArcGIS.OSM.OSMClassExtension.tag x, ESRI.ArcGIS.OSM.OSMClassExtension.tag y)
        {
            bool tagsAreEqual = false;

            if (x.k.Equals(y.k, StringComparison.InvariantCulture) && x.v.Equals(y.v, StringComparison.InvariantCulture))
            {
                tagsAreEqual = true;
            }

            return tagsAreEqual;

        }

        public int GetHashCode(ESRI.ArcGIS.OSM.OSMClassExtension.tag obj)
        {
            return (obj.k + obj.v).GetHashCode();
        }

        #endregion
    }

}
