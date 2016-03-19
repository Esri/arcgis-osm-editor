// (c) Copyright Esri, 2010 - 2016
// This source is subject to the Apache 2.0 License.
// Please see http://www.apache.org/licenses/LICENSE-2.0.html for details.
// All other rights reserved.


using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using ESRI.ArcGIS.Framework;
using ESRI.ArcGIS.ADF.CATIDs;
using System.Resources;
using ESRI.ArcGIS.Editor;
using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.CatalogUI;
using ESRI.ArcGIS.Catalog;

namespace ESRI.ArcGIS.OSM.Editor
{
    /// <summary>
    /// Generic property page implementation for ArcGIS Desktop
    /// </summary>
    [Guid("6c210462-106a-4ca9-8b3e-fd190468fe6b")]
    [ClassInterface(ClassInterfaceType.None)]
    [ProgId("OSMEditor.OSMEditorPropertyPage")]
    public partial class OSMEditorPropertyPage : UserControl, IComPropertyPage
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
            EditorPropertyPages.Register(regKey);

        }
        /// <summary>
        /// Required method for ArcGIS Component Category unregistration -
        /// Do not modify the contents of this method with the code editor.
        /// </summary>
        private static void ArcGISCategoryUnregistration(Type registerType)
        {
            string regKey = string.Format("HKEY_CLASSES_ROOT\\CLSID\\{{{0}}}", registerType.GUID);
            EditorPropertyPages.Unregister(regKey);

        }

        #endregion
        #endregion

        private string m_pageTitle;

        private bool m_dirtyFlag = false;
        private IComPropertyPageSite m_pageSite = null;
        private Dictionary<string, object> m_objectBag;
        private ResourceManager resourceManager = null;
        private IEditor m_editor = null;
        string m_osmbaseURL = String.Empty;

        public OSMEditorPropertyPage()
        {
            InitializeComponent();

            resourceManager = new ResourceManager("ESRI.ArcGIS.OSM.Editor.OSMFeatureInspectorStrings", this.GetType().Assembly);

            //TODO: Modify property page title
            m_pageTitle = resourceManager.GetString("OSMEditor_OSMPropertyPage_tabtitle");
            lblBaseURL.Text = resourceManager.GetString("OSMEditor_OSMPropertyPage_labelBaseURL");
            lblFeatureDomains.Text = resourceManager.GetString("OSMEditor_OSMPropertyPage_labelOSMDomains");
            lblFeatureProperties.Text = resourceManager.GetString("OSMEditor_OSMPropertyPage_labelOSMFeatures");
        }

        /// <summary>
        /// Call this to set dirty flag whenever changes are made to the UI/page
        /// </summary>
        private void SetPageDirty(bool dirty)
        {
            if (m_dirtyFlag != dirty)
            {
                m_dirtyFlag = dirty;
                if (m_pageSite != null)
                    m_pageSite.PageChanged();
            }
        }

        #region IComPropertyPage Members

        string IComPropertyPage.Title
        {
            get
            {
                return m_pageTitle;
            }
            set
            {
                //Uncomment if title can be modified
                //m_pageTitle = value;
            }
        }

        int IComPropertyPage.Width
        {
            get { return this.Width; }
        }

        int IComPropertyPage.Height
        {
            get { return this.Height; }
        }

        int IComPropertyPage.Activate()
        {
            // get the OSM extension and retrieve the base url
            if (m_editor != null)
            {
                IExtensionManager extensionManager = m_editor as IExtensionManager;

                //for (int index = 0; index < extensionManager.ExtensionCount; index++)
                //{
                //    System.Diagnostics.Debug.WriteLine(extensionManager.Extension[index].Name);
                //}

                UID osmEditorExtensionCLSID = new UIDClass();
                osmEditorExtensionCLSID.Value = "{faa799f0-bdc7-4ca4-af0c-a8d591c22058}";
                OSMEditorExtension osmEditorExtension = m_editor.Parent.FindExtensionByCLSID(osmEditorExtensionCLSID) as OSMEditorExtension;

                if (osmEditorExtension != null)
                {
                    m_osmbaseURL = osmEditorExtension.OSMBaseURL;
                    txtOSMBaseURL.Text = m_osmbaseURL;
                    txtOSMDomainFileLocation.Text = osmEditorExtension.OSMDomainsXmlFilePath;
                    txtOSMFeaturePropertiesFileLocation.Text = osmEditorExtension.OSMFeaturePropertiesXmlFilePath;
                }
            }

            txtOSMBaseURL.Focus();
            m_dirtyFlag = false;

            return this.Handle.ToInt32();

        }

        void IComPropertyPage.Deactivate()
        {
            m_objectBag = null;
            this.Dispose(true);
        }

        /// <summary>
        /// Indicates if the page applies to the specified objects
        /// Do not hold on to the objects here.
        /// </summary>
        bool IComPropertyPage.Applies(ESRI.ArcGIS.esriSystem.ISet objects)
        {
            if (objects == null || objects.Count == 0)
                return false;

            bool isEditable = false;
            objects.Reset();
            object testObject;
            while ((testObject = objects.Next()) != null)
            {
                if (testObject != null)
                {
                    isEditable = true;
                }
            }

            return isEditable;
        }

        /// <summary>
        /// Supplies the page with the object(s) to be edited
        /// </summary>
        void IComPropertyPage.SetObjects(ESRI.ArcGIS.esriSystem.ISet objects)
        {
            if (objects == null || objects.Count == 0)
                return;

            //Prepare to hold on to editable objects
            if (m_objectBag == null)
                m_objectBag = new Dictionary<string, object>();
            else
                m_objectBag.Clear();

            objects.Reset();
            object testObject;
            while ((testObject = objects.Next()) != null)
            {
                if (testObject != null)
                {
                    m_editor = testObject as IEditor;
                    break;
                }
            }

            if (m_editor != null)
            {
                IExtensionManager extensionManager = m_editor as IExtensionManager;

                //for (int index = 0; index < extensionManager.ExtensionCount; index++)
                //{
                //    System.Diagnostics.Debug.WriteLine(extensionManager.Extension[index].Name);
                //}

                UID osmEditorExtensionCLSID = new UIDClass();
                osmEditorExtensionCLSID.Value = "{faa799f0-bdc7-4ca4-af0c-a8d591c22058}";
                OSMEditorExtension osmEditorExtension = m_editor.Parent.FindExtensionByCLSID(osmEditorExtensionCLSID) as OSMEditorExtension;

                if (osmEditorExtension != null)
                {
                    m_osmbaseURL = osmEditorExtension.OSMBaseURL;
                }
            }
        }

        IComPropertyPageSite IComPropertyPage.PageSite
        {
            set
            {
                m_pageSite = value;
            }
        }

        /// <summary>
        /// Indicates if the Apply button should be enabled
        /// </summary>
        bool IComPropertyPage.IsPageDirty
        {
            get { return m_dirtyFlag; }
        }

        void IComPropertyPage.Apply()
        {
            if (m_dirtyFlag)
            {
                if (m_editor != null)
                {
                    IExtensionManager extensionManager = m_editor as IExtensionManager;

                    //for (int index = 0; index < extensionManager.ExtensionCount; index++)
                    //{
                    //    System.Diagnostics.Debug.WriteLine(extensionManager.Extension[index].Name);
                    //}

                    UID osmEditorExtensionCLSID = new UIDClass();
                    osmEditorExtensionCLSID.Value = "{faa799f0-bdc7-4ca4-af0c-a8d591c22058}";
                    OSMEditorExtension osmEditorExtension = m_editor.Parent.FindExtensionByCLSID(osmEditorExtensionCLSID) as OSMEditorExtension;

                    if (osmEditorExtension != null)
                    {
                        osmEditorExtension.OSMBaseURL = m_osmbaseURL;
                        osmEditorExtension.OSMDomainsXmlFilePath = txtOSMDomainFileLocation.Text;
                        osmEditorExtension.OSMFeaturePropertiesXmlFilePath = txtOSMFeaturePropertiesFileLocation.Text;

                        // makes the changes persisting throughout
                        osmEditorExtension.PersistOSMSettings();
                    }
                }

                SetPageDirty(false);

            }
        }

        void IComPropertyPage.Cancel()
        {
            if (m_dirtyFlag)
            {
                SetPageDirty(false);
            }
        }

        void IComPropertyPage.Show()
        {
            if (String.IsNullOrEmpty(m_osmbaseURL) == false)
            {
                txtOSMBaseURL.Text = m_osmbaseURL;
                m_dirtyFlag = false;
            }
        }

        void IComPropertyPage.Hide()
        {
        }

        string IComPropertyPage.HelpFile
        {
            get { return string.Empty; }
        }

        int IComPropertyPage.get_HelpContextID(int controlID)
        {
            return 0;
        }

        int IComPropertyPage.Priority
        {
            get
            {
                return 0;
            }
            set
            {
            }
        }

        #endregion

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            m_osmbaseURL = txtOSMBaseURL.Text;
            SetPageDirty(true);
        }

        private void btnFeatureDomains_Click(object sender, EventArgs e)
        {
            try
            {
                bool isDomain = false;

                if (sender.Equals(btnFeatureDomains))
                {
                    isDomain = true;
                }

                IGxDialog xmlFileSelectDialog = new GxDialogClass();
                xmlFileSelectDialog.AllowMultiSelect = false;
                if (isDomain)
                {
                    xmlFileSelectDialog.Title = resourceManager.GetString("OSMEditor_OSMPropertyPage_fileselect_title_domain");
                }
                else
                {
                    xmlFileSelectDialog.Title = resourceManager.GetString("OSMEditor_OSMPropertyPage_fileselect_title_feature");

                }
                xmlFileSelectDialog.RememberLocation = true;
                xmlFileSelectDialog.ButtonCaption = resourceManager.GetString("OSMEditor_OSMPropertyPage_fileselect_buttoncaption");

                xmlFileSelectDialog.ObjectFilter = new GxFilterXmlFiles();

                IEnumGxObject selectedConfigurationFile = null;

                if (xmlFileSelectDialog.DoModalOpen(m_editor.Parent.hWnd, out selectedConfigurationFile))
                {
                    if (selectedConfigurationFile == null)
                        return;

                    selectedConfigurationFile.Reset();

                    IGxFile xmlFile = selectedConfigurationFile.Next() as IGxFile;

                    if (xmlFile == null)
                        return;

                    if (isDomain)
                    {
                        txtOSMDomainFileLocation.Text = xmlFile.Path;
                    }
                    else
                    {
                        txtOSMFeaturePropertiesFileLocation.Text = xmlFile.Path;
                    }

                    SetPageDirty(true);
                }
            }
            catch
            {
            }
        }

        private void FileLocation_TextChanged(object sender, EventArgs e)
        {
            SetPageDirty(true);
        }
    }

    [Guid("87883C5A-FC7B-49C7-8445-D8F63F3E7A97")]
    [ClassInterface(ClassInterfaceType.None)]
    [ProgId("OSMEditor.GxFilterXmlFiles")]
    public class GxFilterXmlFiles : IGxObjectFilter
    {
        private ResourceManager resourceManager = null;

        public GxFilterXmlFiles()
        {
            try
            {
                resourceManager = new ResourceManager("ESRI.ArcGIS.OSM.Editor.OSMFeatureInspectorStrings", this.GetType().Assembly);
            }
            catch { }
        }

        #region IGxObjectFilter Members

        public bool CanChooseObject(IGxObject @object, ref esriDoubleClickResult result)
        {
            bool canChoose = false;

            if (@object == null)
            {
                return canChoose;
            }

            IGxFile gxFile = @object as IGxFile;

            if (gxFile == null)
                return false;

            if (gxFile.Path.EndsWith(".xml"))
                canChoose = true;

            return canChoose;
        }

        public bool CanDisplayObject(IGxObject @object)
        {
            bool canDisplay = false;

            if (@object == null)
            {
                return canDisplay;
            }

            IGxFile gxFile = @object as IGxFile;

            if (gxFile != null)
            {
                if (gxFile.Path.EndsWith(".xml"))
                {
                    canDisplay = true;
                }
            }

            IGxFolder gxFolder = @object as IGxFolder;

            if (gxFolder != null)
            {
                canDisplay = true;
            }

            return canDisplay;
        }

        public bool CanSaveObject(IGxObject Location, string newObjectName, ref bool objectAlreadyExists)
        {
            return false;
        }

        public string Description
        {
            get 
            {
                return resourceManager.GetString("OSMEditor_GxFilterXmlFiles_description");
            }
        }

        public string Name
        {
            get 
            {
                return "SimpleXMLFileSelectFilter";
            }
        }

        #endregion
    }
}
