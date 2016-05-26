// (c) Copyright Esri, 2010 - 2016
// This source is subject to the Apache 2.0 License.
// Please see http://www.apache.org/licenses/LICENSE-2.0.html for details.
// All other rights reserved.


using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.esriSystem;
using System.Resources;
using System.Security;
using ESRI.ArcGIS.Geoprocessing;

namespace ESRI.ArcGIS.OSM.GeoProcessing
{
    [Guid("AD6C56FB-5130-458E-B9AA-30631BF19472")]
    [ComVisible(true)]
    public interface IHttpBasicGPValue
    {
        string UserName { set; get; }
        string PassWord { set; }
        string EncodedUserNamePassWord { get; set; }
    }

    [Guid("fab65ce5-775b-47b2-bc0b-87d93d3ebe94")]
    [ClassInterface(ClassInterfaceType.None)]
    [ProgId("OSMEditor.HttpBasicGPValue")]
    public class HttpBasicGPValue : IHttpBasicGPValue, IGPValue, IClone, IPersistVariant, IXMLSerialize, IGPDescribe
    {
        #region IGPValue Members
        ResourceManager resourceManager = null;
        string httpBasicGPPValueDisplayName = "Username/Password using Http Basic Encoding";
        IGPDataTypeFactory authenticationGPDataTypeFactory = null;
        // the first release of the tools (initial release ArcGIS 10) is 1 // 06/10/2010, ESRI Prototype Lab
        const int m_httpBasicGPValueVersion = 1;
        const string m_xmlElementName = "EncodedAuthentication";

        string m_username = null;
        string m_password = null;
        string m_encodedUserAuthentication = null;

        public HttpBasicGPValue()
        {
            resourceManager = new ResourceManager("ESRI.ArcGIS.OSM.GeoProcessing.OSMGPToolsStrings", this.GetType().Assembly);
            httpBasicGPPValueDisplayName = resourceManager.GetString("GPTools_Authentication_GPValue_displayname");

            if (authenticationGPDataTypeFactory == null)
            {
                authenticationGPDataTypeFactory = new AuthenticationDataTypeFactory();
            }

            m_password = String.Empty;
            m_username = String.Empty;
            m_encodedUserAuthentication = String.Empty;
        }

        public IGPDataType DataType
        {
            get
            {
                return authenticationGPDataTypeFactory.GetDataType("HttpBasicAuthenticationDataType");
            }
        }

        public void Empty()
        {
            m_username = String.Empty;
            m_password = String.Empty;
        }

        public string GetAsText()
        {
            return "";
        }

        public bool IsEmpty()
        {
            if (String.IsNullOrEmpty(m_username) || String.IsNullOrEmpty(m_password))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public IGPMessage SetAsText(string text)
        {
            IGPMessage valueSetMessage = new GPMessageClass();

            try
            {
                DecodeUserNamePassword(text);
            }
            catch (ArgumentNullException ex)
            {
                // pass
            }
            catch
            {
                valueSetMessage.Type = esriGPMessageType.esriGPMessageTypeError;
                valueSetMessage.Description = resourceManager.GetString("GPTools_Authentication_GPValue_UnableParseAuthentication");
                valueSetMessage.ErrorCode = 500;
            }

            return valueSetMessage;
        }

        #endregion

        #region IClone Members

        public void Assign(IClone src)
        {
            IHttpBasicGPValue httpBasicGPValue = src as IHttpBasicGPValue;

            if (httpBasicGPValue == null)
                return;

            DecodeUserNamePassword(httpBasicGPValue.EncodedUserNamePassWord);
        }

        public IClone Clone()
        {
            IClone clone = new HttpBasicGPValue();
            clone.Assign((IClone)this);

            return clone;
        }

        public bool IsEqual(IClone other)
        {
            bool equalResult = false;

            IHttpBasicGPValue httpBasicGPValue = other as IHttpBasicGPValue;

            if (httpBasicGPValue == null)
                return equalResult;

            if (httpBasicGPValue.EncodedUserNamePassWord.Equals(EncodeUserAuthentication(m_username, m_password)))
            {
                equalResult = true;
            }

            return equalResult;
        }

        public bool IsIdentical(IClone other)
        {
            if (this.Equals(other))
            {
                return true;
            }
            else
            {
                return false;
            }

        }

        #endregion

        #region IPersistVariant Members

        public UID ID
        {
            get 
            {
                UID httpBasicGPValueUID = new UIDClass();
                httpBasicGPValueUID.Value = "{fab65ce5-775b-47b2-bc0b-87d93d3ebe94}";

                return httpBasicGPValueUID;
            }
        }

        public void Load(IVariantStream Stream)
        {
            int version = (int)Stream.Read();

            if (version > m_httpBasicGPValueVersion)
                return;

            m_username = Stream.Read() as string;
            m_password = Stream.Read() as string;
        }

        public void Save(IVariantStream Stream)
        {
            Stream.Write(m_httpBasicGPValueVersion);

            Stream.Write(m_username);
            Stream.Write(m_password);
        }

        #endregion

        #region IXMLSerialize Members

        public void Deserialize(IXMLSerializeData data)
        {
            int elementIndex = -1;
            elementIndex = data.Find(m_xmlElementName);

            if (elementIndex > -1)
            {
                DecodeUserNamePassword(data.GetString(elementIndex));
            }
        }

        public void Serialize(IXMLSerializeData data)
        {
            data.TypeName = "HttpBasicAuthenticationGPValue";
            data.TypeNamespaceURI = "http://www.esri.com/schemas/ArcGIS/10.0";

            data.AddString(m_xmlElementName, EncodeUserAuthentication(m_username, m_password));
        }

        #endregion

        #region IGPDescribe Members

        public object Describe(string Name)
        {
            string describeReturn = String.Empty;

            if (Name.Equals(m_xmlElementName))
            {
                describeReturn = "OpenStreetMap Authentication";
            }

            if (Name.Equals("DataType"))
            {
                IGPDataType currentDataType = this.DataType;
                describeReturn = currentDataType.Name;
            }

            return describeReturn;
        }

        #endregion

        #region IHttpBasicGPValue Members

        public string UserName
        {
            set
            {
                m_username = value;
            }
            get
            {
                return m_username;
            }
        }

        public string PassWord
        {
            set
            {
                m_password = value;
            }
        }

        public string EncodedUserNamePassWord
        {
            get
            {
                return EncodeUserAuthentication(m_username, m_password);
            }
            set
            {
                DecodeUserNamePassword(value);
            }
        }

        #endregion

        private void DecodeUserNamePassword(string value)
        {
            if (String.IsNullOrEmpty(value))
                return;

            string authInfo = Encoding.UTF8.GetString(Convert.FromBase64String(value));
            string[] splitAuthInfo = authInfo.Split(":".ToCharArray());

            m_username = splitAuthInfo[0];
            m_password = splitAuthInfo[1];

            splitAuthInfo = null;
            authInfo = null;
        }

        private string EncodeUserAuthentication(string m_username, string m_password)
        {
            string encodedAuthenticationString = String.Empty;
            
            try
            {
                    string authInfo = m_username + ":" + m_password;
                    authInfo = Convert.ToBase64String(Encoding.UTF8.GetBytes(authInfo));
                    encodedAuthenticationString = authInfo;
            }
            catch { }

            return encodedAuthenticationString;
        }
    }
}
