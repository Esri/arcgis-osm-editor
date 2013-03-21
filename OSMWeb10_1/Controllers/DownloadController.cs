// (c) Copyright Esri, 2010 - 2013
// This source is subject to the Apache 2.0 License.
// Please see http://www.apache.org/licenses/LICENSE-2.0.html for details.
// All other rights reserved.

using System;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Web.Mvc;
using OSMWeb.Models;
using OSMWeb.Utils;
using System.Threading;
using System.Collections.Generic;
using ESRI.ArcGIS.OSM.OSMUtilities;

namespace OSMWeb.Controllers
{
    public class DownloadController : Controller
    {
        private OSMWebContext context = new OSMWebContext();
        bool bUseSDE = true;

        // GET: /Download/       
        public string JustDownload(OsmConfig myConfigToDownload)
        {
            string dbName = ConfigurationManager.AppSettings["DatabaseConnection"];
            string datasetName = System.IO.Path.Combine(dbName, myConfigToDownload.FeatureDataSet);

            AppLogs logs = new AppLogs();

            logs.AddLog("DOWNLOADLOGS", "Start Downloading");

            try
            {
                if (SyncState.CanSyncDataset(datasetName))
                {
                    SyncDataGP(myConfigToDownload);
                }
                else
                {
                    DownloadDataGP(myConfigToDownload);
                }
                return null;
            }
            catch (Exception e)
            {
                logs.AddLog("DOWNLOADLOGS", "ERROR: Just Download raised exception " + e.Message);
                return "ERROR: Just Download raised exception " + e.Message;
            }
            finally
            {
                logs.AddLog("DOWNLOADLOGS", "Finished Downloading");
            }
        }

        private void SyncDataGP(OsmConfig myConfigToDownload)
        {
            string sGpUrl = "";

            if (Request != null)
                sGpUrl = "http://" + Request.Url.Host + "/" + System.Configuration.ConfigurationManager.AppSettings["ArcGISInstance"].ToString() +
                    "/rest/services/OSM_on_AGS/GPServer/Sync%20OSM%20Data%20Serverside";
            else
            {
                string smyHost = myConfigToDownload.FeatureService.Substring(0, myConfigToDownload.FeatureService.ToLower().IndexOf("/arcgis/rest"));

                sGpUrl = smyHost + "/" + System.Configuration.ConfigurationManager.AppSettings["ArcGISInstance"].ToString() +
                    "/rest/services/OSM_on_AGS/GPServer/Sync%20OSM%20Data%20Serverside";
            }

            ESRI.ArcGIS.Client.Tasks.Geoprocessor geoprocessorTask = new
                ESRI.ArcGIS.Client.Tasks.Geoprocessor(sGpUrl);

            List<ESRI.ArcGIS.Client.Tasks.GPParameter> parameters = new List<ESRI.ArcGIS.Client.Tasks.GPParameter>();

            parameters.Add(new ESRI.ArcGIS.Client.Tasks.GPString("Start_Time_for_Diff_Files", ""));
            parameters.Add(new ESRI.ArcGIS.Client.Tasks.GPBoolean("Load_updates_only_inside_current_AOI", true));
            parameters.Add(new ESRI.ArcGIS.Client.Tasks.GPString("Name_of_Sync_Feature_Dataset", myConfigToDownload.FeatureDataSet));

            AppLogs logs = new AppLogs();
            ESRI.ArcGIS.Client.Tasks.JobInfo info = null;
            try
            {
                info = geoprocessorTask.SubmitJob(parameters);
            }
            catch (Exception e)
            {
                logs.AddLog("DOWNLOADLOGS", "SyncDataGP Exception " + e.Message);
                if (info != null)
                {
                    for (int i = 0; i < info.Messages.Count; i++)
                        logs.AddLog("DOWNLOADLOGS", "SyncDataGP Exception: SyncDataGP messages " + info.Messages[i].Description);
                }                    
            }

            while (info.JobStatus != ESRI.ArcGIS.Client.Tasks.esriJobStatus.esriJobSucceeded &&
                   info.JobStatus != ESRI.ArcGIS.Client.Tasks.esriJobStatus.esriJobFailed)
            {
                Thread.Sleep(2000);
                info = geoprocessorTask.CheckJobStatus(info.JobId);
            }

            if (info.JobStatus == ESRI.ArcGIS.Client.Tasks.esriJobStatus.esriJobFailed)
            {
                for (int i = 0; i < info.Messages.Count; i++)
                {
                    logs.AddLog("DOWNLOADLOGS", "JobFailed: SyncDataGP messages " + info.Messages[i].Description);
                }
                throw new ApplicationException("SyncDataGP JobFailed: Please view logs for details");
            }
        }


        /// <summary>
        /// Calling the rest end points for the GP
        /// </summary>
        /// <param name="myConfigToDownload"></param>
        private void DownloadDataGP(OsmConfig myConfigToDownload)
        {
            string sPort = ":6080";
            string sGpUrl = string.Empty;
            if (Request != null)
            {
                sGpUrl = "http://" + Request.Url.Host + sPort + "/" 
                    + System.Configuration.ConfigurationManager.AppSettings["ArcGISInstance"].ToString() 
                    + "/rest/services/OSM_on_AGS/GPServer/Download%20OSM%20Data%20Serverside";
            }
            else
            {
                string smyHost = myConfigToDownload.FeatureService.Substring(0, myConfigToDownload.FeatureService.ToLower().IndexOf("/arcgis/rest"));
                sGpUrl = smyHost + "/" + System.Configuration.ConfigurationManager.AppSettings["ArcGISInstance"].ToString() +
                    "/rest/services/OSM_on_AGS/GPServer/Download%20OSM%20Data%20Serverside";
            }

            ESRI.ArcGIS.Client.Tasks.Geoprocessor geoprocessorTask = new
                ESRI.ArcGIS.Client.Tasks.Geoprocessor(sGpUrl);

            List<ESRI.ArcGIS.Client.Tasks.GPParameter> parameters = new List<ESRI.ArcGIS.Client.Tasks.GPParameter>();

            string[] myExtent = myConfigToDownload.Extent.Split(',');

            ESRI.ArcGIS.Client.Geometry.PointCollection pointCollect = new ESRI.ArcGIS.Client.Geometry.PointCollection();

            // X1 Y1
            pointCollect.Add(new
                ESRI.ArcGIS.Client.Geometry.MapPoint(WebMercator.FromWebMercatorX(Convert.ToDouble(myExtent[0])),
                                    WebMercator.FromWebMercatorY(Convert.ToDouble(myExtent[1]))));
            // X2 Y1
            pointCollect.Add(new
                ESRI.ArcGIS.Client.Geometry.MapPoint(WebMercator.FromWebMercatorX(Convert.ToDouble(myExtent[2])),
                                    WebMercator.FromWebMercatorY(Convert.ToDouble(myExtent[1]))));
            // X2 Y2
            pointCollect.Add(new
                ESRI.ArcGIS.Client.Geometry.MapPoint(WebMercator.FromWebMercatorX(Convert.ToDouble(myExtent[2])),
                                    WebMercator.FromWebMercatorY(Convert.ToDouble(myExtent[3]))));
            // X1 Y2
            pointCollect.Add(new
                ESRI.ArcGIS.Client.Geometry.MapPoint(WebMercator.FromWebMercatorX(Convert.ToDouble(myExtent[0])),
                                    WebMercator.FromWebMercatorY(Convert.ToDouble(myExtent[3]))));
            // X1 Y1
            pointCollect.Add(new
                ESRI.ArcGIS.Client.Geometry.MapPoint(WebMercator.FromWebMercatorX(Convert.ToDouble(myExtent[0])),
                                    WebMercator.FromWebMercatorY(Convert.ToDouble(myExtent[1]))));

            ESRI.ArcGIS.Client.Geometry.Polygon polygon = new ESRI.ArcGIS.Client.Geometry.Polygon();
            polygon.Rings.Add(pointCollect);
            polygon.SpatialReference = new ESRI.ArcGIS.Client.Geometry.SpatialReference(4326);

            parameters.Add(new ESRI.ArcGIS.Client.Tasks.GPFeatureRecordSetLayer("Feature_Set", polygon));
            parameters.Add(new ESRI.ArcGIS.Client.Tasks.GPString("Name_of_OSM_Dataset", myConfigToDownload.FeatureDataSet));

            AppLogs logs = new AppLogs();
            ESRI.ArcGIS.Client.Tasks.GPExecuteResults results = null;
            ESRI.ArcGIS.Client.Tasks.JobInfo info = null;
            try
            {   
                //results = geoprocessorTask.Execute(parameters);

                info = geoprocessorTask.SubmitJob(parameters);
            }
            catch (Exception e)
            {
                logs.AddLog("DOWNLOADLOGS", "Exception " + e.Message);
                if ( info != null )
                {
                    for (int i = 0; i < info.Messages.Count; i++)
                        logs.AddLog("DOWNLOADLOGS", "Exception: DownloadDataGP messages " + info.Messages[i].Description);
                }

                //logs.AddLog("DOWNLOADLOGS", "JobWaiting  " + info.Messages[info.Messages.Count-1].Description);                    
            }

            while (info.JobStatus != ESRI.ArcGIS.Client.Tasks.esriJobStatus.esriJobSucceeded &&
                   info.JobStatus != ESRI.ArcGIS.Client.Tasks.esriJobStatus.esriJobFailed)
            {                
                Thread.Sleep(2000);
                info = geoprocessorTask.CheckJobStatus(info.JobId);
            }            

            if (info.JobStatus == ESRI.ArcGIS.Client.Tasks.esriJobStatus.esriJobFailed)
            {
                for (int i = 0; i < info.Messages.Count; i++)
                {
                    logs.AddLog("DOWNLOADLOGS", "JobFailed: DownloadDataGP messages " + info.Messages[i].Description);                    
                }
                throw new ApplicationException("JobFailed: Please view logs for details");
            }
        }



        private string SetTargetDatasetParamGP(OsmConfig myConfigToDownload)
        {
            string DatabaseConnectionConfig = ConfigurationManager.AppSettings["DatabaseConnection"];

            if (DatabaseConnectionConfig.ToLower().Contains(".gdb"))
                bUseSDE = false;

            // Check the database exist
            if (System.IO.File.Exists(DatabaseConnectionConfig) == false)
            {
                if (System.IO.Directory.Exists(DatabaseConnectionConfig) == false)
                {
                    ViewBag.TitleReturn = "The download cannot be completed";
                    ViewBag.Result = "Connection to the database does not exist";
                    return null;
                }
            }

            if (DatabaseConnectionConfig.ToLower().Contains(".sde") == true)
                DatabaseConnectionConfig += "\\";

            return DatabaseConnectionConfig + myConfigToDownload.FeatureDataSet;
        }



        public ActionResult Index(string DataBaseObjectID)
        {
            // Check if OSM is up
            //http://www.openstreetmap.org/api/0.6/map?bbox=-117.32410,33.89612,-117.32147,33.89794
            WebClient client = new WebClient();
            try
            {
                client.DownloadString("http://www.openstreetmap.org/api/0.6/map?bbox=-117.32410,33.89612,-117.32147,33.89794");
            }
            catch
            {

                ViewBag.Success = "OpenStreetMap is down, please try later";
                return View();
            }

            if (DataBaseObjectID != null)
            {
                // Get the configuration to download
                OsmConfig myConfigToDownload = null;
                foreach (OsmConfig config in context.OsmConfigs.ToList())
                {
                    if (config.ID == DataBaseObjectID)
                    {
                        myConfigToDownload = config;
                        break;
                    }
                }

                if (myConfigToDownload == null)
                {
                    ViewBag.Result = "Cannot find the configuration, aborted.";

                    return View();
                }

                // Download the configuration
                string results = JustDownload(myConfigToDownload);
                if (!(string.IsNullOrEmpty(results)))
                {
                    ViewBag.Result = results;

                    return View();
                }

                // publish 
                ServicePublisher.Services publisher = new ServicePublisher.Services();

                // Make sure to publish the correct host
                string sHostRequest = Request.Url.Host;

                // Publish 10.1
                myConfigToDownload.FeatureService = publisher.CreateService10_1(SetTargetDatasetParamGP(myConfigToDownload),
                                                    sHostRequest,
                                                    DataBaseObjectID.Replace("-", ""),
                                                    myConfigToDownload.MxdTemplate,
                                                    bUseSDE,
                                                    ConfigurationManager.AppSettings["MxdOutput"],
                                                    ConfigurationManager.AppSettings["AgsConnectionFile"],
                                                    ConfigurationManager.AppSettings["PythonScriptDir"]);               

                if (ConfigurationManager.AppSettings["HostAddress"] != null)
                {
                    if (ConfigurationManager.AppSettings["HostAddress"].ToString().Length > 0)
                        sHostRequest = ConfigurationManager.AppSettings["HostAddress"].ToString();

                    myConfigToDownload.FeatureService = myConfigToDownload.FeatureService.Replace(Request.Url.Host, sHostRequest);
                }

                //Override the instance of the server if needed
                if (ConfigurationManager.AppSettings["ArcGISServerInstanceName"] != null)
                {
                    if (ConfigurationManager.AppSettings["ArcGISServerInstanceName"].ToString().Length > 0)
                    {
                        myConfigToDownload.FeatureService = myConfigToDownload.FeatureService.Replace("/arcgis/", "/" + ConfigurationManager.AppSettings["ArcGISServerInstanceName"].ToString() + "/");
                    }
                }

                context.Entry(myConfigToDownload).State = System.Data.EntityState.Modified;
                context.SaveChanges();

                ViewBag.Result = myConfigToDownload.FeatureService;
                ViewBag.ResultMapServer = myConfigToDownload.FeatureService.Replace("/FeatureServer", "/MapServer");
                ViewBag.TitleReturn = "Download Complete";

                ViewBag.EditingUrl = Url.Content("~/Editor?FeatureService=" + myConfigToDownload.FeatureService);

                ViewBag.ArcGIS_Com = "http://arcgis.com/home/webmap/viewer.html?url=" + myConfigToDownload.FeatureService;
            }

            ViewBag.Success = "Successfully created the Feature Service by downloading the requested data from OpenStreetMap.";

            return View();
        }



    }
}
