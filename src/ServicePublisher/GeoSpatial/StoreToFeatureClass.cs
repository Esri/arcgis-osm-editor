using System;
using System.Collections.Generic;
using System.Text;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Server;
using System.Runtime.InteropServices;

namespace ServicePublisher.GeoSpatial
{
    public sealed class StoreToFeatureClass : FeatureClassManager
    {
        private SdeServerInfo _info = null;

        public StoreToFeatureClass(SdeServerInfo info, string serverName, string mapService, string sContextMapServer, string sContextMapService)
            : base(info, serverName, mapService, sContextMapServer, sContextMapService)
        {
            _info = info;
        }



        /// <summary>
        /// Search to see if the feature class is there
        /// </summary>
        /// <param name="featureClass"></param>
        /// <param name="sWhereClause"></param>
        /// <returns></returns>
        public List<FeatureResults> SeachFeatureClass(string featureClass, string sWhereClause)
        {
            IFeatureClass featClass = GetFeatureClass(featureClass);

            if (featClass == null)
                return null;

            IQueryFilter queryFilter = (IQueryFilter)_serverContext.CreateObject("esriGeoDatabase.QueryFilter");
            queryFilter.WhereClause = sWhereClause;
            IFeatureCursor pFeatCursor = featClass.Search(queryFilter, true);

            // Get Directory Server info
            IEnumServerDirectoryInfo pEnumSDirInfo = _SOM.GetServerDirectoryInfos();
            IServerDirectoryInfo pSDirInfo = pEnumSDirInfo.Next();

            IFeature pFeature;
            List<FeatureResults> pFeatureCollection = new List<FeatureResults>();
            while ((pFeature = pFeatCursor.NextFeature()) != null)
            {
                FeatureResults Result = new FeatureResults();
                Result.Feature = pFeature;
                pFeatureCollection.Add(Result);
            }
                        
            return pFeatureCollection;
        }

        public void DeleteFeature(string featureClass, string whereClause)
        {
            Exception methodException = null;
            bool bError = false;
            IFeatureClass fc = GetFeatureClass(featureClass);

            if (fc == null)
            {
               
                return;
            }

            IWorkspaceEdit ipWksEdit = ((IDataset)fc).Workspace as IWorkspaceEdit;
            if (null != ipWksEdit)
            {
                try
                {
                    ipWksEdit.StartEditing(true);
                    ipWksEdit.StartEditOperation();
                }
                catch
                {
                    bError = true;
                    return;
                }
            }

            try
            {
                IQueryFilter query = (IQueryFilter)_serverContext.CreateObject("esriGeoDatabase.QueryFilter");
                query.WhereClause = whereClause;
                IFeatureCursor fcur = fc.Search(query, false);
                // Delete them all
                IFeature currentFeature = fcur.NextFeature();
                while (currentFeature != null)
                {
                    currentFeature.Delete();
                    currentFeature = fcur.NextFeature();
                }
                query = null;
               
            }
            catch (Exception e)
            {
                methodException = e;
                bError = true;
            }

            if (null != ipWksEdit)
            {
                try
                {
                    if (bError)
                    {
                        //rollback
                        ipWksEdit.UndoEditOperation();
                        ipWksEdit.StopEditing(false);
                    }
                    else
                    {
                        //commit
                        ipWksEdit.StopEditOperation();
                        ipWksEdit.StopEditing(true);
                    }
                }
                catch
                {
                }
                finally
                {
                   
                    ipWksEdit = null;
                }
            }

            if (methodException != null)
            {
               
                throw methodException;
            }
        }

        

        public new void Release()
        {
            //Releases the server context
            base.Release();
        }

        public void UpdateFeature(string featureClasses,                                    
                                    List<FeatureValues> featureValues,
                                    double[] Coordinates,
                                    string sID,
                                    string sFieldName)
        {            
            bool bError = false;
            IFeatureClass fc = GetFeatureClass(featureClasses);

            IWorkspaceEdit ipWksEdit = ((IDataset)fc).Workspace as IWorkspaceEdit;
            if (null != ipWksEdit)
            {
                try
                {
                    ipWksEdit.StartEditing(true);
                    ipWksEdit.StartEditOperation();
                }
                catch
                {
                    bError = true;
                    return;
                }
            }

            // the meat to update the feature
            foreach (FeatureValues featureValue in featureValues)
            {
                featureValue.Index = fc.Fields.FindField(featureValue.Name);
            }
                        
            IQueryFilter pQueryFilter = (IQueryFilter)_serverContext.CreateObject("esriGeoDatabase.QueryFilter");
            pQueryFilter.WhereClause = sFieldName + "='" +sID + "'";
            IFeatureCursor cursor = fc.Update(pQueryFilter, false);
            IFeature pFeature;
            while ((pFeature = cursor.NextFeature()) != null)
            {
                if (Coordinates != null && Coordinates.Length == 2)
                {
                    double[] points = Coordinates;
                    ESRI.ArcGIS.Geometry.IPoint pt = (ESRI.ArcGIS.Geometry.IPoint)_serverContext.CreateObject("esriGeometry.Point");
                    pt.PutCoords(points[0], points[1]);

                    pFeature.Shape = pt as ESRI.ArcGIS.Geometry.IPoint;
                }
                else if (Coordinates != null && Coordinates.Length > 2)
                {
                    ESRI.ArcGIS.Geometry.IPointCollection pPointCol = _serverContext.CreateObject("esriGeometry.Polygon") as ESRI.ArcGIS.Geometry.IPointCollection;
                    for (int i = 0; i < Coordinates.Length; i += 2)
                    {
                        ESRI.ArcGIS.Geometry.IPoint pt = (ESRI.ArcGIS.Geometry.IPoint)_serverContext.CreateObject("esriGeometry.Point");
                        pt.PutCoords(Coordinates[i], Coordinates[i + 1]);
                        pPointCol.AddPoints(1, ref pt);
                    }

                    pFeature.Shape = pPointCol as ESRI.ArcGIS.Geometry.IPolygon;
                }
                break;
            }

            pFeature.Store();

            if (null != ipWksEdit)
            {
                try
                {
                    if (bError)
                    {
                        //rollback
                        ipWksEdit.UndoEditOperation();
                        ipWksEdit.StopEditing(false);
                    }
                    else
                    {
                        //commit
                        ipWksEdit.StopEditOperation();
                        ipWksEdit.StopEditing(true);
                    }
                }
                catch
                {
                }
                finally
                {
                    Marshal.ReleaseComObject(ipWksEdit);
                    ipWksEdit = null;
                }
            }

            
        }
    }

    public class FeatureValues
    {
        public FeatureValues(string name, string svalue)
        {
            Name = name;
            Value = svalue;
            Index = -1;
        }

        public FeatureValues()
        {
            Index = -1;
        }

        public string Name;
        public object Value;

        public int Index;
    }

    public class FeatureResults
    {
        public IFeature Feature;
    }
}
