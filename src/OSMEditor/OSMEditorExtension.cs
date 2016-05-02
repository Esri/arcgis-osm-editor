// (c) Copyright Esri, 2010 - 2016
// This source is subject to the Apache 2.0 License.
// Please see http://www.apache.org/licenses/LICENSE-2.0.html for details.
// All other rights reserved.

using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.Framework;
using ESRI.ArcGIS.ADF.CATIDs;
using ESRI.ArcGIS.Editor;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.OSM.GeoProcessing;
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.EditorExt;
using System.IO;
using System.Windows.Forms;
using System.Resources;
using System.Xml;
using System.Reflection;
using System.Linq;
using ESRI.ArcGIS.ADF;

namespace ESRI.ArcGIS.OSM.Editor
{
    [Guid("faa799f0-bdc7-4ca4-af0c-a8d591c22058")]
    [ClassInterface(ClassInterfaceType.None)]
    [ProgId("OSMEditor.OSMEditorExtension")]
    public class OSMEditorExtension : IExtension
    {
      [DllImport("user32.dll", CharSet = CharSet.Ansi)]
      private static extern int ShowWindow(int hWnd, int nCmdShow);

      private const short SW_SHOW = 5;
      private const short SW_HIDE = 0;

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
            MxExtension.Register(regKey);

        }
        /// <summary>
        /// Required method for ArcGIS Component Category unregistration -
        /// Do not modify the contents of this method with the code editor.
        /// </summary>
        private static void ArcGISCategoryUnregistration(Type registerType)
        {
            string regKey = string.Format("HKEY_CLASSES_ROOT\\CLSID\\{{{0}}}", registerType.GUID);
            MxExtension.Unregister(regKey);

        }

        #endregion
        #endregion
        private IApplication m_application;
        private IEditEvents2_Event m_editEvents2;
        private IEditEvents_Event m_editEvents;
        private IEditor3 m_editor3 = null;
        private ISpatialReference m_wgs84 = null;
        private ResourceManager resourceManager;

        private string m_osmbaseurl = String.Empty;
        private string m_osmDomainsFilePath = String.Empty;
        private string m_osmFeaturePropertiesFilePath = String.Empty;
        private bool m_isSettingsUpdateRequired = false;
        private bool m_LicenseAlertShownOnce = false;
        private ESRI.ArcGIS.Editor.IEditor m_editor = null;
        private ESRI.ArcGIS.Editor.IEnumRow m_enumRow = null;

        Dictionary<string, string> m_editorConfigurationSettings = null;

        OSMFeatureInspectorUI m_osmFeatureInspector = null;
        ESRI.ArcGIS.Editor.IObjectInspector m_inspector = null;
        ESRI.ArcGIS.OSM.OSMClassExtension.OSMUtility _osmUtility = null;

        #region IExtension Members

        /// <summary>
        /// Name of extension. Do not exceed 31 characters
        /// </summary>
        public string Name
        {
            get
            {
                return "OSMEditorExtension";
            }
        }

        public void Shutdown()
        {
            // persist osm editor specific information like the osm base url, etc.
            OSMGPFactory.StoreOSMEditorSettings(m_editorConfigurationSettings);

            m_editEvents.OnDeleteFeature -= new IEditEvents_OnDeleteFeatureEventHandler(m_editEvents_OnDeleteFeature);
            m_editEvents.OnChangeFeature -= new IEditEvents_OnChangeFeatureEventHandler(m_editEvents_OnChangeFeature);
            m_editEvents.OnCreateFeature -= new IEditEvents_OnCreateFeatureEventHandler(m_editEvents_OnCreateFeature);
            m_editEvents.OnStartEditing -= new IEditEvents_OnStartEditingEventHandler(m_editEvents_OnStartEditing);

            m_editEvents2 = null;
            m_editEvents = null;
            m_editor3 = null;
            m_application = null;
        }

        public string OSMBaseURL
        {
            get
            {
                m_editorConfigurationSettings = OSMGPFactory.ReadOSMEditorSettings();

                if (m_editorConfigurationSettings.ContainsKey("osmbaseurl") == true)
                {
                    m_osmbaseurl = m_editorConfigurationSettings["osmbaseurl"];
                }

                return m_osmbaseurl;
            }
            set
            {
                if (String.IsNullOrEmpty(value) == false)
                {
                    if (String.IsNullOrEmpty(m_osmbaseurl) == false)
                    {
                        if (value.Equals(m_osmbaseurl) == false)
                        {
                            m_osmbaseurl = value;
                            if (m_editorConfigurationSettings.ContainsKey("osmbaseurl"))
                            {
                                m_editorConfigurationSettings["osmbaseurl"] = m_osmbaseurl;
                            }
                            else
                            {
                                m_editorConfigurationSettings.Add("osmbaseurl", m_osmbaseurl);
                            }
                            OSMGPFactory.StoreOSMEditorSettings(m_editorConfigurationSettings);
                            m_isSettingsUpdateRequired = true;
                        }
                    }
                }
            }
        }

        public string OSMDomainsXmlFilePath
        {
            get
            {
                if (m_editorConfigurationSettings.ContainsKey("osmdomainsfilepath"))
                {
                    m_osmDomainsFilePath = m_editorConfigurationSettings["osmdomainsfilepath"];
                }

                return m_osmDomainsFilePath;
            }
            set
            {
                if (String.IsNullOrEmpty(value) == false)
                {
                    if (String.IsNullOrEmpty(m_osmDomainsFilePath) == false)
                    {
                        if (value.Equals(m_osmDomainsFilePath) == false)
                        {
                            m_osmDomainsFilePath = value;
                            if (m_editorConfigurationSettings.ContainsKey("osmdomainsfilepath"))
                            {
                                m_editorConfigurationSettings["osmdomainsfilepath"] = m_osmDomainsFilePath;
                            }
                            else
                            {
                                m_editorConfigurationSettings.Add("osmdomainsfilepath", m_osmDomainsFilePath);
                            }
                            OSMGPFactory.StoreOSMEditorSettings(m_editorConfigurationSettings);
                            m_isSettingsUpdateRequired = true;
                        }
                    }
                }
            }
        }

        public string OSMFeaturePropertiesXmlFilePath
        {
            get
            {
                if (m_editorConfigurationSettings.ContainsKey("osmfeaturepropertiesfilepath"))
                {
                    m_osmFeaturePropertiesFilePath = m_editorConfigurationSettings["osmfeaturepropertiesfilepath"];
                }

                return m_osmFeaturePropertiesFilePath;
            }
            set
            {
                if (String.IsNullOrEmpty(value) == false)
                {
                    if (String.IsNullOrEmpty(m_osmFeaturePropertiesFilePath) == false)
                    {
                        if (value.Equals(m_osmFeaturePropertiesFilePath) == false)
                        {
                            m_osmFeaturePropertiesFilePath = value;
                            if (m_editorConfigurationSettings.ContainsKey("osmfeaturepropertiesfilepath"))
                            {
                                m_editorConfigurationSettings["osmfeaturepropertiesfilepath"] = m_osmFeaturePropertiesFilePath;
                            }
                            else
                            {
                                m_editorConfigurationSettings.Add("osmfeaturepropertiesfilepath", m_osmFeaturePropertiesFilePath);
                            }
                            OSMGPFactory.StoreOSMEditorSettings(m_editorConfigurationSettings);
                            m_isSettingsUpdateRequired = true;
                        }
                    }
                }
            }
        }


        public bool IsSettingsUpdateRequired
        {
            get
            {
                return m_isSettingsUpdateRequired;
            }
        }

        public OSMEditorExtension()
        {
        }

        public void Startup(ref object initializationData)
        {
            try
            {
                m_application = initializationData as IApplication;
                if (m_application == null)
                    return;

                ISpatialReferenceFactory spatialReferenceFactory = new SpatialReferenceEnvironmentClass() as ISpatialReferenceFactory;
                m_wgs84 = spatialReferenceFactory.CreateGeographicCoordinateSystem((int)esriSRGeoCSType.esriSRGeoCS_WGS1984) as ISpatialReference;

                //Get the editor.
                UID editorUid = new UID();
                editorUid.Value = "esriEditor.Editor";
                m_editor3 = m_application.FindExtensionByCLSID(editorUid) as IEditor3;
                m_editEvents2 = m_editor3 as IEditEvents2_Event;
                m_editEvents = m_editor3 as IEditEvents_Event;

                m_editEvents.OnCreateFeature += new IEditEvents_OnCreateFeatureEventHandler(m_editEvents_OnCreateFeature);
                m_editEvents.OnChangeFeature += new IEditEvents_OnChangeFeatureEventHandler(m_editEvents_OnChangeFeature);
                m_editEvents.OnDeleteFeature += new IEditEvents_OnDeleteFeatureEventHandler(m_editEvents_OnDeleteFeature);
                m_editEvents.OnStartEditing += new IEditEvents_OnStartEditingEventHandler(m_editEvents_OnStartEditing);

                resourceManager = new ResourceManager("ESRI.ArcGIS.OSM.Editor.OSMFeatureInspectorStrings", this.GetType().Assembly);
                _osmUtility = new OSMClassExtension.OSMUtility();

                // retrtrieve osm editor specfic information 
                m_editorConfigurationSettings = OSMGPFactory.ReadOSMEditorSettings();
            }
            catch { }
        }

        void m_editEvents_OnStartEditing()
        {
            // only show the license alert during a session of ArcMap once
            if (testEditorContentforOSMLicense() && m_LicenseAlertShownOnce == false)
            {
                LicenseAlertDialog osmLicenseAlert = new LicenseAlertDialog();

                // show the alert about the license and remind the user about the nature of the OSM data
                if (osmLicenseAlert.ShowDialog() != DialogResult.OK)
                {
                    // if the user doesn't aggree with the statement, end the edit session
                    m_editor3.StopEditing(false);
                }

                m_LicenseAlertShownOnce = true;
            }

            // acquire an exclusive lock on the revision table as well for the current workspace as well
        }

        private bool testEditorContentforOSMLicense()
        {
            bool osmDataExists = false;

            try
            {
                IEnumLayer enumLayer = m_editor3.Map.get_Layers(null, true);
                enumLayer.Reset();

                ILayer layer = enumLayer.Next();

                while (layer != null)
                {
                    if (layer is IFeatureLayer)
                    {
                        IFeatureClass featureClass = ((IFeatureLayer)layer).FeatureClass;

                        // check if the current feature class being edited is acutally an OpenStreetMap feature class
                        // all other feature classes should not be touched by this extension
                        UID osmFeatureClassExtensionCLSID = featureClass.EXTCLSID;

                        if (osmFeatureClassExtensionCLSID != null)
                        {
                            if (osmFeatureClassExtensionCLSID.Value.ToString().Equals("{65CA4847-8661-45eb-8E1E-B2985CA17C78}", StringComparison.InvariantCultureIgnoreCase) == true)
                            {
                                osmDataExists = true;
                                break;
                            }
                        }
                    }

                    layer = enumLayer.Next();
                }
            }
            catch {}

                return osmDataExists;
        }

        /// <summary>
        /// Method to persist changes into the configuation file, if required.
        /// </summary>
        public void PersistOSMSettings()
        {
            if (m_isSettingsUpdateRequired)
            {
                OSMGPFactory.StoreOSMEditorSettings(m_editorConfigurationSettings);
            }
        }

        void m_editEvents_OnDeleteFeature(IObject obj)
        {

            // check if the deleted feature participates in a relation
            //   applies to point, lines, and polygons
            //   if it does participate in a relation ask the user if it is ok to delete

            IFeatureClass currentObjectFeatureClass = obj.Class as IFeatureClass;
            if ((currentObjectFeatureClass == null) || (currentObjectFeatureClass.EXTCLSID == null))
                return;

            // check if the current feature class being edited is acutally an OpenStreetMap feature class
            // all other feature class should not be touched by this extension
            UID osmEditorExtensionCLSID = currentObjectFeatureClass.EXTCLSID;

            if (osmEditorExtensionCLSID.Value.ToString().Equals("{65CA4847-8661-45eb-8E1E-B2985CA17C78}", StringComparison.InvariantCultureIgnoreCase) == false)
            {
                return;
            }


            // at this point we are only handling geometry types
            //   relation types are using a separate UI
            IFeature deletedFeature = obj as IFeature;
            if (deletedFeature == null)
                return;

            // block changing features that are supporting features for multi-part geometries (relations)
            if (deletedFeature.Shape is IPolygon || deletedFeature.Shape is IPolyline)
            {
                int memberOFFieldIndex = deletedFeature.Fields.FindField("osmMemberOf");
                int membersFieldIndex = deletedFeature.Fields.FindField("osmMembers");
                int osmIDFieldIndex = deletedFeature.Fields.FindField("OSMID");

                int osmID = 0;

                if (osmIDFieldIndex > -1)
                {
                    object osmIDValue = deletedFeature.get_Value(osmIDFieldIndex);

                    if (osmIDValue != DBNull.Value)
                    {
                        osmID = Convert.ToInt32(osmIDValue);
                    }
                }

                if (membersFieldIndex > -1)
                {
                    ESRI.ArcGIS.OSM.OSMClassExtension.member[] relationMembers = _osmUtility.retrieveMembers(deletedFeature, membersFieldIndex);

                    if (relationMembers != null)
                    {
                        if (relationMembers.Length > 0)
                        {
                            string abortMessage = String.Format(resourceManager.GetString("OSMEditor_FeatureInspector_multipartdeleteparentconflictmessage"), osmID);
                            MessageBox.Show(abortMessage, resourceManager.GetString("OSMEditor_FeatureInspector_relationconflictcaption"), MessageBoxButtons.OK, MessageBoxIcon.Stop);
                            m_editor3.AbortOperation();
                        }
                    }
                }


                if (memberOFFieldIndex > -1)
                {
                    List<string> isMemberOfList = _osmUtility.retrieveIsMemberOf(deletedFeature, memberOFFieldIndex);
                    Dictionary<string, string> dictofParentsAndTypes = _osmUtility.parseIsMemberOfList(isMemberOfList);

                    StringBuilder typeAndIDString = new StringBuilder();
                    foreach (var item in dictofParentsAndTypes)
                    {
                        switch (item.Value)
                        {
                            case "rel":
                                typeAndIDString.Append(resourceManager.GetString("OSMEditor_FeatureInspector_relationidtext") + item.Key + ",");
                                break;
                            case "ply":
                                typeAndIDString.Append(resourceManager.GetString("OSMEditor_FeatureInspector_polygonidtext") + item.Key + ",");
                                break;
                            case "ln":
                                typeAndIDString.Append(resourceManager.GetString("OSMEditor_FeatureInspector_polylineidtext") + item.Key + ",");
                                break;
                            case "pt":
                                typeAndIDString.Append(resourceManager.GetString("OSMEditor_FeatureInspector_pointidtext") + item.Key + ",");
                                break;
                            default:
                                break;
                        }
                    }

                    if (typeAndIDString.Length > 0)
                    {
                        string parentsString = typeAndIDString.ToString(0, typeAndIDString.Length - 1);
                        string abortMessage = String.Format(resourceManager.GetString("OSMEditor_FeatureInspector_relationsconflictmessage"), osmID, parentsString);
                        MessageBox.Show(abortMessage, resourceManager.GetString("OSMEditor_FeatureInspector_relationconflictcaption"), MessageBoxButtons.OK, MessageBoxIcon.Stop);
                        m_editor3.AbortOperation();
                        return;
                    }
                }
            }
            else if (deletedFeature.Shape is IPoint)
            {
                // if we are dealing with points to be deleted then we'll determine the connectedness via a spatial query and then the attributes indicating that 
                // the higher order feature is part of a relation
                IFeatureClass lineFeatureClass = ESRI.ArcGIS.OSM.OSMClassExtension.OpenStreetMapClassExtension.findMatchingFeatureClass(deletedFeature, esriGeometryType.esriGeometryPolyline);

                ISpatialFilter searchPointFilter = new SpatialFilterClass();
                searchPointFilter.Geometry = deletedFeature.Shape;
                searchPointFilter.SpatialRel = esriSpatialRelEnum.esriSpatialRelTouches;

                TestRelationMembership(deletedFeature, lineFeatureClass, searchPointFilter);

                IFeatureClass polygonFeatureClass = ESRI.ArcGIS.OSM.OSMClassExtension.OpenStreetMapClassExtension.findMatchingFeatureClass(deletedFeature, esriGeometryType.esriGeometryPolygon);

                TestRelationMembership(deletedFeature, polygonFeatureClass, searchPointFilter);
            }

            string featureClassName = ((IDataset)obj.Class).Name;

            // find the correspoding relation table
            int baseIndex = featureClassName.IndexOf("_osm_");
            int deleteOSMIDFieldIndex = obj.Fields.FindField("OSMID");
            int deleteIsMemberOfFieldIndex = obj.Fields.FindField("osmMemberOf");

            if (baseIndex > -1)
            {
                string relationTableName = featureClassName.Substring(0, baseIndex) + "_osm_relation";

                IFeatureWorkspace featureWorkspace = m_editor3.EditWorkspace as IFeatureWorkspace;

                ITable relationTable = featureWorkspace.OpenTable(relationTableName);
                int relationOSMIDFieldIndex = relationTable.Fields.FindField("OSMID");

                List<string> memberOfList = _osmUtility.retrieveIsMemberOf(deletedFeature, deleteIsMemberOfFieldIndex);

                Dictionary<string, string> isMemberOfIdsAndTypes = _osmUtility.parseIsMemberOfList(memberOfList);

                if (memberOfList.Count > 0)
                {
                    // the deleted feature is referenced by a relation
                    // check with the user if it is ok to delete
                    // if OK then we are dealing with the delete upon stop editing, if cancel undo the delete
                    string relationsString = String.Empty;
                    int relationCount = 0;
                    foreach (var memberOfItem in isMemberOfIdsAndTypes)
                    {
                        if (memberOfItem.Value == "rel")
                        {
                            relationCount = relationCount + 1;
                            relationsString = relationsString + memberOfItem.Key + ",";
                        }
                    }

                    string errorMessage = String.Empty;

                    if (relationCount > 1)
                    {
                        errorMessage = string.Format(resourceManager.GetString("OSMEditor_FeatureInspector_relationsconflictmessage"), deletedFeature.get_Value(deleteOSMIDFieldIndex), relationsString.Substring(0, relationsString.Length - 1));
                    }
                    else
                    {
                        errorMessage = string.Format(resourceManager.GetString("OSMEditor_FeatureInspector_relationconflictmessage"), deletedFeature.get_Value(deleteOSMIDFieldIndex), relationsString.Substring(0, relationsString.Length - 1));
                    }

                    if (MessageBox.Show(errorMessage, resourceManager.GetString("OSMEditor_FeatureInspector_relationconflictcaption"), MessageBoxButtons.OKCancel, MessageBoxIcon.Warning) == DialogResult.Cancel)
                    {
                        m_editor3.AbortOperation();
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="deletedFeature"></param>
        /// <param name="testFeatureClass"></param>
        /// <param name="searchPointFilter"></param>
        private void TestRelationMembership(IFeature deletedFeature, IFeatureClass testFeatureClass, ISpatialFilter searchPointFilter)
        {
            using (ESRI.ArcGIS.OSM.OSMClassExtension.ComReleaser comReleaser = new ESRI.ArcGIS.OSM.OSMClassExtension.ComReleaser())
            {
                int testMemberOfFieldIndex = testFeatureClass.Fields.FindField("osmMemberOf");
                int testMembersFieldIndex = testFeatureClass.Fields.FindField("osmMembers");
                int testOsmIDFieldIndex = testFeatureClass.Fields.FindField("OSMID");

                IFeatureCursor searchCursor = testFeatureClass.Search(searchPointFilter, false);
                comReleaser.ManageLifetime(searchCursor);

                IFeature touchedFeature = searchCursor.NextFeature();

                while (touchedFeature != null)
                {
                    long osmID = 0;

                    if (testOsmIDFieldIndex > -1)
                    {
                        object osmIDValue = touchedFeature.get_Value(testOsmIDFieldIndex);

                        if (osmIDValue != DBNull.Value)
                        {
                            osmID = Convert.ToInt64(osmIDValue);
                        }
                    }

                    if (testMembersFieldIndex > -1)
                    {
                        ESRI.ArcGIS.OSM.OSMClassExtension.member[] relationMembers = _osmUtility.retrieveMembers(touchedFeature, testMembersFieldIndex);

                        if (relationMembers != null)
                        {
                            if (relationMembers.Length > 0)
                            {
                                string abortMessage = String.Format(resourceManager.GetString("OSMEditor_FeatureInspector_pointmemberofrelation"), osmID);
                                MessageBox.Show(abortMessage, resourceManager.GetString("OSMEditor_FeatureInspector_relationconflictcaption"), MessageBoxButtons.OK, MessageBoxIcon.Stop);
                                m_editor3.AbortOperation();
                                return;
                            }
                        }
                    }


                    if (testMemberOfFieldIndex > -1)
                    {
                        List<string> isMemberOfList = _osmUtility.retrieveIsMemberOf(touchedFeature, testMemberOfFieldIndex);
                        Dictionary<string, string> dictofParentsAndTypes = _osmUtility.parseIsMemberOfList(isMemberOfList);

                        StringBuilder typeAndIDString = new StringBuilder();
                        foreach (var item in dictofParentsAndTypes)
                        {
                            switch (item.Value)
                            {
                                case "rel":
                                    typeAndIDString.Append(resourceManager.GetString("OSMEditor_FeatureInspector_relationidtext") + item.Key + ",");
                                    break;
                                case "ply":
                                    typeAndIDString.Append(resourceManager.GetString("OSMEditor_FeatureInspector_polygonidtext") + item.Key + ",");
                                    break;
                                case "ln":
                                    typeAndIDString.Append(resourceManager.GetString("OSMEditor_FeatureInspector_polylineidtext") + item.Key + ",");
                                    break;
                                case "pt":
                                    typeAndIDString.Append(resourceManager.GetString("OSMEditor_FeatureInspector_pointidtext") + item.Key + ",");
                                    break;
                                default:
                                    break;
                            }
                        }

                        if (typeAndIDString.Length > 0)
                        {
                            string parentsString = typeAndIDString.ToString(0, typeAndIDString.Length - 1);
                            string abortMessage = String.Format(resourceManager.GetString("OSMEditor_FeatureInspector_pointmemberofrelation"), parentsString);
                            MessageBox.Show(abortMessage, resourceManager.GetString("OSMEditor_FeatureInspector_relationconflictcaption"), MessageBoxButtons.OK, MessageBoxIcon.Stop);
                            m_editor3.AbortOperation();
                            return;
                        }
                    }

                    touchedFeature = searchCursor.NextFeature();
                }

            }
        }

        void m_editEvents_OnChangeFeature(IObject obj)
        {
            // check if feature contains more than 2000 nodes/vertices
            //  applies to lines and polygons
            //  notify the user and offer a split
            // check if feature geometry is multi-part
            //  applies to lines and polygons
            //  notify the user and offer a conversion to relation

            IFeatureClass currentObjectFeatureClass = obj.Class as IFeatureClass;
            if ((currentObjectFeatureClass == null) || (currentObjectFeatureClass.EXTCLSID == null))
                return;

            // check if the current feature class being edited is acutally an OpenStreetMap feature class
            // all other feature class should not be touched by this extension
            UID osmEditorExtensionCLSID = currentObjectFeatureClass.EXTCLSID;

            if (osmEditorExtensionCLSID.Value.ToString().Equals("{65CA4847-8661-45eb-8E1E-B2985CA17C78}", StringComparison.InvariantCultureIgnoreCase) == false)
            {
                return;
            }

            IFeature currentFeature = obj as IFeature;
            if (currentFeature == null)
                return;

            IPointCollection pointCollection = currentFeature.Shape as IPointCollection;

            if (pointCollection == null)
            {
                return;
            }

            // block changing features that are supporting features for multi-part geometries (relations)
            if (currentFeature.Shape is IPolygon || currentFeature.Shape is IPolyline)
            {
                if (((IFeatureChanges)currentFeature).ShapeChanged == true)
                {
                    int memberOFFieldIndex = currentFeature.Fields.FindField("osmMemberOf");
                    int membersFieldIndex = currentFeature.Fields.FindField("osmMembers");
                    int osmIDFieldIndex = currentFeature.Fields.FindField("OSMID");

                    long osmID = 0;

                    if (osmIDFieldIndex > -1)
                    {
                        object osmIDValue = currentFeature.get_Value(osmIDFieldIndex);

                        if (osmIDValue != DBNull.Value)
                        {
                            osmID = Convert.ToInt64(osmIDValue);
                        }
                    }

                    if (membersFieldIndex > -1)
                    {
                        ESRI.ArcGIS.OSM.OSMClassExtension.member[] relationMembers = _osmUtility.retrieveMembers(currentFeature, membersFieldIndex);

                        if (relationMembers != null)
                        {
                            if (relationMembers.Length > 0)
                            {
                                string abortMessage = String.Format(resourceManager.GetString("OSMEditor_FeatureInspector_multipartchangeparentconflictmessage"), osmID);
                                MessageBox.Show(abortMessage, resourceManager.GetString("OSMEditor_FeatureInspector_relationconflictcaption"), MessageBoxButtons.OK, MessageBoxIcon.Stop);
                                m_editor3.AbortOperation();
                            }
                        }
                    }

                    if (memberOFFieldIndex > -1)
                    {
                        List<string> isMemberOfList = _osmUtility.retrieveIsMemberOf(currentFeature, memberOFFieldIndex);
                        Dictionary<string, string> dictofParentsAndTypes = _osmUtility.parseIsMemberOfList(isMemberOfList);

                        StringBuilder typeAndIDString = new StringBuilder();
                        foreach (var item in dictofParentsAndTypes)
                        {
                            switch (item.Value)
                            {
                                case "rel":
                                    typeAndIDString.Append(resourceManager.GetString("OSMEditor_FeatureInspector_relationidtext") + item.Key + ",");
                                    break;
                                case "ply":
                                    typeAndIDString.Append(resourceManager.GetString("OSMEditor_FeatureInspector_polygonidtext") + item.Key + ",");
                                    break;
                                case "ln":
                                    typeAndIDString.Append(resourceManager.GetString("OSMEditor_FeatureInspector_polylineidtext") + item.Key + ",");
                                    break;
                                case "pt":
                                    typeAndIDString.Append(resourceManager.GetString("OSMEditor_FeatureInspector_pointidtext") + item.Key + ",");
                                    break;
                                default:
                                    break;
                            }
                        }

                        if (typeAndIDString.Length > 0)
                        {
                            string parentsString = typeAndIDString.ToString(0, typeAndIDString.Length - 1);
                            string abortMessage = String.Format(resourceManager.GetString("OSMEditor_FeatureInspector_multipartchangeconflictmessage"), osmID, parentsString);
                            MessageBox.Show(abortMessage, resourceManager.GetString("OSMEditor_FeatureInspector_relationconflictcaption"), MessageBoxButtons.OK, MessageBoxIcon.Stop);
                            m_editor3.AbortOperation();
                        }
                    }
                }
            }

            ISegmentCollection segmentCollection = currentFeature.Shape as ISegmentCollection;
            bool densifyRequired = false;

            for (int segmentIndex = 0; segmentIndex < segmentCollection.SegmentCount; segmentIndex++)
            {
                ISegment segment = segmentCollection.get_Segment(segmentIndex);

                if (!(segment is Line))
                {
                    densifyRequired = true;
                    break;
                }
            }


            if (densifyRequired)
            {
                IGeometryEnvironment4 geometryEnvironment = new GeometryEnvironmentClass() as IGeometryEnvironment4;

                double densifyTolerance = geometryEnvironment.AutoDensifyTolerance;
                double deviationTolerance = geometryEnvironment.DeviationAutoDensifyTolerance;

                IPolycurve polycurve = currentFeature.Shape as IPolycurve;
                polycurve.Densify(densifyTolerance, deviationTolerance);

                currentFeature.Shape = polycurve;

                obj.Store();
            }
        }

        void m_editEvents_OnCreateFeature(IObject obj)
        {
            // check if feature contains more than 2000 nodes/vertices
            //  applies to lines and polygons
            //  notify the user and offer a split
            // check if feature geometry is multi-part
            //  applies to lines and polygons
            //  notify the user and offer a conversion to relation

            IFeatureClass currentObjectFeatureClass = obj.Class as IFeatureClass;
            if ((currentObjectFeatureClass == null) || (currentObjectFeatureClass.EXTCLSID == null))
                return;

            // check if the current feature class being edited is acutally an OpenStreetMap feature class
            // all other feature class should not be touched by this extension
            UID osmEditorExtensionCLSID = currentObjectFeatureClass.EXTCLSID;

            if (osmEditorExtensionCLSID.Value.ToString().Equals("{65CA4847-8661-45eb-8E1E-B2985CA17C78}", StringComparison.InvariantCultureIgnoreCase) == false)
            {
                return;
            }
            
            IFeature currentFeature = obj as IFeature;
            if (currentFeature == null)
                return;


            ISegmentCollection segmentCollection = currentFeature.Shape as ISegmentCollection;
            bool densifyRequired = false;

            if (segmentCollection != null)
            {
                for (int segmentIndex = 0; segmentIndex < segmentCollection.SegmentCount; segmentIndex++)
                {
                    ISegment segment = segmentCollection.get_Segment(segmentIndex);

                    if (!(segment is Line))
                    {
                        densifyRequired = true;
                        break;
                    }
                }
            }


            if (densifyRequired)
            {
                IGeometryEnvironment4 geometryEnvironment = new GeometryEnvironmentClass() as IGeometryEnvironment4;

                double densifyTolerance = geometryEnvironment.AutoDensifyTolerance;
                double deviationTolerance = geometryEnvironment.DeviationAutoDensifyTolerance;

                IPolycurve polycurve = currentFeature.Shape as IPolycurve;
                polycurve.Densify(densifyTolerance, deviationTolerance);

                currentFeature.Shape = polycurve;

                obj.Store();
            }
        }

        #endregion

        #region IObjectInspector Members

        public void Clear()
        {
          if (m_inspector == null)
          {
            m_inspector = new FeatureInspectorClass();
          }

          m_inspector.Clear();
        }

        public void Copy(ESRI.ArcGIS.Geodatabase.IRow srcRow)
        {
          if (m_inspector == null)
          {
            m_inspector = new FeatureInspectorClass();
          }

          m_inspector.Copy(srcRow);
        }

        public int HWND
        {
          get
          {
            if (m_osmFeatureInspector == null)
            {
              m_osmFeatureInspector = new OSMFeatureInspectorUI();
            }

            return m_osmFeatureInspector.Handle.ToInt32();
          }
        }

        public int tabHwnd
        {
          get
          {
            if (m_osmFeatureInspector == null)
            {
              m_osmFeatureInspector = new OSMFeatureInspectorUI();
            }

            return m_osmFeatureInspector.Handle.ToInt32();
          }
        }

        public void Inspect(ESRI.ArcGIS.Editor.IEnumRow objects, ESRI.ArcGIS.Editor.IEditor Editor)
        {
          try
          {
            if (m_osmFeatureInspector == null)
            {
              m_osmFeatureInspector = new OSMFeatureInspectorUI();
            }

            if (m_inspector == null)
            {
              m_inspector = new FeatureInspectorClass();
            }

            ShowWindow(m_inspector.HWND, SW_SHOW);
            m_inspector.Inspect(objects, Editor);

            if (Editor == null)
            {
              return;
            }

            if (objects == null)
            {
              return;
            }

            if (m_osmFeatureInspector.IsInitialized == false)
              m_osmFeatureInspector.Init(Editor);

            m_editor = Editor;
            m_enumRow = objects;

            IEnumFeature enumFeatures = Editor.EditSelection;
            enumFeatures.Reset();

            int featureCount = 0;

            while (enumFeatures.Next() != null)
            {
              featureCount = featureCount + 1;
            }

            IEnumRow enumRow = objects;
            enumRow.Reset();
            IRow row = enumRow.Next();

            IFeature inspFeature = (IFeature)row;

            //user selected the layer name instead of a feature.
            if (objects.Count > 1)
            {
              m_osmFeatureInspector.prepareGrid4Features(objects);
            }
            else
            {
              m_osmFeatureInspector.prepareGrid4Feature(inspFeature);
            }

            m_osmFeatureInspector.currentlyEditedRows = enumRow;
          }
          catch (Exception ex)
          {
            if (m_osmFeatureInspector != null)
            {
              m_osmFeatureInspector.ClearGrid();
            }

            System.Diagnostics.Debug.WriteLine(ex.Message);
          }

        }

        #endregion

    }

}
