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
using ESRI.ArcGIS.OSM.OSMClassExtension;
using ESRI.ArcGIS.Display;

namespace ESRI.ArcGIS.OSM.GeoProcessing
{
    [Guid("091c4384-8aa5-4d4d-8c30-fafb1ca81b86")]
    [ClassInterface(ClassInterfaceType.None)]
    [ProgId("OSMEditor.OSMGPCombineAttributes")]
    public class OSMGPCombineAttributes : ESRI.ArcGIS.Geoprocessing.IGPFunction2
    {
        string m_DisplayName = String.Empty;
        int in_osmFeatureClassNumber, in_attributeSelectorNumber, out_osmFeatureClassNumber;
        ResourceManager resourceManager = null;
        OSMGPFactory osmGPFactory = null;

        public OSMGPCombineAttributes()
        {
            resourceManager = new ResourceManager("ESRI.ArcGIS.OSM.GeoProcessing.OSMGPToolsStrings", this.GetType().Assembly);
            osmGPFactory = new OSMGPFactory();
        }

        #region "IGPFunction2 Implementations"
        public ESRI.ArcGIS.esriSystem.UID DialogCLSID
        {
            get
            {
                return default(ESRI.ArcGIS.esriSystem.UID);
            }
        }

        public string DisplayName
        {
            get
            {
                if (String.IsNullOrEmpty(m_DisplayName))
                {
                    m_DisplayName = osmGPFactory.GetFunctionName(OSMGPFactory.m_CombineAttributesName).DisplayName;
                }

                return m_DisplayName;
            }
        }

        public void Execute(ESRI.ArcGIS.esriSystem.IArray paramvalues, ESRI.ArcGIS.esriSystem.ITrackCancel TrackCancel, ESRI.ArcGIS.Geoprocessing.IGPEnvironmentManager envMgr, ESRI.ArcGIS.Geodatabase.IGPMessages message)
        {
            try
            {
                IGPUtilities3 execute_Utilities = new GPUtilitiesClass();
                OSMUtility osmUtility = new OSMUtility();

                if (TrackCancel == null)
                {
                    TrackCancel = new CancelTrackerClass();
                }

                IGPParameter inputOSMParameter = paramvalues.get_Element(in_osmFeatureClassNumber) as IGPParameter;
                IGPValue inputOSMGPValue = execute_Utilities.UnpackGPValue(inputOSMParameter);

                IGPParameter tagFieldsParameter = paramvalues.get_Element(in_attributeSelectorNumber) as IGPParameter;
                IGPMultiValue tagCollectionGPValue = execute_Utilities.UnpackGPValue(tagFieldsParameter) as IGPMultiValue;

                if (tagCollectionGPValue == null)
                {
                    message.AddError(120048, string.Format(resourceManager.GetString("GPTools_NullPointerParameterType"), tagFieldsParameter.Name));
                    return;
                }


                IFeatureClass osmFeatureClass = null;
                ITable osmInputTable = null;
                IQueryFilter osmQueryFilter = null;

                try
                {
                    execute_Utilities.DecodeFeatureLayer(inputOSMGPValue, out osmFeatureClass, out osmQueryFilter);
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

                // set up the progress indicator
                IStepProgressor stepProgressor = TrackCancel as IStepProgressor;

                if (stepProgressor != null)
                {
                    int featureCount = osmInputTable.RowCount(osmQueryFilter);

                    stepProgressor.MinRange = 0;
                    stepProgressor.MaxRange = featureCount;
                    stepProgressor.Position = 0;
                    stepProgressor.Message = resourceManager.GetString("GPTools_OSMGPCombineAttributes_progressMessage");
                    stepProgressor.StepValue = 1;
                    stepProgressor.Show();
                }

                String illegalCharacters = String.Empty;

                ISQLSyntax sqlSyntax = ((IDataset)osmInputTable).Workspace as ISQLSyntax;
                if (sqlSyntax != null)
                {
                    illegalCharacters = sqlSyntax.GetInvalidCharacters();
                }

                // establish the list of field indexes only once
                Dictionary<string, int> fieldIndexes = new Dictionary<string, int>();
                for (int selectedGPValueIndex = 0; selectedGPValueIndex < tagCollectionGPValue.Count; selectedGPValueIndex++)
                {
                    // find the field index
                    int fieldIndex = osmInputTable.FindField(tagCollectionGPValue.get_Value(selectedGPValueIndex).GetAsText());

                    if (fieldIndex != -1)
                    {
                        string tagKeyName = osmInputTable.Fields.get_Field(fieldIndex).Name;

                        tagKeyName = OSMToolHelper.convert2OSMKey(tagKeyName, illegalCharacters);

                        fieldIndexes.Add(tagKeyName, fieldIndex);
                    }
                }

                ICursor updateCursor = null;
                IRow osmRow = null;

                using (ComReleaser comReleaser = new ComReleaser())
                {
                    updateCursor = osmInputTable.Update(osmQueryFilter, false);
                    comReleaser.ManageLifetime(updateCursor);

                    osmRow = updateCursor.NextRow();
                    int progressIndex = 0;

                    while (osmRow != null)
                    {
                        // get the current tag collection from the row
                        ESRI.ArcGIS.OSM.OSMClassExtension.tag[] osmTags = osmUtility.retrieveOSMTags(osmRow, osmTagCollectionFieldIndex, ((IDataset)osmInputTable).Workspace);

                        Dictionary<string, string> tagsDictionary = new Dictionary<string, string>();
                        for (int tagIndex = 0; tagIndex < osmTags.Length; tagIndex++)
                        {
                            tagsDictionary.Add(osmTags[tagIndex].k, osmTags[tagIndex].v);
                        }

                        // look if the tag needs to be updated or added
                        bool tagsUpdated = false;
                        foreach (var fieldItem in fieldIndexes)
                        {
                            object fldValue = osmRow.get_Value(fieldItem.Value);
                            if (fldValue != System.DBNull.Value)
                            {
                                if (tagsDictionary.ContainsKey(fieldItem.Key))
                                {
                                    if (!tagsDictionary[fieldItem.Key].Equals(fldValue))
                                    {
                                        tagsDictionary[fieldItem.Key] = Convert.ToString(fldValue);
                                        tagsUpdated = true;
                                    }
                                }
                                else
                                {
                                    tagsDictionary.Add(fieldItem.Key, Convert.ToString(fldValue));
                                    tagsUpdated = true;
                                }
                            }
                        }

                        if (tagsUpdated)
                        {
                            List<ESRI.ArcGIS.OSM.OSMClassExtension.tag> updatedTags = new List<ESRI.ArcGIS.OSM.OSMClassExtension.tag>();

                            foreach (var tagItem in tagsDictionary)
                            {
                                ESRI.ArcGIS.OSM.OSMClassExtension.tag newTag = new ESRI.ArcGIS.OSM.OSMClassExtension.tag();
                                newTag.k = tagItem.Key;
                                newTag.v = tagItem.Value;
                                updatedTags.Add(newTag);
                            }

                            // insert the tags back into the collection field
                            if (updatedTags.Count != 0)
                            {
                                osmUtility.insertOSMTags(osmTagCollectionFieldIndex, osmRow, updatedTags.ToArray(), ((IDataset)osmInputTable).Workspace);

                                updateCursor.UpdateRow(osmRow);
                            }
                        }

                        progressIndex++;
                        if (stepProgressor != null)
                        {
                            stepProgressor.Position = progressIndex;
                        }

                        if (osmRow != null)
                            Marshal.ReleaseComObject(osmRow);

                        osmRow = updateCursor.NextRow();
                    }

                    if (stepProgressor != null)
                    {
                        stepProgressor.Hide();
                    }
                }
            }
            catch (Exception ex)
            {
                message.AddError(120007, ex.Message);
            }
        }

        public ESRI.ArcGIS.esriSystem.IName FullName
        {
            get
            {
                IName fullName = null;

                if (osmGPFactory != null)
                {
                    fullName = osmGPFactory.GetFunctionName(OSMGPFactory.m_CombineAttributesName) as IName;
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
                string metadafile = "osmgpcombineattributes.xml";

                try
                {
                    string[] languageid = System.Threading.Thread.CurrentThread.CurrentUICulture.Name.Split("-".ToCharArray());

                    string ArcGISInstallationLocation = OSMGPFactory.GetArcGIS10InstallLocation();
                    string localizedMetaDataFileShort = ArcGISInstallationLocation + System.IO.Path.DirectorySeparatorChar.ToString() + "help" + System.IO.Path.DirectorySeparatorChar.ToString() + "gp" + System.IO.Path.DirectorySeparatorChar.ToString() + "osmgpcombineattributes_" + languageid[0] + ".xml";
                    string localizedMetaDataFileLong = ArcGISInstallationLocation + System.IO.Path.DirectorySeparatorChar.ToString() + "help" + System.IO.Path.DirectorySeparatorChar.ToString() + "gp" + System.IO.Path.DirectorySeparatorChar.ToString() + "osmgpcombineattributes_" + System.Threading.Thread.CurrentThread.CurrentUICulture.Name + ".xml";

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
                return OSMGPFactory.m_CombineAttributesName;
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
                inputFeatureClass.Direction = esriGPParameterDirection.esriGPParameterDirectionInput;
                inputFeatureClass.DisplayName = resourceManager.GetString("GPTools_OSMGPCombineAttributes_inputlayer_displayname");
                inputFeatureClass.Name = "in_osmfeatureclass";
                inputFeatureClass.ParameterType = esriGPParameterType.esriGPParameterTypeRequired;
                in_osmFeatureClassNumber = 0;
                parameterArray.Add(inputFeatureClass);

                //// attribute multi select parameter
                IGPParameterEdit3 attributeSelect = new GPParameterClass() as IGPParameterEdit3;

                IGPDataType tagKeyDataType = new GPStringTypeClass();
                IGPMultiValue tagKeysMultiValue = new GPMultiValueClass();
                tagKeysMultiValue.MemberDataType = tagKeyDataType;

                IGPCodedValueDomain tagKeyDomains = new GPCodedValueDomainClass();
                tagKeyDomains.AddStringCode("", "");

                IGPMultiValueType tagKeyDataType2 = new GPMultiValueTypeClass();
                tagKeyDataType2.MemberDataType = tagKeyDataType;

                attributeSelect.Name = "in_attributeselect";
                attributeSelect.DisplayName = resourceManager.GetString("GPTools_OSMGPCombineAttributes_inputAttributes_displayname");
                attributeSelect.ParameterType = esriGPParameterType.esriGPParameterTypeRequired;
                attributeSelect.Direction = esriGPParameterDirection.esriGPParameterDirectionInput;
                attributeSelect.Domain = (IGPDomain)tagKeyDomains;
                attributeSelect.DataType = (IGPDataType)tagKeyDataType2;
                attributeSelect.Value = (IGPValue)tagKeysMultiValue;

                in_attributeSelectorNumber = 1;
                parameterArray.Add(attributeSelect);


                // output is the derived feature class
                IGPParameterEdit3 outputFeatureClass = new GPParameterClass() as IGPParameterEdit3;
                outputFeatureClass.DataType = new GPFeatureLayerTypeClass();
                outputFeatureClass.Direction = esriGPParameterDirection.esriGPParameterDirectionOutput;
                outputFeatureClass.DisplayName = resourceManager.GetString("GPTools_OSMGPAttributeSelector_outputFeatureClass");
                outputFeatureClass.Name = "out_osmfeatureclass";
                outputFeatureClass.ParameterType = esriGPParameterType.esriGPParameterTypeDerived;
                outputFeatureClass.AddDependency("in_osmfeatureclass");

                out_osmFeatureClassNumber = 2;
                parameterArray.Add(outputFeatureClass);

                return parameterArray;
            }
        }

        public void UpdateMessages(ESRI.ArcGIS.esriSystem.IArray paramvalues, ESRI.ArcGIS.Geoprocessing.IGPEnvironmentManager pEnvMgr, ESRI.ArcGIS.Geodatabase.IGPMessages Messages)
        {
            IGPUtilities3 gpUtilities3 = new GPUtilitiesClass();

            for (int i = 0; i < Messages.Count; i++)
            {
                IGPMessage blah = Messages.GetMessage(i);
                if (blah.IsError())
                {
                    IGPMessage something = new GPMessageClass();
                    something.Description = String.Empty;
                    something.Type = esriGPMessageType.esriGPMessageTypeInformative;
                    something.ErrorCode = 0;
                    Messages.Replace(i, something);
                }
            }

            IGPParameter inputOSMParameter = paramvalues.get_Element(in_osmFeatureClassNumber) as IGPParameter;
            IGPValue inputOSMGPValue = gpUtilities3.UnpackGPValue(inputOSMParameter);

            if (inputOSMGPValue.IsEmpty() == false)
            {
                IFeatureClass osmFeatureClass = null;
                ITable osmInputTable = null;
                IQueryFilter osmQueryFilter = null;

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

                // find the field that holds tag binary/xml field
                int osmTagCollectionFieldIndex = osmInputTable.FindField("osmTags");

                if (osmTagCollectionFieldIndex == -1)
                {
                    Messages.ReplaceAbort(in_osmFeatureClassNumber, resourceManager.GetString("GPTools_OSMGPCombineAttributes_inputlayer_missingtagfield"));
                    return;
                }
            }
        }

        public void UpdateParameters(ESRI.ArcGIS.esriSystem.IArray paramvalues, ESRI.ArcGIS.Geoprocessing.IGPEnvironmentManager pEnvMgr)
        {
            try
            {
                IGPUtilities3 gpUtilities3 = new GPUtilitiesClass();

                IGPParameter inputOSMParameter = paramvalues.get_Element(in_osmFeatureClassNumber) as IGPParameter;
                IGPValue inputOSMGPValue = gpUtilities3.UnpackGPValue(inputOSMParameter);

                if (inputOSMGPValue.IsEmpty() == false)
                {
                    if (inputOSMParameter.Altered == true)
                    {

                        IGPParameter attributeCollectionParameter = paramvalues.get_Element(in_attributeSelectorNumber) as IGPParameter;
                        IGPValue attributeCollectionGPValue = gpUtilities3.UnpackGPValue(attributeCollectionParameter);

                        if (inputOSMParameter.HasBeenValidated == false && ((IGPMultiValue)attributeCollectionGPValue).Count == 0)
                        {
                            IFeatureClass osmFeatureClass = null;
                            ITable osmInputTable = null;
                            IQueryFilter osmQueryFilter = null;

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

                            using (ComReleaser comReleaser = new ComReleaser())
                            {
                                ICursor osmCursor = osmInputTable.Search(osmQueryFilter, true);
                                comReleaser.ManageLifetime(osmCursor);

                                IRow osmRow = osmCursor.NextRow();
                                List<string> potentialOSMFields = new List<string>();

                                if (osmRow != null)
                                {
                                    IFields osmFields = osmRow.Fields;

                                    for (int fieldIndex = 0; fieldIndex < osmFields.FieldCount; fieldIndex++)
                                    {
                                        if (osmFields.get_Field(fieldIndex).Name.Substring(0, 4).Equals("osm_"))
                                        {
                                            potentialOSMFields.Add(osmFields.get_Field(fieldIndex).Name);
                                        }
                                    }
                                }

                                if (potentialOSMFields.Count == 0)
                                {
                                    return;
                                }

                                IGPCodedValueDomain osmTagKeyCodedValues = new GPCodedValueDomainClass();
                                foreach (string tagOSMField in potentialOSMFields)
                                {
                                    osmTagKeyCodedValues.AddStringCode(tagOSMField, tagOSMField);
                                }

                                ((IGPParameterEdit)attributeCollectionParameter).Domain = (IGPDomain)osmTagKeyCodedValues;

                            }
                        }
                    }
                }
            }
            catch { }
        }

        public ESRI.ArcGIS.Geodatabase.IGPMessages Validate(ESRI.ArcGIS.esriSystem.IArray paramvalues, bool updateValues, ESRI.ArcGIS.Geoprocessing.IGPEnvironmentManager envMgr)
        {
            return default(ESRI.ArcGIS.Geodatabase.IGPMessages);
        }
        #endregion

    }
}
