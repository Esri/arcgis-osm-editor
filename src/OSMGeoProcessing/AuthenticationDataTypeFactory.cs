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
using ESRI.ArcGIS.Geoprocessing;
using System.Resources;
using Microsoft.Win32;

namespace ESRI.ArcGIS.OSM.GeoProcessing
{
    [Guid("1b7b1666-5bda-49ac-b899-cff8b1bfb6d9")]
    [ClassInterface(ClassInterfaceType.None)]
    [ProgId("OSMEditor.HttpBasicDataTypeFactory")]
    public class AuthenticationDataTypeFactory : IGPDataTypeFactory
    {
        #region "Component Category Registration"
        [ComRegisterFunction()]
        static void Reg(string regKey)
        {
            // GPDataTypeFactories
            Registry.ClassesRoot.CreateSubKey(regKey + "\\Implemented Categories\\{CDA62C78-1221-4246-A622-61C354091657}");
        }

        [ComUnregisterFunction()]
        static void Unreg(string regKey)
        {
            //GPDataTypeFactories
            Registry.ClassesRoot.DeleteSubKey(regKey + "\\Implemented Categories\\{CDA62C78-1221-4246-A622-61C354091657}");
        }
        #endregion

        IEnumGPName m_enumGPName = null;
        Dictionary<string, IGPDataType> m_DataTypes = null;
        ResourceManager resourceManager = null;

        public AuthenticationDataTypeFactory()
        {
            m_enumGPName = null;
            m_DataTypes = new Dictionary<string, IGPDataType>();

            resourceManager = new ResourceManager("ESRI.ArcGIS.OSM.GeoProcessing.OSMGPToolsStrings", this.GetType().Assembly);

        }

        #region IGPDataTypeFactory Members

        public esriSystem.UID CLSID
        {
            get
            {
                UID dataTypeFactory = new UIDClass();
                dataTypeFactory.Value = "{1b7b1666-5bda-49ac-b899-cff8b1bfb6d9}";

                return dataTypeFactory;
            }
        }

        public IGPDataType GetDataType(string Name)
        {
            IGPDataType returnGPDataType = null;

            // attempt to read the cached versions first
            switch (Name)
            {
                case "HttpBasicAuthenticationDataType":
                    if (m_DataTypes.ContainsKey(Name))
                    {
                        returnGPDataType = m_DataTypes[Name];
                    }
                    break;
                default:
                    break;
            }

            // if nothing had been previously cached create the data type now
            if (returnGPDataType == null)
            {
                returnGPDataType = CreateDataType(Name);
                m_DataTypes.Add(Name, returnGPDataType);
            }

            return returnGPDataType;
        }

        private IGPDataType CreateDataType(string Name)
        {
            IGPDataType createdDataType = null;

            switch (Name)
            {
                case "HttpBasicAuthenticationDataType":
                    createdDataType = new HttpBasicDataType() as IGPDataType;
                    break;
                default:
                    break;
            }

            return createdDataType;
        }

        public IGPName GetDataTypeName(string Name)
        {
            return GetName(Name);
        }

        private IGPName GetName(string Name)
        {
            IGPName gpName = null;

            try
            {
                InitNames();
                m_enumGPName.Reset();

                gpName = m_enumGPName.Next();
                while (gpName != null)
                {
                    if (gpName.Equals(Name))
                    {
                        gpName = CreateDataTypeName(gpName.Name, gpName.DisplayName, (IGPDataTypeFactory)this);
                        break;
                    }

                    gpName = m_enumGPName.Next();
                }
            }
            catch
            {
                // undecided if I should throw the exception at this point
            }

            return gpName;
        }

        private void InitNames()
        {
            if (m_enumGPName == null)
            {
                m_enumGPName = GetNames(null);
            }
        }

        public IEnumGPName GetDataTypeNames()
        {
            if (m_enumGPName == null)
            {
                m_enumGPName = GetNames(null);
            }

            return m_enumGPName;
        }

        private IEnumGPName GetNames(IGPDataTypeFactory dataTypeFactory)
        {
            IArray names = new EnumGPNameClass();

            addDataTypeName(ref names, "HttpBasicAuthenticationDataType", resourceManager.GetString("GPTools_AuthenticationDataType_DisplayName"), (IGPDataTypeFactory)this);

            return names as IEnumGPName;
        }

        #endregion

        public void addDataTypeName(ref IArray Names, string Name, string DisplayName, IGPDataTypeFactory Factory)
        {
            IGPName gpName = CreateDataTypeName(Name, DisplayName, Factory);
            Names.Add(gpName);
        }


        public IGPName CreateDataTypeName(string Name, string DisplayName, IGPDataTypeFactory Factory)
        {
            IGPName gpName = new GPDataTypeNameClass();
            gpName.Name = Name;
            gpName.DisplayName = DisplayName;

            if (Factory != null)
            {
                gpName.Factory = this;
            }

            return gpName;
        }
    }
}
