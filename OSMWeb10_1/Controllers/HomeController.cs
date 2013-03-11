using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;

using OSMWeb.Models;
using System.Configuration;
using System.Data.Entity;


namespace OSMWeb.Controllers
{
    public class HomeController : Controller
    {
        private OSMWebContext context = new OSMWebContext();
        
        public ActionResult Index()
        {
            ViewBag.Message = "ArcGIS for OSM";

            // Check the installation and if everything is fine, redirect it otherwise
            // display the message
            ViewBag.Error = "";

            //Check database connections
            if (System.IO.File.Exists(ConfigurationManager.AppSettings["DatabaseConnection"]) == false)
                ViewBag.Error += "DatabaseConnection cannot be found " + ConfigurationManager.AppSettings["DatabaseConnection"] + "\r\n";
            
            // Check all MXDS
            string [] allMxds = ConfigurationManager.AppSettings["MxdList"].Split(',');
            foreach (string item in allMxds)
            {
                string[] sMxdField = item.Split('|');
                if (System.IO.File.Exists(sMxdField[1]) == false)
                    ViewBag.Error += "MXD cannot be found " + sMxdField[1] + "\r\n";
            }

            //Check output directory
            if ( System.IO.Directory.Exists(ConfigurationManager.AppSettings["MxdOutput"]) == false)
                ViewBag.Error += "OutPut directory does not exist " + ConfigurationManager.AppSettings["MxdOutput"] + "\r\n";

#region 10.1 for future use
            // Code in progress for 10.1
            /*
            //AgsConnectionFile 
            if (ConfigurationManager.AppSettings["AgsConnectionFile"] != null)
            {
                if (ConfigurationManager.AppSettings["AgsConnectionFile"].ToString().ToLower() != "na")
                {
                    if (System.IO.File.Exists(ConfigurationManager.AppSettings["AgsConnectionFile"]) == false)
                        ViewBag.Error += "AgsConnectionFile does not exist " + ConfigurationManager.AppSettings["AgsConnectionFile"] + "\r\n";
                }
            }

            //Python Script
            if (ConfigurationManager.AppSettings["PythonScript"] != null)
            {
                if (ConfigurationManager.AppSettings["PythonScript"].ToString().ToLower() != "na")
                {
                    if (System.IO.File.Exists(ConfigurationManager.AppSettings["PythonScript"]) == false)
                        ViewBag.Error += "PythonScript does not exist " + ConfigurationManager.AppSettings["PythonScript"] + "\r\n";
                }
            }*/
#endregion         

            if (ViewBag.Error == "")
            {
                Utils.MvcRunner.CurrentUrl = this.Request.Url.ToString();

                try
                {
                    if (context.OsmConfigs.ToList().Count == 0)
                        return RedirectToAction("Create", "OsmConfigs");
                }
                catch
                {
                    // will recreate if need it
                    Database.SetInitializer<OSMWebContext>(new DropCreateDatabaseAlways<OSMWebContext>());
                }

                return RedirectToAction("Index", "OsmConfigs");
            }

            return View();
        }

        public ActionResult About()
        {
            return RedirectPermanent("http://esriosmeditor.codeplex.com/wikipage?title=ArcGIS%20Editor%20for%20OSM%20Feature%20Service%20");            
        }
    }
}
