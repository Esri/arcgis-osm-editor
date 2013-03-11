using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using OSMWeb.Utils;

namespace OSMWeb.Controllers
{
    public class SyncNowController : Controller
    {
        //
        // GET: /SyncNow/
        public ActionResult Index()
        {
            MvcRunner runner = new MvcRunner();
            runner.RunAll();

            return View();
        }

    }
}
