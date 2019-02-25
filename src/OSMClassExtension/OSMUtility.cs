// (c) Copyright Esri, 2010 - 2016
// This source is subject to the Apache 2.0 License.
// Please see http://www.apache.org/licenses/LICENSE-2.0.html for details.
// All other rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.OSM.OSMClassExtension;
using System.IO;
using ESRI.ArcGIS.esriSystem;
using System.Xml;
using System.Xml.Serialization;
using System.Xml.Linq;
using ESRI.ArcGIS.Geometry;
using System.Runtime.InteropServices;

namespace ESRI.ArcGIS.OSM.OSMClassExtension
{
    [Guid("50E9A9C0-6401-4E34-A2E4-AF3A35E489E6")]
    [ComVisible(false)]
    [ProgId("OSMEditor.OSMUtility")]
    public class OSMUtility
    {
        private XmlSerializer _tagSerializer = null;
        private XmlSerializer _memberSerializer = null;
        private XmlSerializer _listStringSerializer = null;
        UTF8Encoding _encoding = null;


        public OSMUtility()
        {
            _tagSerializer = new XmlSerializer(typeof(tag[]));
            _memberSerializer = new XmlSerializer(typeof(member[]));
            _listStringSerializer = new XmlSerializer(typeof(List<string>));
            _encoding = new UTF8Encoding();
        }

        ~OSMUtility()
        {
            if (_tagSerializer != null)
                _tagSerializer = null;

            if (_memberSerializer != null)
                _memberSerializer = null;

            if (_listStringSerializer != null)
                _listStringSerializer = null;

            System.GC.Collect();
        }

        public void insertOSMDocumentIntoCollection(osm osmDocument, Dictionary<string, node> currentNodeList, Dictionary<string, way> currentWayList, Dictionary<string, relation> currentRelationList)
        {
            if (osmDocument.Items != null)
            {
                foreach (var item in osmDocument.Items)
                {
                    if (item is node)
                    {
                        if (currentNodeList != null)
                        {
                            if (currentNodeList.ContainsKey(((node)item).id) == false)
                            {
                                currentNodeList.Add(((node)item).id, item as node);
                            }
                        }
                    }
                    else if (item is way)
                    {
                        if (currentWayList != null)
                        {
                            if (currentWayList.ContainsKey(((way)item).id) == false)
                            {
                                currentWayList.Add(((way)item).id, item as way);
                            }
                        }
                    }
                    else if (item is relation)
                    {
                        if (currentRelationList != null)
                        {
                            if (currentRelationList.ContainsKey(((relation)item).id) == false)
                            {
                                currentRelationList.Add(((relation)item).id, item as relation);
                            }
                        }
                    }
                }
            }
        }

        public void insertMembers(int osmMembersRelationFieldIndex, ref IRow insertRow, member[] relationMembers)
        {
            if (insertRow.Fields.get_Field(osmMembersRelationFieldIndex).Type == esriFieldType.esriFieldTypeBlob)
            {
                byte[] membersBuffer = XmlSerializeMembersBuffer(relationMembers);
                using (System.IO.MemoryStream memoryStream = new MemoryStream(membersBuffer))
                {
                    IMemoryBlobStreamVariant memStream = new MemoryBlobStreamClass() as IMemoryBlobStreamVariant;
                    memStream.ImportFromVariant(memoryStream.ToArray());

                    try
                    {
                        insertRow.set_Value(osmMembersRelationFieldIndex, memStream);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(ex.Message);
                        System.Diagnostics.Debug.WriteLine(ex.StackTrace);
                    }
                }
            }
            else if (insertRow.Fields.get_Field(osmMembersRelationFieldIndex).Type == esriFieldType.esriFieldTypeXML)
            {
                string membersAsString = XmlSerializeMembers(relationMembers);
                byte[] xmlAsBytes = UTF8Encoding.UTF8.GetBytes(membersAsString);
                insertRow.set_Value(osmMembersRelationFieldIndex, xmlAsBytes);
            }
        }

        public void insertMembers(int osmMembersRelationFieldIndex, IRowBuffer rowBuffer, member[] relationMembers)
        {
            if (rowBuffer.Fields.get_Field(osmMembersRelationFieldIndex).Type == esriFieldType.esriFieldTypeBlob)
            {
                byte[] membersBuffer = XmlSerializeMembersBuffer(relationMembers);
                using (System.IO.MemoryStream memoryStream = new MemoryStream(membersBuffer))
                {
                    IMemoryBlobStreamVariant memStream = new MemoryBlobStreamClass() as IMemoryBlobStreamVariant;
                    memStream.ImportFromVariant(memoryStream.ToArray());

                    try
                    {
                        rowBuffer.set_Value(osmMembersRelationFieldIndex, memStream);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(ex.Message);
                        System.Diagnostics.Debug.WriteLine(ex.StackTrace);
                    }
                }
            }
            else if (rowBuffer.Fields.get_Field(osmMembersRelationFieldIndex).Type == esriFieldType.esriFieldTypeXML)
            {
                string membersAsString = XmlSerializeMembers(relationMembers);
                byte[] xmlAsBytes = UTF8Encoding.UTF8.GetBytes(membersAsString);
                rowBuffer.set_Value(osmMembersRelationFieldIndex, xmlAsBytes);
            }
        }


        public void insertIsMemberOf(int isMemberOfFieldIndex, List<string> memberOfList, IRowBuffer rowBuffer)
        {
            if (rowBuffer.Fields.get_Field(isMemberOfFieldIndex).Type == esriFieldType.esriFieldTypeBlob)
            {
                byte[] relationBuffer = XmlSerializeIsMemberOfBuffer(memberOfList);
                using (System.IO.MemoryStream memoryStream = new MemoryStream(relationBuffer))
                {
                    IMemoryBlobStreamVariant memStream = new MemoryBlobStreamClass() as IMemoryBlobStreamVariant;
                    memStream.ImportFromVariant(memoryStream.ToArray());

                    try
                    {
                        rowBuffer.set_Value(isMemberOfFieldIndex, memStream);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(ex.Message);
                        System.Diagnostics.Debug.WriteLine(ex.StackTrace);
                    }
                }
            }
            else if (rowBuffer.Fields.get_Field(isMemberOfFieldIndex).Type == esriFieldType.esriFieldTypeXML)
            {
                string membersAsString = XmlSerializeIsMemberOf(memberOfList);
                byte[] xmlAsBytes = UTF8Encoding.UTF8.GetBytes(membersAsString);
                rowBuffer.set_Value(isMemberOfFieldIndex, xmlAsBytes);
            }
        }

        public List<string> retrieveIsMemberOf(IRow row, int osmIsMemberOfFieldIndex)
        {
            List<string> isMemberOfList = new List<string>();

            if (row.Fields.get_Field(osmIsMemberOfFieldIndex).Type == esriFieldType.esriFieldTypeBlob)
            {
                IMemoryBlobStreamVariant memStream = row.get_Value(osmIsMemberOfFieldIndex) as IMemoryBlobStreamVariant;

                if (memStream != null)
                {
                    try
                    {
                        System.Object memObject;
                        memStream.ExportToVariant(out memObject);

                        MemoryStream newMemStream = new MemoryStream((byte[])memObject);
                        isMemberOfList = DeserializeIsMemberOfBuffer(newMemStream.ToArray());

                        newMemStream.Close();
                        newMemStream.Dispose();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(ex.Message);
                        System.Diagnostics.Debug.WriteLine(ex.StackTrace);
                    }
                }
            }
            else if (row.Fields.get_Field(osmIsMemberOfFieldIndex).Type == esriFieldType.esriFieldTypeXML)
            {
                try
                {
                    byte[] xmlisMemberOFBytes = row.get_Value(osmIsMemberOfFieldIndex) as byte[];
                    string isMembersOfAsString = Encoding.UTF8.GetString(xmlisMemberOFBytes);
                    isMemberOfList = DeserializeIsMemberOf(isMembersOfAsString);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                    System.Diagnostics.Debug.WriteLine(ex.StackTrace);
                }
            }

            return isMemberOfList;
        }

        public member[] retrieveMembers(IRow row, int osmMembersFieldIndex)
        {
            member[] members = new member[0];

            if (row.Fields.get_Field(osmMembersFieldIndex).Type == esriFieldType.esriFieldTypeBlob)
            {
                IMemoryBlobStreamVariant memStream = row.get_Value(osmMembersFieldIndex) as IMemoryBlobStreamVariant;

                if (memStream != null)
                {
                    try
                    {
                        System.Object memObject;
                        memStream.ExportToVariant(out memObject);

                        MemoryStream newMemStream = new MemoryStream((byte[])memObject);
                        members = DeserializeMembersBuffer(newMemStream.ToArray());

                        newMemStream.Close();
                        newMemStream.Dispose();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(ex.Message);
                        System.Diagnostics.Debug.WriteLine(ex.StackTrace);
                    }
                }
            }
            else if (row.Fields.get_Field(osmMembersFieldIndex).Type == esriFieldType.esriFieldTypeXML)
            {
                try
                {
                    byte[] xmlMemberBytes = row.get_Value(osmMembersFieldIndex) as byte[];
                    string membersAsString = Encoding.UTF8.GetString(xmlMemberBytes);
                    members = DeserializeMembers(membersAsString);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                    System.Diagnostics.Debug.WriteLine(ex.StackTrace);
                }
            }

            return members;
        }

        public member[] retrieveOriginalMembers(IRow row, int osmMembersFieldIndex)
        {
            member[] members = new member[0];

            IRowChanges rowChanges = row as IRowChanges;

            if (rowChanges == null)
            {
                return members;
            }

            if (row.Fields.get_Field(osmMembersFieldIndex).Type == esriFieldType.esriFieldTypeBlob)
            {
                IMemoryBlobStreamVariant memStream = rowChanges.get_OriginalValue(osmMembersFieldIndex) as IMemoryBlobStreamVariant;

                if (memStream != null)
                {
                    try
                    {
                        System.Object memObject;
                        memStream.ExportToVariant(out memObject);

                        MemoryStream newMemStream = new MemoryStream((byte[])memObject);
                        members = DeserializeMembersBuffer(newMemStream.ToArray());

                        newMemStream.Close();
                        newMemStream.Dispose();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(ex.Message);
                        System.Diagnostics.Debug.WriteLine(ex.StackTrace);
                    }
                }
            }
            else if (row.Fields.get_Field(osmMembersFieldIndex).Type == esriFieldType.esriFieldTypeXML)
            {
                try
                {
                    byte[] xmlMemberBytes = rowChanges.get_OriginalValue(osmMembersFieldIndex) as byte[];
                    string membersAsString = Encoding.UTF8.GetString(xmlMemberBytes);
                    members = DeserializeMembers(membersAsString);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                    System.Diagnostics.Debug.WriteLine(ex.StackTrace);
                }
            }

            return members;
        }

        public tag[] retrieveOSMTags(IRow row, int osmTagFieldIndex, IWorkspace workspace)
        {
            tag[] retrievedTags = null;

            // if the row itself is a null pointer return an empty tag array
            if (row == null)
            {
                return retrievedTags;
            }

            // if there is no valid field index return an emptry tag array
            if (osmTagFieldIndex == -1)
            {
                return retrievedTags;
            }

            if (row.Fields.get_Field(osmTagFieldIndex).Type == esriFieldType.esriFieldTypeBlob)
            {
                IMemoryBlobStreamVariant memStream = row.get_Value(osmTagFieldIndex) as IMemoryBlobStreamVariant;

                if (memStream != null)
                {
                    try
                    {
                        System.Object memObject;
                        memStream.ExportToVariant(out memObject);

                        MemoryStream newMemStream = new MemoryStream((byte[])memObject);
                        retrievedTags = DeserializeTagCollectionBuffer(newMemStream.ToArray());

                        newMemStream.Close();
                        newMemStream.Dispose();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(ex.Message);
                        System.Diagnostics.Debug.WriteLine(ex.StackTrace);
                    }
                }
            }
            else if (row.Fields.get_Field(osmTagFieldIndex).Type == esriFieldType.esriFieldTypeXML)
            {
                try
                {
                    string tagsString = row.get_Value(osmTagFieldIndex) as string;
                    retrievedTags = DeserializeTagCollection(tagsString);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(ex.Message);
                    System.Diagnostics.Debug.WriteLine(ex.StackTrace);
                }
            }

            // next let's go through the fields with a domain table and check for additional key/value information that might not have been 
            // persisted in the tag yet - 
            // this is only the case for explicitly stored geometries as in point, lines, polygons 
            // relations are treated differently aa they representing a sort of hybrid feature type
            IFeature feature = row as IFeature;

            if (feature != null && workspace != null)
            {
                Dictionary<string, tag> tagDictionary = new Dictionary<string, tag>();
                if (retrievedTags != null)
                {
                    for (int tagIndex = 0; tagIndex < retrievedTags.Length; tagIndex++)
                    {
                        if (tagDictionary.ContainsKey(retrievedTags[tagIndex].k) == false)
                        {
                            tagDictionary.Add(retrievedTags[tagIndex].k, retrievedTags[tagIndex]);
                        }
                    }
                }

                IWorkspaceDomains workspaceDomains = workspace as IWorkspaceDomains;

                string extensionString = "";

                try
                {
                    switch (feature.Shape.GeometryType)
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
                            extensionString = "_pt";
                            break;
                        case esriGeometryType.esriGeometryPolygon:
                            extensionString = "_ply";
                            break;
                        case esriGeometryType.esriGeometryPolyline:
                            extensionString = "_ln";
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
                catch { }

                if (String.IsNullOrEmpty(extensionString))
                {
                    return retrievedTags;
                }

                IEnumDomain enumDomain = workspaceDomains.Domains;

                if (enumDomain != null)
                {
                    enumDomain.Reset();
                    IDomain currentDomain = enumDomain.Next();

                    while (currentDomain != null)
                    {
                        int extensionPosition = currentDomain.Name.IndexOf(extensionString);

                        // only check the attributes that are relevant for the current geometry type
                        if (extensionPosition > 0)
                        {
                            string attributeName = currentDomain.Name.Substring(0, extensionPosition);
                            int attributefieldIndex = row.Fields.FindField(attributeName);

                            if (attributefieldIndex > -1)
                            {
                                object attributeValue = row.get_Value(attributefieldIndex);

                                if (attributeValue != DBNull.Value)
                                {
                                    // check if the current attribute value is already in the tag listing
                                    if (tagDictionary.ContainsKey(attributeName) == false)
                                    {
                                        // create a new tag to store
                                        tag explicitAttributeTag = new tag();
                                        explicitAttributeTag.k = attributeName;
                                        explicitAttributeTag.v = Convert.ToString(attributeValue);

                                        if (String.IsNullOrEmpty(explicitAttributeTag.v) == false)
                                        {
                                            tagDictionary.Add(attributeName, explicitAttributeTag);
                                        }
                                    }
                                }
                            }
                        }

                        currentDomain = enumDomain.Next();
                    }

                    // copy the values back into the array
                    retrievedTags = new tag[tagDictionary.Count];
                    tagDictionary.Values.CopyTo(retrievedTags, 0);
                }
            }
            return retrievedTags;
        }

        public void insertOSMTags(int osmTagsFieldIndex, IRowBuffer rowBuffer, tag[] tags)
        {
            if (rowBuffer.Fields.get_Field(osmTagsFieldIndex).Type == esriFieldType.esriFieldTypeBlob)
            {
                byte[] tagBuffer = XmlSerializeTagObjectBuffer(tags);
                using (System.IO.MemoryStream memoryStream = new MemoryStream(tagBuffer))
                {
                    IMemoryBlobStreamVariant memStream = new MemoryBlobStreamClass() as IMemoryBlobStreamVariant;
                    memStream.ImportFromVariant(memoryStream.ToArray());

                    try
                    {
                        rowBuffer.set_Value(osmTagsFieldIndex, memStream);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(ex.Message);
                        System.Diagnostics.Debug.WriteLine(ex.StackTrace);
                    }
                }
            }
            else if (rowBuffer.Fields.get_Field(osmTagsFieldIndex).Type == esriFieldType.esriFieldTypeXML)
            {
                string tagsAsString = XmlSerializeTagObject(tags);

                rowBuffer.set_Value(osmTagsFieldIndex, tagsAsString);
            }
        }

        public void insertOSMTags(int osmTagsFieldIndex, IRow row, tag[] tags, IWorkspace workspace)
        {
            if (row.Fields.get_Field(osmTagsFieldIndex).Type == esriFieldType.esriFieldTypeBlob)
            {
                byte[] tagBuffer = XmlSerializeTagObjectBuffer(tags);
                using (System.IO.MemoryStream memoryStream = new MemoryStream(tagBuffer))
                {
                    IMemoryBlobStreamVariant memStream = new MemoryBlobStreamClass() as IMemoryBlobStreamVariant;
                    memStream.ImportFromVariant(memoryStream.ToArray());

                    try
                    {
                        row.set_Value(osmTagsFieldIndex, memStream);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine(ex.Message);
                        System.Diagnostics.Debug.WriteLine(ex.StackTrace);
                    }
                }
            }
            else if (row.Fields.get_Field(osmTagsFieldIndex).Type == esriFieldType.esriFieldTypeXML)
            {
                string tagsAsString = XmlSerializeTagObject(tags);

                //XDocument xDocument = new XDocument(tags);
                //string tagsAsString = xDocument.ToString(SaveOptions.DisableFormatting);

                //xDocument = null;

                //byte[] xmlAsBytes = UTF8Encoding.UTF8.GetBytes(tagsAsString);
                row.set_Value(osmTagsFieldIndex, tagsAsString);
            }

            // next let's go through the fields with a domain table and check if the tag is modeled in a domain table as well
            // change the corresponding attribute value as well
            IFeature feature = row as IFeature;

            if (feature != null && workspace != null)
            {

                for (int tagIndex = 0; tagIndex < tags.Length; tagIndex++)
                {
                    int tagFieldindex = feature.Fields.FindField(tags[tagIndex].k);

                    if (tagFieldindex > -1)
                    {

                        IWorkspaceDomains workspaceDomains = workspace as IWorkspaceDomains;

                        string extensionString = "";
                        switch (feature.Shape.GeometryType)
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
                                extensionString = "_pt";
                                break;
                            case esriGeometryType.esriGeometryPolygon:
                                extensionString = "_ply";
                                break;
                            case esriGeometryType.esriGeometryPolyline:
                                extensionString = "_ln";
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

                        if (String.IsNullOrEmpty(extensionString))
                        {
                            return;
                        }

                        IDomain currentDomain = null;
                        try
                        {
                            currentDomain = workspaceDomains.get_DomainByName(tags[tagIndex].k + extensionString);
                        }
                        catch
                        {
                            continue;
                        }

                        if (currentDomain != null)
                        {
                            ICodedValueDomain osmCodedValueDomain = currentDomain as ICodedValueDomain;

                            if (osmCodedValueDomain == null)
                            {
                                continue;
                            }

                            bool valueSet = false;
                            for (int domainCodeIndex = 0; domainCodeIndex < osmCodedValueDomain.CodeCount; domainCodeIndex++)
                            {
                                if (osmCodedValueDomain.get_Value(domainCodeIndex).ToString().Equals(tags[tagIndex].v))
                                {
                                    feature.set_Value(tagFieldindex, osmCodedValueDomain.get_Value(domainCodeIndex));
                                    valueSet = true;
                                }
                            }

                            if (valueSet == false)
                            {
                                feature.set_Value(tagFieldindex, System.DBNull.Value);
                            }
                        }
                    }
                }
            }
        }

        public void CreateOSMMetadata(IDataset inputDataset, string metadataAbstract, string metadataPurpose)
        {
            if (inputDataset == null)
                return;

            try
            {

                IDatasetName datasetName = inputDataset.FullName as IDatasetName;

                IMetadataEdit metadataEdit = datasetName as IMetadataEdit;
                if (metadataEdit.CanEditMetadata)
                {
                    IMetadata metadata = datasetName as IMetadata;

                    IPropertySet metadataPropertySet = metadata.Metadata;
                    IXmlPropertySet2 metadataXML = metadataPropertySet as IXmlPropertySet2;

                    metadataXML.SetPropertyX("dataIdInfo/idCitation/citRespParty/rpIndName", "OpenStreetMap", esriXmlPropertyType.esriXPTText, esriXmlSetPropertyAction.esriXSPAAddIfNotExists, false);
                    metadataXML.SetPropertyX("dataIdInfo/idCitation/citRespParty/rpCntInfo/cntOnlineRes/linkage", "http://www.openstreetmap.org", esriXmlPropertyType.esriXPTLink, esriXmlSetPropertyAction.esriXSPAAddIfNotExists, false);
                    metadataXML.SetAttribute("dataIdInfo/idCitation/citRespParty/rpCntInfo/cntOnlineRes/orFunct/OnFunctCd", "value", "001", esriXmlSetPropertyAction.esriXSPAAddIfNotExists);
                    metadataXML.SetAttribute("dataIdInfo/idCitation/citRespParty/role/RoleCd", "value", "005", esriXmlSetPropertyAction.esriXSPAAddIfNotExists);

                    metadataXML.SetPropertyX("dataIdInfo/idCredit", "Map data (c) OpenStreetMap, ODbL 1.0", esriXmlPropertyType.esriXPTText, esriXmlSetPropertyAction.esriXSPAAddIfNotExists, false);

                    metadataXML.SetPropertyX("dataIdInfo/idPoC/rpOrgName", "OpenStreetMap", esriXmlPropertyType.esriXPTText, esriXmlSetPropertyAction.esriXSPAAddIfNotExists, false);
                    metadataXML.SetPropertyX("dataIdInfo/idPoC/rpCntInfo/cntOnlineRes/linkage", "http://www.openstreetmap.org", esriXmlPropertyType.esriXPTLink, esriXmlSetPropertyAction.esriXSPAAddIfNotExists, false);
                    metadataXML.SetAttribute("dataIdInfo/idPoC/rpCntInfo/cntOnlineRes/orFunct/OnFunctCd", "value", "001", esriXmlSetPropertyAction.esriXSPAAddIfNotExists);
                    metadataXML.SetAttribute("dataIdInfo/idPoC/role/RoleCd", "value", "005", esriXmlSetPropertyAction.esriXSPAAddIfNotExists);

                    metadataXML.SetAttribute("dataIdInfo/resMaint/maintFreq/MaintFreqCd", "value", "009", esriXmlSetPropertyAction.esriXSPAAddIfNotExists);
                    metadataXML.SetAttribute("dataIdInfo/resMaint/maintScp/ScopeCd", "value", "005", esriXmlSetPropertyAction.esriXSPAAddIfNotExists);

                    metadataXML.SetPropertyX("dataIdInfo/resConst/LegConsts/useLimit", "Map data (c) OpenStreetMap, ODbL 1.0; http://opendatacommons.org/licenses/odbl/", esriXmlPropertyType.esriXPTText, esriXmlSetPropertyAction.esriXSPAAddOrReplace, false);
                    metadataXML.SetAttribute("dataIdInfo/resConst/LegConsts/accessConsts/RestrictCd", "value", "005", esriXmlSetPropertyAction.esriXSPAAddIfNotExists);
                    metadataXML.SetAttribute("dataIdInfo/resConst/LegConsts/useConsts/RestrictCd", "value", "005", esriXmlSetPropertyAction.esriXSPAAddIfNotExists);
                    metadataXML.SetAttribute("dataIdInfo/resConst/SecConsts/class/ClasscationCd", "value", "001", esriXmlSetPropertyAction.esriXSPAAddIfNotExists);

                    metadataXML.SetPropertyX("dataIdInfo/resConst/Consts/useLimit", "Map data (c) OpenStreetMap, ODbL 1.0; http://opendatacommons.org/licenses/odbl/", esriXmlPropertyType.esriXPTText, esriXmlSetPropertyAction.esriXSPAAddOrReplace, false);

                    metadataXML.SetPropertyX("dataIdInfo/idPurp", metadataPurpose, esriXmlPropertyType.esriXPTText, esriXmlSetPropertyAction.esriXSPAAddIfNotExists, false);
                    metadataXML.SetPropertyX("dataIdInfo/idAbs", metadataAbstract, esriXmlPropertyType.esriXPTText, esriXmlSetPropertyAction.esriXSPAAddIfNotExists, false);

                    metadataXML.SetPropertyX("dataIdInfo/idConst/LegConsts/useLimit", "CC-BY-SA; http://creativecommons.org/licenses/by-sa/2.0/", esriXmlPropertyType.esriXPTText, esriXmlSetPropertyAction.esriXSPAAddOrReplace, false);
                    metadataXML.SetAttribute("dataIdInfo/idConst/LegConsts/accessConsts/RestrictCd", "value", "005", esriXmlSetPropertyAction.esriXSPAAddIfNotExists);
                    metadataXML.SetAttribute("dataIdInfo/idConst/LegConsts/useConsts/RestrictCd", "value", "005", esriXmlSetPropertyAction.esriXSPAAddIfNotExists);
                    metadataXML.SetAttribute("dataIdInfo/idConst/SecConsts/class/ClasscationCd", "value", "001", esriXmlSetPropertyAction.esriXSPAAddIfNotExists);

                    metadataXML.DeleteProperty("dataIdInfo/searchKeys");
                    metadataXML.SetPropertyX("dataIdInfo/searchKeys/keyword", "OpenStreetMap", esriXmlPropertyType.esriXPTText, esriXmlSetPropertyAction.esriXSPAAddIfNotExists, false);
                    metadataXML.SetPropertyX("dataIdInfo/searchKeys/keyword", "Applications Prototype Lab", esriXmlPropertyType.esriXPTText, esriXmlSetPropertyAction.esriXSPAAddDuplicate, false);
                    metadataXML.SetPropertyX("dataIdInfo/searchKeys/keyword", "ESRI", esriXmlPropertyType.esriXPTText, esriXmlSetPropertyAction.esriXSPAAddDuplicate, false);

                    metadataXML.SetAttribute("mdMaint/maintFreq/MaintFreqCd", "value", "001", esriXmlSetPropertyAction.esriXSPAAddIfNotExists);
                    metadataXML.SetAttribute("mdMaint/maintScp/ScopeCd", "value", "005", esriXmlSetPropertyAction.esriXSPAAddIfNotExists);
                    metadataXML.SetAttribute("mdMaint/maintCont/role/RoleCd", "value", "005", esriXmlSetPropertyAction.esriXSPAAddIfNotExists);

                    metadataXML.SetPropertyX("mdConst/LegConsts/useLimit", "Map data (c) OpenStreetMap, ODbL 1.0; http://opendatacommons.org/licenses/odbl/", esriXmlPropertyType.esriXPTText, esriXmlSetPropertyAction.esriXSPAAddOrReplace, false);
                    metadataXML.SetAttribute("mdConst/LegConsts/accessConsts/RestrictCd", "value", "005", esriXmlSetPropertyAction.esriXSPAAddIfNotExists);
                    metadataXML.SetAttribute("mdConst/LegConsts/useConsts/RestrictCd", "value", "005", esriXmlSetPropertyAction.esriXSPAAddIfNotExists);
                    metadataXML.SetAttribute("mdConst/SecConsts/class/ClasscationCd", "value", "001", esriXmlSetPropertyAction.esriXSPAAddIfNotExists);

                    // save the current metadata
                    metadata.Metadata = metadataPropertySet;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
            }
        }

        public String XmlSerializeIsMemberOf(List<string> membershipList)
        {
            String XmlizedString = null;
            MemoryStream memoryStream = null;
            try
            {
                memoryStream = new MemoryStream();
                using (XmlTextWriter xmlTextWriter = new XmlTextWriter(memoryStream, Encoding.UTF8))
                {
                    _listStringSerializer.Serialize(xmlTextWriter, membershipList);
                    memoryStream = (MemoryStream)xmlTextWriter.BaseStream;
                    XmlizedString = UTF8ByteArrayToString(memoryStream.ToArray());
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
            }
            finally
            {
                memoryStream.Dispose();
            }
            return XmlizedString;
        }

        public byte[] XmlSerializeIsMemberOfBuffer(List<string> membershipList)
        {
            byte[] byteBuffer = null;
            MemoryStream memoryStream = null;
            try
            {
                memoryStream = new MemoryStream();
                using (XmlTextWriter xmlTextWriter = new XmlTextWriter(memoryStream, Encoding.UTF8))
                {
                    _listStringSerializer.Serialize(xmlTextWriter, membershipList);
                    memoryStream = (MemoryStream)xmlTextWriter.BaseStream;
                    byteBuffer = memoryStream.ToArray();

                    memoryStream.Close();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
            }
            finally
            {
                memoryStream.Dispose();
            }

            return byteBuffer;
        }

        public List<string> DeserializeIsMemberOfBuffer(byte[] byteBuffer)
        {
            List<string> isMemberOfList = null;
            try
            {
                using (MemoryStream memoryStream = new MemoryStream(byteBuffer))
                {
                    //XmlTextWriter xmlTextWriter = new XmlTextWriter(memoryStream, Encoding.UTF8);

                    isMemberOfList = _listStringSerializer.Deserialize(memoryStream) as List<string>;
                    memoryStream.Close();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
            }
            return isMemberOfList;

        }

        public List<string> DeserializeIsMemberOf(String pXmlizedString)
        {

            List<string> isMemberOfList = null;

            try
            {
                using (MemoryStream memoryStream = new MemoryStream(StringToUTF8ByteArray(pXmlizedString)))
                {
                    //XmlTextWriter xmlTextWriter = new XmlTextWriter(memoryStream, Encoding.UTF8);
                    isMemberOfList = _listStringSerializer.Deserialize(memoryStream) as List<string>;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
            }

            return isMemberOfList;
        }

        public string XmlSerializeTagObject(tag[] tagCollection)
        {
            String XmlizedString = null;
            MemoryStream memoryStream = null;
            try
            {
                memoryStream = new MemoryStream();
                using (XmlTextWriter xmlTextWriter = new XmlTextWriter(memoryStream, Encoding.UTF8))
                {
                    _tagSerializer.Serialize(xmlTextWriter, tagCollection);
                    memoryStream = (MemoryStream)xmlTextWriter.BaseStream;

                    XmlizedString = UTF8ByteArrayToString(memoryStream.ToArray());
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
            }
            finally
            {
                memoryStream.Dispose();
            }

            return XmlizedString;
        }

        //public String XmlSerializeTagObject(tag[] tagCollection)
        //{
        //    String XmlizedString = String.Empty;

        //    try
        //    {

        //        XElement tags = new XElement("tags");

        //        foreach (tag currentTag in tagCollection)
        //        {
        //            XElement xtag = new XElement("tag");
        //            xtag.Add(new XAttribute("k", currentTag.k));
        //            xtag.Add(new XAttribute("v", currentTag.v));
        //            tags.Add(xtag);
        //        }

        //        XDocument xDocument = new XDocument(new XDeclaration("1.0", "utf-8", "yes"), tags);

        //        XmlizedString = xDocument.ToString(SaveOptions.DisableFormatting);

        //        xDocument = null;
        //    }
        //    catch (Exception ex)
        //    {
        //        System.Diagnostics.Debug.WriteLine(ex.Message);
        //        System.Diagnostics.Debug.WriteLine(ex.StackTrace);
        //    }

        //    return XmlizedString;
        //}

        public byte[] XmlSerializeTagObjectBuffer(tag[] tagCollection)
        {
            byte[] byteBuffer = null;
            MemoryStream memoryStream = null;
            try
            {
                memoryStream = new MemoryStream();
                using (XmlTextWriter xmlTextWriter = new XmlTextWriter(memoryStream, Encoding.UTF8))
                {
                    xmlTextWriter.WriteStartDocument();

                    _tagSerializer.Serialize(xmlTextWriter, tagCollection);
                    memoryStream = (MemoryStream)xmlTextWriter.BaseStream;
                    byteBuffer = memoryStream.ToArray();
                }
                memoryStream.Close();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
            }
            finally
            {
                memoryStream.Dispose();
            }

            return byteBuffer;
        }

        public tag[] DeserializeTagCollection(String xmlString)
        {
            tag[] tagCollection = null;
            try
            {
                using (XmlTextReader tagReader = new XmlTextReader(xmlString))
                {
                    tagCollection = _tagSerializer.Deserialize(tagReader) as tag[];
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
            }

            return tagCollection;
        }

        public tag[] DeserializeTagCollectionBuffer(byte[] byteBuffer)
        {
            tag[] tagCollection = null;
            MemoryStream memoryStream = null;
            try
            {
                memoryStream = new MemoryStream(byteBuffer);
                tagCollection = _tagSerializer.Deserialize(memoryStream) as tag[];
                memoryStream.Close();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
            }
            finally
            {
                if (memoryStream != null)
                    memoryStream.Dispose();
            }

            return tagCollection;

        }

        public String XmlSerializeMembers(member[] members)
        {
            String XmlizedString = null;
            MemoryStream memoryStream = null;
            try
            {
                memoryStream = new MemoryStream();
                using (XmlTextWriter xmlTextWriter = new XmlTextWriter(memoryStream, Encoding.UTF8))
                {
                    _memberSerializer.Serialize(xmlTextWriter, members);
                    memoryStream = (MemoryStream)xmlTextWriter.BaseStream;
                    XmlizedString = UTF8ByteArrayToString(memoryStream.ToArray());
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
            }
            finally
            {
                if (memoryStream != null)
                    memoryStream.Dispose();
            }

            return XmlizedString;
        }

        public byte[] XmlSerializeMembersBuffer(member[] members)
        {
            byte[] byteBuffer = null;
            MemoryStream memoryStream = null;
            try
            {

                memoryStream = new MemoryStream();
                using (XmlTextWriter xmlTextWriter = new XmlTextWriter(memoryStream, Encoding.UTF8))
                {
                    _memberSerializer.Serialize(xmlTextWriter, members);
                    memoryStream = (MemoryStream)xmlTextWriter.BaseStream;
                    byteBuffer = memoryStream.ToArray();

                    memoryStream.Close();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
            }
            finally
            {
                if (memoryStream != null)
                    memoryStream.Dispose();
            }

            return byteBuffer;
        }

        public member[] DeserializeMembersBuffer(byte[] byteBuffer)
        {
            member[] members = null;
            MemoryStream memoryStream = null;
            try
            {
                memoryStream = new MemoryStream(byteBuffer);
                using (XmlTextWriter xmlTextWriter = new XmlTextWriter(memoryStream, Encoding.UTF8))
                {
                    members = _memberSerializer.Deserialize(memoryStream) as member[];
                    memoryStream.Close();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
            }
            finally
            {
                memoryStream.Dispose();
            }
            return members;

        }

        public member[] DeserializeMembers(String pXmlizedString)
        {

            member[] members = null;

            try
            {
                using (MemoryStream memoryStream = new MemoryStream(StringToUTF8ByteArray(pXmlizedString)))
                {
                    using (XmlTextWriter xmlTextWriter = new XmlTextWriter(memoryStream, Encoding.UTF8))
                    {
                        members = _memberSerializer.Deserialize(memoryStream) as member[];
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
            }

            return members;
        }

        /// <summary>
        /// To convert a Byte Array of Unicode values (UTF-8 encoded) to a complete String.
        /// </summary>
        /// <param name="characters">Unicode Byte Array to be converted to String</param>
        /// <returns>String converted from Unicode Byte Array</returns>
        public String UTF8ByteArrayToString(Byte[] characters)
        {
            String constructedString = _encoding.GetString(characters);
            return (constructedString);
        }

        /// <summary>
        /// Converts the String to UTF8 Byte array and is used in the De-serialization
        /// </summary>
        /// <param name="pXmlString"></param>
        /// <returns></returns>
        public Byte[] StringToUTF8ByteArray(String pXmlString)
        {
            Byte[] byteArray = null;
            try
            {
                _encoding.GetBytes(pXmlString);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
            }
            return byteArray;
        }

        /// <summary>
        /// the dictionary will hold the parent id as the first element and the second element is an indicator for the type of parent
        /// pt - point
        /// ln - line
        /// ply - polygon
        /// rel - relation
        /// </summary>
        /// <param name="isMemberOfList"></param>
        /// <returns></returns>
        public Dictionary<string, string> parseIsMemberOfList(List<string> isMemberOfList)
        {
            Dictionary<string, string> isMemberOfDictionary = new Dictionary<string, string>();
            try
            {
                foreach (string isMemberOfItem in isMemberOfList)
                {
                    string[] splitResults = isMemberOfItem.Split("_".ToCharArray());

                    if (splitResults.Length == 1)
                    {
                        isMemberOfDictionary.Add(splitResults[0], "rel");
                    }
                    else if (splitResults.Length == 2)
                    {
                        isMemberOfDictionary.Add(splitResults[0], splitResults[1]);
                    }
                    else
                    {
                        isMemberOfDictionary.Add(splitResults[0], splitResults[1]);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
            }

            return isMemberOfDictionary;
        }

        /// <summary>
        /// Compare the list of tags und the given feature to see if they are same. Only relevant tags are compared.
        /// Non-relevant tags are "created_by", "source", "attribution", "version", "note", "type".
        /// </summary>
        /// <param name="relationTags"></param>
        /// <param name="row"></param>
        /// <param name="tagFieldIndex"></param>
        /// <param name="currentWorkspace"></param>
        /// <returns>Boolean indicating if the relevant tags are the same.</returns>
        public bool AreTagsTheSame(IList<tag> relationTags, IRow row, int tagFieldIndex, IWorkspace currentWorkspace)
        {
            bool tagsAreTheSame = true;

            try
            {
                IList<tag> relevantRelationTags = RetrieveRelevantTags(relationTags);

                tag[] wayTags = retrieveOSMTags(row, tagFieldIndex, currentWorkspace);

                IList<tag> relevantWayTags = RetrieveRelevantTags(new List<tag>(wayTags));

                if (relevantWayTags.Count != relevantRelationTags.Count)
                {
                    tagsAreTheSame = false;
                    return tagsAreTheSame;
                }

                foreach (var wayTag in relevantWayTags)
                {
                    if (!relevantRelationTags.Contains(wayTag, new TagKeyValueComparer()))
                    {
                        tagsAreTheSame = false;
                        return tagsAreTheSame;
                    }
                }
            }
            catch { }

            return tagsAreTheSame;
        }

        private IList<tag> RetrieveRelevantTags(IList<tag> originalTags)
        {
            IList<tag> relevantTags = new List<tag>();

            string[] non_relevant = {"created_by", "source", "attribution", "note", "version", "type"};

            foreach (var currentTag in originalTags)
            {
                if (!System.Array.Exists(non_relevant, str => str.ToLower().Equals(currentTag.k)))
                    relevantTags.Add(currentTag);
            }

            return relevantTags;
        }

        /// <summary>
        /// Given a row object from a feature class determines if valid tags exist.
        /// </summary>
        /// <param name="row">Row object from a feature class</param>
        /// <param name="tagFieldIndex">field index describing the field containing the osmTags.</param>
        /// <param name="currentWorkspace">optional; workspace containing the feature class</param>
        /// <returns>Boolean indicating if the node has relevant tags or not. Non-relevant tags are "created_by", "source", "attribution", "version", "note", "type".</returns>
        public bool DoesHaveKeys(IRow row, int tagFieldIndex, IWorkspace currentWorkspace)
        {
            bool doesHaveKeys = false;
            try
            {
                tag[] currentTags = retrieveOSMTags(row, tagFieldIndex, currentWorkspace);

                if (currentTags != null)
                {
                    foreach (tag rowTag in currentTags)
                    {
                        if (rowTag.k.ToLower().Equals("created_by"))
                        {
                        }
                        else if (rowTag.k.ToLower().Equals("source"))
                        {
                        }
                        else if (rowTag.k.ToLower().Equals("attribution"))
                        {
                        }
                        else if (rowTag.k.ToLower().Equals("note"))
                        {
                        }
                        else if (rowTag.k.ToLower().Equals("version"))
                        {
                        }
                        else if (rowTag.k.ToLower().Equals("type"))
                        {
                        }
                        else
                        {
                            doesHaveKeys = true;
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
            }

            return doesHaveKeys;
        }

        /// <summary>
        /// Given a node object determines if the node has relevant tags.
        /// </summary>
        /// <param name="currentnode">OpenStreetMap node object to be examined. Non-relevant tags are 'created_by', 'source', 'attribution', and 'note'. </param>
        /// <returns>Boolean indicating if the node has relevant tags or not.</returns>
        public bool DoesHaveKeys(tag[] tags)
        {
            bool doesHaveKeys = false;
            bool partsOverride = false;

            try
            {
                if (tags != null)
                {
                    foreach (tag nodetag in tags)
                    {
                        if (nodetag.k.ToLower().Equals("created_by"))
                        {
                        }
                        else if (nodetag.k.ToLower().Equals("source"))
                        {
                        }
                        else if (nodetag.k.ToLower().Equals("attribution"))
                        {
                        }
                        else if (nodetag.k.ToLower().Equals("note"))
                        {
                        }
                        else if (nodetag.k.ToLower().Contains("building:part"))
                        {
                        }
                        else
                        {
                            doesHaveKeys = true;
                            break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
            }

            return doesHaveKeys;
        }

        /// <summary>
        /// Given a relation object determines if the relation has relevant tags.
        /// </summary>
        /// <param name="currentrelation">OpenStreetMap relation object to be examined. Non-relevant tags are 'created_by', 'source', 'attribution', and 'note'. </param>
        /// <returns>Boolean indicating if the relation has relevant tags or not.</returns>
        public bool DoesHaveKeys(relation currentrelation)
        {
            bool doesHaveKeys = false;

            try
            {
                if (currentrelation.Items != null)
                {
                    foreach (var relationItem in currentrelation.Items)
                    {
                        if (relationItem is tag)
                        {
                            tag relationTag = relationItem as tag;

                            if (relationTag.k.ToLower().Equals("created_by"))
                            {
                            }
                            else if (relationTag.k.ToLower().Equals("source"))
                            {
                            }
                            else if (relationTag.k.ToLower().Equals("attribution"))
                            {
                            }
                            else if (relationTag.k.ToLower().Equals("note"))
                            {
                            }
                            else if (relationTag.k.ToLower().Equals("type"))
                            {
                            }
                            else
                            {
                                doesHaveKeys = true;
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
            }

            return doesHaveKeys;
        }

        /// <summary>
        /// Given a way object determines if the way has relevant tags.
        /// </summary>
        /// <param name="currentway">Way object to be examined. Non-relevant tags are 'created_by', 'source', 'attribution', and 'note'. </param>
        /// <returns>Boolean indicating if the way has relevant tags or not.</returns>
        public bool DoesHaveKeys(way currentway)
        {
            IList<tag> relevantTags = new List<tag>();
            bool doesHaveKeys = false;

            if (currentway == null)
                throw new ArgumentNullException("currentway", "Unexpected value.");

            if (currentway.tag == null)
                return doesHaveKeys;

            try
            {
                string[] non_relevant = { "created_by", "source", "attribution", "note", "version", "type" };

                foreach (var currentTag in currentway.tag)
                {
                    if (!System.Array.Exists(non_relevant, str => str.ToLower().Equals(currentTag.k)))
                        relevantTags.Add(currentTag);
                }

                if (relevantTags.Count > 0)
                {
                    doesHaveKeys = true;

                    foreach (var wayTag in relevantTags)
                    {
                        if (wayTag.k.ToLower().Contains("building:part"))
                        {
                            doesHaveKeys = false;

                            // the exception is the existence of both building:part and building,
                            // then building will take precedence
                            if (relevantTags.Contains(new tag() { k = "building" }, new TagKeyComparer()))
                            {
                                doesHaveKeys = true;
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
            }

            return doesHaveKeys;
        }
    }

    [ComVisible(false)]
    public class OSMElementComparer : IComparer<object>
    {
        public int Compare(object x, object y)
        {
            int compareResult = 0;

            if ((x is node) && (y is node))
            {
                return 0; // equal
            }
            else if ((x is node) && (y is way))
            {
                return -1; // node is less than a way -- is sorted ahead
            }
            else if ((x is node) && (y is relation))
            {
                return -1;
            }
            else if ((x is way) && (y is node))
            {
                return 1; // way is more than a way -- is sorted behind node
            }
            else if ((x is way) && (y is way))
            {
                return 0;
            }
            else if ((x is way) && (y is relation))
            {
                return -1;
            }
            else if ((x is relation) && (y is node))
            {
                return 1;
            }
            else if ((x is relation) && (y is way))
            {
                return 1;
            }

            // this would be the last test case  - relation compared to relation 
            return compareResult;
        }
    }
}
