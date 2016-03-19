// (c) Copyright Esri, 2010 - 2016
// This source is subject to the Apache 2.0 License.
// Please see http://www.apache.org/licenses/LICENSE-2.0.html for details.
// All other rights reserved.


using System;
using System.Drawing;
using System.Runtime.InteropServices;
using ESRI.ArcGIS.ADF.BaseClasses;
using ESRI.ArcGIS.ADF.CATIDs;
using ESRI.ArcGIS.Framework;
using ESRI.ArcGIS.ArcMapUI;
using System.Resources;
using System.Windows.Forms;
using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.Editor;

namespace ESRI.ArcGIS.OSM.Editor
{
    /// <summary>
    /// Summary description for OSMConflictEditor.
    /// </summary>
    [Guid("93e976c7-b3a2-4b0f-a575-6626358614e3")]
    [ClassInterface(ClassInterfaceType.None)]
    [ProgId("OSMEditor.OSMConflictEditor")]
    public sealed class OSMConflictEditor : BaseCommand
    {
        ResourceManager resourceManager = null;
        OSMConflictEditorUI m_osmConflictEditorUI = null;

        private IApplication m_application;
        public OSMConflictEditor()
        {

            resourceManager = new ResourceManager("ESRI.ArcGIS.OSM.Editor.OSMFeatureInspectorStrings", this.GetType().Assembly);


            base.m_category = "OSMEditor"; //localizable text
            base.m_caption = "OSM Conflict Editor";  //localizable text
            base.m_message = "Open the OpenStreetMap Conflict Editor";  //localizable text 
            base.m_toolTip = "Open the OpenStreetMap Conflict Editor";  //localizable text 
            base.m_name = "OSMEditor_ConflictEditor";   //unique id, non-localizable (e.g. "MyCategory_ArcMapCommand")

            try
            {
                string bitmapResourceName = GetType().Name + ".bmp";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine(ex.Message, "Invalid Bitmap");
            }

            try
            {
                m_osmConflictEditorUI = new OSMConflictEditorUI();
            }
            catch
            {
            }
        }

        #region Overridden Class Methods

        public override string Category
        {
            get
            {
                return "OSMEditor";
            }
        }

        /// <summary>
        /// Occurs when this command is created
        /// </summary>
        /// <param name="hook">Instance of the application</param>
        public override void OnCreate(object hook)
        {
            if (hook == null)
                return;

            m_application = hook as IApplication;

            //Disable if it is not ArcMap
            if (hook is IMxApplication)
                base.m_enabled = true;
            else
                base.m_enabled = false;

            base.m_category = resourceManager.GetString("OSMEditor_ConflictEditor_category");
            base.m_caption = resourceManager.GetString("OSMEditor_ConflictEditor_caption");
            base.m_message = resourceManager.GetString("OSMEditor_ConflictEditor_message");
            base.m_toolTip = resourceManager.GetString("OSMEditor_ConflictEditor_tooltip");

            if (m_osmConflictEditorUI != null)
            {
                m_osmConflictEditorUI.Text = resourceManager.GetString("OSMEditor_ConflictEditor_formtitle");
            }

            UID editorUID = new UIDClass();  
            editorUID.Value = "esriEditor.Editor";
            IEditor editor = m_application.FindExtensionByCLSID(editorUID) as IEditor;

            base.m_enabled = false;

            if (editor != null)
            {
                IEditEvents_Event editorEvents = editor as IEditEvents_Event;

                if (editorEvents != null)
                {
                    editorEvents.OnStartEditing += new IEditEvents_OnStartEditingEventHandler(editorEvents_OnStartEditing);
                    editorEvents.OnStopEditing += new IEditEvents_OnStopEditingEventHandler(editorEvents_OnStopEditing);
                }

                if (editor.EditState != esriEditState.esriStateNotEditing)
                {
                    base.m_enabled = true;
                }
            }
        }

        void editorEvents_OnStopEditing(bool save)
        {
            base.m_enabled = false;
        }

        void editorEvents_OnStartEditing()
        {
            base.m_enabled = true;
        }

        /// <summary>
        /// Occurs when this command is clicked
        /// </summary>
        public override void OnClick()
        {
            m_osmConflictEditorUI.FocusMap = ((IMxDocument)m_application.Document).FocusMap;
            m_osmConflictEditorUI.defaultSymbols = m_application.Document as IDocumentDefaultSymbols;


            UID osmEditorExtensionCLSID = new UIDClass();
            osmEditorExtensionCLSID.Value = "{faa799f0-bdc7-4ca4-af0c-a8d591c22058}";
            OSMEditorExtension osmEditorExtension = m_application.FindExtensionByCLSID(osmEditorExtensionCLSID) as OSMEditorExtension;

            if (osmEditorExtension != null)
            {
                m_osmConflictEditorUI.OSMBaseURL = osmEditorExtension.OSMBaseURL;
            }

            UID editorUID = new UIDClass();
            editorUID.Value = "esriEditor.Editor";
            IEditor editor = m_application.FindExtensionByCLSID(editorUID) as IEditor;

            if (editor != null)
            {
                m_osmConflictEditorUI.Editor = editor;
            }

            IMouseCursor mouseCursor = new MouseCursorClass();
            mouseCursor.SetCursor(2);

            m_osmConflictEditorUI.ShowDialog(new WindowWrapper(new IntPtr(m_application.hWnd)));
        }

        #endregion
    }

    [ComVisible(false)]
    public class WindowWrapper : IWin32Window
    {
        private IntPtr _hwnd;

        public WindowWrapper(IntPtr handle)
        {
            _hwnd = handle;
        }

        public IntPtr Handle
        {
            get { return _hwnd; }
        }
    }
}
