using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using OSMWeb.Models;
using System.Threading;
using System.Net;

namespace OSMWeb.Utils
{
    /// <summary>
    /// Long running process
    /// </summary>
    public class MvcRunner
    {
        public static string CurrentUrl { get; set; }

        

        public void RunAll()
        {
            AppLogs logs = new AppLogs();
            logs.AddLog("SYNCLOG", "Run All just called ");

            OSMWebContext privateContext = new OSMWebContext();
            // Read the database with Intervals values > than 0            
            foreach (OsmConfig config in privateContext.OsmConfigs.ToList())
            {
                if (config.RefreshInterval > 0 && config.FeatureService != null)
                {
                    privateContext.Entry(config).State = System.Data.EntityState.Modified;

                    if (config.LastTimeRunned.AddMinutes(config.RefreshInterval) < DateTime.Now)
                    {
                        // run one by one.
                        logs.AddLog("SYNCLOG", "Running " + config.FeatureDataSet + " last time runned " + config.LastTimeRunned.ToString());
                        RunOneSync(config);
                    }
                    else
                        logs.AddLog("SYNCLOG", "No need to run " + config.FeatureDataSet + " last time runned " + config.LastTimeRunned.ToString());

                    privateContext.SaveChanges();
                }
            }
        }

        

        private void RunOneSync(OsmConfig config)
        {
            try
            {
                // This Upload controller will also do a download
                Controllers.UploadController upload = new Controllers.UploadController();
                upload.Index(config.ID);
            }
            catch { }

            config.LastTimeRunned = RunnerLog.lastRun = DateTime.Now;
        }

    }

    public static class RunnerLog
    {
        public static List<string> Logs;
        public static DateTime lastRun;
    }
}