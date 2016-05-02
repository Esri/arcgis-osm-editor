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
using ESRI.ArcGIS.Geoprocessing;

namespace ESRI.ArcGIS.OSM.GeoProcessing
{
    [Guid("FD63EBC5-4B63-4649-982E-25D55CB89CE6")]
    [ComVisible(true)]
    public interface IHttpBasicDataType
    {
        string MaskCharacter { get; set; }
    }

    [Guid("6594937a-3199-42e9-af63-a20d5cc2b211")]
    [ClassInterface(ClassInterfaceType.None)]
    [ProgId("OSMEditor.HttpBasicDataType")]
    public class HttpBasicDataType: IHttpBasicDataType, IGPDataType, IClone
    {
        #region IGPDataType Members
        ResourceManager resourceManager = null;
        string m_dataTypeDisplayName ="OpenStreetMap login";
        IGPDataTypeFactory m_dataTypeFactory = null;
        const string m_dataTypeName = "HttpBasicAuthenticationDataType";
        string m_maskingCharacter = "*";

        public HttpBasicDataType()
        {
            resourceManager = new ResourceManager("ESRI.ArcGIS.OSM.GeoProcessing.OSMGPToolsStrings", this.GetType().Assembly);
            m_dataTypeFactory = new AuthenticationDataTypeFactory();

            m_dataTypeDisplayName = resourceManager.GetString("GPTools_Authentication_HttpBasicDataType_displayname");
        }

        public UID ControlCLSID
        {
            get 
            {
                UID authenticationControlCLSID = new UIDClass();
                authenticationControlCLSID.Value = "{F483BDB7-DBA1-45CA-A31E-665422FD2460}";

                return authenticationControlCLSID;
            }
        }

        public IGPValue CreateValue(string text)
        {
            IGPValue gpValue = new HttpBasicGPValue();

            IGPMessage gpValueMessage = gpValue.SetAsText(text);

            if (gpValueMessage.IsInformational())
            {
                return gpValue;
            }
            else
            {
                return null;
            }
        }

        public string DisplayName
        {
            get 
            {
                return m_dataTypeDisplayName;
            }
        }

        public IName FullName
        {
            get 
            {
                return m_dataTypeFactory.GetDataTypeName(m_dataTypeName) as IName;
            }
        }

        public int HelpContext
        {
            get 
            {
                return default(int);
            }
        }

        public string HelpFile
        {
            get 
            {
                return String.Empty;
            }
        }

        public string MetadataFile
        {
            get 
            {
                return String.Empty;
            }
        }

        public string Name
        {
            get 
            {
                return m_dataTypeName;
            }
        }

        public IGPMessage ValidateDataType(IGPDataType Type)
        {
            IGPMessage validateDataTypeMessage = new GPMessageClass();
            IHttpBasicDataType targetType = Type as IHttpBasicDataType;

            if (targetType == null)
            {
                IGPStringType targetTypeString = Type as IGPStringType;
                if (targetTypeString != null)
                    return validateDataTypeMessage;
            }

            if (targetType == null)
            {
                validateDataTypeMessage.ErrorCode = 501;
                validateDataTypeMessage.Type = esriGPMessageType.esriGPMessageTypeError;
                validateDataTypeMessage.Description = resourceManager.GetString("GPTools_Authentication_HttpBasicDataType_typevalidation");
            }

            return validateDataTypeMessage;
        }

        public IGPMessage ValidateValue(IGPValue Value, IGPDomain Domain)
        {
            IGPMessage validateValueMessage = new GPMessageClass();
            IGPUtilities3 gpUtilities = new GPUtilitiesClass();

            IHttpBasicGPValue targetValue = gpUtilities.UnpackGPValue(Value) as IHttpBasicGPValue;
            
            if ( targetValue == null ) {
                IGPString targetValueString = gpUtilities.UnpackGPValue(Value) as IGPString;
                if ( targetValueString != null )
                    return validateValueMessage;
            }
            

            if (targetValue == null)
            {
                validateValueMessage.Type = esriGPMessageType.esriGPMessageTypeError;
                validateValueMessage.ErrorCode = 502;
                validateValueMessage.Description = resourceManager.GetString("GPTools_Authentication_HttpBasicDataType_valuevalidation");
            }

            if (Domain != null)
            {
                validateValueMessage = Domain.MemberOf((IGPValue)targetValue);
            }

            return validateValueMessage;
        }

        #endregion

        #region IClone Members

        public void Assign(IClone src)
        {
        }

        public IClone Clone()
        {
            IClone clone = new HttpBasicDataType() as IClone;
            clone.Assign(this);

            return clone;
        }

        public bool IsEqual(IClone other)
        {
            bool equalResult = false;

            IHttpBasicGPValue httpBasicGPValue = other as IHttpBasicGPValue;

            if (httpBasicGPValue == null)
                return equalResult;

            equalResult = true;

            return equalResult;
        }

        public bool IsIdentical(IClone other)
        {
            return other.Equals(this);
        }

        #endregion

        #region IHttpBasicDataType Members

        public string MaskCharacter
        {
            get
            {
                return m_maskingCharacter;
            }
            set
            {
                m_maskingCharacter = value;
            }
        }

        #endregion
    }
}
