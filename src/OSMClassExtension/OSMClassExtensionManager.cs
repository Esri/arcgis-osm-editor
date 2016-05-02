// (c) Copyright Esri, 2010 - 2016
// This source is subject to the Apache 2.0 License.
// Please see http://www.apache.org/licenses/LICENSE-2.0.html for details.
// All other rights reserved.

using System;
using System.Collections.Generic;
using System.Resources;
using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.Geodatabase;

namespace ESRI.ArcGIS.OSM.OSMClassExtension
{
    /// <summary>
    /// Class to apply the OSM Class Extension to datasets and assist in setting extension variables
    /// </summary>
    public static class OSMClassExtensionManager
    {
        private const string _OSM_CLASS_EXT_GUID = "{65CA4847-8661-45eb-8E1E-B2985CA17C78}";
        private const int _OSM_EXTENSION_VERSION = 2;
        private const string _OSM_MODEL_VERSION_2 = "osmVersion2.1";

        /// <summary>Apply the OSM Class Extension to the given featureClass</summary>
        /// <remarks>Obtains an exclusive schema lock if possible otherwise throws an exception</remarks>
        public static void ApplyOSMClassExtension(this IFeatureClass featureClass)
        {
            ApplyOSMClassExtension((ITable)featureClass);
        }

        /// <summary>Apply the OSM Class Extension to the given table</summary>
        /// <remarks>Obtains an exclusive schema lock if possible otherwise throws an exception</remarks>
        public static void ApplyOSMClassExtension(this ITable table)
        {
            if ((table == null) || (table.Extension is IOSMClassExtension))
                return;

            IClassSchemaEdit3 schemaEdit = table as IClassSchemaEdit3;
            if (schemaEdit == null)
                return;

            int osmIDIndex = table.Fields.FindField("OSMID");

            using (SchemaLockManager lockMgr = new SchemaLockManager(table))
            {
                UID osmClassExtensionUID = new UIDClass() { Value = _OSM_CLASS_EXT_GUID };

                IPropertySet extensionPropertSet = null;
                if (osmIDIndex > -1)
                {
                    // at release 2.1 we changed the OSMID field type to string, hence only when we find the string we are assuming version 2
                    if (table.Fields.get_Field(osmIDIndex).Type == esriFieldType.esriFieldTypeString)
                    {
                        extensionPropertSet = new PropertySetClass();
                        extensionPropertSet.SetProperty("VERSION", _OSM_EXTENSION_VERSION);
                    }
                }

                schemaEdit.AlterClassExtensionCLSID(osmClassExtensionUID, extensionPropertSet);
                schemaEdit.AlterClassExtensionProperties(extensionPropertSet);
            }
        }

        public static string OSMModelName
        {
            get
            {
                return _OSM_MODEL_VERSION_2;
            }
        }

        public static int Version
        {
            get
            {
                return _OSM_EXTENSION_VERSION;
            }
        }

        /// <summary>
        /// Determines based on the table extension which version of the Esri OSM schema we are dealing with.
        /// Version 1 stores the OSM IDs in the geometry ID property.
        /// Version 2 stores the ObjectID of the feature in the geometry ID property.
        /// </summary>
        /// <returns>The version number of the extension. If no OSMExtension is found a value of -1 is returned.</returns>
        public static int CurrentExtensionVersion(this ITable table)
        {
            if ((table == null) || !(table.Extension is IOSMClassExtension))
                return -1;

            if (table.ExtensionProperties == null)
                return 1;

            IPropertySet osmExtensionPropertySet = table.ExtensionProperties;
            int osmVersion = -1;

            try
            {
                osmVersion = Convert.ToInt32(osmExtensionPropertySet.GetProperty("VERSION"));
            }
            catch {}

            return osmVersion;
        }

        /// <summary>
        /// Formulates the WhereClause for the QueryFilter based on the version of the extension
        /// </summary>
        /// <returns>Properly formulated whereclause based on workspace and extension version.</returns>
        public static string WhereClauseByExtensionVersion(this ITable table, object osmID, string fieldName, int extensionVersion)
        {
            string whereClause = String.Empty;

            if (extensionVersion == 1)
            {
                whereClause = table.SqlIdentifier(fieldName) + " = " + Convert.ToString(osmID);
            }
            else if (extensionVersion == 2)
            {
                whereClause = table.SqlIdentifier(fieldName) + " = '" + Convert.ToString(osmID) + "'";
            }
            else
            {
                whereClause = table.SqlIdentifier(fieldName) + " = '" + Convert.ToString(osmID) + "'";
            }

            return whereClause;
        }

        /// <summary>
        /// Returns an array of field names for fields that have the 'required' flag.
        /// </summary>
        /// <returns>Returns an array of field names for fields that have the 'required' flag.
        /// If no required fields are found, or the table is null an empty array is returned.</returns>
        public static string[] ExtractRequiredFields(this IFeatureClass table)
        {
            List<string> requiredFields = new List<string>();

            if (table == null)
                return requiredFields.ToArray();

            for (int fieldIndex = 0; fieldIndex < table.Fields.FieldCount; fieldIndex++)
            {
                if (table.Fields.get_Field(fieldIndex).Required == true)
                {
                    requiredFields.Add(table.Fields.get_Field(fieldIndex).Name);
                }
            }

            return requiredFields.ToArray();
        }


        /// <summary>
        /// Formulates the WhereClause for the QueryFilter based on the version of the extension
        /// </summary>
        /// <returns>Properly formulated whereclause based on workspace and extension version.</returns>
        public static string WhereClauseByExtensionVersion(this IFeatureClass featureClass, object osmID, string fieldName, int extensionVersion)
        {
            return WhereClauseByExtensionVersion((ITable)featureClass, osmID, fieldName, extensionVersion);
        }

        /// <summary>
        /// Determines based on the table extension which version of the Esri OSM schema we are dealing with.
        /// Version 1 stores the OSM IDs in the geometry ID property.
        /// Version 2 stores the ObjectID of the feature in the geometry ID property.
        /// </summary>
        /// <returns>The version number of the extension. If no OSMExtension is found a value of -1 is returned.</returns>
        public static int OSMExtensionVersion(this IFeatureClass featureClass)
        {
            return CurrentExtensionVersion((ITable)featureClass);
        }

        /// <summary>Removes the OSM Class Extension from the given featureClass</summary>
        /// <remarks>Obtains an exclusive schema lock if possible otherwise throws an exception</remarks>
        public static void RemoveOSMClassExtension(this IFeatureClass featureClass)
        {
            RemoveOSMClassExtension((ITable)featureClass);
        }

        /// <summary>Removes the OSM Class Extension from the given table</summary>
        /// <remarks>Obtains an exclusive schema lock if possible otherwise throws an exception</remarks>
        public static void RemoveOSMClassExtension(this ITable table)
        {
            if ((table == null) || !(table.Extension is IOSMClassExtension))
                return;

            IClassSchemaEdit3 schemaEdit = table as IClassSchemaEdit3;
            if (schemaEdit == null)
                return;

            int osmIDIndex = table.Fields.FindField("OSMID");

            using (SchemaLockManager lockMgr = new SchemaLockManager(table))
            {
                IPropertySet extensionPropertSet = null;
                if (osmIDIndex > -1)
                {
                    // at release 2.1 we changed the OSMID field type to string, hence only when we find the string we are assuming version 2
                    if (table.Fields.get_Field(osmIDIndex).Type == esriFieldType.esriFieldTypeString)
                    {
                        extensionPropertSet = new PropertySetClass();
                        extensionPropertSet.SetProperty("VERSION", _OSM_EXTENSION_VERSION);
                    }
                }

                schemaEdit.AlterClassExtensionCLSID(null, extensionPropertSet);
                schemaEdit.AlterClassExtensionProperties(extensionPropertSet);
            }

            schemaEdit = null;
        }

        /// <summary>Clears the OSM Class Extension CanBypassChangeDetection flag</summary>
        /// <remarks>If OSM class extension is not currently applied to the given feature class, it is applied</remarks>
        public static void EnableOSMChangeDetection(this ITable table)
        {
            if (table == null)
                return;

            if (table.Extension is IOSMClassExtension)
                table.SetBypassChangeDetectionFlag(false);
            else
                ((ITable)table).ApplyOSMClassExtension();
        }

        public static void EnableOSMChangeDetection(this IFeatureClass featureClass)
        {
            EnableOSMChangeDetection((ITable)featureClass);
        }

        /// <summary>Sets the OSM Class Extension CanBypassChangeDetection flag</summary>
        public static void DisableOSMChangeDetection(this ITable table)
        {
            table.SetBypassChangeDetectionFlag(true);
        }

        /// <summary>Sets the OSM Class Extension CanBypassChangeDetection flag</summary>
        public static void DisableOSMChangeDetection(this IFeatureClass featureClass)
        {
            featureClass.SetBypassChangeDetectionFlag(true);
        }

        /// <summary>Sets or clears the OSM Class Extension CanBypassChangeDetection flag</summary>
        private static void SetBypassChangeDetectionFlag(this ITable table, bool CanBypassChangeDetection)
        {
            if ((table == null) || (table.Extension == null))
                return;

            IOSMClassExtension osmExt = table.Extension as IOSMClassExtension;
            if (osmExt == null)
                return;

            osmExt.CanBypassChangeDetection = CanBypassChangeDetection;
        }

        /// <summary>Sets or clears the OSM Class Extension CanBypassChangeDetection flag</summary>
        private static void SetBypassChangeDetectionFlag(this IFeatureClass featureClass, bool CanBypassChangeDetection)
        {
            SetBypassChangeDetectionFlag((ITable) featureClass, CanBypassChangeDetection);
        }

    }

    /// <summary>Class to manage schema locks on GDB datasets and demote locks on disposal</summary>
    public class SchemaLockManager : IDisposable
    {
        private List<ISchemaLock> _managedLocks;

        public SchemaLockManager()
        {
            _managedLocks = new List<ISchemaLock>();
        }

        public SchemaLockManager(ITable fc)
            : this()
        {
            LockDatasetSchema(fc);
        }

        /// <summary>
        /// finalizer to release outstanding resources
        /// </summary>
        ~SchemaLockManager()
        {
            foreach (ISchemaLock schemaLock in _managedLocks)
            {
                try
                {
                    if (schemaLock != null)
                        schemaLock.ChangeSchemaLock(esriSchemaLock.esriSharedSchemaLock);
                }
                catch
                {
                    // ignore lock demotion exception
                }
            }

            _managedLocks.Clear();
        }

        /// <summary>Method to perform the actual schema lock</summary>
        /// <remarks>
        /// - If the schema is already locked by a different user, an exception is thrown with lock info (name / table)
        /// - If the schema is already locked by the current user, no lock is performed
        /// - When an exclusive lock is successfully established, the lock is added to the list of managed locks
        ///   for demotion at disposal
        /// </remarks>
        public void LockDatasetSchema(ITable fc)
        {
            ResourceManager resourceManager = new ResourceManager(
                        "ESRI.ArcGIS.OSM.OSMClassExtension.OSMClassExtensionStrings", this.GetType().Assembly);
            
            ISchemaLock schemaLock = fc as ISchemaLock;
            if (schemaLock == null)
                throw new ArgumentNullException("schemaLock");

            // make sure that are not any existing exclusive locks
            IEnumSchemaLockInfo currentlyExistingLocks;
            try
            {
                schemaLock.GetCurrentSchemaLocks(out currentlyExistingLocks);
            }
            catch (Exception ex)
            {
                throw new ApplicationException(resourceManager.GetString("OSMClassExtensionManager_Reading_Lock_Exception"));
            }

            bool gdbAlreadyLockedbyUser = false;
            ISchemaLockInfo schemaLockInfo = null;
            while ((schemaLockInfo = currentlyExistingLocks.Next()) != null)
            {
                if (schemaLockInfo.SchemaLockType == esriSchemaLock.esriExclusiveSchemaLock
                    && !String.IsNullOrEmpty(schemaLockInfo.UserName))
                {
                    throw new ApplicationException(string.Format(
                        resourceManager.GetString("OSMClassExtensionManager_Exclusive_Lock_Exception"),
                        schemaLockInfo.TableName, schemaLockInfo.UserName));
                }
                else if (schemaLockInfo.SchemaLockType == esriSchemaLock.esriExclusiveSchemaLock)
                {
                    gdbAlreadyLockedbyUser = true;
                    break;
                }
            }

            if (!gdbAlreadyLockedbyUser)
            {
                schemaLock.ChangeSchemaLock(esriSchemaLock.esriExclusiveSchemaLock);
            }

            _managedLocks.Add(schemaLock);
        }

        /// <summary>Demote existing managed exclusive locks to shared locks</summary>
        public void Dispose()
        {
            foreach (ISchemaLock schemaLock in _managedLocks)
            {
                try
                {
                    if (schemaLock != null)
                        schemaLock.ChangeSchemaLock(esriSchemaLock.esriSharedSchemaLock);
                }
                catch
                {
                    // ignore lock demotion exception
                }
            }

            _managedLocks.Clear();
        }
    }
}
