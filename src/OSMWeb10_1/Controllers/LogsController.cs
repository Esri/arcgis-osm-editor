using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using OSMWeb.Models;

namespace OSMWeb.Controllers
{
    public class LogsController : Controller
    {
        //
        // GET: /Logs/
        public ActionResult Index()
        {           
            OSMLogsContext context = new OSMLogsContext();

            DeleteYesterday(context);

            return View(context.LogsItem.ToList());
        }

        public ActionResult DeleteAll()
        {
            AppLogs logs = new AppLogs();
            logs.DeleteAll();

            return RedirectToAction("Index");
        }

        public void DeleteYesterday(OSMLogsContext context)
        {
            DateTime dtNow = DateTime.Now.AddDays(-1);
            List<OSMLogs> items = (from c in context.LogsItem
                                   where c.When < dtNow
                                    select c).ToList();

            for (int i=0; i < items.Count; i++)
            {
                context.Entry(items[i]).State = System.Data.EntityState.Deleted;
            }

            context.SaveChanges();
        }

    }
}
