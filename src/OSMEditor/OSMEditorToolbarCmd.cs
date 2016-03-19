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
using ESRI.ArcGIS.esriSystem;
using System.Resources;

namespace ESRI.ArcGIS.OSM.Editor
{
    /// <summary>
    /// Summary description for OSMEditorToolbarCmd.
    /// </summary>
    [Guid("e5aa39c1-ded2-4eb5-8b57-0d09e7413b31")]
    [ClassInterface(ClassInterfaceType.None)]
    [ProgId("OSMEditor.OSMEditorToolbarCmd")]
    public sealed class OSMEditorToolbarCmd : BaseCommand
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
            EditorToolbars.Register(regKey);

        }
        /// <summary>
        /// Required method for ArcGIS Component Category unregistration -
        /// Do not modify the contents of this method with the code editor.
        /// </summary>
        private static void ArcGISCategoryUnregistration(Type registerType)
        {
            string regKey = string.Format("HKEY_CLASSES_ROOT\\CLSID\\{{{0}}}", registerType.GUID);
            EditorToolbars.Unregister(regKey);

        }

        #endregion
        #endregion

        private IApplication m_application = null;
        private ResourceManager resourceManager = null;
        string commandCaption = "OpenStreetMap";
        string commandMessage = "Open/Close OSM toolbar";
        string commandTooltip = "Open/Close OSM toolbar";

        public OSMEditorToolbarCmd()
        {

            resourceManager = new ResourceManager("ESRI.ArcGIS.OSM.Editor.OSMFeatureInspectorStrings", this.GetType().Assembly);

            try
            {
                commandCaption = resourceManager.GetString("OSMEditor_OSMToolbarCmd_caption");
                commandMessage = resourceManager.GetString("OSMEditor_OSMToolbarCmd_message");
                commandTooltip = resourceManager.GetString("OSMEditor_OSMToolbarCmd_tooltip");
            }
            catch { }
            base.m_category = "OSMEditor"; //localizable text
            base.m_caption = commandCaption;  //localizable text
            base.m_message = commandMessage;  //localizable text 
            base.m_toolTip = commandTooltip;  //localizable text 
            base.m_name = "OSMEditor_OSMEditorToolbarCmd";   //unique id, non-localizable (e.g. "MyCategory_ArcMapCommand")
        }

        #region Overridden Class Methods

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
        }

        /// <summary>
        /// Occurs when this command is clicked
        /// </summary>
        public override void OnClick()
        {
            if (m_application == null)
                return;

            UID osmToolbarCLSID = new UIDClass();
            osmToolbarCLSID.Value = "{980ae216-f181-4fe7-95b7-6c0a7342f71b}";

            ICommandBars commandBars = m_application.Document.CommandBars;
            ICommandItem commandItem = commandBars.Find(osmToolbarCLSID, true, false);

            ICommandBar osmCommandBar = commandItem as ICommandBar;

            if (osmCommandBar != null)
            {
                osmCommandBar.Dock(esriDockFlags.esriDockToggle, null);
            }
        }

        public override bool Checked
        {
            get
            {
                bool osmToolbarVisibility = false;

                if (m_application == null)
                    return osmToolbarVisibility;

                UID osmToolbarCLSID = new UIDClass();
                osmToolbarCLSID.Value = "{980ae216-f181-4fe7-95b7-6c0a7342f71b}";

                ICommandBars commandBars = m_application.Document.CommandBars;
                ICommandItem commandItem = commandBars.Find(osmToolbarCLSID, true, true);

                ICommandBar osmCommandBar = commandItem as ICommandBar;

                if (osmCommandBar != null)
                {
                    osmToolbarVisibility = osmCommandBar.IsVisible();
                }
                return osmToolbarVisibility;
            }
        }

        #endregion
    }
}
