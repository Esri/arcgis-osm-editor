// (c) Copyright ESRI, 2010 - 2012
// This source is subject to the Microsoft Public License (Ms-PL).
// Please see http://go.microsoft.com/fwlink/?LinkID=131993 for details.
// All other rights reserved.

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using OSMWeb.Models;
using System.Web.Routing;
using System.Text.RegularExpressions;
using System.Configuration;

namespace OSMWeb.Controllers
{
    public class OsmConfigsController : Controller
    {
        private OSMWebContext context = new OSMWebContext();

        //
        // GET: /OsmConfigs/
        public ViewResult Index()
        {
            return View(context.OsmConfigs.ToList());
        }

        //
        // GET: /OsmConfigs/Details/5
        public ViewResult Details(string id)
        {
            OsmConfig osmconfig = context.OsmConfigs.Single(x => x.ID == id);
            return View(osmconfig);
        }

        //
        // GET: /OsmConfigs/Create
        public ActionResult Create()
        {
            ViewBag.mxdList = OSMWeb.Utils.Config.GetListFromConfig("MxdList");
            ViewBag.refreshList = OSMWeb.Utils.Config.GetListFromConfig("RefreshInterval");

            return View();
        }

        //
        // POST: /OsmConfigs/Create
        [HttpPost]
        public ActionResult Create(OsmConfig osmconfig)
        {
            // Check if the first character is a number
            Regex regex = new Regex("[0-9]");
            string sFirstDataSetCharacter = osmconfig.FeatureDataSet.Substring(0, 1);
            if (regex.IsMatch(sFirstDataSetCharacter) == true)
            {
                ViewBag.mxdList = OSMWeb.Utils.Config.GetListFromConfig("MxdList");
                ViewBag.refreshList = OSMWeb.Utils.Config.GetListFromConfig("RefreshInterval");

                ViewBag.AlphaCheck = "DataSet name cannot begin with a number";
                return View(osmconfig);
            }

            if (osmconfig.Password == null)
                osmconfig.Password = "";
            if (osmconfig.Username == null)
                osmconfig.Username = "";

            osmconfig.LastTimeRunned = DateTime.Now;

            if (ModelState.IsValid)
            {
                osmconfig.ID = Guid.NewGuid().ToString();
                context.OsmConfigs.Add(osmconfig);
                context.SaveChanges();

                // Download the database
                return RedirectToAction("Index",
                    new RouteValueDictionary(
                        new { controller = "Download", action = "Index", DataBaseObjectID = osmconfig.ID }));

            }
            else
            {
                osmconfig.ID = Guid.NewGuid().ToString();
            }

            return View(osmconfig);
        }


        //
        // POST: /OsmConfigs/Edit/5
        [HttpPost]
        public ActionResult Edit(OsmConfig osmconfig)
        {
            if (ModelState.IsValid)
            {
                if (osmconfig.ID.Contains("test") == true)
                {
                    context.OsmConfigs.Add(osmconfig);
                    context.SaveChanges();

                    return RedirectToAction("Index",
                        new RouteValueDictionary(
                            new { controller = "Download", action = "Index", DataBaseObjectID = osmconfig.ID }));
                }
                else
                {
                    context.Entry(osmconfig).State = EntityState.Modified;
                    context.SaveChanges();
                    return RedirectToAction("Index");
                }
            }
            return View(osmconfig);
        }

        //
        // GET: /OsmConfigs/Delete/5 
        public ActionResult Delete(string id)
        {
            OsmConfig osmconfig = context.OsmConfigs.Single(x => x.ID == id);

            //ServicePublisher.Services service = new ServicePublisher.Services();
            //try { service.DeleteService(Request.Url.Host,
            //                            ConfigurationManager.AppSettings["ArcGISInstance"],
            //                            osmconfig.ID, 
            //                            osmconfig.FeatureDataSet, 
            //                            osmconfig.FeatureService, 
            //                            ConfigurationManager.AppSettings["MxdOutput"],
            //                            ConfigurationManager.AppSettings["DatabaseConnection"],
            //                            ConfigurationManager.AppSettings["AgsAdminUser"],
            //                            ConfigurationManager.AppSettings["AgsAdminPwd"],
            //                            ConfigurationManager.AppSettings["PythonScriptDir"]);
            //}
            //catch { }

            return View(osmconfig);
        }

        //
        // POST: /OsmConfigs/Delete/5
        [HttpPost, ActionName("Delete")]
        public ActionResult DeleteConfirmed(string id)
        {
            OsmConfig osmconfig = context.OsmConfigs.Single(x => x.ID == id);

            if (osmconfig.FeatureService != null)
            {
                ServicePublisher.Services service = new ServicePublisher.Services();
                try
                {
                    service.DeleteService(Request.Url.Host,
                                          ConfigurationManager.AppSettings["ArcGISInstance"],
                                          osmconfig.ID,
                                          osmconfig.FeatureDataSet,
                                          osmconfig.FeatureService,
                                          ConfigurationManager.AppSettings["MxdOutput"],
                                          ConfigurationManager.AppSettings["DatabaseConnection"],
                                          ConfigurationManager.AppSettings["AgsAdminUser"],
                                          ConfigurationManager.AppSettings["AgsAdminPwd"],
                                          ConfigurationManager.AppSettings["PythonScriptDir"]);
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
            context.OsmConfigs.Remove(osmconfig);
            context.SaveChanges();


            return RedirectToAction("Index");
        }


        public ActionResult EditFeatures(string id)
        {
            OsmConfig osmconfig = context.OsmConfigs.Single(x => x.ID == id);

            ServicePublisher.Services service = new ServicePublisher.Services();

            return RedirectToAction("Index",
                    new RouteValueDictionary(
                        new { controller = "Editor", action = "Index", FeatureService = osmconfig.FeatureService }));
        }
    }
}