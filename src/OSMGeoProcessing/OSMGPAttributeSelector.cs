// (c) Copyright Esri, 2010 - 2016
// This source is subject to the Apache 2.0 License.
// Please see http://www.apache.org/licenses/LICENSE-2.0.html for details.
// All other rights reserved.


using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Resources;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geoprocessing;
using ESRI.ArcGIS.esriSystem;

using System.Xml;
using System.Xml.Serialization;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.DataSourcesGDB;
using System.IO;
using System.Reflection;
using System.Linq;
using ESRI.ArcGIS.OSM.OSMClassExtension;
using ESRI.ArcGIS.Display;
using System.Globalization;

namespace ESRI.ArcGIS.OSM.GeoProcessing
{
    [Guid("ec0d7b3e-d73d-4d60-86a3-66c40d1bd39a")]
    [ClassInterface(ClassInterfaceType.None)]
    [ProgId("OSMEditor.OSMGPAttributeSelector")]
    public class OSMGPAttributeSelector : ESRI.ArcGIS.Geoprocessing.IGPFunction2
    {

        string m_DisplayName = "OSM Attribute Selector";
        int in_osmFeatureClass, in_attributeSelector, out_osmFeatureClass;
        ResourceManager resourceManager = null;
        OSMGPFactory osmGPFactory = null;
        OSMUtility _osmUtility = null;


        public OSMGPAttributeSelector()
        {
            resourceManager = new ResourceManager("ESRI.ArcGIS.OSM.GeoProcessing.OSMGPToolsStrings", this.GetType().Assembly);
            osmGPFactory = new OSMGPFactory();
            _osmUtility = new OSMUtility();
        }

        #region "IGPFunction2 Implementations"
        public ESRI.ArcGIS.esriSystem.UID DialogCLSID
        {
            get
            {
                return null;
            }
        }

        public string DisplayName
        {
            get
            {
                if (String.IsNullOrEmpty(m_DisplayName))
                {
                    m_DisplayName = osmGPFactory.GetFunctionName(OSMGPFactory.m_AttributeSelectorName).DisplayName;
                }

                return m_DisplayName;
            }
        }

        public void Execute(ESRI.ArcGIS.esriSystem.IArray paramvalues, ESRI.ArcGIS.esriSystem.ITrackCancel TrackCancel, ESRI.ArcGIS.Geoprocessing.IGPEnvironmentManager envMgr, ESRI.ArcGIS.Geodatabase.IGPMessages message)
        {
            try
            {
                IGPUtilities3 execute_Utilities = new GPUtilitiesClass();

                if (TrackCancel == null)
                {
                    TrackCancel = new CancelTrackerClass();
                }

                IGPParameter inputOSMParameter = paramvalues.get_Element(in_osmFeatureClass) as IGPParameter;
                IGPValue inputOSMGPValue = execute_Utilities.UnpackGPValue(inputOSMParameter);

                IGPParameter tagCollectionParameter = paramvalues.get_Element(in_attributeSelector) as IGPParameter;
                IGPMultiValue tagCollectionGPValue = execute_Utilities.UnpackGPValue(tagCollectionParameter) as IGPMultiValue;

                if (tagCollectionGPValue == null)
                {
                    message.AddError(120048, string.Format(resourceManager.GetString("GPTools_NullPointerParameterType"), tagCollectionParameter.Name));
                    return;
                }

                bool useUpdateCursor = false;

                IFeatureClass osmFeatureClass = null;
                ITable osmInputTable = null;
                IQueryFilter osmQueryFilter = new QueryFilterClass();

                try
                {
                    execute_Utilities.DecodeFeatureLayer(inputOSMGPValue, out osmFeatureClass, out osmQueryFilter);
                    if (osmFeatureClass != null)
                    {
                        if (osmFeatureClass.Extension is IOSMClassExtension)
                        {
                            useUpdateCursor = false;
                        }
                        else
                        {
                            useUpdateCursor = true;
                        }
                    }

                    osmInputTable = osmFeatureClass as ITable;
                }
                catch { }

                try
                {
                    if (osmInputTable == null)
                    {
                        execute_Utilities.DecodeTableView(inputOSMGPValue, out osmInputTable, out osmQueryFilter);
                    }
                }
                catch { }

                if (osmInputTable == null)
                {
                    string errorMessage = String.Format(resourceManager.GetString("GPTools_OSMGPAttributeSelecto_unableopentable"), inputOSMGPValue.GetAsText());
                    message.AddError(120053, errorMessage);
                    return;
                }

                // find the field that holds tag binary/xml field
                int osmTagCollectionFieldIndex = osmInputTable.FindField("osmTags");


                // if the Field doesn't exist - wasn't found (index = -1) get out
                if (osmTagCollectionFieldIndex == -1)
                {
                    message.AddError(120005, resourceManager.GetString("GPTools_OSMGPAttributeSelector_notagfieldfound"));
                    return;
                }

                // check if the tag collection includes the keyword "ALL", if does then we'll need to extract all tags
                string whatTagsToExtract = String.Empty;

                for (int valueIndex = 0; valueIndex < tagCollectionGPValue.Count; valueIndex++)
                {
                    if (tagCollectionGPValue.get_Value(valueIndex).GetAsText().Equals("ALL"))
                    {
                        whatTagsToExtract = "ALL";
                        break;
                    }
                    else if (tagCollectionGPValue.get_Value(valueIndex).GetAsText().Equals("EXISTING_TAG_FIELDS"))
                    {
                        whatTagsToExtract = "EXISTING";
                        break;
                    }
                    else
                    {
                        whatTagsToExtract = "SPECIFIC";
                    }
                }

                // get an overall feature count as that determines the progress indicator
                int featureCount = osmInputTable.RowCount(osmQueryFilter);

                // set up the progress indicator
                IStepProgressor stepProgressor = TrackCancel as IStepProgressor;

                if (stepProgressor != null)
                {
                    stepProgressor.MinRange = 0;
                    stepProgressor.MaxRange = featureCount;
                    stepProgressor.Position = 0;
                    stepProgressor.Message = resourceManager.GetString("GPTools_OSMGPAttributeSelector_progressMessage");
                    stepProgressor.StepValue = 1;
                    stepProgressor.Show();
                }

                // let's get all the indices of the desired fields
                // if the field already exists get the index and if it doesn't exist create it                    
                Dictionary<string, int> tagsAttributesIndices = new Dictionary<string, int>();
                Dictionary<int, int> attributeFieldLength = new Dictionary<int, int>();

                IFeatureWorkspaceManage featureWorkspaceManage = ((IDataset)osmInputTable).Workspace as IFeatureWorkspaceManage;

                String illegalCharacters = String.Empty;

                ISQLSyntax sqlSyntax = ((IDataset)osmInputTable).Workspace as ISQLSyntax;
                if (sqlSyntax != null)
                {
                    illegalCharacters = sqlSyntax.GetInvalidCharacters();
                }

                IFieldsEdit fieldsEdit = osmInputTable.Fields as IFieldsEdit;

                List<String> tagstoExtract = new List<string>();
                for (int valueIndex = 0; valueIndex < tagCollectionGPValue.Count; valueIndex++)
                {
                    tagstoExtract.Add(tagCollectionGPValue.get_Value(valueIndex).GetAsText());
                }


                using (SchemaLockManager lockMgr = new SchemaLockManager(osmInputTable))
                {
                    try
                    {
                        string tagKey = String.Empty;
                        ESRI.ArcGIS.Geoprocessing.IGeoProcessor2 gp = new ESRI.ArcGIS.Geoprocessing.GeoProcessorClass();

                        switch (whatTagsToExtract)
                        {
                            case "ALL":
                                List<string> listofAllTags = extractAllTags(osmInputTable, osmQueryFilter, osmTagCollectionFieldIndex);

                                foreach (string nameOfTag in listofAllTags)
                                {
                                    if (TrackCancel.Continue() == false)
                                    {
                                        return;
                                    }

                                    try
                                    {
                                        // Check if the input field already exists.
                                        tagKey = OSMToolHelper.convert2AttributeFieldName(nameOfTag, illegalCharacters);

                                        int fieldIndex = osmInputTable.FindField(tagKey);

                                        if (fieldIndex < 0)
                                        {
                                            // generate a new attribute field
                                            IFieldEdit fieldEdit = new FieldClass();
                                            fieldEdit.Name_2 = tagKey;
                                            fieldEdit.AliasName_2 = nameOfTag + resourceManager.GetString("GPTools_OSMGPAttributeSelector_aliasaddition");
                                            fieldEdit.Type_2 = esriFieldType.esriFieldTypeString;
                                            fieldEdit.Length_2 = 100;

                                            osmInputTable.AddField(fieldEdit);

                                            message.AddMessage(string.Format(resourceManager.GetString("GPTools_OSMGPAttributeSelector_addField"), tagKey, nameOfTag));

                                            // re-generate the attribute index
                                            fieldIndex = osmInputTable.FindField(tagKey);
                                        }

                                        if (fieldIndex > 0)
                                        {
                                            tagsAttributesIndices.Add(nameOfTag, fieldIndex);
                                            attributeFieldLength.Add(fieldIndex, osmInputTable.Fields.get_Field(fieldIndex).Length);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        // the key is already there, this might result because from multiple upper and lower-case combinations of the same key
                                        message.AddWarning(ex.Message + " (" + OSMToolHelper.convert2OSMKey(tagKey, illegalCharacters) + ")");
                                    }
                                }
                                break;
                            case "EXISTING":
                                for (int fieldIndex = 0; fieldIndex < osmInputTable.Fields.FieldCount; fieldIndex++)
                                {
                                    IField currentField = osmInputTable.Fields.get_Field(fieldIndex);
                                    if (currentField.Required == false && currentField.Name.StartsWith("osm_"))
                                    {
                                        tagsAttributesIndices.Add(OSMToolHelper.convert2OSMKey(currentField.Name, illegalCharacters), fieldIndex);
                                        attributeFieldLength.Add(fieldIndex, osmInputTable.Fields.get_Field(fieldIndex).Length);
                                    }
                                }
                                break;
                            default:
                                // if we have explicitly defined tags to extract then go through the list of values now
                                foreach (string nameOfTag in tagstoExtract)
                                {
                                    if (TrackCancel.Continue() == false)
                                    {
                                        return;
                                    }

                                    try
                                    {
                                        // Check if the input field already exists.
                                        tagKey = OSMToolHelper.convert2AttributeFieldName(nameOfTag, illegalCharacters);

                                        int fieldIndex = osmInputTable.FindField(tagKey);

                                        if (fieldIndex < 0)
                                        {
                                            // generate a new attribute field
                                            IFieldEdit fieldEdit = new FieldClass();
                                            fieldEdit.Name_2 = tagKey;
                                            fieldEdit.AliasName_2 = nameOfTag + resourceManager.GetString("GPTools_OSMGPAttributeSelector_aliasaddition");
                                            fieldEdit.Type_2 = esriFieldType.esriFieldTypeString;
                                            fieldEdit.Length_2 = 100;

                                            osmInputTable.AddField(fieldEdit);

                                            message.AddMessage(string.Format(resourceManager.GetString("GPTools_OSMGPAttributeSelector_addField"), tagKey, nameOfTag));

                                            // re-generate the attribute index
                                            fieldIndex = osmInputTable.FindField(tagKey);
                                        }

                                        if (fieldIndex > 0)
                                        {
                                            tagsAttributesIndices.Add(nameOfTag, fieldIndex);
                                            attributeFieldLength.Add(fieldIndex, osmInputTable.Fields.get_Field(fieldIndex).Length);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        // the key is already there, this might result because from multiple upper and lower-case combinations of the same key
                                        message.AddWarning(ex.Message + " (" + OSMToolHelper.convert2OSMKey(tagKey, illegalCharacters) + ")");
                                    }
                                }
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        message.AddWarning(ex.Message);
                    }
                }

                try
                {
                    execute_Utilities.DecodeFeatureLayer(inputOSMGPValue, out osmFeatureClass, out osmQueryFilter);
                    if (osmFeatureClass != null)
                    {
                        if (osmFeatureClass.Extension is IOSMClassExtension)
                        {
                            useUpdateCursor = false;
                        }
                        else
                        {
                            useUpdateCursor = true;
                        }
                    }

                    osmInputTable = osmFeatureClass as ITable;
                }
                catch { }

                try
                {
                    if (osmInputTable == null)
                    {
                        execute_Utilities.DecodeTableView(inputOSMGPValue, out osmInputTable, out osmQueryFilter);
                    }
                }
                catch { }

                if (osmInputTable == null)
                {
                    string errorMessage = String.Format(resourceManager.GetString("GPTools_OSMGPAttributeSelecto_unableopentable"), inputOSMGPValue.GetAsText());
                    message.AddError(120053, errorMessage);
                    return;
                }

                if (osmQueryFilter == null)
                    osmQueryFilter = new QueryFilterClass();

                if (useUpdateCursor)
                {
                    // convert the selected fields into a comma separated string to limit the returned row buffer (fields)
                    string FieldsString = String.Join(",", tagsAttributesIndices.Keys.Select(s => OSMToolHelper.convert2AttributeFieldName(s, illegalCharacters)).ToArray());
                    if (osmInputTable is IFeatureClass)
                    {
                        osmQueryFilter.SubFields = FieldsString + ",osmTags," + ((IFeatureClass)osmInputTable).ShapeFieldName;
                    }
                    else
                    {
                        osmQueryFilter.SubFields = FieldsString + ",osmTags";
                    }
                }

                using (ComReleaser comReleaser = new ComReleaser())
                {
                    using (SchemaLockManager lockMgr = new SchemaLockManager(osmInputTable))
                    {
                        // get an update cursor for all the features to process
                        ICursor rowCursor = null;
                        if (useUpdateCursor)
                        {
                            rowCursor = osmInputTable.Update(osmQueryFilter, false);
                        }
                        else
                        {
                            rowCursor = osmInputTable.Search(osmQueryFilter, false);
                        }

                        comReleaser.ManageLifetime(rowCursor);

                        IRow osmRow = null;


                        Dictionary<string, string> tagKeys = new Dictionary<string, string>();
                        int progessIndex = 0;
#if DEBUG
                        message.AddMessage("useUpdateCursor: " + useUpdateCursor.ToString());
#endif

                        // as long as there are features....
                        while ((osmRow = rowCursor.NextRow()) != null)
                        {
                            // retrieve the tags of the current feature 
                            tag[] storedTags = _osmUtility.retrieveOSMTags(osmRow, osmTagCollectionFieldIndex, ((IDataset)osmInputTable).Workspace);

                            bool rowChanged = false;
                            if (storedTags != null)
                            {
                                foreach (tag tagItem in storedTags)
                                {
                                    // Check for matching values so we only change a minimum number of rows
                                    if (tagsAttributesIndices.ContainsKey(tagItem.k))
                                    {
                                        int fieldIndex = tagsAttributesIndices[tagItem.k];

                                        //...then stored the value in the attribute field
                                        // ensure that the content of the tag actually does fit into the field length...otherwise do truncate it
                                        string tagValue = tagItem.v;

                                        int fieldLength = attributeFieldLength[fieldIndex];

                                        if (tagValue.Length > fieldLength)
                                            tagValue = tagValue.Substring(0, fieldLength);

                                        osmRow.set_Value(fieldIndex, tagValue);
                                        rowChanged = true;
                                    }
                                    else
                                    {
#if DEBUG
                                        //message.AddWarning(tagItem.k);
#endif
                                    }
                                }
                            }

                            storedTags = null;

                            try
                            {
                                if (rowChanged)
                                {
                                    if (useUpdateCursor)
                                    {
                                        rowCursor.UpdateRow(osmRow);
                                    }
                                    else
                                    {
                                        // update the feature through the cursor
                                        osmRow.Store();
                                    }
                                }
                                progessIndex++;
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine(ex.Message);
                                message.AddWarning(ex.Message);
                            }

                            if (osmRow != null)
                            {
                                Marshal.ReleaseComObject(osmRow);
                            }

                            if (stepProgressor != null)
                            {
                                // update the progress UI
                                stepProgressor.Position = progessIndex;
                            }

                            // check for user cancellation (every 100 rows)
                            if ((progessIndex % 100 == 0) && (TrackCancel.Continue() == false))
                            {
                                return;
                            }
                        }

                        if (stepProgressor != null)
                        {
                            stepProgressor.Hide();
                        }
                    }
                }

                execute_Utilities.ReleaseInternals();
                Marshal.ReleaseComObject(execute_Utilities);
            }
            catch (Exception ex)
            {
                message.AddError(120054, ex.Message);
            }
        }



        public ESRI.ArcGIS.esriSystem.IName FullName
        {
            get
            {
                IName fullName = null;

                if (osmGPFactory != null)
                {
                    fullName = osmGPFactory.GetFunctionName(OSMGPFactory.m_AttributeSelectorName) as IName;
                }

                return fullName;
            }
        }

        public object GetRenderer(ESRI.ArcGIS.Geoprocessing.IGPParameter pParam)
        {
            return null;
        }

        public int HelpContext
        {
            get
            {
                return 0;
            }
        }

        public string HelpFile
        {
            get
            {
                return default(string);
            }
        }

        public bool IsLicensed()
        {
            return true;
        }

        public string MetadataFile
        {
            get
            {
                string metadafile = "osmgpattributeselector.xml";

                try
                {
                    string[] languageid = System.Threading.Thread.CurrentThread.CurrentUICulture.Name.Split("-".ToCharArray());

                    string ArcGISInstallationLocation = OSMGPFactory.GetArcGIS10InstallLocation();
                    string localizedMetaDataFileShort = ArcGISInstallationLocation + System.IO.Path.DirectorySeparatorChar.ToString() + "help" + System.IO.Path.DirectorySeparatorChar.ToString() + "gp" + System.IO.Path.DirectorySeparatorChar.ToString() + "osmgpattributeselector_" + languageid[0] + ".xml";
                    string localizedMetaDataFileLong = ArcGISInstallationLocation + System.IO.Path.DirectorySeparatorChar.ToString() + "help" + System.IO.Path.DirectorySeparatorChar.ToString() + "gp" + System.IO.Path.DirectorySeparatorChar.ToString() + "osmgpattributeselector_" + System.Threading.Thread.CurrentThread.CurrentUICulture.Name + ".xml";

                    if (System.IO.File.Exists(localizedMetaDataFileShort))
                    {
                        metadafile = localizedMetaDataFileShort;
                    }
                    else if (System.IO.File.Exists(localizedMetaDataFileLong))
                    {
                        metadafile = localizedMetaDataFileLong;
                    }
                }
                catch { }

                return metadafile;
            }
        }

        public string Name
        {
            get
            {
                return OSMGPFactory.m_AttributeSelectorName;
            }
        }

        public ESRI.ArcGIS.esriSystem.IArray ParameterInfo
        {
            get
            {
                IArray parameterArray = new ArrayClass();

                // input osm feature class
                IGPParameterEdit3 inputFeatureClass = new GPParameterClass() as IGPParameterEdit3;

                IGPCompositeDataType inputCompositeDataType = new GPCompositeDataTypeClass();
                inputCompositeDataType.AddDataType(new GPFeatureLayerTypeClass());
                inputCompositeDataType.AddDataType(new GPTableViewTypeClass());
                inputFeatureClass.DataType = (IGPDataType)inputCompositeDataType;
                //inputFeatureClass.DataType = new GPFeatureLayerTypeClass();
                // Default Value object is GPFeatureLayer
                inputFeatureClass.Value = new GPFeatureLayerClass();
                inputFeatureClass.Direction = esriGPParameterDirection.esriGPParameterDirectionInput;
                inputFeatureClass.DisplayName = resourceManager.GetString("GPTools_OSMGPAttributeSelector_inputFeatureClass");
                inputFeatureClass.Name = "in_osmfeatureclass";
                inputFeatureClass.ParameterType = esriGPParameterType.esriGPParameterTypeRequired;
                in_osmFeatureClass = 0;
                parameterArray.Add(inputFeatureClass);

                //// attribute multi select parameter
                IGPParameterEdit3 attributeSelect = new GPParameterClass() as IGPParameterEdit3;

                IGPDataType tagKeyDataType = new GPStringTypeClass();
                IGPMultiValue tagKeysMultiValue = new GPMultiValueClass();
                tagKeysMultiValue.MemberDataType = tagKeyDataType;

                IGPCodedValueDomain tagKeyDomains = new GPCodedValueDomainClass();
                tagKeyDomains.AddStringCode("ALL", "ALL");
                tagKeyDomains.AddStringCode("EXISTING_TAG_FIELDS", "EXISTING_TAG_FIELDS");

                IGPMultiValueType tagKeyDataType2 = new GPMultiValueTypeClass();
                tagKeyDataType2.MemberDataType = tagKeyDataType;

                attributeSelect.Name = "in_attributeselect";
                attributeSelect.DisplayName = resourceManager.GetString("GPTools_OSMGPAttributeSelector_inputAttributes");
                attributeSelect.ParameterType = esriGPParameterType.esriGPParameterTypeRequired;
                attributeSelect.Direction = esriGPParameterDirection.esriGPParameterDirectionInput;
                attributeSelect.Domain = (IGPDomain)tagKeyDomains;
                attributeSelect.DataType = (IGPDataType)tagKeyDataType2;
                attributeSelect.Value = (IGPValue)tagKeysMultiValue;

                in_attributeSelector = 1;
                parameterArray.Add(attributeSelect);


                // output is the derived feature class
                IGPParameterEdit3 outputFeatureClass = new GPParameterClass() as IGPParameterEdit3;
                outputFeatureClass.DataType = new GPFeatureLayerTypeClass();
                outputFeatureClass.Value = new DEFeatureClassClass();
                outputFeatureClass.Direction = esriGPParameterDirection.esriGPParameterDirectionOutput;
                outputFeatureClass.DisplayName = resourceManager.GetString("GPTools_OSMGPAttributeSelector_outputFeatureClass");
                outputFeatureClass.Name = "out_osmfeatureclass";
                outputFeatureClass.ParameterType = esriGPParameterType.esriGPParameterTypeDerived;
                out_osmFeatureClass = 2;

                // Create a schema object. Schema means the structure or design of the feature class 
                // (field  information, geometry information, extent).
                IGPFeatureSchema outputSchema = new GPFeatureSchemaClass();
                IGPSchema schema = (IGPSchema)outputSchema;

                // Clone the schema from the dependency.
                //This means update the output with the same schema by copying the definition of an input parameter.
                schema.CloneDependency = true;

                // Set the schema on the output, because this tool adds an additional field.
                outputFeatureClass.Schema = outputSchema as IGPSchema;
                outputFeatureClass.AddDependency("in_osmfeatureclass");


                parameterArray.Add(outputFeatureClass);

                return parameterArray;
            }
        }

        public void UpdateMessages(ESRI.ArcGIS.esriSystem.IArray paramvalues, ESRI.ArcGIS.Geoprocessing.IGPEnvironmentManager pEnvMgr, ESRI.ArcGIS.Geodatabase.IGPMessages Messages)
        {
        }

        public void UpdateParameters(ESRI.ArcGIS.esriSystem.IArray paramvalues, ESRI.ArcGIS.Geoprocessing.IGPEnvironmentManager pEnvMgr)
        {
            try
            {
                IGPUtilities3 gpUtilities3 = new GPUtilitiesClass();

                IGPParameter3 inputOSMParameter = paramvalues.get_Element(in_osmFeatureClass) as IGPParameter3;
                IGPValue inputOSMGPValue = null;

                // check if the input is of type variable
                if (inputOSMParameter.Value is IGPVariable)
                {
                    // also check if the variable contains a derived value
                    // -- which in this context means a featureclass that will be produced during execution but 
                    // doesn't exist yet
                    IGPVariable inputOSMVariable = inputOSMParameter.Value as IGPVariable;
                    if (!inputOSMVariable.Derived)
                        inputOSMGPValue = gpUtilities3.UnpackGPValue(paramvalues.get_Element(in_osmFeatureClass));
                }
                else
                {
                    inputOSMGPValue = gpUtilities3.UnpackGPValue(paramvalues.get_Element(in_osmFeatureClass));
                }

                IFeatureClass osmFeatureClass = null;
                ITable osmInputTable = null;
                IQueryFilter osmQueryFilter = null;
                String illegalCharacters = String.Empty;

                if (inputOSMGPValue != null)
                {
                    try
                    {
                        gpUtilities3.DecodeFeatureLayer(inputOSMGPValue, out osmFeatureClass, out osmQueryFilter);
                        osmInputTable = osmFeatureClass as ITable;
                    }
                    catch { }

                    try
                    {
                        if (osmInputTable == null)
                        {
                            gpUtilities3.DecodeTableView(inputOSMGPValue, out osmInputTable, out osmQueryFilter);
                        }
                    }
                    catch { }

                    if (osmInputTable == null)
                    {
                        return;
                    }

                    ISQLSyntax sqlSyntax = ((IDataset)osmInputTable).Workspace as ISQLSyntax;
                    if (sqlSyntax != null)
                    {
                        illegalCharacters = sqlSyntax.GetInvalidCharacters();
                    }

                    // find the field that holds tag binary/xml field
                    int osmTagCollectionFieldIndex = osmInputTable.FindField("osmTags");


                    // if the Field doesn't exist - wasn't found (index = -1) get out
                    if (osmTagCollectionFieldIndex == -1)
                    {
                        return;
                    }
                }

                if (((IGPParameter)paramvalues.get_Element(in_attributeSelector)).Altered)
                {
                    IGPParameter tagCollectionParameter = paramvalues.get_Element(in_attributeSelector) as IGPParameter;
                    IGPMultiValue tagCollectionGPValue = gpUtilities3.UnpackGPValue(tagCollectionParameter) as IGPMultiValue;

                    IGPCodedValueDomain codedTagDomain = tagCollectionParameter.Domain as IGPCodedValueDomain;

                    for (int attributeValueIndex = 0; attributeValueIndex < tagCollectionGPValue.Count; attributeValueIndex++)
                    {
                        string valueString = tagCollectionGPValue.get_Value(attributeValueIndex).GetAsText();
                        IGPValue testFieldValue = codedTagDomain.FindValue(valueString);

                        if (testFieldValue == null)
                        {
                            codedTagDomain.AddStringCode(valueString, valueString);
                        }
                    }

                    // Get the derived output feature class schema and empty the additional fields. This ensures 
                    // that you don't get dublicate entries. 
                    // Derived output is the third parameter, so use index 2 for get_Element.
                    IGPParameter3 derivedFeatures = (IGPParameter3)paramvalues.get_Element(out_osmFeatureClass);
                    IGPFeatureSchema schema = (IGPFeatureSchema)derivedFeatures.Schema;
                    schema.AdditionalFields = null;

                    IFieldsEdit fieldsEdit = new FieldsClass();

                    for (int valueIndex = 0; valueIndex < tagCollectionGPValue.Count; valueIndex++)
                    {
                        string tagString = tagCollectionGPValue.get_Value(valueIndex).GetAsText();

                        if (tagString != "ALL")
                        {
                            // Check if the input field already exists.
                            string cleanedTagKey = OSMToolHelper.convert2AttributeFieldName(tagString, illegalCharacters);
                            IField tagField = gpUtilities3.FindField(inputOSMGPValue, cleanedTagKey);
                            if (tagField == null)
                            {
                                IFieldEdit fieldEdit = new FieldClass();
                                fieldEdit.Name_2 = cleanedTagKey;
                                fieldEdit.AliasName_2 = tagCollectionGPValue.get_Value(valueIndex).GetAsText();
                                fieldEdit.Type_2 = esriFieldType.esriFieldTypeString;
                                fieldEdit.Length_2 = 100;
                                fieldsEdit.AddField(fieldEdit);
                            }
                        }
                    }

                    // Add the additional field to the derived output.
                    IFields fields = fieldsEdit as IFields;
                    schema.AdditionalFields = fields;
                }

                //if (inputOSMGPValue.IsEmpty() == false)
                //{
                //    if (((IGPParameter)paramvalues.get_Element(in_osmFeatureClass)).HasBeenValidated == false)
                //    {
                //        IGPParameter tagCollectionGPParameter = paramvalues.get_Element(in_attributeSelector) as IGPParameter;
                //        IGPValue tagCollectionGPValue = gpUtilities3.UnpackGPValue(tagCollectionGPParameter);
                        
                //        IGPCodedValueDomain osmTagKeyCodedValues = new GPCodedValueDomainClass();
                //        if (((IGPMultiValue)tagCollectionGPValue).Count == 0)
                //        {
                //            if (osmTagKeyCodedValues == null)
                //                extractAllTags(ref osmTagKeyCodedValues, osmInputTable, osmQueryFilter, osmTagCollectionFieldIndex, true);

                //            if (osmTagKeyCodedValues != null)
                //            {
                //                tagsParameter = tagCollectionGPParameter as IGPParameterEdit;
                //                tagsParameter.Domain = (IGPDomain)osmTagKeyCodedValues;
                //            }
                //        }
                //        else
                //        {
                //            // let's take the given values and make then part of the domain -- if they are not already
                //            // if we don't do this step then we won't pass the internal validation
                //            IGPCodedValueDomain gpTagDomain = tagCollectionGPParameter.Domain as IGPCodedValueDomain;

                //            if (gpTagDomain != null)
                //            {
                //                if (gpTagDomain.CodeCount == 0)
                //                {
                //                    // let's add the value existing in the mentioned multi value to the domain
                //                    for (int i = 0; i < ((IGPMultiValue)tagCollectionGPValue).Count; i++)
                //                    {
                //                        string tagStringValue = ((IGPMultiValue)tagCollectionGPValue).get_Value(i).GetAsText();
                //                        gpTagDomain.AddStringCode(tagStringValue, tagStringValue);
                //                    }

                //                    ((IGPParameterEdit)tagCollectionGPParameter).Domain = gpTagDomain as IGPDomain;
                //                }
                //            }
                //        }

                //        // Get the derived output feature class schema and empty the additional fields. This ensures 
                //        // that you don't get dublicate entries. 
                //        // Derived output is the third parameter, so use index 2 for get_Element.
                //        IGPParameter3 derivedFeatures = (IGPParameter3)paramvalues.get_Element(out_osmFeatureClass);
                //        IGPFeatureSchema schema = (IGPFeatureSchema)derivedFeatures.Schema;
                //        schema.AdditionalFields = null;

                //        // Area field name is the second parameter, so use index 1 for get_Element.
                //        IGPMultiValue tagsGPMultiValue = gpUtilities3.UnpackGPValue(paramvalues.get_Element(in_attributeSelector)) as IGPMultiValue;

                //        IFieldsEdit fieldsEdit = new FieldsClass();
                //        bool extractALLTags = false;

                //        // check if the list contains the "ALL" keyword
                //        for (int valueIndex = 0; valueIndex < tagsGPMultiValue.Count; valueIndex++)
                //        {
                //            if (tagsGPMultiValue.get_Value(valueIndex).GetAsText().Equals("ALL"))
                //            {
                //                extractALLTags = true;
                //            }
                //        }

                //        if (extractALLTags)
                //        {
                //            if (osmTagKeyCodedValues == null)
                //            {
                //                extractAllTags(ref osmTagKeyCodedValues, osmInputTable, osmQueryFilter, osmTagCollectionFieldIndex, false);
                //            }

                //            if (osmTagKeyCodedValues != null)
                //            {
                //                for (int valueIndex = 0; valueIndex < osmTagKeyCodedValues.CodeCount; valueIndex++)
                //                {
                //                    // Check if the input field already exists.
                //                    string cleanedTagKey = convert2AttributeFieldName(osmTagKeyCodedValues.get_Value(valueIndex).GetAsText(), illegalCharacters);
                //                    IField tagField = gpUtilities3.FindField(inputOSMGPValue, cleanedTagKey);
                //                    if (tagField == null)
                //                    {
                //                        IFieldEdit fieldEdit = new FieldClass();
                //                        fieldEdit.Name_2 = cleanedTagKey;
                //                        fieldEdit.AliasName_2 = osmTagKeyCodedValues.get_Value(valueIndex).GetAsText();
                //                        fieldEdit.Type_2 = esriFieldType.esriFieldTypeString;
                //                        fieldEdit.Length_2 = 100;
                //                        fieldsEdit.AddField(fieldEdit);
                //                    }
                //                }
                //            }
                //        }
                //        else
                //        {
                //            for (int valueIndex = 0; valueIndex < tagsGPMultiValue.Count; valueIndex++)
                //            {
                //                // Check if the input field already exists.
                //                string cleanedTagKey = convert2AttributeFieldName(tagsGPMultiValue.get_Value(valueIndex).GetAsText(), illegalCharacters);
                //                IField tagField = gpUtilities3.FindField(inputOSMGPValue, cleanedTagKey);
                //                if (tagField == null)
                //                {
                //                    IFieldEdit fieldEdit = new FieldClass();
                //                    fieldEdit.Name_2 = cleanedTagKey;
                //                    fieldEdit.AliasName_2 = tagsGPMultiValue.get_Value(valueIndex).GetAsText();
                //                    fieldEdit.Type_2 = esriFieldType.esriFieldTypeString;
                //                    fieldEdit.Length_2 = 100;
                //                    fieldsEdit.AddField(fieldEdit);
                //                }
                //            }
                //        }

                //        // Add the additional field to the derived output.
                //        IFields fields = fieldsEdit as IFields;
                //        schema.AdditionalFields = fields;

                //    }
                //}
            }
            catch { }
        }

        private List<string> extractAllTags(ITable osmInputTable, IQueryFilter osmQueryFilter, int osmTagCollectionFieldIndex)
        {
            HashSet<string> listOfAllTags = new HashSet<string>();

            if (osmInputTable == null)
            {
                return listOfAllTags.ToList();
            }

            IQueryFilter newQueryFilter = new QueryFilterClass();
            if (osmQueryFilter != null)
                newQueryFilter.WhereClause = osmQueryFilter.WhereClause;
            newQueryFilter.SubFields = "osmTags";

            IWorkspace datasetWorkspace = ((IDataset)osmInputTable).Workspace;

            using (ComReleaser comReleaser = new ComReleaser())
            {
                if (osmQueryFilter == null)
                {
                    osmQueryFilter = new QueryFilterClass();
                }

                //osmQueryFilter.SubFields = osmInputTable.Fields.get_Field(osmTagCollectionFieldIndex).Name;

                ICursor osmCursor = osmInputTable.Search(newQueryFilter, false);
                comReleaser.ManageLifetime(osmCursor);

                IRow osmRow = osmCursor.NextRow();
                comReleaser.ManageLifetime(osmRow);

                while (osmRow != null)
                {
                    ESRI.ArcGIS.OSM.OSMClassExtension.tag[] storedTags = null;

                    try
                    {
                        storedTags = _osmUtility.retrieveOSMTags(osmRow, osmTagCollectionFieldIndex, null);
                    }
                    catch
                    { }

                    if (storedTags != null)
                    {
                        foreach (tag tagItem in storedTags)
                        {
                            listOfAllTags.Add(tagItem.k);
                        }
                    }

                    osmRow = osmCursor.NextRow();
                }
            }

            // sort the tag name alphabetically
            IEnumerable<string> sortedTags = listOfAllTags.OrderBy(nameOfTag => nameOfTag);

            return sortedTags.ToList();
        }

        public ESRI.ArcGIS.Geodatabase.IGPMessages Validate(ESRI.ArcGIS.esriSystem.IArray paramvalues, bool updateValues, ESRI.ArcGIS.Geoprocessing.IGPEnvironmentManager envMgr)
        {
            return default(ESRI.ArcGIS.Geodatabase.IGPMessages);
        }
        #endregion
    }
}
