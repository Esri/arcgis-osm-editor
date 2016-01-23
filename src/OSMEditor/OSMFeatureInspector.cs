// (c) Copyright Esri, 2010 - 2016
// This source is subject to the Apache 2.0 License.
// Please see http://www.apache.org/licenses/LICENSE-2.0.html for details.
// All other rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Windows.Forms.Integration;

using ESRI.ArcGIS.ADF.CATIDs;
using ESRI.ArcGIS.Display;
using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.Editor;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geometry;
using System.Reflection;
using System.IO;
using System.Xml.Serialization;
using System.Xml;
using System.Resources;
using System.Web;
using System.EnterpriseServices;
using ESRI.ArcGIS.ADF;
using ESRI.ArcGIS.OSM.OSMClassExtension;


namespace ESRI.ArcGIS.OSM.Editor
{
    [Guid("703FD76D-8556-4447-A77D-7BF6B5DCF157")]
    [ClassInterface(ClassInterfaceType.None)]
    [ProgId("OSMEditor.OSMFeatureInspectorUI")]
    [ComVisible(true)]
    public partial class OSMFeatureInspectorUI : UserControl, IObjectInspector
    {
        IObjectInspector m_inspector = null;

        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        private static extern int SetParent(int hWndChild, int hWndNewParent);

        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        private static extern int ShowWindow(int hWnd, int nCmdShow);

        private const short SW_SHOW = 5;
        private const short SW_HIDE = 0;

        private Dictionary<string, osmfeature> m_editFeatures = null;
        private Dictionary<string, ESRI.ArcGIS.OSM.OSMClassExtension.tagkey> m_editTags = null;
        private string m_baseInfoURI = null;
        private Dictionary<string, ESRI.ArcGIS.OSM.OSMClassExtension.domain> m_domainDictionary = null;
        private ESRI.ArcGIS.Editor.IEditor m_editor = null;
        private ResourceManager resourceManager = null;
        private OSMUtility _osmUtility = null;
        private IEnumRow m_enumRow = null;

        private bool m_isOSMAttributeChange = false;
        private bool m_isInitialized = false;

        private string m_ChangeTagString = String.Empty;

        private string m_osmbaseurl = String.Empty;
        private string m_osmDomainsFilePath = String.Empty;
        private string m_osmFeaturePropertiesFilePath = String.Empty;

        private ToolTip m_tooltip = new ToolTip();

        public OSMFeatureInspectorUI()
        {
            try
            {
                InitializeComponent();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }

            resourceManager = new ResourceManager("ESRI.ArcGIS.OSM.Editor.OSMFeatureInspectorStrings", this.GetType().Assembly);
            _osmUtility = new OSMUtility();

            m_inspector = new FeatureInspectorClass();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
        }

        protected override void OnLeave(EventArgs e)
        {
            base.OnLeave(e);
        }

        private void loadOSMDomains(string domainFilePath)
        {
            // load the features that are considered for editing
            ESRI.ArcGIS.OSM.OSMClassExtension.OSMDomains availableDomains = null;

            try
            {
                System.Xml.XmlTextReader reader = null;
                if ((String.IsNullOrEmpty(domainFilePath)) == false && File.Exists(domainFilePath))
                {
                    reader = new System.Xml.XmlTextReader(domainFilePath);
                }
                else
                {
                    FileInfo assemblyLocation = new FileInfo(Assembly.GetExecutingAssembly().Location);
                    // Reading the XML document requires a FileStream.
                    reader = new System.Xml.XmlTextReader(assemblyLocation.DirectoryName + System.IO.Path.DirectorySeparatorChar + "osm_domains.xml");
                }

                System.Xml.Serialization.XmlSerializer serializer = new System.Xml.Serialization.XmlSerializer(typeof(ESRI.ArcGIS.OSM.OSMClassExtension.OSMDomains));
                availableDomains = serializer.Deserialize(reader) as ESRI.ArcGIS.OSM.OSMClassExtension.OSMDomains;
            }
            catch
            {
            }

            m_domainDictionary = new Dictionary<string, ESRI.ArcGIS.OSM.OSMClassExtension.domain>();
            foreach (ESRI.ArcGIS.OSM.OSMClassExtension.domain osmDomain in availableDomains.domain)
            {
                try
                {
                    m_domainDictionary.Add(osmDomain.name, osmDomain);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                }
            }
        }

        // the features whose attributes are actively selected for editor (1 or more features from the current editor selection)
        public IEnumRow currentlyEditedRows
        {
            set
            {
                m_enumRow = value;
            }
            get
            {
                return m_enumRow;
            }
        }

        private void loadOSMEditFeatures(string featurePropertiesFilePath)
        {
            // load the features that are considered for editing
            ESRI.ArcGIS.OSM.OSMClassExtension.osmfeatures availableEditFeatures = null;

            try
            {
                System.Xml.XmlTextReader reader = null;
                if ((string.IsNullOrEmpty(featurePropertiesFilePath) == false) && File.Exists(featurePropertiesFilePath))
                {
                    reader = new System.Xml.XmlTextReader(featurePropertiesFilePath);
                }
                else
                {
                    FileInfo assemblyLocation = new FileInfo(Assembly.GetExecutingAssembly().Location);
                    // Reading the XML document requires a FileStream.
                    reader = new System.Xml.XmlTextReader(assemblyLocation.DirectoryName + System.IO.Path.DirectorySeparatorChar + "OSMFeaturesProperties.xml");
                }

                System.Xml.Serialization.XmlSerializer serializer = new System.Xml.Serialization.XmlSerializer(typeof(ESRI.ArcGIS.OSM.OSMClassExtension.osmfeatures));
                availableEditFeatures = serializer.Deserialize(reader) as ESRI.ArcGIS.OSM.OSMClassExtension.osmfeatures;
            }
            catch
            {
            }

            m_editFeatures = new Dictionary<string, ESRI.ArcGIS.OSM.OSMClassExtension.osmfeature>();

            foreach (ESRI.ArcGIS.OSM.OSMClassExtension.osmfeature osmFeature in availableEditFeatures.osmfeature)
            {
                try
                {
                    m_editFeatures.Add(osmFeature.name, osmFeature);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                }
            }

            m_editTags = new Dictionary<string, ESRI.ArcGIS.OSM.OSMClassExtension.tagkey>();

            foreach (ESRI.ArcGIS.OSM.OSMClassExtension.tagkey osmTagKey in availableEditFeatures.osmtags)
            {
                try
                {
                    m_editTags.Add(osmTagKey.name, osmTagKey);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                }
            }

            // sort the tags alphabetically
            m_editTags.OrderBy(tagkey => tagkey.Key);

            m_baseInfoURI = availableEditFeatures.baseInfoURI;
        }

        public void prepareGrid4Features(IEnumRow currentFeatures)
        {
            if (m_inspector == null)
            {
                //this is the default inspector shipped with the editor
                m_inspector = new FeatureInspector();

                SetParent(dataGridView1.Handle.ToInt32(), this.Handle.ToInt32());
            }


            DataGridView featureGridView = this.dataGridView1;

            // reset the feature grid
            featureGridView.Rows.Clear();


            if (currentFeatures == null)
            {
                return;
            }

            currentFeatures.Reset();

            IFeature currentFeature = currentFeatures.Next() as IFeature;

            Dictionary<string, ESRI.ArcGIS.OSM.OSMClassExtension.tagkey> potentialTags = new Dictionary<string, ESRI.ArcGIS.OSM.OSMClassExtension.tagkey>();


            // determine a unique collection of proposed and existing tags
            List<string> uniqueListofTags = new List<string>();
            Dictionary<string, string> commonTags = new Dictionary<string, string>();
            Geometry.esriGeometryType currentGeometryType = esriGeometryType.esriGeometryNull;
            IEnumerable<ESRI.ArcGIS.OSM.OSMClassExtension.tag> sameTags = null;

            while (currentFeature != null)
            {
                int osmTagsFieldIndex = currentFeature.Fields.FindField("osmTags");
                currentGeometryType = currentFeature.Shape.GeometryType;

                if (osmTagsFieldIndex != -1)
                {
                    ESRI.ArcGIS.OSM.OSMClassExtension.tag[] tagsOnCurrentFeature = _osmUtility.retrieveOSMTags((IRow)currentFeature, osmTagsFieldIndex, m_editor.EditWorkspace);

                    if (sameTags == null && tagsOnCurrentFeature != null)
                    {
                        sameTags = tagsOnCurrentFeature.ToArray<ESRI.ArcGIS.OSM.OSMClassExtension.tag>();
                    }
                    else if (sameTags != null && tagsOnCurrentFeature != null)
                    {
                        IEnumerable<ESRI.ArcGIS.OSM.OSMClassExtension.tag> both = tagsOnCurrentFeature.Intersect(sameTags, new ESRI.ArcGIS.OSM.OSMClassExtension.TagKeyComparer());
                        sameTags = both.ToArray<ESRI.ArcGIS.OSM.OSMClassExtension.tag>();
                    }

                    if (tagsOnCurrentFeature != null)
                    {
                        for (int index = 0; index < tagsOnCurrentFeature.Length; index++)
                        {
                            if (uniqueListofTags.Contains(tagsOnCurrentFeature[index].k) == false)
                            {
                                uniqueListofTags.Add(tagsOnCurrentFeature[index].k);
                            }

                            // check if the tag key already exists
                            if (commonTags.ContainsKey(tagsOnCurrentFeature[index].k) == true)
                            {
                                if (commonTags[tagsOnCurrentFeature[index].k] == tagsOnCurrentFeature[index].v)
                                {
                                    // the tag values still match - don't do anything
                                }
                                else
                                {
                                    // the values are different - purge the existing value
                                    commonTags[tagsOnCurrentFeature[index].k] = String.Empty;
                                }
                            }
                            else
                            {
                                // the tag doesn't exist yet in the overall collection, 
                                // add the first entry
                                commonTags.Add(tagsOnCurrentFeature[index].k, tagsOnCurrentFeature[index].v);
                            }
                        }
                    }
                }

                // determine potential tag candidates based on the osmfeature schema
                string featureString = String.Empty;

                //let's get the first domain entry and use it as the main feature theme

                for (int fieldIndex = 0; fieldIndex < currentFeature.Fields.FieldCount; fieldIndex++)
                {
                    if (String.IsNullOrEmpty(featureString))
                    {
                        if (currentFeature.Fields.get_Field(fieldIndex).Type == esriFieldType.esriFieldTypeString)
                        {
                            System.Object attributeValue = currentFeature.get_Value(fieldIndex);

                            if (attributeValue != System.DBNull.Value)
                            {
                                foreach (string lookingforDomain in m_domainDictionary.Keys)
                                {
                                    if (currentFeature.Fields.get_Field(fieldIndex).Name == lookingforDomain)
                                    {
                                        featureString = lookingforDomain + "=" + attributeValue.ToString();
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        break;
                    }
                }

                ESRI.ArcGIS.OSM.OSMClassExtension.osmfeature OSMInspectorFeatureType = null;

                if (m_editFeatures.Keys.Contains(featureString))
                {
                    OSMInspectorFeatureType = m_editFeatures[featureString];
                }

                if (OSMInspectorFeatureType != null)
                {
                    if (OSMInspectorFeatureType.tag != null)
                    {
                        for (int index = 0; index < OSMInspectorFeatureType.tag.Length; index++)
                        {
                            if (commonTags.ContainsKey(OSMInspectorFeatureType.tag[index].@ref) == false)
                            {
                                commonTags.Add(OSMInspectorFeatureType.tag[index].@ref, String.Empty);
                            }
                        }
                    }
                }

                currentFeature = currentFeatures.Next() as IFeature;
            }


            // get a listing of all possible tags
            Dictionary<string, ESRI.ArcGIS.OSM.OSMClassExtension.tagkey> localTags = new Dictionary<string, ESRI.ArcGIS.OSM.OSMClassExtension.tagkey>();

            foreach (ESRI.ArcGIS.OSM.OSMClassExtension.tagkey tagKeyItem in m_editTags.Values)
            {
                localTags.Add(tagKeyItem.name, tagKeyItem);
            }


            // now let's go through our unique list of proposed and existing tags
            // and fill the grid accordingly

            DataGridViewRow currentRow = null;

            foreach (KeyValuePair<string, string> osmTagValuePair in commonTags)
            {
                currentRow = new DataGridViewRow();

                // name of the tag - tag type
                DataGridViewCell currentTagCell = new DataGridViewTextBoxCell();
                currentTagCell.Value = osmTagValuePair.Key;

                // for localization include the translated language into a tooltip
                if (m_editTags.ContainsKey(osmTagValuePair.Key))
                {
                    if (!String.IsNullOrEmpty(m_editTags[osmTagValuePair.Key].displayname))
                    {
                        currentTagCell.ToolTipText = m_editTags[osmTagValuePair.Key].displayname;
                    }
                }


                currentRow.Cells.Insert(0, currentTagCell);

                // the default case is not to allow the user change the key field
                bool canEdit = false;

                if (m_editTags.ContainsKey(osmTagValuePair.Key))
                {
                    if (m_editTags[osmTagValuePair.Key].editableSpecified)
                    {
                        canEdit = m_editTags[osmTagValuePair.Key].editable;
                    }
                }

                currentRow.Cells[0].ReadOnly = !canEdit;

                // value of the tag
                // depending on the tag type we'll need to create a different cell type
                DataGridViewCell currentValueCell = null;

                if (m_editTags.ContainsKey(osmTagValuePair.Key))
                {
                    switch (m_editTags[osmTagValuePair.Key].tagtype)
                    {
                        case ESRI.ArcGIS.OSM.OSMClassExtension.tagkeyTagtype.tag_list:
                            currentValueCell = new DataGridViewComboBoxCell();
                            try
                            {
                                foreach (ESRI.ArcGIS.OSM.OSMClassExtension.tagvalue value in m_editTags[osmTagValuePair.Key].tagvalue)
                                {
                                    ((DataGridViewComboBoxCell)currentValueCell).Items.Add(value.name);
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine(ex.Message);
                            }
                            break;
                        case ESRI.ArcGIS.OSM.OSMClassExtension.tagkeyTagtype.tag_integer:
                            currentValueCell = new DataGridViewTextBoxCell();
                            break;
                        case ESRI.ArcGIS.OSM.OSMClassExtension.tagkeyTagtype.tag_double:
                            currentValueCell = new DataGridViewTextBoxCell();
                            break;
                        case ESRI.ArcGIS.OSM.OSMClassExtension.tagkeyTagtype.tag_string:
                            currentValueCell = new DataGridViewTextBoxCell();
                            break;
                        default:
                            currentValueCell = new DataGridViewTextBoxCell();
                            break;
                    }
                }
                else if (m_domainDictionary.ContainsKey(osmTagValuePair.Key))
                {
                    currentValueCell = new DataGridViewComboBoxCell();
                    ESRI.ArcGIS.OSM.OSMClassExtension.domain currentDomain = null;

                    switch (currentGeometryType)
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
                            currentDomain = m_domainDictionary[osmTagValuePair.Key];

                            foreach (ESRI.ArcGIS.OSM.OSMClassExtension.domainvalue item in currentDomain.domainvalue)
                            {
                                for (int geometryIndex = 0; geometryIndex < item.geometrytype.Length; geometryIndex++)
                                {
                                    if (item.geometrytype[geometryIndex] == ESRI.ArcGIS.OSM.OSMClassExtension.geometrytype.point)
                                    {
                                        try
                                        {
                                            ((DataGridViewComboBoxCell)currentValueCell).Items.Add(item.value);
                                        }
                                        catch (Exception ex)
                                        {
                                            System.Diagnostics.Debug.WriteLine(ex.Message);
                                        }
                                    }
                                }
                            }
                            break;
                        case esriGeometryType.esriGeometryPolygon:
                            currentDomain = m_domainDictionary[osmTagValuePair.Key];

                            foreach (ESRI.ArcGIS.OSM.OSMClassExtension.domainvalue item in currentDomain.domainvalue)
                            {
                                for (int geometryIndex = 0; geometryIndex < item.geometrytype.Length; geometryIndex++)
                                {
                                    if (item.geometrytype[geometryIndex] == ESRI.ArcGIS.OSM.OSMClassExtension.geometrytype.polygon)
                                    {
                                        try
                                        {
                                            ((DataGridViewComboBoxCell)currentValueCell).Items.Add(item.value);
                                        }
                                        catch (Exception ex)
                                        {
                                            System.Diagnostics.Debug.WriteLine(ex.Message);
                                        }
                                    }
                                }
                            }
                            break;
                        case esriGeometryType.esriGeometryPolyline:
                            currentDomain = m_domainDictionary[osmTagValuePair.Key];

                            foreach (ESRI.ArcGIS.OSM.OSMClassExtension.domainvalue item in currentDomain.domainvalue)
                            {
                                for (int geometryIndex = 0; geometryIndex < item.geometrytype.Length; geometryIndex++)
                                {
                                    if (item.geometrytype[geometryIndex] == ESRI.ArcGIS.OSM.OSMClassExtension.geometrytype.line)
                                    {
                                        try
                                        {
                                            ((DataGridViewComboBoxCell)currentValueCell).Items.Add(item.value);
                                        }
                                        catch (Exception ex)
                                        {
                                            System.Diagnostics.Debug.WriteLine(ex.Message);
                                        }
                                    }
                                }
                            }
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
                else
                { // unkown keys are treated as strings
                    currentValueCell = new DataGridViewTextBoxCell();
                }

                // add the value only we have a value and if the tag is common among all features
                if (String.IsNullOrEmpty(osmTagValuePair.Value) == false)
                {
                    ESRI.ArcGIS.OSM.OSMClassExtension.tag compareTag = new ESRI.ArcGIS.OSM.OSMClassExtension.tag();
                    compareTag.k = osmTagValuePair.Key;

                    if (sameTags.Contains(compareTag, new ESRI.ArcGIS.OSM.OSMClassExtension.TagKeyComparer()))
                    {
                        currentValueCell.Value = osmTagValuePair.Value;
                    }
                }

                // for localization include the translated language into a tooltip
                if (m_editTags.ContainsKey((string)currentTagCell.Value))
                {
                    if (!String.IsNullOrEmpty((string)currentValueCell.Value))
                    {
                        ESRI.ArcGIS.OSM.OSMClassExtension.tagvalue[] possibleValues = m_editTags[(string)currentTagCell.Value].tagvalue;

                        if (possibleValues != null)
                        {
                            for (int valueIndex = 0; valueIndex < possibleValues.Length; valueIndex++)
                            {
                                if (currentValueCell.Value.Equals(possibleValues[valueIndex].name) == true)
                                {
                                    if (!String.IsNullOrEmpty(possibleValues[valueIndex].displayname))
                                    {
                                        currentValueCell.ToolTipText = possibleValues[valueIndex].displayname;
                                    }
                                }
                            }
                        }
                    }
                }

                currentRow.Cells.Insert(1, currentValueCell);

                // the assumption here is that values are usually open to user edits
                canEdit = true;
                currentRow.Cells[1].ReadOnly = !canEdit;

                DataGridViewLinkCell currentInfoCell = new DataGridViewLinkCell();
                currentInfoCell.LinkBehavior = LinkBehavior.SystemDefault;

                if (m_editTags.ContainsKey(osmTagValuePair.Key))
                {
                    if (String.IsNullOrEmpty(m_editTags[osmTagValuePair.Key].infoURL))
                    {
                        currentInfoCell.Value = new Uri(m_baseInfoURI + HttpUtility.UrlEncode("Key:" + osmTagValuePair.Key));
                    }
                    else
                    {
                        currentInfoCell.Value = new Uri(m_editTags[osmTagValuePair.Key].infoURL);
                    }
                    currentRow.Cells.Insert(2, currentInfoCell);
                }
                else
                {
                    currentInfoCell.Value = new Uri(m_baseInfoURI + HttpUtility.UrlEncode("Key:" + osmTagValuePair.Key));
                }

                featureGridView.Rows.Add(currentRow);

                if (localTags.ContainsKey(osmTagValuePair.Key))
                {
                    localTags.Remove(osmTagValuePair.Key);
                }
            }

            // add the list in the first column of the last row
            DataGridViewRow lastRow = new DataGridViewRow();
            DataGridViewComboBoxCell currentKeyCell = new DataGridViewComboBoxCell();
            try
            {
                // show a sorted list of tags to the user
                IEnumerable<string> sortedKeys = localTags.Keys.OrderBy(myKey => myKey);

                foreach (string currentKey in sortedKeys)
                {
                    currentKeyCell.Items.Add(currentKey);
                }
            }
            catch { }

            lastRow.Cells.Insert(0, currentKeyCell);

            featureGridView.Rows.Add(lastRow);

        }

        public void prepareGrid4Feature(IFeature currentFeature)
        {
            if (m_inspector == null)
            {
                //this is the default inspector shipped with the editor
                m_inspector = new FeatureInspector();

                SetParent(dataGridView1.Handle.ToInt32(), this.Handle.ToInt32());
            }

            DataGridView featureGridView = this.dataGridView1;

            string featureString = String.Empty;

            //let's get the first domain entry and use it as the main feature theme

            for (int fieldIndex = 0; fieldIndex < currentFeature.Fields.FieldCount; fieldIndex++)
            {
                if (String.IsNullOrEmpty(featureString))
                {
                    if (currentFeature.Fields.get_Field(fieldIndex).Type == esriFieldType.esriFieldTypeString)
                    {
                        System.Object attributeValue = currentFeature.get_Value(fieldIndex);

                        if (attributeValue != System.DBNull.Value)
                        {
                            foreach (string lookingforDomain in m_domainDictionary.Keys)
                            {
                                if (currentFeature.Fields.get_Field(fieldIndex).Name == lookingforDomain)
                                {
                                    featureString = lookingforDomain + "=" + attributeValue.ToString();
                                    break;
                                }
                            }
                        }
                    }
                }
                else
                {
                    break;
                }
            }

            ESRI.ArcGIS.OSM.OSMClassExtension.osmfeature currentFeatureType = null;

            if (m_editFeatures.Keys.Contains(featureString))
            {
                currentFeatureType = m_editFeatures[featureString];
            }


            // reset the feature grid
            featureGridView.Rows.Clear();


            // rehydrate the tag from the container field
            Dictionary<string, ESRI.ArcGIS.OSM.OSMClassExtension.tag> tagList = new Dictionary<string, ESRI.ArcGIS.OSM.OSMClassExtension.tag>();
            ESRI.ArcGIS.OSM.OSMClassExtension.tag[] storedTags = null;

            Dictionary<string, ESRI.ArcGIS.OSM.OSMClassExtension.tagkey> localTags = new Dictionary<string, ESRI.ArcGIS.OSM.OSMClassExtension.tagkey>();

            foreach (ESRI.ArcGIS.OSM.OSMClassExtension.tagkey tagKeyItem in m_editTags.Values)
            {
                localTags.Add(tagKeyItem.name, tagKeyItem);
            }

            int tagCollectionFieldIndex = currentFeature.Fields.FindField("osmTags");

            storedTags = _osmUtility.retrieveOSMTags((IRow)currentFeature, tagCollectionFieldIndex, m_editor.EditWorkspace);

            if (storedTags != null)
            {
                foreach (ESRI.ArcGIS.OSM.OSMClassExtension.tag currenttag in storedTags)
                {
                    tagList.Add(currenttag.k, currenttag);
                }
            }

            // if we have a known entity of tag - OSM map feature that is
            if (currentFeatureType != null)
            {
                DataGridViewRow currentRow = new DataGridViewRow();

                // the feature itself is the main osm key value pair
                string[] osmkeyvalue = null;

                try
                {
                    osmkeyvalue = currentFeatureType.name.Split("=".ToCharArray());
                }
                catch { }


                if (osmkeyvalue != null && osmkeyvalue.Length > 1)
                {
                    // name of the tag - tag type
                    DataGridViewCell currentTagCell = new DataGridViewTextBoxCell();
                    currentTagCell.Value = osmkeyvalue[0];

                    // for localization include the translated language into a tooltip
                    if (m_editTags.ContainsKey(osmkeyvalue[0]))
                    {
                        if (!String.IsNullOrEmpty(m_editTags[osmkeyvalue[0]].displayname))
                        {
                            currentTagCell.ToolTipText = m_editTags[osmkeyvalue[0]].displayname;
                        }
                    }

                    currentRow.Cells.Insert(0, currentTagCell);
                    currentRow.Cells[0].ReadOnly = true;

                    // value of the tag
                    DataGridViewCell currentValueCell = null;
                    currentValueCell = new DataGridViewComboBoxCell();
                    foreach (var domainvalue in m_domainDictionary[osmkeyvalue[0]].domainvalue)
                    {
                        if (IsGeometryTypeEqual(currentFeature.Shape.GeometryType, domainvalue.geometrytype))
                        {
                            ((DataGridViewComboBoxCell)currentValueCell).Items.Add(domainvalue.value);
                        }
                    }
                    currentValueCell.Value = osmkeyvalue[1];

                    // for localization include the translated language into a tooltip
                    if (m_editTags.ContainsKey(osmkeyvalue[0]))
                    {
                        ESRI.ArcGIS.OSM.OSMClassExtension.tagvalue[] possibleValues = m_editTags[osmkeyvalue[0]].tagvalue;

                        if (possibleValues != null)
                        {
                            for (int valueIndex = 0; valueIndex < possibleValues.Length; valueIndex++)
                            {
                                if (osmkeyvalue[1].Equals(possibleValues[valueIndex].name) == true)
                                {
                                    if (!String.IsNullOrEmpty(possibleValues[valueIndex].displayname))
                                    {
                                        currentValueCell.ToolTipText = possibleValues[valueIndex].displayname;
                                    }
                                }
                            }
                        }
                    }

                    currentRow.Cells.Insert(1, currentValueCell);
                    currentRow.Cells[1].ReadOnly = false;


                    DataGridViewLinkCell currentInfoCell = new DataGridViewLinkCell();
                    currentInfoCell.LinkBehavior = LinkBehavior.SystemDefault;
                    currentInfoCell.Value = new Uri(m_baseInfoURI + HttpUtility.UrlEncode("Tag:" + currentFeatureType.name));
                    currentRow.Cells.Insert(2, currentInfoCell);

                    featureGridView.Rows.Add(currentRow);

                    if (tagList.Keys.Contains(osmkeyvalue[0]))
                    {
                        tagList.Remove(osmkeyvalue[0]);
                    }

                    if (localTags.ContainsKey(osmkeyvalue[0]))
                    {
                        localTags.Remove(osmkeyvalue[0]);
                    }
                }

                if (currentFeatureType.tag != null)
                {
                    foreach (ESRI.ArcGIS.OSM.OSMClassExtension.tag osmTagValuePair in currentFeatureType.tag)
                    {
                        try
                        {
                            currentRow = new DataGridViewRow();

                            // name of the tag - tag type
                            DataGridViewCell currentTagCell = new DataGridViewTextBoxCell();
                            currentTagCell.Value = osmTagValuePair.@ref;

                            // for localization include the translated language into a tooltip
                            if (m_editTags.ContainsKey(osmTagValuePair.@ref))
                            {
                                if (!String.IsNullOrEmpty(m_editTags[osmTagValuePair.@ref].displayname))
                                {
                                    currentTagCell.ToolTipText = m_editTags[osmTagValuePair.@ref].displayname;
                                }
                            }

                            currentRow.Cells.Insert(0, currentTagCell);

                            // the default case is not to allow the user change the key field
                            bool canEdit = false;

                            if (m_editTags.ContainsKey(osmTagValuePair.@ref))
                            {
                                if (m_editTags[osmTagValuePair.@ref].editableSpecified)
                                {
                                    canEdit = m_editTags[osmTagValuePair.@ref].editable;
                                }
                            }

                            currentRow.Cells[0].ReadOnly = !canEdit;

                            // value of the tag
                            // depending on the tag type we'll need to create a different cell type
                            DataGridViewCell currentValueCell = null;

                            if (m_editTags.ContainsKey(osmTagValuePair.@ref))
                            {
                                switch (m_editTags[osmTagValuePair.@ref].tagtype)
                                {
                                    case ESRI.ArcGIS.OSM.OSMClassExtension.tagkeyTagtype.tag_list:
                                        currentValueCell = new DataGridViewComboBoxCell();
                                        try
                                        {
                                            foreach (ESRI.ArcGIS.OSM.OSMClassExtension.tagvalue value in m_editTags[osmTagValuePair.@ref].tagvalue)
                                            {
                                                ((DataGridViewComboBoxCell)currentValueCell).Items.Add(value.name);
                                            }
                                        }

                                        catch (Exception ex)
                                        {
                                            System.Diagnostics.Debug.WriteLine(ex.Message);
                                        }
                                        break;
                                    case ESRI.ArcGIS.OSM.OSMClassExtension.tagkeyTagtype.tag_integer:
                                        currentValueCell = new DataGridViewTextBoxCell();
                                        break;
                                    case ESRI.ArcGIS.OSM.OSMClassExtension.tagkeyTagtype.tag_double:
                                        currentValueCell = new DataGridViewTextBoxCell();
                                        break;
                                    case ESRI.ArcGIS.OSM.OSMClassExtension.tagkeyTagtype.tag_string:
                                        currentValueCell = new DataGridViewTextBoxCell();
                                        break;
                                    default:
                                        currentValueCell = new DataGridViewTextBoxCell();
                                        break;
                                }
                            }
                            else
                            { // unkown keys are treated as strings
                                currentValueCell = new DataGridViewTextBoxCell();
                            }

                            if (tagList.Keys.Contains(osmTagValuePair.@ref))
                            {
                                currentValueCell.Value = tagList[osmTagValuePair.@ref].v;
                            }
                            else
                            {
                                if (osmTagValuePair.value != null)
                                {
                                    currentValueCell.Value = osmTagValuePair.value;
                                }
                            }

                            // for localization include the translated language into a tooltip
                            if (m_editTags.ContainsKey((string)currentTagCell.Value))
                            {
                                if (!String.IsNullOrEmpty((string)currentValueCell.Value))
                                {
                                    ESRI.ArcGIS.OSM.OSMClassExtension.tagvalue[] possibleValues = m_editTags[(string)currentTagCell.Value].tagvalue;

                                    if (possibleValues != null)
                                    {
                                        for (int valueIndex = 0; valueIndex < possibleValues.Length; valueIndex++)
                                        {
                                            if (currentValueCell.Value.Equals(possibleValues[valueIndex].name) == true)
                                            {
                                                if (!String.IsNullOrEmpty(possibleValues[valueIndex].displayname))
                                                {
                                                    currentValueCell.ToolTipText = possibleValues[valueIndex].displayname;
                                                }
                                            }
                                        }
                                    }
                                }
                            }

                            currentRow.Cells.Insert(1, currentValueCell);

                            // the assumption here is that values are usually open to user edits
                            canEdit = true;
                            currentRow.Cells[1].ReadOnly = !canEdit;

                            DataGridViewLinkCell currentInfoCell = new DataGridViewLinkCell();
                            currentInfoCell.LinkBehavior = LinkBehavior.SystemDefault;

                            if (m_editTags.ContainsKey(osmTagValuePair.@ref))
                            {
                                if (String.IsNullOrEmpty(m_editTags[osmTagValuePair.@ref].infoURL))
                                {
                                    currentInfoCell.Value = new Uri(m_baseInfoURI + HttpUtility.UrlEncode("Key:" + osmTagValuePair.@ref));
                                }
                                else
                                {
                                    currentInfoCell.Value = new Uri(m_editTags[osmTagValuePair.@ref].infoURL);
                                }
                                currentRow.Cells.Insert(2, currentInfoCell);
                            }
                            else
                            {
                                currentInfoCell.Value = new Uri(m_baseInfoURI + HttpUtility.UrlEncode("Key:" + osmTagValuePair.@ref));
                            }

                            featureGridView.Rows.Add(currentRow);

                            tagList.Remove(osmTagValuePair.@ref);

                            if (localTags.ContainsKey(osmTagValuePair.@ref))
                            {
                                localTags.Remove(osmTagValuePair.@ref);
                            }
                        }
                        catch { }
                    }
                }
            }


            // for all the remaining tags of whatever is passed into this function - known or unkown
            // list the remaining tag key/value pairs
            foreach (ESRI.ArcGIS.OSM.OSMClassExtension.tag currentTag in tagList.Values)
            {
                DataGridViewRow currentRow = new DataGridViewRow();

                DataGridViewCell currentTagCell = new DataGridViewTextBoxCell();
                currentTagCell.Value = currentTag.k;


                // for localization include the translated language into a tooltip
                if (m_editTags.ContainsKey(currentTag.k))
                {
                    if (!String.IsNullOrEmpty(m_editTags[currentTag.k].displayname))
                    {
                        currentTagCell.ToolTipText = m_editTags[currentTag.k].displayname;
                    }
                }


                currentRow.Cells.Insert(0, currentTagCell);

                DataGridViewCell currentValueCell = null;
                if (m_editTags.ContainsKey(currentTag.k))
                {
                    switch (m_editTags[currentTag.k].tagtype)
                    {
                        case ESRI.ArcGIS.OSM.OSMClassExtension.tagkeyTagtype.tag_list:
                            currentValueCell = new DataGridViewComboBoxCell();
                            try
                            {
                                foreach (ESRI.ArcGIS.OSM.OSMClassExtension.tagvalue value in m_editTags[currentTag.k].tagvalue)
                                {
                                    ((DataGridViewComboBoxCell)currentValueCell).Items.Add(value.name);
                                }

                                currentValueCell.Value = currentTag.v;
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine(ex.Message);
                            }
                            break;
                        case ESRI.ArcGIS.OSM.OSMClassExtension.tagkeyTagtype.tag_integer:
                            currentValueCell = new DataGridViewTextBoxCell();
                            currentValueCell.Value = Convert.ToInt32(currentTag.v);
                            break;
                        case ESRI.ArcGIS.OSM.OSMClassExtension.tagkeyTagtype.tag_double:
                            currentValueCell = new DataGridViewTextBoxCell();
                            currentValueCell.Value = Convert.ToDouble(currentTag.v);
                            break;
                        case ESRI.ArcGIS.OSM.OSMClassExtension.tagkeyTagtype.tag_string:
                            currentValueCell = new DataGridViewTextBoxCell();
                            currentValueCell.Value = Convert.ToString(currentTag.v);
                            break;
                        default:
                            currentValueCell = new DataGridViewTextBoxCell();
                            break;
                    }
                }
                else
                {
                    currentValueCell = new DataGridViewTextBoxCell();
                    currentValueCell.Value = Convert.ToString(currentTag.v);
                }

                try
                {
                    // for localization include the translated language into a tooltip
                    if (m_editTags.ContainsKey(currentTag.k))
                    {
                        if (!String.IsNullOrEmpty(Convert.ToString(currentValueCell.Value)))
                        {
                            ESRI.ArcGIS.OSM.OSMClassExtension.tagvalue[] possibleValues = m_editTags[currentTag.k].tagvalue;

                            if (possibleValues != null)
                            {
                                for (int valueIndex = 0; valueIndex < possibleValues.Length; valueIndex++)
                                {
                                    if (currentTag.v.Equals(possibleValues[valueIndex].name) == true)
                                    {
                                        if (!String.IsNullOrEmpty(possibleValues[valueIndex].displayname))
                                        {
                                            currentValueCell.ToolTipText = possibleValues[valueIndex].displayname;
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }

                currentRow.Cells.Insert(1, currentValueCell);

                DataGridViewLinkCell currentInfoCell = new DataGridViewLinkCell();
                currentInfoCell.LinkBehavior = LinkBehavior.SystemDefault;

                if (m_editTags.ContainsKey(currentTag.k))
                {
                    if (String.IsNullOrEmpty(m_editTags[currentTag.k].infoURL))
                    {
                        currentInfoCell.Value = new Uri(m_baseInfoURI + HttpUtility.UrlEncode("Key:" + currentTag.k));
                    }
                    else
                    {
                        currentInfoCell.Value = new Uri(m_editTags[currentTag.k].infoURL);
                    }
                    currentRow.Cells.Insert(2, currentInfoCell);
                }
                else
                {
                    currentInfoCell.Value = new Uri(m_baseInfoURI + HttpUtility.UrlEncode("Key:" + currentTag.k));
                }

                featureGridView.Rows.Add(currentRow);

                if (localTags.ContainsKey(currentTag.k))
                {
                    localTags.Remove(currentTag.k);
                }
            }

            // add the list of remaning known tags in the first column of the last row in ascending order
            DataGridViewRow lastRow = new DataGridViewRow();
            DataGridViewComboBoxCell currentKeyCell = new DataGridViewComboBoxCell();
            try
            {
                // show a sorted list of tags to the user
                IEnumerable<string> sortedKeys = localTags.Keys.OrderBy(myKey => myKey);

                foreach (string currentKey in sortedKeys)
                {
                    currentKeyCell.Items.Add(currentKey);
                }

            }
            catch { }

            lastRow.Cells.Insert(0, currentKeyCell);

            featureGridView.Rows.Add(lastRow);

        }

        private bool IsGeometryTypeEqual(esriGeometryType featureGeometryType, ESRI.ArcGIS.OSM.OSMClassExtension.geometrytype[] osmGeometryType)
        {
            bool geometryEquality = false;

            try
            {
                for (int typeIndex = 0; typeIndex < osmGeometryType.Length; typeIndex++)
                {
                    switch (osmGeometryType[typeIndex])
                    {
                        case ESRI.ArcGIS.OSM.OSMClassExtension.geometrytype.point:
                            if (featureGeometryType == esriGeometryType.esriGeometryPoint)
                            {
                                geometryEquality = true;
                            }
                            break;
                        case ESRI.ArcGIS.OSM.OSMClassExtension.geometrytype.line:
                            if (featureGeometryType == esriGeometryType.esriGeometryPolyline)
                            {
                                geometryEquality = true;
                            }
                            break;
                        case ESRI.ArcGIS.OSM.OSMClassExtension.geometrytype.mpolyline:
                            if (featureGeometryType == esriGeometryType.esriGeometryPolyline)
                            {
                                geometryEquality = true;
                            }
                            break;
                        case ESRI.ArcGIS.OSM.OSMClassExtension.geometrytype.polygon:
                            if (featureGeometryType == esriGeometryType.esriGeometryPolygon)
                            {
                                geometryEquality = true;
                            }
                            break;
                        default:
                            break;
                    }
                }
            }
            catch { }

            return geometryEquality;
        }

        private void dataGridView1_CellContentDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (dataGridView1[e.ColumnIndex, e.RowIndex] is DataGridViewLinkCell)
            {
                DataGridViewLinkCell infoCell = dataGridView1[e.ColumnIndex, e.RowIndex] as DataGridViewLinkCell;

                try
                {
                    System.Diagnostics.Process.Start(((Uri)infoCell.Value).AbsoluteUri);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                }
            }
        }

        private void dataGridView1_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            string cellValueString = String.Empty;

            if (e.ColumnIndex == -1 || e.RowIndex == -1)
            {
                return;
            }


            try
            {
                // handle the event when the value in the first column changed
                // -- if an existing tag key was selected populate the 2nd column and the info 3rd column
                // -- add a new last row with the remaining tag key entries
                if (e.ColumnIndex == 0)
                {
                    Dictionary<string, ESRI.ArcGIS.OSM.OSMClassExtension.tagkey> localTags = new Dictionary<string, ESRI.ArcGIS.OSM.OSMClassExtension.tagkey>();
                    foreach (ESRI.ArcGIS.OSM.OSMClassExtension.tagkey tagKey in m_editTags.Values)
                    {
                        localTags.Add(tagKey.name, tagKey);
                    }

                    string keyValue = string.Empty;
                    // remove the once already used
                    for (int rowIndex = 0; rowIndex < dataGridView1.Rows.Count; rowIndex++)
                    {
                        keyValue = dataGridView1[0, rowIndex].Value as string;
                        if (!String.IsNullOrEmpty(keyValue))
                        {
                            if (localTags.ContainsKey(keyValue))
                            {
                                localTags.Remove(keyValue);
                            }
                        }
                    }


                    keyValue = dataGridView1[e.ColumnIndex, e.RowIndex].Value as string;
                    int deletedRow = -1;

                    // if the 
                    if (string.IsNullOrEmpty(keyValue))
                    {
                        if (!string.IsNullOrEmpty(m_ChangeTagString))
                        {
                            keyValue = m_ChangeTagString;
                            m_ChangeTagString = string.Empty;
                        }
                    }
                    else
                    {
                        try
                        {
                            deletedRow = e.RowIndex;
                            dataGridView1.Rows.RemoveAt(e.RowIndex);
                        }
                        catch
                        {

                        }
                    }

                    if (!string.IsNullOrEmpty(keyValue))
                    {
                        DataGridViewRow newRow = new DataGridViewRow();

                        // name of the tag - tag type
                        DataGridViewCell currentTagCell = new DataGridViewTextBoxCell();
                        currentTagCell.Value = keyValue;

                        // for localization include the translated language into a tooltip
                        if (m_editTags.ContainsKey(keyValue))
                        {
                            if (!String.IsNullOrEmpty(m_editTags[keyValue].displayname))
                            {
                                currentTagCell.ToolTipText = m_editTags[keyValue].displayname;
                            }
                        }

                        newRow.Cells.Insert(0, currentTagCell);

                        // the default case is not to allow the user change the key field
                        bool canEdit = false;

                        if (m_editTags.ContainsKey(keyValue))
                        {
                            if (m_editTags[keyValue].editableSpecified)
                            {
                                canEdit = m_editTags[keyValue].editable;
                            }
                        }

                        newRow.Cells[0].ReadOnly = !canEdit;


                        if (m_editTags.ContainsKey(keyValue))
                        {
                            DataGridViewCell currentValueCell = null;
                            switch (m_editTags[keyValue].tagtype)
                            {
                                case ESRI.ArcGIS.OSM.OSMClassExtension.tagkeyTagtype.tag_list:
                                    currentValueCell = new DataGridViewComboBoxCell();
                                    try
                                    {
                                        foreach (ESRI.ArcGIS.OSM.OSMClassExtension.tagvalue value in m_editTags[keyValue].tagvalue)
                                        {
                                            ((DataGridViewComboBoxCell)currentValueCell).Items.Add(value.name);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        System.Diagnostics.Debug.WriteLine(ex.Message);
                                    }
                                    break;
                                case ESRI.ArcGIS.OSM.OSMClassExtension.tagkeyTagtype.tag_integer:
                                    currentValueCell = new DataGridViewTextBoxCell();
                                    break;
                                case ESRI.ArcGIS.OSM.OSMClassExtension.tagkeyTagtype.tag_double:
                                    currentValueCell = new DataGridViewTextBoxCell();
                                    break;
                                case ESRI.ArcGIS.OSM.OSMClassExtension.tagkeyTagtype.tag_string:
                                    currentValueCell = new DataGridViewTextBoxCell();
                                    break;
                                default:
                                    currentValueCell = new DataGridViewTextBoxCell();
                                    break;
                            }
                            try
                            {
                                newRow.Cells.Insert(1, currentValueCell);
                            }
                            catch
                            {

                            }

                            DataGridViewLinkCell currentInfoCell = new DataGridViewLinkCell();
                            currentInfoCell.LinkBehavior = LinkBehavior.SystemDefault;

                            if (m_editTags.ContainsKey(keyValue))
                            {
                                if (String.IsNullOrEmpty(m_editTags[keyValue].infoURL))
                                {
                                    currentInfoCell.Value = new Uri(m_baseInfoURI + HttpUtility.UrlEncode("Key:" + keyValue));
                                }
                                else
                                {
                                    currentInfoCell.Value = new Uri(m_editTags[keyValue].infoURL);
                                }
                            }
                            else
                            {
                                currentInfoCell.Value = new Uri(m_baseInfoURI + HttpUtility.UrlEncode("Key:" + keyValue));
                            }

                            try
                            {
                                newRow.Cells.Insert(2, currentInfoCell);
                            }
                            catch
                            {
                            }
                        }

                        dataGridView1.Rows.Add(newRow);

                        // add a new last row
                        DataGridViewRow lastRow = new DataGridViewRow();
                        DataGridViewComboBoxCell currentKeyCell = new DataGridViewComboBoxCell();
                        try
                        {
                            // show a sorted list of tags to the user
                            IEnumerable<string> sortedKeys = localTags.Keys.OrderBy(myKey => myKey);

                            foreach (string currentKey in sortedKeys)
                            {
                                currentKeyCell.Items.Add(currentKey);
                            }
                        }
                        catch { }

                        lastRow.Cells.Insert(0, currentKeyCell);

                        dataGridView1.Rows.Add(lastRow);

                        dataGridView1.FirstDisplayedScrollingRowIndex = e.RowIndex;
                        dataGridView1.Refresh();
                        dataGridView1.CurrentCell = dataGridView1.Rows[e.RowIndex].Cells[1];
                    }

                    return;
                }


                // ensure that we have values in the first and the second column before we commit an edit operation
                if (e.ColumnIndex == 0)
                {
                    cellValueString = dataGridView1[1, e.RowIndex].Value as string;
                }
                else if (e.ColumnIndex == 1)
                {
                    cellValueString = dataGridView1[0, e.RowIndex].Value as string;
                }

                if (String.IsNullOrEmpty(cellValueString))
                {
                    return;
                }

                if (m_editor == null)
                {
                    return;
                }

                if (((IWorkspaceEdit2)m_editor.EditWorkspace).IsInEditOperation)
                {
                }
                else
                //MessageBox.Show(resourceManager.GetString("OSMEditor_FeatureInspector_operationwarningcaption"), resourceManager.GetString("OSMClassExtension_FeatureInspector_operationwarningcaption"));
                {

                    m_editor.StartOperation();
                }

                ESRI.ArcGIS.OSM.OSMClassExtension.tag newTag = null;

                string tagString = dataGridView1[0, e.RowIndex].Value as string;
                string valueString = dataGridView1[1, e.RowIndex].Value as string;

                if (String.IsNullOrEmpty(tagString) || String.IsNullOrEmpty(valueString))
                {
                }
                else
                {
                    newTag = new ESRI.ArcGIS.OSM.OSMClassExtension.tag();
                    newTag.k = tagString;
                    newTag.v = valueString;
                }


                if (m_enumRow != null)
                {
                    m_enumRow.Reset();

                    // persist the collection in the blob/xml field
                    IFeature currentFeature = null;

                    while ((currentFeature = m_enumRow.Next() as IFeature) != null)
                    {
                        int tagCollectionFieldIndex = currentFeature.Fields.FindField("osmTags");

                        if (tagCollectionFieldIndex != -1)
                        {

                            ESRI.ArcGIS.OSM.OSMClassExtension.tag[] existingTags = _osmUtility.retrieveOSMTags((IRow)currentFeature, tagCollectionFieldIndex, m_editor.EditWorkspace);

                            if (existingTags != null)
                            {
                                // let's rebuild the tags from scratch based on the items that we found in the UI and the existing feature
                                Dictionary<string, string> existingTagsList = new Dictionary<string, string>();
                                for (int index = 0; index < existingTags.Length; index++)
                                {
                                    if (String.IsNullOrEmpty(existingTags[index].k) == false && String.IsNullOrEmpty(existingTags[index].v) == false)
                                    {
                                        existingTagsList.Add(existingTags[index].k, existingTags[index].v);
                                    }
                                }

                                if (newTag != null)
                                {
                                    if (existingTagsList.ContainsKey(newTag.k))
                                    {
                                        existingTagsList[newTag.k] = newTag.v;
                                    }
                                    else
                                    {
                                        if (!String.IsNullOrEmpty(newTag.v))
                                        {
                                            existingTagsList.Add(newTag.k, newTag.v);
                                        }
                                    }
                                }


                                int tagIndex = 0;

                                // convert the newly assembled tag list into a tag array that can be serialized into the osmtag field
                                existingTags = new ESRI.ArcGIS.OSM.OSMClassExtension.tag[existingTagsList.Count];
                                foreach (KeyValuePair<string, string> item in existingTagsList)
                                {
                                    if (!String.IsNullOrEmpty(item.Key) && !String.IsNullOrEmpty(item.Value))
                                    {
                                        ESRI.ArcGIS.OSM.OSMClassExtension.tag insertTag = new ESRI.ArcGIS.OSM.OSMClassExtension.tag();
                                        insertTag.k = item.Key;
                                        insertTag.v = item.Value;

                                        existingTags[tagIndex] = insertTag;
                                        tagIndex = tagIndex + 1;
                                    }
                                }

                                _osmUtility.insertOSMTags(tagCollectionFieldIndex, (IRow)currentFeature, existingTags, m_editor.EditWorkspace);

                                currentFeature.Store();
                            }
                        }
                    }
                }

                m_editor.StopOperation(resourceManager.GetString("OSMClassExtension_FeatureInspector_operationmenu"));

            }
            catch
            {
            }
        }

        // MSDN code to edit a drop down box
        private void dataGridView1_EditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            ComboBox c = e.Control as ComboBox;
            if (c != null) c.DropDownStyle = ComboBoxStyle.DropDown;
        }

        private void dataGridView1_CellValidating(object sender, DataGridViewCellValidatingEventArgs e)
        {
            //// the second column, index 1, contains the value cells
            //if (e.ColumnIndex == 1)
            //{
            object eFV = e.FormattedValue;

            if (dataGridView1[e.ColumnIndex, e.RowIndex] is DataGridViewComboBoxCell)
            {
                DataGridViewComboBoxCell currentComboBoxCell = dataGridView1[e.ColumnIndex, e.RowIndex] as DataGridViewComboBoxCell;

                if (!(currentComboBoxCell.Items.Contains(eFV)))
                {
                    currentComboBoxCell.Items.Add(eFV);

                    // if the validation happens in the first column, i.e. the collection of tags
                    // add the new value into the overall internal collection of edit tags as well
                    if (e.ColumnIndex == 0)
                    {
                        tagkey newTag = new tagkey();
                        newTag.name = Convert.ToString(eFV);
                        newTag.tagtype = tagkeyTagtype.tag_string;
                        newTag.tagvalue = new tagvalue[0];
                        newTag.editable = true;

                        m_editTags.Add(newTag.name, newTag);

                        m_ChangeTagString = Convert.ToString(eFV);
                    }
                }
            }
            //}
        }

        private void dataGridView1_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            // the second column, index 1, contains the value cells
            if (e.ColumnIndex == 1)
            {
                object eFV = dataGridView1[e.ColumnIndex, e.RowIndex].Value;

                if (dataGridView1[e.ColumnIndex, e.RowIndex] is DataGridViewComboBoxCell)
                {
                    DataGridViewComboBoxCell currentComboBoxCell = dataGridView1[e.ColumnIndex, e.RowIndex] as DataGridViewComboBoxCell;

                    if (!(currentComboBoxCell.Items.Contains(eFV)))
                    {
                        currentComboBoxCell.Items.Add(eFV);
                    }
                }
            }
        }

        private void dataGridView1_RowsRemoved(object sender, DataGridViewRowsRemovedEventArgs e)
        {

            if (m_editor == null)
                return;

            if (e.RowIndex == 0)
                return;

            if (m_enumRow == null)
                return;

            List<tag> currentTagList = new List<tag>();

            // loop through all the tags listed in the grid
            for (int rowIndex = 0; rowIndex < dataGridView1.RowCount; rowIndex++)
            {
                string tagKeyValue = dataGridView1[0, rowIndex].Value as string;
                string tagValueValue = dataGridView1[1, rowIndex].Value as string;

                if ((String.IsNullOrEmpty(tagKeyValue) == false) || (String.IsNullOrEmpty(tagValueValue) == false))
                {
                    tag newTag = new tag();
                    newTag.k = tagKeyValue;
                    newTag.v = tagValueValue;

                    currentTagList.Add(newTag);
                }
            }

            if (currentTagList.Count > 1)
            {

                if (((IWorkspaceEdit2)m_editor.EditWorkspace).IsInEditOperation)
                {
                }
                else
                {
                    //   MessageBox.Show(resourceManager.GetString("OSMEditor_FeatureInspector_operationwarningcaption"), resourceManager.GetString("OSMClassExtension_FeatureInspector_operationwarningcaption"));
                    m_editor.StartOperation();
                }

                try
                {
                    // persist the collection in the blob/xml field
                    m_enumRow.Reset();

                    IFeature currentFeature = null;

                    while ((currentFeature = m_enumRow.Next() as IFeature) != null)
                    {
                        int tagCollectionFieldIndex = currentFeature.Fields.FindField("osmTags");

                        if (tagCollectionFieldIndex != -1)
                        {
                            _osmUtility.insertOSMTags(tagCollectionFieldIndex, (IRow)currentFeature, currentTagList.ToArray(), m_editor.EditWorkspace);

                            currentFeature.Store();
                        }
                    }
                }
                catch
                {
                }
                finally
                {
                    m_editor.StopOperation(resourceManager.GetString("OSMEditor_FeatureInspector_operationmenu"));
                }
            }
        }

        private void ReadOSMEditorSettings()
        {
            string osmEditorFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + System.IO.Path.DirectorySeparatorChar + "ESRI" + System.IO.Path.DirectorySeparatorChar + "OSMEditor";
            string configurationfile = osmEditorFolder + System.IO.Path.DirectorySeparatorChar + "osmeditor.config";


            if (File.Exists(configurationfile))
            {
                using (XmlReader configurationFileReader = XmlReader.Create(configurationfile))
                {
                    try
                    {
                        while (configurationFileReader.Read())
                        {
                            if (configurationFileReader.IsStartElement())
                            {
                                switch (configurationFileReader.Name)
                                {
                                    case "osmbaseurl":
                                        configurationFileReader.Read();
                                        m_osmbaseurl = configurationFileReader.Value.Trim();
                                        break;
                                    case "osmdomainsfilepath":
                                        configurationFileReader.Read();
                                        m_osmDomainsFilePath = configurationFileReader.Value.Trim();
                                        break;
                                    case "osmfeaturepropertiesfilepath":
                                        configurationFileReader.Read();
                                        m_osmFeaturePropertiesFilePath = configurationFileReader.Value.Trim();
                                        break;
                                    default:
                                        break;
                                }
                            }
                        }
                    }
                    catch
                    { }
                }
            }

            if (String.IsNullOrEmpty(m_osmbaseurl))
            {
                // let's start with the very first default settings
                m_osmbaseurl = "http://www.openstreetmap.org";
            }

            string path = Assembly.GetExecutingAssembly().Location;
            FileInfo executingAssembly = new FileInfo(path);

            if (String.IsNullOrEmpty(m_osmDomainsFilePath))
            {
                // initialize with the default configuration files
                if (File.Exists(executingAssembly.Directory.FullName + System.IO.Path.DirectorySeparatorChar + "osm_domains.xml"))
                {
                    m_osmDomainsFilePath = executingAssembly.Directory.FullName + System.IO.Path.DirectorySeparatorChar + "osm_domains.xml";
                }
            }

            if (String.IsNullOrEmpty(m_osmFeaturePropertiesFilePath))
            {
                if (File.Exists(executingAssembly.Directory.FullName + System.IO.Path.DirectorySeparatorChar + "OSMFeaturesProperties.xml"))
                {
                    m_osmFeaturePropertiesFilePath = executingAssembly.Directory.FullName + System.IO.Path.DirectorySeparatorChar + "OSMFeaturesProperties.xml";
                }
            }
        }


        public void Init(IEditor Editor)
        {
            if (m_inspector == null)
            {
                //this is the default inspector shipped with the editor
                m_inspector = new FeatureInspector();

                SetParent(dataGridView1.Handle.ToInt32(), this.Handle.ToInt32());
            }

            m_editor = Editor;


            if (m_editor != null)
            {

                ReadOSMEditorSettings();

                try
                {
                    // populate the internal dictionary and rule set for the OSM features
                    loadOSMEditFeatures(m_osmFeaturePropertiesFilePath);

                    // populate the internal list of available OSM feature domains
                    loadOSMDomains(m_osmDomainsFilePath);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                }
            }

            m_isInitialized = true;
        }

        public bool IsInitialized
        {
            get
            {
                return m_isInitialized;
            }
        }

        public void ClearGrid()
        {
            if (dataGridView1 != null)
            {
                this.dataGridView1.Rows.Clear();
            }
        }

        public bool isOSMAttributeChange
        {
            get
            {
                return m_isOSMAttributeChange;
            }
            set
            {
                m_isOSMAttributeChange = value;
            }
        }

        private void dataGridView1_CellBeginEdit(object sender, DataGridViewCellCancelEventArgs e)
        {
            // when we are entering the edit mode in the first column declare the cell as dirty
            // and it is of type combo box then mark it
            if (e.ColumnIndex == 0)
            {
                if (dataGridView1.CurrentCell is DataGridViewComboBoxCell)
                {
                    dataGridView1.NotifyCurrentCellDirty(true);
                }
            }
        }

        //#region IObjectInspector Members

        //public void Clear()
        //{
        //    throw new NotImplementedException();
        //}

        //public void Copy(IRow srcRow)
        //{
        //    throw new NotImplementedException();
        //}

        //public int HWND
        //{
        //    get { throw new NotImplementedException(); }
        //}

        //public void Inspect(IEnumRow objects, IEditor Editor)
        //{
        //    try
        //    {
        //        if (m_inspector == null)
        //        {
        //            m_inspector = new FeatureInspectorClass();
        //        }

        //        ShowWindow(m_inspector.HWND, SW_SHOW);
        //        m_inspector.Inspect(objects, Editor);

        //        if (Editor == null)
        //        {
        //            return;
        //        }

        //        if (objects == null)
        //        {
        //            return;
        //        }

        //        if (IsInitialized == false)
        //            Init(Editor);

        //        m_editor = Editor;
        //        m_enumRow = objects;

        //        IEnumFeature enumFeatures = Editor.EditSelection;
        //        enumFeatures.Reset();

        //        int featureCount = 0;

        //        while (enumFeatures.Next() != null)
        //        {
        //            featureCount = featureCount + 1;
        //        }

        //        IEnumRow enumRow = objects;
        //        enumRow.Reset();
        //        IRow row = enumRow.Next();

        //        IFeature inspFeature = (IFeature)row;

        //        //user selected the layer name instead of a feature.
        //        if (objects.Count > 1)
        //        {
        //            prepareGrid4Features(objects);
        //        }
        //        else
        //        {
        //            prepareGrid4Feature(inspFeature);
        //        }

        //        currentlyEditedRows = enumRow;
        //    }
        //    catch (Exception ex)
        //    {
        //        ClearGrid();

        //        System.Diagnostics.Debug.WriteLine(ex.Message);
        //    }


        //}

        //#endregion

        #region

        void IObjectInspector.Clear()
        {
            if (m_inspector == null)
            {
                m_inspector = new FeatureInspectorClass();
            }

            if (m_inspector != null)
            {
                ClearGrid();
                m_inspector.Clear();
            }
        }

        void IObjectInspector.Copy(IRow srcRow)
        {
            if (m_inspector == null)
            {
                m_inspector = new FeatureInspectorClass();
            }

            if (m_inspector != null)
            {
                m_inspector.Copy(srcRow);
            }
        }

        int IObjectInspector.HWND
        {
            get
            {
                return this.Handle.ToInt32();
            }
        }

        void IObjectInspector.Inspect(IEnumRow objects, IEditor Editor)
        {
            try
            {
                if (m_inspector == null)
                {
                    m_inspector = new FeatureInspectorClass();
                }

                //SetParent(m_inspector.HWND, this.Handle.ToInt32());
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

                if (IsInitialized == false)
                    Init(Editor);

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
                    prepareGrid4Features(objects);
                }
                else
                {
                    prepareGrid4Feature(inspFeature);
                }

                currentlyEditedRows = enumRow;
            }
            catch (Exception ex)
            {
                ClearGrid();

                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }

        #endregion

        private void dataGridView1_CellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex > -1)
            {
                if (e.Value != null)
                {
                    // for localization include the translated language into a tooltip
                    if (m_editTags.ContainsKey(e.Value.ToString()))
                    {
                        if (!String.IsNullOrEmpty(m_editTags[e.Value.ToString()].displayname))
                        {
                            dataGridView1.Rows[e.RowIndex].Cells[e.ColumnIndex].ToolTipText = m_editTags[e.Value.ToString()].displayname;
                        }
                        else
                        {
                            dataGridView1.Rows[e.RowIndex].Cells[e.ColumnIndex].ToolTipText = e.Value.ToString();
                        }
                    }
                }
            }
        }
    }
}