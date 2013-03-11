using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Text;

using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.esriSystem;



namespace ServicePublisher.MXD
{
  public class ArcObjects
  {

    public ArcObjects()
    {

    }

    private static LicenseInitializer m_AOLicenseInitializer;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="sShapeFile"></param>
    /// <param name="sShakeMapID"></param>
    /// <param name="sTargetFCName"></param>
    public bool ImportShapeFileToGeoDB(string sShapeFile, string sShakeMapID, string sTargetFCName, StringDictionary sdeSD)
    {
      return ImportShapeFileToGeoDB(sShapeFile, sShakeMapID, "", sTargetFCName, sdeSD);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="sShapeFile"></param>
    /// <param name="sShakeMapID"></param>
    /// <param name="sFileGDB"></param>
    /// <param name="sTargetFCName"></param>
    public bool ImportShapeFileToGeoDB(string sShapeFile, string sShakeMapID, string sFileGDB, string sTargetFCName, StringDictionary sdeSD)
    {
      IWorkspace pWS = null;
      IFeatureClass targetFC;
      ArcObjects pArcObjects = new ArcObjects();
      FileInfo fi;
      IFeatureClass sourceFC;
      IFeatureCursor sourceCursor = null;
            
      try
      {
        // Initialize License 
        m_AOLicenseInitializer = new LicenseInitializer();
        m_AOLicenseInitializer.InitializeApplication(new ESRI.ArcGIS.esriSystem.esriLicenseProductCode[] { ESRI.ArcGIS.esriSystem.esriLicenseProductCode.esriLicenseProductCodeEngine },
        new ESRI.ArcGIS.esriSystem.esriLicenseExtensionCode[] { });
                  
        // Get workspace from FGDB or SDE
        if (sFileGDB.Length > 0)
        {
          pWS = pArcObjects.FileGdbWorkspaceFromPath(sFileGDB);
        }
        else
        {
          IPropertySet pPropSet = new PropertySetClass();
          pPropSet.SetProperty("SERVER", sdeSD["SERVER"]);
          pPropSet.SetProperty("INSTANCE", sdeSD["INSTANCE"]);
          pPropSet.SetProperty("DATABASE", sdeSD["DATABASE"]);
          pPropSet.SetProperty("USER", sdeSD["USER"]);
          pPropSet.SetProperty("PASSWORD", sdeSD["PASSWORD"]);
          pPropSet.SetProperty("VERSION", sdeSD["VERSION"]);
          pWS = pArcObjects.ConnectToTransactionalVersion(pPropSet);
        }

        // Make sure we have workspace
        if (pWS == null)
        {          
          return false;
        }

        // Get target feature class
        targetFC = pArcObjects.GetFeatureClass(pWS, sTargetFCName);

        // Make sure we have target feature class
        if (targetFC == null)
        {          
          return false;
        }

        // Set FileInfo object to shape file
        fi = new FileInfo(sShapeFile);

        // Make sure we have fileInfo object
        if (fi == null)
        { 
          return false;
        }

        // Parse out ShapeFile name without extension
        string sShapeFileName = fi.Name.Substring(0, fi.Name.Length - (fi.Name.Length - fi.Name.LastIndexOf('.')));        
        // Get source featue class from shape file
        sourceFC = GetFeatureClassFromShapefileOnDisk(fi.DirectoryName, sShapeFileName);

        // Set source feature cursor.
        sourceCursor = sourceFC.Search(null, false);

        // Insert features
        InsertFeaturesUsingCursor(targetFC, sourceCursor, sShakeMapID);

        return true;
      }
      catch (Exception ex)
      {        
        return false;
      }
      finally
      {          
        System.Runtime.InteropServices.Marshal.ReleaseComObject(sourceCursor);
        sourceCursor = null;
        System.Runtime.InteropServices.Marshal.ReleaseComObject(pWS);
        pWS = null;
        //Do not make any call to ArcObjects after ShutDownApplication()
        if (m_AOLicenseInitializer != null) m_AOLicenseInitializer.ShutdownApplication();
        m_AOLicenseInitializer = null;
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
      }
    }
    
    // For example, path = @"C:\myData\myfGDB.gdb".
    public IWorkspace FileGdbWorkspaceFromPath(String path)
    {
      Type factoryType = Type.GetTypeFromProgID(
          "esriDataSourcesGDB.FileGDBWorkspaceFactory");
      IWorkspaceFactory workspaceFactory = (IWorkspaceFactory)Activator.CreateInstance
          (factoryType);
      return workspaceFactory.OpenFromFile(path, 0);
    }
    
    // For example, server = "Kona".// Database = "SDE" or "" if Oracle.// Instance = "5151".// User = "vtest".// Password = "go".// Version = "SDE.DEFAULT".
    public IWorkspace ConnectToTransactionalVersion(IPropertySet propertySet)
    {
      

      Type factoryType = Type.GetTypeFromProgID(
          "esriDataSourcesGDB.SdeWorkspaceFactory");
      IWorkspaceFactory workspaceFactory = (IWorkspaceFactory)Activator.CreateInstance
          (factoryType);
      return workspaceFactory.Open(propertySet, 0);
    }
    
    public IFeatureClass GetFeatureClass(IWorkspace WS, string sFCName)
    {

      IFeatureClass pFC = null;
      IFeatureWorkspace fWS;
      fWS = (IFeatureWorkspace)WS;
      try
      {
        pFC = fWS.OpenFeatureClass(sFCName);
        return pFC;
      }
      catch (Exception ex)
      {
        throw ex;
      }

    }

    /// <summary>
    /// Get the FeatureClass from a Shapefile on disk (hard drive).
    /// </summary>
    /// <param name="string_ShapefileDirectory">A System.String that is the directory where the shapefile is located. Example: "C:\data\USA"</param>
    /// <param name="string_ShapefileName">A System.String that is the shapefile name. Note: the shapefile extension's (.shp, .shx, .dbf, etc.) is not provided! Example: "States"</param>
    /// <returns>An IFeatureClass interface. Nothing (VB.NET) or null (C#) is returned if unsuccessful.</returns>
    /// <remarks></remarks>
    public ESRI.ArcGIS.Geodatabase.IFeatureClass GetFeatureClassFromShapefileOnDisk(System.String string_ShapefileDirectory, System.String string_ShapefileName)
    {

      System.IO.DirectoryInfo directoryInfo_check = null;
      try
      {
        directoryInfo_check = new System.IO.DirectoryInfo(string_ShapefileDirectory);
        if (directoryInfo_check.Exists)
        {

          //We have a valid directory, proceed

          System.IO.FileInfo fileInfo_check = new System.IO.FileInfo(string_ShapefileDirectory + "\\" + string_ShapefileName + ".shp");
          if (fileInfo_check.Exists)
          {

            //We have a valid shapefile, proceed

            ESRI.ArcGIS.Geodatabase.IWorkspaceFactory workspaceFactory = new ESRI.ArcGIS.DataSourcesFile.ShapefileWorkspaceFactoryClass();
            ESRI.ArcGIS.Geodatabase.IWorkspace workspace = workspaceFactory.OpenFromFile(string_ShapefileDirectory, 0);
            ESRI.ArcGIS.Geodatabase.IFeatureWorkspace featureWorkspace = (ESRI.ArcGIS.Geodatabase.IFeatureWorkspace)workspace; // Explict Cast
            ESRI.ArcGIS.Geodatabase.IFeatureClass featureClass = featureWorkspace.OpenFeatureClass(string_ShapefileName);

            return featureClass;
          }
          else
          {
            //Not valid shapefile
            return null;
          }

        }
        else
        {
          // Not valid directory
          return null;
        }
      }
      catch (Exception ex)
      {
        throw ex;
      }
      finally
      {
        directoryInfo_check = null;
      }
      

    }
    
    public void InsertFeaturesUsingCursor(IFeatureClass ipTargetFC, IFeatureCursor featuresToCopy, string sShakeMapID)
    {
      IFeatureCursor ipInsCursor = null;
      try
      {
        IFeatureBuffer ipFBuff = ipTargetFC.CreateFeatureBuffer();
        ipInsCursor = ipTargetFC.Insert(true);
        IFeature ipFeat;
        IFields ipTargetFields;
        IField ipTargetField;
        int featureOID;

        while ((ipFeat = featuresToCopy.NextFeature()) != null)
        {
          ipFBuff.Shape = ipFeat.ShapeCopy;

          ipTargetFields = ipTargetFC.Fields as IFields;
          for (int i = 0; i < ipTargetFields.FieldCount; i++)
          {
            ipTargetField = ipTargetFields.get_Field(i);

            //skip field that is not editable or is an OID field (OID field automatically being filled)
            if ((!ipTargetField.Editable) || (ipTargetField.Type == esriFieldType.esriFieldTypeOID)
                || ipTargetField.Type == ESRI.ArcGIS.Geodatabase.esriFieldType.esriFieldTypeGeometry)
            {
              continue;
            }

            //not geometry column, not subtype, and not OID or other read-only type
            string sFieldName = ipTargetField.Name;
            int iIndex = ipFeat.Fields.FindField(sFieldName);

            object oValue = null;

            //if the field exists in the srcFeatureCls and the types match, copy the value over
            if ((iIndex != -1) && (ipFeat.Fields.get_Field(iIndex).Type == ipTargetField.Type))
            {
              oValue = ipFeat.get_Value(iIndex);

              if (ipTargetField.CheckValue(oValue) == false)
              {
                // Source feature's value for this field is invalid for destination field
                oValue = ipTargetField.DefaultValue;
              }
            }
            //if the field doesn't exist, set default value for the field
            else
            {
              // Check if sShakeMapID field to populate it.
              if (sFieldName == "ShakeMapID")
              {
                oValue = sShakeMapID;
              }
              else
              {
                oValue = ipTargetField.DefaultValue;
              }
            }

            // assign the value, unless it's null and the field is not nullable
            if (((oValue != null) && (oValue.ToString() != "")) || ipTargetField.IsNullable)
            {
              ipFBuff.set_Value(i, oValue);
            }
          }

          featureOID = (int)ipInsCursor.InsertFeature(ipFBuff);

        }

        ipInsCursor.Flush();
        
      }
      catch (Exception ex)
      {
        throw ex;
      }
      finally
      {
        if (ipInsCursor != null)
        {
          System.Runtime.InteropServices.Marshal.ReleaseComObject(ipInsCursor);
          ipInsCursor = null;
        }
      }
    }
    
  }
}
