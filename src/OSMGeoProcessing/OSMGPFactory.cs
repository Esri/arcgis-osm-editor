// (c) Copyright Esri, 2010 - 2016
// This source is subject to the Apache 2.0 License.
// Please see http://www.apache.org/licenses/LICENSE-2.0.html for details.
// All other rights reserved.

using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;

using ESRI.ArcGIS.Geoprocessing;
using ESRI.ArcGIS.Geodatabase;
using System.Resources;
using ESRI.ArcGIS.esriSystem;
using Microsoft.Win32;
using System.IO;
using System.Xml;
using System.Reflection;

namespace ESRI.ArcGIS.OSM.GeoProcessing
{
    [Guid("5c1a4cc1-5cf3-474d-a209-d6cd05e0effc")]
    [ClassInterface(ClassInterfaceType.None)]
    [ProgId("OSMEditor.OSMGPFactory")]
    public sealed class OSMGPFactory : ESRI.ArcGIS.Geoprocessing.IGPFunctionFactory
    {
        //private static readonly OSMGPFactory factoryInstance = new OSMGPFactory();

        string m_FactoryName = "OpenStreetMap Tools";
        string m_FactoryAlias = "OSMTools";
        ResourceManager resourceManager = null;

        string m_DataManagementCategory = "Data Management";

        string m_CombineLayersDisplayName = "Combine Layers";
        internal const string m_CombineLayersName = "GPCombineLayers";
        string m_CombineLayersDescription = "Combines multiple layers into a single group layer.";
        string m_Category = "OpenStreetMap Tools";

        string m_CopyLayerExtensionDisplayName = "Copy Layer Extensions";
        internal const string m_CopyLayerExtensionName = "GPCopyLayerExtensions";
        string m_CopyLayerExtensionDescription = "Copies all layer extensions from the source layer to the target layer.";

        string m_AddExtensionDisplayName = "Add OSM Editor Extension";
        internal const string m_AddExtensionName = "OSMGPAddExtension";
        string m_AddExtensionDescription = "Adds the OSM feature class extension.";

        string m_AttributeSelectorDisplayName = "OSM Attribute Selector";
        internal const string m_AttributeSelectorName = "OSMGPAttributeSelector";
        string m_AttributeSelectorDescription = "Selects tags from the tag collection and stores them as individual attributes in the feature class.";

        string m_CombineAttributesDisplayName = "Combine OSM Attributes";
        internal const string m_CombineAttributesName = "OSMGPCombineAttributes";
        string m_CombineAttributesCategory = "Data Management";
        string m_CombineAttributesDescription = "Combines attributes to a collection of OSM tags.";

        string m_DiffLoaderDisplayName = "Load OSM Diff Files";
        internal const string m_DiffLoaderName = "OSMGPDiffLoader";
        string m_DiffLoaderCategory = "OpenStreetMap Tools";
        string m_DiffLoaderDescription = "Loads OSM Planet Diff Files.";

        string m_DownloadDataDisplayName = "Download OSM Data";
        internal const string m_DownloadDataName = "OSMGPDownload";
        string m_DownloadDataCategory = "OpenStreetMap Tools";
        string m_DownloadDataDescription = "Downloads data from a specified OSM Server";

        string m_Export2OSMDisplayName = "Export to OSM file";
        internal const string m_Export2OSMName = "OSMGPExport2OSM";
        string m_Export2OSMCategory = "OpenStreetMap Tools";
        string m_Export2OSMDescription = "Export OpenStreetMap feature classes into the OSM XML format.";

        string m_FeatureComparisonDisplayName = "Feature Comparison";
        internal const string m_FeatureComparisonName = "OSMGPFeatureComparison";
        string m_FeatureComparisonCategory = "OpenStreetMap Tools";
        string m_FeatureComparisonDescription = "Allows the comparison of two feature classes to discover geometric similarities.";

        string m_FileLoaderDisplayName = "Load OSM File";
        internal const string m_FileLoaderName = "OSMGPFileLoader";
        string m_FileLoaderCategory = "OpenStreetMap Tools";
        string m_FileLoaderDescription = "Loads stand-alone OSM file (planet files, etc.) into a geodatabase.";

        string m_UploadDataDisplayName = "Upload OSM Data";
        internal const string m_UploadDataName = "OSMGPUpload";
        string m_UploadDataCategory = "OpenStreetMap Tools";
        string m_UploadDataDescription = "Uploads current edits to the specified OSM server.";

        string m_RemoveExtensionDisplayName = "Remove OSM Editor Extension";
        internal const string m_RemoveExtensionName = "OSMGPRemoveExtension";
        string m_RemoveExtensionCategory = "Data Management";
        string m_RemoveExtensionDescription = "Removes the OSM feature class extension.";

        string m_FeatureSymbolizerDisplayName = "OSM Feature Symbolizer";
        internal const string m_FeatureSymbolizerName = "OSMGPSymbolizer";
        string m_FeatureSymbolizerCategory = "Data Management";
        string m_FeatureSymbolizerDescription = "Assigns a set of predefined symbology and prepares the edit templates.";

        string m_CreateNetworkDatasetDisplayName = "Create OSM Network Dataset";
        internal const string m_CreateNetworkDatasetName = "OSMGPCreateNetworkDataset";
        string m_CreateNetworkDatasetCategory = "Data Management";
        string m_CreateNetworkDatasetDescription = "Create a network dataset from a given OSM dataset.";

        string m_MultiLoaderDisplayName = "OSM Multi Loader (Load only)";
        internal const string m_MultiLoaderName = "OSMGPMultiLoader";
        string m_MultiLoaderCategory = "Data Delivery";
        string m_MultiLoaderDescription = "Load OSM file without the metadata. Data loaded without tools cannot be uploaded back to the OSM server.";

        string m_NodeLoaderDisplayName = "OSM Node Loader";
        internal const string m_NodeLoaderName = "OSMGPNodeLoader";
        string m_NodeLoaderCategory = "Data Delivery";
        string m_NodeLoaderDescription = "Load OSM nodes.";

        string m_WayLoaderDisplayName = "OSM Way Loader";
        internal const string m_WayLoaderName = "OSMGPWayLoader";
        string m_WayLoaderCategory = "Data Delivery";
        string m_WayLoaderDescription = "Load OSM ways.";

        string m_RelationLoaderDisplayName = "OSM Relation Loader";
        internal const string m_RelationLoaderName = "OSMGPRelationLoader";
        string m_RelationLoaderCategory = "Data Delivery";
        string m_RelationLoaderDescription = "Load OSM relations.";

        #region "Component Category Registration"
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
            Registry.ClassesRoot.CreateSubKey(regKey.Substring(18) + "\\Implemented Categories\\{FD939A4A-955D-4094-B440-77083E410F41}");

        }
        /// <summary>
        /// Required method for ArcGIS Component Category unregistration -
        /// Do not modify the contents of this method with the code editor.
        /// </summary>
        private static void ArcGISCategoryUnregistration(Type registerType)
        {
            string regKey = string.Format("HKEY_CLASSES_ROOT\\CLSID\\{{{0}}}", registerType.GUID);
            Registry.ClassesRoot.DeleteSubKey(regKey.Substring(18) + "\\Implemented Categories\\{FD939A4A-955D-4094-B440-77083E410F41}");

        }

        #endregion

        #endregion

        public OSMGPFactory()
        {
            try
            {
                resourceManager = new ResourceManager("ESRI.ArcGIS.OSM.GeoProcessing.OSMGPToolsStrings", this.GetType().Assembly);
                m_FactoryName = resourceManager.GetString("GPTools_factoryname");

                m_Category = resourceManager.GetString("GPTools_OSMGPAttributeSelector_categoryName");
                m_CombineLayersDisplayName = resourceManager.GetString("GPTools_GPCombineLayers_displayname");
                m_CombineLayersDescription = resourceManager.GetString("GPTools_GPCombineLayers_desc");

                m_CopyLayerExtensionDisplayName = resourceManager.GetString("GPTools_GPCopyLayerExtension_desc");
                m_CopyLayerExtensionDescription = resourceManager.GetString("GPTools_GPCopyLayerExtension_displayname");
                m_DataManagementCategory = resourceManager.GetString("GPTools_GPCopyLayerExtension_categoryname");

                m_AddExtensionDisplayName = resourceManager.GetString("GPTools_OSMGPAddExtension_displayName");
                m_AddExtensionDescription = resourceManager.GetString("GPTools_OSMGPAddExtension_desc");

                m_AttributeSelectorDisplayName = resourceManager.GetString("GPTools_OSMGPAttributeSelector_displayName");
                m_AttributeSelectorDescription = resourceManager.GetString("GPTools_OSMGPAttributeSelector_desc");

                m_CombineAttributesCategory = resourceManager.GetString("GPTools_OSMGPAttributeSelector_categoryName");
                m_CombineAttributesDisplayName = resourceManager.GetString("GPTools_OSMGPCombineAttributes_displayName");
                m_CombineAttributesDescription = resourceManager.GetString("GPTools_OSMGPCombineAttributes_desc");

                m_DiffLoaderCategory = resourceManager.GetString("GPTools_OSMGPDownload_categoryName");
                m_DiffLoaderDisplayName = resourceManager.GetString("GPTools_OSMGPDiffLoader_displayname");
                m_DiffLoaderDescription = resourceManager.GetString("GPTools_OSMGPDiffLoader_desc");

                m_DownloadDataCategory = resourceManager.GetString("GPTools_OSMGPDownload_categoryName");
                m_DownloadDataDisplayName = resourceManager.GetString("GPTools_OSMGPDownload_displayName");
                m_DownloadDataDescription = resourceManager.GetString("GPTools_OSMGPDownload_desc");

                m_Export2OSMCategory = resourceManager.GetString("GPTools_OSMGPAttributeSelector_categoryName");
                m_Export2OSMDisplayName = resourceManager.GetString("GPTools_OSMGPExport2OSM_displayname");
                m_Export2OSMDescription = resourceManager.GetString("GPTools_OSMGPExport2OSM_desc");

                m_FeatureComparisonCategory = resourceManager.GetString("GPTools_OSMGPDownload_categoryName");
                m_FeatureComparisonDisplayName = resourceManager.GetString("GPTools_OSMGPFeatureComparison_displayname");
                m_FeatureComparisonDescription = resourceManager.GetString("GPTools_OSMGPFeatureComparison_desc");

                m_FileLoaderCategory = resourceManager.GetString("GPTools_OSMGPDownload_categoryName");
                m_FileLoaderDisplayName = resourceManager.GetString("GPTools_OSMGPFileReader_diplayname");
                m_FileLoaderDescription = resourceManager.GetString("GPTools_OSMGPFileReader_desc");

                m_UploadDataCategory = resourceManager.GetString("GPTools_OSMGPDownload_categoryName");
                m_UploadDataDisplayName = resourceManager.GetString("GPTools_OSMGPUpload_displayName");
                m_UploadDataDescription = resourceManager.GetString("GPTools_OSMGPUpload_desc");

                m_RemoveExtensionCategory = resourceManager.GetString("GPTools_OSMGPAttributeSelector_categoryName");
                m_RemoveExtensionDisplayName = resourceManager.GetString("GPTools_OSMGPRemoveExtension_displayName");
                m_RemoveExtensionDescription = resourceManager.GetString("GPTools_OSMGPRemoveExtension_desc");

                m_FeatureSymbolizerCategory = resourceManager.GetString("GPTools_OSMGPAttributeSelector_categoryName");
                m_FeatureSymbolizerDisplayName = resourceManager.GetString("GPTools_OSMGPSymbolizer_displayName");
                m_FeatureSymbolizerDescription = resourceManager.GetString("GPTools_OSMGPSymbolizer_desc");

                m_CreateNetworkDatasetCategory = resourceManager.GetString("GPTools_OSMGPAttributeSelector_categoryName");
                m_CreateNetworkDatasetDisplayName = resourceManager.GetString("GPTools_OSMGPCreateNetworkDataset_displayname");
                m_CreateNetworkDatasetDescription = resourceManager.GetString("GPTools_OSMGPCreateNetworkDataset_desc");

                m_MultiLoaderCategory = resourceManager.GetString("GPTools_OSMGPMultiLoader_categoryName");
                m_MultiLoaderDisplayName = resourceManager.GetString("GPTools_OSMGPMultiLoader_displayName");
                m_MultiLoaderDescription = resourceManager.GetString("GPTools_OSMGPMultiLoader_desc");

                m_NodeLoaderCategory = resourceManager.GetString("GPTools_OSMGPNodeLoader_categoryName");
                m_NodeLoaderDisplayName = resourceManager.GetString("GPTools_OSMGPNodeLoader_displayName");
                m_NodeLoaderDescription = resourceManager.GetString("GPTools_OSMGPNodeLoader_desc");

                m_WayLoaderCategory = resourceManager.GetString("GPTools_OSMGPWayLoader_categoryName");
                m_WayLoaderDisplayName = resourceManager.GetString("GPTools_OSMGPWayLoader_displayName");
                m_WayLoaderDescription = resourceManager.GetString("GPTools_OSMGPWayLoader_desc");

                m_RelationLoaderCategory = resourceManager.GetString("GPTools_OSMGPRelationLoader_categoryName");
                m_RelationLoaderDisplayName = resourceManager.GetString("GPTools_OSMGPRelationLoader_displayName");
                m_RelationLoaderDescription = resourceManager.GetString("GPTools_OSMGPRelationLoader_desc");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }

        #region "IGPFunctionFactory Implementations"
        public string Alias
        {
            get
            {
                return m_FactoryAlias;
            }
        }

        public ESRI.ArcGIS.esriSystem.UID CLSID
        {
            get
            {
                UID gpFactory = new UIDClass();
                gpFactory.Value = "{5c1a4cc1-5cf3-474d-a209-d6cd05e0effc}";

                return gpFactory;
            }
        }

        public ESRI.ArcGIS.Geoprocessing.IGPFunction GetFunction(string Name)
        {
            switch (Name)
            {
                case m_DownloadDataName:
                    IGPFunction osmGPDownloadFunction = new OSMGPDownload() as IGPFunction;
                    return osmGPDownloadFunction;
                case m_UploadDataName:
                    IGPFunction osmGPUploadFunction = new OSMGPUpload() as IGPFunction;
                    return osmGPUploadFunction;
                case m_AttributeSelectorName:
                    IGPFunction osmGPAttributeSelector = new OSMGPAttributeSelector() as IGPFunction;
                    return osmGPAttributeSelector;
                case m_FeatureSymbolizerName:
                    IGPFunction osmGPSymbolizer = new OSMGPSymbolizer() as IGPFunction;
                    return osmGPSymbolizer;
                case m_AddExtensionName:
                    IGPFunction osmGPAddExtension = new OSMGPAddExtension() as IGPFunction;
                    return osmGPAddExtension;
                case m_RemoveExtensionName:
                    IGPFunction osmGPRemoveExtension = new OSMGPRemoveExtension() as IGPFunction;
                    return osmGPRemoveExtension;
                case m_FileLoaderName:
                    IGPFunction osmGPFileLoader = new OSMGPFileLoader() as IGPFunction;
                    return osmGPFileLoader;
                case m_CombineLayersName:
                    IGPFunction combineLayers = new GPCombineLayers() as IGPFunction;
                    return combineLayers;
                case m_CombineAttributesName:
                    IGPFunction osmGPCombineAttributes = new OSMGPCombineAttributes() as IGPFunction;
                    return osmGPCombineAttributes;
                case m_CopyLayerExtensionName:
                    IGPFunction gpCopyLayerExtensions = new GPCopyLayerExtensions() as IGPFunction;
                    return gpCopyLayerExtensions;
                case m_DiffLoaderName:
                    IGPFunction osmGPDiffLoader = new OSMGPDiffLoader() as IGPFunction;
                    return osmGPDiffLoader;
                case m_Export2OSMName:
                    IGPFunction osmGPExport2OSM = new OSMGPExport2OSM() as IGPFunction;
                    return osmGPExport2OSM;
                case m_FeatureComparisonName:
                    IGPFunction osmGPFeatureComparison = new OSMGPFeatureComparison() as IGPFunction;
                    return osmGPFeatureComparison;
                case m_CreateNetworkDatasetName:
                    IGPFunction osmGPCreateNetworkDataset = new OSMGPCreateNetworkDataset() as IGPFunction;
                    return osmGPCreateNetworkDataset;
                case m_MultiLoaderName:
                    IGPFunction osmGPMultiLoader = new OSMGPMultiLoader() as IGPFunction;
                    return osmGPMultiLoader;
                case m_NodeLoaderName:
                    IGPFunction osmGPNodeLoader = new OSMGPNodeLoader() as IGPFunction;
                    return osmGPNodeLoader;
                case m_WayLoaderName:
                    IGPFunction osmGPWayLoader = new OSMGPWayLoader() as IGPFunction;
                    return osmGPWayLoader;
                case m_RelationLoaderName:
                    IGPFunction osmGPRelationLoader = new OSMGPRelationLoader() as IGPFunction;
                    return osmGPRelationLoader;
                default:
                    return null;
            }
        }

        public ESRI.ArcGIS.Geoprocessing.IEnumGPEnvironment GetFunctionEnvironments()
        {
            return default(ESRI.ArcGIS.Geoprocessing.IEnumGPEnvironment);
        }

        public ESRI.ArcGIS.Geodatabase.IGPName GetFunctionName(string Name)
        {
            IGPName toolGPName = new GPFunctionNameClass() as IGPName;
            toolGPName.Factory = (IGPFunctionFactory)this;
            toolGPName.Category = m_Category;

            switch (Name)
            {
                case m_DownloadDataName:
                    toolGPName.Name = m_DownloadDataName;
                    toolGPName.DisplayName = m_DownloadDataDisplayName;
                    toolGPName.Category = m_DownloadDataCategory;
                    toolGPName.Description = m_DownloadDataDescription;
                    break;
                case m_UploadDataName:
                    toolGPName.Name = m_UploadDataName;
                    toolGPName.DisplayName = m_UploadDataDisplayName;
                    toolGPName.Category = m_UploadDataCategory;
                    toolGPName.Description = m_UploadDataDescription;
                    break;
                case m_AttributeSelectorName:
                    toolGPName.Name = m_AttributeSelectorName;
                    toolGPName.DisplayName = m_AttributeSelectorDisplayName;
                    toolGPName.Category = m_DataManagementCategory;
                    toolGPName.Description = m_AttributeSelectorDescription;
                    break;
                //case m_FeatureSymbolizerName:
                //    toolGPName.Name = m_FeatureSymbolizerName;
                //    toolGPName.DisplayName = m_FeatureSymbolizerDisplayName;
                //    toolGPName.Category = m_FeatureSymbolizerCategory;
                //    toolGPName.Description = m_FeatureSymbolizerDescription;
                //    break;
                case m_AddExtensionName:
                    toolGPName.Name = m_AddExtensionName;
                    toolGPName.DisplayName = m_AddExtensionDisplayName;
                    toolGPName.Category = m_DataManagementCategory;
                    toolGPName.Description = m_AddExtensionDescription;
                    break;
                case m_RemoveExtensionName:
                    toolGPName.Name = m_RemoveExtensionName;
                    toolGPName.DisplayName = m_RemoveExtensionDisplayName;
                    toolGPName.Category = m_RemoveExtensionCategory;
                    toolGPName.Description = m_RemoveExtensionDescription;
                    break;;
                case m_FileLoaderName:
                    toolGPName.Name = m_FileLoaderName;
                    toolGPName.DisplayName = m_FileLoaderDisplayName;
                    toolGPName.Category = m_FileLoaderCategory;
                    toolGPName.Description = m_FileLoaderDescription;
                    break;
                case m_CombineLayersName:
                    toolGPName.Name = m_CombineLayersName;
                    toolGPName.DisplayName = m_CombineLayersDisplayName;
                    toolGPName.Category = m_DataManagementCategory;
                    toolGPName.Description = m_CombineLayersDescription;
                    break;
                case m_CombineAttributesName:
                    toolGPName.Name = m_CombineAttributesName;
                    toolGPName.DisplayName = m_CombineAttributesDisplayName;
                    toolGPName.Category = m_DataManagementCategory;
                    toolGPName.Description = m_CombineAttributesDescription;
                    break;
                case m_CopyLayerExtensionName:
                    toolGPName.Name = m_CopyLayerExtensionName;
                    toolGPName.DisplayName = m_CopyLayerExtensionDisplayName;
                    toolGPName.Category = m_DataManagementCategory;
                    toolGPName.Description = m_CopyLayerExtensionDescription;
                    break;
                case m_DiffLoaderName:
                    toolGPName.Name = m_DiffLoaderName;
                    toolGPName.DisplayName = m_DiffLoaderDisplayName;
                    toolGPName.Category = m_DiffLoaderCategory;
                    toolGPName.Description = m_DiffLoaderDescription;
                    break;
                case m_Export2OSMName:
                    toolGPName.Name = m_Export2OSMName;
                    toolGPName.DisplayName = m_Export2OSMDisplayName;
                    toolGPName.Category = m_Export2OSMCategory;
                    toolGPName.Description = m_Export2OSMDescription;
                    break;
                case m_FeatureComparisonName:
                    toolGPName.Name = m_FeatureComparisonName;
                    toolGPName.DisplayName = m_FeatureComparisonDisplayName;
                    toolGPName.Category = m_FeatureComparisonCategory;
                    toolGPName.Description = m_FeatureComparisonDescription;
                    break;
                case m_CreateNetworkDatasetName:
                    toolGPName.Name = m_CreateNetworkDatasetName;
                    toolGPName.DisplayName = m_CreateNetworkDatasetDisplayName;
                    toolGPName.Category = m_CreateNetworkDatasetCategory;
                    toolGPName.Description = m_CreateNetworkDatasetDescription;
                    break;
                case m_MultiLoaderName:
                    toolGPName.Name = m_MultiLoaderName;
                    toolGPName.DisplayName = m_MultiLoaderDisplayName;
                    toolGPName.Category = m_MultiLoaderCategory;
                    toolGPName.Description = m_MultiLoaderDescription;
                    break;
                case m_NodeLoaderName:
                    toolGPName.Name = m_NodeLoaderName;
                    toolGPName.DisplayName = m_NodeLoaderDisplayName;
                    toolGPName.Category = m_NodeLoaderCategory;
                    toolGPName.Description = m_NodeLoaderDescription;
                    break;
                case m_WayLoaderName:
                    toolGPName.Name = m_WayLoaderName;
                    toolGPName.DisplayName = m_WayLoaderDisplayName;
                    toolGPName.Category = m_WayLoaderCategory;
                    toolGPName.Description = m_WayLoaderDescription;
                    break;
                case m_RelationLoaderName:
                    toolGPName.Name = m_RelationLoaderName;
                    toolGPName.DisplayName = m_RelationLoaderDisplayName;
                    toolGPName.Category = m_RelationLoaderCategory;
                    toolGPName.Description = m_RelationLoaderDescription;
                    break;
                default:
                    return null;
            }

            return toolGPName;
        }

        public ESRI.ArcGIS.Geodatabase.IEnumGPName GetFunctionNames()
        {
            IArray allGPFunctionNames = new EnumGPNameClass();
            allGPFunctionNames.Add(this.GetFunctionName(m_DownloadDataName));
            allGPFunctionNames.Add(this.GetFunctionName(m_UploadDataName));
            allGPFunctionNames.Add(this.GetFunctionName(m_AttributeSelectorName));
            //allGPFunctionNames.Add(this.GetFunctionName(m_FeatureSymbolizerName));
            allGPFunctionNames.Add(this.GetFunctionName(m_AddExtensionName));
            allGPFunctionNames.Add(this.GetFunctionName(m_RemoveExtensionName));
            allGPFunctionNames.Add(this.GetFunctionName(m_FileLoaderName));
            allGPFunctionNames.Add(this.GetFunctionName(m_CombineLayersName));
            allGPFunctionNames.Add(this.GetFunctionName(m_CombineAttributesName));
            //allGPFunctionNames.Add(this.GetFunctionName(m_CopyLayerExtensionName));
            allGPFunctionNames.Add(this.GetFunctionName(m_DiffLoaderName));
            allGPFunctionNames.Add(this.GetFunctionName(m_FeatureComparisonName));
            allGPFunctionNames.Add(this.GetFunctionName(m_Export2OSMName));
            allGPFunctionNames.Add(this.GetFunctionName(m_CreateNetworkDatasetName));
            allGPFunctionNames.Add(this.GetFunctionName(m_MultiLoaderName));
            allGPFunctionNames.Add(this.GetFunctionName(m_NodeLoaderName));
            allGPFunctionNames.Add(this.GetFunctionName(m_WayLoaderName));
            allGPFunctionNames.Add(this.GetFunctionName(m_RelationLoaderName));

            return (IEnumGPName)allGPFunctionNames;
        }

        public string Name
        {
            get
            {
                return m_FactoryName;
            }
        }
        #endregion

        /// <summary>
        /// reads the ArcGIS runtime information from the registry
        /// </summary>
        /// <returns></returns>
        public static string GetArcGIS10InstallLocation()
        {
           RegistryKey localMachineKey = null;  
            RegistryKey softwareKey = null;  
            RegistryKey esriKey = null;
            RegistryKey wow6432Key = null;
            RegistryKey arcgisKey = null;
            RegistryKey desktopKey = null;

            System.Object installationDirectory = null;
            string m_foundInstallationDirectory = String.Empty;
            System.Object realVersion = null;
            string majorMinorVersion = String.Empty;

            try
            {
                // read the general registry key for installed ArcGIS software
                localMachineKey = Registry.LocalMachine;
                softwareKey = localMachineKey.OpenSubKey("SOFTWARE");
                esriKey = softwareKey.OpenSubKey("ESRI");
                arcgisKey = esriKey.OpenSubKey("ArcGIS");

                if (arcgisKey == null)
                {
                    wow6432Key = softwareKey.OpenSubKey("Wow6432Node");

                    if (wow6432Key == null)
                        return m_foundInstallationDirectory;

                    esriKey = wow6432Key.OpenSubKey("ESRI");

                    if (esriKey == null)
                        return m_foundInstallationDirectory;

                    arcgisKey = esriKey.OpenSubKey("ArcGIS");

                    if (arcgisKey == null)
                        return m_foundInstallationDirectory;
                }


                realVersion = arcgisKey.GetValue("RealVersion");
                string realVersionAsString = realVersion as String;

                if (!string.IsNullOrEmpty(realVersionAsString))
                {
                    string [] versionNumberComponents = realVersionAsString.Split('.');
                    if (versionNumberComponents.Length > 2)
                        majorMinorVersion = string.Join(".", new string[]{versionNumberComponents[0], versionNumberComponents[1]});
                }

                string desktopKeyName = "Desktop" + majorMinorVersion;

                desktopKey = esriKey.OpenSubKey(desktopKeyName);

                if (desktopKey != null)
                {
                    installationDirectory = desktopKey.GetValue("InstallDir");
                    if (installationDirectory != null)
                        m_foundInstallationDirectory = Convert.ToString(installationDirectory);
                }
                else
                {
                    wow6432Key = softwareKey.OpenSubKey("Wow6432Node");

                    if (wow6432Key == null)
                        return m_foundInstallationDirectory;

                    esriKey = wow6432Key.OpenSubKey("ESRI");

                    if (esriKey == null)
                        return m_foundInstallationDirectory;

                    desktopKey = esriKey.OpenSubKey(desktopKeyName);

                    installationDirectory = desktopKey.GetValue("InstallDir");
                    if (installationDirectory != null)
                        m_foundInstallationDirectory = Convert.ToString(installationDirectory);
                }
            }
            catch
            {
            }            

            return m_foundInstallationDirectory;
        }

        /// <summary>
        /// reads the ArcGIS runtime information from the registry
        /// </summary>
        /// <returns></returns>
        public static string GetPythonArcGISInstallLocation()
        {
            RegistryKey localMachineKey = null;
            RegistryKey softwareKey = null;
            RegistryKey esriKey = null;
            RegistryKey arcgisKey = null;
            RegistryKey pythonKey = null;
            RegistryKey wow6432Key = null;

            System.Object installationDirectory = null;
            string m_foundInstallationDirectory = String.Empty;
            System.Object realVersion = null;
            string majorMinorVersion = String.Empty;
            string pythonCoreDirectory = String.Empty;

            try
            {
                // read the general registry key for installed ArcGIS software
                localMachineKey = Registry.LocalMachine;
                softwareKey = localMachineKey.OpenSubKey("SOFTWARE");
                esriKey = softwareKey.OpenSubKey("ESRI");
                arcgisKey = esriKey.OpenSubKey("ArcGIS");

                if (arcgisKey == null)
                {
                    wow6432Key = softwareKey.OpenSubKey("Wow6432Node");

                    if (wow6432Key == null)
                        return m_foundInstallationDirectory;

                    esriKey = wow6432Key.OpenSubKey("ESRI");

                    if (esriKey == null)
                        return m_foundInstallationDirectory;

                    arcgisKey = esriKey.OpenSubKey("ArcGIS");

                    if (arcgisKey == null)
                        return m_foundInstallationDirectory;
                }


                realVersion = arcgisKey.GetValue("RealVersion");
                string realVersionAsString = realVersion as String;

                if (!string.IsNullOrEmpty(realVersionAsString))
                {
                    string[] versionNumberComponents = realVersionAsString.Split('.');
                    if (versionNumberComponents.Length > 2)
                        majorMinorVersion = string.Join(".", new string[]{versionNumberComponents[0], versionNumberComponents[1]});
                }

                string pythonKeyName = "Python" + majorMinorVersion;
                string python32bitFolderName = "ArcGIS" + majorMinorVersion;

                pythonKey = esriKey.OpenSubKey(pythonKeyName);

                if (pythonKey != null)
                {
                    installationDirectory = pythonKey.GetValue("PythonDir");

                    if (installationDirectory != null)
                        pythonCoreDirectory = Convert.ToString(installationDirectory);

                    string estimatedPythonDirectory = System.IO.Path.Combine(pythonCoreDirectory, python32bitFolderName);

                    if (System.IO.Directory.Exists(estimatedPythonDirectory))
                        m_foundInstallationDirectory = estimatedPythonDirectory;
                }
                else
                {
                    wow6432Key = softwareKey.OpenSubKey("Wow6432Node");

                    if (wow6432Key == null)
                        return m_foundInstallationDirectory;

                    esriKey = wow6432Key.OpenSubKey("ESRI");

                    if (esriKey == null)
                        return m_foundInstallationDirectory;

                    pythonKey = esriKey.OpenSubKey(pythonKeyName);

                    installationDirectory = pythonKey.GetValue("PythonDir");

                    if (installationDirectory != null)
                        pythonCoreDirectory = Convert.ToString(installationDirectory);

                    string estimatedPythonDirectory = System.IO.Path.Combine(pythonCoreDirectory, python32bitFolderName);

                    if (System.IO.Directory.Exists(estimatedPythonDirectory))
                        m_foundInstallationDirectory = estimatedPythonDirectory;
                }
            }
            catch
            {
            }

            return m_foundInstallationDirectory;
        }

        public static Dictionary<string, string> ReadOSMEditorSettings()
        {
            Dictionary<string, string> configurationSettings = new Dictionary<string, string>();

            try
            {
                string osmEditorFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + System.IO.Path.DirectorySeparatorChar + "ESRI" + System.IO.Path.DirectorySeparatorChar + "OSMEditor";
                string configurationfile = osmEditorFolder + System.IO.Path.DirectorySeparatorChar + "osmeditor.config";
                
                if (!File.Exists(configurationfile))
                    configurationfile = "c:\\inetpub\\wwwroot\\osm\\" + "osmeditor.config";
                // Setting the path for when the code runs inside VS2010 without being installed.
                if (!File.Exists(configurationfile))
                {
                    configurationfile = System.AppDomain.CurrentDomain.BaseDirectory + "osmeditor.config";
                    osmEditorFolder = System.AppDomain.CurrentDomain.BaseDirectory;
                }

                if (File.Exists(configurationfile))
                {
                    using (XmlReader configurationFileReader = XmlReader.Create(configurationfile))
                    {
                        while (configurationFileReader.Read())
                        {
                            if (configurationFileReader.IsStartElement())
                            {
                                switch (configurationFileReader.Name)
                                {
                                    case "osmbaseurl":
                                        configurationFileReader.Read();
                                        configurationSettings.Add("osmbaseurl", configurationFileReader.Value.Trim());
                                        break;
                                    case "osmdomainsfilepath":
                                        configurationFileReader.Read();
                                        configurationSettings.Add("osmdomainsfilepath", configurationFileReader.Value.Trim());
                                        break;
                                    case "osmfeaturepropertiesfilepath":
                                        configurationFileReader.Read();
                                        configurationSettings.Add("osmfeaturepropertiesfilepath", configurationFileReader.Value.Trim());
                                        break;
                                    default:
                                        break;
                                }
                            }
                        }
                    }
                }
                //else
                //    throw new Exception("Config file missing osmeditor.config at osm directory"); 

                if (configurationSettings.ContainsKey("osmbaseurl") == false)
                {
                    // let's start with the very first default settings
                    configurationSettings.Add("osmbaseurl", "http://www.openstreetmap.org");
                }

                if (configurationSettings.ContainsKey("osmdiffsurl") == false)
                {
                    // let's start with the very first default settings
                    configurationSettings.Add("osmdiffsurl", "http://planet.openstreetmap.org/replication");
                }

                string path = Assembly.GetExecutingAssembly().Location;
                FileInfo executingAssembly = new FileInfo(path);

                if (configurationSettings.ContainsKey("osmdomainsfilepath") == false)
                {
                    // initialize with the default configuration files
                    if (File.Exists(executingAssembly.Directory.FullName + System.IO.Path.DirectorySeparatorChar + "osm_domains.xml"))
                    {
                        configurationSettings.Add("osmdomainsfilepath", executingAssembly.Directory.FullName + System.IO.Path.DirectorySeparatorChar + "osm_domains.xml");
                    }
                }

                if (configurationSettings.ContainsKey("osmfeaturepropertiesfilepath") == false)
                {
                    if (File.Exists(executingAssembly.Directory.FullName + System.IO.Path.DirectorySeparatorChar + "OSMFeaturesProperties.xml"))
                    {
                        configurationSettings.Add("osmfeaturepropertiesfilepath", executingAssembly.Directory.FullName + System.IO.Path.DirectorySeparatorChar + "OSMFeaturesProperties.xml");
                    }
                }
            }
            catch { }

            return configurationSettings;
        }

        public static void StoreOSMEditorSettings(Dictionary<string, string> inputConfigurations)
        {
            try
            {
                string osmEditorFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + System.IO.Path.DirectorySeparatorChar + "ESRI" + System.IO.Path.DirectorySeparatorChar + "OSMEditor";

                if (Directory.Exists(osmEditorFolder) == false)
                {
                    try
                    {
                        Directory.CreateDirectory(osmEditorFolder);
                    }
                    catch
                    {
                        return;
                    }
                }

                string configurationfile = osmEditorFolder + System.IO.Path.DirectorySeparatorChar + "osmeditor.config";
                
                System.IO.FileStream configurationFileWriter = null;
                try
                {
                    if (File.Exists(configurationfile))
                    {
                        try
                        {
                            File.Delete(configurationfile);
                        }
                        catch { }
                    }

                    configurationFileWriter = File.Create(configurationfile);
                    
                    MemoryStream memoryStream = new MemoryStream();
                    XmlWriterSettings xmlWriterSettings = new XmlWriterSettings();
                    xmlWriterSettings.Indent = true;

                    using (XmlWriter xmlWriter = XmlWriter.Create(memoryStream, xmlWriterSettings))
                    {
                        xmlWriter.WriteStartDocument();
                        xmlWriter.WriteStartElement("OSMEditor");

                        foreach (KeyValuePair<string, string> configurationItem in inputConfigurations)
                        {
                            xmlWriter.WriteElementString(configurationItem.Key, configurationItem.Value);
                        }
                        xmlWriter.WriteEndElement();
                        xmlWriter.WriteEndDocument();
                        xmlWriter.Close();
                    }

                    configurationFileWriter.Write(memoryStream.GetBuffer(), 0, Convert.ToInt32(memoryStream.Length));
                    memoryStream.Close();

                }
                catch { }
                finally
                {
                    if (configurationFileWriter != null)
                    {
                        configurationFileWriter.Close();
                    }
                }
            }
            catch { }
        }
    }
}
