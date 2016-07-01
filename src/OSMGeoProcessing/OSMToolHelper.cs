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
using ESRI.ArcGIS.DataSourcesGDB;
using System.Xml.Linq;
using System.Threading;
using ESRI.ArcGIS.DataSourcesFile;
using System.Collections;
using System.Diagnostics;


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
        private static Semaphore _pool;
        private static ManualResetEvent _manualResetEvent;
        private static int _numberOfThreads = 0;

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

        public static List<String> OSMSmallFeatureClassFields()
        {
            return new List<string>(){"name", "highway", "building", "natural", "waterway", "amenity", "landuse", "place", "railway", "boundary", "power", "leisure",
            "man_made", "shop", "tourism", "route", "barrier", "surface", "type", "service", "sport"};
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
#if DEBUG
                            System.Diagnostics.Debug.WriteLine(ex.Message);
                            System.Diagnostics.Debug.WriteLine(ex.StackTrace);
#endif
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
                        //geometryDef.GridCount_2 = 1;
                        //geometryDef.set_GridSize(0, 0);

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
#if DEBUG
                System.Diagnostics.Debug.WriteLine(ex.Message);
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
#endif
                throw;
            }

            return featureClass;
        }

        ///<summary>Simple helper to create a featureclass in a geodatabase.</summary>
        /// 
        ///<param name="workspace">An IWorkspace2 interface</param>
        ///<param name="featureClassName">A System.String that contains the name of the feature class to open or create. Example: "states"</param>
        ///<param name="strConfigKeyword">An empty System.String or RDBMS table string for ArcSDE. Example: "myTable" or ""</param>
        ///  
        ///<returns>An IFeatureClass interface or a Nothing</returns>
        ///  
        ///<remarks>
        ///  (1) If a 'featureClassName' already exists in the workspace a reference to that feature class 
        ///      object will be returned.
        ///</remarks>
        internal IFeatureClass CreateSmallPointFeatureClass(ESRI.ArcGIS.Geodatabase.IWorkspace2 workspace, System.String featureClassName, System.String strConfigKeyword, string metadataAbstract, string metadataPurpose, List<string> additionalTagFields)
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
                UID CLSID = new ESRI.ArcGIS.esriSystem.UIDClass();
                CLSID.Value = "esriGeoDatabase.Feature";

                ESRI.ArcGIS.Geodatabase.IObjectClassDescription objectClassDescription = new ESRI.ArcGIS.Geodatabase.FeatureClassDescriptionClass();

                // create the fields using the required fields method
                IFields fields = objectClassDescription.RequiredFields;
                ESRI.ArcGIS.Geodatabase.IFieldsEdit fieldsEdit = (ESRI.ArcGIS.Geodatabase.IFieldsEdit)fields; // Explicit Cast

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
                osmXmlTagsField.Type_2 = esriFieldType.esriFieldTypeBlob;
                fieldsEdit.AddField((IField)osmXmlTagsField);


                IFieldEdit osmSupportingElementField = new FieldClass() as IFieldEdit;
                osmSupportingElementField.Name_2 = "osmSupportingElement";
                osmSupportingElementField.Required_2 = true;
                osmSupportingElementField.Type_2 = esriFieldType.esriFieldTypeString;
                osmSupportingElementField.Length_2 = 5;
                osmSupportingElementField.DefaultValue_2 = "no";
                fieldsEdit.AddField((IField)osmSupportingElementField);

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

                System.String strShapeField = "";

                ISpatialReferenceFactory spatialReferenceFactory = new SpatialReferenceEnvironmentClass() as ISpatialReferenceFactory;
                ISpatialReference wgs84 = spatialReferenceFactory.CreateGeographicCoordinateSystem((int)esriSRGeoCSType.esriSRGeoCS_WGS1984) as ISpatialReference;

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
                        //geometryDef.GridCount_2 = 1;
                        //geometryDef.set_GridSize(0, 0);

                        geometryDef.SpatialReference_2 = wgs84;

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
                try
                {
                    featureClass = featureWorkspace.CreateFeatureClass(featureClassName, validatedFields, CLSID, null, ESRI.ArcGIS.Geodatabase.esriFeatureType.esriFTSimple, strShapeField, strConfigKeyword);
                }
                catch
                {
                    throw;
                }

                // create the openstreetmap specific metadata
                _osmUtility.CreateOSMMetadata((IDataset)featureClass, metadataAbstract, metadataPurpose);
            }
            catch (Exception ex)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine(((IWorkspace)workspace).PathName);
                System.Diagnostics.Debug.WriteLine(featureClassName);
                System.Diagnostics.Debug.WriteLine(ex.Message);
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
#endif
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

                    //if (!DeleteDataset((IDataset)table))
                    //{
                        return table;
                    //}
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
#if DEBUG
                            System.Diagnostics.Debug.WriteLine(ex.Message);
                            System.Diagnostics.Debug.WriteLine(ex.StackTrace);
#endif
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
                        geometryDef.GeometryType_2 = esriGeometryType.esriGeometryPolyline;
                        geometryDef.HasZ_2 = false;
                        geometryDef.HasM_2 = false;
                        //geometryDef.GridCount_2 = 1;
                        //geometryDef.set_GridSize(0, 0);

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


        ///<summary>Simple helper to create a the small OSM featureclass in a geodatabase.</summary>
        /// 
        ///<param name="workspace">An IWorkspace2 interface</param>
        ///<param name="featureClassName">A System.String that contains the name of the feature class to open or create. Example: "states"</param>
        ///<param name="strConfigKeyword">An empty System.String or RDBMS table string for ArcSDE. Example: "myTable" or ""</param>
        ///  
        ///<returns>An IFeatureClass interface or a Nothing</returns>
        ///  
        ///<remarks>
        ///  (1) If a 'featureClassName' already exists in the workspace a reference to that feature class 
        ///      object will be returned.
        ///</remarks>
        internal ESRI.ArcGIS.Geodatabase.IFeatureClass CreateSmallLineFeatureClass(ESRI.ArcGIS.Geodatabase.IWorkspace2 workspace, System.String featureClassName, System.String strConfigKeyword, string metadataAbstract, string metadataPurpose, List<String> additionalTagFields)
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
                UID CLSID = new ESRI.ArcGIS.esriSystem.UIDClass();
                CLSID.Value = "esriGeoDatabase.Feature";

                ESRI.ArcGIS.Geodatabase.IObjectClassDescription objectClassDescription = new ESRI.ArcGIS.Geodatabase.FeatureClassDescriptionClass();

                    // create the fields using the required fields method
                IFields fields = objectClassDescription.RequiredFields;
                    ESRI.ArcGIS.Geodatabase.IFieldsEdit fieldsEdit = (ESRI.ArcGIS.Geodatabase.IFieldsEdit)fields; // Explicit Cast

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
                        //geometryDef.GridCount_2 = 1;
                        //geometryDef.set_GridSize(0, 0);

                        ISpatialReferenceFactory spatialReferenceFactory = new SpatialReferenceEnvironmentClass() as ISpatialReferenceFactory;
                        ISpatialReference wgs84 = spatialReferenceFactory.CreateGeographicCoordinateSystem((int)esriSRGeoCSType.esriSRGeoCS_WGS1984) as ISpatialReference;

                        geometryDef.SpatialReference_2 = wgs84;

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
                    featureClass = featureWorkspace.CreateFeatureClass(featureClassName, validatedFields, CLSID, null, ESRI.ArcGIS.Geodatabase.esriFeatureType.esriFTSimple, strShapeField, strConfigKeyword);

                // create the openstreetmap spcific metadata
                _osmUtility.CreateOSMMetadata((IDataset)featureClass, metadataAbstract, metadataPurpose);
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
#if DEBUG
                            System.Diagnostics.Debug.WriteLine(ex.Message);
                            System.Diagnostics.Debug.WriteLine(ex.StackTrace);
#endif
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
                        geometryDef.GeometryType_2 = esriGeometryType.esriGeometryPolygon;
                        geometryDef.HasZ_2 = false;
                        geometryDef.HasM_2 = false;
                        //geometryDef.GridCount_2 = 1;
                        //geometryDef.set_GridSize(0, 0);

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

        ///<summary>Simple helper to create a featureclass in a geodatabase.</summary>
        /// 
        ///<param name="workspace">An IWorkspace2 interface</param>
        ///<param name="featureClassName">A System.String that contains the name of the feature class to open or create. Example: "states"</param>
        ///<param name="fields">An IFields interface</param>
        ///<param name="strConfigKeyword">An empty System.String or RDBMS table string for ArcSDE. Example: "myTable" or ""</param>
        ///  
        ///<returns>An IFeatureClass interface or a Nothing</returns>
        ///  
        ///<remarks>
        ///  (1) If a 'featureClassName' already exists in the workspace a reference to that feature class 
        ///      object will be returned.
        ///</remarks>
        internal ESRI.ArcGIS.Geodatabase.IFeatureClass CreateSmallPolygonFeatureClass(ESRI.ArcGIS.Geodatabase.IWorkspace2 workspace, System.String featureClassName, System.String strConfigKeyword, string metadataAbstract, string metadataPurpose, List<string> additionalTagFields)
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
                UID CLSID = new ESRI.ArcGIS.esriSystem.UIDClass();
                CLSID.Value = "esriGeoDatabase.Feature";

                ESRI.ArcGIS.Geodatabase.IObjectClassDescription objectClassDescription = new ESRI.ArcGIS.Geodatabase.FeatureClassDescriptionClass();

                // if a fields collection is not passed in then supply our own
                // create the fields using the required fields method
                IFields fields = objectClassDescription.RequiredFields;
                ESRI.ArcGIS.Geodatabase.IFieldsEdit fieldsEdit = (ESRI.ArcGIS.Geodatabase.IFieldsEdit)fields; // Explicit Cast

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
                osmXmlTagsField.Type_2 = esriFieldType.esriFieldTypeBlob;
                fieldsEdit.AddField((IField)osmXmlTagsField);

                if (additionalTagFields != null)
                {
                    foreach (string nameOfTag in additionalTagFields)
                    {
                        IFieldEdit osmTagAttributeField = new FieldClass() as IFieldEdit;
                        osmTagAttributeField.Name_2 = OSMToolHelper.convert2AttributeFieldName(nameOfTag, illegalCharacters);
                        osmTagAttributeField.AliasName_2 = nameOfTag + _resourceManager.GetString("GPTools_OSMGPAttributeSelector_aliasaddition");
                        osmTagAttributeField.Type_2 = esriFieldType.esriFieldTypeString;
                        osmTagAttributeField.Length_2 = 120;
                        osmTagAttributeField.Required_2 = false;

                        fieldsEdit.AddField((IField)osmTagAttributeField);
                    }
                }

                fields = (ESRI.ArcGIS.Geodatabase.IFields)fieldsEdit; // Explicit Cast

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
                        //geometryDef.GridCount_2 = 1;
                        //geometryDef.set_GridSize(0, 0);

                        ISpatialReferenceFactory spatialReferenceFactory = new SpatialReferenceEnvironmentClass() as ISpatialReferenceFactory;
                        ISpatialReference wgs84 = spatialReferenceFactory.CreateGeographicCoordinateSystem((int)esriSRGeoCSType.esriSRGeoCS_WGS1984) as ISpatialReference;

                        geometryDef.SpatialReference_2 = wgs84;

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
                featureClass = featureWorkspace.CreateFeatureClass(featureClassName, validatedFields, CLSID, null, ESRI.ArcGIS.Geodatabase.esriFeatureType.esriFTSimple, strShapeField, strConfigKeyword);

                // create the openstreetmap specific metadata
                _osmUtility.CreateOSMMetadata((IDataset)featureClass, metadataAbstract, metadataPurpose);
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
            bool deleteSuccess = false;

            ISchemaLock schemaLock = ds as ISchemaLock;
            if (ds.CanDelete() && (schemaLock != null))
            {
                try
                {
                    schemaLock.ChangeSchemaLock(esriSchemaLock.esriExclusiveSchemaLock);
                    ds.Delete();
                    deleteSuccess = true;
                }
                catch
                {
                    schemaLock.ChangeSchemaLock(esriSchemaLock.esriSharedSchemaLock);
                }
            }

            return deleteSuccess;
        }

        #endregion

        private void UpdateSpatialGridIndex(ESRI.ArcGIS.esriSystem.ITrackCancel TrackCancel, ESRI.ArcGIS.Geodatabase.IGPMessages message, IGeoProcessor2 geoProcessor, string inputFeatureClass, bool removeFirst)
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
                if (removeFirst)
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

        //internal void BuildSpatialIndex(IGPValue gpFeatureClass, Geoprocessor.Geoprocessor geoProcessor, IGPUtilities gpUtil, ITrackCancel trackCancel, IGPMessages message)
        //{
        //    if ((gpFeatureClass == null) || (geoProcessor == null) || (gpUtil == null))
        //        return;

        //    // Check if the feature class supports spatial index grids
        //    IFeatureClass fc = gpUtil.OpenDataset(gpFeatureClass) as IFeatureClass;
        //    if (fc == null)
        //        return;

        //    int idxShapeField = fc.FindField(fc.ShapeFieldName);
        //    if (idxShapeField >= 0)
        //    {
        //        IField shapeField = fc.Fields.get_Field(idxShapeField);
        //        if (shapeField.GeometryDef.GridCount > 0)
        //        {
        //            if (shapeField.GeometryDef.get_GridSize(0) == -2.0)
        //                return;
        //        }
        //    }

        //    // Create the new spatial index grid
        //    bool storedOriginal = geoProcessor.AddOutputsToMap;

        //    try
        //    {
        //        geoProcessor.AddOutputsToMap = false;

        //        DataManagementTools.CalculateDefaultGridIndex calculateDefaultGridIndex =
        //            new DataManagementTools.CalculateDefaultGridIndex(gpFeatureClass);
        //        IGeoProcessorResult2 gpResults2 =
        //            geoProcessor.Execute(calculateDefaultGridIndex, trackCancel) as IGeoProcessorResult2;
        //        message.AddMessages(gpResults2.GetResultMessages());

        //        if (gpResults2 != null)
        //        {
        //            DataManagementTools.RemoveSpatialIndex removeSpatialIndex =
        //                new DataManagementTools.RemoveSpatialIndex(gpFeatureClass.GetAsText());
        //            removeSpatialIndex.out_feature_class = gpFeatureClass.GetAsText();
        //            gpResults2 = geoProcessor.Execute(removeSpatialIndex, trackCancel) as IGeoProcessorResult2;
        //            message.AddMessages(gpResults2.GetResultMessages());

        //            DataManagementTools.AddSpatialIndex addSpatialIndex =
        //                new DataManagementTools.AddSpatialIndex(gpFeatureClass.GetAsText());
        //            addSpatialIndex.out_feature_class = gpFeatureClass.GetAsText();

        //            addSpatialIndex.spatial_grid_1 = calculateDefaultGridIndex.grid_index1;
        //            addSpatialIndex.spatial_grid_2 = calculateDefaultGridIndex.grid_index2;
        //            addSpatialIndex.spatial_grid_3 = calculateDefaultGridIndex.grid_index3;

        //            gpResults2 = geoProcessor.Execute(addSpatialIndex, trackCancel) as IGeoProcessorResult2;
        //            message.AddMessages(gpResults2.GetResultMessages());
        //        }
        //    }
        //    catch (Exception ex)
        //    {
        //        message.AddWarning(ex.Message);
        //    }
        //    finally
        //    {
        //        geoProcessor.AddOutputsToMap = storedOriginal;
        //    }
        //}

        /// <summary>
        /// Generate equal partitions for each capacity
        /// </summary>
        /// <param name="value">capacity number</param>
        /// <param name="count">number of partitions</param>
        /// <returns>an array of length count and the sum all values is the capacity</returns>
        private long[] PartitionValue(long value, int count)
        {
            if (count <= 0) throw new ArgumentException("The count must be greater than zero.", "count");

            var result = new long[count];

            long runningTotal = 0;
            for (int i = 0; i < count; i++)
            {
                var remainder = value - runningTotal;
                var share = remainder > 0 ? remainder / (count - i) : 0;
                result[i] = share;
                runningTotal += share;
            }

            if (runningTotal < value) result[count - 1] += value - runningTotal;

            return result;
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

        /// <summary>
        /// Generates a random string of 11 characters
        /// </summary>
        /// <returns></returns>
        private string GenerateRandomString()
        {
            string path = System.IO.Path.GetRandomFileName();
            path = path.Replace(".", "");
            return path;
        }

        internal void splitOSMFile(string osmFileLocation, string tempFolder, long nodeCapacity, long wayCapacity, long relationCapacity, int numberOfThreads, out List<string> nodeFileNames, out List<string> nodeGDBNames, out List<string> wayFileNames, out List<string> wayGDBNames, out List<string> relationFileNames, out List<string> relationGDBNames)
        {
            long[] node_partitions = PartitionValue(nodeCapacity, numberOfThreads);
            long[] way_partitions = PartitionValue(wayCapacity, numberOfThreads);
            long[] relation_partitions = PartitionValue(relationCapacity, numberOfThreads);

            int node_index = 0;
            int way_index = 0;
            int relation_index = 0;

            nodeFileNames = new List<string>(numberOfThreads);
            wayFileNames = new List<string>(numberOfThreads);
            relationFileNames = new List<string>(numberOfThreads);

            nodeGDBNames = new List<string>(numberOfThreads);
            wayGDBNames = new List<string>(numberOfThreads);
            relationGDBNames = new List<string>(numberOfThreads);

                        String newName = String.Empty;
            String nodeFile = String.Empty;
            String gdbName = String.Empty;

            FileInfo osmFileInfo = new FileInfo(osmFileLocation);

            for (int i = 0; i < numberOfThreads; i++)
            {
                // for the nodes
                string randomString = GenerateRandomString();
                newName = osmFileInfo.Name.Substring(0, osmFileInfo.Name.Length - osmFileInfo.Extension.Length) + "_" + randomString + "_n" + i.ToString() + osmFileInfo.Extension;
                nodeFileNames.Add(String.Join(System.IO.Path.DirectorySeparatorChar.ToString(), new string[] { tempFolder, newName }));
                gdbName = osmFileInfo.Name.Substring(0, osmFileInfo.Name.Length - osmFileInfo.Extension.Length) + "_" + randomString + "_n" + i.ToString() + ".gdb";
                nodeGDBNames.Add(String.Join(System.IO.Path.DirectorySeparatorChar.ToString(), new string[] { tempFolder, gdbName }));

                // for the ways
                randomString = GenerateRandomString();
                newName = osmFileInfo.Name.Substring(0, osmFileInfo.Name.Length - osmFileInfo.Extension.Length) + "_" + randomString + "_w" + i.ToString() + osmFileInfo.Extension;
                wayFileNames.Add(String.Join(System.IO.Path.DirectorySeparatorChar.ToString(), new string[] { tempFolder, newName }));
                gdbName = osmFileInfo.Name.Substring(0, osmFileInfo.Name.Length - osmFileInfo.Extension.Length) + "_" + randomString + "_w" + i.ToString() + ".gdb";
                wayGDBNames.Add(String.Join(System.IO.Path.DirectorySeparatorChar.ToString(), new string[] { tempFolder, gdbName }));

                // for the relations
                randomString = GenerateRandomString();
                newName = osmFileInfo.Name.Substring(0, osmFileInfo.Name.Length - osmFileInfo.Extension.Length) + "_" + randomString + "_r" + i.ToString() + osmFileInfo.Extension;
                relationFileNames.Add(String.Join(System.IO.Path.DirectorySeparatorChar.ToString(), new string[] { tempFolder, newName }));
                gdbName = osmFileInfo.Name.Substring(0, osmFileInfo.Name.Length - osmFileInfo.Extension.Length) + "_" + randomString + "_r" + i.ToString() + ".gdb";
                relationGDBNames.Add(String.Join(System.IO.Path.DirectorySeparatorChar.ToString(), new string[] { tempFolder, gdbName }));
            }

            // in this case we are working with one thread and this can be handled with the target gdb directly
            if (nodeFileNames.Count == 0)
                return;

            XmlWriter node_writer = XmlWriter.Create(nodeFileNames[node_index]);
            node_writer.WriteStartDocument();
            node_writer.WriteStartElement("osm");
            XmlWriter way_writer = XmlWriter.Create(wayFileNames[way_index]);
            way_writer.WriteStartDocument();
            way_writer.WriteStartElement("osm");
            XmlWriter relation_writer = XmlWriter.Create(relationFileNames[relation_index]);
            relation_writer.WriteStartDocument();
            relation_writer.WriteStartElement("osm");

            long nodeCounter = 0;
            long wayCounter = 0;
            long relationCounter = 0;


            XmlReader reader = XmlReader.Create(osmFileLocation);
            reader.MoveToContent();

            while(reader.Read())
            {
                if (reader.IsStartElement())
                {
                    if (reader.Name == "node")
                    {
                        if (nodeCounter < node_partitions[node_index])
                        {
                            node_writer.WriteNode(reader, true);
                            nodeCounter++;
                        }
                        else
                        {
                            node_writer.WriteEndElement();
                            node_writer.Close();

                            node_index++;

                            nodeCounter = 0;

                            node_writer = XmlWriter.Create(nodeFileNames[node_index], new XmlWriterSettings());
                            node_writer.WriteStartDocument();
                            node_writer.WriteStartElement("osm");
                            node_writer.WriteNode(reader, true);
                        }
                    }
                    else if (reader.Name == "way")
                    {
                        if (wayCounter < way_partitions[way_index])
                        {
                            way_writer.WriteNode(reader, true);
                            wayCounter++;
                        }
                        else
                        {
                            way_writer.WriteEndElement();
                            way_writer.Close();
                            way_index++;
                            wayCounter = 0;

                            way_writer = XmlWriter.Create(wayFileNames[way_index], new XmlWriterSettings());
                            way_writer.WriteStartDocument();
                            way_writer.WriteStartElement("osm");
                            way_writer.WriteNode(reader, true);
                        }
                    }
                    else if (reader.Name == "relation")
                    {
                        if (relationCounter < relation_partitions[relation_index])
                        {
                            relation_writer.WriteNode(reader, true);
                            relationCounter++;
                        }
                        else
                        {
                            relation_writer.WriteEndElement();
                            relation_writer.Close();
                            relation_index++;
                            relationCounter = 0;

                            relation_writer = XmlWriter.Create(relationFileNames[relation_index], new XmlWriterSettings());
                            relation_writer.WriteStartDocument();
                            relation_writer.WriteStartElement("osm");
                            relation_writer.WriteNode(reader, true);
                        }
                    }
                }
            }

            reader.Close();

            if (node_writer != null)
                node_writer.Close();
            if (way_writer != null)
                way_writer.Close();
            if (relation_writer != null)
                relation_writer.Close();
        }

        public static IEnumerable<XNode> ParseXml(string xml)
        {
            var settings = new XmlReaderSettings
            {
                ConformanceLevel = ConformanceLevel.Fragment,
                IgnoreWhitespace = true
            };

            using (var stringReader = new StringReader(xml))
            using (var xmlReader = XmlReader.Create(stringReader, settings))
            {
                xmlReader.MoveToContent();
                while (xmlReader.ReadState != ReadState.EndOfFile)
                {
                    yield return XNode.ReadFrom(xmlReader);
                }
            }
        }

        internal void smallLoadOSMWay(string osmFileLocation, string sourcePointsFeatureClassName, string lineGDBLocation, string lineFeatureClassName, string polygonGDBLocation, string polygonFeatureClassName, List<string> lineFieldNames, List<string> polygonFieldNames)
        {
            using (ComReleaser comReleaser = new ComReleaser())
            {
                List<tag> tags = null;

                IGPUtilities3 gpUtilities = new GPUtilitiesClass() as IGPUtilities3;
                comReleaser.ManageLifetime(gpUtilities);

                try
                {
                    IWorkspaceFactory2 lineWorkspaceFactory = guessWorkspaceFactory(lineGDBLocation) as IWorkspaceFactory2;
                    comReleaser.ManageLifetime(lineWorkspaceFactory);
                    IFeatureWorkspace tempLineWorkspace = lineWorkspaceFactory.OpenFromFile(lineGDBLocation, 0) as IFeatureWorkspace;
                    comReleaser.ManageLifetime(tempLineWorkspace);

                    IFeatureClass lineFeatureClass = tempLineWorkspace.OpenFeatureClass(lineFeatureClassName);
                    comReleaser.ManageLifetime(lineFeatureClass);


                    IWorkspaceFactory2 polygonWorkspaceFactory = guessWorkspaceFactory(polygonGDBLocation) as IWorkspaceFactory2;
                    comReleaser.ManageLifetime(polygonWorkspaceFactory);
                    IFeatureWorkspace tempPolygonWorkspace = polygonWorkspaceFactory.OpenFromFile(polygonGDBLocation, 0) as IFeatureWorkspace;
                    comReleaser.ManageLifetime(tempPolygonWorkspace);

                    IFeatureClass polygonFeatureClass = tempPolygonWorkspace.OpenFeatureClass(polygonFeatureClassName);
                    comReleaser.ManageLifetime(polygonFeatureClass);

                    IFeatureWorkspace sourceWorkspace = null;
                    string sourceFCNameString = String.Empty;

                    string[] sourcePointFCElements = sourcePointsFeatureClassName.Split(new char[] { System.IO.Path.DirectorySeparatorChar });
                    sourceFCNameString = sourcePointFCElements[sourcePointFCElements.Length - 1];

                    if (sourcePointsFeatureClassName.Contains(lineGDBLocation))
                    {
                        // re-use the existing workspace connection
                        sourceWorkspace = tempLineWorkspace;
                    }
                    else
                    {
                        IWorkspaceFactory sourceWorkspaceFactory = guessWorkspaceFactory(sourcePointsFeatureClassName);
                        comReleaser.ManageLifetime(sourceWorkspaceFactory);

                        sourceWorkspace = sourceWorkspaceFactory.OpenFromFile(sourcePointsFeatureClassName.Substring(0, sourcePointsFeatureClassName.Length - sourceFCNameString.Length - 1), 0) as IFeatureWorkspace;
                        comReleaser.ManageLifetime(sourceWorkspace);
                    }

                    IFeatureClass sourcePointsFeatureClass = sourceWorkspace.OpenFeatureClass(sourceFCNameString);
                    comReleaser.ManageLifetime(sourcePointsFeatureClass);

                    int osmPointIDFieldIndex = sourcePointsFeatureClass.FindField("OSMID");
                    string sqlPointOSMID = sourcePointsFeatureClass.SqlIdentifier("OSMID");

                    XmlReader wayFileXmlReader = XmlReader.Create(osmFileLocation);
                    wayFileXmlReader.ReadToFollowing("way");

                    int osmLineIDFieldIndex = lineFeatureClass.FindField("OSMID");

                    Dictionary<string, int> mainLineAttributeFieldIndices = new Dictionary<string, int>();
                    foreach (string fieldName in lineFieldNames)
                    {
                        int currentFieldIndex = lineFeatureClass.FindField(OSMToolHelper.convert2AttributeFieldName(fieldName, null));

                        if (currentFieldIndex != -1)
                        {
                            mainLineAttributeFieldIndices.Add(OSMToolHelper.convert2AttributeFieldName(fieldName, null), currentFieldIndex);
                        }
                    }

                    int tagCollectionLineFieldIndex = lineFeatureClass.FindField("osmTags");

                    int osmPolygonIDFieldIndex = polygonFeatureClass.FindField("OSMID");

                    Dictionary<string, int> mainPolygonAttributeFieldIndices = new Dictionary<string, int>();
                    foreach (string fieldName in polygonFieldNames)
                    {
                        int currentFieldIndex = lineFeatureClass.FindField(OSMToolHelper.convert2AttributeFieldName(fieldName, null));

                        if (currentFieldIndex != -1)
                        {
                            mainPolygonAttributeFieldIndices.Add(OSMToolHelper.convert2AttributeFieldName(fieldName, null), currentFieldIndex);
                        }
                    }

                    int tagCollectionPolygonFieldIndex = polygonFeatureClass.FindField("osmTags");

                    IFeatureBuffer lineFeature = lineFeatureClass.CreateFeatureBuffer();
                    comReleaser.ManageLifetime(lineFeature);

                    IFeatureBuffer polygonFeature = polygonFeatureClass.CreateFeatureBuffer();
                    comReleaser.ManageLifetime(polygonFeature);


                    IFeatureCursor lineInsertCursor = lineFeatureClass.Insert(true);
                    comReleaser.ManageLifetime(lineInsertCursor);

                    IFeatureCursor polygonInsertCursor = polygonFeatureClass.Insert(true);
                    comReleaser.ManageLifetime(polygonInsertCursor);

                    ISpatialReferenceFactory spatialRef = new SpatialReferenceEnvironmentClass();
                    ISpatialReference wgs84 = spatialRef.CreateGeographicCoordinateSystem((int)esriSRGeoCSType.esriSRGeoCS_WGS1984);

                    CultureInfo en_us = new CultureInfo("en-US");

                    OSMUtility osmUtility = new OSMUtility();
                    long lineWayCount = 0;
                    long polygonWayCount = 0;

                    // -------------------------------
                    IQueryFilter osmIDQueryFilter = new QueryFilterClass();
                    // the point query filter for updates will not changes, so let's do that ahead of time
                    try
                    {
                        osmIDQueryFilter.SubFields = sourcePointsFeatureClass.ShapeFieldName + "," + sourcePointsFeatureClass.Fields.get_Field(osmPointIDFieldIndex).Name;
                    }
                    catch
                    { }

                    // do a 'small' query to establish an instance for a cursor and manage the cursor throughout the loading process
                    osmIDQueryFilter.WhereClause = sqlPointOSMID + " IN ('n1')";
                    IFeatureCursor searchPointCursor = sourcePointsFeatureClass.Search(osmIDQueryFilter, false);
                    comReleaser.ManageLifetime(searchPointCursor);

                    do
                    {
                        string wayOSMID = "w" + wayFileXmlReader.GetAttribute("id");

                        string ndsAndTags = wayFileXmlReader.ReadInnerXml();

                        bool wayIsLine = true;
                        bool wayIsComplete = true;

                        tags = new List<tag>();
                        List<string> nodes = new List<string>();

                        foreach (XElement item in ParseXml(ndsAndTags))
                        {
                            if (item.Name == "nd")
                            {
                                nodes.Add("n" + item.Attribute("ref").Value);
                            }
                            else if (item.Name == "tag")
                            {
                                tags.Add(new tag() { k = item.Attribute("k").Value, v = item.Attribute("v").Value });
                            }
                        }

                        IPointCollection wayPointCollection = null;
                        wayIsLine = IsThisWayALine(tags, nodes);

                        List<string> idRequests = SplitOSMIDRequests(nodes);
                        osmIDQueryFilter.SubFields = sourcePointsFeatureClass.ShapeFieldName + "," + sourcePointsFeatureClass.Fields.get_Field(osmPointIDFieldIndex).Name;

                        if (wayIsLine)
                        {

                            IPolyline wayPolyline = new PolylineClass();
                            wayPolyline.SpatialReference = wgs84;

                            wayPointCollection = wayPolyline as IPointCollection;

                            // build a list of node ids we can use to determine the point index in the line geometry
                            // as well as a dictionary to determine the position in the list in case of duplicates nodes
                            Dictionary<string, List<int>> nodePositionDictionary = new Dictionary<string, List<int>>(nodes.Count);

                            for (int index = 0; index < nodes.Count; index++)
                            {
                                if (nodePositionDictionary.ContainsKey(nodes[index]))
                                    nodePositionDictionary[nodes[index]].Add(index);
                                else
                                    nodePositionDictionary.Add(nodes[index], new List<int>() { index });

                                wayPointCollection.AddPoint(new PointClass());
                            }

                            foreach (string request in idRequests)
                            {
                                string idCompareString = request;
                                osmIDQueryFilter.WhereClause = sqlPointOSMID + " IN " + request;

                                searchPointCursor = sourcePointsFeatureClass.Search(osmIDQueryFilter, false);

                                IFeature nodeFeature = searchPointCursor.NextFeature();

                                while (nodeFeature != null)
                                {
                                    // determine the index of the point in with respect to the node position
                                    string nodeOSMIDString = Convert.ToString(nodeFeature.get_Value(osmPointIDFieldIndex));

                                    // remove the ID from the request string
                                    // this has the problem of potentially removing the start and the end point
                                    // there will be an additional test to see if the last point is empty
                                    idCompareString = idCompareString.Replace(nodeOSMIDString, String.Empty);

                                    wayPointCollection.UpdatePoint(nodePositionDictionary[nodeOSMIDString][0], (IPoint)nodeFeature.ShapeCopy);

                                    foreach (var index in nodePositionDictionary[nodeOSMIDString])
                                    {
                                        wayPointCollection.UpdatePoint(index, (IPoint)nodeFeature.ShapeCopy);
                                    }

                                    nodeFeature = searchPointCursor.NextFeature();
                                }

                                idCompareString = CleanReportedIDs(idCompareString);

                                // after removing the commas we should be left with only parenthesis left, meaning a string of length 2
                                // if we have more then we have found a missing node, resulting in an incomplete way geometry
                                if (idCompareString.Length > 2)
                                {
                                    wayIsComplete = false;
                                    break;
                                }
                            }

                            if (wayIsComplete)
                            {
                                try
                                {
                                    lineFeature.Shape = wayPolyline;
                                }
                                catch (Exception exs)
                                {
#if DEBUG
                                    System.Diagnostics.Debug.WriteLine(wayOSMID);
                                    System.Diagnostics.Debug.WriteLine(exs.Message);
#endif
                                }
                            }
                            else
                                continue;
                        }
                        else
                        {
                            IPolygon wayPolygon = new PolygonClass();
                            wayPolygon.SpatialReference = wgs84;

                            wayPointCollection = wayPolygon as IPointCollection;

                            Dictionary<string, List<int>> nodePositionDictionary = new Dictionary<string, List<int>>(nodes.Count);

                            // build a list of node ids we can use to determine the point index in the line geometry
                            // -- it is assumed that there are no duplicate nodes in the area
                            for (int index = 0; index < nodes.Count; index++)
                            {
                                if (nodePositionDictionary.ContainsKey(nodes[index]))
                                    nodePositionDictionary[nodes[index]].Add(index);
                                else
                                    nodePositionDictionary.Add(nodes[index], new List<int>() { index });
                                wayPointCollection.AddPoint(new PointClass());
                            }

                            foreach (string osmIDRequest in idRequests)
                            {
                                string idCompareString = osmIDRequest;

                                osmIDQueryFilter.WhereClause = sqlPointOSMID + " IN " + osmIDRequest;
                                searchPointCursor = sourcePointsFeatureClass.Search(osmIDQueryFilter, false);

                                IFeature nodeFeature = searchPointCursor.NextFeature();

                                while (nodeFeature != null)
                                {
                                    // determine the index of the point in with respect to the node position
                                    string nodeOSMIDString = Convert.ToString(nodeFeature.get_Value(osmPointIDFieldIndex));

                                    idCompareString = idCompareString.Replace(nodeOSMIDString, String.Empty);

                                    foreach (var index in nodePositionDictionary[nodeOSMIDString])
                                    {
                                        wayPointCollection.UpdatePoint(index, (IPoint)nodeFeature.ShapeCopy);
                                    }

                                    nodeFeature = searchPointCursor.NextFeature();
                                }

                                idCompareString = CleanReportedIDs(idCompareString);

                                if (idCompareString.Length > 2)
                                {
                                    wayIsComplete = false;
                                    break;
                                }
                            }

                            if (wayIsComplete)
                            {
                                ((ITopologicalOperator2)wayPointCollection).IsKnownSimple_2 = false;
                                ((IPolygon4)wayPointCollection).SimplifyEx(true, true, false);

                                try
                                {
                                    polygonFeature.Shape = (IPolygon)wayPointCollection;
                                }
                                catch (Exception exs)
                                {
#if DEBUG
                                    System.Diagnostics.Debug.WriteLine(wayOSMID);
                                    System.Diagnostics.Debug.WriteLine(exs.Message);
#endif
                                }
                            }
                            else
                                continue;
                        }


                        if (wayIsLine)
                        {
                            insertTags(mainLineAttributeFieldIndices,tagCollectionLineFieldIndex, lineFeature, tags.ToArray());
                            lineFeature.set_Value(osmLineIDFieldIndex, wayOSMID);
                        }
                        else
                        {
                            insertTags(mainPolygonAttributeFieldIndices, tagCollectionPolygonFieldIndex, polygonFeature, tags.ToArray());
                            polygonFeature.set_Value(osmPolygonIDFieldIndex, wayOSMID);
                        }

                        try
                        {
                            if (wayIsLine)
                            {
                                lineInsertCursor.InsertFeature(lineFeature);
                                lineWayCount++;
                            }
                            else
                            {
                                polygonInsertCursor.InsertFeature(polygonFeature);
                                polygonWayCount++;
                            }


                            if ((lineWayCount % 50000) == 0)
                            {
                                lineInsertCursor.Flush();
                            }

                            if ((polygonWayCount % 50000) == 0)
                            {
                                polygonInsertCursor.Flush();
                            }

                        }
                        catch (Exception ex)
                        {
#if DEBUG
                            foreach (var item in tags)
                            {
                                System.Diagnostics.Debug.WriteLine(string.Format("{0},{1}", item.k, item.v));
                            }
                            System.Diagnostics.Debug.WriteLine(ex.Message);
                            System.Diagnostics.Debug.WriteLine(ex.StackTrace);
#endif
                        }

                        // if we encounter a whitespace, attempt to find the next way if it exists
                        if (wayFileXmlReader.NodeType != XmlNodeType.Element)
                            wayFileXmlReader.ReadToFollowing("way");

                    } while (wayFileXmlReader.Name == "way");

                    wayFileXmlReader.Close();
                }
                catch (Exception ex)
                {
#if DEBUG
                    System.Diagnostics.Debug.WriteLine(osmFileLocation);
                    System.Diagnostics.Debug.WriteLine(sourcePointsFeatureClassName);
                    System.Diagnostics.Debug.WriteLine(lineGDBLocation);
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                    System.Diagnostics.Debug.WriteLine(ex.StackTrace);
                    System.Diagnostics.Debug.WriteLine(ex.Source);
#endif
                }
                finally
                {

                }
            }
        }

        internal void smallLoadOSMNode(string osmFileLocation, string fileGDBLocation, string featureClassName, List<string> tagsToLoad, bool useFeatureBuffer)
        {
            using (ComReleaser comReleaser = new ComReleaser())
            {
                List<tag> tags = null;

                try
                {
                    IWorkspaceFactory2 workspaceFactory = new FileGDBWorkspaceFactoryClass();
                    comReleaser.ManageLifetime(workspaceFactory);
                    IFeatureWorkspace nodeWorkspace = workspaceFactory.OpenFromFile(fileGDBLocation, 0) as IFeatureWorkspace;
                    comReleaser.ManageLifetime(nodeWorkspace);

                    IFeatureClass nodeFeatureClass = nodeWorkspace.OpenFeatureClass(featureClassName);
                    comReleaser.ManageLifetime(nodeFeatureClass);

                    XmlReader nodeFileXmlReader = XmlReader.Create(osmFileLocation);
                    nodeFileXmlReader.ReadToFollowing("node");

                    int osmPointIDFieldIndex = nodeFeatureClass.FindField("OSMID");

                    Dictionary<string, int> mainPointAttributeFieldIndices = new Dictionary<string, int>();
                    foreach (string fieldName in tagsToLoad)
                    {
                        int currentFieldIndex = nodeFeatureClass.FindField(OSMToolHelper.convert2AttributeFieldName(fieldName, null));

                        if (currentFieldIndex != -1)
                        {
                            mainPointAttributeFieldIndices.Add(OSMToolHelper.convert2AttributeFieldName(fieldName, null), currentFieldIndex);
                        }
                    }

                    int tagCollectionPointFieldIndex = nodeFeatureClass.FindField("osmTags");
                    int osmSupportingElementPointFieldIndex = nodeFeatureClass.FindField("osmSupportingElement");

                    IFeatureBuffer pointFeature = nodeFeatureClass.CreateFeatureBuffer();
                    comReleaser.ManageLifetime(pointFeature);

                    IFeatureCursor pointInsertCursor = nodeFeatureClass.Insert(true);
                    comReleaser.ManageLifetime(pointInsertCursor);
                    CultureInfo en_us = new CultureInfo("en-US");

                    IPoint pointGeometry = null;
                    OSMUtility osmUtility = new OSMUtility();
                    long counter = 0;

                    ISpatialReferenceFactory spatialReferenceFactory = new SpatialReferenceEnvironmentClass() as ISpatialReferenceFactory;
                    ISpatialReference wgs84 = spatialReferenceFactory.CreateGeographicCoordinateSystem((int)esriSRGeoCSType.esriSRGeoCS_WGS1984) as ISpatialReference;

                    do
                    {
                        string osmID = "n" + nodeFileXmlReader.GetAttribute("id");
                        double latitude = Convert.ToDouble(nodeFileXmlReader.GetAttribute("lat"), en_us);
                        double longitude = Convert.ToDouble(nodeFileXmlReader.GetAttribute("lon"), en_us);

                        string xmlTags = nodeFileXmlReader.ReadInnerXml();

                        tags = new List<tag>();

                        if (xmlTags.Length > 0)
                        {
                            foreach (XElement item in ParseXml(xmlTags))
                            {
                                tags.Add(new tag() { k = item.Attribute("k").Value, v = item.Attribute("v").Value });
                            }
                        }

                        pointGeometry = new PointClass();
                        pointGeometry.X = longitude;
                        pointGeometry.Y = latitude;
                        pointGeometry.SpatialReference = wgs84;

                        pointFeature.Shape = pointGeometry;
                        pointFeature.set_Value(osmPointIDFieldIndex, osmID);

                        if (tags.Count > 0)
                        {
                            // if the feature buffer is used only update/enter attributes if there are tags
                            if (useFeatureBuffer)
                                insertTags(mainPointAttributeFieldIndices, tagCollectionPointFieldIndex, pointFeature, tags.ToArray());

                            pointFeature.set_Value(osmSupportingElementPointFieldIndex, "no");
                        }
                        else
                            pointFeature.set_Value(osmSupportingElementPointFieldIndex, "yes");

                        try
                        {
                            if (useFeatureBuffer == false)
                                insertTags(mainPointAttributeFieldIndices, tagCollectionPointFieldIndex, pointFeature, tags.ToArray());

                            pointInsertCursor.InsertFeature(pointFeature);
                        }
                        catch (Exception inEx)
                        {
#if DEBUG
                            foreach (var item in tags)
                            {
                                System.Diagnostics.Debug.WriteLine(string.Format("{0},{1}", item.k, item.v));
                            }
                            System.Diagnostics.Debug.WriteLine(inEx.Message);
                            System.Diagnostics.Debug.WriteLine(inEx.StackTrace);
#endif
                        }

                        if ((counter % 50000) == 0)
                        {
                            pointInsertCursor.Flush();
                        }

                        counter++;

                        // if we encounter a whitespace, attempt to find the next node if it exists
                        if (nodeFileXmlReader.NodeType != XmlNodeType.Element)
                            nodeFileXmlReader.ReadToFollowing("node");

                    } while (nodeFileXmlReader.Name == "node");

                    nodeFileXmlReader.Close();
                }
                catch (Exception ex)
                {
#if DEBUG
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                    System.Diagnostics.Debug.WriteLine(ex.StackTrace);
#endif
                }
                finally
                {
                }
            }
        }

        internal void PythonLoadOSMRelations(System.Object args)
        {
            using (ComReleaser comReleaser = new ComReleaser())
            {
                string loadRelationsScriptName = String.Empty;

                try
                {
                    string osmFileLocation = (args as List<string>)[0];
                    string loadSuperRelations = (args as List<string>)[1];
                    string sourceLineFeatureClassLocation = (args as List<string>)[2];
                    string sourcePolygonFeatureClassLocation = (args as List<string>)[3];
                    string lineFieldNames = (args as List<string>)[4];
                    string polygonFieldNames = (args as List<string>)[5];
                    string lineFeatureClassLocation = (args as List<string>)[6];
                    string polygonFeatureClassLocation = (args as List<string>)[7];

                    FileInfo parseFileInfo = new FileInfo(osmFileLocation);
                    string pyScriptFileName = parseFileInfo.Name.Split('.')[0] + ".py";
                    loadRelationsScriptName = System.IO.Path.GetTempPath() + pyScriptFileName;
                    string toolboxPath = System.IO.Path.Combine(OSMGPFactory.GetArcGIS10InstallLocation(),
                            @"ArcToolbox\Toolboxes\OpenStreetMap Toolbox.tbx");

                    using (TextWriter writer = new StreamWriter(loadRelationsScriptName))
                    {
                        writer.WriteLine("import arcpy, sys");
                        writer.WriteLine("");
                        writer.WriteLine("# load the standard OpenStreetMap tool box as it references the core OSM tools");
                        writer.WriteLine(String.Format("arcpy.ImportToolbox(r'{0}')", toolboxPath));
                        writer.WriteLine("arcpy.env.overwriteOutput = True");
                        writer.WriteLine("");
                        writer.WriteLine("arcpy.OSMGPRelationLoader_osmtools(sys.argv[1], sys.argv[2], sys.argv[3], sys.argv[4], sys.argv[5], sys.argv[6], sys.argv[7], sys.argv[8])");
                    }

                    string arcgisPythonFolder = OSMGPFactory.GetPythonArcGISInstallLocation();

                    if (String.IsNullOrEmpty(arcgisPythonFolder))
                        throw new ArgumentOutOfRangeException(arcgisPythonFolder);

                    System.Diagnostics.ProcessStartInfo processStartInfo = new System.Diagnostics.ProcessStartInfo(
                        doubleQuote(System.IO.Path.Combine(arcgisPythonFolder, "python.exe")),
                        String.Join(" ", new string[] {
                            doubleQuote(loadRelationsScriptName),
                            doubleQuote(osmFileLocation),
                            doubleQuote(loadSuperRelations),
                            doubleQuote(sourceLineFeatureClassLocation),
                            doubleQuote(sourcePolygonFeatureClassLocation),
                            doubleQuote(lineFieldNames),
                            doubleQuote(polygonFieldNames),
                            doubleQuote(lineFeatureClassLocation),
                            doubleQuote(polygonFeatureClassLocation)
                        })
                        );

                    processStartInfo.RedirectStandardError = true;
                    processStartInfo.RedirectStandardOutput = true;
                    processStartInfo.UseShellExecute = false;

                    processStartInfo.CreateNoWindow = true;

                    System.Diagnostics.Process loadProcess = new System.Diagnostics.Process();
                    loadProcess.StartInfo = processStartInfo;
                    loadProcess.Start();

                    string result = loadProcess.StandardOutput.ReadToEnd();
                }
                catch (Exception ex)
                {
#if DEBUG
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                    System.Diagnostics.Debug.WriteLine(ex.StackTrace);
#endif
                }
                finally
                {
                    if (!String.IsNullOrEmpty(loadRelationsScriptName))
                        System.IO.File.Delete(loadRelationsScriptName);

                    if (Interlocked.Decrement(ref _numberOfThreads) == 0)
                        _manualResetEvent.Set();
                }
            }
        }

        internal void PythonLoadOSMWays(System.Object args)
        {
            using (ComReleaser comReleaser = new ComReleaser())
            {
                string loadWaysScriptName = String.Empty;

                try
                {
                    string osmFileLocation = (args as List<string>)[0];
                    string sourcePointsFeatureClassLocation = (args as List<string>)[1];
                    string lineFieldNames = (args as List<string>)[2];
                    string polygonFieldNames = (args as List<string>)[3];
                    string lineFeatureClassLocation = (args as List<string>)[4];
                    string polygonFeatureClassLocation = (args as List<string>)[5];


                    FileInfo parseFileInfo = new FileInfo(osmFileLocation);
                    string pyScriptFileName = parseFileInfo.Name.Split('.')[0] + ".py";
                    loadWaysScriptName = System.IO.Path.GetTempPath() + pyScriptFileName;
                    string toolboxPath = System.IO.Path.Combine(OSMGPFactory.GetArcGIS10InstallLocation(),
                            @"ArcToolbox\Toolboxes\OpenStreetMap Toolbox.tbx");

                    using (TextWriter writer = new StreamWriter(loadWaysScriptName))
                    {
                        writer.WriteLine("import arcpy, sys");
                        writer.WriteLine("");
                        writer.WriteLine("# load the standard OpenStreetMap tool box as it references the core OSM tools");
                        writer.WriteLine(String.Format("arcpy.ImportToolbox(r'{0}')", toolboxPath));
                        writer.WriteLine("arcpy.env.overwriteOutput = True");
                        writer.WriteLine("");
                        writer.WriteLine("arcpy.OSMGPWayLoader_osmtools(sys.argv[1], sys.argv[2], sys.argv[3], sys.argv[4], sys.argv[5], sys.argv[6])");
                    }

                    string arcgisPythonFolder = OSMGPFactory.GetPythonArcGISInstallLocation();

                    if (String.IsNullOrEmpty(arcgisPythonFolder))
                        throw new ArgumentOutOfRangeException(arcgisPythonFolder);

                    System.Diagnostics.ProcessStartInfo processStartInfo = new System.Diagnostics.ProcessStartInfo(
                        doubleQuote(System.IO.Path.Combine(arcgisPythonFolder, "python.exe")),
                        String.Join(" ", new string[] {
                            doubleQuote(loadWaysScriptName),
                            doubleQuote(osmFileLocation),
                            doubleQuote(sourcePointsFeatureClassLocation),
                            doubleQuote(lineFieldNames),
                            doubleQuote(polygonFieldNames),
                            doubleQuote(lineFeatureClassLocation),
                            doubleQuote(polygonFeatureClassLocation)
                        })
                        );

                    processStartInfo.RedirectStandardError = true;
                    processStartInfo.RedirectStandardOutput = true;
                    processStartInfo.UseShellExecute = false;

                    processStartInfo.CreateNoWindow = true;

                    System.Diagnostics.Process loadProcess = new System.Diagnostics.Process();
                    loadProcess.StartInfo = processStartInfo;
                    loadProcess.Start();

                    string result = loadProcess.StandardOutput.ReadToEnd();
                }
                catch (Exception ex)
                {
#if DEBUG
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                    System.Diagnostics.Debug.WriteLine(ex.StackTrace);
#endif
                }
                finally
                {
                    if (!String.IsNullOrEmpty(loadWaysScriptName))
                        System.IO.File.Delete(loadWaysScriptName);

                    if (Interlocked.Decrement(ref _numberOfThreads) == 0)
                        _manualResetEvent.Set();
                }
            }
        }

        internal void PythonLoadOSMNodes(System.Object args)
        {
            using (ComReleaser comReleaser = new ComReleaser())
            {
                string loadNodeScriptName = String.Empty;

                try
                {
                    string osmFileLocation = (args as List<string>)[0];
                    string fileGDBLocation = (args as List<string>)[1];
                    string featureClassName = (args as List<string>)[2];
                    string fieldNames = (args as List<string>)[3];
                    string useCacheString = (args as List<string>)[4];

                    FileInfo parseFileInfo = new FileInfo(fileGDBLocation);
                    string pyScriptFileName = parseFileInfo.Name.Split('.')[0] + ".py";
                    loadNodeScriptName = System.IO.Path.GetTempPath() + pyScriptFileName;
                    string toolboxPath = System.IO.Path.Combine(OSMGPFactory.GetArcGIS10InstallLocation(),
                            @"ArcToolbox\Toolboxes\OpenStreetMap Toolbox.tbx");

                    using (TextWriter writer = new StreamWriter(loadNodeScriptName))
                    {
                        writer.WriteLine("import arcpy, sys");
                        writer.WriteLine("");
                        writer.WriteLine("# load the standard OpenStreetMap tool box as it references the core OSM tools");
                        writer.WriteLine(String.Format("arcpy.ImportToolbox(r'{0}')", toolboxPath));
                        writer.WriteLine("arcpy.env.overwriteOutput = True");
                        writer.WriteLine("");
                        writer.WriteLine("arcpy.OSMGPNodeLoader_osmtools(sys.argv[1], sys.argv[2], sys.argv[3], sys.argv[4])");
                    }

                    string arcgisPythonFolder = OSMGPFactory.GetPythonArcGISInstallLocation();

                    if (String.IsNullOrEmpty(arcgisPythonFolder))
                        throw new ArgumentOutOfRangeException(arcgisPythonFolder);

#if DEBUG
                    System.Diagnostics.Debug.WriteLine(String.Join(" ", new string[] {"/c",
                            doubleQuote(System.IO.Path.Combine(arcgisPythonFolder, "python.exe")),
                            doubleQuote(loadNodeScriptName),
                            doubleQuote(osmFileLocation),
                            doubleQuote(fieldNames),
                            doubleQuote(useCacheString),
                            doubleQuote(String.Join(System.IO.Path.DirectorySeparatorChar.ToString(), new string[] { fileGDBLocation, featureClassName }))
                        })
                    );
#endif

                    System.Diagnostics.ProcessStartInfo processStartInfo = new System.Diagnostics.ProcessStartInfo(
                        doubleQuote(System.IO.Path.Combine(arcgisPythonFolder, "python.exe")), 
                        String.Join(" ", new string[] {
                            doubleQuote(loadNodeScriptName),
                            doubleQuote(osmFileLocation),
                            doubleQuote(fieldNames),
                            doubleQuote(useCacheString),
                            doubleQuote(String.Join(System.IO.Path.DirectorySeparatorChar.ToString(), new string[] { fileGDBLocation, featureClassName }))
                        })
                        );

                    //processStartInfo.RedirectStandardOutput = true;
                    processStartInfo.RedirectStandardError = true;
                    processStartInfo.UseShellExecute = false;

                    processStartInfo.CreateNoWindow = true;

                    System.Diagnostics.Process loadProcess = new System.Diagnostics.Process();
                    loadProcess.StartInfo = processStartInfo;
                    loadProcess.Start();

                    string result = loadProcess.StandardError.ReadToEnd();
                }
                catch (Exception ex)
                {
#if DEBUG
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                    System.Diagnostics.Debug.WriteLine(ex.StackTrace);
#endif
                }
                finally
                {
                    if (!string.IsNullOrEmpty(loadNodeScriptName))
                        System.IO.File.Delete(loadNodeScriptName);

                    if (Interlocked.Decrement(ref _numberOfThreads) == 0)
                        _manualResetEvent.Set();
                }
            }
        }

        private string doubleQuote(string inputString)
        {
            return String.Format(@"""{0}""", inputString);
        }

        internal void loadOSMWays(List<string> osmWayFileNames, string sourcePointFCName, string targetLineFCName, string targetPolygonFCName, List<string> wayGDBNames, string lineFeatureClassName, string polygonFeatureClassName, List<string> lineFieldNames, List<string> polygonFieldNames, ref IGPMessages toolMessages, ref ITrackCancel CancelTracker)
        {
            // create the point feature classes in the temporary loading fgdbs
            OSMToolHelper toolHelper = new OSMToolHelper();
            IGeoProcessor2 geoProcessor = new GeoProcessorClass() as IGeoProcessor2;
            geoProcessor.AddOutputsToMap = false;
            IGeoProcessorResult gpResults = null;
            IVariantArray parameterArray = null;

            Stopwatch executionStopwatch = System.Diagnostics.Stopwatch.StartNew();
            toolMessages.AddMessage(String.Format(_resourceManager.GetString("GPTools_OSMGPMultiLoader_loading_ways")));

            // in the case of a single thread we can use the parent process directly to convert the osm to the target featureclass
            if (osmWayFileNames.Count == 1)
            {
                string[] lineFCNameElements = targetLineFCName.Split(System.IO.Path.DirectorySeparatorChar);
                string[] lineGDBComponents = new string[lineFCNameElements.Length - 1];
                System.Array.Copy(lineFCNameElements, lineGDBComponents, lineFCNameElements.Length - 1);
                string lineGDBLocation = String.Join(System.IO.Path.DirectorySeparatorChar.ToString(), lineGDBComponents);

                string[] polygonFCNameElements = targetPolygonFCName.Split(System.IO.Path.DirectorySeparatorChar);
                string[] polygonGDBComponents = new string[polygonFCNameElements.Length - 1];
                System.Array.Copy(polygonFCNameElements, polygonGDBComponents, polygonFCNameElements.Length - 1);
                string polygonGDBLocation = String.Join(System.IO.Path.DirectorySeparatorChar.ToString(), polygonGDBComponents);

                toolHelper.smallLoadOSMWay(osmWayFileNames[0], sourcePointFCName, lineGDBLocation, lineFCNameElements[lineFCNameElements.Length - 1],
                    polygonGDBLocation, polygonFCNameElements[polygonFCNameElements.Length - 1], lineFieldNames, polygonFieldNames);

                executionStopwatch.Stop();
                TimeSpan wayLoadingTimeSpan = executionStopwatch.Elapsed;
                toolMessages.AddMessage(String.Format(_resourceManager.GetString("GPTools_OSMGPMultiLoader_doneloading_ways"), wayLoadingTimeSpan.Hours, wayLoadingTimeSpan.Minutes, wayLoadingTimeSpan.Seconds));
            }
            else
            {
                using (ComReleaser comReleaser = new ComReleaser())
                {
                    IWorkspaceFactory workspaceFactory = new FileGDBWorkspaceFactoryClass();
                    comReleaser.ManageLifetime(workspaceFactory);

                    for (int gdbIndex = 0; gdbIndex < wayGDBNames.Count; gdbIndex++)
                    {
                        FileInfo gdbFileInfo = new FileInfo(wayGDBNames[gdbIndex]);

                        if (!gdbFileInfo.Exists)
                        {
                            IWorkspaceName workspaceName = workspaceFactory.Create(gdbFileInfo.DirectoryName, gdbFileInfo.Name, new PropertySetClass(), 0);
                            comReleaser.ManageLifetime(workspaceName);
                        }
                    }
                }

                _manualResetEvent = new ManualResetEvent(false);
                _numberOfThreads = osmWayFileNames.Count;

                for (int i = 0; i < osmWayFileNames.Count; i++)
                {
                    Thread t = new Thread(new ParameterizedThreadStart(PythonLoadOSMWays));
                    t.Start(new List<string>() { 
                        osmWayFileNames[i], 
                        sourcePointFCName, 
                        String.Join(";", lineFieldNames.ToArray()),
                        String.Join(";", polygonFieldNames.ToArray()),
                        String.Join(System.IO.Path.DirectorySeparatorChar.ToString(), new string [] {wayGDBNames[i], lineFeatureClassName}),
                        String.Join(System.IO.Path.DirectorySeparatorChar.ToString(), new string [] {wayGDBNames[i], polygonFeatureClassName}) });
                }

                // wait for all nodes to complete loading before appending all into the target feature class
                _manualResetEvent.WaitOne();
                _manualResetEvent.Close();


                executionStopwatch.Stop();
                TimeSpan wayLoadingTimeSpan = executionStopwatch.Elapsed;
                toolMessages.AddMessage(String.Format(_resourceManager.GetString("GPTools_OSMGPMultiLoader_doneloading_ways"), wayLoadingTimeSpan.Hours, wayLoadingTimeSpan.Minutes, wayLoadingTimeSpan.Seconds));


                // delete the temp osm files from disk
                foreach (string osmFile in osmWayFileNames)
                {
                    try
                    {
                        System.IO.File.Delete(osmFile);
                    }
                    catch { }
                }

                // we will need one less as the first osm file is loaded into the target feature classes
                List<string> linesFCNamesArray = new List<string>(wayGDBNames.Count);
                List<string> polygonFCNamesArray = new List<string>(wayGDBNames.Count);

                // append all lines into the target feature class
                for (int gdbIndex = 0; gdbIndex < wayGDBNames.Count; gdbIndex++)
                {
                    linesFCNamesArray.Add(String.Join(System.IO.Path.DirectorySeparatorChar.ToString(), new string[] { wayGDBNames[gdbIndex], lineFeatureClassName }));
                    polygonFCNamesArray.Add(String.Join(System.IO.Path.DirectorySeparatorChar.ToString(), new string[] { wayGDBNames[gdbIndex], polygonFeatureClassName }));
                }

                string[] pointFCElement = sourcePointFCName.Split(System.IO.Path.DirectorySeparatorChar);
                string sourceFGDB = sourcePointFCName.Substring(0, sourcePointFCName.Length - pointFCElement[pointFCElement.Length - 1].Length - 1);

                // append all the lines
                parameterArray = new VarArrayClass();
                parameterArray.Add(String.Join(";", linesFCNamesArray.ToArray()));
                parameterArray.Add(targetLineFCName);

                gpResults = geoProcessor.Execute("Append_management", parameterArray, CancelTracker);

                IGPMessages messages = gpResults.GetResultMessages();
                toolMessages.AddMessages(gpResults.GetResultMessages());

#if DEBUG
                for (int i = 0; i < messages.Count; i++)
                {
                    System.Diagnostics.Debug.WriteLine(messages.GetMessage(i).Description);
                }
#endif

                // append all the polygons
                parameterArray = new VarArrayClass();
                parameterArray.Add(String.Join(";", polygonFCNamesArray.ToArray()));
                parameterArray.Add(targetPolygonFCName);

                gpResults = geoProcessor.Execute("Append_management", parameterArray, CancelTracker);

                messages = gpResults.GetResultMessages();
                toolMessages.AddMessages(gpResults.GetResultMessages());

#if DEBUG
                for (int i = 0; i < messages.Count; i++)
                {
                    System.Diagnostics.Debug.WriteLine(messages.GetMessage(i).Description);
                }
#endif

                // delete temp file geodatabases
                for (int gdbIndex = 0; gdbIndex < wayGDBNames.Count; gdbIndex++)
                {
                    if (!sourceFGDB.Equals(wayGDBNames[gdbIndex]))
                    {
                        parameterArray = new VarArrayClass();
                        parameterArray.Add(wayGDBNames[gdbIndex]);
                        geoProcessor.Execute("Delete_management", parameterArray, CancelTracker);
                    }
                }

            }

            // compute the OSM index on the target line featureclass
            parameterArray = CreateAddIndexParameterArray(targetLineFCName, "OSMID", "osmID_IDX", "UNIQUE", "");
            gpResults = geoProcessor.Execute("AddIndex_management", parameterArray, CancelTracker);
            toolMessages.AddMessages(gpResults.GetResultMessages());

            // compute the OSM index on the target polygon featureclass
            parameterArray = CreateAddIndexParameterArray(targetPolygonFCName, "OSMID", "osmID_IDX", "UNIQUE", "");
            gpResults = geoProcessor.Execute("AddIndex_management", parameterArray, CancelTracker);
            toolMessages.AddMessages(gpResults.GetResultMessages());

            ComReleaser.ReleaseCOMObject(geoProcessor);
        }

        internal void loadOSMNodes(List<string> osmNodeFileNames, List<string> nodeGDBNames, string featureClassName, string targetFeatureClass, List<string> tagsToLoad, bool deleteNodes, ref IGPMessages toolMessages, ref ITrackCancel CancelTracker)
        {
            // create the point feature classes in the temporary loading fgdbs
            OSMToolHelper toolHelper = new OSMToolHelper();
            IGeoProcessor2 geoProcessor = new GeoProcessorClass() as IGeoProcessor2;
            geoProcessor.AddOutputsToMap = false;
            IGeoProcessorResult gpResults = null;
            IVariantArray parameterArray = null;

            Stopwatch executionStopwatch = System.Diagnostics.Stopwatch.StartNew();

            toolMessages.AddMessage(String.Format(_resourceManager.GetString("GPTools_OSMGPMultiLoader_loading_nodes")));

            string useCacheString = "USE_CACHE";
            if (!deleteNodes)
                useCacheString = "DO_NOT_USE_CACHE";


            // in the case of a single thread we can use the parent process directly to convert the osm to the target featureclass
            if (osmNodeFileNames.Count == 1)
            {
                toolHelper.smallLoadOSMNode(osmNodeFileNames[0], nodeGDBNames[0], featureClassName, tagsToLoad, true);

                executionStopwatch.Stop();
                TimeSpan nodeLoadingTimeSpan = executionStopwatch.Elapsed;

                toolMessages.AddMessage(String.Format(_resourceManager.GetString("GPTools_OSMGPMultiLoader_doneloading_nodes"), nodeLoadingTimeSpan.Hours, nodeLoadingTimeSpan.Minutes, nodeLoadingTimeSpan.Seconds));
            }
            else
            {
                using (ComReleaser comReleaser = new ComReleaser())
                {
                    IWorkspaceFactory workspaceFactory = new FileGDBWorkspaceFactoryClass();
                    comReleaser.ManageLifetime(workspaceFactory);

                    for (int gdbIndex = 1; gdbIndex < nodeGDBNames.Count; gdbIndex++)
                    {
                        FileInfo gdbFileInfo = new FileInfo(nodeGDBNames[gdbIndex]);

                        if (!gdbFileInfo.Exists)
                        {
                            IWorkspaceName workspaceName = workspaceFactory.Create(gdbFileInfo.DirectoryName, gdbFileInfo.Name, new PropertySetClass(), 0);
                            comReleaser.ManageLifetime(workspaceName);
                        }
                    }
                }

                _manualResetEvent = new ManualResetEvent(false);
                _numberOfThreads = osmNodeFileNames.Count;

                for (int i = 0; i < osmNodeFileNames.Count; i++)
                {
                    Thread t = new Thread(new ParameterizedThreadStart(PythonLoadOSMNodes));
                    t.Start(new List<string>() { osmNodeFileNames[i], nodeGDBNames[i], featureClassName, String.Join(";", tagsToLoad.ToArray()), useCacheString });
                }

                // wait for all nodes to complete loading before appending all into the target feature class
                _manualResetEvent.WaitOne();
                _manualResetEvent.Close();

                executionStopwatch.Stop();
                TimeSpan nodeLoadingTimeSpan = executionStopwatch.Elapsed;
                toolMessages.AddMessage(String.Format(_resourceManager.GetString("GPTools_OSMGPMultiLoader_doneloading_nodes"), nodeLoadingTimeSpan.Hours, nodeLoadingTimeSpan.Minutes, nodeLoadingTimeSpan.Seconds));

                // we done using the osm files for loading
                foreach (string osmFile in osmNodeFileNames)
                {
                    try
                    {
                        System.IO.File.Delete(osmFile);
                    }
                    catch { }
                }

                // we need one less as the first node is already loaded into the target feature class
                List<string> fcNamesArray = new List<string>(osmNodeFileNames.Count - 1);
                // 

                // append all points into the target feature class
                for (int gdbIndex = 1; gdbIndex < nodeGDBNames.Count; gdbIndex++)
                {
                    fcNamesArray.Add(String.Join(System.IO.Path.DirectorySeparatorChar.ToString(), new string[] { nodeGDBNames[gdbIndex], featureClassName }));
                }

                parameterArray = new VarArrayClass();
                parameterArray.Add(String.Join(";", fcNamesArray.ToArray()));
                parameterArray.Add(targetFeatureClass);

                gpResults = geoProcessor.Execute("Append_management", parameterArray, CancelTracker);

                IGPMessages messages = gpResults.GetResultMessages();
                toolMessages.AddMessages(gpResults.GetResultMessages());

                // delete the temp loading fgdb for points
                for (int gdbIndex = 1; gdbIndex < nodeGDBNames.Count; gdbIndex++)
                {
                    parameterArray = new VarArrayClass();
                    parameterArray.Add(nodeGDBNames[gdbIndex]);
                    geoProcessor.Execute("Delete_management", parameterArray, CancelTracker);
                }
            }

            // compute the OSM index on the target featureclass
            parameterArray = CreateAddIndexParameterArray(targetFeatureClass, "OSMID", "osmID_IDX", "UNIQUE", "");
            gpResults = geoProcessor.Execute("AddIndex_management", parameterArray, CancelTracker);
            toolMessages.AddMessages(gpResults.GetResultMessages());

            if (deleteNodes)
            {
                // compute the support element index on the target featureclass
                parameterArray = CreateAddIndexParameterArray(targetFeatureClass, "osmSupportingElement", "supEl_IDX", "NON_UNIQUE", "");
                gpResults = geoProcessor.Execute("AddIndex_management", parameterArray, CancelTracker);
                toolMessages.AddMessages(gpResults.GetResultMessages());
            }

            ComReleaser.ReleaseCOMObject(geoProcessor);
        }

        internal void loadOSMNodes(string osmFileLocation, ref ITrackCancel TrackCancel, ref IGPMessages message, IGPValue targetGPValue, IFeatureClass osmPointFeatureClass, bool conserveMemory, bool fastLoad, int nodeCapacity, ref Dictionary<string, simplePointRef> osmNodeDictionary, IFeatureWorkspace featureWorkspace, ISpatialReference downloadSpatialReference, OSMDomains availableDomains, bool checkForExisting)
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

                            //if (((IWorkspace)featureWorkspace).WorkspaceFactory.WorkspaceType == esriWorkspaceType.esriRemoteDatabaseWorkspace)
                            //{
                                pointFeatureLoad = osmPointFeatureClass as IFeatureClassLoad;
                            //}

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
                                            if (_osmUtility.DoesHaveKeys(currentNode.tag))
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
#if DEBUG
                                                System.Diagnostics.Debug.WriteLine(ex.Message);
#endif
                                                message.AddWarning(ex.Message);
                                            }


                                            if ((pointCount % 50000) == 0)
                                            {
                                                message.AddMessage(String.Format(_resourceManager.GetString("GPTools_OSMGPFileReader_pointsloaded"), pointCount));
                                                pointInsertCursor.Flush();
                                                System.GC.Collect();

                                                if (TrackCancel.Continue() == false)
                                                {
                                                    return;
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
#if DEBUG
                                            System.Diagnostics.Debug.WriteLine(ex.Message);
#endif
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
                                    UpdateSpatialGridIndex(TrackCancel, message, geoProcessor, fcLocation, true);
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

        private void insertTags(Dictionary<string, int> AttributeFieldIndices, int tagCollectionFieldIndex, IRowBuffer row, tag[] tagsToInsert)
        {
            if (tagsToInsert != null)
            {
                TagKeyComparer tagKeyComparer = new TagKeyComparer();

                foreach (var fieldName in AttributeFieldIndices.Keys)
                {
                    string keyName = convert2OSMKey(fieldName, string.Empty);

                    if (tagsToInsert.Contains(new tag() { k = keyName }, tagKeyComparer))
                    {
                        tag item = tagsToInsert.Where(t => t.k == keyName).First();

                        int fieldIndex = AttributeFieldIndices[fieldName];
                        if (fieldIndex > -1)
                        {
                            if (item.v.Length > row.Fields.get_Field(fieldIndex).Length)
                                row.set_Value(fieldIndex, item.v.Substring(0, row.Fields.get_Field(fieldIndex).Length));
                            else
                                row.set_Value(fieldIndex, item.v);
                        }
                    }
                    else
                    {
                        int fieldIndex = AttributeFieldIndices[fieldName];
                        if (fieldIndex > -1)
                        {
                            row.set_Value(fieldIndex, System.DBNull.Value);
                        }
                    }
                }

                if (tagCollectionFieldIndex > -1)
                {
                    if (tagsToInsert.Count() == 0)
                        row.set_Value(tagCollectionFieldIndex, System.DBNull.Value);
                    else
                        _osmUtility.insertOSMTags(tagCollectionFieldIndex, row, tagsToInsert);
                }
            }
            else
            {
                if (tagCollectionFieldIndex > -1)
                {
                    row.set_Value(tagCollectionFieldIndex, System.DBNull.Value);
                }
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

        private IWorkspaceFactory guessWorkspaceFactory(string workspacePath)
        {
            IWorkspaceFactory workspaceFactory = null;

            if (!String.IsNullOrEmpty(workspacePath))
            {
                if (workspacePath.ToLower().Contains(".gdb"))
                    workspaceFactory = new FileGDBWorkspaceFactoryClass();
                else if (workspacePath.ToLower().Contains(".sde"))
                    workspaceFactory = new SdeWorkspaceFactoryClass();
                else if (workspacePath.ToLower().Contains(".gds"))
                    workspaceFactory = new SqlWorkspaceFactoryClass();
            }

            return workspaceFactory;
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

                        //if (((IWorkspace)featureWorkspace).WorkspaceFactory.WorkspaceType == esriWorkspaceType.esriRemoteDatabaseWorkspace)
                        //{
                            lineFeatureLoad = osmLineFeatureClass as IFeatureClassLoad;
                            polygonFeatureLoad = osmPolygonFeatureClass as IFeatureClassLoad;
                        //}

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

                                        //featureLineBuffer = osmLineFeatureClass.CreateFeatureBuffer();
                                        //featurePolygonBuffer = osmPolygonFeatureClass.CreateFeatureBuffer();

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

                                                        idCompareString = CleanReportedIDs(idCompareString);

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

                                            featureLineBuffer = osmLineFeatureClass.CreateFeatureBuffer();

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
                                                    osmIDQueryFilter.SubFields = osmPointFeatureClass.ShapeFieldName + "," + osmPointFeatureClass.Fields.get_Field(osmPointIDFieldIndex).Name + "," + osmPointFeatureClass.Fields.get_Field(osmWayRefCountFieldIndex).Name + "," + osmPointFeatureClass.OIDFieldName;
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

                                                            while (nodePositionIndex > -1)
                                                            {
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

                                                                nodePositionIndex = nodeIDs.IndexOf(nodeOSMIDString, nodePositionDictionary[nodeOSMIDString]);
                                                            }

                                                            if (nodeFeature != null)
                                                                Marshal.ReleaseComObject(nodeFeature);

                                                            nodeFeature = updatePointCursor.NextFeature();
                                                        }

                                                        idCompareString = CleanReportedIDs(idCompareString);

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
                                                missingWays.Add(currentWay.id);
                                                continue; // continue to read the next way
                                            }

                                            featurePolygonBuffer = osmPolygonFeatureClass.CreateFeatureBuffer();

                                            ((IPolygon)wayPointCollection).Close();
                                            ((IPolygon)wayPointCollection).SimplifyPreserveFromTo();

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
                                                    if (_osmUtility.DoesHaveKeys(currentWay))
                                                    {
                                                        featureLineBuffer.set_Value(osmSupportingElementPolylineFieldIndex, "no");
                                                    }
                                                    else
                                                    {
                                                        featureLineBuffer.set_Value(osmSupportingElementPolylineFieldIndex, "yes");
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                if (osmSupportingElementPolygonFieldIndex > -1)
                                                {
                                                    if (_osmUtility.DoesHaveKeys(currentWay))
                                                    {
                                                        featurePolygonBuffer.set_Value(osmSupportingElementPolygonFieldIndex, "no");
                                                    }
                                                    else
                                                    {
                                                        featurePolygonBuffer.set_Value(osmSupportingElementPolygonFieldIndex, "yes");
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
#if DEBUG
                                        System.Diagnostics.Debug.WriteLine(String.Format("Feature OSMID {0}", currentWay.id));
                                        System.Diagnostics.Debug.WriteLine(ex.Message);
                                        System.Diagnostics.Debug.WriteLine(ex.StackTrace);
#endif
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
                                UpdateSpatialGridIndex(TrackCancel, message, geoProcessor, fcLocation, true);
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
                                UpdateSpatialGridIndex(TrackCancel, message, geoProcessor, fcLocation, true);
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

        /// <summary>
        /// 
        /// </summary>
        /// <param name="environmentManager"></param>
        /// <param name="name"></param>
        /// <returns>returns null pointer if no environment with the specified name is found.</returns>
        public static IGPEnvironment getEnvironment(IGPEnvironmentManager environmentManager, string name)
        {
            IGPUtilities3 gpUtils = new GPUtilitiesClass();
            IGPEnvironment returnEnv = null;

            try
            {
                if (environmentManager.GetLocalEnvironments().Count > 0)
                    returnEnv = gpUtils.GetEnvironment(environmentManager.GetLocalEnvironments(), name);

                if (returnEnv == null)
                    returnEnv = gpUtils.GetEnvironment(environmentManager.GetEnvironments(), name);
            }
            catch (Exception ex)
            {
#if DEBUG
                System.Diagnostics.Debug.WriteLine(ex.Message);
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
#endif
            }

            return returnEnv;
        }

        private static string CleanReportedIDs(string idCompareString)
        {
            // if all the requested IDs were returned we should only have brackets and commas left
            string save_compareString = idCompareString;
            string searchPattern = @"(\b[nrw0-9]\d+)";
            Regex regularExpression = new Regex(searchPattern);

            List<string> missingIDs = new List<string>();

            foreach (Match match in regularExpression.Matches(save_compareString))
            {
                if (String.IsNullOrEmpty(match.Value) == false)
                {
                    missingIDs.Add(match.Value);
                }
            }

            idCompareString = "(" + string.Join(",", missingIDs.ToArray()) + ")";
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

        /// <summary>
        /// This method counts nodes, ways, and relations. However it does assume a tidy XML file (line formatted).
        /// </summary>
        /// <param name="osmFileLocation"></param>
        /// <param name="nodeCapacity"></param>
        /// <param name="wayCapacity"></param>
        /// <param name="relationCapacity"></param>
        /// <param name="CancelTracker"></param>
        internal void countOSMStuffFast(string osmFileLocation, ref long nodeCapacity, ref long wayCapacity, ref long relationCapacity, ref ITrackCancel CancelTracker)
        {
            using (System.IO.FileStream fStream = new System.IO.FileStream(osmFileLocation, FileMode.Open, FileAccess.Read))
            {
                using (StreamReader sReader = new StreamReader(fStream))
                {
                    string line;
                    while ((line = sReader.ReadLine()) != null)
                    {
                        //if (!CancelTracker.Continue())
                        //    return;

                        if (line.Contains("<node"))
                            nodeCapacity++;
                        else if (line.Contains("<way"))
                            wayCapacity++;
                        else if (line.Contains("<relation"))
                            relationCapacity++;
                    }
                }
            }
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

        internal void loadOSMRelations(List<string> osmRelationFileNames, string sourceLineFCName, string sourcePolygonFCName, string targetLineFCName, string targetPolygonFCName, List<string> relationGDBNames, List<string> lineFieldNames, List<string> polygonFieldNames, ref IGPMessages toolMessages, ref ITrackCancel TrackCancel)
        {
            // create the point feature classes in the temporary loading fgdbs
            OSMToolHelper toolHelper = new OSMToolHelper();
            IGeoProcessor2 geoProcessor = new GeoProcessorClass() as IGeoProcessor2;
            geoProcessor.AddOutputsToMap = false;
            IGeoProcessorResult gpResults = null;

            Stopwatch executionStopwatch = System.Diagnostics.Stopwatch.StartNew();
            toolMessages.AddMessage(String.Format(_resourceManager.GetString("GPTools_OSMGPMultiLoader_loading_relations")));

            string loadSuperRelationParameterValue = "DO_NOT_LOAD_SUPER_RELATION";

            // take the name of the temp line and polygon featureclass from the source names
            string[] sourceLineNameElements = sourceLineFCName.Split(System.IO.Path.DirectorySeparatorChar);
            string lineFeatureClassName = sourceLineNameElements[sourceLineNameElements.Length - 1];
            string[] sourcePolygonNameElements = sourcePolygonFCName.Split(System.IO.Path.DirectorySeparatorChar);
            string polygonFeatureClassName = sourcePolygonNameElements[sourcePolygonNameElements.Length - 1];

            // in the case of a single thread we can use the parent process directly to convert the osm to the target featureclass
            if (osmRelationFileNames.Count == 1)
            {
                // do relations first
                toolHelper.smallLoadOSMRelations(osmRelationFileNames[0], sourceLineFCName, sourcePolygonFCName, targetLineFCName, targetPolygonFCName,
                    lineFieldNames, polygonFieldNames, false);


                executionStopwatch.Stop();
                TimeSpan relationLoadingTimeSpan = executionStopwatch.Elapsed;
                toolMessages.AddMessage(String.Format(_resourceManager.GetString("GPTools_OSMGPMultiLoader_doneloading_relations"), relationLoadingTimeSpan.Hours, relationLoadingTimeSpan.Minutes, relationLoadingTimeSpan.Seconds));


                toolMessages.AddMessage(String.Format(_resourceManager.GetString("GPTools_OSMGPMultiLoader_loading_super_relations")));
                executionStopwatch.Reset();
                executionStopwatch.Start();


                // then do super-relations
                toolHelper.smallLoadOSMRelations(osmRelationFileNames[0], sourceLineFCName, sourcePolygonFCName, targetLineFCName, targetPolygonFCName,
                    lineFieldNames, polygonFieldNames, true);

                executionStopwatch.Stop();
                relationLoadingTimeSpan = executionStopwatch.Elapsed;
                toolMessages.AddMessage(String.Format(_resourceManager.GetString("GPTools_OSMGPMultiLoader_doneloading_super_relations"), relationLoadingTimeSpan.Hours, relationLoadingTimeSpan.Minutes, relationLoadingTimeSpan.Seconds));
            }
            else
            {
                #region load relation containing only ways
                using (ComReleaser comReleaser = new ComReleaser())
                {
                    IWorkspaceFactory workspaceFactory = new FileGDBWorkspaceFactoryClass();
                    comReleaser.ManageLifetime(workspaceFactory);

                    for (int gdbIndex = 0; gdbIndex < relationGDBNames.Count; gdbIndex++)
                    {
                        FileInfo gdbFileInfo = new FileInfo(relationGDBNames[gdbIndex]);

                        if (!gdbFileInfo.Exists)
                        {
                            IWorkspaceName workspaceName = workspaceFactory.Create(gdbFileInfo.DirectoryName, gdbFileInfo.Name, new PropertySetClass(), 0);
                            comReleaser.ManageLifetime(workspaceName);
                        }
                    }
                }

                _manualResetEvent = new ManualResetEvent(false);
                _numberOfThreads = osmRelationFileNames.Count;


                for (int i = 0; i < osmRelationFileNames.Count; i++)
                {
                    Thread t = new Thread(new ParameterizedThreadStart(PythonLoadOSMRelations));
                    t.Start(new List<string>() { 
                        osmRelationFileNames[i],
                        loadSuperRelationParameterValue,
                        sourceLineFCName, 
                        sourcePolygonFCName,
                        String.Join(";", lineFieldNames.ToArray()),
                        String.Join(";", polygonFieldNames.ToArray()),
                        String.Join(System.IO.Path.DirectorySeparatorChar.ToString(), new string[] { relationGDBNames[i], lineFeatureClassName }),
                        String.Join(System.IO.Path.DirectorySeparatorChar.ToString(), new string[] { relationGDBNames[i], polygonFeatureClassName }),
                });
                }

                // wait for all nodes to complete loading before appending all into the target feature class
                _manualResetEvent.WaitOne();
                _manualResetEvent.Close();


                executionStopwatch.Stop();
                TimeSpan relationLoadingTimeSpan = executionStopwatch.Elapsed;
                toolMessages.AddMessage(String.Format(_resourceManager.GetString("GPTools_OSMGPMultiLoader_doneloading_relations"), relationLoadingTimeSpan.Hours, relationLoadingTimeSpan.Minutes, relationLoadingTimeSpan.Seconds));

                List<string> linesFCNamesArray = new List<string>(relationGDBNames.Count);
                List<string> polygonFCNamesArray = new List<string>(relationGDBNames.Count);

                // append all lines into the target feature class
                foreach (string  fileGDB in relationGDBNames)
                {
                    linesFCNamesArray.Add(String.Join(System.IO.Path.DirectorySeparatorChar.ToString(), new string[] { fileGDB, lineFeatureClassName }));
                    polygonFCNamesArray.Add(String.Join(System.IO.Path.DirectorySeparatorChar.ToString(), new string[] { fileGDB, polygonFeatureClassName }));
                }

                // append all the lines
                IVariantArray parameterArray = new VarArrayClass();
                parameterArray.Add(String.Join(";", linesFCNamesArray.ToArray()));
                parameterArray.Add(targetLineFCName);

                gpResults = geoProcessor.Execute("Append_management", parameterArray, TrackCancel);

                IGPMessages messages = gpResults.GetResultMessages();
                toolMessages.AddMessages(gpResults.GetResultMessages());

#if DEBUG
                for (int i = 0; i < messages.Count; i++)
                {
                    System.Diagnostics.Debug.WriteLine(messages.GetMessage(i).Description);
                }
#endif

                // append all the polygons
                parameterArray = new VarArrayClass();
                parameterArray.Add(String.Join(";", polygonFCNamesArray.ToArray()));
                parameterArray.Add(targetPolygonFCName);

                gpResults = geoProcessor.Execute("Append_management", parameterArray, TrackCancel);

                messages = gpResults.GetResultMessages();
                toolMessages.AddMessages(gpResults.GetResultMessages());

#if DEBUG
                for (int i = 0; i < messages.Count; i++)
                {
                    System.Diagnostics.Debug.WriteLine(messages.GetMessage(i).Description);
                }
#endif

                // delete the temp loading fgdbs
                for (int gdbIndex = 0; gdbIndex < relationGDBNames.Count; gdbIndex++)
                {
                    parameterArray = new VarArrayClass();
                    parameterArray.Add(relationGDBNames[gdbIndex]);
                    geoProcessor.Execute("Delete_management", parameterArray, new CancelTrackerClass());
                }
                #endregion

                #region load super-relations

                using (ComReleaser comReleaser = new ComReleaser())
                {
                    IWorkspaceFactory workspaceFactory = new FileGDBWorkspaceFactoryClass();
                    comReleaser.ManageLifetime(workspaceFactory);

                    for (int gdbIndex = 0; gdbIndex < relationGDBNames.Count; gdbIndex++)
                    {
                        FileInfo gdbFileInfo = new FileInfo(relationGDBNames[gdbIndex]);

                        if (!gdbFileInfo.Exists)
                        {
                            IWorkspaceName workspaceName = workspaceFactory.Create(gdbFileInfo.DirectoryName, gdbFileInfo.Name, new PropertySetClass(), 0);
                            comReleaser.ManageLifetime(workspaceName);
                        }
                    }
                }


                loadSuperRelationParameterValue = "LOAD_SUPER_RELATION";
                toolMessages.AddMessage(String.Format(_resourceManager.GetString("GPTools_OSMGPMultiLoader_loading_super_relations")));
                executionStopwatch.Reset();
                executionStopwatch.Start();

                _manualResetEvent = new ManualResetEvent(false);
                _numberOfThreads = osmRelationFileNames.Count;


                for (int i = 0; i < osmRelationFileNames.Count; i++)
                {
                    Thread t = new Thread(new ParameterizedThreadStart(PythonLoadOSMRelations));
                    t.Start(new List<string>() { 
                        osmRelationFileNames[i],
                        loadSuperRelationParameterValue,
                        sourceLineFCName, 
                        sourcePolygonFCName,
                        String.Join(";", lineFieldNames.ToArray()),
                        String.Join(";", polygonFieldNames.ToArray()),
                        String.Join(System.IO.Path.DirectorySeparatorChar.ToString(), new string[] { relationGDBNames[i], lineFeatureClassName }),
                        String.Join(System.IO.Path.DirectorySeparatorChar.ToString(), new string[] { relationGDBNames[i], polygonFeatureClassName }),
                });
                }

                // wait for all nodes to complete loading before appending all into the target feature class
                _manualResetEvent.WaitOne();
                _manualResetEvent.Close();


                executionStopwatch.Stop();
                relationLoadingTimeSpan = executionStopwatch.Elapsed;
                toolMessages.AddMessage(String.Format(_resourceManager.GetString("GPTools_OSMGPMultiLoader_doneloading_super_relations"), relationLoadingTimeSpan.Hours, relationLoadingTimeSpan.Minutes, relationLoadingTimeSpan.Seconds));

                linesFCNamesArray = new List<string>(relationGDBNames.Count);
                polygonFCNamesArray = new List<string>(relationGDBNames.Count);

                // append all lines into the target feature class
                foreach (string fileGDB in relationGDBNames)
                {
                    linesFCNamesArray.Add(String.Join(System.IO.Path.DirectorySeparatorChar.ToString(), new string[] { fileGDB, lineFeatureClassName }));
                    polygonFCNamesArray.Add(String.Join(System.IO.Path.DirectorySeparatorChar.ToString(), new string[] { fileGDB, polygonFeatureClassName }));
                }

                // append all the lines
                parameterArray = new VarArrayClass();
                parameterArray.Add(String.Join(";", linesFCNamesArray.ToArray()));
                parameterArray.Add(targetLineFCName);

                gpResults = geoProcessor.Execute("Append_management", parameterArray, TrackCancel);

                messages = gpResults.GetResultMessages();
                toolMessages.AddMessages(gpResults.GetResultMessages());

#if DEBUG
                for (int i = 0; i < messages.Count; i++)
                {
                    System.Diagnostics.Debug.WriteLine(messages.GetMessage(i).Description);
                }
#endif

                // append all the polygons
                parameterArray = new VarArrayClass();
                parameterArray.Add(String.Join(";", polygonFCNamesArray.ToArray()));
                parameterArray.Add(targetPolygonFCName);

                gpResults = geoProcessor.Execute("Append_management", parameterArray, TrackCancel);

                messages = gpResults.GetResultMessages();
                toolMessages.AddMessages(gpResults.GetResultMessages());

#if DEBUG
                for (int i = 0; i < messages.Count; i++)
                {
                    System.Diagnostics.Debug.WriteLine(messages.GetMessage(i).Description);
                }
#endif

                // delete the temp loading fgdbs
                for (int gdbIndex = 0; gdbIndex < relationGDBNames.Count; gdbIndex++)
                {
                    parameterArray = new VarArrayClass();
                    parameterArray.Add(relationGDBNames[gdbIndex]);
                    geoProcessor.Execute("Delete_management", parameterArray, new CancelTrackerClass());
                }

                #endregion

                // delete the temp loading relation osm files
                foreach (string osmFile in osmRelationFileNames)
                {
                    try
                    {
                        System.IO.File.Delete(osmFile);
                    }
                    catch { }
                }


            }
        }

        internal void smallLoadOSMRelations(string osmFileLocation, string sourceLineFeatureClassLocation, string sourcePolygonFeatureClassLocation, string targetLineFeatureClassLocation, string targetPolygonFeatureClassLocation, List<string> lineFieldNames, List<string> polygonFieldNames, bool includeSuperRelations)
        {
            using (ComReleaser comReleaser = new ComReleaser())
            {
                List<tag> tags = null;

                IGPUtilities3 gpUtilities = new GPUtilitiesClass() as IGPUtilities3;
                comReleaser.ManageLifetime(gpUtilities);
                XmlReader relationFileXmlReader = null;

                try
                {
                    // info about the source lines
                    IFeatureClass sourceLineFeatureClass = gpUtilities.OpenFeatureClassFromString(sourceLineFeatureClassLocation);
                    comReleaser.ManageLifetime(sourceLineFeatureClass);
                    int osmSourceLineIDFieldIndex = sourceLineFeatureClass.FindField("OSMID");
                    string sourceSQLLineOSMID = sourceLineFeatureClass.SqlIdentifier("OSMID");

                    // info about the source polygons
                    IFeatureClass sourcePolygonFeatureClass = gpUtilities.OpenFeatureClassFromString(sourcePolygonFeatureClassLocation);
                    comReleaser.ManageLifetime(sourcePolygonFeatureClass);
                    int osmSourcePolygonIDFieldIndex = sourcePolygonFeatureClass.FindField("OSMID");
                    string sourceSQLPolygonOSMID = sourcePolygonFeatureClass.SqlIdentifier("OSMID");

                    // info about the target lines
                    IFeatureClass targetLineFeatureClass = gpUtilities.OpenFeatureClassFromString(targetLineFeatureClassLocation);
                    comReleaser.ManageLifetime(targetLineFeatureClass);
                    int osmTargetLineIDFieldIndex = targetLineFeatureClass.FindField("OSMID");
                    int osmTargetLineTagCollectionFieldIndex = targetLineFeatureClass.FindField("osmTags");
                    string targetSQLLineOSMID = targetLineFeatureClass.SqlIdentifier("OSMID");

                    Dictionary<string, int> mainLineAttributeFieldIndices = new Dictionary<string, int>();
                    foreach (string fieldName in lineFieldNames)
                    {
                        int currentFieldIndex = targetLineFeatureClass.FindField(OSMToolHelper.convert2AttributeFieldName(fieldName, null));

                        if (currentFieldIndex != -1)
                        {
                            mainLineAttributeFieldIndices.Add(OSMToolHelper.convert2AttributeFieldName(fieldName, null), currentFieldIndex);
                        }
                    }

                    // info about the target polygons
                    IFeatureClass targetPolygonFeatureClass = gpUtilities.OpenFeatureClassFromString(targetPolygonFeatureClassLocation);
                    comReleaser.ManageLifetime(targetPolygonFeatureClass);
                    int osmTargetPolygonIDFieldIndex = targetPolygonFeatureClass.FindField("OSMID");
                    int osmTargetPolygonTagCollectionFieldIndex = targetPolygonFeatureClass.FindField("osmTags");
                    string targetSQLPolygonOSMID = targetPolygonFeatureClass.SqlIdentifier("OSMID");

                    Dictionary<string, int> mainPolygonAttributeFieldIndices = new Dictionary<string, int>();
                    foreach (string fieldName in polygonFieldNames)
                    {
                        int currentFieldIndex = targetPolygonFeatureClass.FindField(OSMToolHelper.convert2AttributeFieldName(fieldName, null));

                        if (currentFieldIndex != -1)
                        {
                            mainPolygonAttributeFieldIndices.Add(OSMToolHelper.convert2AttributeFieldName(fieldName, null), currentFieldIndex);
                        }
                    }

                    relationFileXmlReader = XmlReader.Create(osmFileLocation);
                    relationFileXmlReader.ReadToFollowing("relation");


                    IFeatureBuffer lineFeatureBuffer = targetLineFeatureClass.CreateFeatureBuffer();
                    comReleaser.ManageLifetime(lineFeatureBuffer);
                    IFeatureCursor lineFeatureInsertCursor = targetLineFeatureClass.Insert(true);
                    comReleaser.ManageLifetime(lineFeatureInsertCursor);


                    IFeatureBuffer polygonFeatureBuffer = targetPolygonFeatureClass.CreateFeatureBuffer();
                    comReleaser.ManageLifetime(polygonFeatureBuffer);
                    IFeatureCursor polygonFeatureInsertCursor = targetPolygonFeatureClass.Insert(true);
                    comReleaser.ManageLifetime(polygonFeatureInsertCursor);


                    IQueryFilter lineOSMIDQueryFilter = new QueryFilterClass();
                    // the line query filter for updates will not changes, so let's do that ahead of time
                    try
                    {
                        lineOSMIDQueryFilter.SubFields = sourceLineFeatureClass.ShapeFieldName + "," + sourceLineFeatureClass.Fields.get_Field(osmSourceLineIDFieldIndex).Name;
                    }
                    catch
                    { }

                    IQueryFilter polygonOSMIDQueryFilter = new QueryFilterClass();
                    // the line query filter for updates will not changes, so let's do that ahead of time
                    try
                    {
                        polygonOSMIDQueryFilter.SubFields = sourcePolygonFeatureClass.ShapeFieldName + "," + sourcePolygonFeatureClass.Fields.get_Field(osmSourcePolygonIDFieldIndex).Name;
                    }
                    catch
                    { }

                    IQueryFilter outerPolygonQueryFilter = new QueryFilterClass();
                    try
                    {
                        outerPolygonQueryFilter.SubFields = sourcePolygonFeatureClass.Fields.get_Field(osmTargetPolygonTagCollectionFieldIndex).Name + "," + sourcePolygonFeatureClass.Fields.get_Field(osmSourcePolygonIDFieldIndex).Name;
                    }
                    catch { }

                    polygonOSMIDQueryFilter.WhereClause = sourceSQLPolygonOSMID + " IN ('w1')";
                    IFeatureCursor searchPolygonCursor = sourcePolygonFeatureClass.Search(polygonOSMIDQueryFilter, false);
                    comReleaser.ManageLifetime(searchPolygonCursor);

                    lineOSMIDQueryFilter.WhereClause = sourceSQLLineOSMID + " IN ('w1')";
                    IFeatureCursor searchLineCursor = sourceLineFeatureClass.Search(lineOSMIDQueryFilter, false);
                    comReleaser.ManageLifetime(searchLineCursor);

                    TagKeyValueComparer routeTagComparer = new TagKeyValueComparer();

                    do {

                        try
                        {
                            string relationOSMID = "r" + relationFileXmlReader.GetAttribute("id");

                            string membersAndTags = relationFileXmlReader.ReadInnerXml();

                            bool relationIsComplete = true;

                            tags = new List<tag>();

                            Dictionary<string, List<string>> members = new Dictionary<string, List<string>>();

                            List<string> itemIDs = new List<string>();
                            List<string> outerIDs = new List<string>();
                            List<string> innerIDs = new List<string>();
                            List<string> subAreaIDs = new List<string>();

                            // determine the member of the relations and the tags
                            foreach (XElement item in ParseXml(membersAndTags))
                            {
                                if (item.Name == "member")
                                {
                                    // if the member is of type way, relation, point, or something else
                                    string memberType = item.Attribute("type").Value;
                                    string prefix = String.Empty;
                                    if (memberType == "node")
                                        prefix = "n";
                                    else if (memberType == "way")
                                        prefix = "w";
                                    else if (memberType == "relation")
                                        prefix = "r";

                                    string refID = item.Attribute("ref").Value;
                                    string role = item.Attribute("role").Value;

                                    if (role == "outer")
                                        outerIDs.Add(prefix + refID);

                                    if (role == "inner")
                                        innerIDs.Add(prefix + refID);

                                    if (role == "subarea")
                                        subAreaIDs.Add(prefix + refID);

                                    if (includeSuperRelations)
                                    {
                                        if (memberType == "way" || memberType == "relation")
                                            itemIDs.Add(prefix + refID);
                                    }
                                    else
                                    {
                                        if (memberType == "way")
                                            itemIDs.Add(prefix + refID);
                                    }

                                    if (!members.ContainsKey(memberType))
                                        members[memberType] = new List<string>();

                                    members[memberType].Add(prefix + refID);
                                }
                                else if (item.Name == "tag")
                                {
                                    tags.Add(new tag() { k = item.Attribute("k").Value, v = item.Attribute("v").Value });
                                }
                            }

                            // if instructed to ignore relations (even though containing relations)
                            // then empty out the list of collected IDs, and hence ignore the relation for loading
                            if (!includeSuperRelations && members.ContainsKey("relation"))
                                itemIDs.Clear();
                            else if (includeSuperRelations && !members.ContainsKey("relation"))
                                itemIDs.Clear();

                            // remove items categorized as subareas
                            if (includeSuperRelations)
                            {
                                foreach (var subAreaID in subAreaIDs)
                                {
                                    itemIDs.Remove(subAreaID);
                                }
                            }

                            bool isRoute = false;
                            // check for the existence of a route, route_master, network tag -> indicating a linear feature and overrules the 
                            // geometry determination of polygon or polyline
                            tag routeTag = new tag() { k = "type", v = "route" };
                            tag routeMasterTag = new tag() { k = "type", v = "route_master" };
                            tag networkTag = new tag() { k = "type", v = "network" };
                            if (tags.Contains(routeTag, routeTagComparer) || tags.Contains(routeMasterTag, routeTagComparer) || tags.Contains(networkTag, routeTagComparer))
                            {
                                isRoute = true;
                            }

                            bool checkOuter = false;
                            bool hasMultiPolygonTag = false;
                            tag multiPolygonTag = new tag() { k = "type", v = "multipolygon" };
                            if (tags.Contains(multiPolygonTag, routeTagComparer))
                            {
                                hasMultiPolygonTag = true;
                                if (tags.Count == 1)
                                {
                                    checkOuter = true;
                                }
                            }

                            // attempt to assemble the relation feature from the way and relation IDs
                            if (itemIDs.Count > 0)
                            {
                                bool isArea = false;

                                List<string> idRequests = SplitOSMIDRequests(itemIDs);
                                List<IGeometry> itemGeometries = new List<IGeometry>(itemIDs.Count);
                                Dictionary<string, int> itemPositionDictionary = new Dictionary<string, int>(itemIDs.Count);

                                // build a list of way ids we can use to determine the order in the relation
                                for (int index = 0; index < itemIDs.Count; index++)
                                {
                                    itemGeometries.Add(new PointClass());
                                    itemPositionDictionary[itemIDs[index]] = index;
                                }

                                List<string> polygonIDs = new List<string>();

                                // check in the line feature class first
                                foreach (string request in idRequests)
                                {
                                    string idCompareString = request;
                                    lineOSMIDQueryFilter.WhereClause = sourceSQLLineOSMID + " IN " + request;

                                    searchLineCursor = sourceLineFeatureClass.Search(lineOSMIDQueryFilter, false);

                                        IFeature lineFeature = searchLineCursor.NextFeature();

                                        while (lineFeature != null)
                                        {
                                            // determine the ID of the line in with respect to the node position
                                            string lineOSMIDString = Convert.ToString(lineFeature.get_Value(osmSourceLineIDFieldIndex));

                                            // remove the ID from the request string
                                            idCompareString = idCompareString.Replace(lineOSMIDString, String.Empty);

                                            itemGeometries[itemPositionDictionary[lineOSMIDString]] = lineFeature.ShapeCopy;

                                            lineFeature = searchLineCursor.NextFeature();
                                        }

                                    idCompareString = CleanReportedIDs(idCompareString);

                                    // after removing the commas we should be left with only paranthesis left, meaning a string of length 2
                                    // if we have more then we have found a missing way as a line we still need to search the polygons
                                    if (idCompareString.Length > 2)
                                    {
                                        string[] wayIDs = idCompareString.Substring(1, idCompareString.Length - 2).Split(",".ToCharArray());
                                        polygonIDs.AddRange(wayIDs);
                                    }
                                }

                                // next collect the polygon geometries
                                idRequests = SplitOSMIDRequests(polygonIDs);

                                foreach (string request in idRequests)
                                {
                                    string idCompareString = request;
                                    polygonOSMIDQueryFilter.WhereClause = sourceSQLPolygonOSMID + " IN " + request;

                                    searchPolygonCursor = sourcePolygonFeatureClass.Search(polygonOSMIDQueryFilter, false);

                                        IFeature polygonFeature = searchPolygonCursor.NextFeature();

                                        while (polygonFeature != null)
                                        {
                                            // determine the ID of the polygon in with respect to the way position
                                            string polygonOSMIDString = Convert.ToString(polygonFeature.get_Value(osmSourcePolygonIDFieldIndex));

                                            // remove the ID from the request string
                                            idCompareString = idCompareString.Replace(polygonOSMIDString, String.Empty);

                                            itemGeometries[itemPositionDictionary[polygonOSMIDString]] = polygonFeature.ShapeCopy;

                                            polygonFeature = searchPolygonCursor.NextFeature();
                                        }

                                    idCompareString = CleanReportedIDs(idCompareString);

                                    // after removing the commas we should be left with only paranthesis left, meaning a string of length 2
                                    // if we have more then we have found a missing way as a line we still need to search the polygons
                                    if (idCompareString.Length > 2)
                                    {
                                        relationIsComplete = false;
                                        break;
                                    }
                                }

                                if (relationIsComplete == true)
                                {
                                    List<IGeometryCollection> relationParts = new List<IGeometryCollection>();

                                    // special case for multipolygon
                                    // in this case we know we are dealing with polygon -- in other words, we just need to piece it together
                                    if (hasMultiPolygonTag)
                                    {
                                        isArea = true;

                                        #region multipolygon
                                        // find the first polyline in the geometry collection
                                        int startIndex = 0;
                                        foreach (var itemGeometry in itemGeometries)
                                        {
                                            startIndex++;

                                            if (itemGeometry is IPolyline)
                                            {
                                                relationParts.Add(itemGeometry as IGeometryCollection);
                                                break;
                                            }
                                        }

                                        for (int i = startIndex; i < itemGeometries.Count; i++)
                                        {
                                            IPolyline wayGeometry = itemGeometries[i] as IPolyline;

                                            // first pieces the polylines together and into parts
                                            if (wayGeometry == null)
                                                continue;

                                            IGeometry mergedGeometry = FitPolylinePiecesTogether(relationParts[relationParts.Count - 1] as IPolyline, wayGeometry, false);

                                            if (mergedGeometry == null)
                                                relationParts.Add(wayGeometry as IGeometryCollection);
                                            else if (mergedGeometry is IPolyline)
                                                relationParts[relationParts.Count - 1] = mergedGeometry as IGeometryCollection;
                                            else if (mergedGeometry is IPolygon)
                                            {
                                                relationParts[relationParts.Count - 1] = mergedGeometry as IGeometryCollection;

                                                for (int newPartIndex = i + 1; newPartIndex < itemGeometries.Count; newPartIndex++)
                                                {
                                                    if (itemGeometries[newPartIndex] is IPolyline)
                                                    {
                                                        relationParts.Add(itemGeometries[newPartIndex] as IGeometryCollection);
                                                        i = newPartIndex;
                                                        break;
                                                    }
                                                }
                                            }
                                        }

                                        for (int i = 0; i < itemGeometries.Count; i++)
                                        {
                                            IPolygon wayGeometry = itemGeometries[i] as IPolygon;

                                            if (wayGeometry != null)
                                            {
                                                relationParts.Add(wayGeometry as IGeometryCollection);
                                            }
                                        }
                                        #endregion
                                    }
                                    else
                                    {
                                        int startIndex = 0;
                                        foreach (var itemGeometry in itemGeometries)
                                        {
                                            startIndex++;

                                            if (itemGeometry is IPolyline)
                                            {
                                                relationParts.Add(itemGeometry as IGeometryCollection);
                                                break;
                                            }
                                            else if (itemGeometry is IPolygon)
                                            {
                                                if (!isRoute)
                                                {
                                                    isArea = true;
                                                    relationParts.Add(itemGeometry as IGeometryCollection);
                                                }
                                            }
                                        }

                                        for (int i = startIndex; i < itemGeometries.Count; i++)
                                        {
                                            IGeometry wayGeometry = itemGeometries[i];

                                            if (wayGeometry is IPolygon)
                                            {
                                                if (!isRoute)
                                                {
                                                    isArea = true;
                                                    relationParts.Add(wayGeometry as IGeometryCollection);

                                                    #region Ensure that the next part is a polyline
                                                    for (int newPartIndex = i + 1; newPartIndex < itemGeometries.Count; newPartIndex++)
                                                    {
                                                        if (itemGeometries[newPartIndex] is IPolyline)
                                                        {
                                                            relationParts.Add(itemGeometries[newPartIndex] as IGeometryCollection);
                                                            i = newPartIndex;
                                                            break;
                                                        }
                                                        else if (itemGeometries[newPartIndex] is IPolygon)
                                                        {
                                                            if (!isRoute)
                                                            {
                                                                isArea = true;
                                                                relationParts.Add(itemGeometries[newPartIndex] as IGeometryCollection);
                                                            }

                                                            i = newPartIndex;
                                                        }
                                                    }
                                                    #endregion
                                                }
                                            }
                                            else if (wayGeometry is IPolyline)
                                            {
                                                IGeometry mergedGeometry = FitPolylinePiecesTogether(relationParts[relationParts.Count - 1] as IPolyline, wayGeometry as IPolyline, isRoute);

                                                if (mergedGeometry == null)
                                                    relationParts.Add(wayGeometry as IGeometryCollection);
                                                else if (mergedGeometry is IPolyline)
                                                    relationParts[relationParts.Count - 1] = mergedGeometry as IGeometryCollection;
                                                else if (mergedGeometry is IPolygon)
                                                {
                                                    isArea = true;
                                                    relationParts[relationParts.Count - 1] = mergedGeometry as IGeometryCollection;

                                                    #region Ensure that the next part is a polyline
                                                    for (int newPartIndex = i + 1; newPartIndex < itemGeometries.Count; newPartIndex++)
                                                    {
                                                        if (itemGeometries[newPartIndex] is IPolyline)
                                                        {
                                                            relationParts.Add(itemGeometries[newPartIndex] as IGeometryCollection);
                                                            i = newPartIndex;
                                                            break;
                                                        }
                                                        else if (itemGeometries[newPartIndex] is IPolygon)
                                                        {
                                                            if (!isRoute)
                                                            {
                                                                isArea = true;
                                                                relationParts.Add(itemGeometries[newPartIndex] as IGeometryCollection);
                                                            }

                                                            i = newPartIndex;
                                                        }
                                                    }
                                                    #endregion
                                                }
                                            }
                                        }
                                    }

                                    // some pieces might be still out of order - this call will reorder and connect linear geometries as well
                                    // as close outstanding polygons
                                    relationParts = HarmonizeGeometries(relationParts, isRoute);

                                    //re-assess the type of geometry, additional lines might have joined into polygons
                                    // favor tags over geometry determination
                                    if (!isRoute & !hasMultiPolygonTag)
                                    {
                                        isArea = true;

                                        foreach (var part in relationParts)
                                        {
                                            if (part is IPolyline)
                                            {
                                                isArea = false;
                                                break;
                                            }
                                        }
                                    }

                                    // now assemble the final geometry based on our discovery if there is an area and store the relation as a new feature
                                    if (isArea)
                                    {
                                        #region transfer for outer tags to relation itself
                                        // if needed to one more request to assemble the information of the outer rings to be transfer to the "empty"
                                        // relation enity
                                        if (checkOuter)
                                        {
                                            idRequests = SplitOSMIDRequests(outerIDs);

                                            foreach (string request in idRequests)
                                            {
                                                string idCompareString = request;
                                                outerPolygonQueryFilter.WhereClause = sourceSQLPolygonOSMID + " IN " + request;

                                                searchPolygonCursor = sourcePolygonFeatureClass.Search(outerPolygonQueryFilter, false);

                                                IFeature polygonFeature = searchPolygonCursor.NextFeature();

                                                while (polygonFeature != null)
                                                {
                                                    // determine the ID of the polygon in with respect to the way position
                                                    tag[] outerRingsTags = _osmUtility.retrieveOSMTags(polygonFeature, osmTargetPolygonTagCollectionFieldIndex, null);

                                                    if (outerRingsTags != null)
                                                    {
                                                        if (outerRingsTags.Count() > 0)
                                                        {
                                                            foreach (var outerTag in outerRingsTags)
                                                            {
                                                                if (!tags.Contains(outerTag, new TagKeyComparer()))
                                                                {
                                                                    tags.Add(outerTag);
                                                                }
                                                            }
                                                        }
                                                    }

                                                    polygonFeature = searchPolygonCursor.NextFeature();
                                                }
                                            }
                                        }
                                        #endregion

                                        IGeometryCollection relationPolygon = new PolygonClass();

                                        foreach (var part in relationParts)
                                        {
                                            for (int ringIndex = 0; ringIndex < part.GeometryCount; ringIndex++)
                                            {
                                                ISegmentCollection ringSegmentCollection = new RingClass();
                                                ringSegmentCollection.AddSegmentCollection(part.get_Geometry(ringIndex) as ISegmentCollection);
                                                relationPolygon.AddGeometry(ringSegmentCollection as IGeometry);
                                            }

                                        }

                                        ((ITopologicalOperator2)relationPolygon).IsKnownSimple_2 = false;
                                        ((IPolygon4)relationPolygon).SimplifyEx(true, false, false);


                                        // set the shape
                                        polygonFeatureBuffer.Shape = relationPolygon as IGeometry;

                                        // insert the relation ID
                                        polygonFeatureBuffer.set_Value(osmTargetPolygonIDFieldIndex, relationOSMID);

                                        // insert the tags into the appropriate fields
                                        insertTags(mainPolygonAttributeFieldIndices, osmTargetPolygonTagCollectionFieldIndex, polygonFeatureBuffer, tags.ToArray());

                                        try
                                        {
                                            // load the polygon feature
                                            polygonFeatureInsertCursor.InsertFeature(polygonFeatureBuffer);
                                        }
                                        catch (Exception inEx)
                                        {
#if DEBUG
                                            foreach (var item in tags)
                                            {
                                                System.Diagnostics.Debug.WriteLine(string.Format("{0},{1}", item.k, item.v));
                                            }
                                            System.Diagnostics.Debug.WriteLine(inEx.Message);
                                            System.Diagnostics.Debug.WriteLine(inEx.StackTrace);
#endif
                                        }
                                    }
                                    else
                                    {
                                        IGeometryCollection relationPolyline = new PolylineClass();

                                        foreach (var part in relationParts)
                                        {
                                            for (int pathIndex = 0; pathIndex < part.GeometryCount; pathIndex++)
                                            {
                                                ISegmentCollection pathSegmentCollection = new PathClass();
                                                pathSegmentCollection.AddSegmentCollection(part.get_Geometry(pathIndex) as ISegmentCollection);
                                                relationPolyline.AddGeometry(pathSegmentCollection as IGeometry);
                                            }
                                        }

                                        // set the shape
                                        lineFeatureBuffer.Shape = relationPolyline as IGeometry;

                                        // insert the relation ID
                                        lineFeatureBuffer.set_Value(osmTargetLineIDFieldIndex, relationOSMID);

                                        // insert the tags into the appropriate fields
                                        insertTags(mainLineAttributeFieldIndices, osmTargetLineIDFieldIndex, lineFeatureBuffer, tags.ToArray());

                                        try
                                        {
                                            // load the line feature
                                            lineFeatureInsertCursor.InsertFeature(lineFeatureBuffer);
                                        }
                                        catch (Exception inEx)
                                        {
#if DEBUG
                                            foreach (var item in tags)
                                            {
                                                System.Diagnostics.Debug.WriteLine(string.Format("{0},{1}", item.k, item.v));
                                            }
                                            System.Diagnostics.Debug.WriteLine(inEx.Message);
                                            System.Diagnostics.Debug.WriteLine(inEx.StackTrace);
#endif
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception hmmEx)
                        {
#if DEBUG
                            System.Diagnostics.Debug.WriteLine("Unexpected error : !!!!!!");
                            System.Diagnostics.Debug.WriteLine(hmmEx.Message);
                            System.Diagnostics.Debug.WriteLine(hmmEx.StackTrace);
#endif
                        }

                        // if we encounter a whitespace, attempt to find the next relation if it exists
                        if (relationFileXmlReader.NodeType != XmlNodeType.Element)
                            relationFileXmlReader.ReadToFollowing("relation");

                    } while (relationFileXmlReader.Name == "relation");

                }
                catch (Exception ex)
                {
#if DEBUG
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                    System.Diagnostics.Debug.WriteLine(ex.StackTrace);
#endif
                }
                finally
                {
                    if (relationFileXmlReader != null)
                        relationFileXmlReader.Close();
                }
            }
        }

        private List<IGeometryCollection> HarmonizeGeometries(List<IGeometryCollection> relationParts, bool isLinear)
        {
            List<IGeometryCollection> harmonizedList = new List<IGeometryCollection>();

            List<int> polylineIndices = new List<int>();
            List<bool> isVisited = new List<bool>();

            for (int partIndex = 0; partIndex < relationParts.Count; partIndex++)
            {
                if (relationParts[partIndex] is IPolyline)
                {
                    polylineIndices.Add(partIndex);
                    isVisited.Add(true);
                }
                else if (relationParts[partIndex] is IPolygon)
                    harmonizedList.Add(relationParts[partIndex] as IGeometryCollection);
            }

            if (polylineIndices.Count < 2)
                return relationParts;

            IPolyline tempPolyline = ((IClone)relationParts[polylineIndices[0]]).Clone() as IPolyline;
            isVisited[0] = false;

            int startIndex = 1;

            while (startIndex < polylineIndices.Count)
            {
                for (int i = startIndex; i < polylineIndices.Count; i++)
                {
                    if (isVisited[i] == false)
                        continue;

                    IGeometry compareGeometry = FitPolylinePiecesTogether(tempPolyline, relationParts[polylineIndices[i]] as IPolyline, isLinear);

                    if (compareGeometry is IPolyline)
                    {
                        tempPolyline = ((IClone)compareGeometry).Clone() as IPolyline;
                        isVisited[i] = false;
                        i = startIndex - 1;
                    }
                    else if (compareGeometry is IPolygon)
                    {
                        harmonizedList.Add(compareGeometry as IGeometryCollection);
                        isVisited[i] = false;
                        tempPolyline = null;

                        for (int ni = startIndex; ni < polylineIndices.Count; ni++)
                        {
                            if (isVisited[ni] == false)
                                continue;

                            tempPolyline = ((IClone)relationParts[polylineIndices[ni]]).Clone() as IPolyline;
                            isVisited[ni] = false;
                            i = startIndex - 1;
                            break;
                        }
                    }
                }

                if (tempPolyline == null)
                    startIndex = polylineIndices.Count;
                else
                {
                    harmonizedList.Add(((IClone)tempPolyline).Clone() as IGeometryCollection);
                    tempPolyline = null;

                    for (int i = startIndex; i < polylineIndices.Count; i++)
                    {
                        if (isVisited[i] == false)
                            continue;
                        else
                        {
                            startIndex++;
                            tempPolyline = ((IClone)relationParts[polylineIndices[i]]).Clone() as IPolyline;
                            isVisited[i] = false;
                            break;
                        }
                    }
                }
            }

            if (tempPolyline != null)
            {
                harmonizedList.Add(tempPolyline as IGeometryCollection);
            }

            return harmonizedList;
        }

        /// <summary>
        /// Attempts to merge two polylines if the to and from points are coincident
        /// </summary>
        /// <param name="partOne"></param>
        /// <param name="partTwo"></param>
        /// <returns>A Null pointer if the two polylines are disjoint, a polyline if there is 
        /// a coincidence in the from or to points, or a polygon if the to and from point of the merged polyline
        /// are coincident, so they are forming a closed ring.</returns>
        private IGeometry FitPolylinePiecesTogether(IPolyline partOne, IPolyline partTwo, bool isLinear)
        {
            IGeometry mergedPart = null;

                IPoint partOneFromPoint = null;
                IPoint partOneToPoint = null;

                if (partOne == null)
                    throw new NullReferenceException("partOne is Null");

                if (partTwo == null)
                    throw new NullReferenceException("partTwo is Null");

                partOneFromPoint = partOne.FromPoint;
                partOneToPoint = partOne.ToPoint;

                bool FromPointConnect = ((IRelationalOperator)partOneToPoint).Equals(partTwo.FromPoint);
                bool ToPointConnect = ((IRelationalOperator)partOneToPoint).Equals(partTwo.ToPoint);

                if (FromPointConnect || ToPointConnect)
                {
                if (FromPointConnect)
                {
                    mergedPart = new PolylineClass();
                    ((IGeometryCollection) mergedPart).AddGeometryCollection(partOne as IGeometryCollection);
                    ((IGeometryCollection) mergedPart).AddGeometryCollection(partTwo as IGeometryCollection);
                }

                if (ToPointConnect)
                {
                    mergedPart = new PolylineClass();
                    ((IGeometryCollection) mergedPart).AddGeometryCollection(partOne as IGeometryCollection);

                    IPolyline flippedPartTwoGeometry = ((IClone) partTwo).Clone() as IPolyline;
                    flippedPartTwoGeometry.ReverseOrientation();

                    ((IGeometryCollection) mergedPart).AddGeometryCollection(((IClone)flippedPartTwoGeometry).Clone() as IGeometryCollection);
                }
                }

                FromPointConnect = ((IRelationalOperator)partOneFromPoint).Equals(partTwo.FromPoint);
                ToPointConnect = ((IRelationalOperator)partOneFromPoint).Equals(partTwo.ToPoint);

                if (FromPointConnect || ToPointConnect)
                {
                if (FromPointConnect)
                {
                    mergedPart = new PolylineClass();

                    IPolyline flippedPartOneGeometry = ((IClone) partOne).Clone() as IPolyline;
                    flippedPartOneGeometry.ReverseOrientation();

                    ((IGeometryCollection) mergedPart).AddGeometryCollection(((IClone)flippedPartOneGeometry).Clone() as IGeometryCollection);
                    ((IGeometryCollection) mergedPart).AddGeometryCollection(partTwo as IGeometryCollection);
                }

                if (ToPointConnect)
                {
                    mergedPart = new PolylineClass();
                    ((IGeometryCollection) mergedPart).AddGeometryCollection(partTwo as IGeometryCollection);
                    ((IGeometryCollection) mergedPart).AddGeometryCollection(partOne as IGeometryCollection);
                }
                }

                if (!isLinear)
                {
                    // now check if from and to points on the merged polyline are coincident
                    if (mergedPart != null)
                    {
                        IPolyline mergedPolyline = mergedPart as IPolyline;

                        if (((IRelationalOperator)mergedPolyline.FromPoint).Equals(mergedPolyline.ToPoint))
                        {
                            IPolygon tempPolygon = new PolygonClass();
                            ((ISegmentCollection)tempPolygon).AddSegmentCollection(mergedPart as ISegmentCollection);

                            tempPolygon.Close();

                            mergedPart = ((IClone)tempPolygon).Clone() as IGeometry;
                        }
                    }
                }


            return mergedPart;
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
                            ICursor rowCursor = relationTable.Insert(true);
                            comReleaser.ManageLifetime(rowCursor);
                            IRowBuffer rowBuffer = null;

                            IFeatureCursor lineFeatureInsertCursor = osmLineFeatureClass.Insert(true);

                            comReleaser.ManageLifetime(lineFeatureInsertCursor);
                            IFeatureBuffer lineFeatureBuffer = null;

                            IFeatureCursor polygonFeatureInsertCursor = osmPolygonFeatureClass.Insert(true);

                            comReleaser.ManageLifetime(polygonFeatureInsertCursor);
                            IFeatureBuffer polygonFeatureBuffer = null;

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
                                                                    wayList.Add(memberItem.@ref, "line" + "_" + memberItem.role);
                                                                else
                                                                    wayList.Add(memberItem.@ref, "polygon" + "_" + memberItem.role);

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
                                                                    wayList.Add(memberItem.@ref, "line" + "_" + memberItem.role);
                                                                else
                                                                    wayList.Add(memberItem.@ref, "polygon" + "_" + memberItem.role);

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
                                                IPolygon relationMPPolygon = new PolygonClass();
                                                relationMPPolygon.SpatialReference = ((IGeoDataset)osmPolygonFeatureClass).SpatialReference;

                                                ISegmentCollection relationPolygonGeometryCollection = relationMPPolygon as ISegmentCollection;

                                                IQueryFilter osmIDQueryFilter = new QueryFilterClass();
                                                string sqlPolyOSMID = osmPolygonFeatureClass.SqlIdentifier("OSMID");
                                                string sqlLineOSMID = osmLineFeatureClass.SqlIdentifier("OSMID");
                                                object missing = Type.Missing;
                                                bool relationComplete = true;
                                                string missingWayID = String.Empty;

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

                                                    string wayType = wayKey.Value.Split(new Char[]{'_'})[0];

                                                    switch (wayType)
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
                                                    
#if DEBUG
                                                    System.Diagnostics.Debug.WriteLine("Relation (Polygon) #: " + relationDebugCount + " :___: " + currentRelation.id + " :___: " + wayKey);
#endif
                                                    using (ComReleaser relationComReleaser = new ComReleaser())
                                                    {
                                                        IFeatureCursor featureCursor = null;
                                                        switch (wayType)
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
                                                            ISegmentCollection ringCollection = partFeature.Shape as ISegmentCollection;

                                                            // test for available content in the geometry collection  
                                                            if (ringCollection.SegmentCount > 0)
                                                            {
                                                                // test if we dealing with a valid geometry
                                                                if (ringCollection.get_Segment(0).IsEmpty == false)
                                                                {
                                                                    // add it to the new geometry and mark the added geometry as a supporting element
                                                                    relationPolygonGeometryCollection.AddSegmentCollection(ringCollection);

                                                                    // TE - 10/14/2014 ( 1/5/2015 -- still under consideration)
                                                                    // the initial assessment if the feature is a supporting element based on the existence of tags
                                                                    // has been made, at this point I don't think there is a reason to reassess the nature of feature
                                                                    // based on its appearance in a relation
                                                                    if (osmSupportingElementPolygonFieldIndex > -1)
                                                                    {
                                                                        string roleType = wayKey.Value.Split(new Char[] { '_' })[1];

                                                                        // if the member of a relation has the role of "outer" and the tags are the same as that of the 
                                                                        // of the relation parent, then mark it as a supporting element
                                                                        if (roleType.ToLower().Equals("outer"))
                                                                        {
                                                                            if (partFeature.Shape.GeometryType == esriGeometryType.esriGeometryPolyline)
                                                                            {
                                                                                if (_osmUtility.AreTagsTheSame(relationTagList, partFeature, tagCollectionPolylineFieldIndex, null))
                                                                                {
                                                                                    partFeature.set_Value(osmSupportingElementPolylineFieldIndex, "yes");
                                                                                    partFeature.Store();
                                                                                }
                                                                            }
                                                                            else
                                                                            {
                                                                                if (_osmUtility.AreTagsTheSame(relationTagList, partFeature, tagCollectionPolygonFieldIndex, null))
                                                                                {
                                                                                    partFeature.set_Value(osmSupportingElementPolygonFieldIndex, "yes");
                                                                                    partFeature.Store();
                                                                                }
                                                                            }
                                                                        }
                                                                        //else
                                                                        //{
                                                                        //    // relation member without an explicit role or the role of "outer" are turned into
                                                                        //    // supporting features if they don't have relevant attribute
                                                                        //    if (!_osmUtility.DoesHaveKeys(partFeature, tagCollectionPolylineFieldIndex, null))
                                                                        //    {
                                                                        //        partFeature.set_Value(osmSupportingElementPolygonFieldIndex, "yes");
                                                                        //    }
                                                                        //}
                                                                    }
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
                                                                missingWayID = wayKey.Key;
                                                                relationComplete = false;
                                                                break; 
                                                            }
                                                        //}
                                                    }
                                                }

                                                // mark the relation as incomplete
                                                if (relationComplete == false)
                                                {
                                                    missingRelations.Add(currentRelation.id);
#if DEBUG
                                                    System.Diagnostics.Debug.WriteLine("Incomplete Polygon # " + currentRelation.id + "; missing Way ID #" + missingWayID);
#endif
                                                    continue;
                                                }

                                                // transform the added collections for geometries into a topological correct geometry representation
                                                ((IPolygon4)relationMPPolygon).SimplifyEx(true, false, false);

                                                polygonFeatureBuffer = osmPolygonFeatureClass.CreateFeatureBuffer();

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
                                                    polygonFeatureInsertCursor.InsertFeature(polygonFeatureBuffer);

                                                    //if ((relationCount % 5000) == 0)
                                                    //{
                                                    //    polygonFeatureInsertCursor.Flush();
                                                    //}
                                                }
                                                catch (Exception ex)
                                                {
                                                    polygonFeatureInsertCursor.Flush();

                                                    message.AddWarning(ex.Message + "(Polygon # " + currentRelation.id + ")");
                                                    message.AddWarning(ex.StackTrace);
                                                }
                                                #endregion
                                            }
                                            else if (detectedGeometryType == esriGeometryType.esriGeometryPolyline)
                                            {
                                                #region create multipart polyline geometry
                                                IPolyline relationMPPolyline = new PolylineClass();
                                                relationMPPolyline.SpatialReference = ((IGeoDataset)osmLineFeatureClass).SpatialReference;

                                                ISegmentCollection relationPolylineGeometryCollection = relationMPPolyline as ISegmentCollection;

                                                IQueryFilter osmIDQueryFilter = new QueryFilterClass();
                                                object missing = Type.Missing;
                                                bool relationComplete = true;
                                                string missingWayID = String.Empty;

                                                // loop through the 
                                                foreach (KeyValuePair<string, string> wayKey in wayList)
                                                {
                                                    if (TrackCancel.Continue() == false)
                                                    {
                                                        return missingRelations;
                                                    }

                                                    osmIDQueryFilter.WhereClause = osmLineFeatureClass.WhereClauseByExtensionVersion(wayKey.Key, "OSMID", 2);
#if DEBUG
                                                    System.Diagnostics.Debug.WriteLine("Relation (Polyline) #: " + relationDebugCount + " :___: " + currentRelation.id + " :___: " + wayKey);
#endif
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
                                                                ISegmentCollection pathCollection = partFeature.Shape as ISegmentCollection;
                                                                relationPolylineGeometryCollection.AddSegmentCollection(pathCollection);

                                                                // TE - 10/14/2014 - see comment above
                                                                if (osmSupportingElementPolylineFieldIndex > -1)
                                                                {
                                                                    string roleType = wayKey.Value.Split(new Char[] { '_' })[1];

                                                                    // if the member of a relation has the role of "outer" and the tags are the same as that of the 
                                                                    // of the relation parent, then mark it as a supporting element
                                                                    if (roleType.ToLower().Equals("outer"))
                                                                    {
                                                                        if (!_osmUtility.AreTagsTheSame(relationTagList, partFeature, tagCollectionPolylineFieldIndex, null))
                                                                        {
                                                                            partFeature.set_Value(osmSupportingElementPolylineFieldIndex, "yes");
                                                                            partFeature.Store();
                                                                        }
                                                                    }
                                                                }
                                                            }
                                                        }
                                                        else
                                                        {
                                                            missingWayID = wayKey.Key;
                                                            relationComplete = false;
                                                            break;
                                                        }
                                                    }
                                                }

                                                if (relationComplete == false)
                                                {
                                                    missingRelations.Add(currentRelation.id);
#if DEBUG
                                                    System.Diagnostics.Debug.WriteLine("Incomplete Polyline # " + currentRelation.id + "; missing Way ID #" + missingWayID);
#endif
                                                    continue;
                                                }

                                                lineFeatureBuffer = osmLineFeatureClass.CreateFeatureBuffer();

                                                relationMPPolyline.SimplifyNetwork();

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

                                                    //if ((relationCount % 5000) == 0)
                                                    //{
                                                    //    lineFeatureInsertCursor.Flush();
                                                    //}
                                                }
                                                catch (Exception ex)
                                                {
                                                    lineFeatureInsertCursor.Flush();

                                                    message.AddWarning(ex.Message + "(Line #" + currentRelation.id + ")");
                                                }
                                                #endregion

                                            }
                                            else if (detectedGeometryType == esriGeometryType.esriGeometryPoint)
                                            {
#if DEBUG
                                                System.Diagnostics.Debug.WriteLine("Relation #: " + relationDebugCount + " :____: POINT!!!");
#endif

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

#if DEBUG
                                                System.Diagnostics.Debug.WriteLine("Relation #: " + relationDebugCount + " :____: Kept as relation");
#endif

                                                rowBuffer = relationTable.CreateRowBuffer();

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

                                                    //if ((relationCount % 5000) == 0)
                                                    //{
                                                    //    rowCursor.Flush();
                                                    //}

                                                    relationIndexRebuildRequired = true;
                                                }
                                                catch (Exception ex)
                                                {
#if DEBUG
                                                    System.Diagnostics.Debug.WriteLine(ex.Message + " (row #" + currentRelation.id + ")");
#endif
                                                    message.AddWarning(ex.Message + " (row #" + currentRelation.id + ")");
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

                                            relationCount = relationCount + 1;

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
#if DEBUG
                                            System.Diagnostics.Debug.WriteLine(ex.Message);
                                            System.Diagnostics.Debug.WriteLine(ex.StackTrace);
#endif
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
                                    } // relation element
                                } // is start element?
                            } // osmFileXmlReader

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

                        string fcLocation = GetLocationString(targetGPValue, osmLineFeatureClass);
                        UpdateSpatialGridIndex(TrackCancel, message, geoProcessor, fcLocation, false);

                        fcLocation = GetLocationString(targetGPValue, osmPolygonFeatureClass);
                        UpdateSpatialGridIndex(TrackCancel, message, geoProcessor, fcLocation, false);

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
                int tagFieldIndex = polygonFeatureClass.FindField("osmTags");
                int osmSupportingElementFieldIndex = polygonFeatureClass.FindField("osmSupportingElement");
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
                                osmIDQueryFilter.WhereClause = polygonFeatureClass.WhereClauseByExtensionVersion(currentRelationMember.@ref, "OSMID", 2);

                                IFeatureCursor featureCursor = polygonFeatureClass.Update(osmIDQueryFilter, false);
                                comReleaser.ManageLifetime(featureCursor);

                                IFeature foundPolygonFeature = featureCursor.NextFeature();

                                if (foundPolygonFeature == null)
                                    continue;

                                tag[] foundTags = _osmUtility.retrieveOSMTags(foundPolygonFeature, tagFieldIndex, ((IDataset)polygonFeatureClass).Workspace);

                                // set this feature from which we transfer to become a supporting element
                                if (osmSupportingElementFieldIndex > -1)
                                    foundPolygonFeature.set_Value(osmSupportingElementFieldIndex, "yes");

                                featureCursor.UpdateFeature(foundPolygonFeature);

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

        public static bool IsThisWayALine(List<tag> tags, List<string> nodeIDs)
        {
            bool isALine = true;
            bool startAndEndCoincide = false;

            try
            {
                if (nodeIDs[0] == nodeIDs[nodeIDs.Count - 1])
                {
                    startAndEndCoincide = true;
                    isALine = false;
                }
                else
                {
                    startAndEndCoincide = false;
                }

                // coastlines are special cases and we will accept them as lines only
                //bool isCoastline = false;

                tag coastlineTag = new tag();
                coastlineTag.k = "natural";
                coastlineTag.v = "coastline";


                tag areaTag = new tag();
                areaTag.k = "area";
                areaTag.v = "yes";

                tag highwayTag = new tag();
                highwayTag.k = "highway";
                highwayTag.v = "something";

                tag routeTag = new tag();
                routeTag.k = "type";
                routeTag.v = "route";

                tag serviceParkingTag = new tag();
                serviceParkingTag.k = "service";
                serviceParkingTag.v = "parking_aisle";

                //if (tags.Contains(coastlineTag, new TagKeyValueComparer()))
                //{
                //    isCoastline = true;
                //    isALine = true;
                //    return isALine;
                //}

                if (tags.Contains(highwayTag, new TagKeyComparer()))
                {
                    if (tags.Contains(serviceParkingTag, new TagKeyValueComparer()))
                    { } // do nothing
                    else
                        isALine = true;
                }

                if (tags.Contains(areaTag, new TagKeyValueComparer()))
                {
                    if (startAndEndCoincide)
                        isALine = false;
                    else
                        isALine = true;
                }

                if (tags.Contains(routeTag, new TagKeyValueComparer()))
                {
                    isALine = true;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
            }

            return isALine;
        }

        public static bool IsThisWayALine(way currentway)
        {
            bool isALine = true;
            bool startAndEndCoincide = false;

            try
            {
                if (currentway.nd != null)
                {
                    if (currentway.nd[0].@ref == currentway.nd[currentway.nd.Length - 1].@ref)
                    {
                        startAndEndCoincide = true;
                        isALine = false;
                    }
                    else
                    {
                        startAndEndCoincide = false;
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

                    tag serviceParkingTag = new tag();
                    serviceParkingTag.k = "service";
                    serviceParkingTag.v = "parking_aisle";

                    if (currentway.tag.Contains(coastlineTag, new TagKeyValueComparer()))
                    {
                        isCoastline = true;
                        isALine = true;
                        return isALine;
                    }

                    if (currentway.tag.Contains(highwayTag, new TagKeyComparer()))
                    {
                        if (currentway.tag.Contains(serviceParkingTag, new TagKeyValueComparer()))
                        {} // do nothing
                        else
                            isALine = true;
                    }

                    if (currentway.tag.Contains(areaTag, new TagKeyValueComparer()))
                    {
                        if (isCoastline == false)
                        {
                            // only consider the area=yes combination if the way closes onto itself
                            // otherwise it is most likely an attribute error
                            if (startAndEndCoincide)
                                isALine = false;
                            else
                                isALine = true;
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

        // split to osm element ids into manageable chunks of db requests
        internal List<string> SplitOSMIDRequests<T>(List<T> osmElements, int extensionVersion) where T : class
        {
            List<string> osmIDRequests = new List<string>();

            if (osmElements == null)
                return osmIDRequests;

            if (osmElements.Count == 0)
                return osmIDRequests;

            try
            {
                StringBuilder newQueryString = new StringBuilder();
                newQueryString.Append("(");

                foreach (T currentElement in osmElements)
                {
                    string elementID = String.Empty;
                    if (currentElement is node)
                        elementID = (currentElement as node).id;
                    else if (currentElement is way)
                        elementID = (currentElement as way).id;
                    else if (currentElement is relation)
                        elementID = (currentElement as relation).id;

                    if (extensionVersion == 1)
                        newQueryString.Append(elementID + ",");
                    else if (extensionVersion == 2)
                    {
                        newQueryString.Append("'");
                        newQueryString.Append(elementID);
                        newQueryString.Append("'");
                        newQueryString.Append(",");
                    }

                    // not too sure about this hard coded length of 2048
                    // since the SQL implementation is data source dependent
                    if (newQueryString.Length > 2048)
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

        internal List<string> SplitOSMIDRequests(List<string> nodeIDs)
        {
            List<string> osmIDRequests = new List<string>();

            if (nodeIDs == null)
                return osmIDRequests;

            if (nodeIDs.Count == 0)
                return osmIDRequests;

            try
            {
                StringBuilder newQueryString = new StringBuilder();
                newQueryString.Append("(");

                foreach (string nodeID in nodeIDs)
                {
                    newQueryString.Append("'");
                    newQueryString.Append(nodeID);
                    newQueryString.Append("'");
                    newQueryString.Append(",");

                    // not too sure about this hard coded length of 2048
                    // since the SQL implementation is data source dependent
                    if (newQueryString.Length > 2048)
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

                    // not too sure about this hard coded length of 2048
                    // since the SQL implementation is data source dependent
                    if (newQueryString.Length > 2048)
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
                        osmIDQueryFilter.SubFields = lineFeatureClass.OIDFieldName + "," + lineFeatureClass.ShapeFieldName;

                        IFeatureCursor lineFeatureCursor = lineFeatureClass.Search(osmIDQueryFilter, false);
                        comReleaser.ManageLifetime(lineFeatureCursor);

                        IFeature foundLineFeature = lineFeatureCursor.NextFeature();
                        if (foundLineFeature != null)
                        {
                            determinedGeometryType = osmRelationGeometryType.osmPolyline;
                            return determinedGeometryType;
                        }

                        osmIDQueryFilter.WhereClause = polygonFeatureClass.WhereClauseByExtensionVersion(osmIDtoFind, "OSMID", 2);
                        osmIDQueryFilter.SubFields = polygonFeatureClass.OIDFieldName + "," + polygonFeatureClass.ShapeFieldName;

                        IFeatureCursor polygonFeatureCursor = polygonFeatureClass.Search(osmIDQueryFilter, false);
                        comReleaser.ManageLifetime(polygonFeatureCursor);

                        IFeature foundPolygonFeature = polygonFeatureCursor.NextFeature();
                        if (foundPolygonFeature != null)
                        {
                            determinedGeometryType = osmRelationGeometryType.osmPolygon;
                            return determinedGeometryType;
                        }

                        osmIDQueryFilter.WhereClause = relationTable.WhereClauseByExtensionVersion(osmIDtoFind, "OSMID", 2);
                        osmIDQueryFilter.SubFields = relationTable.OIDFieldName;

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

                            if ((currentTag.v.ToUpper().Equals("ROUTE")) || (currentTag.v.ToUpper().Equals("ROUTE_MASTER")) || (currentTag.v.ToUpper().Equals("NETWORK")))
                            {
                                detectedGeometryType = esriGeometryType.esriGeometryPolyline;
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
                                osmIDQueryFilter.SubFields = osmLineFeatureClass.OIDFieldName + "," + osmLineFeatureClass.ShapeFieldName;

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
                                osmIDQueryFilter.SubFields = osmPolygonFeatureClass.OIDFieldName + "," + osmPolygonFeatureClass.ShapeFieldName;

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
                                osmIDQueryFilter.SubFields = relationTable.OIDFieldName;

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

    /// <summary>
    /// Adaptation from the implementation at
    /// https://code.google.com/p/tambon/source/browse/trunk/AHGeo/GeoHash.cs
    /// </summary>
    static internal class GeoHash
    {
        private static Char[] _Digits = { '0', '1', '2', '3', '4', '5', '6', '7', '8',
                        '9', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'j', 'k', 'm', 'n', 'p',
                        'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z' };

        private static int _NumberOfBits = 6 * 5;
        private static Dictionary<Char, Int32> _LookupTable = CreateLookup();

        private static Dictionary<Char, Int32> CreateLookup()
        {

            Dictionary<Char, Int32> result = new Dictionary<char, Int32>();
            Int32 i = 0;

            foreach (Char c in _Digits)
            {
                result[c] = i;
                i++;
            }

            return result;
        }

        private static double GeoHashDecode(BitArray bits, double floorValue, double ceilingValue)
        {
            Double middle = 0;
            Double floor = floorValue;
            Double ceiling = ceilingValue;

            for (Int32 i = 0; i < bits.Length; i++)
            {
                middle = (floor + ceiling) / 2;

                if (bits[i])
                {
                    floor = middle;
                }
                else
                {
                    ceiling = middle;
                }
            }

            return middle;
        }

        private static BitArray GeoHashEncode(double value, double floorValue, double ceilingValue)
        {
            BitArray result = new BitArray(_NumberOfBits);
            Double floor = floorValue;
            Double ceiling = ceilingValue;

            for (Int32 i = 0; i < _NumberOfBits; i++)
            {
                Double middle = (floor + ceiling) / 2;

                if (value >= middle)
                {
                    result[i] = true;
                    floor = middle;
                }
                else
                {
                    result[i] = false;
                    ceiling = middle;
                }
            }

            return result;
        }

        private static String EncodeBase32(String binaryStringValue)
        {
            StringBuilder buffer = new StringBuilder();
            String binaryString = binaryStringValue;

            while (binaryString.Length > 0)
            {
                String currentBlock = binaryString.Substring(0, 5).PadLeft(5, '0');

                if (binaryString.Length > 5)
                {
                    binaryString = binaryString.Substring(5, binaryString.Length - 5);
                }
                else
                {
                    binaryString = String.Empty;
                }

                Int32 value = Convert.ToInt32(currentBlock, 2);
                buffer.Append(_Digits[value]);
            }

            String result = buffer.ToString();

            return result;
        }

        internal static IPoint DecodeGeoHash(String value)
        {
            StringBuilder lBuffer = new StringBuilder();

            foreach (Char c in value)
            {
                if (!_LookupTable.ContainsKey(c))
                {
                    throw new ArgumentException("Invalid character " + c);
                }

                Int32 i = _LookupTable[c] + 32;
                lBuffer.Append(Convert.ToString(i, 2).Substring(1));
            }

            BitArray lonset = new BitArray(_NumberOfBits);
            BitArray latset = new BitArray(_NumberOfBits);

            //even bits
            int j = 0;

            for (int i = 0; i < _NumberOfBits * 2; i += 2)
            {
                Boolean isSet = false;

                if (i < lBuffer.Length)
                {
                    isSet = lBuffer[i] == '1';
                }

                lonset[j] = isSet;
                j++;
            }

            //odd bits
            j = 0;

            for (int i = 1; i < _NumberOfBits * 2; i += 2)
            {
                Boolean isSet = false;

                if (i < lBuffer.Length)
                {
                    isSet = lBuffer[i] == '1';
                }

                latset[j] = isSet;
                j++;
            }

            double longitude = GeoHashDecode(lonset, -180, 180);
            double latitude = GeoHashDecode(latset, -90, 90);

            IPoint pointResult = new PointClass() { X = longitude, Y = latitude };

            return pointResult;
        }

        internal static String EncodeGeoHash(IPoint data, Int32 accuracy)
        {
            BitArray latitudeBits = GeoHashEncode(data.Y, -90, 90);
            BitArray longitudeBits = GeoHashEncode(data.X, -180, 180);

            StringBuilder buffer = new StringBuilder();

            for (Int32 i = 0; i < _NumberOfBits; i++)
            {
                buffer.Append((longitudeBits[i]) ? '1' : '0');
                buffer.Append((latitudeBits[i]) ? '1' : '0');
            }

            String binaryValue = buffer.ToString();
            String result = EncodeBase32(binaryValue);

            result = result.Substring(0, accuracy);
            return result;
        }
    }
}