// (c) Copyright Esri, 2010 - 2016
// This source is subject to the Apache 2.0 License.
// Please see http://www.apache.org/licenses/LICENSE-2.0.html for details.
// All other rights reserved.

using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Resources;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geoprocessing;
using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.Display;
using System.Xml;
using ESRI.ArcGIS.OSM.OSMClassExtension;
using System.Globalization;
using ESRI.ArcGIS.DataSourcesFile;

namespace ESRI.ArcGIS.OSM.GeoProcessing
{
    [Guid("3379c124-ea23-49f7-905a-03c3694a6fdb")]
    [ClassInterface(ClassInterfaceType.None)]
    [ProgId("ESRI.ArcGIS.OSM.GeoProcessing.OSMGPExport2OSM")]
    public class OSMGPExport2OSM : ESRI.ArcGIS.Geoprocessing.IGPFunction2
    {

        string m_DisplayName = String.Empty;

        int in_featureDatasetParameterNumber, out_osmFileLocationParameterNumber;
        ResourceManager resourceManager = null;
        OSMGPFactory osmGPFactory = null;
        OSMUtility _osmUtility = null;

        ISpatialReference m_wgs84 = null;

        public OSMGPExport2OSM()
        {
            resourceManager = new ResourceManager("ESRI.ArcGIS.OSM.GeoProcessing.OSMGPToolsStrings", this.GetType().Assembly);

            osmGPFactory = new OSMGPFactory();
            _osmUtility = new OSMUtility();

            ISpatialReferenceFactory spatialReferenceFactory = new SpatialReferenceEnvironmentClass() as ISpatialReferenceFactory;
            m_wgs84 = spatialReferenceFactory.CreateGeographicCoordinateSystem((int)esriSRGeoCSType.esriSRGeoCS_WGS1984) as ISpatialReference;
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
                    m_DisplayName = osmGPFactory.GetFunctionName(OSMGPFactory.m_Export2OSMName).DisplayName;
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

                IGPParameter inputFeatureDatasetParameter = paramvalues.get_Element(in_featureDatasetParameterNumber) as IGPParameter;
                IGPValue inputFeatureDatasetGPValue = execute_Utilities.UnpackGPValue(inputFeatureDatasetParameter);
                IGPValue outputOSMFileGPValue = execute_Utilities.UnpackGPValue(paramvalues.get_Element(out_osmFileLocationParameterNumber));

                // get the name of the feature dataset
                int fdDemlimiterPosition = inputFeatureDatasetGPValue.GetAsText().LastIndexOf("\\");

                string nameOfFeatureDataset = inputFeatureDatasetGPValue.GetAsText().Substring(fdDemlimiterPosition + 1);


                XmlWriterSettings settings = new XmlWriterSettings();
                settings.Indent = true;

                System.Xml.XmlWriter xmlWriter = null;

                try
                {
                    xmlWriter = XmlWriter.Create(outputOSMFileGPValue.GetAsText(), settings);
                }
                catch (Exception ex)
                {
                    message.AddError(120021, ex.Message);
                    return;
                }

                xmlWriter.WriteStartDocument();
                xmlWriter.WriteStartElement("osm"); // start the osm root node
                xmlWriter.WriteAttributeString("version", "0.6"); // add the version attribute
                xmlWriter.WriteAttributeString("generator", "ArcGIS Editor for OpenStreetMap"); // add the generator attribute

                // write all the nodes
                // use a feature search cursor to loop through all the known points and write them out as osm node

                IFeatureClassContainer osmFeatureClasses = execute_Utilities.OpenDataset(inputFeatureDatasetGPValue) as IFeatureClassContainer;

                if (osmFeatureClasses == null)
                {
                    message.AddError(120022, string.Format(resourceManager.GetString("GPTools_NullPointerParameterType"), inputFeatureDatasetParameter.Name));
                    return;
                }

                IFeatureClass osmPointFeatureClass = osmFeatureClasses.get_ClassByName(nameOfFeatureDataset + "_osm_pt");

                if (osmPointFeatureClass == null)
                {
                    message.AddError(120023, string.Format(resourceManager.GetString("GPTools_OSMGPExport2OSM_no_pointfeatureclass"), nameOfFeatureDataset + "_osm_pt"));
                    return;
                }

                // check the extension of the point feature class to determine its version
                int internalOSMExtensionVersion = osmPointFeatureClass.OSMExtensionVersion();

                IFeatureCursor searchCursor = null;

                System.Xml.Serialization.XmlSerializerNamespaces xmlnsEmpty = new System.Xml.Serialization.XmlSerializerNamespaces();
                xmlnsEmpty.Add("", "");

                message.AddMessage(resourceManager.GetString("GPTools_OSMGPExport2OSM_exporting_pts_msg"));
                int pointCounter = 0;

                string nodesExportedMessage = String.Empty;

                // collect the indices for the point feature class once
                int pointOSMIDFieldIndex = osmPointFeatureClass.Fields.FindField("OSMID");
                int pointChangesetFieldIndex = osmPointFeatureClass.Fields.FindField("osmchangeset");
                int pointVersionFieldIndex = osmPointFeatureClass.Fields.FindField("osmversion");
                int pointUIDFieldIndex = osmPointFeatureClass.Fields.FindField("osmuid");
                int pointUserFieldIndex = osmPointFeatureClass.Fields.FindField("osmuser");
                int pointTimeStampFieldIndex = osmPointFeatureClass.Fields.FindField("osmtimestamp");
                int pointVisibleFieldIndex = osmPointFeatureClass.Fields.FindField("osmvisible");
                int pointTagsFieldIndex = osmPointFeatureClass.Fields.FindField("osmTags");

                using (ComReleaser comReleaser = new ComReleaser())
                {
                    searchCursor = osmPointFeatureClass.Search(null, false);
                    comReleaser.ManageLifetime(searchCursor);

                    System.Xml.Serialization.XmlSerializer pointSerializer = new System.Xml.Serialization.XmlSerializer(typeof(node));

                    IFeature currentFeature = searchCursor.NextFeature();

                    IWorkspace pointWorkspace = ((IDataset)osmPointFeatureClass).Workspace;

                    while (currentFeature != null)
                    {
                        if (TrackCancel.Continue() == true)
                        {
                            // convert the found point feature into a osm node representation to store into the OSM XML file
                            node osmNode = ConvertPointFeatureToOSMNode(currentFeature, pointWorkspace, pointTagsFieldIndex, pointOSMIDFieldIndex, pointChangesetFieldIndex, pointVersionFieldIndex, pointUIDFieldIndex, pointUserFieldIndex, pointTimeStampFieldIndex, pointVisibleFieldIndex, internalOSMExtensionVersion);

                            pointSerializer.Serialize(xmlWriter, osmNode, xmlnsEmpty);

                            // increase the point counter to later status report
                            pointCounter++;

                            currentFeature = searchCursor.NextFeature();
                        }
                        else
                        {
                            // properly close the document
                            xmlWriter.WriteEndElement(); // closing the osm root element
                            xmlWriter.WriteEndDocument(); // finishing the document

                            xmlWriter.Close(); // closing the document

                            // report the number of elements loader so far
                            nodesExportedMessage = String.Format(resourceManager.GetString("GPTools_OSMGPExport2OSM_pts_exported_msg"), pointCounter);
                            message.AddMessage(nodesExportedMessage);

                            return;
                        }
                    }
                }

                nodesExportedMessage = String.Format(resourceManager.GetString("GPTools_OSMGPExport2OSM_pts_exported_msg"), pointCounter);
                message.AddMessage(nodesExportedMessage);

                // next loop through the line and polygon feature classes to export those features as ways
                // in case we encounter a multi-part geometry, store it in a relation collection that will be serialized when exporting the relations table
                IFeatureClass osmLineFeatureClass = osmFeatureClasses.get_ClassByName(nameOfFeatureDataset + "_osm_ln");

                if (osmLineFeatureClass == null)
                {
                    message.AddError(120023, string.Format(resourceManager.GetString("GPTools_OSMGPExport2OSM_no_linefeatureclass"), nameOfFeatureDataset + "_osm_ln"));
                    return;
                }

                message.AddMessage(resourceManager.GetString("GPTools_OSMGPExport2OSM_exporting_ways_msg"));

                // as we are looping through the line and polygon feature classes let's collect the multi-part features separately 
                // as they are considered relations in the OSM world
                List<relation> multiPartElements = new List<relation>();

                System.Xml.Serialization.XmlSerializer waySerializer = new System.Xml.Serialization.XmlSerializer(typeof(way));
                int lineCounter = 0;
                int relationCounter = 0;
                string waysExportedMessage = String.Empty;

                using (ComReleaser comReleaser = new ComReleaser())
                {
                    searchCursor = osmLineFeatureClass.Search(null, false);
                    comReleaser.ManageLifetime(searchCursor);

                    IFeature currentFeature = searchCursor.NextFeature();

                    // collect the indices for the point feature class once
                    int lineOSMIDFieldIndex = osmLineFeatureClass.Fields.FindField("OSMID");
                    int lineChangesetFieldIndex = osmLineFeatureClass.Fields.FindField("osmchangeset");
                    int lineVersionFieldIndex = osmLineFeatureClass.Fields.FindField("osmversion");
                    int lineUIDFieldIndex = osmLineFeatureClass.Fields.FindField("osmuid");
                    int lineUserFieldIndex = osmLineFeatureClass.Fields.FindField("osmuser");
                    int lineTimeStampFieldIndex = osmLineFeatureClass.Fields.FindField("osmtimestamp");
                    int lineVisibleFieldIndex = osmLineFeatureClass.Fields.FindField("osmvisible");
                    int lineTagsFieldIndex = osmLineFeatureClass.Fields.FindField("osmTags");
                    int lineMembersFieldIndex = osmLineFeatureClass.Fields.FindField("osmMembers");

                    IWorkspace lineWorkspace = ((IDataset)osmLineFeatureClass).Workspace;

                    while (currentFeature != null)
                    {
                        if (TrackCancel.Continue() == false)
                        {
                            // properly close the document
                            xmlWriter.WriteEndElement(); // closing the osm root element
                            xmlWriter.WriteEndDocument(); // finishing the document

                            xmlWriter.Close(); // closing the document

                            // report the number of elements loaded so far
                            waysExportedMessage = String.Format(resourceManager.GetString("GPTools_OSMGPExport2OSM_ways_exported_msg"), lineCounter);
                            message.AddMessage(waysExportedMessage);

                            return;
                        }

                        //test if the feature geometry has multiple parts
                        IGeometryCollection geometryCollection = currentFeature.Shape as IGeometryCollection;

                        if (geometryCollection != null)
                        {
                            if (geometryCollection.GeometryCount == 1)
                            {
                                // convert the found polyline feature into a osm way representation to store into the OSM XML file
                                way osmWay = ConvertFeatureToOSMWay(currentFeature, lineWorkspace, osmPointFeatureClass, pointOSMIDFieldIndex, lineTagsFieldIndex, lineOSMIDFieldIndex, lineChangesetFieldIndex, lineVersionFieldIndex, lineUIDFieldIndex, lineUserFieldIndex, lineTimeStampFieldIndex, lineVisibleFieldIndex, internalOSMExtensionVersion);
                                waySerializer.Serialize(xmlWriter, osmWay, xmlnsEmpty);

                                // increase the line counter for later status report
                                lineCounter++;
                            }
                            else
                            {
                                relation osmRelation = ConvertRowToOSMRelation((IRow)currentFeature, lineWorkspace, lineTagsFieldIndex, lineOSMIDFieldIndex, lineChangesetFieldIndex, lineVersionFieldIndex, lineUIDFieldIndex, lineUserFieldIndex, lineTimeStampFieldIndex, lineVisibleFieldIndex, lineMembersFieldIndex, internalOSMExtensionVersion);
                                multiPartElements.Add(osmRelation);

                                // increase the line counter for later status report
                                relationCounter++;
                            }
                        }

                        currentFeature = searchCursor.NextFeature();
                    }
                }


                IFeatureClass osmPolygonFeatureClass = osmFeatureClasses.get_ClassByName(nameOfFeatureDataset + "_osm_ply");
                IFeatureWorkspace commonWorkspace = ((IDataset)osmPolygonFeatureClass).Workspace as IFeatureWorkspace;

                if (osmPolygonFeatureClass == null)
                {
                    message.AddError(120024, string.Format(resourceManager.GetString("GPTools_OSMGPExport2OSM_no_polygonfeatureclass"), nameOfFeatureDataset + "_osm_ply"));
                    return;
                }

                using (ComReleaser comReleaser = new ComReleaser())
                {
                    searchCursor = osmPolygonFeatureClass.Search(null, false);
                    comReleaser.ManageLifetime(searchCursor);

                    IFeature currentFeature = searchCursor.NextFeature();

                    // collect the indices for the point feature class once
                    int polygonOSMIDFieldIndex = osmPolygonFeatureClass.Fields.FindField("OSMID");
                    int polygonChangesetFieldIndex = osmPolygonFeatureClass.Fields.FindField("osmchangeset");
                    int polygonVersionFieldIndex = osmPolygonFeatureClass.Fields.FindField("osmversion");
                    int polygonUIDFieldIndex = osmPolygonFeatureClass.Fields.FindField("osmuid");
                    int polygonUserFieldIndex = osmPolygonFeatureClass.Fields.FindField("osmuser");
                    int polygonTimeStampFieldIndex = osmPolygonFeatureClass.Fields.FindField("osmtimestamp");
                    int polygonVisibleFieldIndex = osmPolygonFeatureClass.Fields.FindField("osmvisible");
                    int polygonTagsFieldIndex = osmPolygonFeatureClass.Fields.FindField("osmTags");
                    int polygonMembersFieldIndex = osmPolygonFeatureClass.Fields.FindField("osmMembers");

                    IWorkspace polygonWorkspace = ((IDataset)osmPolygonFeatureClass).Workspace;

                    while (currentFeature != null)
                    {
                        if (TrackCancel.Continue() == false)
                        {
                            // properly close the document
                            xmlWriter.WriteEndElement(); // closing the osm root element
                            xmlWriter.WriteEndDocument(); // finishing the document

                            xmlWriter.Close(); // closing the document

                            // report the number of elements loaded so far
                            waysExportedMessage = String.Format(resourceManager.GetString("GPTools_OSMGPExport2OSM_ways_exported_msg"), lineCounter);
                            message.AddMessage(waysExportedMessage);

                            message.AddAbort(resourceManager.GetString("GPTools_toolabort"));
                            return;
                        }

                        //test if the feature geometry has multiple parts
                        IGeometryCollection geometryCollection = currentFeature.Shape as IGeometryCollection;

                        if (geometryCollection != null)
                        {
                            if (geometryCollection.GeometryCount == 1)
                            {
                                // convert the found polyline feature into a osm way representation to store into the OSM XML file
                                way osmWay = ConvertFeatureToOSMWay(currentFeature, polygonWorkspace, osmPointFeatureClass, pointOSMIDFieldIndex, polygonTagsFieldIndex, polygonOSMIDFieldIndex, polygonChangesetFieldIndex, polygonVersionFieldIndex, polygonUIDFieldIndex, polygonUserFieldIndex, polygonTimeStampFieldIndex, polygonVisibleFieldIndex, internalOSMExtensionVersion);
                                waySerializer.Serialize(xmlWriter, osmWay, xmlnsEmpty);

                                // increase the line counter for later status report
                                lineCounter++;
                            }
                            else
                            {
                                relation osmRelation = ConvertRowToOSMRelation((IRow)currentFeature, polygonWorkspace, polygonTagsFieldIndex, polygonOSMIDFieldIndex, polygonChangesetFieldIndex, polygonVersionFieldIndex, polygonUIDFieldIndex, polygonUserFieldIndex, polygonTimeStampFieldIndex, polygonVisibleFieldIndex, polygonMembersFieldIndex, internalOSMExtensionVersion);
                                multiPartElements.Add(osmRelation);

                                // increase the line counter for later status report
                                relationCounter++;
                            }
                        }

                        currentFeature = searchCursor.NextFeature();
                    }
                }

                waysExportedMessage = String.Format(resourceManager.GetString("GPTools_OSMGPExport2OSM_ways_exported_msg"), lineCounter);
                message.AddMessage(waysExportedMessage);


                // now let's go through the relation table 
                message.AddMessage(resourceManager.GetString("GPTools_OSMGPExport2OSM_exporting_relations_msg"));
                ITable relationTable = commonWorkspace.OpenTable(nameOfFeatureDataset + "_osm_relation");

                if (relationTable == null)
                {
                    message.AddError(120025, String.Format(resourceManager.GetString("GPTools_OSMGPExport2OSM_no_relationTable"), nameOfFeatureDataset + "_osm_relation"));
                    return;
                }


                System.Xml.Serialization.XmlSerializer relationSerializer = new System.Xml.Serialization.XmlSerializer(typeof(relation));
                string relationsExportedMessage = String.Empty;

                using (ComReleaser comReleaser = new ComReleaser())
                {
                    ICursor rowCursor = relationTable.Search(null, false);
                    comReleaser.ManageLifetime(rowCursor);

                    IRow currentRow = rowCursor.NextRow();

                    // collect the indices for the relation table once
                    int relationOSMIDFieldIndex = relationTable.Fields.FindField("OSMID");
                    int relationChangesetFieldIndex = relationTable.Fields.FindField("osmchangeset");
                    int relationVersionFieldIndex = relationTable.Fields.FindField("osmversion");
                    int relationUIDFieldIndex = relationTable.Fields.FindField("osmuid");
                    int relationUserFieldIndex = relationTable.Fields.FindField("osmuser");
                    int relationTimeStampFieldIndex = relationTable.Fields.FindField("osmtimestamp");
                    int relationVisibleFieldIndex = relationTable.Fields.FindField("osmvisible");
                    int relationTagsFieldIndex = relationTable.Fields.FindField("osmTags");
                    int relationMembersFieldIndex = relationTable.Fields.FindField("osmMembers");

                    IWorkspace polygonWorkspace = ((IDataset)osmPolygonFeatureClass).Workspace;


                    while (currentRow != null)
                    {
                        if (TrackCancel.Continue() == false)
                        {
                            // properly close the document
                            xmlWriter.WriteEndElement(); // closing the osm root element
                            xmlWriter.WriteEndDocument(); // finishing the document

                            xmlWriter.Close(); // closing the document

                            // report the number of elements loaded so far
                            relationsExportedMessage = String.Format(resourceManager.GetString("GPTools_OSMGPExport2OSM_relations_exported_msg"), relationCounter);
                            message.AddMessage(relationsExportedMessage);

                            message.AddAbort(resourceManager.GetString("GPTools_toolabort"));
                            return;
                        }

                        relation osmRelation = ConvertRowToOSMRelation(currentRow, (IWorkspace)commonWorkspace, relationTagsFieldIndex, relationOSMIDFieldIndex, relationChangesetFieldIndex, relationVersionFieldIndex, relationUIDFieldIndex, relationUserFieldIndex, relationTimeStampFieldIndex, relationVisibleFieldIndex, relationMembersFieldIndex, internalOSMExtensionVersion);
                        relationSerializer.Serialize(xmlWriter, osmRelation, xmlnsEmpty);

                        // increase the line counter for later status report
                        relationCounter++;

                        currentRow = rowCursor.NextRow();
                    }
                }

                // lastly let's serialize the collected multipart-geometries back into relation elements
                foreach (relation currentRelation in multiPartElements)
                {
                    if (TrackCancel.Continue() == false)
                    {
                        // properly close the document
                        xmlWriter.WriteEndElement(); // closing the osm root element
                        xmlWriter.WriteEndDocument(); // finishing the document

                        xmlWriter.Close(); // closing the document

                        // report the number of elements loaded so far
                        relationsExportedMessage = String.Format(resourceManager.GetString("GPTools_OSMGPExport2OSM_relations_exported_msg"), relationCounter);
                        message.AddMessage(relationsExportedMessage);

                        return;
                    }

                    relationSerializer.Serialize(xmlWriter, currentRelation, xmlnsEmpty);
                    relationCounter++;
                }

                relationsExportedMessage = String.Format(resourceManager.GetString("GPTools_OSMGPExport2OSM_relations_exported_msg"), relationCounter);
                message.AddMessage(relationsExportedMessage);


                xmlWriter.WriteEndElement(); // closing the osm root element
                xmlWriter.WriteEndDocument(); // finishing the document

                xmlWriter.Close(); // closing the document
            }
            catch (Exception ex)
            {
                message.AddError(120026, ex.Message);
            }
        }

        public ESRI.ArcGIS.esriSystem.IName FullName
        {
            get
            {
                IName fullName = null;

                if (osmGPFactory != null)
                {
                    fullName = osmGPFactory.GetFunctionName(OSMGPFactory.m_Export2OSMName) as IName;
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
                return String.Empty;
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
                string metadafile = "osmgpexport2osm.xml";

                try
                {
                    string[] languageid = System.Threading.Thread.CurrentThread.CurrentUICulture.Name.Split("-".ToCharArray());

                    string ArcGISInstallationLocation = OSMGPFactory.GetArcGIS10InstallLocation();
                    string localizedMetaDataFileShort = ArcGISInstallationLocation + System.IO.Path.DirectorySeparatorChar.ToString() + "help" + System.IO.Path.DirectorySeparatorChar.ToString() + "gp" + System.IO.Path.DirectorySeparatorChar.ToString() + "osmgpfileloader_" + languageid[0] + ".xml";
                    string localizedMetaDataFileLong = ArcGISInstallationLocation + System.IO.Path.DirectorySeparatorChar.ToString() + "help" + System.IO.Path.DirectorySeparatorChar.ToString() + "gp" + System.IO.Path.DirectorySeparatorChar.ToString() + "osmgpfileloader_" + System.Threading.Thread.CurrentThread.CurrentUICulture.Name + ".xml";

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
                return OSMGPFactory.m_Export2OSMName;
            }
        }

        public ESRI.ArcGIS.esriSystem.IArray ParameterInfo
        {
            get
            {
                //
                IArray parameterArray = new ArrayClass();

                // input feature dataset (required)
                IGPParameterEdit3 inputOSMDataset = new GPParameterClass() as IGPParameterEdit3;
                inputOSMDataset.DataType = new DEFeatureDatasetTypeClass();
                inputOSMDataset.Direction = esriGPParameterDirection.esriGPParameterDirectionInput;
                inputOSMDataset.DisplayName = resourceManager.GetString("GPTools_OSMGPExport2OSM_desc_inputDataset_desc");
                inputOSMDataset.Name = "in_osmFeatureDataset";
                inputOSMDataset.ParameterType = esriGPParameterType.esriGPParameterTypeRequired;

                in_featureDatasetParameterNumber = 0;
                parameterArray.Add(inputOSMDataset);

                // output osm XML file
                IGPParameterEdit3 outputOSMFile = new GPParameterClass() as IGPParameterEdit3;
                outputOSMFile.DataType = new DEFileTypeClass();
                outputOSMFile.Direction = esriGPParameterDirection.esriGPParameterDirectionOutput;
                outputOSMFile.DisplayName = resourceManager.GetString("GPTools_OSMGPExport2OSM_desc_outputXMLFile_desc");
                outputOSMFile.Name = "out_osmXMLFile";
                outputOSMFile.ParameterType = esriGPParameterType.esriGPParameterTypeRequired;

                IGPFileDomain osmFileDomainFilter = new GPFileDomainClass();
                osmFileDomainFilter.AddType("osm");

                outputOSMFile.Domain = osmFileDomainFilter as IGPDomain;

                out_osmFileLocationParameterNumber = 1;
                parameterArray.Add(outputOSMFile);

                return parameterArray;

            }
        }

        public void UpdateMessages(ESRI.ArcGIS.esriSystem.IArray paramvalues, ESRI.ArcGIS.Geoprocessing.IGPEnvironmentManager pEnvMgr, ESRI.ArcGIS.Geodatabase.IGPMessages Messages)
        {
            IGPUtilities3 execute_Utilities = new GPUtilitiesClass();
            IGPValue inputFeatureDatasetGPValue = execute_Utilities.UnpackGPValue(paramvalues.get_Element(in_featureDatasetParameterNumber));

            // get the name of the feature dataset
            int fdDemlimiterPosition = inputFeatureDatasetGPValue.GetAsText().LastIndexOf("\\");

            if (fdDemlimiterPosition == -1)
            {
                Messages.ReplaceError(in_featureDatasetParameterNumber, -33, resourceManager.GetString("GPTools_OSMGPExport2OSM_invalid_featuredataset"));
            }
        }

        public void UpdateParameters(ESRI.ArcGIS.esriSystem.IArray paramvalues, ESRI.ArcGIS.Geoprocessing.IGPEnvironmentManager pEnvMgr)
        {
        }

        public ESRI.ArcGIS.Geodatabase.IGPMessages Validate(ESRI.ArcGIS.esriSystem.IArray paramvalues, bool updateValues, ESRI.ArcGIS.Geoprocessing.IGPEnvironmentManager envMgr)
        {
            return default(ESRI.ArcGIS.Geodatabase.IGPMessages);
        }
        #endregion

        private String ConvertPointFeatureToXmlString(IFeature currentFeature, IWorkspace featureWorkspace, int tagsFieldIndex, int osmIDFieldIndex, int changesetIDFieldIndex, int osmVersionFieldIndex, int userIDFieldIndex, int userNameFieldIndex, int timeStampFieldIndex, int visibleFieldIndex, int extensionVersion)
        {
            StringBuilder xmlNodeStringBuilder = new StringBuilder(200);


            if (currentFeature == null)
                throw new ArgumentNullException("currentFeature");

            object featureValue = DBNull.Value;

            string latitudeString = String.Empty;
            string longitudeString = String.Empty;

            if (currentFeature.Shape.IsEmpty == false)
            {
                IPoint wgs84Point = currentFeature.Shape as IPoint;
                wgs84Point.Project(m_wgs84);

                NumberFormatInfo exportCultureInfo = new CultureInfo("en-US", false).NumberFormat;
                exportCultureInfo.NumberDecimalDigits = 6;

                latitudeString = wgs84Point.Y.ToString("N", exportCultureInfo);
                longitudeString = wgs84Point.X.ToString("N", exportCultureInfo);

                Marshal.ReleaseComObject(wgs84Point);
            }

            string osmIDString = String.Empty;
            if (osmIDFieldIndex != -1)
            {
                osmIDString = Convert.ToString(currentFeature.get_Value(osmIDFieldIndex));
            }

            string changeSetString = String.Empty;
            if (changesetIDFieldIndex != -1)
            {
                featureValue = currentFeature.get_Value(changesetIDFieldIndex);

                if (featureValue != DBNull.Value)
                {
                    changeSetString = Convert.ToString(currentFeature.get_Value(changesetIDFieldIndex));
                }
            }

            string osmVersionString = String.Empty;
            if (osmVersionFieldIndex != -1)
            {
                featureValue = currentFeature.get_Value(osmVersionFieldIndex);

                if (featureValue != DBNull.Value)
                {
                    osmVersionString = Convert.ToString(featureValue);
                }
            }

            string userIDString = String.Empty;

            if (userIDFieldIndex != -1)
            {
                featureValue = currentFeature.get_Value(userIDFieldIndex);

                if (featureValue != DBNull.Value)
                {
                    userIDString = Convert.ToString(featureValue);
                }
            }

            string userNameString = String.Empty;
            if (userNameFieldIndex != -1)
            {
                featureValue = currentFeature.get_Value(userNameFieldIndex);

                if (featureValue != DBNull.Value)
                {
                    userNameString = Convert.ToString(featureValue);
                }
            }

            string timeStampString = String.Empty;

            if (timeStampFieldIndex != -1)
            {
                featureValue = currentFeature.get_Value(timeStampFieldIndex);

                if (featureValue != DBNull.Value)
                {
                    timeStampString = Convert.ToDateTime(featureValue).ToUniversalTime().ToString("u");
                }
            }

            string visibilityString = bool.TrueString;
            if (visibleFieldIndex != -1)
            {
                featureValue = currentFeature.get_Value(visibleFieldIndex);

                if (featureValue != DBNull.Value)
                {
                    try
                    {
                        visibilityString = Convert.ToString(featureValue);
                    }
                    catch { }
                }
            }


            tag[] tags = null;

            if (tagsFieldIndex > -1)
            {
                tags = _osmUtility.retrieveOSMTags((IRow)currentFeature, tagsFieldIndex, featureWorkspace);
            }

            // no tags only the node itself
            if (tags != null || tags.Length > 0)
            {
                xmlNodeStringBuilder.Append("<node id=\"");
                xmlNodeStringBuilder.Append(osmIDString);
                xmlNodeStringBuilder.Append("\" version=\"");
                xmlNodeStringBuilder.Append(osmVersionString);
                xmlNodeStringBuilder.Append("\" timestamp=\"");
                xmlNodeStringBuilder.Append(timeStampString);
                xmlNodeStringBuilder.Append("\" uid=\"");
                xmlNodeStringBuilder.Append(userIDString);
                xmlNodeStringBuilder.Append("\" user=\"");
                xmlNodeStringBuilder.Append(userNameString);
                xmlNodeStringBuilder.Append("\" changeset=\"");
                xmlNodeStringBuilder.Append(changeSetString);
                xmlNodeStringBuilder.Append("\" lat=\"");
                xmlNodeStringBuilder.Append(latitudeString);
                xmlNodeStringBuilder.Append("\" lon=\"");
                xmlNodeStringBuilder.Append(longitudeString);
                xmlNodeStringBuilder.AppendLine("\" />");

            }
            else
            {
                xmlNodeStringBuilder.Append("<node id=\"");
                xmlNodeStringBuilder.Append(osmIDString);
                xmlNodeStringBuilder.Append("\" version=\"");
                xmlNodeStringBuilder.Append(osmVersionString);
                xmlNodeStringBuilder.Append("\" timestamp=\"");
                xmlNodeStringBuilder.Append(timeStampString);
                xmlNodeStringBuilder.Append("\" uid=\"");
                xmlNodeStringBuilder.Append(userIDString);
                xmlNodeStringBuilder.Append("\" user=\"");
                xmlNodeStringBuilder.Append(userNameString);
                xmlNodeStringBuilder.Append("\" changeset=\"");
                xmlNodeStringBuilder.Append(changeSetString);
                xmlNodeStringBuilder.Append("\" lat=\"");
                xmlNodeStringBuilder.Append(latitudeString);
                xmlNodeStringBuilder.Append("\" lon=\"");
                xmlNodeStringBuilder.Append(longitudeString);
                xmlNodeStringBuilder.AppendLine("\" >");

                foreach (tag item in tags)
                {
                    xmlNodeStringBuilder.Append("<tag k=\"");
                    xmlNodeStringBuilder.Append(item.k);
                    xmlNodeStringBuilder.Append("\" v=\"");
                    xmlNodeStringBuilder.Append(item.v);
                    xmlNodeStringBuilder.AppendLine("\" />");

                }

                xmlNodeStringBuilder.AppendLine("</node>");
            }


            return xmlNodeStringBuilder.ToString();
        }

        private node ConvertPointFeatureToOSMNode(IFeature currentFeature, IWorkspace featureWorkspace, int tagsFieldIndex, int osmIDFieldIndex, int changesetIDFieldIndex, int osmVersionFieldIndex, int userIDFieldIndex, int userNameFieldIndex, int timeStampFieldIndex, int visibleFieldIndex, int extensionVersion)
        {

            if (currentFeature == null)
                throw new ArgumentNullException("currentFeature");

            node osmNode = new node();
            object featureValue = DBNull.Value;

            if (currentFeature.Shape.IsEmpty == false)
            {
                IPoint wgs84Point = currentFeature.Shape as IPoint;
                wgs84Point.Project(m_wgs84);

                NumberFormatInfo exportCultureInfo = new CultureInfo("en-US", false).NumberFormat;
                exportCultureInfo.NumberDecimalDigits = 6;

                osmNode.lat = wgs84Point.Y.ToString("N", exportCultureInfo);
                osmNode.lon = wgs84Point.X.ToString("N", exportCultureInfo);

                Marshal.ReleaseComObject(wgs84Point);
            }

            if (osmIDFieldIndex != -1)
            {
                osmNode.id = Convert.ToString(currentFeature.get_Value(osmIDFieldIndex));
            }

            if (changesetIDFieldIndex != -1)
            {
                featureValue = currentFeature.get_Value(changesetIDFieldIndex);

                if (featureValue != DBNull.Value)
                {
                    osmNode.changeset = Convert.ToString(currentFeature.get_Value(changesetIDFieldIndex));
                }
            }

            if (osmVersionFieldIndex != -1)
            {
                featureValue = currentFeature.get_Value(osmVersionFieldIndex);

                if (featureValue != DBNull.Value)
                {
                    osmNode.version = Convert.ToString(featureValue);
                }
            }

            if (userIDFieldIndex != -1)
            {
                featureValue = currentFeature.get_Value(userIDFieldIndex);

                if (featureValue != DBNull.Value)
                {
                    osmNode.uid = Convert.ToString(featureValue);
                }
            }

            if (userNameFieldIndex != -1)
            {
                featureValue = currentFeature.get_Value(userNameFieldIndex);

                if (featureValue != DBNull.Value)
                {
                    osmNode.user = Convert.ToString(featureValue);
                }
            }

            if (timeStampFieldIndex != -1)
            {
                featureValue = currentFeature.get_Value(timeStampFieldIndex);

                if (featureValue != DBNull.Value)
                {
                    osmNode.timestamp = Convert.ToDateTime(featureValue).ToUniversalTime().ToString("u");
                }
            }

            if (visibleFieldIndex != -1)
            {
                featureValue = currentFeature.get_Value(visibleFieldIndex);

                if (featureValue != DBNull.Value)
                {
                    try
                    {
                        osmNode.visible = (nodeVisible)Enum.Parse(typeof(nodeVisible), Convert.ToString(featureValue));
                    }
                    catch
                    {
                        osmNode.visible = nodeVisible.@true;
                    }
                }
            }

            if (tagsFieldIndex > -1)
            {
                tag[] tags = null;
                tags = _osmUtility.retrieveOSMTags((IRow)currentFeature, tagsFieldIndex, featureWorkspace);

                if (tags.Length != 0)
                {
                    osmNode.tag = tags;
                }
            }

            return osmNode;
        }

        private way ConvertFeatureToOSMWay(IFeature currentFeature, IWorkspace featureWorkspace, IFeatureClass pointFeatureClass, int osmIDPointFieldIndex, int tagsFieldIndex, int osmIDFieldIndex, int changesetIDFieldIndex, int osmVersionFieldIndex, int userIDFieldIndex, int userNameFieldIndex, int timeStampFieldIndex, int visibleFieldIndex, int extensionVersion)
        {

            if (currentFeature == null)
                throw new ArgumentNullException("currentFeature");

            way osmWay = new way();
            object featureValue = DBNull.Value;

            List<nd> vertexIDs = new List<nd>();

            if (currentFeature.Shape.IsEmpty == false)
            {
                IPointCollection pointCollection = currentFeature.Shape as IPointCollection;

                if (currentFeature.Shape.GeometryType == esriGeometryType.esriGeometryPolygon)
                {
                    for (int pointIndex = 0; pointIndex < pointCollection.PointCount - 1; pointIndex++)
                    {
                        nd vertex = new nd();
                        vertex.@ref = OSMToolHelper.retrieveNodeID(pointFeatureClass, osmIDPointFieldIndex, extensionVersion, pointCollection.get_Point(pointIndex));
                        vertexIDs.Add(vertex);
                    }

                    // the last node is the first one again even though it doesn't have an internal ID
                    nd lastVertex = new nd();
                    lastVertex.@ref = OSMToolHelper.retrieveNodeID(pointFeatureClass, osmIDPointFieldIndex, extensionVersion, pointCollection.get_Point(0));
                    vertexIDs.Add(lastVertex);

                }
                else
                {
                    for (int pointIndex = 0; pointIndex < pointCollection.PointCount; pointIndex++)
                    {
                        nd vertex = new nd();
                        vertex.@ref = OSMToolHelper.retrieveNodeID(pointFeatureClass, osmIDPointFieldIndex, extensionVersion, pointCollection.get_Point(pointIndex));
                        vertexIDs.Add(vertex);
                    }
                }

                osmWay.nd = vertexIDs.ToArray();
            }

            if (osmIDFieldIndex != -1)
            {
                osmWay.id = Convert.ToString(currentFeature.get_Value(osmIDFieldIndex));
            }

            if (changesetIDFieldIndex != -1)
            {
                featureValue = currentFeature.get_Value(changesetIDFieldIndex);

                if (featureValue != DBNull.Value)
                {
                    osmWay.changeset = Convert.ToString(currentFeature.get_Value(changesetIDFieldIndex));
                }
            }

            if (osmVersionFieldIndex != -1)
            {
                featureValue = currentFeature.get_Value(osmVersionFieldIndex);

                if (featureValue != DBNull.Value)
                {
                    osmWay.version = Convert.ToString(featureValue);
                }
            }

            if (userIDFieldIndex != -1)
            {
                featureValue = currentFeature.get_Value(userIDFieldIndex);

                if (featureValue != DBNull.Value)
                {
                    osmWay.uid = Convert.ToString(featureValue);
                }
            }

            if (userNameFieldIndex != -1)
            {
                featureValue = currentFeature.get_Value(userNameFieldIndex);

                if (featureValue != DBNull.Value)
                {
                    osmWay.user = Convert.ToString(featureValue);
                }
            }

            if (timeStampFieldIndex != -1)
            {
                featureValue = currentFeature.get_Value(timeStampFieldIndex);

                if (featureValue != DBNull.Value)
                {
                    osmWay.timestamp = Convert.ToDateTime(featureValue).ToUniversalTime().ToString("u");
                }
            }

            if (visibleFieldIndex != -1)
            {
                featureValue = currentFeature.get_Value(visibleFieldIndex);

                if (featureValue != DBNull.Value)
                {
                    try
                    {
                        osmWay.visible = (wayVisible)Enum.Parse(typeof(wayVisible), Convert.ToString(featureValue));
                    }
                    catch
                    {
                        osmWay.visible = wayVisible.@true;
                    }
                }
            }

            if (tagsFieldIndex > -1)
            {
                tag[] tags = null;
                tags = _osmUtility.retrieveOSMTags((IRow)currentFeature, tagsFieldIndex, featureWorkspace);

                if (tags.Length != 0)
                {
                    osmWay.tag = tags;
                }
            }

            return osmWay;
        }

        private relation ConvertRowToOSMRelation(IRow currentRow, IWorkspace featureWorkspace, int tagsFieldIndex, int osmIDFieldIndex, int changesetIDFieldIndex, int osmVersionFieldIndex, int userIDFieldIndex, int userNameFieldIndex, int timeStampFieldIndex, int visibleFieldIndex, int membersFieldIndex, int extensionVersion)
        {

            if (currentRow == null)
                throw new ArgumentNullException("currentRow");

            relation osmRelation = new relation();
            object featureValue = DBNull.Value;
            List<object> relationItems = new List<object>();

            if (membersFieldIndex != -1)
            {
                member[] members = _osmUtility.retrieveMembers(currentRow, membersFieldIndex);
                relationItems.AddRange(members);
            }

            if (osmIDFieldIndex != -1)
            {
                osmRelation.id = Convert.ToString(currentRow.get_Value(osmIDFieldIndex));
            }

            if (changesetIDFieldIndex != -1)
            {
                featureValue = currentRow.get_Value(changesetIDFieldIndex);

                if (featureValue != DBNull.Value)
                {
                    osmRelation.changeset = Convert.ToString(currentRow.get_Value(changesetIDFieldIndex));
                }
            }

            if (osmVersionFieldIndex != -1)
            {
                featureValue = currentRow.get_Value(osmVersionFieldIndex);

                if (featureValue != DBNull.Value)
                {
                    osmRelation.version = Convert.ToString(featureValue);
                }
            }

            if (userIDFieldIndex != -1)
            {
                featureValue = currentRow.get_Value(userIDFieldIndex);

                if (featureValue != DBNull.Value)
                {
                    osmRelation.uid = Convert.ToString(featureValue);
                }
            }

            if (userNameFieldIndex != -1)
            {
                featureValue = currentRow.get_Value(userNameFieldIndex);

                if (featureValue != DBNull.Value)
                {
                    osmRelation.user = Convert.ToString(featureValue);
                }
            }

            if (timeStampFieldIndex != -1)
            {
                featureValue = currentRow.get_Value(timeStampFieldIndex);

                if (featureValue != DBNull.Value)
                {
                    osmRelation.timestamp = Convert.ToDateTime(featureValue).ToUniversalTime().ToString("u");
                }
            }

            if (visibleFieldIndex != -1)
            {
                featureValue = currentRow.get_Value(visibleFieldIndex);

                if (featureValue != DBNull.Value)
                {
                    osmRelation.visible = Convert.ToString(featureValue);
                }
            }

            if (tagsFieldIndex > -1)
            {
                tag[] tags = null;
                tags = _osmUtility.retrieveOSMTags((IRow)currentRow, tagsFieldIndex, featureWorkspace);

                // if the row is of type IFeature and a polygon then add the type=multipolygon tag
                if (currentRow is IFeature)
                {
                    IFeature currentFeature = currentRow as IFeature;

                    if (currentFeature.Shape.GeometryType == esriGeometryType.esriGeometryPolygon)
                    {
                        tag mpTag = new tag();
                        mpTag.k = "type";
                        mpTag.v = "multipolygon";

                        relationItems.Add(mpTag);
                    }
                }

                if (tags.Length != 0)
                {
                    relationItems.AddRange(tags);
                }
            }

            // add all items (member and tags) to the relation element
            osmRelation.Items = relationItems.ToArray();

            return osmRelation;
        }
    }
}