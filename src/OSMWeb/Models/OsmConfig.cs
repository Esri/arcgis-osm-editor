using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Data.Entity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel;
using System.Web.Mvc;
using System.Configuration;

namespace OSMWeb.Models
{
    public class OsmConfig
    {        
        public string ID { get; set; }
        
        
        [DisplayName("Your OpenStreetMap username")]
        public string Username { get; set; }
        
        
        [DisplayName("Your OpenStreetMap password")]
        public string Password { get; set; }
        
        [Required]
        [DisplayName("Name the feature service:")]
        public string FeatureDataSet { get; set; }
        
        [Required]
        [DisplayName("Extent selected:")]        
        public string Extent { get; set; }
        public string FeatureService { get; set; }

        [DisplayName("Got a special template in mind?")]
        public string MxdTemplate { get; set; }
        [DisplayName("Refresh interval (minutes):")]
        public int RefreshInterval { get; set; }    //Minutes 

        public DateTime LastTimeRunned { get; set; }

        public bool ThreadStarted { get; set; }
    }

    public class OsmConfigContext : DbContext
    {
        public DbSet<OsmConfig> ConfigItem { get; set; }       
    }
}