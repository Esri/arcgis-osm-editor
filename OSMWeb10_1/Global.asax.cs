using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Routing;
using ESRI.ArcGIS;
using System.Data.Entity;
using System.Data.Entity.Infrastructure;

namespace OSMWeb
{
    // Note: For instructions on enabling IIS6 or IIS7 classic mode, 
    // visit http://go.microsoft.com/?LinkId=9394801
        
    public class MvcApplication : System.Web.HttpApplication
    {
        //OSMWeb.Utils.MvcRunner runner = null;

        public static void RegisterGlobalFilters(GlobalFilterCollection filters)
        {
            filters.Add(new HandleErrorAttribute());
        }

        public static void RegisterRoutes(RouteCollection routes)
        {
            routes.IgnoreRoute("{resource}.axd/{*pathInfo}");

            routes.MapRoute(
                "Default", // Route name
                "{controller}/{action}/{id}", // URL with parameters
                new { controller = "Home", action = "Index", id = UrlParameter.Optional } // Parameter defaults
            );

            routes.MapRoute(
                "Download",
                "{controller}/{action}/{id}",
                new { controller = "Download", action = "Index", id = UrlParameter.Optional }
                );

        }

        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();

            RegisterGlobalFilters(GlobalFilters.Filters);
            RegisterRoutes(RouteTable.Routes);

            // Entity Framework, code first 
            Database.DefaultConnectionFactory = new SqlCeConnectionFactory("System.Data.SqlServerCe.4.0");
           

            /*if (runner == null)
            {   
                runner = new Utils.MvcRunner();
                runner.IsRunning();
            }*/
        }   
        
    }
}