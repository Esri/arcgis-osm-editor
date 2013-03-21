using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using OSMWeb.Models;
using System.Web.Routing;
using System.Web.Mvc.Async;
using System.Threading;

namespace OSMWeb.Controllers
{
    public class EditorController : Controller
    {
        private OSMWebContext context = new OSMWebContext();
        
        // GET: /Editor/
        public ActionResult Index(string FeatureService)
        {
            ViewBag.FeatureService = FeatureService;

            OsmConfig myConfigToDownload = null;
            foreach (OsmConfig config in context.OsmConfigs.ToList())
            {
                if (config.FeatureService == FeatureService)
                {
                    myConfigToDownload = config;
                    if (config.RefreshInterval > 0)
                    {
                        // We going to need it soon
                        ViewBag.ConfigID = config.ID;
                    }
                    break;
                }
            }

            if (myConfigToDownload != null)
            {
                // Setting up the name of the FeatureDataSet for the view.
                if (myConfigToDownload.RefreshInterval == 0)
                {
                    ViewBag.FeatureDataSet = myConfigToDownload.FeatureDataSet;
                }
            }

            return View();
        }

        
    }
}
