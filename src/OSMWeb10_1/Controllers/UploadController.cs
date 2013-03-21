using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Runtime.InteropServices;
using OSMWeb.Models;
using System.Configuration;
using System.Threading;

namespace OSMWeb.Controllers
{
    public class UploadController : Controller
    {        
        private OSMWebContext context = new OSMWebContext();

        // GET: /Upload/
        public ActionResult Index(string DataBaseObjectID)
        {
            OsmConfig myConfigToDownload = null;
            foreach (OsmConfig config in context.OsmConfigs.ToList())
            {
                if (config.ID == DataBaseObjectID)
                {
                    myConfigToDownload = config;
                    break;
                }
            }
                        
            DoUploadGP(myConfigToDownload.Username, myConfigToDownload.Password, myConfigToDownload.FeatureDataSet, myConfigToDownload);

            // Download again the same extent
            DownloadController download = new DownloadController();
            download.JustDownload(myConfigToDownload);

            return View();
        }

        // GET /Upload/
        public ActionResult Upload(string sUsername, string sPassword, string sFeatureDataSet)
        {            
            DoUploadGP(sUsername, sPassword, sFeatureDataSet, null);

            return View();
        }

        /// <summary>
        /// Do upload using the ArcGIS GP
        /// </summary>
        /// <param name="sUsername"></param>
        /// <param name="sPassword"></param>
        /// <param name="sFeatureDataSet"></param>
        public void DoUploadGP(string sUsername, string sPassword, string sFeatureDataSet, OsmConfig myConfigToDownload)
        {
            string sGpUrl;

            if (Request != null)
                sGpUrl = "http://" + Request.Url.Host + ":6080/" + System.Configuration.ConfigurationManager.AppSettings["ArcGISInstance"].ToString() + 
                    "/rest/services/OSM_on_AGS/GPServer/Upload%20OSM%20Data%20Serverside";
            else if ( myConfigToDownload != null )
            {
                string smyHost = myConfigToDownload.FeatureService.Substring(0, myConfigToDownload.FeatureService.ToLower().IndexOf("/arcgis/rest"));
                sGpUrl = smyHost + "/" + System.Configuration.ConfigurationManager.AppSettings["ArcGISInstance"].ToString() +
                    "/rest/services/OSM_on_AGS/GPServer/Upload%20OSM%20Data%20Serverside";
            }
            else
                sGpUrl = "http://localhost:6080/ArcGIS/rest/services/OSM_on_AGS/GPServer/Upload%20OSM%20Data%20Serverside";

        
            ESRI.ArcGIS.Client.Tasks.Geoprocessor geoprocessorTask = new
                ESRI.ArcGIS.Client.Tasks.Geoprocessor(sGpUrl);

            List<ESRI.ArcGIS.Client.Tasks.GPParameter> parameters = new List<ESRI.ArcGIS.Client.Tasks.GPParameter>();

            string sCredentials = OSMWeb.Utils.StringManipulation.EncodeTo64(sUsername + ":" + sPassword);
            parameters.Add(new ESRI.ArcGIS.Client.Tasks.GPString("OSM_login_credentials", sCredentials));
            parameters.Add(new ESRI.ArcGIS.Client.Tasks.GPString("Name_of_Upload_Feature_Dataset", sFeatureDataSet));
            parameters.Add(new 
                ESRI.ArcGIS.Client.Tasks.GPString("Comment_describing_the_upload_content",
                System.Configuration.ConfigurationManager.AppSettings["UploadComment"].ToString() + 
                sFeatureDataSet + " at " + DateTime.Now.ToString()));

            AppLogs logs = new AppLogs();
            ESRI.ArcGIS.Client.Tasks.JobInfo info = null;
            try
            {
                info = geoprocessorTask.SubmitJob(parameters);
            }
            catch (Exception e)
            {
                logs.AddLog("DOWNLOADLOGS", "DoUploadGP Exception " + e.Message);
                if (info != null)
                {
                    for (int i = 0; i < info.Messages.Count; i++)
                        logs.AddLog("DOWNLOADLOGS", "DoUploadGP Exception: DoUploadGP messages " + info.Messages[i].Description);
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
                    logs.AddLog("DOWNLOADLOGS", "JobFailed: DoUploadGP messages " + info.Messages[i].Description);
                }
                throw new ApplicationException("DoUploadGP JobFailed: Please view logs for details");
            }
        }
        
        
    }
}
