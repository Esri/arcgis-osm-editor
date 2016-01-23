// (c) Copyright Esri, 2010 - 2016
// This source is subject to the Apache 2.0 License.
// Please see http://www.apache.org/licenses/LICENSE-2.0.html for details.
// All other rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Resources;
using System.Net;
using System.Xml;
using System.Xml.Serialization;
using System.IO;
using System.Reflection;
using System.Globalization;
using System.Security;

using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.DataSourcesGDB;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geoprocessing;
using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.OSM.OSMClassExtension;
using ESRI.ArcGIS.Display;

namespace ESRI.ArcGIS.OSM.GeoProcessing
{
    [Guid("d3956b0d-a2b0-45de-82fe-0ea43aa86f46")]
    [ClassInterface(ClassInterfaceType.None)]
    [ProgId("OSMEditor.OSMGPUpload")]
    public class OSMGPUpload : ESRI.ArcGIS.Geoprocessing.IGPFunction2
    {
        string m_DisplayName = String.Empty;
        string m_Generator = "ArcGIS";
        int in_uploadURLNumber, in_changesTablesNumber, in_uploadCommentNumber, in_uploadFormatNumber, in_userNameNumber, in_passwordNumber;
        ResourceManager resourceManager = null;
        OSMUtility _osmUtility = null;
        OSMGPFactory osmGPFactory = null;
        ISpatialReference m_wgs84 = null;
        Dictionary<string, string> m_editorConfigurationSettings = null;

        public OSMGPUpload()
        {
            resourceManager = new ResourceManager("ESRI.ArcGIS.OSM.GeoProcessing.OSMGPToolsStrings", this.GetType().Assembly);

            osmGPFactory = new OSMGPFactory();
            _osmUtility = new OSMUtility();

            m_editorConfigurationSettings = OSMGPFactory.ReadOSMEditorSettings();
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
                    m_DisplayName = osmGPFactory.GetFunctionName(OSMGPFactory.m_UploadDataName).DisplayName;
                }

                return m_DisplayName;
            }
        }

        public string[] Logs
        {
            get
            {
                try
                {
                    string[] stemp = new string[_message.Count];
                    for (int i = 0; i < _message.Count; i++)
                    {
                        IGPMessage msg = _message.GetMessage(i);
                        stemp[i] = msg.Description;
                    }

                    return stemp;
                }
                catch { }

                return null;
            }
        }

        private ESRI.ArcGIS.Geodatabase.IGPMessages _message;
        private static System.Object uploadLock = new System.Object();

        public void Execute(ESRI.ArcGIS.esriSystem.IArray paramvalues, ESRI.ArcGIS.esriSystem.ITrackCancel TrackCancel, ESRI.ArcGIS.Geoprocessing.IGPEnvironmentManager envMgr, ESRI.ArcGIS.Geodatabase.IGPMessages message)
        {
            _message = message;
           
            // classes to carry out the basic client/server communication
            HttpWebResponse httpResponse = null;
            string changeSetID = "-1";
            IGPString baseURLGPString = new GPStringClass();
            ICursor searchCursor = null;

            IGPUtilities3 gpUtilities3 = new GPUtilitiesClass();

            if (TrackCancel == null)
            {
                TrackCancel = new CancelTrackerClass();
            }

            IGPParameter userNameParameter = paramvalues.get_Element(in_userNameNumber) as IGPParameter;
            IGPString userNameGPValue = gpUtilities3.UnpackGPValue(userNameParameter) as IGPString;

            IHttpBasicGPValue userCredentialGPValue = new HttpBasicGPValue();

            if (userNameGPValue != null)
            {
                userCredentialGPValue.UserName = userNameGPValue.Value;
            }
            else
            {
                return;
            }

            IGPParameter passwordParameter = paramvalues.get_Element(in_passwordNumber) as IGPParameter;
            IGPStringHidden passwordGPValue = gpUtilities3.UnpackGPValue(passwordParameter) as IGPStringHidden;

            if (passwordGPValue != null)
            {
                userCredentialGPValue.PassWord = passwordGPValue.Value;
            }
            else
            {
                return;
            }

            ITable revisionTable = null;
            int secondsToTimeout = 10;

            try
            {
                UpdateMessages(paramvalues, envMgr, message);

                if ((message.MaxSeverity == esriGPMessageSeverity.esriGPMessageSeverityAbort) ||
                    (message.MaxSeverity == esriGPMessageSeverity.esriGPMessageSeverityError))
                {
                    message.AddMessages(message);
                    return;
                }


                IGPParameter baseURLParameter = paramvalues.get_Element(in_uploadURLNumber) as IGPParameter;
                baseURLGPString = gpUtilities3.UnpackGPValue(baseURLParameter) as IGPString;

                IGPParameter commentParameter = paramvalues.get_Element(in_uploadCommentNumber) as IGPParameter;
                IGPString uploadCommentGPString = gpUtilities3.UnpackGPValue(commentParameter) as IGPString;


                ISpatialReferenceFactory spatialReferenceFactory = new SpatialReferenceEnvironmentClass() as ISpatialReferenceFactory;
                m_wgs84 = spatialReferenceFactory.CreateGeographicCoordinateSystem((int)esriSRGeoCSType.esriSRGeoCS_WGS1984) as ISpatialReference;

                System.Xml.Serialization.XmlSerializer serializer = null;
                serializer = new XmlSerializer(typeof(osm));

                osm createChangeSetOSM = new osm();
                string user_displayname = "";
                int userID = -1;

                // set the "default" value of the OSM server
                int maxElementsinChangeSet = 50000;

                HttpWebRequest httpClient = HttpWebRequest.Create(baseURLGPString.Value + "/api/capabilities") as HttpWebRequest;
                httpClient = OSMGPDownload.AssignProxyandCredentials(httpClient);
                SetBasicAuthHeader(httpClient, userCredentialGPValue.EncodedUserNamePassWord);
                httpClient.Timeout = secondsToTimeout * 1000;

                createChangeSetOSM.generator = m_Generator;
                createChangeSetOSM.version = "0.6";

                changeset createChangeSet = new changeset();
                createChangeSet.id = "0";
                createChangeSet.open = changesetOpen.@false;

                List<tag> changeSetTags = new List<tag>();

                tag createdByTag = new tag();
                createdByTag.k = "created_by";
                createdByTag.v = "ArcGIS Editor for OpenStreetMap";
                changeSetTags.Add(createdByTag);

                tag commentTag = new tag();
                commentTag.k = "comment";
                commentTag.v = uploadCommentGPString.Value;
                changeSetTags.Add(commentTag);

                createChangeSet.tag = changeSetTags.ToArray();
                createChangeSetOSM.Items = new object[] { createChangeSet };

                api apiCapabilities = null;

                // retrieve some server settings

                try
                {
                    httpResponse = httpClient.GetResponse() as HttpWebResponse;

                    osm osmCapabilities = null;

                    Stream stream = httpResponse.GetResponseStream();

                    XmlTextReader xmlReader = new XmlTextReader(stream);
                    osmCapabilities = serializer.Deserialize(xmlReader) as osm;
                    xmlReader.Close();

                    apiCapabilities = osmCapabilities.Items[0] as api;
                    httpResponse.Close();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                    message.AddWarning(ex.Message);
                }

                if (apiCapabilities != null)
                {
                    // read the timeout parameter
                    secondsToTimeout = Convert.ToInt32(apiCapabilities.timeout.seconds);
                    httpClient.Timeout = secondsToTimeout * 1000;

                    // update the setting of allowed features per changeset from the actual capabilities response
                    maxElementsinChangeSet = Convert.ToInt32(apiCapabilities.changesets.maximum_elements);
                }

                // retrieve some information about the user
                try
                {
                    httpClient = null;
                    httpClient = HttpWebRequest.Create(baseURLGPString.Value + "/api/0.6/user/details") as HttpWebRequest;
                    httpClient = OSMGPDownload.AssignProxyandCredentials(httpClient);
                    SetBasicAuthHeader(httpClient, userCredentialGPValue.EncodedUserNamePassWord);

                    httpResponse = httpClient.GetResponse() as HttpWebResponse;

                    osm osmCapabilities = null;

                    Stream stream = httpResponse.GetResponseStream();

                    XmlTextReader xmlReader = new XmlTextReader(stream);
                    osmCapabilities = serializer.Deserialize(xmlReader) as osm;
                    xmlReader.Close();
                    user userInformation = osmCapabilities.Items[0] as user;

                    if (userInformation != null)
                    {
                        user_displayname = userInformation.display_name;
                        userID = Convert.ToInt32(userInformation.id);
                    }
                }

                catch (ArgumentOutOfRangeException ex)
                {
                    message.AddError(120044, ex.Message);
                    return;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                    message.AddWarning(ex.Message);
                }



                IGPParameter revisionTableParameter = paramvalues.get_Element(in_changesTablesNumber) as IGPParameter;
                int featureUpdateCounter = 0;

                IQueryFilter revisionTableQueryFilter = null;

                try
                {
                    gpUtilities3.DecodeTableView(gpUtilities3.UnpackGPValue(revisionTableParameter), out revisionTable, out revisionTableQueryFilter);
                }
                catch
                {
                    message.AddError(120045,resourceManager.GetString("GPTools_OSMGPUpload_missingRevisionTable"));
                    return;
                }

                int revChangeSetIDFieldIndex = revisionTable.Fields.FindField("osmchangeset");
                int revActionFieldIndex = revisionTable.Fields.FindField("osmaction");
                int revElementTypeFieldIndex = revisionTable.Fields.FindField("osmelementtype");
                int revVersionFieldIndex = revisionTable.Fields.FindField("osmversion");
                int revFCNameFieldIndex = revisionTable.Fields.FindField("sourcefcname");
                int revOldIDFieldIndex = revisionTable.Fields.FindField("osmoldid");
                int revNewIDFieldIndex = revisionTable.Fields.FindField("osmnewid");
                int revStatusFieldIndex = revisionTable.Fields.FindField("osmstatus");
                int revStatusCodeFieldIndex = revisionTable.Fields.FindField("osmstatuscode");
                int revErrorMessageFieldIndex = revisionTable.Fields.FindField("osmerrormessage");
                int revLongitudeFieldIndex = revisionTable.Fields.FindField("osmlon");
                int revLatitudeFieldIndex = revisionTable.Fields.FindField("osmlat");

                // let's find all the rows that have a different status than OK - meaning success
                IQueryFilter queryFilter = new QueryFilterClass();

                searchCursor = revisionTable.Search(queryFilter, false);
                IRow searchRowToUpdate = null;

                // lookup table to adjust all osm ID references if there are know entities
                Dictionary<long, long> nodeosmIDLookup = new Dictionary<long, long>();
                Dictionary<long, long> wayosmIDLookup = new Dictionary<long, long>();
                Dictionary<long, long> relationosmIDLookup = new Dictionary<long, long>();

                // let's pre-populate the lookup IDs with already know entities
                // it is necessary if the revision table is used more than once and in different sessions
                queryFilter.WhereClause = "NOT " + revisionTable.SqlIdentifier("osmnewid") + " IS NULL";

                using (ComReleaser comReleaser = new ComReleaser())
                {
                    ICursor searchIDCursor = revisionTable.Search(queryFilter, false);
                    comReleaser.ManageLifetime(searchIDCursor);

                    IRow searchRow = searchIDCursor.NextRow();
                    comReleaser.ManageLifetime(searchRow);

                    while (searchRow != null)
                    {
                        if (revOldIDFieldIndex > -1 && revNewIDFieldIndex > -1)
                        {
                            string elementType = Convert.ToString(searchRow.get_Value(revElementTypeFieldIndex));

                            switch (elementType)
                            {
                                case "node":
                                    if (nodeosmIDLookup.ContainsKey(Convert.ToInt64(searchRow.get_Value(revOldIDFieldIndex))) == false)
                                    {
                                        nodeosmIDLookup.Add(Convert.ToInt64(searchRow.get_Value(revOldIDFieldIndex)), Convert.ToInt64(searchRow.get_Value(revNewIDFieldIndex)));
                                    }
                                    break;
                                case "way":
                                    if (wayosmIDLookup.ContainsKey(Convert.ToInt64(searchRow.get_Value(revOldIDFieldIndex))) == false)
                                    {
                                        wayosmIDLookup.Add(Convert.ToInt64(searchRow.get_Value(revOldIDFieldIndex)), Convert.ToInt64(searchRow.get_Value(revNewIDFieldIndex)));
                                    }
                                    break;
                                case "relation":
                                    if (relationosmIDLookup.ContainsKey(Convert.ToInt64(searchRow.get_Value(revOldIDFieldIndex))) == false)
                                    {
                                        relationosmIDLookup.Add(Convert.ToInt64(searchRow.get_Value(revOldIDFieldIndex)), Convert.ToInt64(searchRow.get_Value(revNewIDFieldIndex)));
                                    }
                                    break;
                                default:
                                    break;
                            }
                        }
                        searchRow = searchIDCursor.NextRow();
                    }
                }

                IFeatureClass pointFeatureClass = null;
                int pointOSMIDFieldIndex = -1;
                IFeatureClass lineFeatureClass = null;
                IFeatureClass polygonFeatureClass = null;
                ITable relationTable = null;

                int osmDelimiterPosition = ((IDataset)revisionTable).Name.IndexOf("_osm_");
                string osmBaseName = ((IDataset)revisionTable).Name.Substring(0, osmDelimiterPosition);

                IFeatureWorkspace osmFeatureWorkspace = ((IDataset)revisionTable).Workspace as IFeatureWorkspace;

                if (osmFeatureWorkspace != null)
                {
                    pointFeatureClass = osmFeatureWorkspace.OpenFeatureClass(osmBaseName + "_osm_pt");
                    pointOSMIDFieldIndex = pointFeatureClass.FindField("OSMID");
                    lineFeatureClass = osmFeatureWorkspace.OpenFeatureClass(osmBaseName + "_osm_ln");
                    polygonFeatureClass = osmFeatureWorkspace.OpenFeatureClass(osmBaseName + "_osm_ply");
                    relationTable = osmFeatureWorkspace.OpenTable(osmBaseName + "_osm_relation");
                }

                // determine version of extension
                int internalExtensionVersion = pointFeatureClass.OSMExtensionVersion();

                string sData = OsmRest.SerializeUtils.CreateXmlSerializable(createChangeSetOSM, serializer, Encoding.ASCII, "text/xml");
                HttpWebRequest httpClient2 = HttpWebRequest.Create(baseURLGPString.Value + "/api/0.6/changeset/create") as HttpWebRequest;
                httpClient2.Method = "PUT";
                httpClient2 = OSMGPDownload.AssignProxyandCredentials(httpClient2);
                SetBasicAuthHeader(httpClient2, userCredentialGPValue.EncodedUserNamePassWord);
                httpClient2.Timeout = secondsToTimeout * 1000;

                try
                {
                    Stream requestStream = httpClient2.GetRequestStream();
                    StreamWriter mywriter = new StreamWriter(requestStream);

                    mywriter.Write(sData);
                    mywriter.Close();

                    WebResponse clientResponse = httpClient2.GetResponse();
                    Stream readStream = clientResponse.GetResponseStream();
                    StreamReader streamReader = new StreamReader(readStream);
                    changeSetID = streamReader.ReadToEnd();
                    streamReader.Close();

                    message.AddMessage(String.Format(resourceManager.GetString("GPTools_OSMGPUpload_openChangeSet"), changeSetID));

                }
                catch (Exception ex)
                {
                    if (httpResponse != null)
                    {
                        if (httpResponse.StatusCode != System.Net.HttpStatusCode.OK)
                        {
                            foreach (var errorItem in httpResponse.Headers.GetValues("Error"))
                            {
                                message.AddError(120009, errorItem);
                            }

                            message.AddError(120009, httpResponse.StatusCode.ToString());
                            message.AddError(120009, ex.Message);
                        }
                    }
                    else
                    {
                        message.AddError(120047, ex.Message);
                    }

                    return;
                }

                IGPParameter uploadFormatParameter = paramvalues.get_Element(in_uploadFormatNumber) as IGPParameter;
                IGPBoolean useOSMChangeFormatGPValue = gpUtilities3.UnpackGPValue(uploadFormatParameter) as IGPBoolean;

                // Al Hack
                if (useOSMChangeFormatGPValue == null)
                {
                    useOSMChangeFormatGPValue = new GPBoolean();
                    useOSMChangeFormatGPValue.Value = false;
                }


                SQLFormatter sqlFormatter = new SQLFormatter(revisionTable);

                if (useOSMChangeFormatGPValue.Value == true)
                {
                    #region osmchange upload format

                    osmChange osmChangeDocument = new osmChange();
                    osmChangeDocument.generator = m_Generator;
                    osmChangeDocument.version = "0.6";

                    // xml elements to describe the changeset
                    create uploadCreates = null;
                    modify uploadModify = null;
                    delete uploadDelete = null;

                    // helper classes to keep track of elements entered into a changeset
                    List<object> listOfCreates = null;
                    List<object> listOfModifies = null;
                    List<object> listOfDeletes = null;

                    List<object> changeSetItems = new List<object>();

                    #region upload create actions
                    // loop through creates
                    queryFilter.WhereClause = "(" + sqlFormatter.SqlIdentifier("osmstatuscode") + " <> 200 OR "
                        + sqlFormatter.SqlIdentifier("osmstatus") + " IS NULL) AND "
                        + sqlFormatter.SqlIdentifier("osmaction") + " = 'create'";

                    using (ComReleaser comReleaser = new ComReleaser())
                    {
                        searchCursor = revisionTable.Search(queryFilter, false);
                        comReleaser.ManageLifetime(searchCursor);

                        searchRowToUpdate = searchCursor.NextRow();
                        comReleaser.ManageLifetime(searchRowToUpdate);

                        // if we have at least one entry with a create action, then add the 'create' element to the changeset representation
                        if (searchRowToUpdate != null)
                        {
                            uploadCreates = new create();
                            listOfCreates = new List<object>();
                        }

                        while (searchRowToUpdate != null)
                        {
                            try
                            {
                                if (TrackCancel.Continue() == false)
                                {
                                    closeChangeSet(message, userCredentialGPValue.EncodedUserNamePassWord, secondsToTimeout, changeSetID, baseURLGPString);
                                    return;
                                }

                                string action = String.Empty;
                                if (revActionFieldIndex != -1)
                                {
                                    action = searchRowToUpdate.get_Value(revActionFieldIndex) as string;
                                }
                                string elementType = String.Empty;
                                if (revElementTypeFieldIndex != -1)
                                {
                                    elementType = searchRowToUpdate.get_Value(revElementTypeFieldIndex) as string;
                                }

                                string sourceFCName = String.Empty;
                                if (revFCNameFieldIndex != -1)
                                {
                                    sourceFCName = searchRowToUpdate.get_Value(revFCNameFieldIndex) as string;
                                }

                                long osmOldID = -1;
                                if (revOldIDFieldIndex != -1)
                                {
                                    osmOldID = Convert.ToInt64(searchRowToUpdate.get_Value(revOldIDFieldIndex));
                                }

                                // if the overall number of uploaded elements is too big for a single changeset we do need to split it up
                                // into multiple sets
                                if (featureUpdateCounter > 0 & (featureUpdateCounter % maxElementsinChangeSet) == 0)
                                {
                                    // add any outstanding creations to the changeset items
                                    if (listOfCreates != null && uploadCreates != null)
                                    {
                                        uploadCreates.Items = listOfCreates.ToArray();
                                        // in case there are any creates let's add them to the changeset document
                                        changeSetItems.Add(uploadCreates);
                                    }

                                    // add all the changeset items to the changeset document
                                    osmChangeDocument.Items = changeSetItems.ToArray();

                                    // submit changeset
                                    try
                                    {
                                        httpClient = HttpWebRequest.Create(baseURLGPString.Value + "/api/0.6/changeset/" + changeSetID + "/upload") as HttpWebRequest;
                                        httpClient = OSMGPDownload.AssignProxyandCredentials(httpClient);
                                        httpClient.Method = "POST";
                                        SetBasicAuthHeader(httpClient, userCredentialGPValue.EncodedUserNamePassWord);
                                        httpClient.Timeout = secondsToTimeout * 1000;

                                        string sContent = OsmRest.SerializeUtils.CreateXmlSerializable(osmChangeDocument, null, Encoding.UTF8, "text/xml");

                                        message.AddMessage(String.Format(resourceManager.GetString("GPTools_OSMGPUpload_featureSubmit"), featureUpdateCounter));

                                        OsmRest.HttpUtils.Post(httpClient, sContent);

                                        httpResponse = httpClient.GetResponse() as HttpWebResponse;
                                        diffResult diffResultResonse = OsmRest.HttpUtils.GetResponse(httpResponse);

                                        // parse changes locally and update local data sources
                                        if (diffResultResonse != null)
                                        {
                                            ParseResultDiff(diffResultResonse, revisionTable, pointFeatureClass, lineFeatureClass, polygonFeatureClass, relationTable, user_displayname, userID, changeSetID, nodeosmIDLookup, wayosmIDLookup, relationosmIDLookup, internalExtensionVersion);
                                        }

                                    }
                                    catch (Exception ex)
                                    {
                                        closeChangeSet(message, userCredentialGPValue.EncodedUserNamePassWord, secondsToTimeout, changeSetID, baseURLGPString);
                                        message.AddError(120009, ex.Message);

                                        if (ex is WebException)
                                        {
                                            WebException webException = ex as WebException;
                                            string serverErrorMessage = webException.Response.Headers["Error"];
                                            if (!String.IsNullOrEmpty(serverErrorMessage))
                                            {
                                                message.AddError(120009, serverErrorMessage);
                                            }
                                        }

                                        if (httpResponse != null)
                                        {
                                            httpResponse.Close();
                                        }
                                        return;
                                    }
                                    finally
                                    {
                                        // reset the list and containers of modifications for the next batch
                                        listOfCreates.Clear();
                                        changeSetItems.Clear();

                                        if (httpResponse != null)
                                        {
                                            httpResponse.Close();
                                        }
                                    }

                                    if (TrackCancel.Continue() == false)
                                    {
                                        closeChangeSet(message, userCredentialGPValue.EncodedUserNamePassWord, secondsToTimeout, changeSetID, baseURLGPString);
                                        return;
                                    }

                                    CreateNextChangeSet(message, createChangeSetOSM, userCredentialGPValue.EncodedUserNamePassWord, secondsToTimeout, ref changeSetID, baseURLGPString, ref featureUpdateCounter);
                                }

                                switch (elementType)
                                {
                                    case "node":
                                        node createNode = CreateNodeRepresentation(pointFeatureClass, action, osmOldID, changeSetID, 1, null, internalExtensionVersion);
                                        listOfCreates.Add(createNode);
                                        break;
                                    case "way":
                                        way createWay = null;
                                        if (sourceFCName.Contains("_osm_ln"))
                                        {
                                            createWay = CreateWayRepresentation(lineFeatureClass, action, osmOldID, changeSetID, 1, nodeosmIDLookup, pointFeatureClass, pointOSMIDFieldIndex, internalExtensionVersion);
                                        }
                                        else if (sourceFCName.Contains("_osm_ply"))
                                        {
                                            createWay = CreateWayRepresentation(polygonFeatureClass, action, osmOldID, changeSetID, 1, nodeosmIDLookup, pointFeatureClass, pointOSMIDFieldIndex, internalExtensionVersion);
                                        }
                                        listOfCreates.Add(createWay);
                                        break;
                                    case "relation":
                                        relation createRelation = null;
                                        if (sourceFCName.Contains("_osm_ln"))
                                        {
                                            createRelation = CreateRelationRepresentation((ITable)lineFeatureClass, action, osmOldID, changeSetID, 1, nodeosmIDLookup, wayosmIDLookup, relationosmIDLookup, internalExtensionVersion);
                                        }
                                        else if (sourceFCName.Contains("_osm_ply"))
                                        {
                                            createRelation = CreateRelationRepresentation((ITable)polygonFeatureClass, action, osmOldID, changeSetID, 1, nodeosmIDLookup, wayosmIDLookup, relationosmIDLookup, internalExtensionVersion);
                                        }
                                        else if (sourceFCName.Contains("_osm_relation"))
                                        {
                                            createRelation = CreateRelationRepresentation(relationTable, action, osmOldID, changeSetID, 1, nodeosmIDLookup, wayosmIDLookup, relationosmIDLookup, internalExtensionVersion);
                                        }
                                        listOfCreates.Add(createRelation);
                                        break;
                                    default:
                                        break;
                                }
                                // increment the counter keeping track of the submitted changes
                                featureUpdateCounter = featureUpdateCounter + 1;
                            }
                            catch
                            {
                            }
                            searchRowToUpdate = searchCursor.NextRow();
                        }

                        if (listOfCreates != null && uploadCreates != null)
                        {
                            // sort the list of created elements in the order of nodes, ways, relations
                            listOfCreates.Sort(new OSMElementComparer());

                            uploadCreates.Items = listOfCreates.ToArray();
                            // in case there are any creates let's add them to the changeset document
                            changeSetItems.Add(uploadCreates);
                        }
                    }
                    #endregion

                    #region upload modify actions
                    // loop through modifies
                    using (ComReleaser comReleaser = new ComReleaser())
                    {
                        queryFilter.WhereClause = "(" +
                            sqlFormatter.SqlIdentifier("osmstatuscode") + " <> 200 OR "
                            + sqlFormatter.SqlIdentifier("osmstatus") + " IS NULL) AND "
                            + sqlFormatter.SqlIdentifier("osmaction") + " = 'modify'";

                        searchCursor = revisionTable.Search(queryFilter, false);
                        comReleaser.ManageLifetime(searchCursor);

                        searchRowToUpdate = searchCursor.NextRow();

                        if (searchRowToUpdate != null)
                        {
                            uploadModify = new modify();
                            listOfModifies = new List<object>();
                        }

                        while (searchRowToUpdate != null)
                        {
                            if (TrackCancel.Continue() == false)
                            {
                                closeChangeSet(message, userCredentialGPValue.EncodedUserNamePassWord, secondsToTimeout, changeSetID, baseURLGPString);
                                return;
                            }

                            try
                            {
                                string action = String.Empty;
                                if (revActionFieldIndex != -1)
                                {
                                    action = searchRowToUpdate.get_Value(revActionFieldIndex) as string;
                                }

                                string elementType = String.Empty;
                                if (revElementTypeFieldIndex != -1)
                                {
                                    elementType = searchRowToUpdate.get_Value(revElementTypeFieldIndex) as string;
                                }

                                string sourceFCName = String.Empty;
                                if (revFCNameFieldIndex != -1)
                                {
                                    sourceFCName = searchRowToUpdate.get_Value(revFCNameFieldIndex) as string;
                                }

                                long osmOldID = -1;
                                if (revOldIDFieldIndex != -1)
                                {
                                    osmOldID = Convert.ToInt64(searchRowToUpdate.get_Value(revOldIDFieldIndex));
                                }

                                // if the overall number of uploaded elements is too big for a single changeset we do need to split it up
                                long modifyID = -1;
                                if (revNewIDFieldIndex != -1)
                                {
                                    object osmIDValue = searchRowToUpdate.get_Value(revNewIDFieldIndex);

                                    if (osmIDValue == DBNull.Value)
                                    {
                                        osmIDValue = osmOldID;
                                    }

                                    try
                                    {
                                        modifyID = Convert.ToInt64(osmIDValue);
                                    }
                                    catch { }

                                    // modifies should only happen to osm IDs > 0
                                    // if that condition is not met let's skip this feature as something is not right
                                    if (modifyID < 0)
                                    {
                                        searchRowToUpdate = searchCursor.NextRow();
                                        continue;
                                    }
                                }

                                int osmVersion = -1;
                                if (revVersionFieldIndex != -1)
                                {
                                    osmVersion = Convert.ToInt32(searchRowToUpdate.get_Value(revVersionFieldIndex));
                                }

                                // into multiple sets
                                if ((featureUpdateCounter % maxElementsinChangeSet) == 0)
                                {
                                    // add any outstanding modifications to the changeset items
                                    if (listOfModifies != null && uploadModify != null)
                                    {
                                        uploadModify.Items = listOfModifies.ToArray();
                                        // in case there are any creates let's add them to the changeset document
                                        changeSetItems.Add(uploadModify);
                                    }

                                    // add all the changeset items to the changeset document
                                    osmChangeDocument.Items = changeSetItems.ToArray();

                                    // submit changeset
                                    try
                                    {
                                        httpClient = HttpWebRequest.Create(baseURLGPString.Value + "/api/0.6/changeset/" + changeSetID + "/upload") as HttpWebRequest;
                                        httpClient = OSMGPDownload.AssignProxyandCredentials(httpClient);
                                        httpClient.Method = "POST";
                                        SetBasicAuthHeader(httpClient, userCredentialGPValue.EncodedUserNamePassWord);
                                        httpClient.Timeout = secondsToTimeout * 1000;


                                        string sContent = OsmRest.SerializeUtils.CreateXmlSerializable(osmChangeDocument, null, Encoding.UTF8, "text/xml");
                                        OsmRest.HttpUtils.Post(httpClient, sContent);

                                        httpResponse = httpClient.GetResponse() as HttpWebResponse;
                                        diffResult diffResultResonse = OsmRest.HttpUtils.GetResponse(httpResponse);

                                        // parse changes locally and update local data sources
                                        if (diffResultResonse != null)
                                        {
                                            ParseResultDiff(diffResultResonse, revisionTable, pointFeatureClass, lineFeatureClass, polygonFeatureClass, relationTable, user_displayname, userID, changeSetID, nodeosmIDLookup, wayosmIDLookup, relationosmIDLookup, internalExtensionVersion);
                                        }

                                    }
                                    catch (Exception ex)
                                    {
                                        closeChangeSet(message, userCredentialGPValue.EncodedUserNamePassWord, secondsToTimeout, changeSetID, baseURLGPString);
                                        message.AddError(120009, ex.Message);

                                        if (ex is WebException)
                                        {
                                            WebException webException = ex as WebException;
                                            string serverErrorMessage = webException.Response.Headers["Error"];
                                            if (!String.IsNullOrEmpty(serverErrorMessage))
                                            {
                                                message.AddError(120009, serverErrorMessage);
                                            }

                                            if (httpResponse != null)
                                            {
                                                httpResponse.Close();
                                            }
                                        }
                                        return;
                                    }
                                    finally
                                    {
                                        // reset the list and containers of modifications for the next batch
                                        listOfModifies.Clear();
                                        changeSetItems.Clear();

                                        if (httpResponse != null)
                                        {
                                            httpResponse.Close();
                                        }
                                    }

                                    if (TrackCancel.Continue() == false)
                                    {
                                        closeChangeSet(message, userCredentialGPValue.EncodedUserNamePassWord, secondsToTimeout, changeSetID, baseURLGPString);
                                        return;
                                    }

                                    CreateNextChangeSet(message, createChangeSetOSM, userCredentialGPValue.EncodedUserNamePassWord, secondsToTimeout, ref changeSetID, baseURLGPString, ref featureUpdateCounter);
                                }

                                switch (elementType)
                                {
                                    case "node":
                                        node updateNode = CreateNodeRepresentation(pointFeatureClass, action, modifyID, changeSetID, osmVersion, null, internalExtensionVersion);
                                        listOfModifies.Add(updateNode);
                                        break;
                                    case "way":
                                        way updateWay = null;
                                        if (sourceFCName.Contains("_osm_ln"))
                                        {
                                            updateWay = CreateWayRepresentation(lineFeatureClass, action, modifyID, changeSetID, osmVersion, nodeosmIDLookup, pointFeatureClass, pointOSMIDFieldIndex, internalExtensionVersion);
                                        }
                                        else if (sourceFCName.Contains("_osm_ply"))
                                        {
                                            updateWay = CreateWayRepresentation(polygonFeatureClass, action, modifyID, changeSetID, osmVersion, nodeosmIDLookup, pointFeatureClass, pointOSMIDFieldIndex, internalExtensionVersion);
                                        }
                                        listOfModifies.Add(updateWay);
                                        break;
                                    case "relation":
                                        relation updateRelation = null;
                                        if (sourceFCName.Contains("_osm_ln"))
                                        {
                                            updateRelation = CreateRelationRepresentation((ITable)lineFeatureClass, action, modifyID, changeSetID, osmVersion, nodeosmIDLookup, wayosmIDLookup, relationosmIDLookup, internalExtensionVersion);
                                        }
                                        else if (sourceFCName.Contains("_osm_ply"))
                                        {
                                            updateRelation = CreateRelationRepresentation((ITable)polygonFeatureClass, action, modifyID, changeSetID, osmVersion, nodeosmIDLookup, wayosmIDLookup, relationosmIDLookup, internalExtensionVersion);
                                        }
                                        else if (sourceFCName.Contains("_osm_relation"))
                                        {
                                            updateRelation = CreateRelationRepresentation(relationTable, action, modifyID, changeSetID, osmVersion, nodeosmIDLookup, wayosmIDLookup, relationosmIDLookup, internalExtensionVersion);
                                        }
                                        listOfModifies.Add(updateRelation);
                                        break;
                                    default:
                                        break;
                                }


                                // track the update/sync requests against the server
                                featureUpdateCounter = featureUpdateCounter + 1;
                            }
                            catch
                            {
                            }

                            searchRowToUpdate = searchCursor.NextRow();
                        }

                        if (listOfModifies != null && uploadModify != null)
                        {
                            uploadModify.Items = listOfModifies.ToArray();
                            // in case there are any creates let's add them to the changeset document
                            changeSetItems.Add(uploadModify);
                        }

                    }
                    #endregion

                    #region upload delete actions
                    // loop through deletes in "reverse" - relation, then way, then node
                    string[] elementTypes = new string[] { "relation", "way", "node" };

                    foreach (string osmElementType in elementTypes)
                    {
                        using (ComReleaser comReleaser = new ComReleaser())
                        {
                            queryFilter.WhereClause = "(" +  sqlFormatter.SqlIdentifier("osmstatuscode") + " <> 200 OR "
                                + sqlFormatter.SqlIdentifier("osmstatus") + " IS NULL) AND "
                                + sqlFormatter.SqlIdentifier("osmaction") + " = 'delete' AND "
                                + sqlFormatter.SqlIdentifier("osmelementtype") + " = '" + osmElementType + "'";

                            searchCursor = revisionTable.Search(queryFilter, false);
                            comReleaser.ManageLifetime(searchCursor);

                            searchRowToUpdate = searchCursor.NextRow();

                            if (searchRowToUpdate != null)
                            {
                                if (TrackCancel.Continue() == false)
                                {
                                    closeChangeSet(message, userCredentialGPValue.EncodedUserNamePassWord, secondsToTimeout, changeSetID, baseURLGPString);
                                    return;
                                }

                                if (uploadDelete == null)
                                {
                                    uploadDelete = new delete();
                                    listOfDeletes = new List<object>();
                                }
                            }

                            while (searchRowToUpdate != null)
                            {
                                try
                                {
                                    string action = String.Empty;
                                    if (revActionFieldIndex != -1)
                                    {
                                        action = searchRowToUpdate.get_Value(revActionFieldIndex) as string;
                                    }

                                    string elementType = String.Empty;
                                    if (revElementTypeFieldIndex != -1)
                                    {
                                        elementType = searchRowToUpdate.get_Value(revElementTypeFieldIndex) as string;
                                    }

                                    string sourceFCName = String.Empty;
                                    if (revFCNameFieldIndex != -1)
                                    {
                                        sourceFCName = searchRowToUpdate.get_Value(revFCNameFieldIndex) as string;
                                    }

                                    long osmOldID = -1;
                                    if (revOldIDFieldIndex != -1)
                                    {
                                        osmOldID = Convert.ToInt64(searchRowToUpdate.get_Value(revOldIDFieldIndex));
                                    }

                                    int osmVersion = -1;
                                    if (revVersionFieldIndex != -1)
                                    {
                                        osmVersion = Convert.ToInt32(searchRowToUpdate.get_Value(revVersionFieldIndex));
                                    }

                                    // into multiple sets
                                    if ((featureUpdateCounter % maxElementsinChangeSet) == 0)
                                    {
                                        // add any outstanding creations to the changeset items
                                        if (listOfDeletes != null && uploadDelete != null)
                                        {
                                            uploadDelete.Items = listOfDeletes.ToArray();
                                            // in case there are any creates let's add them to the changeset document
                                            changeSetItems.Add(uploadDelete);
                                        }

                                        // add all the changeset items to the changeset document
                                        osmChangeDocument.Items = changeSetItems.ToArray();

                                        // submit changeset
                                        try
                                        {
                                            httpClient = HttpWebRequest.Create(baseURLGPString.Value + "/api/0.6/changeset/" + changeSetID + "/upload") as HttpWebRequest;
                                            httpClient = OSMGPDownload.AssignProxyandCredentials(httpClient);
                                            httpClient.Method = "POST";
                                            SetBasicAuthHeader(httpClient, userCredentialGPValue.EncodedUserNamePassWord);
                                            httpClient.Timeout = secondsToTimeout * 1000;


                                            string sContent = OsmRest.SerializeUtils.CreateXmlSerializable(osmChangeDocument, null, Encoding.UTF8, "text/xml");
                                            OsmRest.HttpUtils.Post(httpClient, sContent);

                                            httpResponse = httpClient.GetResponse() as HttpWebResponse;
                                            diffResult diffResultResonse = OsmRest.HttpUtils.GetResponse(httpResponse);

                                            // parse changes locally and update local data sources
                                            if (diffResultResonse != null)
                                            {
                                                ParseResultDiff(diffResultResonse, revisionTable, pointFeatureClass, lineFeatureClass, polygonFeatureClass, relationTable, user_displayname, userID, changeSetID, nodeosmIDLookup, wayosmIDLookup, relationosmIDLookup, internalExtensionVersion);
                                            }

                                        }
                                        catch (Exception ex)
                                        {
                                            closeChangeSet(message, userCredentialGPValue.EncodedUserNamePassWord, secondsToTimeout, changeSetID, baseURLGPString);
                                            message.AddError(120009, ex.Message);

                                            if (ex is WebException)
                                            {
                                                WebException webException = ex as WebException;
                                                string serverErrorMessage = webException.Response.Headers["Error"];
                                                if (!String.IsNullOrEmpty(serverErrorMessage))
                                                {
                                                    message.AddError(120009, serverErrorMessage);
                                                }
                                            }

                                            if (httpResponse != null)
                                            {
                                                httpResponse.Close();
                                            }

                                            return;
                                        }
                                        finally
                                        {
                                            // reset the list and containers of modifications for the next batch
                                            listOfDeletes.Clear();
                                            changeSetItems.Clear();

                                            if (httpResponse != null)
                                            {
                                                httpResponse.Close();
                                            }
                                        }

                                        if (TrackCancel.Continue() == false)
                                        {
                                            closeChangeSet(message, userCredentialGPValue.EncodedUserNamePassWord, secondsToTimeout, changeSetID, baseURLGPString);
                                            return;
                                        }

                                        CreateNextChangeSet(message, createChangeSetOSM, userCredentialGPValue.EncodedUserNamePassWord, secondsToTimeout, ref changeSetID, baseURLGPString, ref featureUpdateCounter);
                                    }

                                    switch (elementType)
                                    {
                                        case "node":
                                            IPoint deletePoint = null;
                                            if (revLongitudeFieldIndex != -1 && revLatitudeFieldIndex != -1)
                                            {
                                                try
                                                {
                                                    // let's reconstruct the delete point
                                                    deletePoint = new PointClass();
                                                    deletePoint.X = Convert.ToDouble(searchRowToUpdate.get_Value(revLongitudeFieldIndex));
                                                    deletePoint.Y = Convert.ToDouble(searchRowToUpdate.get_Value(revLatitudeFieldIndex));
                                                    deletePoint.SpatialReference = m_wgs84;
                                                }
                                                catch (Exception ex)
                                                {
                                                    message.AddWarning(ex.Message);
                                                }

                                                if (deletePoint == null)
                                                {
                                                    // inform the about the issue - no successful creation of point and continue on to the next delete instruction
                                                    // in the revision table
                                                    message.AddWarning(resourceManager.GetString("GPTools_OSMGPUpload_invalidPoint"));
                                                    searchRowToUpdate = searchCursor.NextRow();
                                                    continue;
                                                }
                                            }


                                            node deleteNode = CreateNodeRepresentation(pointFeatureClass, action, osmOldID, changeSetID, osmVersion, deletePoint, internalExtensionVersion);
                                            listOfDeletes.Add(deleteNode);
                                            break;
                                        case "way":
                                            way deleteWay = null;
                                            if (sourceFCName.Contains("_osm_ln"))
                                            {
                                                deleteWay = CreateWayRepresentation(lineFeatureClass, action, osmOldID, changeSetID, osmVersion, nodeosmIDLookup, pointFeatureClass, pointOSMIDFieldIndex, internalExtensionVersion);
                                            }
                                            else if (sourceFCName.Contains("_osm_ply"))
                                            {
                                                deleteWay = CreateWayRepresentation(polygonFeatureClass, action, osmOldID, changeSetID, osmVersion, nodeosmIDLookup, pointFeatureClass, pointOSMIDFieldIndex, internalExtensionVersion);
                                            }
                                            listOfDeletes.Add(deleteWay);
                                            break;
                                        case "relation":
                                            relation deleteRelation = null;
                                            if (sourceFCName.Contains("_osm_ln"))
                                            {
                                                deleteRelation = CreateRelationRepresentation((ITable)lineFeatureClass, action, osmOldID, changeSetID, osmVersion, nodeosmIDLookup, wayosmIDLookup, relationosmIDLookup, internalExtensionVersion);
                                            }
                                            else if (sourceFCName.Contains("_osm_ply"))
                                            {
                                                deleteRelation = CreateRelationRepresentation((ITable)polygonFeatureClass, action, osmOldID, changeSetID, osmVersion, nodeosmIDLookup, wayosmIDLookup, relationosmIDLookup, internalExtensionVersion);
                                            }
                                            else if (sourceFCName.Contains("_osm_relation"))
                                            {
                                                deleteRelation = CreateRelationRepresentation(relationTable, action, osmOldID, changeSetID, osmVersion, nodeosmIDLookup, wayosmIDLookup, relationosmIDLookup, internalExtensionVersion);
                                            }
                                            listOfDeletes.Add(deleteRelation);
                                            break;
                                        default:
                                            break;
                                    }


                                    // track the update/sync requests against the server
                                    featureUpdateCounter = featureUpdateCounter + 1;
                                }
                                catch
                                {
                                }

                                searchRowToUpdate = searchCursor.NextRow();
                            }

                        }
                    }

                    if (listOfDeletes != null && uploadDelete != null)
                    {
                        uploadDelete.Items = listOfDeletes.ToArray();
                        // in case there are any creates let's add them to the changeset document
                        changeSetItems.Add(uploadDelete);
                    }
                    #endregion

                    if (TrackCancel.Continue() == false)
                    {
                        closeChangeSet(message, userCredentialGPValue.EncodedUserNamePassWord, secondsToTimeout, changeSetID, baseURLGPString);
                        return;
                    }

                    // add all the changeset items to the changeset document
                    osmChangeDocument.Items = changeSetItems.ToArray();

                    // submit changeset
                    try
                    {
                        httpClient = HttpWebRequest.Create(baseURLGPString.Value + "/api/0.6/changeset/" + changeSetID + "/upload") as HttpWebRequest;
                        httpClient = OSMGPDownload.AssignProxyandCredentials(httpClient);
                        httpClient.Method = "POST";
                        SetBasicAuthHeader(httpClient, userCredentialGPValue.EncodedUserNamePassWord);
                        httpClient.Timeout = secondsToTimeout * 1000;

                        message.AddMessage(String.Format(resourceManager.GetString("GPTools_OSMGPUpload_featureSubmit"), featureUpdateCounter));

                        string sContent = OsmRest.SerializeUtils.CreateXmlSerializable(osmChangeDocument, null, Encoding.UTF8, "text/xml");
                        OsmRest.HttpUtils.Post(httpClient, sContent);

                        httpResponse = httpClient.GetResponse() as HttpWebResponse;

                        //Exception with an error HTTP 400
                        diffResult diffResultResonse = OsmRest.HttpUtils.GetResponse(httpResponse);

                        message.AddMessage(resourceManager.GetString("GPTools_OSMGPUpload_updatelocalData"));

                        // parse changes locally and update local data sources
                        if (diffResultResonse != null)
                        {
                            ParseResultDiff(diffResultResonse, revisionTable, pointFeatureClass, lineFeatureClass, polygonFeatureClass, relationTable, user_displayname, userID, changeSetID, nodeosmIDLookup, wayosmIDLookup, relationosmIDLookup, internalExtensionVersion);
                        }
                    }
                    catch (Exception ex)
                    {
                        message.AddError(120009, ex.Message);

                        try
                        {
                            if (ex is WebException)
                            {
                                WebException webException = ex as WebException;
                                string serverErrorMessage = webException.Response.Headers["Error"];
                                if (!String.IsNullOrEmpty(serverErrorMessage))
                                {
                                    message.AddError(120009, serverErrorMessage);
                                }
                            }
                        }
                        catch (Exception innerexception)
                        {
                            message.AddError(120009, innerexception.Message);
                        }
                    }
                    finally
                    {
                        if (httpResponse != null)
                        {
                            httpResponse.Close();
                        }
                    }
                    #endregion
                }
                else
                {
                    #region single upload format
                    #region submit the create nodes first
                    queryFilter.WhereClause = "(" + 
                        sqlFormatter.SqlIdentifier("osmstatuscode") + " <> 200 OR "
                        + sqlFormatter.SqlIdentifier("osmstatus") + " IS NULL) AND "
                        + sqlFormatter.SqlIdentifier("osmaction") + " = 'create'  AND "
                        + sqlFormatter.SqlIdentifier("osmelementtype") + " = 'node'";

                    using (ComReleaser comReleaser = new ComReleaser())
                    {
                        searchCursor = revisionTable.Search(queryFilter, false);
                        comReleaser.ManageLifetime(searchCursor);

                        while ((searchRowToUpdate = searchCursor.NextRow()) != null)
                        {
                            try
                            {
                                string action = String.Empty;
                                if (revActionFieldIndex != -1)
                                {
                                    action = searchRowToUpdate.get_Value(revActionFieldIndex) as string;
                                }
                                string elementType = String.Empty;
                                if (revElementTypeFieldIndex != -1)
                                {
                                    elementType = searchRowToUpdate.get_Value(revElementTypeFieldIndex) as string;
                                }

                                string sourceFCName = String.Empty;
                                if (revFCNameFieldIndex != -1)
                                {
                                    sourceFCName = searchRowToUpdate.get_Value(revFCNameFieldIndex) as string;
                                }

                                long osmOldID = -1;
                                if (revOldIDFieldIndex != -1)
                                {
                                    osmOldID = Convert.ToInt64(searchRowToUpdate.get_Value(revOldIDFieldIndex));
                                }

                                // if the overall number of uploaded elements is too big for a single changeset we do need to split it up
                                // into multiple sets
                                if ((featureUpdateCounter % maxElementsinChangeSet) == 0)
                                {
                                    CreateNextChangeSet(message, createChangeSetOSM, userCredentialGPValue.EncodedUserNamePassWord, secondsToTimeout, ref changeSetID, baseURLGPString, ref featureUpdateCounter);
                                }

                                osm createNode = CreateOSMNodeRepresentation(pointFeatureClass, action, osmOldID, changeSetID, -1, null, internalExtensionVersion);

                                httpClient = null;
                                httpClient = HttpWebRequest.Create(baseURLGPString.Value + "/api/0.6/" + elementType + "/create") as HttpWebRequest;
                                httpClient = OSMGPDownload.AssignProxyandCredentials(httpClient);
                                httpClient.Method = "PUT";
                                SetBasicAuthHeader(httpClient, userCredentialGPValue.EncodedUserNamePassWord);
                                httpClient.Timeout = secondsToTimeout * 1000;

                                httpResponse = null;

                                string nodeContent = OsmRest.SerializeUtils.CreateXmlSerializable(createNode, serializer, Encoding.UTF8, "text/xml");

                                if (String.IsNullOrEmpty(nodeContent))
                                {
                                    continue;
                                }

                                OsmRest.HttpUtils.Put(httpClient, nodeContent);

                                httpResponse = httpClient.GetResponse() as HttpWebResponse;

                                createNode = null;

                                // track the update/sync requests against the server
                                featureUpdateCounter = featureUpdateCounter + 1;

                                if (httpResponse != null)
                                {
                                    string newIDString = OsmRest.HttpUtils.GetResponseContent(httpResponse);

                                    nodeosmIDLookup.Add(osmOldID, Convert.ToInt64(newIDString));

                                    // update the revision table
                                    if (revNewIDFieldIndex != -1)
                                    {
                                        searchRowToUpdate.set_Value(revNewIDFieldIndex, Convert.ToString(newIDString));
                                    }
                                    if (revVersionFieldIndex != -1)
                                    {
                                        searchRowToUpdate.set_Value(revVersionFieldIndex, 1);
                                    }
                                    if (revStatusFieldIndex != -1)
                                    {
                                        searchRowToUpdate.set_Value(revStatusFieldIndex, httpResponse.StatusCode.ToString());
                                    }
                                    if (revStatusCodeFieldIndex != -1)
                                    {
                                        searchRowToUpdate.set_Value(revStatusCodeFieldIndex, (int)httpResponse.StatusCode);
                                    }
                                    if (revChangeSetIDFieldIndex != -1)
                                    {
                                        searchRowToUpdate.set_Value(revChangeSetIDFieldIndex, Convert.ToString(changeSetID));
                                    }

                                    // update the source point feature class as well
                                    updateSource((ITable)pointFeatureClass, action, osmOldID, Convert.ToInt64(newIDString), user_displayname, userID, 1, Convert.ToInt32(changeSetID), nodeosmIDLookup, wayosmIDLookup, relationosmIDLookup,internalExtensionVersion);
                                }
                            }
                            catch (Exception ex)
                            {

                                message.AddError(120009, ex.Message);

                                if (ex is WebException)
                                {
                                    updateErrorStatus(message, revStatusFieldIndex, revStatusCodeFieldIndex, revErrorMessageFieldIndex, ref searchRowToUpdate, ex);
                                }
                            }
                            finally
                            {
                                try
                                {
                                    searchRowToUpdate.Store();
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine(ex.Message);
                                }

                                if (httpResponse != null)
                                {
                                    httpResponse.Close();
                                }
                            }

                            if (TrackCancel.Continue() == false)
                            {
                                closeChangeSet(message, userCredentialGPValue.EncodedUserNamePassWord, secondsToTimeout, changeSetID, baseURLGPString);
                                return;
                            }
                        }
                    }
                    #endregion

                    #region next the create ways
                    queryFilter.WhereClause = "(" + 
                        sqlFormatter.SqlIdentifier("osmstatuscode") + " <> 200 OR "
                        + sqlFormatter.SqlIdentifier("osmstatus") + " IS NULL) AND "
                        + sqlFormatter.SqlIdentifier("osmaction") + " = 'create'  AND "
                        + sqlFormatter.SqlIdentifier("osmelementtype") + " = 'way'";

                    using (ComReleaser comReleaser = new ComReleaser())
                    {
                        searchCursor = revisionTable.Search(queryFilter, false);
                        comReleaser.ManageLifetime(searchCursor);

                        while ((searchRowToUpdate = searchCursor.NextRow()) != null)
                        {
                            try
                            {
                                string action = String.Empty;
                                if (revActionFieldIndex != -1)
                                {
                                    action = searchRowToUpdate.get_Value(revActionFieldIndex) as string;
                                }
                                string elementType = String.Empty;
                                if (revElementTypeFieldIndex != -1)
                                {
                                    elementType = searchRowToUpdate.get_Value(revElementTypeFieldIndex) as string;
                                }

                                string sourceFCName = String.Empty;
                                if (revFCNameFieldIndex != -1)
                                {
                                    sourceFCName = searchRowToUpdate.get_Value(revFCNameFieldIndex) as string;
                                }

                                long osmOldID = -1;
                                if (revOldIDFieldIndex != -1)
                                {
                                    osmOldID = Convert.ToInt64(searchRowToUpdate.get_Value(revOldIDFieldIndex));
                                }

                                bool isPolygon = false;
                                if (sourceFCName.IndexOf("_osm_ply") > -1)
                                {
                                    isPolygon = true;
                                }

                                // if the overall number of uploaded elements is too big for a single changeset we do need to split it up
                                // into multiple sets
                                if ((featureUpdateCounter % maxElementsinChangeSet) == 0)
                                {
                                    CreateNextChangeSet(message, createChangeSetOSM, userCredentialGPValue.EncodedUserNamePassWord, secondsToTimeout, ref changeSetID, baseURLGPString, ref featureUpdateCounter);
                                }

                                osm createWay = new osm();
                                if (isPolygon == false)
                                {
                                    createWay = CreateOSMWayRepresentation(lineFeatureClass, action, osmOldID, changeSetID, -1, nodeosmIDLookup, pointFeatureClass, pointOSMIDFieldIndex, internalExtensionVersion);
                                }
                                else
                                {
                                    createWay = CreateOSMWayRepresentation(polygonFeatureClass, action, osmOldID, changeSetID, -1, nodeosmIDLookup, pointFeatureClass, pointOSMIDFieldIndex, internalExtensionVersion);
                                }

                                try
                                {
                                    HttpWebRequest httpClient3 = HttpWebRequest.Create(baseURLGPString.Value + "/api/0.6/" + elementType + "/create") as HttpWebRequest;
                                    httpClient3 = OSMGPDownload.AssignProxyandCredentials(httpClient3);
                                    httpClient3.Method = "PUT";
                                    SetBasicAuthHeader(httpClient3, userCredentialGPValue.EncodedUserNamePassWord);
                                    httpClient.Timeout = secondsToTimeout * 1000;

                                    string sContent = OsmRest.SerializeUtils.CreateXmlSerializable(createWay, serializer, Encoding.UTF8, "text/xml");
                                    OsmRest.HttpUtils.Put(httpClient3, sContent);
                                    createWay = null;

                                    httpResponse = null;
                                    httpResponse = httpClient3.GetResponse() as HttpWebResponse;

                                    // track the update/sync requests against the server
                                    featureUpdateCounter = featureUpdateCounter + 1;
                                }
                                catch (Exception ex)
                                {
                                    message.AddError(120009, ex.Message);

                                    if (ex is WebException)
                                    {
                                        updateErrorStatus(message, revStatusFieldIndex, revStatusCodeFieldIndex, revErrorMessageFieldIndex, ref searchRowToUpdate, ex);
                                    }
                                }

                                if (httpResponse != null)
                                {
                                    string newIDString = OsmRest.HttpUtils.GetResponseContent(httpResponse);

                                    wayosmIDLookup.Add(osmOldID, Convert.ToInt64(newIDString));

                                    // update the revision table
                                    if (revNewIDFieldIndex != -1)
                                    {
                                        searchRowToUpdate.set_Value(revNewIDFieldIndex, Convert.ToString(newIDString));
                                    }
                                    if (revVersionFieldIndex != -1)
                                    {
                                        searchRowToUpdate.set_Value(revVersionFieldIndex, 1);
                                    }
                                    if (revStatusFieldIndex != -1)
                                    {
                                        searchRowToUpdate.set_Value(revStatusFieldIndex, httpResponse.StatusCode.ToString());
                                    }
                                    if (revStatusCodeFieldIndex != -1)
                                    {
                                        searchRowToUpdate.set_Value(revStatusCodeFieldIndex, (int)httpResponse.StatusCode);
                                    }
                                    if (revChangeSetIDFieldIndex != -1)
                                    {
                                        searchRowToUpdate.set_Value(revChangeSetIDFieldIndex, Convert.ToString(changeSetID));
                                    }

                                    // update the source line/polygon feature class as well
                                    if (isPolygon == false)
                                    {
                                        updateSource((ITable)lineFeatureClass, action, osmOldID, Convert.ToInt64(newIDString), user_displayname, userID, 1, Convert.ToInt32(changeSetID), nodeosmIDLookup, wayosmIDLookup, relationosmIDLookup, internalExtensionVersion);
                                    }
                                    else
                                    {
                                        updateSource((ITable)polygonFeatureClass, action, osmOldID, Convert.ToInt64(newIDString), user_displayname, userID, 1, Convert.ToInt32(changeSetID), nodeosmIDLookup, wayosmIDLookup, relationosmIDLookup, internalExtensionVersion);
                                    }
                                }
                            }

                            catch (Exception ex)
                            {
                                message.AddError(120009, ex.Message);

                                if (ex is WebException)
                                {
                                    updateErrorStatus(message, revStatusFieldIndex, revStatusCodeFieldIndex, revErrorMessageFieldIndex, ref searchRowToUpdate, ex);
                                }
                            }
                            finally
                            {
                                try
                                {
                                    searchRowToUpdate.Store();
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine(ex.Message);
                                }

                                if (httpResponse != null)
                                {
                                    httpResponse.Close();
                                }
                            }

                            if (TrackCancel.Continue() == false)
                            {
                                closeChangeSet(message, userCredentialGPValue.EncodedUserNamePassWord, secondsToTimeout, changeSetID, baseURLGPString);

                                return;
                            }
                        }
                    }
                    #endregion

                    #region and then create relations
                    queryFilter.WhereClause = "(" + 
                        sqlFormatter.SqlIdentifier("osmstatuscode") + " <> 200 OR "
                        + sqlFormatter.SqlIdentifier("osmstatus") + " IS NULL) AND "
                        + sqlFormatter.SqlIdentifier("osmaction") + " = 'create'  AND "
                        + sqlFormatter.SqlIdentifier("osmelementtype") + " = 'relation'";

                    using (ComReleaser comReleaser = new ComReleaser())
                    {
                        searchCursor = revisionTable.Search(queryFilter, false);
                        comReleaser.ManageLifetime(searchCursor);

                        while ((searchRowToUpdate = searchCursor.NextRow()) != null)
                        {
                            try
                            {

                                string action = String.Empty;
                                if (revActionFieldIndex != -1)
                                {
                                    action = searchRowToUpdate.get_Value(revActionFieldIndex) as string;
                                }
                                string elementType = String.Empty;
                                if (revElementTypeFieldIndex != -1)
                                {
                                    elementType = searchRowToUpdate.get_Value(revElementTypeFieldIndex) as string;
                                }

                                string sourceFCName = String.Empty;
                                if (revFCNameFieldIndex != -1)
                                {
                                    sourceFCName = searchRowToUpdate.get_Value(revFCNameFieldIndex) as string;
                                }

                                long osmOldID = -1;
                                if (revOldIDFieldIndex != -1)
                                {
                                    osmOldID = Convert.ToInt64(searchRowToUpdate.get_Value(revOldIDFieldIndex));
                                }

                                bool isPolygon = false;
                                if (sourceFCName.IndexOf("_osm_ply") > -1)
                                {
                                    isPolygon = true;
                                }

                                // if the overall number of uploaded elements is too big for a single changeset we do need to split it up
                                // into multiple sets
                                if ((featureUpdateCounter % maxElementsinChangeSet) == 0)
                                {
                                    CreateNextChangeSet(message, createChangeSetOSM, userCredentialGPValue.EncodedUserNamePassWord, secondsToTimeout, ref changeSetID, baseURLGPString, ref featureUpdateCounter);
                                }

                                osm createRelation = null;
                                // the relation is acutally multi-part line
                                if (sourceFCName.Contains("_osm_ln"))
                                {
                                    createRelation = CreateOSMRelationRepresentation((ITable)lineFeatureClass, action, osmOldID, changeSetID, -1, nodeosmIDLookup, wayosmIDLookup, relationosmIDLookup, internalExtensionVersion);
                                }
                                else if (sourceFCName.Contains("_osm_ply"))
                                {
                                    createRelation = CreateOSMRelationRepresentation((ITable)polygonFeatureClass, action, osmOldID, changeSetID, -1, nodeosmIDLookup, wayosmIDLookup, relationosmIDLookup, internalExtensionVersion);
                                }
                                else
                                {
                                    createRelation = CreateOSMRelationRepresentation(relationTable, action, osmOldID, changeSetID, -1, nodeosmIDLookup, wayosmIDLookup, relationosmIDLookup, internalExtensionVersion);
                                }
                                try
                                {
                                    HttpWebRequest httpClient4 = HttpWebRequest.Create(baseURLGPString.Value + "/api/0.6/" + elementType + "/create") as HttpWebRequest;
                                    httpClient4 = OSMGPDownload.AssignProxyandCredentials(httpClient4);
                                    SetBasicAuthHeader(httpClient4, userCredentialGPValue.EncodedUserNamePassWord);
                                    httpClient4.Timeout = secondsToTimeout * 1000;
                                    string sContent = OsmRest.SerializeUtils.CreateXmlSerializable(createRelation, serializer, Encoding.UTF8, "text/xml");

                                    OsmRest.HttpUtils.Put(httpClient4, sContent);

                                    httpResponse = null;
                                    httpResponse = httpClient4.GetResponse() as HttpWebResponse;
                                    // track the update/sync requests against the server
                                    featureUpdateCounter = featureUpdateCounter + 1;
                                }
                                catch (Exception ex)
                                {
                                    message.AddError(120009, ex.Message);

                                    if (ex is WebException)
                                    {
                                        updateErrorStatus(message, revStatusFieldIndex, revStatusCodeFieldIndex, revErrorMessageFieldIndex, ref searchRowToUpdate, ex);
                                    }
                                }

                                if (httpResponse != null)
                                {
                                    string newIDString = OsmRest.HttpUtils.GetResponseContent(httpResponse);

                                    relationosmIDLookup.Add(osmOldID, Convert.ToInt64(newIDString));

                                    // update the revision table
                                    if (revNewIDFieldIndex != -1)
                                    {
                                        searchRowToUpdate.set_Value(revNewIDFieldIndex, Convert.ToString(newIDString));
                                    }
                                    if (revVersionFieldIndex != -1)
                                    {
                                        searchRowToUpdate.set_Value(revVersionFieldIndex, 1);
                                    }
                                    if (revStatusFieldIndex != -1)
                                    {
                                        searchRowToUpdate.set_Value(revStatusFieldIndex, httpResponse.StatusCode.ToString());
                                    }
                                    if (revStatusCodeFieldIndex != -1)
                                    {
                                        searchRowToUpdate.set_Value(revStatusCodeFieldIndex, (int)httpResponse.StatusCode);
                                    }
                                    if (revChangeSetIDFieldIndex != -1)
                                    {
                                        searchRowToUpdate.set_Value(revChangeSetIDFieldIndex, Convert.ToInt32(changeSetID));
                                    }

                                    if (sourceFCName.Contains("_osm_ln"))
                                    {
                                        updateSource((ITable)lineFeatureClass, action, osmOldID, Convert.ToInt64(newIDString), user_displayname, userID, 1, Convert.ToInt32(changeSetID), nodeosmIDLookup, wayosmIDLookup, relationosmIDLookup, internalExtensionVersion);
                                    }
                                    else if (sourceFCName.Contains("_osm_ply"))
                                    {
                                        updateSource((ITable)polygonFeatureClass, action, osmOldID, Convert.ToInt64(newIDString), user_displayname, userID, 1, Convert.ToInt32(changeSetID), nodeosmIDLookup, wayosmIDLookup, relationosmIDLookup, internalExtensionVersion);
                                    }
                                    else
                                    {
                                        // update the source table holding the relation information class as well
                                        updateSource(relationTable, action, osmOldID, Convert.ToInt64(newIDString), user_displayname, userID, 1, Convert.ToInt32(changeSetID), nodeosmIDLookup, wayosmIDLookup, relationosmIDLookup, internalExtensionVersion);
                                    }
                                }
                            }

                            catch (Exception ex)
                            {
                                message.AddError(120009, ex.Message);

                                if (ex is WebException)
                                {
                                    updateErrorStatus(message, revStatusFieldIndex, revStatusCodeFieldIndex, revErrorMessageFieldIndex, ref searchRowToUpdate, ex);
                                }
                            }
                            finally
                            {
                                try
                                {
                                    searchRowToUpdate.Store();
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine(ex.Message);
                                }
                            }

                            if (TrackCancel.Continue() == false)
                            {
                                closeChangeSet(message, userCredentialGPValue.EncodedUserNamePassWord, secondsToTimeout, changeSetID, baseURLGPString);
                                return;
                            }
                        }
                    }
                    #endregion

                    #region after that submit the modify node, way, relation
                    using (ComReleaser comReleaser = new ComReleaser())
                    {
                        queryFilter.WhereClause = "(" +
                            sqlFormatter.SqlIdentifier("osmstatuscode") + " <> 200 OR "
                            + sqlFormatter.SqlIdentifier("osmstatus") + " IS NULL) AND "
                            + sqlFormatter.SqlIdentifier("osmaction") + " = 'modify'";

                        searchCursor = revisionTable.Search(queryFilter, false);
                        comReleaser.ManageLifetime(searchCursor);

                        while ((searchRowToUpdate = searchCursor.NextRow()) != null)
                        {
                            try
                            {

                                string action = String.Empty;
                                if (revActionFieldIndex != -1)
                                {
                                    action = searchRowToUpdate.get_Value(revActionFieldIndex) as string;
                                }
                                string elementType = String.Empty;
                                if (revElementTypeFieldIndex != -1)
                                {
                                    elementType = searchRowToUpdate.get_Value(revElementTypeFieldIndex) as string;
                                }

                                string sourceFCName = String.Empty;
                                if (revFCNameFieldIndex != -1)
                                {
                                    sourceFCName = searchRowToUpdate.get_Value(revFCNameFieldIndex) as string;
                                }

                                long osmOldID = -1;
                                if (revOldIDFieldIndex != -1)
                                {
                                    osmOldID = Convert.ToInt64(searchRowToUpdate.get_Value(revOldIDFieldIndex));
                                }

                                // if the overall number of uploaded elements is too big for a single changeset we do need to split it up
                                // into multiple sets
                                if ((featureUpdateCounter % maxElementsinChangeSet) == 0)
                                {
                                    CreateNextChangeSet(message, createChangeSetOSM, userCredentialGPValue.EncodedUserNamePassWord, secondsToTimeout, ref changeSetID, baseURLGPString, ref featureUpdateCounter);
                                }

                                switch (elementType)
                                {
                                    case "node":
                                        #region submit nodes to OSM server
                                        switch (action)
                                        {
                                            case "modify":
                                                long modifyID = -1;

                                                if (revNewIDFieldIndex != -1)
                                                {
                                                    object osmIDValue = searchRowToUpdate.get_Value(revNewIDFieldIndex);

                                                    if (osmIDValue == DBNull.Value)
                                                    {
                                                        osmIDValue = osmOldID;
                                                    }

                                                    try
                                                    {
                                                        modifyID = Convert.ToInt64(osmIDValue);
                                                    }
                                                    catch { }

                                                    // modifies should only happen to osm IDs > 0
                                                    // if that condition is not met let's skip this feature as something is not right
                                                    if (modifyID < 0)
                                                    {
                                                        continue;
                                                    }
                                                }

                                                int osmVersion = -1;
                                                if (revVersionFieldIndex != -1)
                                                {
                                                    osmVersion = Convert.ToInt32(searchRowToUpdate.get_Value(revVersionFieldIndex));
                                                }

                                                try
                                                {
                                                    HttpWebRequest httpClient5 = HttpWebRequest.Create(baseURLGPString.Value + "/api/0.6/" + elementType + "/" + modifyID.ToString()) as HttpWebRequest;
                                                    httpClient5 = OSMGPDownload.AssignProxyandCredentials(httpClient5);
                                                    SetBasicAuthHeader(httpClient5, userCredentialGPValue.EncodedUserNamePassWord);

                                                    osm updateNode = CreateOSMNodeRepresentation(pointFeatureClass, action, modifyID, changeSetID, osmVersion, null, internalExtensionVersion);

                                                    string sContent = OsmRest.SerializeUtils.CreateXmlSerializable(updateNode, serializer, Encoding.UTF8, "text/xml");

                                                    // if the serialized node at this time is a null or an empty string let's continue to the next point
                                                    if (String.IsNullOrEmpty(sContent))
                                                    {
                                                        continue;
                                                    }

                                                    OsmRest.HttpUtils.Put(httpClient5, sContent);

                                                    httpResponse = httpClient5.GetResponse() as HttpWebResponse;

                                                    // track the update/sync requests against the server
                                                    featureUpdateCounter = featureUpdateCounter + 1;

                                                    if (httpResponse != null)
                                                    {
                                                        string newVersionString = OsmRest.HttpUtils.GetResponseContent(httpResponse);

                                                        // update the revision table
                                                        if (revVersionFieldIndex != -1)
                                                        {
                                                            searchRowToUpdate.set_Value(revVersionFieldIndex, Convert.ToString(newVersionString));
                                                        }
                                                        if (revStatusFieldIndex != -1)
                                                        {
                                                            searchRowToUpdate.set_Value(revStatusFieldIndex, httpResponse.StatusCode.ToString());
                                                        }
                                                        if (revStatusCodeFieldIndex != -1)
                                                        {
                                                            searchRowToUpdate.set_Value(revStatusCodeFieldIndex, (int)httpResponse.StatusCode);
                                                        }
                                                        if (revChangeSetIDFieldIndex != -1)
                                                        {
                                                            searchRowToUpdate.set_Value(revChangeSetIDFieldIndex, Convert.ToInt32(changeSetID));
                                                        }

                                                        // for a modify the old id is still the new id
                                                        if (revNewIDFieldIndex != -1 && revOldIDFieldIndex != -1)
                                                        {
                                                            searchRowToUpdate.set_Value(revNewIDFieldIndex, searchRowToUpdate.get_Value(revOldIDFieldIndex));
                                                        }

                                                        // update the source point feature class as well
                                                        updateSource((ITable)pointFeatureClass, action, modifyID, modifyID, user_displayname, userID, Convert.ToInt32(newVersionString), Convert.ToInt32(changeSetID), null, null, null, internalExtensionVersion);

                                                        httpResponse.Close();
                                                    }
                                                }
                                                catch (Exception ex)
                                                {
                                                    message.AddError(120009, ex.Message);

                                                    if (ex is WebException)
                                                    {
                                                        updateErrorStatus(message, revStatusFieldIndex, revStatusCodeFieldIndex, revErrorMessageFieldIndex, ref searchRowToUpdate, ex);
                                                    }
                                                }

                                                break;
                                            case "delete":
                                                // the delete operations are handled separately
                                                break;
                                            default:
                                                break;
                                        }
                                        break;
                                        #endregion
                                    case "way":
                                        #region submit ways to the OSM server
                                        // determine if we have a polygon or a polyline feature class
                                        bool isPolygon = false;
                                        if (sourceFCName.IndexOf("_osm_ply") > -1)
                                        {
                                            isPolygon = true;
                                        }


                                        switch (action)
                                        {
                                            case "modify":

                                                long modifyID = -1;

                                                if (revNewIDFieldIndex != -1)
                                                {
                                                    object osmIDValue = searchRowToUpdate.get_Value(revNewIDFieldIndex);

                                                    if (osmIDValue == DBNull.Value)
                                                    {
                                                        osmIDValue = osmOldID;
                                                    }

                                                    try
                                                    {
                                                        modifyID = Convert.ToInt64(osmIDValue);
                                                    }
                                                    catch { }

                                                    // modifies should only happen to osm IDs > 0
                                                    // if that condition is not met let's skip this feature as something is not right
                                                    if (modifyID < 0)
                                                    {
                                                        continue;
                                                    }
                                                }

                                                int osmVersion = -1;
                                                if (revVersionFieldIndex != -1)
                                                {
                                                    osmVersion = Convert.ToInt32(searchRowToUpdate.get_Value(revVersionFieldIndex));
                                                }

                                                osm updateWay = new osm();
                                                if (isPolygon == false)
                                                {
                                                    updateWay = CreateOSMWayRepresentation(lineFeatureClass, action, modifyID, changeSetID, osmVersion, nodeosmIDLookup, pointFeatureClass, pointOSMIDFieldIndex, internalExtensionVersion);
                                                }
                                                else
                                                {
                                                    updateWay = CreateOSMWayRepresentation(polygonFeatureClass, action, modifyID, changeSetID, osmVersion, nodeosmIDLookup, pointFeatureClass, pointOSMIDFieldIndex, internalExtensionVersion);
                                                }
                                                try
                                                {
                                                    string sContent = OsmRest.SerializeUtils.CreateXmlSerializable(updateWay, serializer, Encoding.UTF8, "text/xml");
                                                    httpClient = HttpWebRequest.Create(baseURLGPString.Value + "/api/0.6/" + elementType + "/" + modifyID.ToString()) as HttpWebRequest;
                                                    httpClient = OSMGPDownload.AssignProxyandCredentials(httpClient);
                                                    SetBasicAuthHeader(httpClient, userCredentialGPValue.EncodedUserNamePassWord);
                                                    OsmRest.HttpUtils.Put(httpClient, sContent);

                                                    httpResponse = httpClient.GetResponse() as HttpWebResponse;

                                                    // track the update/sync requests against the server
                                                    featureUpdateCounter = featureUpdateCounter + 1;

                                                    if (httpResponse != null)
                                                    {
                                                        string newVersionString = OsmRest.HttpUtils.GetResponseContent(httpResponse);

                                                        // update the revision table
                                                        if (revVersionFieldIndex != -1)
                                                        {
                                                            searchRowToUpdate.set_Value(revVersionFieldIndex, Convert.ToString(newVersionString));
                                                        }
                                                        if (revStatusFieldIndex != -1)
                                                        {
                                                            searchRowToUpdate.set_Value(revStatusFieldIndex, httpResponse.StatusCode.ToString());
                                                        }
                                                        if (revStatusCodeFieldIndex != -1)
                                                        {
                                                            searchRowToUpdate.set_Value(revStatusCodeFieldIndex, (int)httpResponse.StatusCode);
                                                        }
                                                        if (revChangeSetIDFieldIndex != -1)
                                                        {
                                                            searchRowToUpdate.set_Value(revChangeSetIDFieldIndex, Convert.ToInt32(changeSetID));
                                                        }

                                                        // for a modify the old id is still the new id
                                                        if (revNewIDFieldIndex != -1 && revOldIDFieldIndex != -1)
                                                        {
                                                            searchRowToUpdate.set_Value(revNewIDFieldIndex, searchRowToUpdate.get_Value(revOldIDFieldIndex));
                                                        }

                                                        // update the source line/polygon feature class as well
                                                        if (isPolygon == false)
                                                        {
                                                            updateSource((ITable)lineFeatureClass, action, modifyID, modifyID, user_displayname, userID, Convert.ToInt32(newVersionString), Convert.ToInt32(changeSetID), nodeosmIDLookup, wayosmIDLookup, relationosmIDLookup, internalExtensionVersion);
                                                        }
                                                        else
                                                        {
                                                            updateSource((ITable)polygonFeatureClass, action, modifyID, modifyID, user_displayname, userID, Convert.ToInt32(newVersionString), Convert.ToInt32(changeSetID), nodeosmIDLookup, wayosmIDLookup, relationosmIDLookup, internalExtensionVersion);
                                                        }

                                                        httpResponse.Close();
                                                    }
                                                }
                                                catch (Exception ex)
                                                {
                                                    message.AddError(120009, ex.Message);

                                                    if (ex is WebException)
                                                    {
                                                        updateErrorStatus(message, revStatusFieldIndex, revStatusCodeFieldIndex, revErrorMessageFieldIndex, ref searchRowToUpdate, ex);
                                                    }
                                                }
                                                break;
                                            case "delete":
                                                // the delete operations are handled separately
                                                break;
                                            default:
                                                break;
                                        }
                                        break;
                                        #endregion
                                    case "relation":
                                        #region submit relations to the OSM server
                                        switch (action)
                                        {
                                            case "create":
                                                break;
                                            case "modify":

                                                long modifyID = -1;

                                                if (revNewIDFieldIndex != -1)
                                                {
                                                    object osmIDValue = searchRowToUpdate.get_Value(revNewIDFieldIndex);

                                                    if (osmIDValue == DBNull.Value)
                                                    {
                                                        osmIDValue = osmOldID;
                                                    }

                                                    try
                                                    {
                                                        modifyID = Convert.ToInt64(osmIDValue);
                                                    }
                                                    catch { }

                                                    // modifies should only happen to osm IDs > 0
                                                    // if that condition is not met let's skip this feature as something is not right
                                                    if (modifyID < 0)
                                                    {
                                                        continue;
                                                    }
                                                }

                                                int osmVersion = -1;
                                                if (revVersionFieldIndex != -1)
                                                {
                                                    osmVersion = Convert.ToInt32(searchRowToUpdate.get_Value(revVersionFieldIndex));
                                                }

                                                osm updateRelation = CreateOSMRelationRepresentation(relationTable, action, modifyID, changeSetID, osmVersion, nodeosmIDLookup, wayosmIDLookup, relationosmIDLookup, internalExtensionVersion);

                                                try
                                                {
                                                    string sContent = OsmRest.SerializeUtils.CreateXmlSerializable(updateRelation, serializer, Encoding.UTF8, "text/xml");
                                                    httpClient = HttpWebRequest.Create(baseURLGPString.Value + "/api/0.6/" + elementType + "/" + modifyID.ToString()) as HttpWebRequest;
                                                    httpClient = OSMGPDownload.AssignProxyandCredentials(httpClient);
                                                    SetBasicAuthHeader(httpClient, userCredentialGPValue.EncodedUserNamePassWord);
                                                    OsmRest.HttpUtils.Put(httpClient, sContent);

                                                    httpResponse = httpClient.GetResponse() as HttpWebResponse;

                                                    // track the update/sync requests against the server
                                                    featureUpdateCounter = featureUpdateCounter + 1;

                                                    if (httpResponse != null)
                                                    {
                                                        string newVersionString = OsmRest.HttpUtils.GetResponseContent(httpResponse);

                                                        // update the revision table
                                                        if (revVersionFieldIndex != -1)
                                                        {
                                                            searchRowToUpdate.set_Value(revVersionFieldIndex, Convert.ToInt32(newVersionString));
                                                        }
                                                        if (revStatusFieldIndex != -1)
                                                        {
                                                            searchRowToUpdate.set_Value(revStatusFieldIndex, httpResponse.StatusCode.ToString());
                                                        }
                                                        if (revStatusCodeFieldIndex != -1)
                                                        {
                                                            searchRowToUpdate.set_Value(revStatusCodeFieldIndex, (int)httpResponse.StatusCode);
                                                        }
                                                        if (revChangeSetIDFieldIndex != -1)
                                                        {
                                                            searchRowToUpdate.set_Value(revChangeSetIDFieldIndex, Convert.ToInt32(changeSetID));
                                                        }

                                                        // for a modify the old id is still the new id
                                                        if (revNewIDFieldIndex != -1 && revOldIDFieldIndex != -1)
                                                        {
                                                            searchRowToUpdate.set_Value(revNewIDFieldIndex, searchRowToUpdate.get_Value(revOldIDFieldIndex));
                                                        }

                                                        // update the source table holding the relation information class as well
                                                        updateSource(relationTable, action, modifyID, modifyID, user_displayname, userID, Convert.ToInt32(newVersionString), Convert.ToInt32(changeSetID), nodeosmIDLookup, wayosmIDLookup, relationosmIDLookup, internalExtensionVersion);

                                                        httpResponse.Close();
                                                    }
                                                }
                                                catch (Exception ex)
                                                {
                                                    message.AddError(120009, ex.Message);

                                                    if (ex is WebException)
                                                    {
                                                        updateErrorStatus(message, revStatusFieldIndex, revStatusCodeFieldIndex, revErrorMessageFieldIndex, ref searchRowToUpdate, ex);
                                                    }
                                                }
                                                break;
                                            case "delete":
                                                // the delete operations are handled separately

                                                break;
                                            default:
                                                break;
                                        }
                                        break;
                                        #endregion
                                    default:
                                        break;
                                }
                            }

                            catch (Exception ex)
                            {
                                message.AddAbort(ex.Message);

                                if (ex is WebException)
                                {
                                    updateErrorStatus(message, revStatusFieldIndex, revStatusCodeFieldIndex, revErrorMessageFieldIndex, ref searchRowToUpdate, ex);
                                }
                            }
                            finally
                            {
                                try
                                {
                                    searchRowToUpdate.Store();
                                }
                                catch (Exception ex)
                                {
                                    System.Diagnostics.Debug.WriteLine(ex.Message);
                                }

                                if (httpResponse != null)
                                {
                                    httpResponse.Close();
                                }
                            }

                            if (TrackCancel.Continue() == false)
                            {
                                closeChangeSet(message, userCredentialGPValue.EncodedUserNamePassWord, secondsToTimeout, changeSetID, baseURLGPString);

                                return;
                            }

                        }
                    }
                    #endregion

                    #region now let's handle the delete in the reverse order - relation first, then ways, and then nodes as the last entity
                    #region delete relations
                    // now let's handle the delete in the reverse order - relation first, then ways, and then nodes as the last entity
                    queryFilter.WhereClause = "("
                        + sqlFormatter.SqlIdentifier("osmstatuscode") + " <> 200 OR "
                        + sqlFormatter.SqlIdentifier("osmstatus") + " IS NULL) AND "
                        + sqlFormatter.SqlIdentifier("osmaction") + " = 'delete' AND "
                        + sqlFormatter.SqlIdentifier("osmelementtype") + " = 'relation'";

                    using (ComReleaser comReleaser = new ComReleaser())
                    {
                        searchCursor = revisionTable.Search(queryFilter, false);
                        comReleaser.ManageLifetime(searchCursor);

                        while ((searchRowToUpdate = searchCursor.NextRow()) != null)
                        {

                            // if the overall number of uploaded elements is too big for a single changeset we do need to split it up
                            // into multiple sets
                            if ((featureUpdateCounter % maxElementsinChangeSet) == 0)
                            {
                                CreateNextChangeSet(message, createChangeSetOSM, userCredentialGPValue.EncodedUserNamePassWord, secondsToTimeout, ref changeSetID, baseURLGPString, ref featureUpdateCounter);
                            }


                            long osmID = -1;
                            if (revOldIDFieldIndex != -1)
                            {
                                osmID = Convert.ToInt64(searchRowToUpdate.get_Value(revOldIDFieldIndex));
                            }
                            int osmVersion = -1;
                            if (revVersionFieldIndex != -1)
                            {
                                osmVersion = Convert.ToInt32(searchRowToUpdate.get_Value(revVersionFieldIndex));
                            }

                            osm deleteRelation = CreateOSMRelationRepresentation(relationTable, "delete", osmID, changeSetID, osmVersion, nodeosmIDLookup, wayosmIDLookup, relationosmIDLookup, internalExtensionVersion);

                            string sContent = OsmRest.SerializeUtils.CreateXmlSerializable(deleteRelation, serializer, Encoding.UTF8, "text/xml");

                            string errorMessage = String.Empty;
                            try
                            {
                                httpResponse = OsmRest.HttpUtils.Delete(baseURLGPString.Value + "/api/0.6/relation/" + Convert.ToString(osmID), sContent, userCredentialGPValue.EncodedUserNamePassWord, secondsToTimeout) as HttpWebResponse;

                                // track the update/sync requests against the server
                                featureUpdateCounter = featureUpdateCounter + 1;

                                if (revStatusFieldIndex != -1)
                                {
                                    searchRowToUpdate.set_Value(revStatusFieldIndex, (int)httpResponse.StatusCode);
                                }
                                if (revStatusCodeFieldIndex != -1)
                                {
                                    searchRowToUpdate.set_Value(revStatusCodeFieldIndex, (int)httpResponse.StatusCode);
                                }

                                if (httpResponse != null)
                                {
                                    httpResponse.Close();
                                }
                            }
                            catch (Exception ex)
                            {
                                message.AddError(120009, ex.Message);

                                if (ex is WebException)
                                {
                                    updateErrorStatus(message, revStatusFieldIndex, revStatusCodeFieldIndex, revErrorMessageFieldIndex, ref searchRowToUpdate, ex);
                                }

                                if (httpResponse != null)
                                {
                                    httpResponse.Close();
                                }
                            }

                            try
                            {
                                searchRowToUpdate.Store();
                            }
                            catch { }

                            if (TrackCancel.Continue() == false)
                            {
                                closeChangeSet(message, userCredentialGPValue.EncodedUserNamePassWord, secondsToTimeout, changeSetID, baseURLGPString);
                                return;
                            }

                        }
                    }

                    #endregion

                    #region handle delete ways
                    queryFilter.WhereClause = "(" 
                        + sqlFormatter.SqlIdentifier("osmstatuscode") + " <> 200 OR "
                        + sqlFormatter.SqlIdentifier("osmstatus") + " IS NULL) AND "
                        + sqlFormatter.SqlIdentifier("osmaction") + " = 'delete' AND "
                        + sqlFormatter.SqlIdentifier("osmelementtype") + " = 'way'";

                    using (ComReleaser comReleaser = new ComReleaser())
                    {
                        searchCursor = revisionTable.Search(queryFilter, false);
                        comReleaser.ManageLifetime(searchCursor);

                        while ((searchRowToUpdate = searchCursor.NextRow()) != null)
                        {

                            // if the overall number of uploaded elements is too big for a single changeset we do need to split it up
                            // into multiple sets
                            if ((featureUpdateCounter % maxElementsinChangeSet) == 0)
                            {
                                CreateNextChangeSet(message, createChangeSetOSM, userCredentialGPValue.EncodedUserNamePassWord, secondsToTimeout, ref changeSetID, baseURLGPString, ref featureUpdateCounter);
                            }

                            long osmID = -1;
                            if (revOldIDFieldIndex != -1)
                            {
                                osmID = Convert.ToInt64(searchRowToUpdate.get_Value(revOldIDFieldIndex));
                            }
                            int osmVersion = -1;
                            if (revVersionFieldIndex != -1)
                            {
                                osmVersion = Convert.ToInt32(searchRowToUpdate.get_Value(revVersionFieldIndex));
                            }

                            osm deleteWay = CreateOSMWayRepresentation(lineFeatureClass, "delete", osmID, changeSetID, osmVersion, wayosmIDLookup, pointFeatureClass, pointOSMIDFieldIndex, internalExtensionVersion);

                            string sContent = OsmRest.SerializeUtils.CreateXmlSerializable(deleteWay, serializer, Encoding.UTF8, "text/xml");

                            try
                            {
                                httpResponse = null;
                                httpResponse = OsmRest.HttpUtils.Delete(baseURLGPString.Value + "/api/0.6/way/" + Convert.ToString(osmID), sContent, userCredentialGPValue.EncodedUserNamePassWord, secondsToTimeout) as HttpWebResponse;

                                // track the update/sync requests against the server
                                featureUpdateCounter = featureUpdateCounter + 1;

                                string errorMessage = String.Empty;
                                // just grab the response and set it on the database 
                                if (revStatusFieldIndex != -1)
                                {
                                    searchRowToUpdate.set_Value(revStatusFieldIndex, (int)httpResponse.StatusCode);
                                }
                                if (revStatusCodeFieldIndex != -1)
                                {
                                    searchRowToUpdate.set_Value(revStatusCodeFieldIndex, (int)httpResponse.StatusCode);
                                }

                                if (httpResponse != null)
                                {
                                    httpResponse.Close();
                                }
                            }

                            catch (Exception ex)
                            {
                                message.AddError(1200009, ex.Message);

                                if (ex is WebException)
                                {
                                    updateErrorStatus(message, revStatusFieldIndex, revStatusCodeFieldIndex, revErrorMessageFieldIndex, ref searchRowToUpdate, ex);
                                }

                                if (httpResponse != null)
                                {
                                    httpResponse.Close();
                                }
                            }

                            try
                            {
                                searchRowToUpdate.Store();
                            }
                            catch { }

                            if (TrackCancel.Continue() == false)
                            {
                                closeChangeSet(message, userCredentialGPValue.EncodedUserNamePassWord, secondsToTimeout, changeSetID, baseURLGPString);
                                return;
                            }
                        }
                    }

                    #endregion

                    #region handle delete points
                    queryFilter.WhereClause = "("
                        + sqlFormatter.SqlIdentifier("osmstatuscode") + " <> 200 OR "
                        + sqlFormatter.SqlIdentifier("osmstatus") + " IS NULL) AND "
                        + sqlFormatter.SqlIdentifier("osmaction") + " = 'delete' AND "
                        + sqlFormatter.SqlIdentifier("osmelementtype") + " = 'node'";

                    using (ComReleaser comReleaser = new ComReleaser())
                    {
                        searchCursor = revisionTable.Search(queryFilter, false);
                        comReleaser.ManageLifetime(searchCursor);

                        while ((searchRowToUpdate = searchCursor.NextRow()) != null)
                        {
                            // if the overall number of uploaded elements is too big for a single changeset we do need to split it up
                            // into multiple sets
                            if ((featureUpdateCounter % maxElementsinChangeSet) == 0)
                            {
                                CreateNextChangeSet(message, createChangeSetOSM, userCredentialGPValue.EncodedUserNamePassWord, secondsToTimeout, ref changeSetID, baseURLGPString, ref featureUpdateCounter);
                            }

                            long osmID = -1;
                            if (revOldIDFieldIndex != -1)
                            {
                                osmID = Convert.ToInt64(searchRowToUpdate.get_Value(revOldIDFieldIndex));
                            }
                            int osmVersion = -1;
                            if (revVersionFieldIndex != -1)
                            {
                                osmVersion = Convert.ToInt32(searchRowToUpdate.get_Value(revVersionFieldIndex));
                            }


                            IPoint deletePoint = null;
                            if (revLongitudeFieldIndex != -1 && revLatitudeFieldIndex != -1)
                            {
                                try
                                {
                                    // let's reconstruct the delete point
                                    deletePoint = new PointClass();
                                    deletePoint.X = Convert.ToDouble(searchRowToUpdate.get_Value(revLongitudeFieldIndex));
                                    deletePoint.Y = Convert.ToDouble(searchRowToUpdate.get_Value(revLatitudeFieldIndex));
                                    deletePoint.SpatialReference = m_wgs84;
                                }
                                catch (Exception ex)
                                {
                                    message.AddWarning(ex.Message);
                                }

                                if (deletePoint == null)
                                {
                                    // inform the about the issue - no successful creation of point and continue on to the next delete instruction
                                    // in the revision table
                                    message.AddWarning(resourceManager.GetString("GPTools_OSMGPUpload_invalidPoint"));
                                    continue;
                                }
                            }

                            osm deleteNode = CreateOSMNodeRepresentation(pointFeatureClass, "delete", osmID, changeSetID, osmVersion, deletePoint, internalExtensionVersion);

                            string sContent = OsmRest.SerializeUtils.CreateXmlSerializable(deleteNode, serializer, Encoding.UTF8, "text/xml");

                            if (String.IsNullOrEmpty(sContent))
                            {
                                continue;
                            }

                            string errorMessage = String.Empty;

                            try
                            {
                                httpResponse = OsmRest.HttpUtils.Delete(baseURLGPString.Value + "/api/0.6/node/" + Convert.ToString(osmID), sContent, userCredentialGPValue.EncodedUserNamePassWord, secondsToTimeout) as HttpWebResponse;

                                if (revStatusFieldIndex != -1)
                                {
                                    searchRowToUpdate.set_Value(revStatusFieldIndex, (int)httpResponse.StatusCode);
                                }

                                if (revStatusCodeFieldIndex != -1)
                                {
                                    searchRowToUpdate.set_Value(revStatusCodeFieldIndex, (int)httpResponse.StatusCode);
                                }

                                if (httpResponse != null)
                                {
                                    httpResponse.Close();
                                }
                            }
                            catch (Exception ex)
                            {
                                message.AddError(120009, ex.Message);

                                if (ex is WebException)
                                {
                                    updateErrorStatus(message, revStatusFieldIndex, revStatusCodeFieldIndex, revErrorMessageFieldIndex, ref searchRowToUpdate, ex);
                                }

                                if (httpResponse != null)
                                {
                                    httpResponse.Close();
                                }

                            }


                            // track the update/sync requests against the server
                            featureUpdateCounter = featureUpdateCounter + 1;

                            try
                            {
                                searchRowToUpdate.Store();
                            }
                            catch { }

                            if (TrackCancel.Continue() == false)
                            {
                                closeChangeSet(message, userCredentialGPValue.EncodedUserNamePassWord, secondsToTimeout, changeSetID, baseURLGPString);
                                return;
                            }
                        }
                    }

                    #endregion
                    #endregion
                    #endregion
                }
            }
            catch (Exception ex)
            {
                message.AddError(120058, ex.Message);
                message.AddError(120058, ex.StackTrace);
            }
            finally
            {
                closeChangeSet(message, userCredentialGPValue.EncodedUserNamePassWord, secondsToTimeout, changeSetID, baseURLGPString);

                if (revisionTable != null)
                {
                    try
                    {
                        ISchemaLock tableSchemaLock = revisionTable as ISchemaLock;

                        if (tableSchemaLock != null)
                        {
                            tableSchemaLock.ChangeSchemaLock(esriSchemaLock.esriSharedSchemaLock);
                        }
                    }
                    catch (Exception eLock)
                    {
                        message.AddError(120059, resourceManager.GetString("GPTools_OSMGPUpload_LockErrorTitle") + eLock.Message);
                    }
                }

                if (revisionTable != null)
                {
                    Marshal.FinalReleaseComObject(revisionTable);
                }

                // if the searchCursor still has a reference somewhere do release it now - and as a result release any remaining table locks
                if (searchCursor != null)
                {
                    Marshal.FinalReleaseComObject(searchCursor);
                }

                gpUtilities3.RemoveInternalData();
                gpUtilities3.ReleaseInternals();

                //Marshal.ReleaseComObject(gpUtilities3);
            }
        }

        private static void updateErrorStatus(ESRI.ArcGIS.Geodatabase.IGPMessages message, int revStatusFieldIndex, int revStatusCodeFieldIndex, int revErrorMessageFieldIndex, ref IRow searchRowToUpdate, Exception ex)
        {
            WebException webException = ex as WebException;

            if (webException == null)
                return;

            if (webException.Response == null)
                return;

            string serverErrorMessage = webException.Response.Headers["Error"];

            if (!String.IsNullOrEmpty(serverErrorMessage))
            {
                message.AddError(120009, serverErrorMessage);
            }

            string serverErrorStatus = webException.Response.Headers["Status"];

            if (revStatusFieldIndex != -1)
            {
                searchRowToUpdate.set_Value(revStatusFieldIndex, serverErrorStatus);
            }
            if (revStatusCodeFieldIndex != -1)
            {
                searchRowToUpdate.set_Value(revStatusCodeFieldIndex, Convert.ToInt32(serverErrorStatus));
            }

            if (searchRowToUpdate.Fields.Field[revErrorMessageFieldIndex].Length < serverErrorMessage.Length)
            {
                serverErrorMessage = serverErrorMessage.Substring(0, searchRowToUpdate.Fields.Field[revErrorMessageFieldIndex].Length);
            }

            if (revErrorMessageFieldIndex != -1)
            {
                searchRowToUpdate.set_Value(revErrorMessageFieldIndex, serverErrorMessage);
            }
        }

        public ESRI.ArcGIS.esriSystem.IName FullName
        {
            get
            {
                IName fullName = null;

                if (osmGPFactory != null)
                {
                    fullName = osmGPFactory.GetFunctionName(OSMGPFactory.m_UploadDataName) as IName;
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
                return "";
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
                string metadafile = "osmgpupload.xml";

                try
                {
                    string[] languageid = System.Threading.Thread.CurrentThread.CurrentUICulture.Name.Split("-".ToCharArray());

                    string ArcGISInstallationLocation = OSMGPFactory.GetArcGIS10InstallLocation();
                    string localizedMetaDataFileShort = ArcGISInstallationLocation + System.IO.Path.DirectorySeparatorChar.ToString() + "help" + System.IO.Path.DirectorySeparatorChar.ToString() + "gp" + System.IO.Path.DirectorySeparatorChar.ToString() + "osmgpupload_" + languageid[0] + ".xml";
                    string localizedMetaDataFileLong = ArcGISInstallationLocation + System.IO.Path.DirectorySeparatorChar.ToString() + "help" + System.IO.Path.DirectorySeparatorChar.ToString() + "gp" + System.IO.Path.DirectorySeparatorChar.ToString() + "osmgpupload_" + System.Threading.Thread.CurrentThread.CurrentUICulture.Name + ".xml";

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
                return OSMGPFactory.m_UploadDataName;
            }
        }

        public ESRI.ArcGIS.esriSystem.IArray ParameterInfo
        {
            get
            {
                IArray parameters = new ArrayClass();

                // upload URL
                IGPParameterEdit3 uploadURLParameter = new GPParameterClass() as IGPParameterEdit3;
                uploadURLParameter.DataType = new GPStringTypeClass();
                uploadURLParameter.Direction = esriGPParameterDirection.esriGPParameterDirectionInput;
                uploadURLParameter.DisplayName = resourceManager.GetString("GPTools_OSMGPUpload_uploadURL_displayName");
                uploadURLParameter.Name = "in_osmURL";
                uploadURLParameter.ParameterType = esriGPParameterType.esriGPParameterTypeRequired;

                IGPString urlDownloadString = new GPStringClass();
                if (m_editorConfigurationSettings != null)
                {
                    if (m_editorConfigurationSettings.ContainsKey("osmbaseurl"))
                    {
                        urlDownloadString.Value = m_editorConfigurationSettings["osmbaseurl"];
                    }
                }

                uploadURLParameter.Value = urlDownloadString as IGPValue;

                in_uploadURLNumber = 0;
                parameters.Add((IGPParameter)uploadURLParameter);

                // table containing the edit changes
                IGPParameterEdit3 changesTableParameter = new GPParameterClass() as IGPParameterEdit3;
                changesTableParameter.DataType = new GPTableViewTypeClass();
                changesTableParameter.Direction = esriGPParameterDirection.esriGPParameterDirectionInput;
                changesTableParameter.DisplayName = resourceManager.GetString("GPTools_OSMGPUpload_inputTable_displayName");
                changesTableParameter.Name = "in_editchanges";
                changesTableParameter.ParameterType = esriGPParameterType.esriGPParameterTypeRequired;
                changesTableParameter.Domain = new GPTablesDomainClass();

                in_changesTablesNumber = 1;
                parameters.Add((IGPParameter)changesTableParameter);


                IGPParameterEdit3 commentParameter = new GPParameterClass() as IGPParameterEdit3;
                commentParameter.DataType = new GPStringTypeClass();
                commentParameter.Direction = esriGPParameterDirection.esriGPParameterDirectionInput;
                commentParameter.DisplayName = resourceManager.GetString("GPTools_OSMGPUpload_comment_displayName");
                commentParameter.Name = "in_uploadcomment";
                commentParameter.ParameterType = esriGPParameterType.esriGPParameterTypeRequired;

                in_uploadCommentNumber = 2;
                parameters.Add((IGPParameter)commentParameter);

                // option to use OSMChange format or individual requests
                IGPParameterEdit3 uploadFormatParameter = new GPParameterClass() as IGPParameterEdit3;
                IGPCodedValueDomain uploadFormatDomain = new GPCodedValueDomainClass();

                IGPBoolean osmChangeFormat = new GPBooleanClass();
                osmChangeFormat.Value = true;
                IGPBoolean singleUploadFormat = new GPBooleanClass();
                singleUploadFormat.Value = false;

                uploadFormatDomain.AddCode((IGPValue)osmChangeFormat, "OSMCHANGE_FORMAT");
                uploadFormatDomain.AddCode((IGPValue)singleUploadFormat, "SINGLE_UPLOAD_FORMAT");
                uploadFormatParameter.Domain = (IGPDomain)uploadFormatDomain;
                uploadFormatParameter.Value = (IGPValue)osmChangeFormat;

                uploadFormatParameter.DataType = new GPBooleanTypeClass();
                uploadFormatParameter.Direction = esriGPParameterDirection.esriGPParameterDirectionInput;
                uploadFormatParameter.ParameterType = esriGPParameterType.esriGPParameterTypeRequired;
                uploadFormatParameter.DisplayName = resourceManager.GetString("GPTools_OSMGPUpload_uploadFormat_displayName");
                uploadFormatParameter.Name = "in_uploadformat";

                in_uploadFormatNumber = 3;
                parameters.Add((IGPParameter)uploadFormatParameter);

                // user name
                IGPParameterEdit3 userNameParameter = new GPParameterClass() as IGPParameterEdit3;
                userNameParameter.DataType = new GPStringTypeClass();
                userNameParameter.Direction = esriGPParameterDirection.esriGPParameterDirectionInput;
                userNameParameter.DisplayName = resourceManager.GetString("GPTools_OSMGPUpload_username");
                // oauthUserNameParameter.Category = resourceManager.GetString("GPTools_OSMGPUpload_oauth_category");
                userNameParameter.Name = "in_osm_username";
                userNameParameter.ParameterType = esriGPParameterType.esriGPParameterTypeRequired;

                in_userNameNumber = 4;
                parameters.Add((IGPParameter)userNameParameter);

                 // password
                IGPParameterEdit3 passwordParameter = new GPParameterClass() as IGPParameterEdit3;
                passwordParameter.DataType = new GPStringHiddenTypeClass();
                passwordParameter.Direction = esriGPParameterDirection.esriGPParameterDirectionInput;
                passwordParameter.DisplayName = resourceManager.GetString("GPTools_OSMGPUpload_password");
                // oauthUserNameParameter.Category = resourceManager.GetString("GPTools_OSMGPUpload_oauth_category");
                passwordParameter.Name = "in_osm_password";
                passwordParameter.ParameterType = esriGPParameterType.esriGPParameterTypeRequired;

                in_passwordNumber = 5;
                parameters.Add((IGPParameter)passwordParameter);
                return parameters;
            }
        }

        public void UpdateMessages(ESRI.ArcGIS.esriSystem.IArray paramvalues, ESRI.ArcGIS.Geoprocessing.IGPEnvironmentManager pEnvMgr, ESRI.ArcGIS.Geodatabase.IGPMessages Messages)
        {
            IGPUtilities3 gpUtilities3 = new GPUtilitiesClass();

            // check for a valid download url
            IGPParameter uploadURLParameter = paramvalues.get_Element(in_uploadURLNumber) as IGPParameter;
            IGPString uploadURLGPString = uploadURLParameter.Value as IGPString;

            if (uploadURLGPString == null)
            {
                Messages.ReplaceError(in_uploadURLNumber, -198, String.Format(resourceManager.GetString("GPTools_NullPointerParameterType"), uploadURLParameter.Value.GetAsText()));
            }
            else
            {
                try
                {
                    if (uploadURLParameter.HasBeenValidated == false)
                    {
                        Uri downloadURI = new Uri(uploadURLGPString.Value);

                        // check base url
                        api osmAPICapabilities = OSMGPDownload.CheckValidServerURL(uploadURLGPString.Value);

                        // if we can construct a valid URI  class then we are accepting the value and store it in the user settings as well
                        if (m_editorConfigurationSettings != null)
                        {
                            if (m_editorConfigurationSettings.ContainsKey("osmbaseurl"))
                            {
                                m_editorConfigurationSettings["osmbaseurl"] = uploadURLGPString.Value;
                            }
                            else
                            {
                                m_editorConfigurationSettings.Add("osmbaseurl", uploadURLGPString.Value);
                            }

                            OSMGPFactory.StoreOSMEditorSettings(m_editorConfigurationSettings);
                        }
                    }
                }
                catch (Exception ex)
                {
                    StringBuilder errorMessage = new StringBuilder();
                    errorMessage.AppendLine(resourceManager.GetString("GPTools_OSMGPUpload_invaliduploadurl"));
                    errorMessage.AppendLine(ex.Message);
                    Messages.ReplaceError(in_uploadURLNumber, -3, errorMessage.ToString());
                }
            }

            IGPParameter revisionTableParameter = paramvalues.get_Element(in_changesTablesNumber) as IGPParameter;
            IGPValue revisionTableGPValue = gpUtilities3.UnpackGPValue(revisionTableParameter);

            if (revisionTableGPValue.IsEmpty())
                return;

            ITable revisionTable = null;
            IQueryFilter revisionTableQueryFilter = null;

            try
            {
                using (ComReleaser comReleaser = new ComReleaser())
                {
                    gpUtilities3.DecodeTableView(revisionTableGPValue, out revisionTable, out revisionTableQueryFilter);
                    comReleaser.ManageLifetime(revisionTable);

                    if (revisionTable is IFeatureClass)
                    {
                        Messages.ReplaceError(in_changesTablesNumber, -4, resourceManager.GetString("GPTools_OSMGPUpload_notarevisiontable"));
                        return;
                    }

                    IDatasetEdit datasetEdit = revisionTable as IDatasetEdit;
                    comReleaser.ManageLifetime(datasetEdit);

                    if (datasetEdit == null)
                    {
                        return;
                    }

                    if (datasetEdit.IsBeingEdited())
                    {
                        Messages.ReplaceError(in_changesTablesNumber, -4, resourceManager.GetString("GPTools_OSMGPUpload_inputnotvalidduringedit"));
                    }
                }

                gpUtilities3.ReleaseInternals();
            }
            catch
            {
                // check if we are dealing with a variable -- if we do then leave this string alone
                string tableName = revisionTableGPValue.GetAsText();
                string tableNameModified = tableName.Replace("%", String.Empty);

                if (((tableName.Length - tableNameModified.Length) % 2) == 0)
                {
                    Messages.Replace(in_changesTablesNumber, new GPMessageClass());
                }
            }
        }

        public void UpdateParameters(ESRI.ArcGIS.esriSystem.IArray paramvalues, ESRI.ArcGIS.Geoprocessing.IGPEnvironmentManager pEnvMgr)
        {
            IGPUtilities3 gpUtilities3 = new GPUtilitiesClass();

            IGPParameter revisionTableParameter = paramvalues.get_Element(in_changesTablesNumber) as IGPParameter;
            IGPValue revisionTableGPValue = gpUtilities3.UnpackGPValue(revisionTableParameter);

            if (revisionTableGPValue.IsEmpty() == false)
            {
                if (gpUtilities3.Exists(revisionTableGPValue) == false)
                {
                    IGPEnvironment workspaceEnvironment = gpUtilities3.GetEnvironment(pEnvMgr.GetEnvironments(), "workspace");
                    IGPValue workspace = workspaceEnvironment.Value;

                    if (workspace.IsEmpty() == false)
                    {
                        string old_locationValue = workspace.GetAsText() + System.IO.Path.DirectorySeparatorChar + revisionTableGPValue.GetAsText();
                        try
                        {
                            string location = gpUtilities3.QualifyOutputCatalogPath(old_locationValue);

                            if (location.Length != old_locationValue.Length)
                            {
                                revisionTableGPValue.SetAsText(location);
                                gpUtilities3.PackGPValue(revisionTableGPValue, revisionTableParameter);
                            }
                        }
                        catch { }
                    }
                }
            }

            gpUtilities3.ReleaseInternals();
        }

        public ESRI.ArcGIS.Geodatabase.IGPMessages Validate(ESRI.ArcGIS.esriSystem.IArray paramvalues, bool updateValues, ESRI.ArcGIS.Geoprocessing.IGPEnvironmentManager envMgr)
        {
            return default(ESRI.ArcGIS.Geodatabase.IGPMessages);
        }
        #endregion

        private void closeChangeSet(ESRI.ArcGIS.Geodatabase.IGPMessages message, string userAuthentication, int timeOut, string changeSetID, IGPString baseURLGPString)
        {
            HttpWebResponse httpResponse = null;

            if (timeOut < 1)
            {
                timeOut = 10;
            }

            // close is only needed if we actually opened a changeset
            if (changeSetID.Equals("-1") == false)
            {
                try
                {
                    HttpWebRequest httpClient = HttpWebRequest.Create(baseURLGPString.Value + "/api/0.6/changeset/" + changeSetID + "/close") as HttpWebRequest;
                    httpClient = OSMGPDownload.AssignProxyandCredentials(httpClient);
                    httpClient.Method = "PUT";

                    SetBasicAuthHeader(httpClient, userAuthentication);
                    httpClient.Timeout = timeOut * 1000;

                    httpResponse = httpClient.GetResponse() as HttpWebResponse;

                    message.AddMessage(String.Format(resourceManager.GetString("GPTools_OSMGPUpload_closeChangeSet"), changeSetID));
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                    if (httpResponse != null)
                    {
                        foreach (var errorItem in httpResponse.Headers.GetValues("Error"))
                        {
                            message.AddError(120009, errorItem);
                        }
                    }
                }
                finally
                {
                    if (httpResponse != null)
                    {
                        httpResponse.Close();
                    }
                }
            }


            return;
        }

        private void ParseResultDiff(diffResult diffResultResponse, ITable revisionTable, IFeatureClass pointFeatureClass, IFeatureClass lineFeatureClass, IFeatureClass polygonFeatureClass, ITable relationTable, string userName, int userID, string changetSetID, Dictionary<long, long> nodeIDLookup, Dictionary<long, long> wayIDLookup, Dictionary<long, long> relationIDLookup, int extensionVersion)
        {
            if (diffResultResponse == null)
                return;

            if (diffResultResponse.Items == null)
                return;

            // let's find all the matching row with the old osm feature ID
            IQueryFilter queryFilter = new QueryFilterClass();

            int sourceFCNameFieldIndex = revisionTable.Fields.FindField("sourcefcname");
            int statusFieldIndex = revisionTable.Fields.FindField("osmstatus");
            int statusCodeFieldIndex = revisionTable.Fields.FindField("osmstatuscode");
            int newIDFieldIndex = revisionTable.Fields.FindField("osmnewid");
            int osmVersionFieldIndex = revisionTable.Fields.FindField("osmversion");
            int changesetIDFieldIndex = revisionTable.Fields.FindField("osmchangeset");

            SQLFormatter sqlFormatter = new SQLFormatter(revisionTable);

            foreach (var item in diffResultResponse.Items)
            {
                if (item is diffResultNode)
                {
                    diffResultNode diffNode = item as diffResultNode;
                    long newID = -1;
                    int newVersion = -1;
                    long oldID = -1;

                    long.TryParse(diffNode.new_id, out newID);
                    int.TryParse(diffNode.new_version, out newVersion);
                    long.TryParse(diffNode.old_id, out oldID);

                    //
                    if (nodeIDLookup.ContainsKey(oldID) == false)
                    {
                        nodeIDLookup.Add(oldID, newID);
                    }

                    updateSource((ITable)pointFeatureClass, determineAction(diffNode), Convert.ToInt64(diffNode.old_id), newID, userName, userID, newVersion, Convert.ToInt32(changetSetID), nodeIDLookup, wayIDLookup, relationIDLookup, extensionVersion);

                    queryFilter.WhereClause = revisionTable.WhereClauseByExtensionVersion(diffNode.old_id, "osmoldid", extensionVersion)
                        + " AND " + sqlFormatter.SqlIdentifier("osmelementtype") + " = 'node'" + " AND " + sqlFormatter.SqlIdentifier("osmstatuscode") + " IS NULL"; ;

                    using (ComReleaser comReleaser = new ComReleaser())
                    {
                        ICursor updateCursor = revisionTable.Update(queryFilter, false);
                        comReleaser.ManageLifetime(updateCursor);

                        IRow currentRow = updateCursor.NextRow();

                        if (currentRow == null)
                            continue;

                        if (statusFieldIndex > -1)
                        {
                            currentRow.set_Value(statusFieldIndex, HttpStatusCode.OK.ToString());
                        }

                        if (statusCodeFieldIndex > -1)
                        {
                            currentRow.set_Value(statusCodeFieldIndex, (int)HttpStatusCode.OK);
                        }

                        if (newIDFieldIndex > -1)
                        {
                            currentRow.set_Value(newIDFieldIndex, newID);
                        }

                        if (osmVersionFieldIndex > -1)
                        {
                            currentRow.set_Value(osmVersionFieldIndex, newVersion);
                        }

                        if (changesetIDFieldIndex > -1)
                        {
                            currentRow.set_Value(changesetIDFieldIndex, changetSetID);
                        }

                        updateCursor.UpdateRow(currentRow);
                    }
                }
                else if (item is diffResultWay)
                {
                    diffResultWay diffWay = item as diffResultWay;
                    queryFilter.WhereClause = revisionTable.WhereClauseByExtensionVersion(diffWay.old_id, "osmoldid", extensionVersion)
                        + " AND " + sqlFormatter.SqlIdentifier("osmelementtype") + " = 'way'" + " AND " + sqlFormatter.SqlIdentifier("osmstatuscode") + " IS NULL";

                    long newID = -1;
                    int newVersion = -1;
                    long oldID = -1;

                    long.TryParse(diffWay.new_id, out newID);
                    int.TryParse(diffWay.new_version, out newVersion);
                    long.TryParse(diffWay.old_id, out oldID);

                    //
                    if (wayIDLookup.ContainsKey(oldID) == false)
                    {
                        wayIDLookup.Add(oldID, newID);
                    }

                    using (ComReleaser comReleaser = new ComReleaser())
                    {
                        ICursor updateCursor = revisionTable.Update(queryFilter, false);
                        comReleaser.ManageLifetime(updateCursor);

                        IRow currentRow = updateCursor.NextRow();

                        if (currentRow == null)
                            continue;

                        string sourceFCName = String.Empty;
                        if (sourceFCNameFieldIndex > -1)
                        {
                            sourceFCName = currentRow.get_Value(sourceFCNameFieldIndex) as string;
                        }

                        if (string.IsNullOrEmpty(sourceFCName))
                            continue;

                        if (sourceFCName.Contains("_osm_ln"))
                        {
                            updateSource((ITable)lineFeatureClass, determineAction(diffWay), Convert.ToInt64(diffWay.old_id), newID, userName, userID, newVersion, Convert.ToInt32(changetSetID), nodeIDLookup, wayIDLookup, relationIDLookup, extensionVersion);
                        }
                        else if (sourceFCName.Contains("_osm_ply"))
                        {
                            updateSource((ITable)polygonFeatureClass, determineAction(diffWay), Convert.ToInt64(diffWay.old_id), newID, userName, userID, newVersion, Convert.ToInt32(changetSetID), nodeIDLookup, wayIDLookup, relationIDLookup, extensionVersion);
                        }

                        if (statusFieldIndex > -1)
                        {
                            currentRow.set_Value(statusFieldIndex, HttpStatusCode.OK.ToString());
                        }

                        if (statusCodeFieldIndex > -1)
                        {
                            currentRow.set_Value(statusCodeFieldIndex, (int)HttpStatusCode.OK);
                        }

                        if (newIDFieldIndex > -1)
                        {
                            currentRow.set_Value(newIDFieldIndex, newID);
                        }

                        if (osmVersionFieldIndex > -1)
                        {
                            currentRow.set_Value(osmVersionFieldIndex, newVersion);
                        }

                        if (changesetIDFieldIndex > -1)
                        {
                            currentRow.set_Value(changesetIDFieldIndex, changetSetID);
                        }

                        updateCursor.UpdateRow(currentRow);
                    }
                }
                else if (item is diffResultRelation)
                {
                    diffResultRelation diffRelation = item as diffResultRelation;
                    queryFilter.WhereClause = revisionTable.WhereClauseByExtensionVersion(diffRelation.old_id, "osmoldid", extensionVersion)
                        + " AND " + sqlFormatter.SqlIdentifier("osmelementtype") + " = 'relation'" + " AND " + sqlFormatter.SqlIdentifier("osmstatuscode") + " IS NULL"; ;

                    long newID = -1;
                    int newVersion = -1;
                    long oldID = -1;

                    long.TryParse(diffRelation.new_id, out newID);
                    int.TryParse(diffRelation.new_version, out newVersion);
                    long.TryParse(diffRelation.old_id, out oldID);

                    //
                    if (relationIDLookup.ContainsKey(oldID) == false)
                    {
                        relationIDLookup.Add(oldID, newID);
                    }

                    using (ComReleaser comReleaser = new ComReleaser())
                    {
                        ICursor updateCursor = revisionTable.Update(queryFilter, false);
                        comReleaser.ManageLifetime(updateCursor);

                        IRow currentRow = updateCursor.NextRow();

                        if (currentRow == null)
                            continue;

                        string sourceFCName = String.Empty;
                        if (sourceFCNameFieldIndex > -1)
                        {
                            sourceFCName = currentRow.get_Value(sourceFCNameFieldIndex) as string;
                        }

                        if (string.IsNullOrEmpty(sourceFCName))
                            continue;

                        if (sourceFCName.Contains("_osm_ln"))
                        {
                            updateSource((ITable)lineFeatureClass, determineAction(diffRelation), Convert.ToInt64(diffRelation.old_id), newID, userName, userID, newVersion, Convert.ToInt32(changetSetID), nodeIDLookup, wayIDLookup, relationIDLookup, extensionVersion);
                        }
                        else if (sourceFCName.Contains("_osm_ply"))
                        {
                            updateSource((ITable)polygonFeatureClass, determineAction(diffRelation), Convert.ToInt64(diffRelation.old_id), newID, userName, userID, newVersion, Convert.ToInt32(changetSetID), nodeIDLookup, wayIDLookup, relationIDLookup, extensionVersion);
                        }
                        else if (sourceFCName.Contains("_osm_relation"))
                        {
                            updateSource(relationTable, determineAction(diffRelation), Convert.ToInt64(diffRelation.old_id), newID, userName, userID, newVersion, Convert.ToInt32(changetSetID), nodeIDLookup, wayIDLookup, relationIDLookup, extensionVersion);
                        }

                        if (statusFieldIndex > -1)
                        {
                            currentRow.set_Value(statusFieldIndex, HttpStatusCode.OK.ToString());
                        }

                        if (statusCodeFieldIndex > -1)
                        {
                            currentRow.set_Value(statusCodeFieldIndex, (int)HttpStatusCode.OK);
                        }

                        if (newIDFieldIndex > -1)
                        {
                            currentRow.set_Value(newIDFieldIndex, newID);
                        }

                        if (osmVersionFieldIndex > -1)
                        {
                            currentRow.set_Value(osmVersionFieldIndex, newVersion);
                        }

                        if (changesetIDFieldIndex > -1)
                        {
                            currentRow.set_Value(changesetIDFieldIndex, changetSetID);
                        }


                        updateCursor.UpdateRow(currentRow);
                    }
                }
            }
        }

        private string determineAction(diffResultNode diffNode)
        {
            string resultAction = String.Empty;

            if (string.IsNullOrEmpty(diffNode.new_version))
            {
                resultAction = "delete";
            }
            else
            {
                if (diffNode.old_id.Equals(diffNode.new_version))
                {
                    resultAction = "modify";
                }
                else
                {
                    resultAction = "create";
                }
            }

            return resultAction;
        }

        private string determineAction(diffResultWay diffWay)
        {
            string resultAction = String.Empty;

            if (string.IsNullOrEmpty(diffWay.new_version))
            {
                resultAction = "delete";
            }
            else
            {
                if (diffWay.old_id.Equals(diffWay.new_version))
                {
                    resultAction = "modify";
                }
                else
                {
                    resultAction = "create";
                }
            }

            return resultAction;
        }

        private string determineAction(diffResultRelation diffRelation)
        {
            string resultAction = String.Empty;

            if (string.IsNullOrEmpty(diffRelation.new_version))
            {
                resultAction = "delete";
            }
            else
            {
                if (diffRelation.old_id.Equals(diffRelation.new_version))
                {
                    resultAction = "modify";
                }
                else
                {
                    resultAction = "create";
                }
            }

            return resultAction;
        }


        /// <summary>
        /// Closes the current changeset and opens a new one
        /// </summary>
        /// <param name="message"></param>
        /// <param name="httpResponse"></param>
        /// <param name="httpClient"></param>
        /// <param name="changeSetID">returns the id of the new changeset</param>
        /// <param name="baseURLGPString"></param>
        /// <param name="featureUpdateCounter"></param>
        /// <param name="httpContent"></param>
        private void CreateNextChangeSet(ESRI.ArcGIS.Geodatabase.IGPMessages message, osm createChangeSetDocument, string authenticationHeader, int secondsToTimeout, ref string changeSetID, IGPString baseURLGPString, ref int featureUpdateCounter)
        {
            // close the existing changeset
            HttpWebResponse httpResponse = null;

            HttpWebRequest httpClient = HttpWebRequest.Create(baseURLGPString.Value + "/api/0.6/changeset/" + changeSetID + "/close") as HttpWebRequest;
            httpClient = OSMGPDownload.AssignProxyandCredentials(httpClient);
            httpClient.Method = "PUT";

            SetBasicAuthHeader(httpClient, authenticationHeader);

            httpClient.Timeout = secondsToTimeout * 1000;

            try
            {
                httpResponse = httpClient.GetResponse() as HttpWebResponse;
                message.AddMessage(String.Format(resourceManager.GetString("GPTools_OSMGPUpload_closeChangeSet"), changeSetID));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
                if (httpResponse != null)
                {
                    foreach (var errorItem in httpResponse.Headers.GetValues("Error"))
                    {
                        message.AddError(120009, errorItem);
                    }
                    httpResponse.Close();
                }

                throw ex;
            }

            featureUpdateCounter = 1;

            // open/create a new changeset
            // ----------------------------------------
            try
            {
                string sData = OsmRest.SerializeUtils.CreateXmlSerializable(createChangeSetDocument, null, Encoding.ASCII, "text/xml");

                httpClient = HttpWebRequest.Create(baseURLGPString.Value + "/api/0.6/changeset/create") as HttpWebRequest;
                httpClient = OSMGPDownload.AssignProxyandCredentials(httpClient);
                httpClient.Method = "PUT";
                SetBasicAuthHeader(httpClient, authenticationHeader);
                httpClient.Timeout = secondsToTimeout * 1000;

                Stream requestStream = httpClient.GetRequestStream();
                StreamWriter mywriter = new StreamWriter(requestStream);

                mywriter.Write(sData);
                mywriter.Close();

                httpResponse = httpClient.GetResponse() as HttpWebResponse;
            }
            catch (Exception ex)
            {
                if (httpResponse != null)
                {
                    if (httpResponse.StatusCode != System.Net.HttpStatusCode.OK)
                    {
                        foreach (var errorItem in httpResponse.Headers.GetValues("Error"))
                        {
                            message.AddError(120009, errorItem);
                        }

                        message.AddError(120009, httpResponse.StatusCode.ToString());
                    }
                    else
                    {
                        message.AddError(120009, ex.Message);
                    }
                    httpResponse.Close();
                }

                throw ex;
            }


            // updated changeset id
            Stream responseStreamclient = httpResponse.GetResponseStream();
            StreamReader myreader = new StreamReader(responseStreamclient);
            changeSetID = myreader.ReadToEnd();

            myreader.Close();
            httpResponse.Close();

            message.AddMessage(String.Format(resourceManager.GetString("GPTools_OSMGPUpload_openChangeSet"), changeSetID));
        }

        private void updateSource(ITable sourceTable, string action, long oldOSMFeatureID, long newOSMFeatureID, string userName, int userID, int osmVersion, int changeSetID, Dictionary<long, long> nodeIDLookup, Dictionary<long, long> wayIDLookup, Dictionary<long, long> relationIDLookup, int extensionVersion)
        {
            IOSMClassExtension osmExtension = sourceTable.Extension as IOSMClassExtension;

            try
            {
                if (osmExtension != null)
                {
                    osmExtension.CanBypassChangeDetection = true;
                }

                // let's find all the matching row with the old osm feature ID
                IQueryFilter queryFilter = new QueryFilterClass();
                queryFilter.WhereClause = sourceTable.WhereClauseByExtensionVersion(oldOSMFeatureID, "osmID", extensionVersion);

                using (ComReleaser comReleaser = new ComReleaser())
                {
                    ICursor updateCursor = sourceTable.Update(queryFilter, false);
                    comReleaser.ManageLifetime(updateCursor);

                    IRow updateRow = updateCursor.NextRow();
                    comReleaser.ManageLifetime(updateRow);

                    int osmIDFieldIndex = sourceTable.Fields.FindField("osmID");
                    int osmUserFieldIndex = sourceTable.Fields.FindField("osmuser");
                    int osmUIDFieldIndex = sourceTable.Fields.FindField("osmuid");
                    int osmVersionFieldIndex = sourceTable.Fields.FindField("osmversion");
                    int osmVisibleFieldIndex = sourceTable.Fields.FindField("osmvisible");
                    int osmChangesetIDFieldIndex = sourceTable.Fields.FindField("osmchangeset");
                    int osmTimeStampFieldIndex = sourceTable.Fields.FindField("osmtimestamp");
                    int osmMembersFieldIndex = sourceTable.FindField("osmMembers");
                    int osmisMemberOfFieldIndex = sourceTable.FindField("osmMemberOf");
                    int trackChangesFieldIndex = sourceTable.FindField("osmTrackChanges");

                    if (updateRow != null)
                    {
                        if (osmIDFieldIndex > -1)
                        {
                            if (extensionVersion == 1)
                                updateRow.set_Value(osmIDFieldIndex, Convert.ToInt32(newOSMFeatureID));
                            else if (extensionVersion == 2)
                                updateRow.set_Value(osmIDFieldIndex, Convert.ToString(newOSMFeatureID));
                        }
                        if (osmUserFieldIndex > -1)
                        {
                            updateRow.set_Value(osmUserFieldIndex, userName);
                        }
                        if (osmUIDFieldIndex > -1)
                        {
                            updateRow.set_Value(osmUIDFieldIndex, userID);
                        }
                        if (osmVisibleFieldIndex > -1)
                        {
                            updateRow.set_Value(osmVisibleFieldIndex, "true");
                        }
                        if (osmChangesetIDFieldIndex > -1)
                        {
                            updateRow.set_Value(osmChangesetIDFieldIndex, changeSetID);
                        }
                        if (osmTimeStampFieldIndex > -1)
                        {
                            updateRow.set_Value(osmTimeStampFieldIndex, DateTime.Now.ToUniversalTime());
                        }
                        if (trackChangesFieldIndex > -1)
                        {
                            updateRow.set_Value(trackChangesFieldIndex, 1);
                        }

                        // if we are dealing with a row with a shape field (a feature), then change the IDs of the geometry as well
                        IFeature updateFeature = updateRow as IFeature;
                        if (updateFeature != null)
                        {
                            if (updateFeature.Shape.IsEmpty == false)
                            {
                                switch (updateFeature.Shape.GeometryType)
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
                                        IPoint featurePoint = updateFeature.Shape as IPoint;
                                        if (extensionVersion == 1)
                                            featurePoint.ID = Convert.ToInt32(newOSMFeatureID);
                                        else if (extensionVersion == 2)
                                            // nothing required as the ObjectID of there referenced feature doesn't change
                                            updateFeature.Shape = featurePoint;
                                        if (featurePoint != null)
                                            ComReleaser.ReleaseCOMObject(featurePoint);
                                        break;
                                    case esriGeometryType.esriGeometryPolygon:
                                        IPointCollection pointCollection = updateFeature.Shape as IPointCollection;
                                        IEnumVertex enumVertex = pointCollection.EnumVertices;
                                        enumVertex.Reset();

                                        IPoint currentPoint = null;
                                        int vertexIndex = -1;
                                        int partIndex = -1;

                                        enumVertex.Next(out currentPoint, out partIndex, out vertexIndex);

                                        while (currentPoint != null)
                                        {
                                            if (extensionVersion == 1)
                                            {
                                                if (nodeIDLookup.ContainsKey(currentPoint.ID))
                                                {
                                                    enumVertex.put_ID(Convert.ToInt32(nodeIDLookup[currentPoint.ID]));
                                                }
                                            }
                                            else if (extensionVersion == 2)
                                            {
                                                // nothing required as the ObjectIDs of referenced features don't change
                                            }
                                            enumVertex.Next(out currentPoint, out partIndex, out vertexIndex);
                                        }
                                        updateFeature.Shape = (IGeometry)pointCollection;

                                        // if we are dealing with a multi-part feature then we will need to referenced members as well

                                        break;
                                    case esriGeometryType.esriGeometryPolyline:
                                        pointCollection = updateFeature.Shape as IPointCollection;
                                        enumVertex = pointCollection.EnumVertices;
                                        enumVertex.Reset();

                                        currentPoint = null;
                                        vertexIndex = -1;
                                        partIndex = -1;

                                        enumVertex.Next(out currentPoint, out partIndex, out vertexIndex);

                                        while (currentPoint != null)
                                        {
                                            if (extensionVersion == 1)
                                            {
                                                if (nodeIDLookup.ContainsKey(currentPoint.ID))
                                                {
                                                    enumVertex.put_ID(Convert.ToInt32(nodeIDLookup[currentPoint.ID]));
                                                }
                                            }
                                            else if (extensionVersion == 2)
                                            {
                                                // nothing required as the ObjectIDs of referenced features don't change
                                            }
                                            enumVertex.Next(out currentPoint, out partIndex, out vertexIndex);
                                        }
                                        updateFeature.Shape = (IGeometry)pointCollection;

                                        // if we are dealing with a multi-part feature then we will need to referenced members as well

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
                        }

                        if (osmisMemberOfFieldIndex > -1)
                        {
                            List<string> isMemberOfList = _osmUtility.retrieveIsMemberOf(updateRow, osmisMemberOfFieldIndex);

                            if (isMemberOfList != null && isMemberOfList.Count > 0)
                            {
                                List<string> newIsMemberOfList = new List<string>();

                                if (((IDataset)sourceTable).Name.Contains("_osm_ln") || ((IDataset)sourceTable).Name.Contains("_osm_ply"))
                                {
                                    // since version 1.1 is only dealing with homogeneous features  - lines/polygons 
                                    // or in other words only ways we only go through the way look up at this point
                                    foreach (string parentString in isMemberOfList)
                                    {
                                        long idToCheck = Convert.ToInt64(parentString);
                                        if (wayIDLookup.ContainsKey(idToCheck))
                                        {
                                            newIsMemberOfList.Add(wayIDLookup[idToCheck].ToString());
                                        }
                                        else
                                        {
                                            newIsMemberOfList.Add(parentString);
                                        }
                                    }
                                }
                                else
                                {
                                    // in case we dealing with a relation the entity is assumed to be a hybrid and we need to check against node, way, and relations
                                    foreach (string parentString in isMemberOfList)
                                    {
                                        long idToCheck = Convert.ToInt64(parentString);
                                        if (wayIDLookup.ContainsKey(idToCheck))
                                        {
                                            idToCheck = wayIDLookup[idToCheck];
                                        }

                                        if (nodeIDLookup.ContainsKey(idToCheck))
                                        {
                                            idToCheck = nodeIDLookup[idToCheck];
                                        }

                                        if (relationIDLookup.ContainsKey(idToCheck))
                                        {
                                            idToCheck = relationIDLookup[idToCheck];
                                        }

                                        newIsMemberOfList.Add(idToCheck.ToString());
                                    }
                                }

                                _osmUtility.insertIsMemberOf(osmisMemberOfFieldIndex, newIsMemberOfList, updateRow);
                            }
                        }

                        if (osmMembersFieldIndex > -1)
                        {
                            member[] members = _osmUtility.retrieveMembers(updateRow, osmMembersFieldIndex);

                            if (members != null)
                            {
                                for (int memberIndex = 0; memberIndex < members.Length; memberIndex++)
                                {
                                    switch (members[memberIndex].type)
                                    {
                                        case memberType.way:
                                            long wayID = Convert.ToInt64(members[memberIndex].@ref);
                                            if (wayIDLookup.ContainsKey(wayID))
                                            {
                                                members[memberIndex].@ref = wayIDLookup[wayID].ToString();
                                            }
                                            break;
                                        case memberType.node:
                                            long nodeID = Convert.ToInt64(members[memberIndex].@ref);
                                            if (nodeIDLookup.ContainsKey(nodeID))
                                            {
                                                members[memberIndex].@ref = nodeIDLookup[nodeID].ToString();
                                            }
                                            break;
                                        case memberType.relation:
                                            long relationID = Convert.ToInt64(members[memberIndex].@ref);
                                            if (relationIDLookup.ContainsKey(relationID))
                                            {
                                                members[memberIndex].@ref = relationIDLookup[relationID].ToString();
                                            }
                                            break;
                                        default:
                                            break;
                                    }
                                }

                                _osmUtility.insertMembers(osmMembersFieldIndex, updateRow, members);
                            }
                        }

                        if (osmVersionFieldIndex > -1)
                        {
                            updateRow.set_Value(osmVersionFieldIndex, osmVersion);
                        }

                        updateCursor.UpdateRow(updateRow);
                    }
                }
            }
            catch { }
            finally
            {
                if (osmExtension != null)
                    osmExtension.CanBypassChangeDetection = false;
            }
        }

        private ESRI.ArcGIS.OSM.OSMClassExtension.osm CreateOSMNodeRepresentation(IFeatureClass pointFeatureClass, string action, long osmID, string osmChangeSetID, int osmVersion, IPoint deletePoint, int extensionVersion)
        {
            ESRI.ArcGIS.OSM.OSMClassExtension.osm nodeOSMPresentation = null;

            node simpleNode = CreateNodeRepresentation(pointFeatureClass, action, osmID, osmChangeSetID, osmVersion, deletePoint, extensionVersion);

            // if the node representation is unsuccessfull then return a null pointer
            if (simpleNode != null)
            {
                nodeOSMPresentation = new ESRI.ArcGIS.OSM.OSMClassExtension.osm();
                nodeOSMPresentation.Items = new object[] { simpleNode };
            }

            return nodeOSMPresentation;
        }

        private ESRI.ArcGIS.OSM.OSMClassExtension.node CreateNodeRepresentation(IFeatureClass pointFeatureClass, string action, long osmID, string osmChangeSetID, int osmVersion, IPoint deletePoint, int extensionVersion)
        {
            node nodeRepresentation = new node();

            // let's find all the rows that have a different status than 200 - meaning success
            IQueryFilter queryFilter = new QueryFilterClass();
            queryFilter.WhereClause = pointFeatureClass.WhereClauseByExtensionVersion(osmID, "OSMID", extensionVersion);

            using (ComReleaser comReleaser = new ComReleaser())
            {
                IFeatureCursor searchCursor = pointFeatureClass.Search(queryFilter, false);
                comReleaser.ManageLifetime(searchCursor);

                IFeature pointFeature = searchCursor.NextFeature();

                int osmTagsFieldIndex = pointFeatureClass.Fields.FindField("osmTags");
                int osmIDFieldIndex = pointFeatureClass.Fields.FindField("osmID");
                int osmUserFieldIndex = pointFeatureClass.Fields.FindField("osmuser");
                int osmUIDFieldIndex = pointFeatureClass.Fields.FindField("osmuid");
                int osmVisibleFieldIndex = pointFeatureClass.Fields.FindField("osmvisible");
                int osmVersionFieldIndex = pointFeatureClass.Fields.FindField("osmversion");

                IPoint pointGeometry = null;

                if (pointFeature != null)
                {
                    switch (action)
                    {
                        case "create":
                            // the newly created node needs to carry the changeset info, the coordinate and the tags
                            nodeRepresentation.changeset = osmChangeSetID;

                            pointGeometry = pointFeature.Shape as IPoint;
                            pointGeometry.Project(m_wgs84);

                            nodeRepresentation.lat = Convert.ToString(pointGeometry.Y, new CultureInfo("en-US"));
                            nodeRepresentation.lon = Convert.ToString(pointGeometry.X, new CultureInfo("en-US"));

                            tag[] tags = null;
                            if (osmTagsFieldIndex > -1)
                            {
                                tags = _osmUtility.retrieveOSMTags((IRow)pointFeature, osmTagsFieldIndex, ((IDataset)pointFeatureClass).Workspace);
                            }

                            List<tag> valueOnlyTags = new List<tag>();

                            for (int index = 0; index < tags.Length; index++)
                            {
                                if (!String.IsNullOrEmpty(tags[index].v))
                                {
                                    valueOnlyTags.Add(tags[index]);
                                }
                            }

                            nodeRepresentation.tag = valueOnlyTags.ToArray();

                            if (osmIDFieldIndex > -1)
                            {
                                nodeRepresentation.id = Convert.ToString(pointFeature.get_Value(osmIDFieldIndex), new CultureInfo("en-US"));
                            }

                            break;
                        case "modify":
                            // for an update the complete (full) node needs to be returned
                            nodeRepresentation.changeset = osmChangeSetID;

                            pointGeometry = pointFeature.Shape as IPoint;
                            pointGeometry.Project(m_wgs84);

                            nodeRepresentation.lat = Convert.ToString(pointGeometry.Y, new CultureInfo("en-US"));
                            nodeRepresentation.lon = Convert.ToString(pointGeometry.X, new CultureInfo("en-US"));

                            if (osmIDFieldIndex > -1)
                            {
                                nodeRepresentation.id = Convert.ToString(pointFeature.get_Value(osmIDFieldIndex), new CultureInfo("en-US"));
                            }

                            if (osmUserFieldIndex > -1)
                            {
                                nodeRepresentation.user = Convert.ToString(pointFeature.get_Value(osmUserFieldIndex));
                            }

                            if (osmUIDFieldIndex > -1)
                            {
                                nodeRepresentation.uid = Convert.ToString(pointFeature.get_Value(osmUIDFieldIndex), new CultureInfo("en-US"));
                            }

                            if (osmVisibleFieldIndex > -1)
                            {
                                try
                                {
                                    nodeRepresentation.visible = (nodeVisible)Enum.Parse(typeof(nodeVisible), Convert.ToString(pointFeature.get_Value(osmVisibleFieldIndex)));
                                }
                                catch
                                {
                                    nodeRepresentation.visible = nodeVisible.@true;
                                }
                            }

                            if (osmVersionFieldIndex > -1)
                            {
                                nodeRepresentation.version = Convert.ToString(pointFeature.get_Value(osmVersionFieldIndex));
                            }

                            tags = null;
                            if (osmTagsFieldIndex > -1)
                            {
                                tags = _osmUtility.retrieveOSMTags((IRow)pointFeature, osmTagsFieldIndex, ((IDataset)pointFeatureClass).Workspace);
                            }

                            valueOnlyTags = new List<tag>();

                            for (int index = 0; index < tags.Length; index++)
                            {
                                if (!String.IsNullOrEmpty(tags[index].v))
                                {
                                    valueOnlyTags.Add(tags[index]);
                                }
                            }

                            nodeRepresentation.tag = valueOnlyTags.ToArray();

                            break;
                        case "delete":

                            nodeRepresentation.changeset = osmChangeSetID;
                            nodeRepresentation.id = Convert.ToString(osmID);
                            nodeRepresentation.version = Convert.ToString(osmVersion);


                            pointGeometry = pointFeature.Shape as IPoint;
                            pointGeometry.Project(m_wgs84);

                            nodeRepresentation.lat = Convert.ToString(pointGeometry.Y, new CultureInfo("en-US"));
                            nodeRepresentation.lon = Convert.ToString(pointGeometry.X, new CultureInfo("en-US"));

                            break;
                        default:
                            break;
                    }
                }
                else
                {
                    if (action.Equals("delete", StringComparison.InvariantCultureIgnoreCase))
                    {
                        nodeRepresentation.changeset = osmChangeSetID;
                        nodeRepresentation.id = Convert.ToString(osmID);
                        nodeRepresentation.version = Convert.ToString(osmVersion);

                        if (deletePoint != null)
                        {
                            deletePoint.Project(m_wgs84);

                            nodeRepresentation.lat = Convert.ToString(deletePoint.Y, new CultureInfo("en-US"));
                            nodeRepresentation.lon = Convert.ToString(deletePoint.X, new CultureInfo("en-US"));
                        }
                        else
                        {
                            nodeRepresentation = null;
                        }
                    }
                }
            }

            return nodeRepresentation;
        }

        private ESRI.ArcGIS.OSM.OSMClassExtension.osm CreateOSMWayRepresentation(IFeatureClass featureClass, string action, long osmID, string changeSetID, int osmVersion, Dictionary<long, long> osmIDLookup, IFeatureClass pointFeatureClass, int pointOSMIDFieldIndex, int extensionVersion)
        {
            osm wayOSMPresentation = new osm();

            wayOSMPresentation.Items = new object[] { CreateWayRepresentation(featureClass, action, osmID, changeSetID, osmVersion, osmIDLookup, pointFeatureClass, pointOSMIDFieldIndex, extensionVersion) };

            return wayOSMPresentation;
        }

        private ESRI.ArcGIS.OSM.OSMClassExtension.way CreateWayRepresentation(IFeatureClass featureClass, string action, long osmID, string changeSetID, int osmVersion, Dictionary<long, long> osmIDLookup, IFeatureClass pointFeatureClass, int pointOSMIDFieldIndex, int extensionVersion)
        {
            way wayRepresentation = new way();

            // let's find all the rows that have a different status than 200 - meaning success
            IQueryFilter queryFilter = new QueryFilterClass();
            queryFilter.WhereClause = featureClass.WhereClauseByExtensionVersion(osmID, "OSMID", extensionVersion);

            using (ComReleaser comReleaser = new ComReleaser())
            {
                IFeatureCursor searchCursor = featureClass.Search(queryFilter, false);
                comReleaser.ManageLifetime(searchCursor);

                IFeature wayFeature = searchCursor.NextFeature();

                int osmTagsFieldIndex = featureClass.Fields.FindField("osmTags");
                int osmIDFieldIndex = featureClass.Fields.FindField("osmID");
                int osmUserFieldIndex = featureClass.Fields.FindField("osmuser");
                int osmUIDFieldIndex = featureClass.Fields.FindField("osmuid");
                int osmVisibleFieldIndex = featureClass.Fields.FindField("osmvisible");
                int osmVersionFieldIndex = featureClass.Fields.FindField("osmversion");

                if (wayFeature != null)
                {
                    switch (action)
                    {
                        case "create":
                            // the newly created node needs to carry the changeset info, the coordinate and the tags
                            wayRepresentation.changeset = changeSetID;

                            tag[] tags = null;
                            if (osmTagsFieldIndex > -1)
                            {
                                tags = _osmUtility.retrieveOSMTags((IRow)wayFeature, osmTagsFieldIndex, ((IDataset)featureClass).Workspace);
                            }

                            List<tag> valueOnlyTags = new List<tag>();

                            for (int index = 0; index < tags.Length; index++)
                            {
                                if (!String.IsNullOrEmpty(tags[index].v))
                                {
                                    valueOnlyTags.Add(tags[index]);
                                }
                            }

                            wayRepresentation.tag = valueOnlyTags.ToArray();

                            if (osmIDFieldIndex > -1)
                            {
                                wayRepresentation.id = osmID.ToString();
                            }

                            List<nd> nodeList = new List<nd>();

                            IPointCollection pointCollection = wayFeature.Shape as IPointCollection;
                            IEnumVertex enumVertex = pointCollection.EnumVertices;
                            enumVertex.Reset();

                            IPoint currentPoint = null;
                            int vertexIndex = -1;
                            int partIndex = -1;
                            bool isStoreRequired = false;

                            enumVertex.Next(out currentPoint, out partIndex, out vertexIndex);

                            while (currentPoint != null)
                            {
                                if (osmIDLookup.ContainsKey(currentPoint.ID))
                                {
                                    if (extensionVersion == 1)
                                    {
                                        enumVertex.put_ID(Convert.ToInt32(osmIDLookup[currentPoint.ID]));
                                        isStoreRequired = true;
                                    }
                                    else if (extensionVersion == 2)
                                    {
                                        // the initial established ObjectIDs don't change
                                    }
                                }

                                nd ndElement = new nd();
                                ndElement.@ref = OSMToolHelper.retrieveNodeID(pointFeatureClass, osmIDFieldIndex, extensionVersion, pointCollection.get_Point(vertexIndex));
                                nodeList.Add(ndElement);

                                enumVertex.Next(out currentPoint, out partIndex, out vertexIndex);
                            }

                            // only do a store operation if the vertex IDs have actually changed
                            if (isStoreRequired)
                            {
                                wayFeature.Shape = (IGeometry)pointCollection;
                                wayFeature.Store();
                            }

                            nodeList = CorrectNodeIDs(nodeList);

                            wayRepresentation.nd = nodeList.ToArray();

                            break;
                        case "modify":
                            // for an update the complete (full) way needs to be returned
                            wayRepresentation.changeset = changeSetID;
                            if (osmIDFieldIndex > -1)
                            {
                                wayRepresentation.id = Convert.ToString(wayFeature.get_Value(osmIDFieldIndex), new CultureInfo("en-US"));
                            }

                            if (osmUserFieldIndex > -1)
                            {
                                wayRepresentation.user = Convert.ToString(wayFeature.get_Value(osmUserFieldIndex));
                            }

                            if (osmUIDFieldIndex > -1)
                            {
                                wayRepresentation.uid = Convert.ToString(wayFeature.get_Value(osmUIDFieldIndex), new CultureInfo("en-US"));
                            }

                            if (osmVisibleFieldIndex > -1)
                            {
                                try
                                {
                                    wayRepresentation.visible = (wayVisible)Enum.Parse(typeof(wayVisible), Convert.ToString(wayFeature.get_Value(osmVisibleFieldIndex)));
                                }
                                catch
                                {
                                    wayRepresentation.visible = wayVisible.@true;
                                }
                            }

                            if (osmVersionFieldIndex > -1)
                            {
                                wayRepresentation.version = Convert.ToString(wayFeature.get_Value(osmVersionFieldIndex));
                            }

                            tags = null;
                            if (osmTagsFieldIndex > -1)
                            {
                                tags = _osmUtility.retrieveOSMTags((IRow)wayFeature, osmTagsFieldIndex, ((IDataset)featureClass).Workspace);
                            }


                            valueOnlyTags = new List<tag>();

                            for (int index = 0; index < tags.Length; index++)
                            {
                                if (!String.IsNullOrEmpty(tags[index].v))
                                {
                                    valueOnlyTags.Add(tags[index]);
                                }
                            }

                            pointCollection = wayFeature.Shape as IPointCollection;
                            enumVertex = pointCollection.EnumVertices;
                            enumVertex.Reset();

                            currentPoint = null;
                            vertexIndex = -1;
                            partIndex = -1;

                            // use flag if we need to call store on the update feature
                            // it is somewhat of a costly  operation and we would like to avoid it if possible
                            isStoreRequired = false;

                            nodeList = new List<nd>();

                            enumVertex.Next(out currentPoint, out partIndex, out vertexIndex);

                            while (currentPoint != null)
                            {
                                if (osmIDLookup.ContainsKey(currentPoint.ID))
                                {
                                    if (extensionVersion == 1)
                                    {
                                        enumVertex.put_ID(Convert.ToInt32(osmIDLookup[currentPoint.ID]));
                                        isStoreRequired = true;
                                    }
                                    else if (extensionVersion == 2)
                                    {
                                        // the ObjectIDs of the referenced features don't change
                                    }
                                }

                                nd ndElement = new nd();
                                ndElement.@ref = OSMToolHelper.retrieveNodeID(pointFeatureClass, osmIDFieldIndex, extensionVersion, pointCollection.get_Point(vertexIndex));
                                nodeList.Add(ndElement);

                                enumVertex.Next(out currentPoint, out partIndex, out vertexIndex);
                            }

                            if (isStoreRequired)
                            {
                                wayFeature.Shape = (IGeometry)pointCollection;
                                wayFeature.Store();
                            }

                            nodeList = CorrectNodeIDs(nodeList);

                            wayRepresentation.nd = nodeList.ToArray();

                            wayRepresentation.tag = valueOnlyTags.ToArray();

                            break;
                        case "delete":

                            wayRepresentation.changeset = changeSetID;
                            wayRepresentation.id = Convert.ToString(osmID);
                            wayRepresentation.version = Convert.ToString(osmVersion);

                            break;
                        default:
                            break;
                    }
                }
                else
                {
                    if (action.Equals("delete", StringComparison.InvariantCultureIgnoreCase))
                    {
                        wayRepresentation.changeset = changeSetID;
                        wayRepresentation.id = Convert.ToString(osmID);
                        wayRepresentation.version = Convert.ToString(osmVersion);
                    }
                }
            }

            return wayRepresentation;

        }

        private List<nd> CorrectNodeIDs(List<nd> nodeList)
        {
            if (nodeList == null)
                return nodeList;

            if (nodeList.Count == 0)
                return nodeList;

            // sanity check for closing geometry
            // if the last vertex has an ID of 0 then somewhere/something called used the close method on a polygon instance
            // in this special case (last node 0) we assign  the ID from the first node to the last node
            if (nodeList[nodeList.Count - 1].@ref == "0")
            {
                nodeList[nodeList.Count - 1] = nodeList[0];
            }

            // there is also the chance of containing an empty string
            if (String.IsNullOrEmpty(nodeList[nodeList.Count - 1].@ref))
            {
                nodeList[nodeList.Count - 1] = nodeList[0];
            }

            return nodeList;
        }

        /// <summary>
        /// This function assembles a version of the logged relation action from the revision table in the OsmChange format http://wiki.openstreetmap.org/wiki/OsmChange. 
        /// </summary>
        /// <param name="relationTable"></param>
        /// <param name="action"></param>
        /// <param name="osmID"></param>
        /// <param name="changeSetID"></param>
        /// <param name="osmVersion"></param>
        /// <param name="nodeosmIDLookup"></param>
        /// <param name="wayosmIDLookup"></param>
        /// <param name="relationosmIDLookup"></param>
        /// <param name="extensionVersion"></param>
        /// <returns></returns>
        private ESRI.ArcGIS.OSM.OSMClassExtension.osm CreateOSMRelationRepresentation(ITable relationTable, string action, long osmID, string changeSetID, int osmVersion, Dictionary<long, long> nodeosmIDLookup, Dictionary<long, long> wayosmIDLookup, Dictionary<long, long> relationosmIDLookup, int extensionVersion)
        {
            ESRI.ArcGIS.OSM.OSMClassExtension.osm relationOSMPresentation = new ESRI.ArcGIS.OSM.OSMClassExtension.osm();

            relationOSMPresentation.Items = new object[] { CreateRelationRepresentation(relationTable, action, osmID, changeSetID, osmVersion, nodeosmIDLookup, wayosmIDLookup, relationosmIDLookup, extensionVersion) };

            return relationOSMPresentation;
        }

        /// <summary>
        /// This function assembles a version of the logged relation action from the revision table in the OsmChange format http://wiki.openstreetmap.org/wiki/OsmChange
        /// </summary>
        /// <param name="relationTable"></param>
        /// <param name="action"></param>
        /// <param name="osmID"></param>
        /// <param name="changeSetID"></param>
        /// <param name="osmVersion"></param>
        /// <param name="nodeosmIDLookup"></param>
        /// <param name="wayosmIDLookup"></param>
        /// <param name="relationosmIDLookup"></param>
        /// <param name="extensionVersion"></param>
        /// <returns></returns>
        private ESRI.ArcGIS.OSM.OSMClassExtension.relation CreateRelationRepresentation(ITable relationTable, string action, long osmID, string changeSetID, int osmVersion, Dictionary<long, long> nodeosmIDLookup, Dictionary<long, long> wayosmIDLookup, Dictionary<long, long> relationosmIDLookup, int extensionVersion)
        {
            ESRI.ArcGIS.OSM.OSMClassExtension.relation relationRepresentation = new ESRI.ArcGIS.OSM.OSMClassExtension.relation();

            // let's find all the rows that have a different status than 200 - meaning success
            IQueryFilter queryFilter = new QueryFilterClass();
            queryFilter.WhereClause = relationTable.WhereClauseByExtensionVersion(osmID, "OSMID", extensionVersion);

            using (ComReleaser comReleaser = new ComReleaser())
            {
                ICursor searchCursor = relationTable.Search(queryFilter, false);
                comReleaser.ManageLifetime(searchCursor);

                IRow relationRow = searchCursor.NextRow();

                int osmTagsFieldIndex = relationTable.Fields.FindField("osmTags");
                int osmIDFieldIndex = relationTable.Fields.FindField("osmID");
                int osmUserFieldIndex = relationTable.Fields.FindField("osmuser");
                int osmUIDFieldIndex = relationTable.Fields.FindField("osmuid");
                int osmVisibleFieldIndex = relationTable.Fields.FindField("osmvisible");
                int osmVersionFieldIndex = relationTable.Fields.FindField("osmversion");
                int osmMembersFieldIndex = relationTable.Fields.FindField("osmMembers");

                if (relationRow != null)
                {
                    switch (action)
                    {
                        case "create":
                            // the newly created node needs to carry the changeset info, the coordinate and the tags
                            relationRepresentation.changeset = changeSetID;

                            ESRI.ArcGIS.OSM.OSMClassExtension.tag[] tags = null;
                            if (osmTagsFieldIndex > -1)
                            {
                                tags = _osmUtility.retrieveOSMTags(relationRow, osmTagsFieldIndex, ((IDataset)relationTable).Workspace);
                            }

                            List<tag> valueOnlyTags = new List<tag>();

                            for (int index = 0; index < tags.Length; index++)
                            {
                                if (!String.IsNullOrEmpty(tags[index].v))
                                {
                                    valueOnlyTags.Add(tags[index]);
                                }
                            }

                            if (osmIDFieldIndex > -1)
                            {
                                relationRepresentation.id = osmID.ToString();
                            }

                            ESRI.ArcGIS.OSM.OSMClassExtension.member[] members = null;
                            if (osmMembersFieldIndex > -1)
                            {
                                members = _osmUtility.retrieveMembers(relationRow, osmMembersFieldIndex);

                                // run the member ids through the lookup table
                                for (int memberIndex = 0; memberIndex < members.Length; memberIndex++)
                                {
                                    switch (members[memberIndex].type)
                                    {
                                        case ESRI.ArcGIS.OSM.OSMClassExtension.memberType.way:
                                            if (wayosmIDLookup.ContainsKey(Convert.ToInt64(members[memberIndex].@ref)))
                                            {
                                                members[memberIndex].@ref = Convert.ToString(wayosmIDLookup[Convert.ToInt64(members[memberIndex].@ref)]);
                                            }
                                            break;
                                        case ESRI.ArcGIS.OSM.OSMClassExtension.memberType.node:
                                            if (nodeosmIDLookup.ContainsKey(Convert.ToInt64(members[memberIndex].@ref)))
                                            {
                                                members[memberIndex].@ref = Convert.ToString(nodeosmIDLookup[Convert.ToInt64(members[memberIndex].@ref)]);
                                            }
                                            break;
                                        case ESRI.ArcGIS.OSM.OSMClassExtension.memberType.relation:
                                            if (relationosmIDLookup.ContainsKey(Convert.ToInt64(members[memberIndex].@ref)))
                                            {
                                                members[memberIndex].@ref = Convert.ToString(relationosmIDLookup[Convert.ToInt64(members[memberIndex].@ref)]);
                                            }
                                            break;
                                        default:
                                            break;
                                    }
                                }

                                _osmUtility.insertMembers(osmMembersFieldIndex, relationRow, members);
                            }

                            // add the member and the tags to the relation element
                            List<object> relationItems = new List<object>();
                            relationItems.AddRange(members);
                            relationItems.AddRange(valueOnlyTags.ToArray());

                            relationRepresentation.Items = relationItems.ToArray();

                            break;
                        case "modify":
                            // for an update the complete (full) relation needs to be returned
                            relationRepresentation.changeset = changeSetID;
                            if (osmIDFieldIndex > -1)
                            {
                                relationRepresentation.id = Convert.ToString(relationRow.get_Value(osmIDFieldIndex), new CultureInfo("en-US"));
                            }

                            if (osmUserFieldIndex > -1)
                            {
                                relationRepresentation.user = Convert.ToString(relationRow.get_Value(osmUserFieldIndex));
                            }

                            if (osmUIDFieldIndex > -1)
                            {
                                relationRepresentation.uid = Convert.ToString(relationRow.get_Value(osmUIDFieldIndex), new CultureInfo("en-US"));
                            }

                            if (osmVersionFieldIndex > -1)
                            {
                                relationRepresentation.version = Convert.ToString(relationRow.get_Value(osmVersionFieldIndex));
                            }

                            tags = null;
                            if (osmTagsFieldIndex > -1)
                            {
                                tags = _osmUtility.retrieveOSMTags((IRow)relationRow, osmTagsFieldIndex, ((IDataset)relationTable).Workspace);
                            }

                            valueOnlyTags = new List<tag>();

                            for (int index = 0; index < tags.Length; index++)
                            {
                                if (!String.IsNullOrEmpty(tags[index].v))
                                {
                                    valueOnlyTags.Add(tags[index]);
                                }
                            }

                            members = null;
                            if (osmMembersFieldIndex > -1)
                            {
                                members = _osmUtility.retrieveMembers(relationRow, osmMembersFieldIndex);

                                // run the member ids through the lookup table
                                for (int memberIndex = 0; memberIndex < members.Length; memberIndex++)
                                {
                                    switch (members[memberIndex].type)
                                    {
                                        case memberType.way:
                                            if (wayosmIDLookup.ContainsKey(Convert.ToInt64(members[memberIndex].@ref)))
                                            {
                                                members[memberIndex].@ref = Convert.ToString(wayosmIDLookup[Convert.ToInt64(members[memberIndex].@ref)]);
                                            }
                                            break;
                                        case memberType.node:
                                            if (nodeosmIDLookup.ContainsKey(Convert.ToInt64(members[memberIndex].@ref)))
                                            {
                                                members[memberIndex].@ref = Convert.ToString(nodeosmIDLookup[Convert.ToInt64(members[memberIndex].@ref)]);
                                            }
                                            break;
                                        case memberType.relation:
                                            if (relationosmIDLookup.ContainsKey(Convert.ToInt64(members[memberIndex].@ref)))
                                            {
                                                members[memberIndex].@ref = Convert.ToString(relationosmIDLookup[Convert.ToInt64(members[memberIndex].@ref)]);
                                            }
                                            break;
                                        default:
                                            break;
                                    }
                                }

                                _osmUtility.insertMembers(osmMembersFieldIndex, relationRow, members);
                            }

                            // add the member and the tags to the relation element
                            relationItems = new List<object>();
                            relationItems.AddRange(valueOnlyTags.ToArray());
                            relationItems.AddRange(members);

                            relationRepresentation.Items = relationItems.ToArray();

                            break;
                        case "delete":

                            relationRepresentation.changeset = changeSetID;
                            relationRepresentation.id = Convert.ToString(osmID);
                            relationRepresentation.version = Convert.ToString(osmVersion);

                            break;
                        default:
                            break;
                    }
                }
                else
                {
                    if (action.Equals("delete", StringComparison.InvariantCultureIgnoreCase))
                    {
                        relationRepresentation.changeset = changeSetID;
                        relationRepresentation.id = Convert.ToString(osmID);
                        relationRepresentation.version = Convert.ToString(osmVersion);
                    }
                }
            }

            return relationRepresentation;
        }

        private void SetBasicAuthHeader(HttpWebRequest requestHeaders, string loginAuthentication)
        {
            string authInfo = loginAuthentication;

            if (requestHeaders == null)
                return;

            if (requestHeaders.Headers == null)
                return;

            string authHeader = requestHeaders.Headers.Get("Authorization");

            if (String.IsNullOrEmpty(authHeader))
                requestHeaders.Headers.Add("Authorization", "Basic " + authInfo);
        }

        /// <summary>
        /// To convert a Byte Array of Unicode values (UTF-8 encoded) to a complete String.
        /// </summary>
        /// <param name="characters">Unicode Byte Array to be converted to String</param>
        /// <returns>String converted from Unicode Byte Array</returns>
        private String UTF8ByteArrayToString(Byte[] characters)
        {
            UTF8Encoding encoding = new UTF8Encoding();
            String constructedString = encoding.GetString(characters);
            return (constructedString);
        }
    }
}
