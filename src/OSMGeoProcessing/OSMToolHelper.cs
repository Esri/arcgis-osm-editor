using ESRI.ArcGIS.Geodatabase;
using System.Runtime.InteropServices;
using ESRI.ArcGIS.esriSystem;
using System.IO;
using System.Xml;
using System.Reflection;
using System.Globalization;
using ESRI.ArcGIS.Display;
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.OSM.OSMClassExtension;
using System;
using System.Linq;
using ESRI.ArcGIS.Geometry;
using System.Resources;
using System.Xml.Serialization;
using System.Collections.Generic;
using ESRI.ArcGIS.Geoprocessing;
using System.Text;
using System.Text.RegularExpressions;


namespace ESRI.ArcGIS.OSM.GeoProcessing
{
    [Guid("FBC34774-9382-4607-B3BA-1DB56C8030A0")]
    [ClassInterface(ClassInterfaceType.None)]
    [ProgId("OSMEditor.OSMToolHelper")]
    [ComVisible(true)]
    public class OSMToolHelper
    {
        ResourceManager _resourceManager = null;
        OSMUtility _osmUtility = null;

        [ComVisible(false)]
        public class OSMNodeFeature
        {
            public IPoint nodeGeometry;
            public string nodeID;
            public List<string> relationList;
            public bool isSupportElement;

            ~OSMNodeFeature()
            {
                ComReleaser.ReleaseCOMObject(nodeGeometry);
            }
        }

        [ComVisible(false)]
        public class OSMLineFeature
        {
            public IPolyline lineGeometry;
            public string lineID;
            public List<OSMRelationMember> relationMembers;
            public List<string> relationList;
            public bool isSupportElement;

            ~OSMLineFeature()
            {
                ComReleaser.ReleaseCOMObject(lineGeometry);
            }
        }

        [ComVisible(false)]
        public class OSMPolygonFeature
        {
            public IPolygon polygonGeometry;
            public string polygonID;
            public List<OSMRelationMember> relationMembers;
            public List<string> relationList;
            public bool isSupportElement;

                    ~OSMPolygonFeature()
            {
                ComReleaser.ReleaseCOMObject(polygonGeometry);
            }
        
        }

        [ComVisible(false)]
        public class OSMRelation
        {
            public string relationID;
            public List<OSMRelationMember> relationMembers;
            public List<string> relationList;
            public bool isSupportElement;
        }

        [ComVisible(false)]
        public class OSMRelationMember
        {
            public string memberType;
            public string referenceID;
            public string role;
            public osmRelationGeometryType osmGeometryType;
        }

        [ComVisible(false)]
        public enum osmRelationGeometryType
        {
            osmPoint,
            osmPolyline,
            osmPolygon,
            osmHybridGeometry,
            osmUnknownGeometry
        }

        public class osmItemReference
        {
            public int refCount;
            public List<string> relations;
        }

        public class simplePointRef
        {
            public float Longitude { get; set; }
            public float Latitude { get; set; }
            public int RefCounter { get; set; }
            public int pointObjectID { get; set; }

            public simplePointRef(float longitude, float latitude, int refCount, int pointobjectid)
            {
                Longitude = longitude;
                Latitude = latitude;
                RefCounter = refCount;
                pointObjectID = pointobjectid;
            }
        }

        public OSMToolHelper()
        {
            _resourceManager = new ResourceManager("ESRI.ArcGIS.OSM.GeoProcessing.OSMGPToolsStrings", this.GetType().Assembly);
            _osmUtility = new OSMUtility();
        }

        ~OSMToolHelper()
        {
            if (_osmUtility != null)
                _osmUtility = null;
        }

        #region"Create OSM Point FeatureClass"
        internal IFeatureClass CreatePointFeatureClass(ESRI.ArcGIS.Geodatabase.IWorkspace2 workspace, ESRI.ArcGIS.Geodatabase.IFeatureDataset featureDataset, System.String featureClassName, ESRI.ArcGIS.Geodatabase.IFields fields, ESRI.ArcGIS.esriSystem.UID CLSID, ESRI.ArcGIS.esriSystem.UID CLSEXT, System.String strConfigKeyword, OSMDomains osmDomains, string metadataAbstract, string metadataPurpose)
        {
            return CreatePointFeatureClass(workspace, featureDataset, featureClassName, fields, CLSID, CLSEXT, strConfigKeyword, osmDomains, metadataAbstract, metadataPurpose, null);
        }


        ///<summary>Simple helper to create a featureclass in a geodatabase.</summary>
        /// 
        ///<param name="workspace">An IWorkspace2 interface</param>
        ///<param name="featureDataset">An IFeatureDataset interface or Nothing</param>
        ///<param name="featureClassName">A System.String that contains the name of the feature class to open or create. Example: "states"</param>
        ///<param name="fields">An IFields interface</param>
        ///<param name="CLSID">A UID value or Nothing. Example "esriGeoDatabase.Feature" or Nothing</param>
        ///<param name="CLSEXT">A UID value or Nothing (this is the class extension if you want to reference a class extension when creating the feature class).</param>
        ///<param name="strConfigKeyword">An empty System.String or RDBMS table string for ArcSDE. Example: "myTable" or ""</param>
        ///  
        ///<returns>An IFeatureClass interface or a Nothing</returns>
        ///  
        ///<remarks>
        ///  (1) If a 'featureClassName' already exists in the workspace a reference to that feature class 
        ///      object will be returned.
        ///  (2) If an IFeatureDataset is passed in for the 'featureDataset' argument the feature class
        ///      will be created in the dataset. If a Nothing is passed in for the 'featureDataset'
        ///      argument the feature class will be created in the workspace.
        ///  (3) When creating a feature class in a dataset the spatial reference is inherited 
        ///      from the dataset object.
        ///  (4) If an IFields interface is supplied for the 'fields' collection it will be used to create the
        ///      table. If a Nothing value is supplied for the 'fields' collection, a table will be created using 
        ///      default values in the method.
        ///  (5) The 'strConfigurationKeyword' parameter allows the application to control the physical layout 
        ///      for this table in the underlying RDBMS—for example, in the case of an Oracle database, the 
        ///      configuration keyword controls the tablespace in which the table is created, the initial and 
        ///     next extents, and other properties. The 'strConfigurationKeywords' for an ArcSDE instance are 
        ///      set up by the ArcSDE data administrator, the list of available keywords supported by a workspace 
        ///      may be obtained using the IWorkspaceConfiguration interface. For more information on configuration 
        ///      keywords, refer to the ArcSDE documentation. When not using an ArcSDE table use an empty 
        ///      string (ex: "").
        ///</remarks>
        internal IFeatureClass CreatePointFeatureClass(ESRI.ArcGIS.Geodatabase.IWorkspace2 workspace, ESRI.ArcGIS.Geodatabase.IFeatureDataset featureDataset, System.String featureClassName, ESRI.ArcGIS.Geodatabase.IFields fields, ESRI.ArcGIS.esriSystem.UID CLSID, ESRI.ArcGIS.esriSystem.UID CLSEXT, System.String strConfigKeyword, OSMDomains osmDomains, string metadataAbstract, string metadataPurpose, List<string> additionalTagFields)
        {
            if (featureClassName == "") return null; // name was not passed in 

            ESRI.ArcGIS.Geodatabase.IFeatureClass featureClass = null;

            try
            {
                ESRI.ArcGIS.Geodatabase.IFeatureWorkspace featureWorkspace = (ESRI.ArcGIS.Geodatabase.IFeatureWorkspace)workspace; // Explicit Cast

                if (workspace.get_NameExists(ESRI.ArcGIS.Geodatabase.esriDatasetType.esriDTFeatureClass, featureClassName)) //feature class with that name already exists 
                {
                    // if a feature class with the same name already exists delete it....
                    featureClass = featureWorkspace.OpenFeatureClass(featureClassName);

                    if (!DeleteDataset((IDataset)featureClass))
                    {
                        return featureClass;
                    }
                }

                String illegalCharacters = String.Empty;

                ISQLSyntax sqlSyntax = workspace as ISQLSyntax;
                if (sqlSyntax != null)
                {
                    illegalCharacters = sqlSyntax.GetInvalidCharacters();
                }

                // assign the class id value if not assigned
                if (CLSID == null)
                {
                    CLSID = new ESRI.ArcGIS.esriSystem.UIDClass();
                    CLSID.Value = "esriGeoDatabase.Feature";
                }

                ESRI.ArcGIS.Geodatabase.IObjectClassDescription objectClassDescription = new ESRI.ArcGIS.Geodatabase.FeatureClassDescriptionClass();

                // if a fields collection is not passed in then supply our own
                if (fields == null)
                {
                    // create the fields using the required fields method
                    fields = objectClassDescription.RequiredFields;
                    ESRI.ArcGIS.Geodatabase.IFieldsEdit fieldsEdit = (ESRI.ArcGIS.Geodatabase.IFieldsEdit)fields; // Explicit Cast

                    // add the domain driven string field for the OSM features
                    foreach (var domainAttribute in osmDomains.domain)
                    {
                        IFieldEdit domainField = new FieldClass() as IFieldEdit;
                        domainField.Name_2 = domainAttribute.name;
                        domainField.Required_2 = true;
                        domainField.Type_2 = esriFieldType.esriFieldTypeString;
                        domainField.Length_2 = 30;
                        try
                        {
                            domainField.Domain_2 = ((IWorkspaceDomains)workspace).get_DomainByName(domainAttribute.name + "_pt");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine(ex.Message);
                            System.Diagnostics.Debug.WriteLine(ex.StackTrace);
                        }

                        fieldsEdit.AddField((IField)domainField);
                    }

                    // add the OSM ID field
                    IFieldEdit osmIDField = new FieldClass() as IFieldEdit;
                    osmIDField.Name_2 = "OSMID";
                    osmIDField.Required_2 = true;
                    osmIDField.Type_2 = esriFieldType.esriFieldTypeString;
                    osmIDField.Length_2 = 20;
                    fieldsEdit.AddField((IField)osmIDField);

                    // add the field for the tag cloud for all other tag/value pairs
                    IFieldEdit osmXmlTagsField = new FieldClass() as IFieldEdit;
                    osmXmlTagsField.Name_2 = "osmTags";
                    osmXmlTagsField.Required_2 = true;
                    //if (((IWorkspace)workspace).Type == esriWorkspaceType.esriLocalDatabaseWorkspace)
                    //{
                    osmXmlTagsField.Type_2 = esriFieldType.esriFieldTypeBlob;
                    //}
                    //else
                    //{
                    //    osmXmlTagsField.Type_2 = esriFieldType.esriFieldTypeXML;
                    //}
                    fieldsEdit.AddField((IField)osmXmlTagsField);

                    // user, uid, visible, version, changeset, timestamp
                    IFieldEdit osmuserField = new FieldClass() as IFieldEdit;
                    osmuserField.Name_2 = "osmuser";
                    osmuserField.Required_2 = true;
                    osmuserField.Type_2 = esriFieldType.esriFieldTypeString;
                    osmuserField.Length_2 = 100;
                    fieldsEdit.AddField((IField)osmuserField);

                    IFieldEdit osmuidField = new FieldClass() as IFieldEdit;
                    osmuidField.Name_2 = "osmuid";
                    osmuidField.Required_2 = true;
                    osmuidField.Type_2 = esriFieldType.esriFieldTypeInteger;
                    fieldsEdit.AddField((IField)osmuidField);

                    IFieldEdit osmvisibleField = new FieldClass() as IFieldEdit;
                    osmvisibleField.Name_2 = "osmvisible";
                    osmvisibleField.Required_2 = true;
                    osmvisibleField.Type_2 = esriFieldType.esriFieldTypeString;
                    osmvisibleField.Length_2 = 20;
                    fieldsEdit.AddField((IField)osmvisibleField);

                    IFieldEdit osmversionField = new FieldClass() as IFieldEdit;
                    osmversionField.Name_2 = "osmversion";
                    osmversionField.Required_2 = true;
                    osmversionField.Type_2 = esriFieldType.esriFieldTypeSmallInteger;
                    fieldsEdit.AddField((IField)osmversionField);

                    IFieldEdit osmchangesetField = new FieldClass() as IFieldEdit;
                    osmchangesetField.Name_2 = "osmchangeset";
                    osmchangesetField.Required_2 = true;
                    osmchangesetField.Type_2 = esriFieldType.esriFieldTypeInteger;
                    fieldsEdit.AddField((IField)osmchangesetField);

                    IFieldEdit osmtimestampField = new FieldClass() as IFieldEdit;
                    osmtimestampField.Name_2 = "osmtimestamp";
                    osmtimestampField.Required_2 = true;
                    osmtimestampField.Type_2 = esriFieldType.esriFieldTypeDate;
                    fieldsEdit.AddField((IField)osmtimestampField);

                    IFieldEdit osmrelationIDField = new FieldClass() as IFieldEdit;
                    osmrelationIDField.Name_2 = "osmMemberOf";
                    osmrelationIDField.Required_2 = true;
                    //if (((IWorkspace)workspace).Type == esriWorkspaceType.esriLocalDatabaseWorkspace)
                    //{
                    osmrelationIDField.Type_2 = esriFieldType.esriFieldTypeBlob;
                    //}
                    //else
                    //{
                    //    osmrelationIDField.Type_2 = esriFieldType.esriFieldTypeXML;
                    //}
                    fieldsEdit.AddField((IField)osmrelationIDField);

                    //IFieldEdit osmrelationsField = new FieldClass() as IFieldEdit;
                    //osmrelationsField.Name_2 = "osmMembers";
                    //if (((IWorkspace)workspace).Type == esriWorkspaceType.esriLocalDatabaseWorkspace)
                    //{
                    //    osmrelationsField.Type_2 = esriFieldType.esriFieldTypeBlob;
                    //}
                    //else
                    //{
                    //    osmrelationsField.Type_2 = esriFieldType.esriFieldTypeXML;
                    //}
                    //fieldsEdit.AddField((IField)osmrelationsField);

                    IFieldEdit osmSupportingElementField = new FieldClass() as IFieldEdit;
                    osmSupportingElementField.Name_2 = "osmSupportingElement";
                    osmSupportingElementField.Required_2 = true;
                    osmSupportingElementField.Type_2 = esriFieldType.esriFieldTypeString;
                    osmSupportingElementField.Length_2 = 5;
                    osmSupportingElementField.DefaultValue_2 = "no";
                    fieldsEdit.AddField((IField)osmSupportingElementField);

                    IFieldEdit wayRefCountField = new FieldClass() as IFieldEdit;
                    wayRefCountField.Name_2 = "wayRefCount";
                    wayRefCountField.Required_2 = true;
                    wayRefCountField.Type_2 = esriFieldType.esriFieldTypeInteger;
                    wayRefCountField.DefaultValue_2 = 0;
                    fieldsEdit.AddField((IField)wayRefCountField);

                    //IFieldEdit osmTrackChangesField = new FieldClass() as IFieldEdit;
                    //osmTrackChangesField.Name_2 = "osmTrackChanges";
                    //osmTrackChangesField.Required_2 = true;
                    //osmTrackChangesField.Type_2 = esriFieldType.esriFieldTypeSmallInteger;
                    //osmTrackChangesField.DefaultValue_2 = 0;
                    //fieldsEdit.AddField((IField)osmTrackChangesField);

                    if (additionalTagFields != null)
                    {
                        foreach (string nameOfTag in additionalTagFields)
                        {
                            IFieldEdit osmTagAttributeField = new FieldClass() as IFieldEdit;
                            osmTagAttributeField.Name_2 = OSMToolHelper.convert2AttributeFieldName(nameOfTag, illegalCharacters);
                            osmTagAttributeField.AliasName_2 = nameOfTag + _resourceManager.GetString("GPTools_OSMGPAttributeSelector_aliasaddition");
                            osmTagAttributeField.Type_2 = esriFieldType.esriFieldTypeString;
                            osmTagAttributeField.Length_2 = 100;
                            osmTagAttributeField.Required_2 = false;

                            fieldsEdit.AddField((IField)osmTagAttributeField);
                        }
                    }

                    fields = (ESRI.ArcGIS.Geodatabase.IFields)fieldsEdit; // Explicit Cast
                }

                System.String strShapeField = "";

                // locate the shape field
                for (int j = 0; j < fields.FieldCount; j++)
                {
                    if (fields.get_Field(j).Type == ESRI.ArcGIS.Geodatabase.esriFieldType.esriFieldTypeGeometry)
                    {
                        strShapeField = fields.get_Field(j).Name;

                        // redefine geometry type

                        IFieldEdit shapeField = fields.get_Field(j) as IFieldEdit;
                        IGeometryDefEdit geometryDef = new GeometryDefClass() as IGeometryDefEdit;
                        geometryDef.GeometryType_2 = esriGeometryType.esriGeometryPoint;
                        geometryDef.HasZ_2 = false;
                        geometryDef.HasM_2 = false;
                        geometryDef.GridCount_2 = 1;
                        geometryDef.set_GridSize(0, 0);

                        geometryDef.SpatialReference_2 = ((IGeoDataset)featureDataset).SpatialReference;

                        shapeField.GeometryDef_2 = (IGeometryDef)geometryDef;

                        break;
                    }
                }

                // Use IFieldChecker to create a validated fields collection.
                ESRI.ArcGIS.Geodatabase.IFieldChecker fieldChecker = new ESRI.ArcGIS.Geodatabase.FieldCheckerClass();
                ESRI.ArcGIS.Geodatabase.IEnumFieldError enumFieldError = null;
                ESRI.ArcGIS.Geodatabase.IFields validatedFields = null;
                fieldChecker.ValidateWorkspace = (ESRI.ArcGIS.Geodatabase.IWorkspace)workspace;
                fieldChecker.Validate(fields, out enumFieldError, out validatedFields);

                // The enumFieldError enumerator can be inspected at this point to determine 
                // which fields were modified during validation.


                // finally create and return the feature class
                if (featureDataset == null)// if no feature dataset passed in, create at the workspace level
                {
                    try
                    {
                        featureClass = featureWorkspace.CreateFeatureClass(featureClassName, validatedFields, CLSID, CLSEXT, ESRI.ArcGIS.Geodatabase.esriFeatureType.esriFTSimple, strShapeField, strConfigKeyword);
                        IPropertySet extensionPropertySet = new PropertySetClass();
                        extensionPropertySet.SetProperty("VERSION", OSMClassExtensionManager.Version);
                        ((IClassSchemaEdit2)featureClass).AlterClassExtensionProperties(extensionPropertySet);
                    }
                    catch
                    {
                        throw;
                    }
                }
                else
                {
                    try
                    {
                        featureClass = featureDataset.CreateFeatureClass(featureClassName, validatedFields, CLSID, CLSEXT, ESRI.ArcGIS.Geodatabase.esriFeatureType.esriFTSimple, strShapeField, strConfigKeyword);
                        IPropertySet extensionPropertySet = new PropertySetClass();
                        extensionPropertySet.SetProperty("VERSION", OSMClassExtensionManager.Version);
                        ((IClassSchemaEdit2)featureClass).AlterClassExtensionProperties(extensionPropertySet);
                    }
                    catch
                    {
                        throw;
                    }
                }


                // create the openstreetmap specific metadata
                _osmUtility.CreateOSMMetadata((IDataset)featureClass, metadataAbstract, metadataPurpose);

                // the change at release 2.1 requires a new model name
                IModelInfo fcModelInfo = featureClass as IModelInfo;
                if (fcModelInfo != null)
                {
                    fcModelInfo.ModelName = OSMClassExtensionManager.OSMModelName;
                }

            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
                throw;
            }

            return featureClass;
        }
        #endregion

        #region"Create Table"
        // ArcGIS Snippet Title:
        // Create Table
        // 
        // Long Description:
        // Creates a dataset in a workspace.
        // 
        // Add the following references to the project:
        // ESRI.ArcGIS.Geodatabase
        // ESRI.ArcGIS.Geometry
        // ESRI.ArcGIS.System
        // 
        // Intended ArcGIS Products for this snippet:
        // ArcGIS Desktop (ArcEditor, ArcInfo, ArcView)
        // ArcGIS Engine
        // ArcGIS Server
        // 
        // Applicable ArcGIS Product Versions:
        // 9.2
        // 9.3
        // 9.3.1
        // 10.0
        // 
        // Required ArcGIS Extensions:
        // (NONE)
        // 
        // Notes:
        // This snippet is intended to be inserted at the base level of a Class.
        // It is not intended to be nested within an existing Method.
        // 

        ///<summary>Creates a table with some default fields.</summary>
        /// 
        ///<param name="workspace">An IWorkspace2 interface</param>
        ///<param name="tableName">A System.String of the table name in the workspace. Example: "owners"</param>
        ///<param name="fields">An IFields interface or Nothing</param>
        ///  
        ///<returns>An ITable interface or Nothing</returns>
        ///  
        ///<remarks>
        ///Notes:
        ///(1) If an IFields interface is supplied for the 'fields' collection it will be used to create the
        ///    table. If a Nothing value is supplied for the 'fields' collection, a table will be created using 
        ///    default values in the method.
        ///(2) If a table with the supplied 'tableName' exists in the workspace an ITable will be returned.
        ///    if table does not exit a new one will be created.
        ///</remarks>
        internal ESRI.ArcGIS.Geodatabase.ITable CreateRelationTable(ESRI.ArcGIS.Geodatabase.IWorkspace2 workspace, System.String tableName, ESRI.ArcGIS.Geodatabase.IFields fields, string storageKeyword, string metadataAbstract, string metadataPurpose)
        {
            // create the behavior clasid for the featureclass
            ESRI.ArcGIS.esriSystem.UID uid = new ESRI.ArcGIS.esriSystem.UIDClass();

            if (workspace == null) return null; // valid feature workspace not passed in as an argument to the method

            ESRI.ArcGIS.Geodatabase.ITable table = null;

            try
            {
                ESRI.ArcGIS.Geodatabase.IFeatureWorkspace featureWorkspace = (ESRI.ArcGIS.Geodatabase.IFeatureWorkspace)workspace; // Explicit Cast

                if (workspace.get_NameExists(ESRI.ArcGIS.Geodatabase.esriDatasetType.esriDTTable, tableName))
                {
                    // if a table with the same name already exists delete it....
                    table = featureWorkspace.OpenTable(tableName);

                    if (!DeleteDataset((IDataset)table))
                    {
                        return table;
                    }
                }

                uid.Value = "esriGeoDatabase.Object";

                ESRI.ArcGIS.Geodatabase.IObjectClassDescription objectClassDescription = new ESRI.ArcGIS.Geodatabase.ObjectClassDescriptionClass();

                // if a fields collection is not passed in then supply our own
                if (fields == null)
                {
                    // create the fields using the required fields method
                    fields = objectClassDescription.RequiredFields;
                    ESRI.ArcGIS.Geodatabase.IFieldsEdit fieldsEdit = (ESRI.ArcGIS.Geodatabase.IFieldsEdit)fields; // Explicit Cast

                    // create OSM base fields the osm administrative fields
                    // add the OSM ID field
                    IFieldEdit osmIDField = new FieldClass() as IFieldEdit;
                    osmIDField.Name_2 = "OSMID";
                    osmIDField.Required_2 = true;
                    osmIDField.Type_2 = esriFieldType.esriFieldTypeString;
                    osmIDField.Length_2 = 20;
                    fieldsEdit.AddField((IField)osmIDField);

                    // user, uid, visible, version, changeset, timestamp
                    IFieldEdit osmuserField = new FieldClass() as IFieldEdit;
                    osmuserField.Name_2 = "osmuser";
                    osmuserField.Required_2 = true;
                    osmuserField.Type_2 = esriFieldType.esriFieldTypeString;
                    osmuserField.Length_2 = 100;
                    fieldsEdit.AddField((IField)osmuserField);

                    IFieldEdit osmuidField = new FieldClass() as IFieldEdit;
                    osmuidField.Name_2 = "osmuid";
                    osmuidField.Required_2 = true;
                    osmuidField.Type_2 = esriFieldType.esriFieldTypeInteger;
                    fieldsEdit.AddField((IField)osmuidField);

                    IFieldEdit osmvisibleField = new FieldClass() as IFieldEdit;
                    osmvisibleField.Name_2 = "osmvisible";
                    osmvisibleField.Required_2 = true;
                    osmvisibleField.Type_2 = esriFieldType.esriFieldTypeString;
                    osmvisibleField.Length_2 = 20;
                    fieldsEdit.AddField((IField)osmvisibleField);

                    IFieldEdit osmversionField = new FieldClass() as IFieldEdit;
                    osmversionField.Name_2 = "osmversion";
                    osmversionField.Required_2 = true;
                    osmversionField.Type_2 = esriFieldType.esriFieldTypeSmallInteger;
                    fieldsEdit.AddField((IField)osmversionField);

                    IFieldEdit osmchangesetField = new FieldClass() as IFieldEdit;
                    osmchangesetField.Name_2 = "osmchangeset";
                    osmchangesetField.Required_2 = true;
                    osmchangesetField.Type_2 = esriFieldType.esriFieldTypeInteger;
                    fieldsEdit.AddField((IField)osmchangesetField);

                    IFieldEdit osmtimestampField = new FieldClass() as IFieldEdit;
                    osmtimestampField.Name_2 = "osmtimestamp";
                    osmtimestampField.Required_2 = true;
                    osmtimestampField.Type_2 = esriFieldType.esriFieldTypeDate;
                    fieldsEdit.AddField((IField)osmtimestampField);

                    IFieldEdit osmrelationIDField = new FieldClass() as IFieldEdit;
                    osmrelationIDField.Name_2 = "osmMemberOf";
                    osmrelationIDField.Required_2 = true;
                    //if (((IWorkspace)workspace).Type == esriWorkspaceType.esriLocalDatabaseWorkspace)
                    //{
                    osmrelationIDField.Type_2 = esriFieldType.esriFieldTypeBlob;
                    //}
                    //else
                    //{
                    //    osmrelationIDField.Type_2 = esriFieldType.esriFieldTypeXML;
                    //}
                    fieldsEdit.AddField((IField)osmrelationIDField);

                    IFieldEdit osmMembersField = new FieldClass() as IFieldEdit;
                    osmMembersField.Name_2 = "osmMembers";
                    //if (((IWorkspace)workspace).Type == esriWorkspaceType.esriLocalDatabaseWorkspace)
                    //{
                    osmMembersField.Required_2 = true;
                    osmMembersField.Type_2 = esriFieldType.esriFieldTypeBlob;
                    //}
                    //else
                    //{
                    //    osmMembersField.Type_2 = esriFieldType.esriFieldTypeXML;
                    //}
                    fieldsEdit.AddField((IField)osmMembersField);

                    // add the field for the tag cloud for all other tag/value pairs
                    IFieldEdit osmXmlTagsField = new FieldClass() as IFieldEdit;
                    osmXmlTagsField.Name_2 = "osmTags";
                    osmXmlTagsField.Required_2 = true;
                    //if (((IWorkspace)workspace).Type == esriWorkspaceType.esriLocalDatabaseWorkspace)
                    //{
                    osmXmlTagsField.Type_2 = esriFieldType.esriFieldTypeBlob;
                    //}
                    //else
                    //{
                    //    osmXmlTagsField.Type_2 = esriFieldType.esriFieldTypeXML;
                    //}
                    fieldsEdit.AddField((IField)osmXmlTagsField);

                    //IFieldEdit osmTrackChangesField = new FieldClass() as IFieldEdit;
                    //osmTrackChangesField.Name_2 = "osmTrackChanges";
                    //osmTrackChangesField.Required_2 = true;
                    //osmTrackChangesField.Type_2 = esriFieldType.esriFieldTypeSmallInteger;
                    //osmTrackChangesField.DefaultValue_2 = 0;
                    //fieldsEdit.AddField((IField)osmTrackChangesField);



                    fields = (ESRI.ArcGIS.Geodatabase.IFields)fieldsEdit; // Explicit Cast
                }

                // Use IFieldChecker to create a validated fields collection.
                ESRI.ArcGIS.Geodatabase.IFieldChecker fieldChecker = new ESRI.ArcGIS.Geodatabase.FieldCheckerClass();
                ESRI.ArcGIS.Geodatabase.IEnumFieldError enumFieldError = null;
                ESRI.ArcGIS.Geodatabase.IFields validatedFields = null;
                fieldChecker.ValidateWorkspace = (ESRI.ArcGIS.Geodatabase.IWorkspace)workspace;
                fieldChecker.Validate(fields, out enumFieldError, out validatedFields);

                // The enumFieldError enumerator can be inspected at this point to determine 
                // which fields were modified during validation.


                // create and return the table
                table = featureWorkspace.CreateTable(tableName, validatedFields, uid, null, storageKeyword);


                // create the openstreetmap spcific metadata
                _osmUtility.CreateOSMMetadata((IDataset)table, metadataAbstract, metadataPurpose);
            }
            catch
            {
                throw;
            }

            return table;
        }
        #endregion

        #region create revision table
        internal ESRI.ArcGIS.Geodatabase.ITable CreateRevisionTable(ESRI.ArcGIS.Geodatabase.IWorkspace2 workspace, System.String tableName, ESRI.ArcGIS.Geodatabase.IFields fields, string storageKeyword)
        {
            // create the behavior clasid for the featureclass
            ESRI.ArcGIS.esriSystem.UID uid = new ESRI.ArcGIS.esriSystem.UIDClass();

            if (workspace == null) return null; // valid feature workspace not passed in as an argument to the method

            ESRI.ArcGIS.Geodatabase.ITable table = null;

            try
            {
                ESRI.ArcGIS.Geodatabase.IFeatureWorkspace featureWorkspace = (ESRI.ArcGIS.Geodatabase.IFeatureWorkspace)workspace; // Explicit Cast

                if (workspace.get_NameExists(ESRI.ArcGIS.Geodatabase.esriDatasetType.esriDTTable, tableName))
                {
                    // if a table with the same name already exists delete it....if that is not possible return the current instance
                    table = featureWorkspace.OpenTable(tableName);

                    if (!DeleteDataset((IDataset)table))
                    {
                        return table;
                    }
                }

                uid.Value = "esriGeoDatabase.Object";

                ESRI.ArcGIS.Geodatabase.IObjectClassDescription objectClassDescription = new ESRI.ArcGIS.Geodatabase.ObjectClassDescriptionClass();

                // if a fields collection is not passed in then supply our own
                if (fields == null)
                {
                    // create the fields using the required fields method
                    fields = objectClassDescription.RequiredFields;
                    ESRI.ArcGIS.Geodatabase.IFieldsEdit fieldsEdit = (ESRI.ArcGIS.Geodatabase.IFieldsEdit)fields; // Explicit Cast

                    // 
                    IFieldEdit osmChangesetField = new FieldClass() as IFieldEdit;
                    osmChangesetField.Name_2 = "osmchangeset";
                    osmChangesetField.Required_2 = true;
                    osmChangesetField.Type_2 = esriFieldType.esriFieldTypeInteger;
                    fieldsEdit.AddField((IField)osmChangesetField);

                    IFieldEdit osmActionField = new FieldClass() as IFieldEdit;
                    osmActionField.Name_2 = "osmaction";
                    osmActionField.Required_2 = true;
                    osmActionField.Type_2 = esriFieldType.esriFieldTypeString;
                    osmActionField.Length_2 = 10;
                    fieldsEdit.AddField((IField)osmActionField);

                    IFieldEdit osmElementTypeField = new FieldClass() as IFieldEdit;
                    osmElementTypeField.Name_2 = "osmelementtype";
                    osmElementTypeField.Required_2 = true;
                    osmElementTypeField.Type_2 = esriFieldType.esriFieldTypeString;
                    osmElementTypeField.Length_2 = 10;
                    fieldsEdit.AddField((IField)osmElementTypeField);

                    IFieldEdit osmVersionField = new FieldClass() as IFieldEdit;
                    osmVersionField.Name_2 = "osmversion";
                    osmVersionField.Required_2 = true;
                    osmVersionField.Type_2 = esriFieldType.esriFieldTypeInteger;
                    fieldsEdit.AddField((IField)osmVersionField);

                    IFieldEdit fcNameField = new FieldClass() as IFieldEdit;
                    fcNameField.Name_2 = "sourcefcname";
                    fcNameField.Required_2 = true;
                    fcNameField.Type_2 = esriFieldType.esriFieldTypeString;
                    fcNameField.Length_2 = 50;
                    fieldsEdit.AddField((IField)fcNameField);

                    IFieldEdit osmOldIDField = new FieldClass() as IFieldEdit;
                    osmOldIDField.Name_2 = "osmoldid";
                    osmOldIDField.Required_2 = true;
                    osmOldIDField.Type_2 = esriFieldType.esriFieldTypeString;
                    osmOldIDField.Length_2 = 20;
                    fieldsEdit.AddField((IField)osmOldIDField);

                    IFieldEdit osmNewIDField = new FieldClass() as IFieldEdit;
                    osmNewIDField.Name_2 = "osmnewid";
                    osmNewIDField.Required_2 = true;
                    osmNewIDField.Type_2 = esriFieldType.esriFieldTypeString;
                    osmNewIDField.Length_2 = 20;
                    fieldsEdit.AddField((IField)osmNewIDField);

                    IFieldEdit latField = new FieldClass() as IFieldEdit;
                    latField.Name_2 = "osmlat";
                    latField.Required_2 = true;
                    latField.Type_2 = esriFieldType.esriFieldTypeDouble;
                    latField.Scale_2 = 10;
                    fieldsEdit.AddField((IField)latField);

                    IFieldEdit lonField = new FieldClass() as IFieldEdit;
                    lonField.Name_2 = "osmlon";
                    lonField.Required_2 = true;
                    lonField.Type_2 = esriFieldType.esriFieldTypeDouble;
                    lonField.Scale_2 = 10;
                    fieldsEdit.AddField((IField)lonField);

                    IFieldEdit statusField = new FieldClass() as IFieldEdit;
                    statusField.Name_2 = "osmstatus";
                    statusField.Required_2 = true;
                    statusField.Type_2 = esriFieldType.esriFieldTypeString;
                    statusField.Length_2 = 40;
                    fieldsEdit.AddField((IField)statusField);

                    IFieldEdit statusCodeField = new FieldClass() as IFieldEdit;
                    statusCodeField.Name_2 = "osmstatuscode";
                    statusCodeField.Required_2 = true;
                    statusCodeField.Type_2 = esriFieldType.esriFieldTypeInteger;
                    fieldsEdit.AddField((IField)statusCodeField);

                    IFieldEdit errorMessageField = new FieldClass() as IFieldEdit;
                    errorMessageField.Name_2 = "osmerrormessage";
                    errorMessageField.Required_2 = true;
                    errorMessageField.Type_2 = esriFieldType.esriFieldTypeString;
                    errorMessageField.Length_2 = 255;
                    fieldsEdit.AddField((IField)errorMessageField);

                    fields = (ESRI.ArcGIS.Geodatabase.IFields)fieldsEdit; // Explicit Cast
                }

                // Use IFieldChecker to create a validated fields collection.
                ESRI.ArcGIS.Geodatabase.IFieldChecker fieldChecker = new ESRI.ArcGIS.Geodatabase.FieldCheckerClass();
                ESRI.ArcGIS.Geodatabase.IEnumFieldError enumFieldError = null;
                ESRI.ArcGIS.Geodatabase.IFields validatedFields = null;
                fieldChecker.ValidateWorkspace = (ESRI.ArcGIS.Geodatabase.IWorkspace)workspace;
                fieldChecker.Validate(fields, out enumFieldError, out validatedFields);

                // The enumFieldError enumerator can be inspected at this point to determine 
                // which fields were modified during validation.


                // create and return the table
                table = featureWorkspace.CreateTable(tableName, validatedFields, uid, null, storageKeyword);
            }
            catch
            {
                throw;
            }

            return table;
        }
        #endregion

        #region"Create OSM Line FeatureClass"
        internal ESRI.ArcGIS.Geodatabase.IFeatureClass CreateLineFeatureClass(ESRI.ArcGIS.Geodatabase.IWorkspace2 workspace, ESRI.ArcGIS.Geodatabase.IFeatureDataset featureDataset, System.String featureClassName, ESRI.ArcGIS.Geodatabase.IFields fields, ESRI.ArcGIS.esriSystem.UID CLSID, ESRI.ArcGIS.esriSystem.UID CLSEXT, System.String strConfigKeyword, OSMDomains osmDomains, string metadataAbstract, string metadataPurpose)
        {
            return CreateLineFeatureClass(workspace, featureDataset, featureClassName, fields, CLSID, CLSEXT, strConfigKeyword, osmDomains, metadataAbstract, metadataPurpose, null);
        }

        ///<summary>Simple helper to create a featureclass in a geodatabase.</summary>
        /// 
        ///<param name="workspace">An IWorkspace2 interface</param>
        ///<param name="featureDataset">An IFeatureDataset interface or Nothing</param>
        ///<param name="featureClassName">A System.String that contains the name of the feature class to open or create. Example: "states"</param>
        ///<param name="fields">An IFields interface</param>
        ///<param name="CLSID">A UID value or Nothing. Example "esriGeoDatabase.Feature" or Nothing</param>
        ///<param name="CLSEXT">A UID value or Nothing (this is the class extension if you want to reference a class extension when creating the feature class).</param>
        ///<param name="strConfigKeyword">An empty System.String or RDBMS table string for ArcSDE. Example: "myTable" or ""</param>
        ///  
        ///<returns>An IFeatureClass interface or a Nothing</returns>
        ///  
        ///<remarks>
        ///  (1) If a 'featureClassName' already exists in the workspace a reference to that feature class 
        ///      object will be returned.
        ///  (2) If an IFeatureDataset is passed in for the 'featureDataset' argument the feature class
        ///      will be created in the dataset. If a Nothing is passed in for the 'featureDataset'
        ///      argument the feature class will be created in the workspace.
        ///  (3) When creating a feature class in a dataset the spatial reference is inherited 
        ///      from the dataset object.
        ///  (4) If an IFields interface is supplied for the 'fields' collection it will be used to create the
        ///      table. If a Nothing value is supplied for the 'fields' collection, a table will be created using 
        ///      default values in the method.
        ///  (5) The 'strConfigurationKeyword' parameter allows the application to control the physical layout 
        ///      for this table in the underlying RDBMS—for example, in the case of an Oracle database, the 
        ///      configuration keyword controls the tablespace in which the table is created, the initial and 
        ///     next extents, and other properties. The 'strConfigurationKeywords' for an ArcSDE instance are 
        ///      set up by the ArcSDE data administrator, the list of available keywords supported by a workspace 
        ///      may be obtained using the IWorkspaceConfiguration interface. For more information on configuration 
        ///      keywords, refer to the ArcSDE documentation. When not using an ArcSDE table use an empty 
        ///      string (ex: "").
        ///</remarks>
        internal ESRI.ArcGIS.Geodatabase.IFeatureClass CreateLineFeatureClass(ESRI.ArcGIS.Geodatabase.IWorkspace2 workspace, ESRI.ArcGIS.Geodatabase.IFeatureDataset featureDataset, System.String featureClassName, ESRI.ArcGIS.Geodatabase.IFields fields, ESRI.ArcGIS.esriSystem.UID CLSID, ESRI.ArcGIS.esriSystem.UID CLSEXT, System.String strConfigKeyword, OSMDomains osmDomains, string metadataAbstract, string metadataPurpose, List<String> additionalTagFields)
        {
            if (featureClassName == "") return null; // name was not passed in 

            ESRI.ArcGIS.Geodatabase.IFeatureClass featureClass = null;

            try
            {
                ESRI.ArcGIS.Geodatabase.IFeatureWorkspace featureWorkspace = (ESRI.ArcGIS.Geodatabase.IFeatureWorkspace)workspace; // Explicit Cast

                if (workspace.get_NameExists(ESRI.ArcGIS.Geodatabase.esriDatasetType.esriDTFeatureClass, featureClassName)) //feature class with that name already exists 
                {
                    // if a feature class with the same name already exists delete it....
                    featureClass = featureWorkspace.OpenFeatureClass(featureClassName);

                    if (!DeleteDataset((IDataset)featureClass))
                    {
                        return featureClass;
                    }
                }

                String illegalCharacters = String.Empty;

                ISQLSyntax sqlSyntax = workspace as ISQLSyntax;
                if (sqlSyntax != null)
                {
                    illegalCharacters = sqlSyntax.GetInvalidCharacters();
                }

                // assign the class id value if not assigned
                if (CLSID == null)
                {
                    CLSID = new ESRI.ArcGIS.esriSystem.UIDClass();
                    CLSID.Value = "esriGeoDatabase.Feature";
                }

                ESRI.ArcGIS.Geodatabase.IObjectClassDescription objectClassDescription = new ESRI.ArcGIS.Geodatabase.FeatureClassDescriptionClass();

                // if a fields collection is not passed in then supply our own
                if (fields == null)
                {
                    // create the fields using the required fields method
                    fields = objectClassDescription.RequiredFields;
                    ESRI.ArcGIS.Geodatabase.IFieldsEdit fieldsEdit = (ESRI.ArcGIS.Geodatabase.IFieldsEdit)fields; // Explicit Cast

                    // add the domain driven string field for the OSM features
                    foreach (var domainAttribute in osmDomains.domain)
                    {
                        IFieldEdit domainField = new FieldClass() as IFieldEdit;
                        domainField.Name_2 = domainAttribute.name;
                        domainField.Required_2 = true;
                        domainField.Type_2 = esriFieldType.esriFieldTypeString;
                        domainField.Length_2 = 30;
                        try
                        {
                            domainField.Domain_2 = ((IWorkspaceDomains)workspace).get_DomainByName(domainAttribute.name + "_ln");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine(ex.Message);
                            System.Diagnostics.Debug.WriteLine(ex.StackTrace);
                        }

                        fieldsEdit.AddField((IField)domainField);
                    }

                    // add the OSM ID field
                    IFieldEdit osmIDField = new FieldClass() as IFieldEdit;
                    osmIDField.Name_2 = "OSMID";
                    osmIDField.Required_2 = true;
                    osmIDField.Type_2 = esriFieldType.esriFieldTypeString;
                    osmIDField.Length_2 = 20;
                    fieldsEdit.AddField((IField)osmIDField);

                    // user, uid, visible, version, changeset, timestamp
                    IFieldEdit osmuserField = new FieldClass() as IFieldEdit;
                    osmuserField.Name_2 = "osmuser";
                    osmuserField.Required_2 = true;
                    osmuserField.Type_2 = esriFieldType.esriFieldTypeString;
                    osmuserField.Length_2 = 100;
                    fieldsEdit.AddField((IField)osmuserField);

                    IFieldEdit osmuidField = new FieldClass() as IFieldEdit;
                    osmuidField.Name_2 = "osmuid";
                    osmuidField.Required_2 = true;
                    osmuidField.Type_2 = esriFieldType.esriFieldTypeInteger;
                    fieldsEdit.AddField((IField)osmuidField);

                    IFieldEdit osmvisibleField = new FieldClass() as IFieldEdit;
                    osmvisibleField.Name_2 = "osmvisible";
                    osmvisibleField.Required_2 = true;
                    osmvisibleField.Type_2 = esriFieldType.esriFieldTypeString;
                    osmvisibleField.Length_2 = 20;
                    fieldsEdit.AddField((IField)osmvisibleField);

                    IFieldEdit osmversionField = new FieldClass() as IFieldEdit;
                    osmversionField.Name_2 = "osmversion";
                    osmversionField.Required_2 = true;
                    osmversionField.Type_2 = esriFieldType.esriFieldTypeSmallInteger;
                    fieldsEdit.AddField((IField)osmversionField);

                    IFieldEdit osmchangesetField = new FieldClass() as IFieldEdit;
                    osmchangesetField.Name_2 = "osmchangeset";
                    osmchangesetField.Required_2 = true;
                    osmchangesetField.Type_2 = esriFieldType.esriFieldTypeInteger;
                    fieldsEdit.AddField((IField)osmchangesetField);

                    IFieldEdit osmtimestampField = new FieldClass() as IFieldEdit;
                    osmtimestampField.Name_2 = "osmtimestamp";
                    osmtimestampField.Required_2 = true;
                    osmtimestampField.Type_2 = esriFieldType.esriFieldTypeDate;
                    fieldsEdit.AddField((IField)osmtimestampField);

                    IFieldEdit osmrelationIDField = new FieldClass() as IFieldEdit;
                    osmrelationIDField.Name_2 = "osmMemberOf";
                    osmrelationIDField.Required_2 = true;
                    //if (((IWorkspace)workspace).Type == esriWorkspaceType.esriLocalDatabaseWorkspace)
                    //{
                    osmrelationIDField.Type_2 = esriFieldType.esriFieldTypeBlob;
                    //}
                    //else
                    //{
                    //    osmrelationIDField.Type_2 = esriFieldType.esriFieldTypeXML;
                    //}
                    fieldsEdit.AddField((IField)osmrelationIDField);

                    //IFieldEdit osmrelationsField = new FieldClass() as IFieldEdit;
                    //osmrelationsField.Name_2 = "osmMembers";
                    //if (((IWorkspace)workspace).Type == esriWorkspaceType.esriLocalDatabaseWorkspace)
                    //{
                    //    osmrelationsField.Type_2 = esriFieldType.esriFieldTypeBlob;
                    //}
                    //else
                    //{
                    //    osmrelationsField.Type_2 = esriFieldType.esriFieldTypeXML;
                    //}
                    //fieldsEdit.AddField((IField)osmrelationsField);

                    // add the field for the tag cloud for all other tag/value pairs
                    IFieldEdit osmXmlTagsField = new FieldClass() as IFieldEdit;
                    osmXmlTagsField.Name_2 = "osmTags";
                    osmXmlTagsField.Required_2 = true;
                    //if (((IWorkspace)workspace).Type == esriWorkspaceType.esriLocalDatabaseWorkspace)
                    //{
                    osmXmlTagsField.Type_2 = esriFieldType.esriFieldTypeBlob;
                    //}
                    //else
                    //{
                    //    osmXmlTagsField.Type_2 = esriFieldType.esriFieldTypeXML;
                    //}
                    fieldsEdit.AddField((IField)osmXmlTagsField);

                    IFieldEdit osmSupportingElementField = new FieldClass() as IFieldEdit;
                    osmSupportingElementField.Name_2 = "osmSupportingElement";
                    osmSupportingElementField.Required_2 = true;
                    osmSupportingElementField.Type_2 = esriFieldType.esriFieldTypeString;
                    osmSupportingElementField.Length_2 = 5;
                    osmSupportingElementField.DefaultValue_2 = "no";
                    fieldsEdit.AddField((IField)osmSupportingElementField);

                    IFieldEdit osmMembersField = new FieldClass() as IFieldEdit;
                    osmMembersField.Name_2 = "osmMembers";
                    osmMembersField.Required_2 = true;
                    //if (((IWorkspace)workspace).Type == esriWorkspaceType.esriLocalDatabaseWorkspace)
                    //{
                    osmMembersField.Type_2 = esriFieldType.esriFieldTypeBlob;
                    //}
                    //else
                    //{
                    //    osmMembersField.Type_2 = esriFieldType.esriFieldTypeXML;
                    //}
                    fieldsEdit.AddField((IField)osmMembersField);

                    //IFieldEdit osmTrackChangesField = new FieldClass() as IFieldEdit;
                    //osmTrackChangesField.Name_2 = "osmTrackChanges";
                    //osmTrackChangesField.Required_2 = true;
                    //osmTrackChangesField.Type_2 = esriFieldType.esriFieldTypeSmallInteger;
                    //osmTrackChangesField.DefaultValue_2 = 0;
                    //fieldsEdit.AddField((IField)osmTrackChangesField);

                    foreach (string nameOfTag in additionalTagFields)
	                {
                        IFieldEdit osmTagAttributeField = new FieldClass() as IFieldEdit;
                        osmTagAttributeField.Name_2 = OSMToolHelper.convert2AttributeFieldName(nameOfTag, illegalCharacters);
                        osmTagAttributeField.AliasName_2 = nameOfTag + _resourceManager.GetString("GPTools_OSMGPAttributeSelector_aliasaddition");
                        osmTagAttributeField.Type_2 = esriFieldType.esriFieldTypeString;
                        osmTagAttributeField.Length_2 = 100;
                        osmTagAttributeField.Required_2 = false;

                        fieldsEdit.AddField((IField) osmTagAttributeField);
                    }

                    fields = (ESRI.ArcGIS.Geodatabase.IFields)fieldsEdit; // Explicit Cast
                }

                System.String strShapeField = "";

                // locate the shape field
                for (int j = 0; j < fields.FieldCount; j++)
                {
                    if (fields.get_Field(j).Type == ESRI.ArcGIS.Geodatabase.esriFieldType.esriFieldTypeGeometry)
                    {
                        strShapeField = fields.get_Field(j).Name;

                        // redefine geometry type

                        IFieldEdit shapeField = fields.get_Field(j) as IFieldEdit;
                        IGeometryDefEdit geometryDef = new GeometryDefClass() as IGeometryDefEdit;
                        geometryDef.GeometryType_2 = esriGeometryType.esriGeometryPolyline;
                        geometryDef.HasZ_2 = false;
                        geometryDef.HasM_2 = false;
                        geometryDef.GridCount_2 = 1;
                        geometryDef.set_GridSize(0, 0);

                        geometryDef.SpatialReference_2 = ((IGeoDataset)featureDataset).SpatialReference;

                        shapeField.GeometryDef_2 = (IGeometryDef)geometryDef;

                        break;
                    }
                }

                // Use IFieldChecker to create a validated fields collection.
                ESRI.ArcGIS.Geodatabase.IFieldChecker fieldChecker = new ESRI.ArcGIS.Geodatabase.FieldCheckerClass();
                ESRI.ArcGIS.Geodatabase.IEnumFieldError enumFieldError = null;
                ESRI.ArcGIS.Geodatabase.IFields validatedFields = null;
                fieldChecker.ValidateWorkspace = (ESRI.ArcGIS.Geodatabase.IWorkspace)workspace;
                fieldChecker.Validate(fields, out enumFieldError, out validatedFields);

                // The enumFieldError enumerator can be inspected at this point to determine 
                // which fields were modified during validation.


                // finally create and return the feature class
                if (featureDataset == null)// if no feature dataset passed in, create at the workspace level
                {
                    featureClass = featureWorkspace.CreateFeatureClass(featureClassName, validatedFields, CLSID, CLSEXT, ESRI.ArcGIS.Geodatabase.esriFeatureType.esriFTSimple, strShapeField, strConfigKeyword);
                    IPropertySet extensionPropertySet = new PropertySetClass();
                    extensionPropertySet.SetProperty("VERSION", OSMClassExtensionManager.Version);
                    ((IClassSchemaEdit2)featureClass).AlterClassExtensionProperties(extensionPropertySet);
                }
                else
                {
                    featureClass = featureDataset.CreateFeatureClass(featureClassName, validatedFields, CLSID, CLSEXT, ESRI.ArcGIS.Geodatabase.esriFeatureType.esriFTSimple, strShapeField, strConfigKeyword);
                    IPropertySet extensionPropertySet = new PropertySetClass();
                    extensionPropertySet.SetProperty("VERSION", OSMClassExtensionManager.Version);
                    ((IClassSchemaEdit2)featureClass).AlterClassExtensionProperties(extensionPropertySet);
                }

                // create the openstreetmap spcific metadata
                _osmUtility.CreateOSMMetadata((IDataset)featureClass, metadataAbstract, metadataPurpose);

                // the change at release 2.1 requires a new model name
                IModelInfo fcModelInfo = featureClass as IModelInfo;
                if (fcModelInfo != null)
                {
                    fcModelInfo.ModelName = OSMClassExtensionManager.OSMModelName;
                }
            }
            catch
            {
                throw;
            }

            return featureClass;
        }
        #endregion

        #region"Create OSM Polygon FeatureClass"
        internal ESRI.ArcGIS.Geodatabase.IFeatureClass CreatePolygonFeatureClass(ESRI.ArcGIS.Geodatabase.IWorkspace2 workspace, ESRI.ArcGIS.Geodatabase.IFeatureDataset featureDataset, System.String featureClassName, ESRI.ArcGIS.Geodatabase.IFields fields, ESRI.ArcGIS.esriSystem.UID CLSID, ESRI.ArcGIS.esriSystem.UID CLSEXT, System.String strConfigKeyword, OSMDomains osmDomains, string metadataAbstract, string metadataPurpose)
        {
            return CreatePolygonFeatureClass(workspace, featureDataset, featureClassName, fields, CLSID, CLSEXT, strConfigKeyword, osmDomains, metadataAbstract, metadataPurpose, null);
        }

        ///<summary>Simple helper to create a featureclass in a geodatabase.</summary>
        /// 
        ///<param name="workspace">An IWorkspace2 interface</param>
        ///<param name="featureDataset">An IFeatureDataset interface or Nothing</param>
        ///<param name="featureClassName">A System.String that contains the name of the feature class to open or create. Example: "states"</param>
        ///<param name="fields">An IFields interface</param>
        ///<param name="CLSID">A UID value or Nothing. Example "esriGeoDatabase.Feature" or Nothing</param>
        ///<param name="CLSEXT">A UID value or Nothing (this is the class extension if you want to reference a class extension when creating the feature class).</param>
        ///<param name="strConfigKeyword">An empty System.String or RDBMS table string for ArcSDE. Example: "myTable" or ""</param>
        ///  
        ///<returns>An IFeatureClass interface or a Nothing</returns>
        ///  
        ///<remarks>
        ///  (1) If a 'featureClassName' already exists in the workspace a reference to that feature class 
        ///      object will be returned.
        ///  (2) If an IFeatureDataset is passed in for the 'featureDataset' argument the feature class
        ///      will be created in the dataset. If a Nothing is passed in for the 'featureDataset'
        ///      argument the feature class will be created in the workspace.
        ///  (3) When creating a feature class in a dataset the spatial reference is inherited 
        ///      from the dataset object.
        ///  (4) If an IFields interface is supplied for the 'fields' collection it will be used to create the
        ///      table. If a Nothing value is supplied for the 'fields' collection, a table will be created using 
        ///      default values in the method.
        ///  (5) The 'strConfigurationKeyword' parameter allows the application to control the physical layout 
        ///      for this table in the underlying RDBMS—for example, in the case of an Oracle database, the 
        ///      configuration keyword controls the tablespace in which the table is created, the initial and 
        ///     next extents, and other properties. The 'strConfigurationKeywords' for an ArcSDE instance are 
        ///      set up by the ArcSDE data administrator, the list of available keywords supported by a workspace 
        ///      may be obtained using the IWorkspaceConfiguration interface. For more information on configuration 
        ///      keywords, refer to the ArcSDE documentation. When not using an ArcSDE table use an empty 
        ///      string (ex: "").
        ///</remarks>
        internal ESRI.ArcGIS.Geodatabase.IFeatureClass CreatePolygonFeatureClass(ESRI.ArcGIS.Geodatabase.IWorkspace2 workspace, ESRI.ArcGIS.Geodatabase.IFeatureDataset featureDataset, System.String featureClassName, ESRI.ArcGIS.Geodatabase.IFields fields, ESRI.ArcGIS.esriSystem.UID CLSID, ESRI.ArcGIS.esriSystem.UID CLSEXT, System.String strConfigKeyword, OSMDomains osmDomains, string metadataAbstract, string metadataPurpose, List<string> additionalTagFields)
        {
            if (featureClassName == "") return null; // name was not passed in 

            ESRI.ArcGIS.Geodatabase.IFeatureClass featureClass = null;

            try
            {

                ESRI.ArcGIS.Geodatabase.IFeatureWorkspace featureWorkspace = (ESRI.ArcGIS.Geodatabase.IFeatureWorkspace)workspace; // Explicit Cast

                if (workspace.get_NameExists(ESRI.ArcGIS.Geodatabase.esriDatasetType.esriDTFeatureClass, featureClassName)) //feature class with that name already exists 
                {
                    // if a feature class with the same name already exists delete it....
                    featureClass = featureWorkspace.OpenFeatureClass(featureClassName);

                    if (!DeleteDataset((IDataset)featureClass))
                    {
                        return featureClass;
                    }
                }


                String illegalCharacters = String.Empty;

                ISQLSyntax sqlSyntax = workspace as ISQLSyntax;
                if (sqlSyntax != null)
                {
                    illegalCharacters = sqlSyntax.GetInvalidCharacters();
                }

                // assign the class id value if not assigned
                if (CLSID == null)
                {
                    CLSID = new ESRI.ArcGIS.esriSystem.UIDClass();
                    CLSID.Value = "esriGeoDatabase.Feature";
                }

                ESRI.ArcGIS.Geodatabase.IObjectClassDescription objectClassDescription = new ESRI.ArcGIS.Geodatabase.FeatureClassDescriptionClass();

                // if a fields collection is not passed in then supply our own
                if (fields == null)
                {
                    // create the fields using the required fields method
                    fields = objectClassDescription.RequiredFields;
                    ESRI.ArcGIS.Geodatabase.IFieldsEdit fieldsEdit = (ESRI.ArcGIS.Geodatabase.IFieldsEdit)fields; // Explicit Cast

                    // add the domain driven string field for the OSM features
                    foreach (var domainAttribute in osmDomains.domain)
                    {
                        IFieldEdit domainField = new FieldClass() as IFieldEdit;
                        domainField.Name_2 = domainAttribute.name;
                        domainField.Type_2 = esriFieldType.esriFieldTypeString;
                        domainField.Required_2 = true;
                        domainField.Length_2 = 30;
                        try
                        {
                            domainField.Domain_2 = ((IWorkspaceDomains)workspace).get_DomainByName(domainAttribute.name + "_ply");
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine(ex.Message);
                            System.Diagnostics.Debug.WriteLine(ex.StackTrace);
                        }

                        fieldsEdit.AddField((IField)domainField);
                    }

                    // add the OSM ID field
                    IFieldEdit osmIDField = new FieldClass() as IFieldEdit;
                    osmIDField.Name_2 = "OSMID";
                    osmIDField.Type_2 = esriFieldType.esriFieldTypeString;
                    osmIDField.Length_2 = 20;
                    osmIDField.Required_2 = true;
                    fieldsEdit.AddField((IField)osmIDField);

                    // add the field for the tag cloud for all other tag/value pairs
                    IFieldEdit osmXmlTagsField = new FieldClass() as IFieldEdit;
                    osmXmlTagsField.Name_2 = "osmTags";
                    osmXmlTagsField.Required_2 = true;
                    //if (((IWorkspace)workspace).Type == esriWorkspaceType.esriLocalDatabaseWorkspace)
                    //{
                    osmXmlTagsField.Type_2 = esriFieldType.esriFieldTypeBlob;
                    //}
                    //else
                    //{
                    //    osmXmlTagsField.Type_2 = esriFieldType.esriFieldTypeXML;
                    //}
                    fieldsEdit.AddField((IField)osmXmlTagsField);

                    // user, uid, visible, version, changeset, timestamp
                    IFieldEdit osmuserField = new FieldClass() as IFieldEdit;
                    osmuserField.Name_2 = "osmuser";
                    osmuserField.Type_2 = esriFieldType.esriFieldTypeString;
                    osmuserField.Required_2 = true;
                    osmuserField.Length_2 = 100;
                    fieldsEdit.AddField((IField)osmuserField);

                    IFieldEdit osmuidField = new FieldClass() as IFieldEdit;
                    osmuidField.Name_2 = "osmuid";
                    osmuidField.Required_2 = true;
                    osmuidField.Type_2 = esriFieldType.esriFieldTypeInteger;
                    fieldsEdit.AddField((IField)osmuidField);

                    IFieldEdit osmvisibleField = new FieldClass() as IFieldEdit;
                    osmvisibleField.Name_2 = "osmvisible";
                    osmvisibleField.Required_2 = true;
                    osmvisibleField.Type_2 = esriFieldType.esriFieldTypeString;
                    osmvisibleField.Length_2 = 20;
                    fieldsEdit.AddField((IField)osmvisibleField);

                    IFieldEdit osmversionField = new FieldClass() as IFieldEdit;
                    osmversionField.Name_2 = "osmversion";
                    osmversionField.Required_2 = true;
                    osmversionField.Type_2 = esriFieldType.esriFieldTypeSmallInteger;
                    fieldsEdit.AddField((IField)osmversionField);

                    IFieldEdit osmchangesetField = new FieldClass() as IFieldEdit;
                    osmchangesetField.Name_2 = "osmchangeset";
                    osmchangesetField.Required_2 = true;
                    osmchangesetField.Type_2 = esriFieldType.esriFieldTypeInteger;
                    fieldsEdit.AddField((IField)osmchangesetField);

                    IFieldEdit osmtimestampField = new FieldClass() as IFieldEdit;
                    osmtimestampField.Name_2 = "osmtimestamp";
                    osmtimestampField.Required_2 = true;
                    osmtimestampField.Type_2 = esriFieldType.esriFieldTypeDate;
                    fieldsEdit.AddField((IField)osmtimestampField);

                    IFieldEdit osmrelationIDField = new FieldClass() as IFieldEdit;
                    osmrelationIDField.Name_2 = "osmMemberOf";
                    osmrelationIDField.Required_2 = true;
                    //if (((IWorkspace)workspace).Type == esriWorkspaceType.esriLocalDatabaseWorkspace)
                    //{
                    osmrelationIDField.Type_2 = esriFieldType.esriFieldTypeBlob;
                    //}
                    //else
                    //{
                    //    osmrelationIDField.Type_2 = esriFieldType.esriFieldTypeXML;
                    //}
                    fieldsEdit.AddField((IField)osmrelationIDField);

                    //IFieldEdit osmrelationsField = new FieldClass() as IFieldEdit;
                    //osmrelationsField.Name_2 = "osmMembers";
                    //if (((IWorkspace)workspace).Type == esriWorkspaceType.esriLocalDatabaseWorkspace)
                    //{
                    //    osmrelationsField.Type_2 = esriFieldType.esriFieldTypeBlob;
                    //}
                    //else
                    //{
                    //    osmrelationsField.Type_2 = esriFieldType.esriFieldTypeXML;
                    //}
                    //fieldsEdit.AddField((IField)osmrelationsField);
                    IFieldEdit osmSupportingElementField = new FieldClass() as IFieldEdit;
                    osmSupportingElementField.Name_2 = "osmSupportingElement";
                    osmSupportingElementField.Type_2 = esriFieldType.esriFieldTypeString;
                    osmSupportingElementField.Length_2 = 5;
                    osmSupportingElementField.DefaultValue_2 = "no";
                    osmSupportingElementField.Required_2 = true;
                    fieldsEdit.AddField((IField)osmSupportingElementField);

                    IFieldEdit osmMembersField = new FieldClass() as IFieldEdit;
                    osmMembersField.Name_2 = "osmMembers";
                    //if (((IWorkspace)workspace).Type == esriWorkspaceType.esriLocalDatabaseWorkspace)
                    //{
                    osmMembersField.Type_2 = esriFieldType.esriFieldTypeBlob;
                    osmMembersField.Required_2 = true;
                    //}
                    //else
                    //{
                    //    osmMembersField.Type_2 = esriFieldType.esriFieldTypeXML;
                    //}
                    fieldsEdit.AddField((IField)osmMembersField);

                    //IFieldEdit osmTrackChangesField = new FieldClass() as IFieldEdit;
                    //osmTrackChangesField.Name_2 = "osmTrackChanges";
                    //osmTrackChangesField.Required_2 = true;
                    //osmTrackChangesField.Type_2 = esriFieldType.esriFieldTypeSmallInteger;
                    //osmTrackChangesField.DefaultValue_2 = 0;
                    //fieldsEdit.AddField((IField)osmTrackChangesField);

                    foreach (string nameOfTag in additionalTagFields)
	                {
                        IFieldEdit osmTagAttributeField = new FieldClass() as IFieldEdit;
                        osmTagAttributeField.Name_2 = OSMToolHelper.convert2AttributeFieldName(nameOfTag, illegalCharacters);
                        osmTagAttributeField.AliasName_2 = nameOfTag + _resourceManager.GetString("GPTools_OSMGPAttributeSelector_aliasaddition");
                        osmTagAttributeField.Type_2 = esriFieldType.esriFieldTypeString;
                        osmTagAttributeField.Length_2 = 100;
                        osmTagAttributeField.Required_2 = false;

                        fieldsEdit.AddField((IField) osmTagAttributeField);
                    }

                    fields = (ESRI.ArcGIS.Geodatabase.IFields)fieldsEdit; // Explicit Cast
                }

                System.String strShapeField = "";

                // locate the shape field
                for (int j = 0; j < fields.FieldCount; j++)
                {
                    if (fields.get_Field(j).Type == ESRI.ArcGIS.Geodatabase.esriFieldType.esriFieldTypeGeometry)
                    {
                        strShapeField = fields.get_Field(j).Name;

                        // redefine geometry type

                        IFieldEdit shapeField = fields.get_Field(j) as IFieldEdit;
                        IGeometryDefEdit geometryDef = new GeometryDefClass() as IGeometryDefEdit;
                        geometryDef.GeometryType_2 = esriGeometryType.esriGeometryPolygon;
                        geometryDef.HasZ_2 = false;
                        geometryDef.HasM_2 = false;
                        geometryDef.GridCount_2 = 1;
                        geometryDef.set_GridSize(0, 0);

                        geometryDef.SpatialReference_2 = ((IGeoDataset)featureDataset).SpatialReference;

                        shapeField.GeometryDef_2 = (IGeometryDef)geometryDef;

                        break;
                    }
                }

                // Use IFieldChecker to create a validated fields collection.
                ESRI.ArcGIS.Geodatabase.IFieldChecker fieldChecker = new ESRI.ArcGIS.Geodatabase.FieldCheckerClass();
                ESRI.ArcGIS.Geodatabase.IEnumFieldError enumFieldError = null;
                ESRI.ArcGIS.Geodatabase.IFields validatedFields = null;
                fieldChecker.ValidateWorkspace = (ESRI.ArcGIS.Geodatabase.IWorkspace)workspace;
                fieldChecker.Validate(fields, out enumFieldError, out validatedFields);

                // The enumFieldError enumerator can be inspected at this point to determine 
                // which fields were modified during validation.


                // finally create and return the feature class
                if (featureDataset == null)// if no feature dataset passed in, create at the workspace level
                {
                    featureClass = featureWorkspace.CreateFeatureClass(featureClassName, validatedFields, CLSID, CLSEXT, ESRI.ArcGIS.Geodatabase.esriFeatureType.esriFTSimple, strShapeField, strConfigKeyword);
                    IPropertySet extensionPropertySet = new PropertySetClass();
                    extensionPropertySet.SetProperty("VERSION", OSMClassExtensionManager.Version);
                    ((IClassSchemaEdit2)featureClass).AlterClassExtensionProperties(extensionPropertySet);

                }
                else
                {
                    featureClass = featureDataset.CreateFeatureClass(featureClassName, validatedFields, CLSID, CLSEXT, ESRI.ArcGIS.Geodatabase.esriFeatureType.esriFTSimple, strShapeField, strConfigKeyword);
                    IPropertySet extensionPropertySet = new PropertySetClass();
                    extensionPropertySet.SetProperty("VERSION", OSMClassExtensionManager.Version);
                    ((IClassSchemaEdit2)featureClass).AlterClassExtensionProperties(extensionPropertySet);
                }

                // create the openstreetmap spcific metadata
                _osmUtility.CreateOSMMetadata((IDataset)featureClass, metadataAbstract, metadataPurpose);

                // the change at release 2.1 requires a new model name
                IModelInfo fcModelInfo = featureClass as IModelInfo;
                if (fcModelInfo != null)
                {
                    fcModelInfo.ModelName = OSMClassExtensionManager.OSMModelName;
                }


            }
            catch
            {
                throw;
            }

            return featureClass;
        }
        #endregion

        #region Utility Methods
        /// <summary>
        /// Deletes the dataset if possible
        /// </summary>
        /// <remarks>
        /// - Checks for a shcema lock before deleting (CanDelete returns true even if the dataset is locked)
        /// - Returns true if the dataset was deleted, else false
        /// </remarks>
        private static bool DeleteDataset(IDataset ds)
        {
            ISchemaLock schemaLock = ds as ISchemaLock;

            if (ds.CanDelete() && (schemaLock != null))
            {
                try
                {
                    schemaLock.ChangeSchemaLock(esriSchemaLock.esriExclusiveSchemaLock);
                    ds.Delete();
                    return true;
                }
                catch
                {
                    schemaLock.ChangeSchemaLock(esriSchemaLock.esriSharedSchemaLock);
                }
            }

            return false;
        }

        #endregion

        private void UpdateSpatialGridIndex(ESRI.ArcGIS.esriSystem.ITrackCancel TrackCancel, ESRI.ArcGIS.Geodatabase.IGPMessages message, IGeoProcessor2 geoProcessor, string inputFeatureClass)
        {
            IVariantArray parameterArrary = null;
            IGeoProcessorResult2 gpResults2 = null;

            parameterArrary = CreateDefaultGridParamterArrary(inputFeatureClass);
            gpResults2 = geoProcessor.Execute("CalculateDefaultGridIndex_management", parameterArrary, TrackCancel) as IGeoProcessorResult2;

            List<double> gridIndexList = new List<double>(3);

            for (int index = 0; index < gpResults2.OutputCount; index++)
            {
                string gridIndexString = gpResults2.GetOutput(index).GetAsText();

                double gridIndexValue = 0;

                Double.TryParse(gridIndexString, out gridIndexValue);

                gridIndexList.Add(gridIndexValue);
            }

            // delete the current spatial index if it does exist 
            // this is expected to fail if no such index exists
            try
            {
                gpResults2 = geoProcessor.Execute("RemoveSpatialIndex_management", parameterArrary, TrackCancel) as IGeoProcessorResult2;
            }
            catch (Exception ex)
            {
                message.AddWarning(ex.Message);
                if (gpResults2 != null)
                {
                    message.AddMessages(gpResults2.GetResultMessages());
                }
            }

            parameterArrary = CreateAddGridIndexParameterArray(inputFeatureClass, gridIndexList[0], gridIndexList[1], gridIndexList[2]);
            gpResults2 = geoProcessor.Execute("AddSpatialIndex_management", parameterArrary, TrackCancel) as IGeoProcessorResult2;
        }

        private IVariantArray CreateDefaultGridParamterArrary(IGPValue featureClassName)
        {
            IVariantArray featureClassArray = new VarArrayClass();

            featureClassArray.Add(featureClassName);

            return featureClassArray;
        }

        private IVariantArray CreateDefaultGridParamterArrary(string featureClassName)
        {
            IVariantArray featureClassArray = new VarArrayClass();

            featureClassArray.Add(featureClassName);

            return featureClassArray;
        }

        private IVariantArray CreateAddGridIndexParameterArray(string featureClassName, double gridIndex1, double gridIndex2, double gridIndex3)
        {
            IVariantArray featureClassArray = new VarArrayClass();

            // the feature class information as the first parameter
            featureClassArray.Add(featureClassName);

            // add the grid indices as additional parameters
            featureClassArray.Add(gridIndex1);
            featureClassArray.Add(gridIndex2);
            featureClassArray.Add(gridIndex3);

            return featureClassArray;
        }

        internal IVariantArray CreateAddIndexParameterArray(string featureClassName, string fieldsToIndex, string IndexName, string unique, string sortingOrder)
        {
            IVariantArray parameterArrary = new VarArrayClass();
            // add first the name of the feature class
            parameterArrary.Add(featureClassName);
            // second parameter is the list of field to be indexed
            parameterArrary.Add(fieldsToIndex);
            // the third parameter is the name of index
            parameterArrary.Add(IndexName);
            // the fourth parameter is the (optional) indicator of the index contains unique values
            if (!String.IsNullOrEmpty(unique))
            {
                parameterArrary.Add(unique);
            }
            // the fifth parameter is the (optional) sorting order of the index
            if (!String.IsNullOrEmpty(sortingOrder))
            {
                parameterArrary.Add(sortingOrder);
            }

            return parameterArrary;
        }

        internal void loadOSMNodes(string osmFileLocation, ref ITrackCancel TrackCancel, ref IGPMessages message,IGPValue targetGPValue,  IFeatureClass osmPointFeatureClass, bool conserveMemory, bool fastLoad, int nodeCapacity, ref Dictionary<string, simplePointRef> osmNodeDictionary, IFeatureWorkspace featureWorkspace, ISpatialReference downloadSpatialReference, OSMDomains availableDomains, bool checkForExisting)
        {
            XmlReader osmFileXmlReader = null;
            XmlSerializer nodeSerializer = null;

            try
            {
                osmFileXmlReader = System.Xml.XmlReader.Create(osmFileLocation);
                nodeSerializer = new XmlSerializer(typeof(node));

                ISpatialReferenceFactory spatialRef = new SpatialReferenceEnvironmentClass();
                ISpatialReference wgs84 = spatialRef.CreateGeographicCoordinateSystem((int)esriSRGeoCSType.esriSRGeoCS_WGS1984);

                bool shouldProject = !((IClone)wgs84).IsEqual((IClone)downloadSpatialReference);

                int osmPointIDFieldIndex = osmPointFeatureClass.FindField("OSMID");
                Dictionary<string, int> osmPointDomainAttributeFieldIndices = new Dictionary<string, int>();
                Dictionary<string, int> osmPointDomainAttributeFieldLength = new Dictionary<string, int>();

                foreach (var domains in availableDomains.domain)
                {
                    int currentFieldIndex = osmPointFeatureClass.FindField(domains.name);

                    if (currentFieldIndex != -1)
                    {
                        osmPointDomainAttributeFieldIndices.Add(domains.name, currentFieldIndex);
                        osmPointDomainAttributeFieldLength.Add(domains.name, osmPointFeatureClass.Fields.get_Field(currentFieldIndex).Length);
                    }
                }

                int tagCollectionPointFieldIndex = osmPointFeatureClass.FindField("osmTags");
                int osmUserPointFieldIndex = osmPointFeatureClass.FindField("osmuser");
                int osmUIDPointFieldIndex = osmPointFeatureClass.FindField("osmuid");
                int osmVisiblePointFieldIndex = osmPointFeatureClass.FindField("osmvisible");
                int osmVersionPointFieldIndex = osmPointFeatureClass.FindField("osmversion");
                int osmChangesetPointFieldIndex = osmPointFeatureClass.FindField("osmchangeset");
                int osmTimeStampPointFieldIndex = osmPointFeatureClass.FindField("osmtimestamp");
                int osmMemberOfPointFieldIndex = osmPointFeatureClass.FindField("osmMemberOf");
                int osmSupportingElementPointFieldIndex = osmPointFeatureClass.FindField("osmSupportingElement");
                int osmWayRefCountFieldIndex = osmPointFeatureClass.FindField("wayRefCount");


                // set up the progress indicator
                IStepProgressor stepProgressor = TrackCancel as IStepProgressor;

                if (stepProgressor != null)
                {
                    stepProgressor.MinRange = 0;
                    stepProgressor.MaxRange = nodeCapacity;
                    stepProgressor.StepValue = (1);
                    stepProgressor.Message = _resourceManager.GetString("GPTools_OSMGPFileReader_loadingNodes");
                    stepProgressor.Position = 0;
                    stepProgressor.Show();
                }

                // flag to determine if a computation of indices is required
                bool indexBuildRequired = false;
                if (nodeCapacity > 0)
                    indexBuildRequired = true;

                int pointCount = 0;


                // let's insert all the points first
                if (osmPointFeatureClass != null)
                {
                    IPoint pointGeometry = null;
                    IFeatureBuffer pointFeature = null;
                    IFeatureClassLoad pointFeatureLoad = null;

                    using (ComReleaser comReleaser = new ComReleaser())
                    {
                        using (SchemaLockManager schemaLockManager = new SchemaLockManager(osmPointFeatureClass as ITable))
                        {

                            if (((IWorkspace)featureWorkspace).WorkspaceFactory.WorkspaceType == esriWorkspaceType.esriRemoteDatabaseWorkspace)
                            {
                                pointFeatureLoad = osmPointFeatureClass as IFeatureClassLoad;
                            }

                            IFeatureCursor pointInsertCursor = osmPointFeatureClass.Insert(true);
                            comReleaser.ManageLifetime(pointInsertCursor);

                            if (pointFeatureLoad != null)
                            {
                                pointFeatureLoad.LoadOnlyMode = true;
                            }

                            osmFileXmlReader.MoveToContent();

                            while (osmFileXmlReader.Read())
                            {
                                if (osmFileXmlReader.IsStartElement())
                                {
                                    if (osmFileXmlReader.Name == "node")
                                    {
                                        string currentNodeString = osmFileXmlReader.ReadOuterXml();
                                        // turn the xml node representation into a node class representation
                                        ESRI.ArcGIS.OSM.OSMClassExtension.node currentNode = null;
                                        using (StringReader nodeReader = new System.IO.StringReader(currentNodeString))
                                        {
                                            currentNode = nodeSerializer.Deserialize(nodeReader) as ESRI.ArcGIS.OSM.OSMClassExtension.node;
                                        }

                                        // check if a feature with the same OSMID already exists, because the can only be one
                                        if (checkForExisting == true)
                                        {
                                            if (CheckIfExists(osmPointFeatureClass as ITable, currentNode.id))
                                            {
                                                continue;
                                            }
                                        }

                                        try
                                        {
                                            pointFeature = osmPointFeatureClass.CreateFeatureBuffer();

                                            pointGeometry = new PointClass();
                                            pointGeometry.X = Convert.ToDouble(currentNode.lon, new CultureInfo("en-US"));
                                            pointGeometry.Y = Convert.ToDouble(currentNode.lat, new CultureInfo("en-US"));
                                            pointGeometry.SpatialReference = wgs84;

                                            if (shouldProject)
                                            {
                                                pointGeometry.Project(downloadSpatialReference);
                                            }

                                            pointFeature.Shape = pointGeometry;

                                            pointFeature.set_Value(osmPointIDFieldIndex, currentNode.id);

                                            string isSupportingNode = "";
                                            if (_osmUtility.DoesHaveKeys(currentNode))
                                            {
                                                // if case it has tags I assume that the node presents an entity of it own,
                                                // hence it is not a supporting node in the context of supporting a way or relation
                                                isSupportingNode = "no";

                                                if (conserveMemory == false)
                                                {
                                                    osmNodeDictionary[currentNode.id] = new simplePointRef(Convert.ToSingle(currentNode.lon, new CultureInfo("en-US")), Convert.ToSingle(currentNode.lat, new CultureInfo("en-US")), 0, 0);
                                                }
                                            }
                                            else
                                            {
                                                // node has no tags -- at this point I assume that the absence of tags indicates that it is a supporting node
                                                // for a way or a relation
                                                isSupportingNode = "yes";

                                                if (conserveMemory == false)
                                                {
                                                    osmNodeDictionary[currentNode.id] = new simplePointRef(Convert.ToSingle(currentNode.lon, new CultureInfo("en-US")), Convert.ToSingle(currentNode.lat, new CultureInfo("en-US")), 0, 0);
                                                }
                                            }

                                            insertTags(osmPointDomainAttributeFieldIndices, osmPointDomainAttributeFieldLength, tagCollectionPointFieldIndex, pointFeature, currentNode.tag);

                                            if (fastLoad == false)
                                            {
                                                if (osmSupportingElementPointFieldIndex > -1)
                                                {
                                                    pointFeature.set_Value(osmSupportingElementPointFieldIndex, isSupportingNode);
                                                }

                                                if (osmWayRefCountFieldIndex > -1)
                                                {
                                                    pointFeature.set_Value(osmWayRefCountFieldIndex, 0);
                                                }

                                                // store the administrative attributes
                                                // user, uid, version, changeset, timestamp, visible
                                                if (osmUserPointFieldIndex > -1)
                                                {
                                                    if (!String.IsNullOrEmpty(currentNode.user))
                                                    {
                                                        pointFeature.set_Value(osmUserPointFieldIndex, currentNode.user);
                                                    }
                                                }

                                                if (osmUIDPointFieldIndex > -1)
                                                {
                                                    if (!String.IsNullOrEmpty(currentNode.uid))
                                                    {
                                                        pointFeature.set_Value(osmUIDPointFieldIndex, Convert.ToInt32(currentNode.uid));
                                                    }
                                                }

                                                if (osmVisiblePointFieldIndex > -1)
                                                {
                                                    pointFeature.set_Value(osmVisiblePointFieldIndex, currentNode.visible.ToString());
                                                }

                                                if (osmVersionPointFieldIndex > -1)
                                                {
                                                    if (!String.IsNullOrEmpty(currentNode.version))
                                                    {
                                                        pointFeature.set_Value(osmVersionPointFieldIndex, Convert.ToInt32(currentNode.version));
                                                    }
                                                }

                                                if (osmChangesetPointFieldIndex > -1)
                                                {
                                                    if (!String.IsNullOrEmpty(currentNode.changeset))
                                                    {
                                                        pointFeature.set_Value(osmChangesetPointFieldIndex, Convert.ToInt32(currentNode.changeset));
                                                    }
                                                }

                                                if (osmTimeStampPointFieldIndex > -1)
                                                {
                                                    if (!String.IsNullOrEmpty(currentNode.timestamp))
                                                    {
                                                        try
                                                        {
                                                            pointFeature.set_Value(osmTimeStampPointFieldIndex, Convert.ToDateTime(currentNode.timestamp));
                                                        }
                                                        catch (Exception ex)
                                                        {
                                                            message.AddWarning(String.Format(_resourceManager.GetString("GPTools_OSMGPFileReader_invalidTimeFormat"), ex.Message));
                                                        }
                                                    }
                                                }
                                            }
                                            try
                                            {
                                                pointInsertCursor.InsertFeature(pointFeature);
                                                pointCount = pointCount + 1;

                                                if (stepProgressor != null)
                                                {
                                                    stepProgressor.Position = pointCount;
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                System.Diagnostics.Debug.WriteLine(ex.Message);
                                                message.AddWarning(ex.Message);
                                            }


                                            if (TrackCancel.Continue() == false)
                                            {
                                                return;
                                            }

                                            if ((pointCount % 50000) == 0)
                                            {
                                                message.AddMessage(String.Format(_resourceManager.GetString("GPTools_OSMGPFileReader_pointsloaded"), pointCount));
                                                pointInsertCursor.Flush();
                                                System.GC.Collect();
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            System.Diagnostics.Debug.WriteLine(ex.Message);
                                            message.AddWarning(ex.Message);
                                        }
                                        finally
                                        {
                                            if (pointFeature != null)
                                            {
                                                Marshal.FinalReleaseComObject(pointFeature);
                                                pointFeature = null;
                                            }

                                            if (pointGeometry != null)
                                            {
                                                Marshal.FinalReleaseComObject(pointGeometry);
                                                pointGeometry = null;
                                            }
                                        }

                                        currentNode = null;
                                    }
                                }
                            }

                            if (stepProgressor != null)
                            {
                                stepProgressor.Hide();
                            }

                            pointInsertCursor.Flush();
                            osmFileXmlReader.Close();

                            message.AddMessage(String.Format(_resourceManager.GetString("GPTools_OSMGPFileReader_pointsloaded"), pointCount));
                            message.AddMessage(_resourceManager.GetString("GPTools_buildingpointidx"));

                            if (pointFeatureLoad != null)
                            {
                                pointFeatureLoad.LoadOnlyMode = false;
                            }
                        }
                    }

                    if (TrackCancel.Continue() == false)
                    {
                        return;
                    }

                    using (ComReleaser comReleaser = new ComReleaser())
                    {
                        IFeatureCursor updatePoints = osmPointFeatureClass.Update(null, false);
                        comReleaser.ManageLifetime(updatePoints);

                        IFeature feature2Update = updatePoints.NextFeature();

                        while (feature2Update != null)
                        {
                            pointGeometry = feature2Update.Shape as IPoint;
                            pointGeometry.ID = feature2Update.OID;
                            feature2Update.Shape = pointGeometry;

                            if (conserveMemory == false)
                            {
                                string osmid = Convert.ToString(feature2Update.get_Value(osmPointIDFieldIndex));
                                if (osmNodeDictionary.ContainsKey(osmid))
                                    osmNodeDictionary[osmid].pointObjectID = feature2Update.OID;
                            }

                            updatePoints.UpdateFeature(feature2Update);

                            if (TrackCancel.Continue() == false)
                            {
                                return;
                            }

                            if (feature2Update != null)
                                Marshal.ReleaseComObject(feature2Update);

                            if (pointGeometry != null)
                                Marshal.ReleaseComObject(pointGeometry);

                            feature2Update = updatePoints.NextFeature();
                        }
                    }

                    if (indexBuildRequired)
                    {

                        IGeoProcessor2 geoProcessor = new GeoProcessorClass();
                        bool storedOriginal = geoProcessor.AddOutputsToMap;

                        try
                        {
                            IGPUtilities3 gpUtilities3 = new GPUtilitiesClass();

                            IGPValue pointFeatureClass = gpUtilities3.MakeGPValueFromObject(osmPointFeatureClass);

                            string fcLocation = GetLocationString(targetGPValue, osmPointFeatureClass);

                            IIndexes featureClassIndexes = osmPointFeatureClass.Indexes;
                            int indexPosition = -1;
                            featureClassIndexes.FindIndex("osmID_IDX", out indexPosition);

                            if (indexPosition == -1)
                            {
                                {
                                    geoProcessor.AddOutputsToMap = false;

                                    IVariantArray parameterArrary = CreateAddIndexParameterArray(fcLocation, "OSMID", "osmID_IDX", "UNIQUE", "");
                                    IGeoProcessorResult2 gpResults2 = geoProcessor.Execute("AddIndex_management", parameterArrary, TrackCancel) as IGeoProcessorResult2;
                                }
                            }
                            if (pointCount > 500)
                            {
                                if (pointFeatureLoad == null)
                                {
                                    UpdateSpatialGridIndex(TrackCancel, message, geoProcessor, fcLocation);
                                }
                            }
                        }
                        catch (COMException comEx)
                        {
                            message.AddWarning(comEx.Message);
                        }
                        catch (Exception ex)
                        {
                            message.AddWarning(ex.Message);
                        }
                        finally
                        {
                            geoProcessor.AddOutputsToMap = storedOriginal;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                message.AddError(120100, String.Format(_resourceManager.GetString("GPTools_Utility_NodeLoadError"), ex.Message));
            }
            finally
            {
                if (osmFileXmlReader != null)
                    osmFileXmlReader = null;

                if (nodeSerializer != null)
                    nodeSerializer = null;
            }
        }

        private void insertTags(Dictionary<string, int> domainAttributeFieldIndices, Dictionary<string, int> domainAttributeFieldLength, int tagCollectionFieldIndex, IRowBuffer row, tag[] tagsToInsert)
        {
            Dictionary<string, object> tagGdbStorageValues = new Dictionary<string, object>(domainAttributeFieldIndices.Count);
            foreach (var item in domainAttributeFieldIndices)
            {
                tagGdbStorageValues[item.Key] = null;
            }

            if (tagsToInsert != null)
            {
                foreach (tag tagItem in tagsToInsert)
                {
                    if (domainAttributeFieldIndices.ContainsKey(tagItem.k))
                    {
                        if (tagItem.v.Length <= domainAttributeFieldLength[tagItem.k])
                        {
                            tagGdbStorageValues[tagItem.k] = tagItem.v;
                        }
                    }
                }
            }

            foreach (var item in domainAttributeFieldIndices)
            {
                row.set_Value(item.Value, tagGdbStorageValues[item.Key]);
            }


            if (tagCollectionFieldIndex > -1)
            {
                _osmUtility.insertOSMTags(tagCollectionFieldIndex, row, tagsToInsert);
            }

            tagGdbStorageValues.Clear();
            tagGdbStorageValues = null;
        }

        private string GetLocationString(IGPValue gpValue, ITable table)
        {
            string locationString = String.Empty;

            {
                if (((IDataset)table).Workspace.PathName.ToUpper().Contains(".GDS\\"))
                {
                    string partialString = gpValue.GetAsText().Substring(0, gpValue.GetAsText().ToUpper().IndexOf(".GDS\\") + 4);

                    string secondString = gpValue.GetAsText().Substring(0, gpValue.GetAsText().LastIndexOf("\\"));

                    locationString = secondString + System.IO.Path.DirectorySeparatorChar + ((IDataset)table).BrowseName;
                }
                else
                {
                    locationString = ((IDataset)table).Workspace.PathName + System.IO.Path.DirectorySeparatorChar + ((IDataset)table).BrowseName;
                }
            }

            return locationString;
        }

        private string GetLocationString(IGPValue gpValue, IFeatureClass featureClass)
        {
            string locationString = String.Empty;

            if (featureClass.FeatureDataset != null)
            {
                if (featureClass.FeatureDataset.Workspace.PathName.ToUpper().Contains(".GDS\\"))
                {
                    locationString = gpValue.GetAsText() + System.IO.Path.DirectorySeparatorChar + ((IDataset)featureClass).BrowseName;
                }
                else
                    locationString = featureClass.FeatureDataset.Workspace.PathName + System.IO.Path.DirectorySeparatorChar + featureClass.FeatureDataset.BrowseName + System.IO.Path.DirectorySeparatorChar + ((IDataset)featureClass).BrowseName;
            }
            else
            {
                locationString = ((IDataset)featureClass).Workspace.PathName + System.IO.Path.DirectorySeparatorChar + ((IDataset)featureClass).BrowseName;
            }

            return locationString;
        }

        private bool CheckIfExists(ITable searchTable, string osmID)
        {
            bool featureAlreadyExists = true;

            try
            {
                string sqlIdentifier = searchTable.SqlIdentifier(osmID);
                using (ComReleaser comReleaser = new ComReleaser())
                {

                IQueryFilter queryFilter = new QueryFilterClass();
                queryFilter.WhereClause = sqlIdentifier + " = '" + osmID + "'";

                    ICursor rowCursor = searchTable.Search(queryFilter, false);
                    comReleaser.ManageLifetime(rowCursor);

                    IRow foundRow = rowCursor.NextRow();

                    if (foundRow == null)
                    {
                        featureAlreadyExists = false;
                    }

                    Marshal.ReleaseComObject(foundRow);
                }
            }
            catch { }

            return featureAlreadyExists;

        }

        internal List<string> loadOSMWays(string osmFileLocation, ref ITrackCancel TrackCancel, ref IGPMessages message, IGPValue targetGPValue, IFeatureClass osmPointFeatureClass, IFeatureClass osmLineFeatureClass, IFeatureClass osmPolygonFeatureClass, bool conserveMemory, bool fastLoad, int wayCapacity, ref Dictionary<string, simplePointRef> osmNodeDictionary, IFeatureWorkspace featureWorkspace, ISpatialReference downloadSpatialReference, OSMDomains availableDomains, bool checkForExisting)
        {
            if (osmLineFeatureClass == null)
            {
                throw new ArgumentNullException("osmLineFeatureClass");
            }

            if (osmPolygonFeatureClass == null)
            {
                throw new ArgumentNullException("osmPolygonFeatureClass");
            }

            XmlReader osmFileXmlReader = null;
            XmlSerializer waySerializer = null;
            List<string> missingWays = null;

            try
            {
                missingWays = new List<string>();

                int osmPointIDFieldIndex = osmPointFeatureClass.FindField("OSMID");
                int osmWayRefCountFieldIndex = osmPointFeatureClass.FindField("wayRefCount");

                int osmLineIDFieldIndex = osmLineFeatureClass.FindField("OSMID");
                Dictionary<string, int> osmLineDomainAttributeFieldIndices = new Dictionary<string, int>();
                Dictionary<string, int> osmLineDomainAttributeFieldLength = new Dictionary<string, int>();
                foreach (var domains in availableDomains.domain)
                {
                    int currentFieldIndex = osmLineFeatureClass.FindField(domains.name);

                    if (currentFieldIndex != -1)
                    {
                        osmLineDomainAttributeFieldIndices.Add(domains.name, currentFieldIndex);
                        osmLineDomainAttributeFieldLength.Add(domains.name, osmLineFeatureClass.Fields.get_Field(currentFieldIndex).Length);
                    }
                }
                int tagCollectionPolylineFieldIndex = osmLineFeatureClass.FindField("osmTags");
                int osmUserPolylineFieldIndex = osmLineFeatureClass.FindField("osmuser");
                int osmUIDPolylineFieldIndex = osmLineFeatureClass.FindField("osmuid");
                int osmVisiblePolylineFieldIndex = osmLineFeatureClass.FindField("osmvisible");
                int osmVersionPolylineFieldIndex = osmLineFeatureClass.FindField("osmversion");
                int osmChangesetPolylineFieldIndex = osmLineFeatureClass.FindField("osmchangeset");
                int osmTimeStampPolylineFieldIndex = osmLineFeatureClass.FindField("osmtimestamp");
                int osmMemberOfPolylineFieldIndex = osmLineFeatureClass.FindField("osmMemberOf");
                int osmMembersPolylineFieldIndex = osmLineFeatureClass.FindField("osmMembers");
                int osmSupportingElementPolylineFieldIndex = osmLineFeatureClass.FindField("osmSupportingElement");


                int osmPolygonIDFieldIndex = osmPolygonFeatureClass.FindField("OSMID");
                Dictionary<string, int> osmPolygonDomainAttributeFieldIndices = new Dictionary<string, int>();
                Dictionary<string, int> osmPolygonDomainAttributeFieldLength = new Dictionary<string, int>();
                foreach (var domains in availableDomains.domain)
                {
                    int currentFieldIndex = osmPolygonFeatureClass.FindField(domains.name);

                    if (currentFieldIndex != -1)
                    {
                        osmPolygonDomainAttributeFieldIndices.Add(domains.name, currentFieldIndex);
                        osmPolygonDomainAttributeFieldLength.Add(domains.name, osmPolygonFeatureClass.Fields.get_Field(currentFieldIndex).Length);
                    }
                }
                int tagCollectionPolygonFieldIndex = osmPolygonFeatureClass.FindField("osmTags");
                int osmUserPolygonFieldIndex = osmPolygonFeatureClass.FindField("osmuser");
                int osmUIDPolygonFieldIndex = osmPolygonFeatureClass.FindField("osmuid");
                int osmVisiblePolygonFieldIndex = osmPolygonFeatureClass.FindField("osmvisible");
                int osmVersionPolygonFieldIndex = osmPolygonFeatureClass.FindField("osmversion");
                int osmChangesetPolygonFieldIndex = osmPolygonFeatureClass.FindField("osmchangeset");
                int osmTimeStampPolygonFieldIndex = osmPolygonFeatureClass.FindField("osmtimestamp");
                int osmMemberOfPolygonFieldIndex = osmPolygonFeatureClass.FindField("osmMemberOf");
                int osmMembersPolygonFieldIndex = osmPolygonFeatureClass.FindField("osmMembers");
                int osmSupportingElementPolygonFieldIndex = osmPolygonFeatureClass.FindField("osmSupportingElement");

                ISpatialReferenceFactory spatialRef = new SpatialReferenceEnvironmentClass();
                ISpatialReference wgs84 = spatialRef.CreateGeographicCoordinateSystem((int)esriSRGeoCSType.esriSRGeoCS_WGS1984);
                
                bool shouldProject = !((IClone)wgs84).IsEqual((IClone)downloadSpatialReference);

                // set up the progress indicator
                IStepProgressor stepProgressor = TrackCancel as IStepProgressor;

                if (stepProgressor != null)
                {
                    stepProgressor.MinRange = 0;
                    stepProgressor.MaxRange = wayCapacity;
                    stepProgressor.Position = 0;
                    stepProgressor.Message = _resourceManager.GetString("GPTools_OSMGPFileReader_loadingWays");
                    stepProgressor.StepValue = 1;
                    stepProgressor.Show();
                }

                bool lineIndexRebuildRequired = false;
                bool polygonIndexRebuildRequired = false;

                int wayCount = 0;
                object missingValue = System.Reflection.Missing.Value;

                // enterprise GDB indicator -- supporting load only mode
                IFeatureClassLoad lineFeatureLoad = null;
                IFeatureClassLoad polygonFeatureLoad = null;

                using (SchemaLockManager lineLock = new SchemaLockManager(osmLineFeatureClass as ITable), polygonLock = new SchemaLockManager(osmPolygonFeatureClass as ITable))
                {
                    using (ComReleaser comReleaser = new ComReleaser())
                    {
                        IFeatureBuffer featureLineBuffer = null;
                        IFeatureCursor insertLineCursor = osmLineFeatureClass.Insert(true);
                        comReleaser.ManageLifetime(insertLineCursor);

                        IFeatureBuffer featurePolygonBuffer = null;
                        IFeatureCursor insertPolygonCursor = osmPolygonFeatureClass.Insert(true);
                        comReleaser.ManageLifetime(insertPolygonCursor);

                        if (((IWorkspace)featureWorkspace).WorkspaceFactory.WorkspaceType == esriWorkspaceType.esriRemoteDatabaseWorkspace)
                        {
                            lineFeatureLoad = osmLineFeatureClass as IFeatureClassLoad;
                            polygonFeatureLoad = osmPolygonFeatureClass as IFeatureClassLoad;
                        }

                        if (lineFeatureLoad != null)
                        {
                            lineFeatureLoad.LoadOnlyMode = true;
                        }

                        if (polygonFeatureLoad != null)
                        {
                            polygonFeatureLoad.LoadOnlyMode = true;
                        }

                        ISpatialReference nativeLineSpatialReference = ((IGeoDataset)osmLineFeatureClass).SpatialReference;
                        ISpatialReference nativePolygonSpatialReference = ((IGeoDataset)osmPolygonFeatureClass).SpatialReference;

                        IQueryFilter osmIDQueryFilter = new QueryFilterClass();
                        string sqlPointOSMID = osmPointFeatureClass.SqlIdentifier("OSMID");
                        IFeatureCursor updatePointCursor = null;

                        osmFileXmlReader = System.Xml.XmlReader.Create(osmFileLocation);
                        waySerializer = new XmlSerializer(typeof(way));

                        // the point query filter for updates will not changes, so let's do that ahead of time
                        try
                        {
                            osmIDQueryFilter.SubFields = osmPointFeatureClass.ShapeFieldName + "," + osmPointFeatureClass.Fields.get_Field(osmPointIDFieldIndex).Name + "," + osmPointFeatureClass.Fields.get_Field(osmWayRefCountFieldIndex).Name;
                        }
                        catch
                        { }

                        osmFileXmlReader.MoveToContent();
                        while (osmFileXmlReader.Read())
                        {
                            if (osmFileXmlReader.IsStartElement())
                            {
                                if (osmFileXmlReader.Name == "way")
                                {
                                    string currentwayString = osmFileXmlReader.ReadOuterXml();

                                    // assuming the way to be a polyline is sort of a safe assumption 
                                    // and won't cause any topology problem due to orientation and closeness
                                    bool wayIsLine = true;
                                    bool wayIsComplete = true;

                                    way currentWay = null;

                                    try
                                    {
                                        using (StringReader wayReader = new System.IO.StringReader(currentwayString))
                                        {
                                            currentWay = waySerializer.Deserialize(wayReader) as way;
                                        }

                                        // if the deserialization fails then go ahead and read the next xml element
                                        if (currentWay == null)
                                        {
                                            continue;
                                        }

                                        // and we are expecting at least some nodes on the way itself
                                        if (currentWay.nd == null)
                                        {
                                            continue;
                                        }

                                        featureLineBuffer = osmLineFeatureClass.CreateFeatureBuffer();
                                        featurePolygonBuffer = osmPolygonFeatureClass.CreateFeatureBuffer();

                                        IPointCollection wayPointCollection = null;
                                        wayIsLine = IsThisWayALine(currentWay);

                                        if (wayIsLine)
                                        {

                                            // check if a feature with the same OSMID already exists, because the can only be one
                                            if (checkForExisting == true)
                                            {
                                                if (CheckIfExists(osmLineFeatureClass as ITable, currentWay.id))
                                                {
                                                    continue;
                                                }
                                            }

                                            IPolyline wayPolyline = new PolylineClass();
                                            wayPolyline.SpatialReference = downloadSpatialReference;

                                            IPointIDAware polylineIDAware = wayPolyline as IPointIDAware;
                                            polylineIDAware.PointIDAware = true;

                                            wayPointCollection = wayPolyline as IPointCollection;

                                            # region generate line geometry
                                            if (conserveMemory == false)
                                            {
                                                for (int ndIndex = 0; ndIndex < currentWay.nd.Length; ndIndex++)
                                                {
                                                    string ndID = currentWay.nd[ndIndex].@ref;
                                                    if (osmNodeDictionary.ContainsKey(ndID))
                                                    {
                                                        IPoint newPoint = new PointClass();
                                                        newPoint.X = osmNodeDictionary[ndID].Longitude;
                                                        newPoint.Y = osmNodeDictionary[ndID].Latitude;

                                                        newPoint.SpatialReference = wgs84;

                                                        if (shouldProject)
                                                        {
                                                            newPoint.Project(((IGeoDataset)osmLineFeatureClass).SpatialReference);
                                                        }

                                                        IPointIDAware idAware = newPoint as IPointIDAware;
                                                        idAware.PointIDAware = true;

                                                        newPoint.ID = osmNodeDictionary[ndID].pointObjectID;

                                                        wayPointCollection.AddPoint(newPoint, ref missingValue, ref missingValue);

                                                        osmNodeDictionary[ndID].RefCounter = osmNodeDictionary[ndID].RefCounter + 1;
                                                    }
                                                    else
                                                    {
                                                        message.AddWarning(String.Format(_resourceManager.GetString("GPTools_OSMGPFileReader_undeterminedline_node"), currentWay.id, ndID));
                                                        // set the flag that the way is complete due to a missing node
                                                        wayIsComplete = false;
                                                        break;
                                                    }
                                                }

                                            }
                                            else
                                            {
                                                for (int pointIndex = 0; pointIndex < currentWay.nd.Length; pointIndex++)
                                                {
                                                    wayPointCollection.AddPoint(new PointClass());
                                                }

                                                List<string> idRequests = SplitOSMIDRequests(currentWay, 2);

                                                // build a list of node ids we can use to determine the point index in the line geometry
                                                // as well as a dictionary to determine the position in the list in case of duplicates nodes
                                                Dictionary<string, int> nodePositionDictionary = new Dictionary<string, int>(currentWay.nd.Length);
                                                List<string> nodeIDs = new List<string>(currentWay.nd.Length);

                                                foreach (nd wayNode in currentWay.nd)
                                                {
                                                    nodeIDs.Add(wayNode.@ref);

                                                    if (nodePositionDictionary.ContainsKey(wayNode.@ref) == false)
                                                    {
                                                        nodePositionDictionary.Add(wayNode.@ref, 0);
                                                    }
                                                }

                                                try
                                                {
                                                    osmIDQueryFilter.SubFields = osmPointFeatureClass.ShapeFieldName + "," + osmPointFeatureClass.Fields.get_Field(osmPointIDFieldIndex).Name + "," + osmPointFeatureClass.Fields.get_Field(osmWayRefCountFieldIndex).Name;
                                                }
                                                catch
                                                { }


                                                foreach (string request in idRequests)
                                                {
                                                    string idCompareString = request;
                                                    osmIDQueryFilter.WhereClause = sqlPointOSMID + " IN " + request;
                                                    using (ComReleaser innerComReleaser = new ComReleaser())
                                                    {
                                                        updatePointCursor = osmPointFeatureClass.Update(osmIDQueryFilter, true);
                                                        innerComReleaser.ManageLifetime(updatePointCursor);

                                                        IFeature nodeFeature = updatePointCursor.NextFeature();

                                                        while (nodeFeature != null)
                                                        {
                                                            // determine the index of the point in with respect to the node position
                                                            string nodeOSMIDString = Convert.ToString(nodeFeature.get_Value(osmPointIDFieldIndex));

                                                            // remove the ID from the request string
                                                            idCompareString = idCompareString.Replace(nodeOSMIDString, String.Empty);

                                                            int nodePositionIndex = -1;

                                                            while ((nodePositionIndex = nodeIDs.IndexOf(nodeOSMIDString, nodePositionDictionary[nodeOSMIDString])) != -1)
                                                            {
                                                                //// update the new position start search index
                                                                nodePositionDictionary[nodeOSMIDString] = nodePositionIndex + 1;

                                                                IPoint nodePoint = (IPoint)nodeFeature.ShapeCopy;
                                                                nodePoint.ID = nodeFeature.OID;
                                                                wayPointCollection.UpdatePoint(nodePositionIndex, nodePoint);

                                                                // increase the reference counter
                                                                if (osmWayRefCountFieldIndex != -1)
                                                                {
                                                                    nodeFeature.set_Value(osmWayRefCountFieldIndex, ((int)nodeFeature.get_Value(osmWayRefCountFieldIndex)) + 1);

                                                                    updatePointCursor.UpdateFeature(nodeFeature);
                                                                }
                                                            }

                                                            if (nodeFeature != null)
                                                                Marshal.ReleaseComObject(nodeFeature);

                                                            nodeFeature = updatePointCursor.NextFeature();
                                                        }

                                                        idCompareString = CleanReportedNodes(idCompareString);

                                                        // after removing the commas we should be left with only paranthesis left, meaning a string of length 2
                                                        // if we have more then we have found a missing node, resulting in an incomplete way geometry
                                                        if (idCompareString.Length > 2)
                                                        {
                                                            message.AddWarning(String.Format(_resourceManager.GetString("GPTools_OSMGPFileReader_undeterminedline_node"), currentWay.id, idCompareString));
                                                            wayIsComplete = false;
                                                        }
                                                    }
                                                }
                                            }
                                            #endregion

                                            if (wayIsComplete == false)
                                            {
                                                // if the way geometry is incomplete due to a missing node let's continue to the next way element
                                                missingWays.Add(currentWay.id);
                                                continue;
                                            }

                                            featureLineBuffer.Shape = wayPolyline;
                                            featureLineBuffer.set_Value(osmLineIDFieldIndex, currentWay.id);
                                        }
                                        else
                                        {
                                            // check if a feature with the same OSMID already exists, because the can only be one
                                            if (checkForExisting == true)
                                            {
                                                if (CheckIfExists(osmPolygonFeatureClass as ITable, currentWay.id))
                                                {
                                                    continue;
                                                }
                                            }


                                            IPolygon wayPolygon = new PolygonClass();
                                            wayPolygon.SpatialReference = downloadSpatialReference;

                                            IPointIDAware polygonIDAware = wayPolygon as IPointIDAware;
                                            polygonIDAware.PointIDAware = true;

                                            wayPointCollection = wayPolygon as IPointCollection;

                                            #region generate polygon geometry
                                            if (conserveMemory == false)
                                            {
                                                for (int ndIndex = 0; ndIndex < currentWay.nd.Length; ndIndex++)
                                                {
                                                    string ndID = currentWay.nd[ndIndex].@ref;
                                                    if (osmNodeDictionary.ContainsKey(ndID))
                                                    {
                                                        IPoint newPoint = new PointClass();
                                                        newPoint.X = osmNodeDictionary[ndID].Longitude;
                                                        newPoint.Y = osmNodeDictionary[ndID].Latitude;
                                                        newPoint.SpatialReference = wgs84;

                                                        if (shouldProject)
                                                        {
                                                            newPoint.Project(nativePolygonSpatialReference);
                                                        }

                                                        IPointIDAware idAware = newPoint as IPointIDAware;
                                                        idAware.PointIDAware = true;

                                                        newPoint.ID = osmNodeDictionary[ndID].pointObjectID;

                                                        wayPointCollection.AddPoint(newPoint, ref missingValue, ref missingValue);
                                                    }
                                                    else
                                                    {
                                                        message.AddWarning(String.Format(_resourceManager.GetString("GPTools_OSMGPFileReader_undeterminedpolygon_node"), currentWay.id, ndID));
                                                        wayIsComplete = false;
                                                        break;
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                for (int pointIndex = 0; pointIndex < currentWay.nd.Length; pointIndex++)
                                                {
                                                    wayPointCollection.AddPoint(new PointClass());
                                                }

                                                List<string> idRequests = SplitOSMIDRequests(currentWay, 2);

                                                // build a list of node ids we can use to determine the point index in the line geometry
                                                // as well as a dictionary to determine the position in the list in case of duplicates nodes
                                                Dictionary<string, int> nodePositionDictionary = new Dictionary<string, int>(currentWay.nd.Length);
                                                List<string> nodeIDs = new List<string>(currentWay.nd.Length);

                                                foreach (nd wayNode in currentWay.nd)
                                                {
                                                    nodeIDs.Add(wayNode.@ref);

                                                    if (nodePositionDictionary.ContainsKey(wayNode.@ref) == false)
                                                    {
                                                        nodePositionDictionary.Add(wayNode.@ref, 0);
                                                    }
                                                }

                                                try
                                                {
                                                    osmIDQueryFilter.SubFields = osmPointFeatureClass.ShapeFieldName + "," + osmPointFeatureClass.Fields.get_Field(osmPointIDFieldIndex).Name + "," + osmPointFeatureClass.Fields.get_Field(osmWayRefCountFieldIndex).Name;
                                                }
                                                catch
                                                { }


                                                foreach (string osmIDRequest in idRequests)
                                                {
                                                    string idCompareString = osmIDRequest;

                                                    using (ComReleaser innercomReleaser = new ComReleaser())
                                                    {
                                                        osmIDQueryFilter.WhereClause = sqlPointOSMID + " IN " + osmIDRequest;
                                                        updatePointCursor = osmPointFeatureClass.Update(osmIDQueryFilter, false);
                                                        innercomReleaser.ManageLifetime(updatePointCursor);

                                                        IFeature nodeFeature = updatePointCursor.NextFeature();

                                                        while (nodeFeature != null)
                                                        {
                                                            // determine the index of the point in with respect to the node position
                                                            string nodeOSMIDString = Convert.ToString(nodeFeature.get_Value(osmPointIDFieldIndex));

                                                            idCompareString = idCompareString.Replace(nodeOSMIDString, String.Empty);

                                                            int nodePositionIndex = nodeIDs.IndexOf(nodeOSMIDString, nodePositionDictionary[nodeOSMIDString]);

                                                            // update the new position start search index
                                                            nodePositionDictionary[nodeOSMIDString] = nodePositionIndex + 1;

                                                            IPoint nodePoint = (IPoint)nodeFeature.ShapeCopy;
                                                            nodePoint.ID = nodeFeature.OID;
                                                            wayPointCollection.UpdatePoint(nodePositionIndex, nodePoint);

                                                            // increase the reference counter
                                                            if (osmWayRefCountFieldIndex != -1)
                                                            {
                                                                nodeFeature.set_Value(osmWayRefCountFieldIndex, ((int)nodeFeature.get_Value(osmWayRefCountFieldIndex)) + 1);

                                                                updatePointCursor.UpdateFeature(nodeFeature);
                                                            }

                                                            if (nodeFeature != null)
                                                                Marshal.ReleaseComObject(nodeFeature);

                                                            nodeFeature = updatePointCursor.NextFeature();
                                                        }

                                                        idCompareString = CleanReportedNodes(idCompareString);

                                                        if (idCompareString.Length > 2)
                                                        {
                                                            message.AddWarning(String.Format(_resourceManager.GetString("GPTools_OSMGPFileReader_undeterminedpolygon_node"), currentWay.id, idCompareString));
                                                            wayIsComplete = false;
                                                        }
                                                    }
                                                }
                                            }
                                            #endregion

                                            if (wayIsComplete == false)
                                            {
                                                continue;
                                            }

                                            // remove the last point as OSM considers them to be coincident
                                            wayPointCollection.RemovePoints(wayPointCollection.PointCount - 1, 1);
                                            ((IPolygon)wayPointCollection).Close();

                                            featurePolygonBuffer.Shape = (IPolygon)wayPointCollection;
                                            featurePolygonBuffer.set_Value(osmPolygonIDFieldIndex, currentWay.id);
                                        }


                                        if (wayIsLine)
                                        {
                                            insertTags(osmLineDomainAttributeFieldIndices, osmLineDomainAttributeFieldLength, tagCollectionPolylineFieldIndex, featureLineBuffer, currentWay.tag);
                                        }
                                        else
                                        {
                                            insertTags(osmPolygonDomainAttributeFieldIndices, osmPolygonDomainAttributeFieldLength, tagCollectionPolygonFieldIndex, featurePolygonBuffer, currentWay.tag);
                                        }

                                        // store the administrative attributes
                                        // user, uid, version, changeset, timestamp, visible
                                        if (fastLoad == false)
                                        {
                                            if (!String.IsNullOrEmpty(currentWay.user))
                                            {
                                                if (wayIsLine)
                                                {
                                                    if (osmUserPolylineFieldIndex != -1)
                                                    {
                                                        featureLineBuffer.set_Value(osmUserPolylineFieldIndex, currentWay.user);
                                                    }
                                                }
                                                else
                                                {
                                                    if (osmUserPolygonFieldIndex != -1)
                                                    {
                                                        featurePolygonBuffer.set_Value(osmUserPolygonFieldIndex, currentWay.user);
                                                    }
                                                }
                                            }

                                            if (!String.IsNullOrEmpty(currentWay.uid))
                                            {
                                                if (wayIsLine)
                                                {
                                                    if (osmUIDPolylineFieldIndex != -1)
                                                    {
                                                        featureLineBuffer.set_Value(osmUIDPolylineFieldIndex, Convert.ToInt32(currentWay.uid));
                                                    }
                                                }
                                                else
                                                {
                                                    if (osmUIDPolygonFieldIndex != -1)
                                                    {
                                                        featurePolygonBuffer.set_Value(osmUIDPolygonFieldIndex, Convert.ToInt32(currentWay.uid));
                                                    }
                                                }
                                            }

                                            if (wayIsLine)
                                            {
                                                if (osmVisiblePolylineFieldIndex != -1)
                                                {
                                                    featureLineBuffer.set_Value(osmVisiblePolylineFieldIndex, currentWay.visible.ToString());
                                                }
                                            }
                                            else
                                            {
                                                if (osmVisiblePolygonFieldIndex != -1)
                                                {
                                                    featurePolygonBuffer.set_Value(osmVisiblePolygonFieldIndex, currentWay.visible.ToString());
                                                }
                                            }

                                            if (!String.IsNullOrEmpty(currentWay.version))
                                            {
                                                if (wayIsLine)
                                                {
                                                    if (osmVersionPolylineFieldIndex != -1)
                                                    {
                                                        featureLineBuffer.set_Value(osmVersionPolylineFieldIndex, Convert.ToInt32(currentWay.version));
                                                    }
                                                }
                                                else
                                                {
                                                    if (osmVersionPolygonFieldIndex != -1)
                                                    {
                                                        featurePolygonBuffer.set_Value(osmVersionPolygonFieldIndex, Convert.ToInt32(currentWay.version));
                                                    }
                                                }
                                            }

                                            if (!String.IsNullOrEmpty(currentWay.changeset))
                                            {
                                                if (wayIsLine)
                                                {
                                                    if (osmChangesetPolylineFieldIndex != -1)
                                                    {
                                                        featureLineBuffer.set_Value(osmChangesetPolylineFieldIndex, Convert.ToInt32(currentWay.changeset));
                                                    }
                                                }
                                                else
                                                {
                                                    if (osmChangesetPolygonFieldIndex != -1)
                                                    {
                                                        featurePolygonBuffer.set_Value(osmChangesetPolygonFieldIndex, Convert.ToInt32(currentWay.changeset));
                                                    }
                                                }
                                            }

                                            if (!String.IsNullOrEmpty(currentWay.timestamp))
                                            {
                                                try
                                                {
                                                    if (wayIsLine)
                                                    {
                                                        if (osmTimeStampPolylineFieldIndex != -1)
                                                        {
                                                            featureLineBuffer.set_Value(osmTimeStampPolylineFieldIndex, Convert.ToDateTime(currentWay.timestamp));
                                                        }
                                                    }
                                                    else
                                                    {
                                                        if (osmTimeStampPolygonFieldIndex != -1)
                                                        {
                                                            featurePolygonBuffer.set_Value(osmTimeStampPolygonFieldIndex, Convert.ToDateTime(currentWay.timestamp));
                                                        }
                                                    }
                                                }
                                                catch (Exception ex)
                                                {
                                                    message.AddWarning(String.Format(_resourceManager.GetString("GPTools_OSMGPFileReader_invalidTimeFormat"), ex.Message));
                                                }
                                            }

                                            if (wayIsLine)
                                            {
                                                if (osmSupportingElementPolylineFieldIndex > -1)
                                                {
                                                    if (currentWay.tag == null)
                                                    {
                                                        featureLineBuffer.set_Value(osmSupportingElementPolylineFieldIndex, "yes");
                                                    }
                                                    else
                                                    {
                                                        featureLineBuffer.set_Value(osmSupportingElementPolylineFieldIndex, "no");
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                if (osmSupportingElementPolygonFieldIndex > -1)
                                                {
                                                    if (currentWay.tag == null)
                                                    {
                                                        featurePolygonBuffer.set_Value(osmSupportingElementPolygonFieldIndex, "yes");
                                                    }
                                                    else
                                                    {
                                                        featurePolygonBuffer.set_Value(osmSupportingElementPolygonFieldIndex, "no");
                                                    }
                                                }
                                            }
                                        } // fast load

                                        try
                                        {
                                            if (wayIsLine)
                                            {
                                                insertLineCursor.InsertFeature(featureLineBuffer);
                                                lineIndexRebuildRequired = true;
                                            }
                                            else
                                            {
                                                insertPolygonCursor.InsertFeature(featurePolygonBuffer);
                                                polygonIndexRebuildRequired = true;
                                            }

                                            wayCount = wayCount + 1;


                                            if (stepProgressor != null)
                                            {
                                                stepProgressor.Position = wayCount;
                                            }


                                            if ((wayCount % 50000) == 0)
                                            {
                                                message.AddMessage(String.Format(_resourceManager.GetString("GPTools_OSMGPFileReader_waysloaded"), wayCount));
                                            }

                                        }
                                        catch (Exception ex)
                                        {
                                            message.AddWarning(ex.Message);
                                            message.AddWarning(currentwayString);
                                        }

                                    }
                                    catch (Exception ex)
                                    {
                                        System.Diagnostics.Debug.WriteLine(ex.Message);
                                        System.Diagnostics.Debug.WriteLine(ex.StackTrace);
                                    }
                                    finally
                                    {
                                        if (featureLineBuffer != null)
                                        {
                                            Marshal.ReleaseComObject(featureLineBuffer);

                                            if (featureLineBuffer != null)
                                                featureLineBuffer = null;
                                        }

                                        if (featurePolygonBuffer != null)
                                        {
                                            Marshal.ReleaseComObject(featurePolygonBuffer);

                                            if (featurePolygonBuffer != null)
                                                featurePolygonBuffer = null;
                                        }

                                        currentWay = null;
                                    }

                                    if (TrackCancel.Continue() == false)
                                    {
                                        insertPolygonCursor.Flush();
                                        if (polygonFeatureLoad != null)
                                        {
                                            polygonFeatureLoad.LoadOnlyMode = false;
                                        }

                                        insertLineCursor.Flush();
                                        if (lineFeatureLoad != null)
                                        {
                                            lineFeatureLoad.LoadOnlyMode = false;
                                        }

                                        return missingWays;
                                    }
                                }
                            }
                        }

                        osmFileXmlReader.Close();

                        if (stepProgressor != null)
                        {
                            stepProgressor.Hide();
                        }

                        message.AddMessage(String.Format(_resourceManager.GetString("GPTools_OSMGPFileReader_waysloaded"), wayCount));

                        insertPolygonCursor.Flush();
                        if (polygonFeatureLoad != null)
                        {
                            polygonFeatureLoad.LoadOnlyMode = false;
                        }

                        insertLineCursor.Flush();
                        if (lineFeatureLoad != null)
                        {
                            lineFeatureLoad.LoadOnlyMode = false;
                        }
                    }
                }

                IGeoProcessor2 geoProcessor = new GeoProcessorClass();
                IGPUtilities3 gpUtilities3 = new GPUtilitiesClass();
                bool storedOriginal = geoProcessor.AddOutputsToMap;
                IVariantArray parameterArrary = null;
                IGeoProcessorResult2 gpResults2 = null;

                try
                {
                    geoProcessor.AddOutputsToMap = false;

                    if (lineIndexRebuildRequired)
                    {
                        IIndexes featureClassIndexes = osmLineFeatureClass.Indexes;
                        int indexPosition = -1;
                        featureClassIndexes.FindIndex("osmID_IDX", out indexPosition);

                        string fcLocation = GetLocationString(targetGPValue, osmLineFeatureClass);

                        if (indexPosition == -1)
                        {
                            message.AddMessage(_resourceManager.GetString("GPTools_buildinglineidx"));

                            // Addd index for osmid column
                            parameterArrary = CreateAddIndexParameterArray(fcLocation, "OSMID", "osmID_IDX", "UNIQUE", "");
                            gpResults2 = geoProcessor.Execute("AddIndex_management", parameterArrary, TrackCancel) as IGeoProcessorResult2;
                        }

                        if (wayCount > 100)
                        {
                            // in this case we are dealing with a file geodatabase
                            if (lineFeatureLoad == null)
                            {
                                UpdateSpatialGridIndex(TrackCancel, message, geoProcessor, fcLocation);
                            }
                        }
                    }

                    if (polygonIndexRebuildRequired)
                    {
                        IIndexes featureClassIndexes = osmPolygonFeatureClass.Indexes;
                        int indexPosition = -1;
                        featureClassIndexes.FindIndex("osmID_IDX", out indexPosition);

                        string fcLocation = GetLocationString(targetGPValue, osmPolygonFeatureClass);

                        if (indexPosition == -1)
                        {
                            message.AddMessage(_resourceManager.GetString("GPTools_buildingpolygonidx"));

                            IGPValue polygonFeatureClassGPValue = gpUtilities3.MakeGPValueFromObject(osmPolygonFeatureClass);

                            if (polygonFeatureClassGPValue != null)
                            {
                                // Addd index for osmid column
                                parameterArrary = CreateAddIndexParameterArray(fcLocation, "OSMID", "osmID_IDX", "UNIQUE", "");
                                gpResults2 = geoProcessor.Execute("AddIndex_management", parameterArrary, TrackCancel) as IGeoProcessorResult2;
                            }
                        }

                        if (wayCount > 100)
                        {
                            if (polygonFeatureLoad == null)
                            {
                                UpdateSpatialGridIndex(TrackCancel, message, geoProcessor, fcLocation);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    message.AddWarning(ex.Message);
                }
                finally
                {
                    geoProcessor.AddOutputsToMap = storedOriginal;

                    Marshal.FinalReleaseComObject(gpUtilities3);
                    Marshal.FinalReleaseComObject(geoProcessor);
                }
            }
            catch (Exception ex)
            {
                message.AddWarning(ex.Message);
            }
            finally
            {
                if (waySerializer != null)
                    waySerializer = null;

                if (osmFileXmlReader != null)
                    osmFileXmlReader = null;

                System.GC.Collect();
                System.GC.WaitForPendingFinalizers();
            }

            return missingWays;
        }

        private static string CleanReportedNodes(string idCompareString)
        {
            // if all the requested IDs were returned we should only have brackets and commas left
            string save_compareString = idCompareString;
            string searchPattern = @"(\d+,\d*)";
            Regex regularExpression = new Regex(searchPattern);

            Match matches = regularExpression.Match(save_compareString);
            StringBuilder missingNodesString = new StringBuilder();

            foreach (Group matchGroup in matches.Groups)
            {
                if (String.IsNullOrEmpty(matchGroup.Value) == false)
                {
                    missingNodesString.Append(matchGroup.Value);
                    missingNodesString.Append(",");
                }
            }

            idCompareString = "(" + missingNodesString.ToString().Replace(",,", ",") + ")";
            return idCompareString;
        }

        internal static string retrieveNodeID(IFeatureClass pointFeatureClass, int osmIDPointFieldIndex, int extensionVersion, IPoint pointGeometry)
        {
            string nodeID = string.Empty;

            if (pointGeometry == null)
                return String.Empty;

            if (pointGeometry.IsEmpty)
                return String.Empty;

            if (((IPointIDAware)pointGeometry).PointIDAware == false)
            {
                return nodeID;
            }
            if (extensionVersion == 1)
            {
                nodeID = Convert.ToString(pointGeometry.ID);
            }
            else if (extensionVersion == 2)
            {
                IFeature pointFeature = null;
                try
                {
                    pointFeature = pointFeatureClass.GetFeature(pointGeometry.ID);
                }
                catch { }
                if (pointFeature != null)
                {
                    if (osmIDPointFieldIndex > -1)
                    {
                        nodeID = Convert.ToString(pointFeature.get_Value(osmIDPointFieldIndex));
                    }

                    Marshal.ReleaseComObject(pointFeature);
                }
            }
            return nodeID;
        }


        internal void countOSMStuff(string osmFileLocation, ref long nodeCapacity, ref long wayCapacity, ref long relationCapacity, ref ITrackCancel CancelTracker)
        {
            using (System.Xml.XmlReader osmFileXmlReader = System.Xml.XmlReader.Create(osmFileLocation))
            {
                osmFileXmlReader.MoveToContent();

                while (osmFileXmlReader.Read())
                {
                    if (CancelTracker.Continue())
                    {
                        if (osmFileXmlReader.IsStartElement())
                        {
                            string currentNodeName = osmFileXmlReader.Name;

                            switch (currentNodeName)
                            {
                                case "node":
                                    nodeCapacity++;
                                    break;
                                case "way":
                                    wayCapacity++;
                                    break;
                                case "relation":
                                    relationCapacity++;
                                    break;
                            }
                        }
                    }
                    else
                        return;
                }

                osmFileXmlReader.Close();
            }
        }

        internal Dictionary<esriGeometryType, List<string>> countOSMCapacityAndTags(string osmFileLocation, ref long nodeCapacity, ref long wayCapacity, ref long relationCapacity, ref ITrackCancel CancelTracker)
        {
            Dictionary<esriGeometryType, List<string>> attributesDictionary = new Dictionary<esriGeometryType, List<string>>();

            try
            {
                XmlSerializer nodeSerializer = new XmlSerializer(typeof(node));
                XmlSerializer waySerializer = new XmlSerializer(typeof(way));
                XmlSerializer relationSerializer = new XmlSerializer(typeof(relation));

                List<string> pointTags = new List<string>();
                List<string> lineTags = new List<string>();
                List<string> polygonTags = new List<string>();

                using (System.Xml.XmlReader osmFileXmlReader = System.Xml.XmlReader.Create(osmFileLocation))
                {
                    osmFileXmlReader.MoveToContent();

                    while (osmFileXmlReader.Read())
                    {
                        if (CancelTracker.Continue())
                        {
                            if (osmFileXmlReader.IsStartElement())
                            {
                                string currentNodeName = osmFileXmlReader.Name;

                                switch (currentNodeName)
                                {
                                    case "node":
                                        string currentNodeString = osmFileXmlReader.ReadOuterXml();
                                        // turn the xml node representation into a node class representation
                                        ESRI.ArcGIS.OSM.OSMClassExtension.node currentNode = null;
                                        using (StringReader nodeReader = new System.IO.StringReader(currentNodeString))
                                        {
                                            try
                                            {
                                                currentNode = nodeSerializer.Deserialize(nodeReader) as ESRI.ArcGIS.OSM.OSMClassExtension.node;
                                            }
                                            catch { }
                                        }

                                        if (currentNode != null)
                                        {
                                            if (currentNode.tag != null)
                                            {
                                                foreach (tag currentTag in currentNode.tag)
                                                {
                                                    if (!pointTags.Contains(currentTag.k))
                                                    {
                                                        pointTags.Add(currentTag.k);
                                                    }
                                                }
                                            }
                                        }

                                        nodeCapacity++;
                                        break;
                                    case "way":
                                        wayCapacity++;

                                        string currentWayString = osmFileXmlReader.ReadOuterXml();
                                        // turn the xml node representation into a node class representation
                                        ESRI.ArcGIS.OSM.OSMClassExtension.way currentWay = null;
                                        using (StringReader wayReader = new System.IO.StringReader(currentWayString))
                                        {
                                            try
                                            {
                                                currentWay = waySerializer.Deserialize(wayReader) as ESRI.ArcGIS.OSM.OSMClassExtension.way;
                                            }
                                            catch { }
                                        }

                                        if (currentWay != null)
                                        {
                                            if (currentWay.tag != null)
                                            {
                                                foreach (tag currentTag in currentWay.tag)
                                                {
                                                    if (OSMToolHelper.IsThisWayALine(currentWay) && !lineTags.Contains(currentTag.k))
                                                    {
                                                        lineTags.Add(currentTag.k);
                                                    }
                                                    else if (!polygonTags.Contains(currentTag.k))
                                                    {
                                                        polygonTags.Add(currentTag.k);
                                                    }
                                                }
                                            }
                                        }

                                        break;
                                    case "relation":
                                        relationCapacity++;

                                        // for relation let's NOT do an exhaustive determine if we have a polygon or maybe a multipart polyline
                                        // or maybe a super relation
                                        string currentRelationString = osmFileXmlReader.ReadOuterXml();

                                        ESRI.ArcGIS.OSM.OSMClassExtension.relation currentRelation = null;
                                        using (StringReader relationReader = new System.IO.StringReader(currentRelationString))
                                        {
                                            try
                                            {
                                                currentRelation = relationSerializer.Deserialize(relationReader) as ESRI.ArcGIS.OSM.OSMClassExtension.relation;
                                            }
                                            catch { }
                                        }

                                        if (currentRelation != null)
                                        {
                                            if (currentRelation.Items != null)
                                            {
                                                bool polygonTagDetected = false;

                                                foreach (var item in currentRelation.Items)
                                                {
                                                    if (item is ESRI.ArcGIS.OSM.OSMClassExtension.tag)
                                                    {
                                                        ESRI.ArcGIS.OSM.OSMClassExtension.tag currentTag = item as ESRI.ArcGIS.OSM.OSMClassExtension.tag;

                                                        if (polygonTagDetected)
                                                        {
                                                            if (!polygonTags.Contains(currentTag.k))
                                                            {
                                                                polygonTags.Add(currentTag.k);
                                                            }
                                                        }
                                                        else if (currentTag.k.ToUpper().Equals("TYPE"))
                                                        {
                                                            if ((currentTag.v.ToUpper().Equals("POLYGON") || currentTag.v.ToUpper().Equals("MULTIPOLYGON")))
                                                            {
                                                                polygonTagDetected = true;
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                        break;
                                }
                            }
                        }
                    }

                    osmFileXmlReader.Close();
                }

                attributesDictionary.Add(esriGeometryType.esriGeometryPoint, pointTags);
                attributesDictionary.Add(esriGeometryType.esriGeometryPolyline, lineTags);
                attributesDictionary.Add(esriGeometryType.esriGeometryPolygon, polygonTags);
            }

            catch { }

            return attributesDictionary;
        }

        internal List<string> loadOSMRelations(string osmFileLocation, ref ITrackCancel TrackCancel, ref IGPMessages message, IGPValue targetGPValue, IFeatureClass osmPointFeatureClass, IFeatureClass osmLineFeatureClass, IFeatureClass osmPolygonFeatureClass, int relationCapacity, ITable relationTable, OSMDomains availableDomains, bool fastLoad, bool checkForExisting)
        {

            List<string> missingRelations = null;
            XmlReader osmFileXmlReader = null;
            XmlSerializer relationSerializer = null;

            try
            {

                missingRelations = new List<string>();

                if (osmLineFeatureClass == null)
                {
                    throw new ArgumentNullException("osmLineFeatureClass");
                }

                if (osmPolygonFeatureClass == null)
                {
                    throw new ArgumentNullException("osmPolygonFeatureClass");
                }

                if (relationTable == null)
                {
                    throw new ArgumentNullException("relationTable");
                }

                int osmPointIDFieldIndex = osmPointFeatureClass.FindField("OSMID");
                Dictionary<string, int> osmPointDomainAttributeFieldIndices = new Dictionary<string, int>();
                foreach (var domains in availableDomains.domain)
                {
                    int currentFieldIndex = osmPointFeatureClass.FindField(domains.name);

                    if (currentFieldIndex != -1)
                    {
                        osmPointDomainAttributeFieldIndices.Add(domains.name, currentFieldIndex);
                    }
                }
                int tagCollectionPointFieldIndex = osmPointFeatureClass.FindField("osmTags");
                int osmUserPointFieldIndex = osmPointFeatureClass.FindField("osmuser");
                int osmUIDPointFieldIndex = osmPointFeatureClass.FindField("osmuid");
                int osmVisiblePointFieldIndex = osmPointFeatureClass.FindField("osmvisible");
                int osmVersionPointFieldIndex = osmPointFeatureClass.FindField("osmversion");
                int osmChangesetPointFieldIndex = osmPointFeatureClass.FindField("osmchangeset");
                int osmTimeStampPointFieldIndex = osmPointFeatureClass.FindField("osmtimestamp");
                int osmMemberOfPointFieldIndex = osmPointFeatureClass.FindField("osmMemberOf");
                int osmSupportingElementPointFieldIndex = osmPointFeatureClass.FindField("osmSupportingElement");
                int osmWayRefCountFieldIndex = osmPointFeatureClass.FindField("wayRefCount");


                int osmLineIDFieldIndex = osmLineFeatureClass.FindField("OSMID");
                Dictionary<string, int> osmLineDomainAttributeFieldIndices = new Dictionary<string, int>();
                Dictionary<string, int> osmLineDomainAttributeFieldLength = new Dictionary<string, int>();
                foreach (var domains in availableDomains.domain)
                {
                    int currentFieldIndex = osmLineFeatureClass.FindField(domains.name);

                    if (currentFieldIndex != -1)
                    {
                        osmLineDomainAttributeFieldIndices.Add(domains.name, currentFieldIndex);
                        osmLineDomainAttributeFieldLength.Add(domains.name, osmLineFeatureClass.Fields.get_Field(currentFieldIndex).Length);
                    }
                }
                int tagCollectionPolylineFieldIndex = osmLineFeatureClass.FindField("osmTags");
                int osmUserPolylineFieldIndex = osmLineFeatureClass.FindField("osmuser");
                int osmUIDPolylineFieldIndex = osmLineFeatureClass.FindField("osmuid");
                int osmVisiblePolylineFieldIndex = osmLineFeatureClass.FindField("osmvisible");
                int osmVersionPolylineFieldIndex = osmLineFeatureClass.FindField("osmversion");
                int osmChangesetPolylineFieldIndex = osmLineFeatureClass.FindField("osmchangeset");
                int osmTimeStampPolylineFieldIndex = osmLineFeatureClass.FindField("osmtimestamp");
                int osmMemberOfPolylineFieldIndex = osmLineFeatureClass.FindField("osmMemberOf");
                int osmMembersPolylineFieldIndex = osmLineFeatureClass.FindField("osmMembers");
                int osmSupportingElementPolylineFieldIndex = osmLineFeatureClass.FindField("osmSupportingElement");


                int osmPolygonIDFieldIndex = osmPolygonFeatureClass.FindField("OSMID");
                Dictionary<string, int> osmPolygonDomainAttributeFieldIndices = new Dictionary<string, int>();
                Dictionary<string, int> osmPolygonDomainAttributeFieldLength = new Dictionary<string, int>();
                foreach (var domains in availableDomains.domain)
                {
                    int currentFieldIndex = osmPolygonFeatureClass.FindField(domains.name);

                    if (currentFieldIndex != -1)
                    {
                        osmPolygonDomainAttributeFieldIndices.Add(domains.name, currentFieldIndex);
                        osmPolygonDomainAttributeFieldLength.Add(domains.name, osmPolygonFeatureClass.Fields.get_Field(currentFieldIndex).Length);
                    }
                }
                int tagCollectionPolygonFieldIndex = osmPolygonFeatureClass.FindField("osmTags");
                int osmUserPolygonFieldIndex = osmPolygonFeatureClass.FindField("osmuser");
                int osmUIDPolygonFieldIndex = osmPolygonFeatureClass.FindField("osmuid");
                int osmVisiblePolygonFieldIndex = osmPolygonFeatureClass.FindField("osmvisible");
                int osmVersionPolygonFieldIndex = osmPolygonFeatureClass.FindField("osmversion");
                int osmChangesetPolygonFieldIndex = osmPolygonFeatureClass.FindField("osmchangeset");
                int osmTimeStampPolygonFieldIndex = osmPolygonFeatureClass.FindField("osmtimestamp");
                int osmMemberOfPolygonFieldIndex = osmPolygonFeatureClass.FindField("osmMemberOf");
                int osmMembersPolygonFieldIndex = osmPolygonFeatureClass.FindField("osmMembers");
                int osmSupportingElementPolygonFieldIndex = osmPolygonFeatureClass.FindField("osmSupportingElement");


                int osmRelationIDFieldIndex = relationTable.FindField("OSMID");
                int tagCollectionRelationFieldIndex = relationTable.FindField("osmTags");
                int osmUserRelationFieldIndex = relationTable.FindField("osmuser");
                int osmUIDRelationFieldIndex = relationTable.FindField("osmuid");
                int osmVisibleRelationFieldIndex = relationTable.FindField("osmvisible");
                int osmVersionRelationFieldIndex = relationTable.FindField("osmversion");
                int osmChangesetRelationFieldIndex = relationTable.FindField("osmchangeset");
                int osmTimeStampRelationFieldIndex = relationTable.FindField("osmtimestamp");
                int osmMemberOfRelationFieldIndex = relationTable.FindField("osmMemberOf");
                int osmMembersRelationFieldIndex = relationTable.FindField("osmMembers");
                int osmSupportingElementRelationFieldIndex = relationTable.FindField("osmSupportingElement");


                // list for reference count and relation list for lines/polygons/relations
                // set up the progress indicator
                IStepProgressor stepProgressor = TrackCancel as IStepProgressor;

                if (stepProgressor != null)
                {
                    stepProgressor.MinRange = 0;
                    stepProgressor.MaxRange = relationCapacity;
                    stepProgressor.Position = 0;
                    stepProgressor.Message = _resourceManager.GetString("GPTools_OSMGPFileReader_loadingRelations");
                    stepProgressor.StepValue = 1;
                    stepProgressor.Show();
                }

                bool relationIndexRebuildRequired = false;

                if (relationTable != null)
                {
                    osmFileXmlReader = System.Xml.XmlReader.Create(osmFileLocation);
                    relationSerializer = new XmlSerializer(typeof(relation));

                    using (ComReleaser comReleaser = new ComReleaser())
                    {
                        using (SchemaLockManager linelock = new SchemaLockManager(osmLineFeatureClass as ITable), polygonLock = new SchemaLockManager(osmPolygonFeatureClass as ITable), relationLock = new SchemaLockManager(relationTable))
                        {
                            IRowBuffer rowBuffer = null;
                            ICursor rowCursor = relationTable.Insert(true);
                            comReleaser.ManageLifetime(rowCursor);

                            IFeatureBuffer lineFeatureBuffer = null;
                            IFeatureCursor lineFeatureInsertCursor = osmLineFeatureClass.Insert(true);
                            comReleaser.ManageLifetime(lineFeatureInsertCursor);

                            IFeatureBuffer polygonFeatureBuffer = null;
                            IFeatureCursor polygonFeatureInsertCursor = osmPolygonFeatureClass.Insert(true);
                            comReleaser.ManageLifetime(polygonFeatureInsertCursor);

                            int relationCount = 1;
                            int relationDebugCount = 1;

                            string lineSQLIdentifier = osmLineFeatureClass.SqlIdentifier("OSMID");
                            string polygonSQLIdentifier = osmPolygonFeatureClass.SqlIdentifier("OSMID");

                            message.AddMessage(_resourceManager.GetString("GPTools_OSMGPFileReader_resolvegeometries"));

                            osmFileXmlReader.MoveToContent();
                            while (osmFileXmlReader.Read())
                            {
                                if (osmFileXmlReader.IsStartElement())
                                {
                                    if (osmFileXmlReader.Name == "relation")
                                    {
                                        relation currentRelation = null;
                                        try
                                        {
                                            // read the full relation node
                                            string currentrelationString = osmFileXmlReader.ReadOuterXml();

                                            using (StringReader relationReader = new System.IO.StringReader(currentrelationString))
                                            {
                                                // de-serialize the xml into to the class instance
                                                currentRelation = relationSerializer.Deserialize(relationReader) as relation;
                                            }

                                            if (currentRelation == null)
                                                continue;

                                            relationDebugCount = relationDebugCount + 1;
                                            esriGeometryType detectedGeometryType = determineRelationGeometryType(osmLineFeatureClass, osmPolygonFeatureClass, relationTable, currentRelation);

                                            #region Check if already exists when syncing
                                            if (checkForExisting)
                                            {
                                                switch (detectedGeometryType)
                                                {
                                                    case esriGeometryType.esriGeometryAny:
                                                        if (CheckIfExists(relationTable, currentRelation.id))
                                                            continue;
                                                        break;
                                                    case esriGeometryType.esriGeometryBag:
                                                        if (CheckIfExists(relationTable, currentRelation.id))
                                                            continue;
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
                                                        if (CheckIfExists(osmLineFeatureClass as ITable, currentRelation.id))
                                                            continue;
                                                        break;
                                                    case esriGeometryType.esriGeometryMultiPatch:
                                                        break;
                                                    case esriGeometryType.esriGeometryMultipoint:
                                                        break;
                                                    case esriGeometryType.esriGeometryNull:
                                                        if (CheckIfExists(relationTable, currentRelation.id))
                                                            continue;
                                                        break;
                                                    case esriGeometryType.esriGeometryPath:
                                                        break;
                                                    case esriGeometryType.esriGeometryPoint:
                                                        break;
                                                    case esriGeometryType.esriGeometryPolygon:
                                                        if (CheckIfExists(osmPolygonFeatureClass as ITable, currentRelation.id))
                                                            continue;
                                                        break;
                                                    case esriGeometryType.esriGeometryPolyline:
                                                        if (CheckIfExists(osmLineFeatureClass as ITable, currentRelation.id))
                                                            continue;
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
                                            #endregion

                                            List<tag> relationTagList = new List<tag>();
                                            List<member> relationMemberList = new List<member>();
                                            Dictionary<string, string> wayList = new Dictionary<string, string>();

                                            // sanity check that the overall relation notation contains at least something
                                            if (currentRelation.Items == null)
                                                continue;

                                            List<OSMNodeFeature> osmPointList = null;
                                            List<OSMLineFeature> osmLineList = null;
                                            List<OSMPolygonFeature> osmPolygonList = null;
                                            List<OSMRelation> osmRelationList = null;

                                            rowBuffer = relationTable.CreateRowBuffer();
                                            lineFeatureBuffer = osmLineFeatureClass.CreateFeatureBuffer();
                                            polygonFeatureBuffer = osmPolygonFeatureClass.CreateFeatureBuffer();

                                            foreach (var item in currentRelation.Items)
                                            {
                                                if (item is member)
                                                {
                                                    member memberItem = item as member;
                                                    relationMemberList.Add(memberItem);

                                                    switch (memberItem.type)
                                                    {
                                                        case memberType.way:

                                                            // check the referenced item on the relation
                                                            // - the referenced way still be either line or polygon
                                                            bool isPolyline = true;
                                                            if (detectedGeometryType == esriGeometryType.esriGeometryPolyline)
                                                            {
                                                                isPolyline = true;
                                                            }
                                                            else 
                                                            {
                                                                isPolyline =  IsThisWayALine(memberItem.@ref, osmLineFeatureClass, lineSQLIdentifier, osmPolygonFeatureClass, polygonSQLIdentifier); 
                                                            }

                                                            if (!wayList.ContainsKey(memberItem.@ref))
                                                                if (isPolyline)
                                                                    wayList.Add(memberItem.@ref, "line");
                                                                else
                                                                    wayList.Add(memberItem.@ref, "polygon");

                                                            if (isPolyline)
                                                            {
                                                                if (osmLineList == null)
                                                                {
                                                                    osmLineList = new List<OSMLineFeature>();
                                                                }

                                                                OSMLineFeature lineFeature = new OSMLineFeature();
                                                                lineFeature.relationList = new List<string>();
                                                                lineFeature.lineID = memberItem.@ref;

                                                                if (detectedGeometryType == esriGeometryType.esriGeometryPolygon)
                                                                {
                                                                    lineFeature.relationList.Add(currentRelation.id + "_ply");
                                                                }
                                                                else if (detectedGeometryType == esriGeometryType.esriGeometryPolyline)
                                                                {
                                                                    lineFeature.relationList.Add(currentRelation.id + "_ln");
                                                                }
                                                                else
                                                                {
                                                                    lineFeature.relationList.Add(currentRelation.id + "_rel");
                                                                }

                                                                osmLineList.Add(lineFeature);
                                                            }
                                                            else
                                                            {
                                                                if (osmPolygonList == null)
                                                                {
                                                                    osmPolygonList = new List<OSMPolygonFeature>();
                                                                }

                                                                OSMPolygonFeature polygonFeature = new OSMPolygonFeature();
                                                                polygonFeature.relationList = new List<string>();
                                                                polygonFeature.polygonID = memberItem.@ref;

                                                                if (detectedGeometryType == esriGeometryType.esriGeometryPolygon)
                                                                {
                                                                    polygonFeature.relationList.Add(currentRelation.id + "_ply");
                                                                }
                                                                else if (detectedGeometryType == esriGeometryType.esriGeometryPolyline)
                                                                {
                                                                    polygonFeature.relationList.Add(currentRelation.id + "_ln");
                                                                }
                                                                else
                                                                {
                                                                    polygonFeature.relationList.Add(currentRelation.id + "_rel");
                                                                }

                                                                osmPolygonList.Add(polygonFeature);
                                                            }

                                                            break;
                                                        case memberType.node:

                                                            if (osmPointList == null)
                                                            {
                                                                osmPointList = new List<OSMNodeFeature>();
                                                            }

                                                            OSMNodeFeature nodeFeature = new OSMNodeFeature();
                                                            nodeFeature.relationList = new List<string>();
                                                            nodeFeature.nodeID = memberItem.@ref;

                                                            nodeFeature.relationList.Add(currentRelation.id + "_rel");

                                                            osmPointList.Add(nodeFeature);

                                                            break;
                                                        case memberType.relation:

                                                            // check the referenced item on the relation
                                                            // - the referenced relation still be either line or polygon
                                                            isPolyline = true;
                                                            if (detectedGeometryType == esriGeometryType.esriGeometryPolyline)
                                                            {
                                                                isPolyline = true;
                                                            }
                                                            else
                                                            {
                                                                isPolyline = IsThisWayALine(memberItem.@ref, osmLineFeatureClass, lineSQLIdentifier, osmPolygonFeatureClass, polygonSQLIdentifier);
                                                            }

                                                            if (!wayList.ContainsKey(memberItem.@ref))
                                                                if (isPolyline)
                                                                    wayList.Add(memberItem.@ref, "line");
                                                                else
                                                                    wayList.Add(memberItem.@ref, "polygon");

                                                            if (osmRelationList == null)
                                                            {
                                                                osmRelationList = new List<OSMRelation>();
                                                            }

                                                            OSMRelation relation = new OSMRelation();
                                                            relation.relationList = new List<string>();
                                                            relation.relationID = memberItem.@ref;
                                                            relation.relationList.Add(currentRelation.id + "_rel");

                                                            break;
                                                        default:
                                                            break;
                                                    }

                                                }
                                                else if (item is tag)
                                                {
                                                    relationTagList.Add((tag)item);
                                                }
                                            }

                                            // if there is a defined geometry type use it to generate a multipart geometry
                                            if (detectedGeometryType == esriGeometryType.esriGeometryPolygon)
                                            {
                                                #region create multipart polygon geometry
                                                //IFeature mpFeature = osmPolygonFeatureClass.CreateFeature();

                                                IPolygon relationMPPolygon = new PolygonClass();
                                                relationMPPolygon.SpatialReference = ((IGeoDataset)osmPolygonFeatureClass).SpatialReference;

                                                ISegmentCollection relationPolygonGeometryCollection = relationMPPolygon as ISegmentCollection;

                                                IQueryFilter osmIDQueryFilter = new QueryFilterClass();
                                                string sqlPolyOSMID = osmPolygonFeatureClass.SqlIdentifier("OSMID");
                                                string sqlLineOSMID = osmLineFeatureClass.SqlIdentifier("OSMID");
                                                object missing = Type.Missing;
                                                bool relationComplete = true;

                                                // loop through the list of referenced ways that are listed in a relation
                                                // for each of the items we need to make a decision if they have merit to qualify as stand-alone features
                                                // due to the presence of meaningful attributes (tags) <-- changed TE 10/14/2014
                                                foreach (KeyValuePair<string, string> wayKey in wayList)
                                                {
                                                    if (relationComplete == false)
                                                        break;

                                                    if (TrackCancel.Continue() == false)
                                                    {
                                                        return missingRelations;
                                                    }

                                                    switch (wayKey.Value)
                                                    {
                                                        case "line":
                                                            osmIDQueryFilter.WhereClause = sqlLineOSMID + " = '" + wayKey.Key + "'";
                                                            break;
                                                        case "polygon":
                                                            osmIDQueryFilter.WhereClause = sqlPolyOSMID + " = '" + wayKey.Key + "'";
                                                            break;
                                                        default:
                                                            break;
                                                    }
                                                    

                                                    System.Diagnostics.Debug.WriteLine("Relation (Polygon) #: " + relationDebugCount + " :___: " + currentRelation.id + " :___: " + wayKey.Key);

                                                    using (ComReleaser relationComReleaser = new ComReleaser())
                                                    {
                                                        IFeatureCursor featureCursor = null;
                                                        switch (wayKey.Value)
                                                        {
                                                            case "line":
                                                                featureCursor = osmLineFeatureClass.Search(osmIDQueryFilter, false);
                                                                break;
                                                            case "polygon":
                                                                featureCursor = osmPolygonFeatureClass.Search(osmIDQueryFilter, false);
                                                                break;
                                                            default:
                                                                break;
                                                        }
                                                            
                                                        relationComReleaser.ManageLifetime(featureCursor);

                                                        IFeature partFeature = featureCursor.NextFeature();

                                                        // set the appropriate field attribute to become invisible as a standalone features
                                                        if (partFeature != null)
                                                        {
                                                            IGeometryCollection ringCollection = partFeature.Shape as IGeometryCollection;

                                                            // test for available content in the geometry collection  
                                                            if (ringCollection.GeometryCount > 0)
                                                            {
                                                                // test if we dealing with a valid geometry
                                                                if (ringCollection.get_Geometry(0).IsEmpty == false)
                                                                {
                                                                    // add it to the new geometry and mark the added geometry as a supporting element
                                                                    relationPolygonGeometryCollection.AddSegmentCollection((ISegmentCollection)ringCollection.get_Geometry(0));

                                                                    // TE - 10/14/2014
                                                                    // the initial assessment if the feature is a supporting element based on the existence of tags
                                                                    // has been made, at this point I don't think there is a reason to reassess the nature of feature
                                                                    // based on its appearance in a relation
                                                                    //if (osmSupportingElementPolygonFieldIndex > -1)
                                                                    //{
                                                                    //    // if the member of a relation has the role of "inner" and it has tags, then let's keep it
                                                                    //    // as a standalone feature as well
                                                                    //    // the geometry is then a hole in the relation but due to the tags it also has merits to be
                                                                    //    // considered a stand-alone feature
                                                                    //    if (wayKey.Value.ToLower().Equals("inner"))
                                                                    //    {
                                                                    //        if (!_osmUtility.DoesHaveKeys(partFeature, tagCollectionPolygonFieldIndex, null))
                                                                    //        {
                                                                    //            partFeature.set_Value(osmSupportingElementPolygonFieldIndex, "yes");
                                                                    //        }
                                                                    //    }
                                                                    //    else
                                                                    //    {
                                                                    //        // relation member without an explicit role or the role of "outer" are turned into
                                                                    //        // supporting features if they don't have relevant attribute
                                                                    //        if (!_osmUtility.DoesHaveKeys(partFeature, tagCollectionPolylineFieldIndex, null))
                                                                    //        {
                                                                    //            partFeature.set_Value(osmSupportingElementPolygonFieldIndex, "yes");
                                                                    //        }
                                                                    //    }
                                                                    //}

                                                                    //partFeature.Store();
                                                                }
                                                            }
                                                        //}
                                                        //else
                                                        //{
                                                        //    // it still can be a line geometry that will be pieced together into a polygon
                                                        //    IFeatureCursor lineFeatureCursor = osmLineFeatureClass.Search(osmIDQueryFilter, false);
                                                        //    relationComReleaser.ManageLifetime(lineFeatureCursor);

                                                        //    partFeature = lineFeatureCursor.NextFeature();

                                                        //    if (partFeature != null)
                                                        //    {
                                                        //        IGeometryCollection ringCollection = partFeature.Shape as IGeometryCollection;

                                                        //        // test for available content in the geometry collection  
                                                        //        if (ringCollection.GeometryCount > 0)
                                                        //        {
                                                        //            // test if we dealing with a valid geometry
                                                        //            if (ringCollection.get_Geometry(0).IsEmpty == false)
                                                        //            {
                                                        //                // add it to the new geometry and mark the added geometry as a supporting element
                                                        //                relationPolygonGeometryCollection.AddSegmentCollection((ISegmentCollection)ringCollection.get_Geometry(0));

                                                        //                // TE - 10/14/2014 -- see comment above
                                                        //                //if (osmSupportingElementPolylineFieldIndex > -1)
                                                        //                //{
                                                        //                //    // if the member of a relation has the role of "inner" and it has tags, then let's keep it
                                                        //                //    // as a standalone feature as well
                                                        //                //    // the geometry is then a hole in the relation but due to the tags it also has merits to be
                                                        //                //    // considered a stand-alone feature
                                                        //                //    if (wayKey.Value.ToLower().Equals("inner"))
                                                        //                //    {
                                                        //                //        if (!_osmUtility.DoesHaveKeys(partFeature, tagCollectionPolylineFieldIndex, null))
                                                        //                //        {
                                                        //                //            partFeature.set_Value(osmSupportingElementPolylineFieldIndex, "yes");
                                                        //                //        }
                                                        //                //    }
                                                        //                //    else
                                                        //                //    {
                                                        //                //        // relation member without an explicit role or the role of "outer" are turned into
                                                        //                //        // supporting features if they don't have relevant attribute
                                                        //                //        if (!_osmUtility.DoesHaveKeys(partFeature, tagCollectionPolylineFieldIndex, null))
                                                        //                //        {
                                                        //                //            partFeature.set_Value(osmSupportingElementPolylineFieldIndex, "yes");
                                                        //                //        }
                                                        //                //    }
                                                        //                //}

                                                        //                //partFeature.Store();
                                                        //            }
                                                        //        }
                                                            }
                                                            else
                                                            {
                                                                relationComplete = false;
                                                                continue;
                                                            }
                                                        //}
                                                    }
                                                }

                                                // mark the relation as incomplete
                                                if (relationComplete == false)
                                                {
                                                    missingRelations.Add(currentRelation.id);
                                                    continue;
                                                }

                                                // transform the added collections for geometries into a topological correct geometry representation
                                                ((IPolygon4)relationMPPolygon).SimplifyEx(true, false, true);

                                                polygonFeatureBuffer.Shape = relationMPPolygon;

                                                if (_osmUtility.DoesHaveKeys(currentRelation))
                                                {
                                                }
                                                else
                                                {
                                                    relationTagList = MergeTagsFromOuterPolygonToRelation(currentRelation, osmPolygonFeatureClass);
                                                }

                                                insertTags(osmPolygonDomainAttributeFieldIndices, osmPolygonDomainAttributeFieldLength, tagCollectionPolygonFieldIndex, polygonFeatureBuffer, relationTagList.ToArray());

                                                if (fastLoad == false)
                                                {
                                                    if (osmMembersPolygonFieldIndex > -1)
                                                    {
                                                        _osmUtility.insertMembers(osmMembersPolygonFieldIndex, (IFeature)polygonFeatureBuffer, relationMemberList.ToArray());
                                                    }

                                                    // store the administrative attributes
                                                    // user, uid, version, changeset, timestamp, visible
                                                    if (osmUserPolygonFieldIndex != -1)
                                                    {
                                                        if (!String.IsNullOrEmpty(currentRelation.user))
                                                        {
                                                            polygonFeatureBuffer.set_Value(osmUserPolygonFieldIndex, currentRelation.user);
                                                        }
                                                    }

                                                    if (osmUIDPolygonFieldIndex != -1)
                                                    {
                                                        if (!String.IsNullOrEmpty(currentRelation.uid))
                                                        {
                                                            polygonFeatureBuffer.set_Value(osmUIDPolygonFieldIndex, Convert.ToInt32(currentRelation.uid));
                                                        }
                                                    }

                                                    if (osmVisiblePolygonFieldIndex != -1)
                                                    {
                                                        if (String.IsNullOrEmpty(currentRelation.visible) == false)
                                                        {
                                                            polygonFeatureBuffer.set_Value(osmVisiblePolygonFieldIndex, currentRelation.visible.ToString());
                                                        }
                                                        else
                                                        {
                                                            polygonFeatureBuffer.set_Value(osmVisiblePolygonFieldIndex, "unknown");
                                                        }
                                                    }

                                                    if (osmVersionPolygonFieldIndex != -1)
                                                    {
                                                        if (!String.IsNullOrEmpty(currentRelation.version))
                                                        {
                                                            polygonFeatureBuffer.set_Value(osmVersionPolygonFieldIndex, Convert.ToInt32(currentRelation.version));
                                                        }
                                                    }

                                                    if (osmChangesetPolygonFieldIndex != -1)
                                                    {
                                                        if (!String.IsNullOrEmpty(currentRelation.changeset))
                                                        {
                                                            polygonFeatureBuffer.set_Value(osmChangesetPolygonFieldIndex, Convert.ToInt32(currentRelation.changeset));
                                                        }
                                                    }

                                                    if (osmTimeStampPolygonFieldIndex != -1)
                                                    {
                                                        if (!String.IsNullOrEmpty(currentRelation.timestamp))
                                                        {
                                                            polygonFeatureBuffer.set_Value(osmTimeStampPolygonFieldIndex, Convert.ToDateTime(currentRelation.timestamp));
                                                        }
                                                    }

                                                    if (osmPolygonIDFieldIndex != -1)
                                                    {
                                                        polygonFeatureBuffer.set_Value(osmPolygonIDFieldIndex, currentRelation.id);
                                                    }

                                                    if (osmSupportingElementPolygonFieldIndex > -1)
                                                    {
                                                        polygonFeatureBuffer.set_Value(osmSupportingElementPolygonFieldIndex, "no");
                                                    }
                                                }

                                                try
                                                {
                                                    //mpFeature.Store();
                                                    polygonFeatureInsertCursor.InsertFeature(polygonFeatureBuffer);
                                                }
                                                catch (Exception ex)
                                                {
                                                    message.AddWarning(ex.Message);
                                                }
                                                #endregion
                                            }
                                            else if (detectedGeometryType == esriGeometryType.esriGeometryPolyline)
                                            {
                                                #region create multipart polyline geometry
                                                //IFeature mpFeature = osmLineFeatureClass.CreateFeature();

                                                IPolyline relationMPPolyline = new PolylineClass();
                                                relationMPPolyline.SpatialReference = ((IGeoDataset)osmLineFeatureClass).SpatialReference;

                                                IGeometryCollection relationPolylineGeometryCollection = relationMPPolyline as IGeometryCollection;

                                                IQueryFilter osmIDQueryFilter = new QueryFilterClass();
                                                object missing = Type.Missing;

                                                // loop through the 
                                                foreach (KeyValuePair<string, string> wayKey in wayList)
                                                {
                                                    if (TrackCancel.Continue() == false)
                                                    {
                                                        return missingRelations;
                                                    }

                                                    osmIDQueryFilter.WhereClause = osmLineFeatureClass.WhereClauseByExtensionVersion(wayKey.Key, "OSMID", 2);

                                                    System.Diagnostics.Debug.WriteLine("Relation (Polyline) #: " + relationDebugCount + " :___: " + currentRelation.id + " :___: " + wayKey);

                                                    using (ComReleaser relationComReleaser = new ComReleaser())
                                                    {
                                                        IFeatureCursor featureCursor = osmLineFeatureClass.Search(osmIDQueryFilter, false);
                                                        relationComReleaser.ManageLifetime(featureCursor);

                                                        IFeature partFeature = featureCursor.NextFeature();

                                                        // set the appropriate field attribute to become invisible as a standalone features
                                                        if (partFeature != null)
                                                        {
                                                            if (partFeature.Shape.IsEmpty == false)
                                                            {
                                                                IGeometryCollection pathCollection = partFeature.Shape as IGeometryCollection;
                                                                relationPolylineGeometryCollection.AddGeometry(pathCollection.get_Geometry(0), ref missing, ref missing);

                                                                // TE - 10/14/2014 - see comment above
                                                                //if (osmSupportingElementPolylineFieldIndex > -1)
                                                                //{
                                                                //    if (!_osmUtility.DoesHaveKeys(partFeature, tagCollectionPolylineFieldIndex, null))
                                                                //    {
                                                                //        partFeature.set_Value(osmSupportingElementPolylineFieldIndex, "yes");
                                                                //    }
                                                                //}

                                                                //partFeature.Store();
                                                            }
                                                        }
                                                    }
                                                }

                                                lineFeatureBuffer.Shape = relationMPPolyline;

                                                insertTags(osmLineDomainAttributeFieldIndices, osmLineDomainAttributeFieldLength, tagCollectionPolylineFieldIndex, lineFeatureBuffer, relationTagList.ToArray());

                                                if (fastLoad == false)
                                                {
                                                    if (osmMembersPolylineFieldIndex > -1)
                                                    {
                                                        _osmUtility.insertMembers(osmMembersPolylineFieldIndex, (IFeature)lineFeatureBuffer, relationMemberList.ToArray());
                                                    }

                                                    // store the administrative attributes
                                                    // user, uid, version, changeset, timestamp, visible
                                                    if (osmUserPolylineFieldIndex != -1)
                                                    {
                                                        if (!String.IsNullOrEmpty(currentRelation.user))
                                                        {
                                                            lineFeatureBuffer.set_Value(osmUserPolylineFieldIndex, currentRelation.user);
                                                        }
                                                    }

                                                    if (osmUIDPolylineFieldIndex != -1)
                                                    {
                                                        if (!String.IsNullOrEmpty(currentRelation.uid))
                                                        {
                                                            lineFeatureBuffer.set_Value(osmUIDPolylineFieldIndex, Convert.ToInt32(currentRelation.uid));
                                                        }
                                                    }

                                                    if (osmVisiblePolylineFieldIndex != -1)
                                                    {
                                                        if (String.IsNullOrEmpty(currentRelation.visible) == false)
                                                        {
                                                            lineFeatureBuffer.set_Value(osmVisiblePolylineFieldIndex, currentRelation.visible.ToString());
                                                        }
                                                        else
                                                        {
                                                            lineFeatureBuffer.set_Value(osmVisiblePolylineFieldIndex, "unknown");
                                                        }
                                                    }

                                                    if (osmVersionPolylineFieldIndex != -1)
                                                    {
                                                        if (!String.IsNullOrEmpty(currentRelation.version))
                                                        {
                                                            lineFeatureBuffer.set_Value(osmVersionPolylineFieldIndex, Convert.ToInt32(currentRelation.version));
                                                        }
                                                    }

                                                    if (osmChangesetPolylineFieldIndex != -1)
                                                    {
                                                        if (!String.IsNullOrEmpty(currentRelation.changeset))
                                                        {
                                                            lineFeatureBuffer.set_Value(osmChangesetPolylineFieldIndex, Convert.ToInt32(currentRelation.changeset));
                                                        }
                                                    }

                                                    if (osmTimeStampPolylineFieldIndex != -1)
                                                    {
                                                        if (!String.IsNullOrEmpty(currentRelation.timestamp))
                                                        {
                                                            lineFeatureBuffer.set_Value(osmTimeStampPolylineFieldIndex, Convert.ToDateTime(currentRelation.timestamp));
                                                        }
                                                    }

                                                    if (osmLineIDFieldIndex != -1)
                                                    {
                                                        lineFeatureBuffer.set_Value(osmLineIDFieldIndex, currentRelation.id);
                                                    }

                                                    if (osmSupportingElementPolylineFieldIndex > -1)
                                                    {
                                                        lineFeatureBuffer.set_Value(osmSupportingElementPolylineFieldIndex, "no");
                                                    }
                                                }

                                                try
                                                {
                                                    lineFeatureInsertCursor.InsertFeature(lineFeatureBuffer);
                                                }
                                                catch (Exception ex)
                                                {
                                                    message.AddWarning(ex.Message);
                                                }
                                                #endregion

                                            }
                                            else if (detectedGeometryType == esriGeometryType.esriGeometryPoint)
                                            {
                                                System.Diagnostics.Debug.WriteLine("Relation #: " + relationDebugCount + " :____: POINT!!!");

                                                if (TrackCancel.Continue() == false)
                                                {
                                                    return missingRelations;
                                                }
                                            }
                                            else
                                            // otherwise it is relation that needs to be dealt with separately
                                            {
                                                if (TrackCancel.Continue() == false)
                                                {
                                                    return missingRelations;
                                                }


                                                System.Diagnostics.Debug.WriteLine("Relation #: " + relationDebugCount + " :____: Kept as relation");

                                                if (tagCollectionRelationFieldIndex != -1)
                                                {
                                                    _osmUtility.insertOSMTags(tagCollectionRelationFieldIndex, rowBuffer, relationTagList.ToArray());
                                                }

                                                if (fastLoad == false)
                                                {
                                                    if (osmMembersRelationFieldIndex != -1)
                                                    {
                                                        _osmUtility.insertMembers(osmMembersRelationFieldIndex, rowBuffer, relationMemberList.ToArray());
                                                    }

                                                    // store the administrative attributes
                                                    // user, uid, version, changeset, timestamp, visible
                                                    if (osmUserRelationFieldIndex != -1)
                                                    {
                                                        if (!String.IsNullOrEmpty(currentRelation.user))
                                                        {
                                                            rowBuffer.set_Value(osmUserRelationFieldIndex, currentRelation.user);
                                                        }
                                                    }

                                                    if (osmUIDRelationFieldIndex != -1)
                                                    {
                                                        if (!String.IsNullOrEmpty(currentRelation.uid))
                                                        {
                                                            rowBuffer.set_Value(osmUIDRelationFieldIndex, Convert.ToInt64(currentRelation.uid));
                                                        }
                                                    }

                                                    if (osmVisibleRelationFieldIndex != -1)
                                                    {
                                                        if (currentRelation.visible != null)
                                                        {
                                                            rowBuffer.set_Value(osmVisibleRelationFieldIndex, currentRelation.visible.ToString());
                                                        }
                                                    }

                                                    if (osmVersionRelationFieldIndex != -1)
                                                    {
                                                        if (!String.IsNullOrEmpty(currentRelation.version))
                                                        {
                                                            rowBuffer.set_Value(osmVersionRelationFieldIndex, Convert.ToInt32(currentRelation.version));
                                                        }
                                                    }

                                                    if (osmChangesetRelationFieldIndex != -1)
                                                    {
                                                        if (!String.IsNullOrEmpty(currentRelation.changeset))
                                                        {
                                                            rowBuffer.set_Value(osmChangesetRelationFieldIndex, Convert.ToInt32(currentRelation.changeset));
                                                        }
                                                    }

                                                    if (osmTimeStampRelationFieldIndex != -1)
                                                    {
                                                        if (!String.IsNullOrEmpty(currentRelation.timestamp))
                                                        {
                                                            try
                                                            {
                                                                rowBuffer.set_Value(osmTimeStampRelationFieldIndex, Convert.ToDateTime(currentRelation.timestamp));
                                                            }
                                                            catch (Exception ex)
                                                            {
                                                                message.AddWarning(String.Format(_resourceManager.GetString("GPTools_OSMGPFileReader_invalidTimeFormat"), ex.Message));
                                                            }
                                                        }
                                                    }

                                                    if (osmRelationIDFieldIndex != -1)
                                                    {
                                                        rowBuffer.set_Value(osmRelationIDFieldIndex, currentRelation.id);
                                                    }
                                                }

                                                try
                                                {
                                                    rowCursor.InsertRow(rowBuffer);
                                                    relationCount = relationCount + 1;

                                                    relationIndexRebuildRequired = true;
                                                }
                                                catch (Exception ex)
                                                {
                                                    System.Diagnostics.Debug.WriteLine(ex.Message);
                                                }

                                                // check for user interruption
                                                if (TrackCancel.Continue() == false)
                                                {
                                                    return missingRelations;
                                                }
                                            }

                                            // update the isMemberOf fields of the attached features
                                            if (osmPointList != null)
                                            {
                                                foreach (OSMNodeFeature nodeFeature in osmPointList)
                                                {
                                                    updateIsMemberOf(osmLineFeatureClass, osmMemberOfPolylineFieldIndex, nodeFeature.nodeID, nodeFeature.relationList);
                                                }
                                            }

                                            if (osmLineList != null)
                                            {
                                                foreach (OSMLineFeature lineFeature in osmLineList)
                                                {
                                                    updateIsMemberOf(osmLineFeatureClass, osmMemberOfPolylineFieldIndex, lineFeature.lineID, lineFeature.relationList);
                                                }
                                            }

                                            if (osmPolygonList != null)
                                            {
                                                foreach (OSMPolygonFeature polygonFeature in osmPolygonList)
                                                {
                                                    updateIsMemberOf(osmLineFeatureClass, osmMemberOfPolylineFieldIndex, polygonFeature.polygonID, polygonFeature.relationList);
                                                }
                                            }

                                            if (stepProgressor != null)
                                            {
                                                stepProgressor.Position = relationCount;
                                            }

                                            if ((relationCount % 50000) == 0)
                                            {
                                                message.AddMessage(String.Format(_resourceManager.GetString("GPTools_OSMGPFileReader_relationsloaded"), relationCount));
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            message.AddWarning(ex.Message);
                                        }
                                        finally
                                        {
                                            if (rowBuffer != null)
                                            {
                                                Marshal.ReleaseComObject(rowBuffer);
                                                rowBuffer = null;
                                            }
                                            if (lineFeatureBuffer != null)
                                            {
                                                Marshal.ReleaseComObject(lineFeatureBuffer);
                                                lineFeatureBuffer = null;
                                            }

                                            if (polygonFeatureBuffer != null)
                                            {
                                                Marshal.ReleaseComObject(polygonFeatureBuffer);
                                                polygonFeatureBuffer = null;
                                            }

                                            currentRelation = null;
                                        }
                                    }
                                }
                            }

                            // close the OSM file
                            osmFileXmlReader.Close();

                            // flush any remaining entities from the cursor
                            rowCursor.Flush();
                            polygonFeatureInsertCursor.Flush();
                            lineFeatureInsertCursor.Flush();

                            // force a garbage collection
                            System.GC.Collect();

                            // let the user know that we are done dealing with the relations
                            message.AddMessage(String.Format(_resourceManager.GetString("GPTools_OSMGPFileReader_relationsloaded"), relationCount));
                        }
                    }

                    if (stepProgressor != null)
                    {
                        stepProgressor.Hide();
                    }

                    // Addd index for osmid column as well
                    IGeoProcessor2 geoProcessor = new GeoProcessorClass();
                    bool storedOriginalLocal = geoProcessor.AddOutputsToMap;
                    IGPUtilities3 gpUtilities3 = new GPUtilitiesClass();

                    try
                    {
                        geoProcessor.AddOutputsToMap = false;

                        if (relationIndexRebuildRequired)
                        {
                            IIndexes tableIndexes = relationTable.Indexes;
                            int indexPosition = -1;
                            tableIndexes.FindIndex("osmID_IDX", out indexPosition);

                            if (indexPosition == -1)
                            {
                                IGPValue relationTableGPValue = gpUtilities3.MakeGPValueFromObject(relationTable);
                                string sddd = targetGPValue.GetAsText();
                                string tableLocation = GetLocationString(targetGPValue, relationTable);
                                IVariantArray parameterArrary = CreateAddIndexParameterArray(tableLocation, "OSMID", "osmID_IDX", "UNIQUE", "");
                                IGeoProcessorResult2 gpResults2 = geoProcessor.Execute("AddIndex_management", parameterArrary, TrackCancel) as IGeoProcessorResult2;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        message.AddWarning(ex.Message);
                    }
                    finally
                    {
                        geoProcessor.AddOutputsToMap = storedOriginalLocal;

                        Marshal.FinalReleaseComObject(gpUtilities3);
                        Marshal.FinalReleaseComObject(geoProcessor);
                    }
                }
            }
            catch (Exception)
            {
            }
            finally
            {
                if (relationSerializer != null)
                    relationSerializer = null;

                if (osmFileXmlReader != null)
                    osmFileXmlReader = null;

                System.GC.Collect();
                System.GC.WaitForPendingFinalizers();
            }

            return missingRelations;
        }

        private void updateIsMemberOf(IFeatureClass osmLineFeatureClass, int osmMemberOfPolylineFieldIndex, string osmID, List<string> parentList)
        {
            using (ComReleaser comReleaserInternal = new ComReleaser())
            {
                IQueryFilter lineQueryFilter = new QueryFilterClass();
                lineQueryFilter.WhereClause = osmLineFeatureClass.WhereClauseByExtensionVersion(osmID, "OSMID", 2);
                IFeatureCursor searchCursor = osmLineFeatureClass.Update(lineQueryFilter, false);
                comReleaserInternal.ManageLifetime(searchCursor);

                IFeature featureToModify = searchCursor.NextFeature();
                if (featureToModify != null)
                {
                    List<string> isMemberOfList = _osmUtility.retrieveIsMemberOf(featureToModify, osmMemberOfPolylineFieldIndex);

                    // create a union of both lists
                    List<string> unionedMemberOfList = new List<string>(isMemberOfList.Union(parentList));

                    isMemberOfList.AddRange(unionedMemberOfList);

                    _osmUtility.insertIsMemberOf(osmMemberOfPolylineFieldIndex, unionedMemberOfList, featureToModify);

                    searchCursor.UpdateFeature(featureToModify);

                    if (featureToModify != null)
                        Marshal.ReleaseComObject(featureToModify);
                }
            }
        }

        internal bool IsThisWayALine(string osmID, IFeatureClass lineFeatureClass, string lineQueryIdentifier, IFeatureClass polygonFeatureClass, string polygonQueryIdentifier)
        {
            bool isALine = true;

            using (ComReleaser comReleaser = new ComReleaser())
            {
                IQueryFilter polygonQueryFilter = new QueryFilterClass();
                polygonQueryFilter.WhereClause = polygonQueryIdentifier + " = '" + osmID + "'";
                IFeatureCursor polygonCursor = polygonFeatureClass.Search(polygonQueryFilter, false);
                comReleaser.ManageLifetime(polygonCursor);

                IFeature polyFeature = polygonCursor.NextFeature();

                if (polyFeature != null)
                {
                    isALine = false;
                }
            }

            return isALine;
        }

        internal List<ESRI.ArcGIS.OSM.OSMClassExtension.tag> MergeTagsFromOuterPolygonToRelation(ESRI.ArcGIS.OSM.OSMClassExtension.relation currentRelation, IFeatureClass polygonFeatureClass)
        {
            Dictionary<string, ESRI.ArcGIS.OSM.OSMClassExtension.tag> mergedTagList = new Dictionary<string, ESRI.ArcGIS.OSM.OSMClassExtension.tag>();

            IQueryFilter osmIDQueryFilter = new QueryFilterClass();

            try
            {
                int osmIDPolygonFieldIndex = polygonFeatureClass.FindField("OSMID");
                string sqlPolyOSMID = polygonFeatureClass.SqlIdentifier("OSMID");

                foreach (var relationItem in currentRelation.Items)
                {
                    if (relationItem is ESRI.ArcGIS.OSM.OSMClassExtension.member)
                    {
                        ESRI.ArcGIS.OSM.OSMClassExtension.member currentRelationMember = relationItem as ESRI.ArcGIS.OSM.OSMClassExtension.member;

                        if (currentRelationMember.role.ToLower().Equals("outer"))
                        {
                            using (ComReleaser comReleaser = new ComReleaser())
                            {
                                osmIDQueryFilter.WhereClause = sqlPolyOSMID + " = " + currentRelationMember.@ref;

                                IFeatureCursor featureCursor = polygonFeatureClass.Search(osmIDQueryFilter, false);
                                comReleaser.ManageLifetime(featureCursor);

                                IFeature foundPolygonFeature = featureCursor.NextFeature();

                                if (foundPolygonFeature == null)
                                    continue;

                                tag[] foundTags = _osmUtility.retrieveOSMTags(foundPolygonFeature, osmIDPolygonFieldIndex, ((IDataset)polygonFeatureClass).Workspace);

                                foreach (tag currentWayTag in foundTags)
                                {
                                    // first one in wins
                                    try
                                    {
                                        if (!mergedTagList.ContainsKey(currentWayTag.k))
                                        {
                                            mergedTagList.Add(currentWayTag.k, currentWayTag);
                                        }
                                    }
                                    catch { }
                                }
                            }
                        }
                    }
                    else if (relationItem is tag)
                    {
                        tag relationTag = relationItem as tag;

                        try
                        {
                            if (!mergedTagList.ContainsKey(relationTag.k))
                            {
                                mergedTagList.Add(relationTag.k, relationTag);
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }

            return mergedTagList.Values.ToList();
        }
     
        public static string convert2OSMKey(string attributeFieldName, string IllegalCharacters)
        {
            string osmKey = attributeFieldName.Substring(4);

            // older encodings until version 2.0
            osmKey = osmKey.Replace("__", ":");
            osmKey = osmKey.Replace("_b_", " ");
            osmKey = osmKey.Replace("_d_", ".");
            osmKey = osmKey.Replace("_c_", ",");
            osmKey = osmKey.Replace("_sc_", ";");

            // ensure to safely encode all illegal SQL characters
            if (!String.IsNullOrEmpty(IllegalCharacters))
            {
                char[] illegals = IllegalCharacters.ToCharArray();
                foreach (char offender in illegals)
                {
                    osmKey = osmKey.Replace("_" + ((int)offender).ToString() + "_", offender.ToString());
                }
            }

            return osmKey.ToString();
        }

        public static string convert2AttributeFieldName(string OSMKey, string IllegalCharacters)
        {
            string attributeFieldName = OSMKey;

            // ensure to safely encode all illegal SQL characters
            if (!String.IsNullOrEmpty(IllegalCharacters))
            {
                char[] illegals = IllegalCharacters.ToCharArray();
                foreach (char offender in illegals)
                {
                    attributeFieldName = attributeFieldName.Replace(offender.ToString(), "_" + ((int)offender).ToString() + "_");
                }
            }

            attributeFieldName = "osm_" + attributeFieldName;

            return attributeFieldName;
        }

        public static bool IsThisWayALine(way currentway)
        {
            bool isALine = true;

            try
            {
                if (currentway.nd != null)
                {
                    if (currentway.nd[0].@ref == currentway.nd[currentway.nd.Length - 1].@ref)
                    {
                        isALine = false;
                    }
                    else
                    {
                        isALine = true;
                    }
                }

                // coastlines are special cases and we will accept them as lines only
                bool isCoastline = false;

                if (currentway.tag != null)
                {
                    tag coastlineTag = new tag();
                    coastlineTag.k = "natural";
                    coastlineTag.v = "coastline";


                    tag areaTag = new tag();
                    areaTag.k = "area";
                    areaTag.v = "yes";

                    tag highwayTag = new tag();
                    highwayTag.k = "highway";
                    highwayTag.v = "something";

                    if (currentway.tag.Contains(coastlineTag, new TagKeyValueComparer()))
                    {
                        isCoastline = true;
                        isALine = true;
                    }

                    if (currentway.tag.Contains(highwayTag, new TagKeyComparer()))
                    {
                        isALine = true;
                    }

                    if (currentway.tag.Contains(areaTag, new TagKeyValueComparer()))
                    {
                        if (isCoastline == false)
                        {
                            isALine = false;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
            }

            return isALine;
        }

        // split to node ids into manageable chunks of db requests
        internal List<string> SplitOSMIDRequests(ESRI.ArcGIS.OSM.OSMClassExtension.way currentway, int extensionVersion)
        {
            List<string> osmIDRequests = new List<string>();

            if (currentway == null)
                return osmIDRequests;

            if (currentway.nd == null)
                return osmIDRequests;

            try
            {
                StringBuilder newQueryString = new StringBuilder();
                newQueryString.Append("(");

                foreach (nd currentNode in currentway.nd)
                {
                    if (extensionVersion == 1)
                        newQueryString.Append(currentNode.@ref + ",");
                    else if (extensionVersion == 2)
                    {
                        newQueryString.Append("'");
                        newQueryString.Append(currentNode.@ref);
                        newQueryString.Append("'");
                        newQueryString.Append(",");
                    }

                    // not too sure about this hard coded length of 6500
                    // since the SQL implementation is data source dependent
                    if (newQueryString.Length > 6500)
                    {
                        newQueryString = newQueryString.Remove(newQueryString.Length - 1, 1);

                        newQueryString.Append(")");
                        osmIDRequests.Add(newQueryString.ToString());

                        newQueryString = new StringBuilder();
                        newQueryString.Append("(");
                    }
                }

                if (newQueryString.Length > 2)
                {
                    newQueryString = newQueryString.Remove(newQueryString.Length - 1, 1);
                    newQueryString.Append(")");
                    osmIDRequests.Add(newQueryString.ToString());
                }
            }
            catch
            {
            }

            return osmIDRequests;
        }

        //internal List<ESRI.ArcGIS.OSM.OSMClassExtension.tag> MergeTagsFromOuterPolygonToRelation(ESRI.ArcGIS.OSM.OSMClassExtension.relation currentRelation, IFeatureClass polygonFeatureClass)
        //{
        //    Dictionary<string, ESRI.ArcGIS.OSM.OSMClassExtension.tag> mergedTagList = new Dictionary<string, ESRI.ArcGIS.OSM.OSMClassExtension.tag>();

        //    IQueryFilter osmIDQueryFilter = new QueryFilterClass();

        //    try
        //    {
        //        int osmIDPolygonFieldIndex = polygonFeatureClass.FindField("OSMID");
        //        string sqlPolyOSMID = polygonFeatureClass.SqlIdentifier("OSMID");

        //        foreach (var relationItem in currentRelation.Items)
        //        {
        //            if (relationItem is ESRI.ArcGIS.OSM.OSMClassExtension.member)
        //            {
        //                ESRI.ArcGIS.OSM.OSMClassExtension.member currentRelationMember = relationItem as ESRI.ArcGIS.OSM.OSMClassExtension.member;

        //                if (currentRelationMember.role.ToLower().Equals("outer"))
        //                {
        //                    using (ComReleaser comReleaser = new ComReleaser())
        //                    {
        //                        osmIDQueryFilter.WhereClause = sqlPolyOSMID + " = " + currentRelationMember.@ref;

        //                        IFeatureCursor featureCursor = polygonFeatureClass.Search(osmIDQueryFilter, false);
        //                        comReleaser.ManageLifetime(featureCursor);

        //                        IFeature foundPolygonFeature = featureCursor.NextFeature();

        //                        if (foundPolygonFeature == null)
        //                            continue;

        //                        tag[] foundTags = OSMUtility.retrieveOSMTags(foundPolygonFeature, osmIDPolygonFieldIndex, ((IDataset)polygonFeatureClass).Workspace);

        //                        foreach (tag currentWayTag in foundTags)
        //                        {
        //                            // first one in wins
        //                            try
        //                            {
        //                                if (!mergedTagList.ContainsKey(currentWayTag.k))
        //                                {
        //                                    mergedTagList.Add(currentWayTag.k, currentWayTag);
        //                                }
        //                            }
        //                            catch { }
        //                        }
        //                    }
        //                }
        //            }
        //            else if (relationItem is tag)
        //            {
        //                tag relationTag = relationItem as tag;

        //                try
        //                {
        //                    if (!mergedTagList.ContainsKey(relationTag.k))
        //                    {
        //                        mergedTagList.Add(relationTag.k, relationTag);
        //                    }
        //                }
        //                catch { }
        //            }
        //        }
        //    }
        //    catch { }

        //    return mergedTagList.Values.ToList();
        //}

        internal osmRelationGeometryType determineOSMGeometryType(IFeatureClass lineFeatureClass, IFeatureClass polygonFeatureClass, ITable relationTable, string osmIDtoFind)
        {
            osmRelationGeometryType determinedGeometryType = osmRelationGeometryType.osmUnknownGeometry;

            try
            {
                IQueryFilter osmIDQueryFilter = new QueryFilterClass();
                osmIDQueryFilter.SubFields = "OSMID";

                using (ComReleaser comReleaser = new ComReleaser())
                {
                    if (lineFeatureClass != null)
                    {
                        osmIDQueryFilter.WhereClause = lineFeatureClass.WhereClauseByExtensionVersion(osmIDtoFind, "OSMID", 2);
                        IFeatureCursor lineFeatureCursor = lineFeatureClass.Search(osmIDQueryFilter, false);
                        comReleaser.ManageLifetime(lineFeatureCursor);

                        IFeature foundLineFeature = lineFeatureCursor.NextFeature();
                        if (foundLineFeature != null)
                        {
                            determinedGeometryType = osmRelationGeometryType.osmPolyline;
                            return determinedGeometryType;
                        }

                        osmIDQueryFilter.WhereClause = polygonFeatureClass.WhereClauseByExtensionVersion(osmIDtoFind, "OSMID", 2);
                        IFeatureCursor polygonFeatureCursor = polygonFeatureClass.Search(osmIDQueryFilter, false);
                        comReleaser.ManageLifetime(polygonFeatureCursor);

                        IFeature foundPolygonFeature = polygonFeatureCursor.NextFeature();
                        if (foundPolygonFeature != null)
                        {
                            determinedGeometryType = osmRelationGeometryType.osmPolygon;
                            return determinedGeometryType;
                        }

                        osmIDQueryFilter.WhereClause = relationTable.WhereClauseByExtensionVersion(osmIDtoFind, "OSMID", 2);
                        ICursor relationCursor = relationTable.Search(osmIDQueryFilter, false);
                        comReleaser.ManageLifetime(relationCursor);

                        IRow foundRelation = relationCursor.NextRow();
                        if (foundRelation != null)
                        {
                            // in order to be in the relation table is needs to be a either a super-relation or a mixed type entity (hence hybrid)
                            determinedGeometryType = osmRelationGeometryType.osmHybridGeometry;
                            return determinedGeometryType;
                        }
                    }
                }
            }
            catch
            {
            }

            return determinedGeometryType;
        }

        /// <summary>
        /// determine if the relation is considered a multipart geometry
        /// </summary>
        /// <param name="currentRelation"></param>
        /// <returns></returns>
        internal esriGeometryType determineRelationGeometryType(IFeatureClass osmLineFeatureClass, IFeatureClass osmPolygonFeatureClass, ITable relationTable, ESRI.ArcGIS.OSM.OSMClassExtension.relation currentRelation)
        {
            esriGeometryType detectedGeometryType = esriGeometryType.esriGeometryNull;

            string geoType = String.Empty;
            bool isUniform = true;

            if (currentRelation.Items != null)
            {
                foreach (var item in currentRelation.Items)
                {
                    if (item is ESRI.ArcGIS.OSM.OSMClassExtension.tag)
                    {
                        ESRI.ArcGIS.OSM.OSMClassExtension.tag currentTag = item as ESRI.ArcGIS.OSM.OSMClassExtension.tag;

                        if (currentTag.k.ToUpper().Equals("TYPE"))
                        {
                            if ((currentTag.v.ToUpper().Equals("POLYGON") || currentTag.v.ToUpper().Equals("MULTIPOLYGON") || currentTag.v.ToUpper().Equals("BOUNDARY")))
                            {
                                detectedGeometryType = esriGeometryType.esriGeometryPolygon;
                                // now , it could be argued that the descriptive tag of 'polygon' is not sufficient to ensure 
                                // or guarantuee that the geometry is indeed a 'polygon'
                                return detectedGeometryType;
                            }
                        }
                        // consider administrative boundaries as polygonal in their type
                        else if (currentTag.k.ToUpper().Equals("BOUNDARY"))
                        {
                            if ((currentTag.v.ToUpper().Equals("ADMINISTRATIVE")))
                            {
                                detectedGeometryType = esriGeometryType.esriGeometryPolygon;
                                // now , it could be argued that the descriptive tag of 'polygon' is not sufficient to ensure 
                                // or guarantuee that the geometry is indeed a 'polygon'
                                return detectedGeometryType;
                            }
                        }
                    }
                }
            }

            try
            {
                if (currentRelation.Items != null)
                {
                    foreach (var item in currentRelation.Items)
                    {
                        if (item is ESRI.ArcGIS.OSM.OSMClassExtension.member)
                        {
                            // test if ways and nodes are mixed
                            if (String.IsNullOrEmpty(geoType))
                            {
                                geoType = ((ESRI.ArcGIS.OSM.OSMClassExtension.member)item).type.ToString();
                            }
                            else
                            {
                                if (geoType != ((ESRI.ArcGIS.OSM.OSMClassExtension.member)item).type.ToString())
                                {
                                    isUniform = false;
                                    break;
                                }
                            }

                            // check for the existence of of the referenced node/way
                            // because if it doesn't exist, we will keep it as a relation only and not turn it into a multi-part geometry
                            // unfortunately it also means more queries
                            // in this case we are using the method call to determine the geometry of the referenced OSM ID, if we determine the 
                            // type of geometry, then the referenced ID might not be part of the osm file and hence it is not really a valid multi-part geometry
                            // we'll keep the relation around in stand-alone relation table
                            osmRelationGeometryType foundGeometry = determineOSMGeometryType(osmLineFeatureClass, osmPolygonFeatureClass, relationTable, ((member)item).@ref);

                            if (foundGeometry == osmRelationGeometryType.osmUnknownGeometry ||
                                foundGeometry == osmRelationGeometryType.osmHybridGeometry)
                            {
                                isUniform = false;
                                break;
                            }
                        }
                    }

                    if (detectedGeometryType == esriGeometryType.esriGeometryNull && isUniform == true)
                    {
                        detectedGeometryType = esriGeometryType.esriGeometryPolyline;
                    }

                    // just in case
                    // if we have polygon tag and a mixture of way and nodes, then let's say the the geometry is unknown
                    if (isUniform == false)
                    {
                        detectedGeometryType = esriGeometryType.esriGeometryNull;
                    }
                    else if (detectedGeometryType != esriGeometryType.esriGeometryPolygon) // if there was no tag identifying a multipart polygon we need to "walk" it to find what it is
                    {
                        osmRelationGeometryType walkedGeometryType = walkRelationGeometry(osmLineFeatureClass, osmPolygonFeatureClass, relationTable, currentRelation);

                        switch (walkedGeometryType)
                        {
                            case osmRelationGeometryType.osmPoint:
                                detectedGeometryType = esriGeometryType.esriGeometryPoint;
                                break;
                            case osmRelationGeometryType.osmPolyline:
                                detectedGeometryType = esriGeometryType.esriGeometryPolyline;
                                break;
                            case osmRelationGeometryType.osmPolygon:
                                detectedGeometryType = esriGeometryType.esriGeometryPolygon;
                                break;
                            case osmRelationGeometryType.osmHybridGeometry:
                                detectedGeometryType = esriGeometryType.esriGeometryNull;
                                break;
                            case osmRelationGeometryType.osmUnknownGeometry:
                                detectedGeometryType = esriGeometryType.esriGeometryNull;
                                break;
                            default:
                                detectedGeometryType = esriGeometryType.esriGeometryNull;
                                break;
                        }
                    }
                }
            }
            catch
            {
                detectedGeometryType = esriGeometryType.esriGeometryNull;
            }

            return detectedGeometryType;
        }

        private osmRelationGeometryType walkRelationGeometry(IFeatureClass osmLineFeatureClass, IFeatureClass osmPolygonFeatureClass, ITable relationTable, relation currentRelation)
        {
            osmRelationGeometryType testedGeometry = osmRelationGeometryType.osmUnknownGeometry;

            // we use this dictionary to determine if we can fully walk this relation
            // - the assumption is that we can walk the geometry is all node counts are 2
            Dictionary<int, int> nodeCountDictionary = new Dictionary<int, int>();

            try
            {
                if (currentRelation.Items == null)
                    return testedGeometry;

                foreach (var item in currentRelation.Items)
                {
                    if (item is ESRI.ArcGIS.OSM.OSMClassExtension.member)
                    {
                        member memberItem = item as member;
                        IQueryFilter osmIDQueryFilter = new QueryFilterClass();

                        using (ComReleaser comReleaser = new ComReleaser())
                        {
                            if (osmLineFeatureClass != null)
                            {
                                osmIDQueryFilter.WhereClause = osmLineFeatureClass.WhereClauseByExtensionVersion(memberItem.@ref, "OSMID", 2);
                                IFeatureCursor lineFeatureCursor = osmLineFeatureClass.Search(osmIDQueryFilter, false);
                                comReleaser.ManageLifetime(lineFeatureCursor);

                                IFeature foundLineFeature = lineFeatureCursor.NextFeature();

                                if (foundLineFeature != null)
                                {
                                    IPointCollection pointCollection = foundLineFeature.Shape as IPointCollection;

                                    int firstPointID = pointCollection.get_Point(0).ID;
                                    int lastPointID = pointCollection.get_Point(pointCollection.PointCount - 1).ID;

                                    if (nodeCountDictionary.ContainsKey(firstPointID))
                                    {
                                        nodeCountDictionary[firstPointID] = nodeCountDictionary[firstPointID] + 1;
                                    }
                                    else
                                    {
                                        nodeCountDictionary.Add(firstPointID, 1);
                                    }


                                    if (nodeCountDictionary.ContainsKey(lastPointID))
                                    {
                                        nodeCountDictionary[lastPointID] = nodeCountDictionary[lastPointID] + 1;
                                    }
                                    else
                                    {
                                        nodeCountDictionary.Add(lastPointID, 1);
                                    }

                                    // if we found a match in the lines so there is no need to check in the polygons
                                    continue;
                                }

                                osmIDQueryFilter.WhereClause = osmPolygonFeatureClass.WhereClauseByExtensionVersion(memberItem.@ref, "OSMID", 2);
                                IFeatureCursor polygonFeatureCursor = osmPolygonFeatureClass.Search(osmIDQueryFilter, false);
                                comReleaser.ManageLifetime(polygonFeatureCursor);

                                IFeature foundPolygonFeature = polygonFeatureCursor.NextFeature();

                                if (foundPolygonFeature != null)
                                {
                                    IPointCollection pointCollection = foundPolygonFeature.Shape as IPointCollection;

                                    int firstPointID = pointCollection.get_Point(0).ID;

                                    if (nodeCountDictionary.ContainsKey(firstPointID))
                                    {
                                        nodeCountDictionary[firstPointID] = nodeCountDictionary[firstPointID] + 2;
                                    }
                                    else
                                    {
                                        nodeCountDictionary.Add(firstPointID, 2);
                                    }

                                    // if we found a match in the polygons go to the next item
                                    continue;
                                }

                                osmIDQueryFilter.WhereClause = relationTable.WhereClauseByExtensionVersion(memberItem.@ref, "OSMID", 2);
                                ICursor relationCursor = relationTable.Search(osmIDQueryFilter, false);
                                comReleaser.ManageLifetime(relationCursor);

                                IRow foundRelation = relationCursor.NextRow();

                                if (foundRelation != null)
                                {
                                    // in order to be in the relation table is needs to be a either a super-relation or a mixed type entity (hence hybrid)
                                    testedGeometry = osmRelationGeometryType.osmHybridGeometry;
                                    break;
                                }
                            }
                        }
                    }
                }

                // check if there are any nodes counts other than 2, if there are then we cannot fully "walk" the geometry and it will be considered a line
                if (nodeCountDictionary.Values.Any(e => (e != 2)))
                {
                    testedGeometry = osmRelationGeometryType.osmPolyline;
                }
                else
                {
                    testedGeometry = osmRelationGeometryType.osmPolygon;
                }
            }

            catch
            {
                testedGeometry = osmRelationGeometryType.osmHybridGeometry;
            }

            return testedGeometry;
        }
    }
}