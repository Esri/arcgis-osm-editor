// (c) Copyright Esri, 2010 - 2016
// This source is subject to the Apache 2.0 License.
// Please see http://www.apache.org/licenses/LICENSE-2.0.html for details.
// All other rights reserved.

using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using ESRI.ArcGIS.ADF.CATIDs;
using ESRI.ArcGIS.ADF.BaseClasses;
using ESRI.ArcGIS.SystemUI;
using System.Resources;

namespace ESRI.ArcGIS.OSM.Editor
{
    /// <summary>
    /// Summary description for OSMEditorToolbar.
    /// </summary>
    [Guid("980ae216-f181-4fe7-95b7-6c0a7342f71b")]
    [ClassInterface(ClassInterfaceType.None)]
    [ProgId("OSMEditor.OSMEditorToolbar")]
    public sealed class OSMEditorToolbar : BaseToolbar
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
            MxCommandBars.Register(regKey);
        }
        /// <summary>
        /// Required method for ArcGIS Component Category unregistration -
        /// Do not modify the contents of this method with the code editor.
        /// </summary>
        private static void ArcGISCategoryUnregistration(Type registerType)
        {
            string regKey = string.Format("HKEY_CLASSES_ROOT\\CLSID\\{{{0}}}", registerType.GUID);
            MxCommandBars.Unregister(regKey);
        }

        #endregion
        #endregion

        ResourceManager resourceManager = null;

        public OSMEditorToolbar()
        {
            resourceManager = new ResourceManager("ESRI.ArcGIS.OSM.Editor.OSMFeatureInspectorStrings", this.GetType().Assembly);

            AddItem("{93e976c7-b3a2-4b0f-a575-6626358614e3}");
            //BeginGroup(); //Separator

        }

        public override string Caption
        {
            get
            {
                String toolbarCaption = String.Empty;

                if (resourceManager != null)
                {
                    toolbarCaption = resourceManager.GetString("OSMEditor_OSMToolbar_Caption");
                }

                return toolbarCaption;
            }
        }
        public override string Name
        {
            get
            {
                return "OSMEditor_OSMEditorToolbar";
            }
        }
    }
}