using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Data.Entity;
using System.ComponentModel;

namespace OSMWeb.Models
{
    public class OSMLogs
    {
        public string ID { get; set; }

        [DisplayName("DateTime Now")]
        public DateTime When { get; set; }

        [DisplayName("Section Name")]
        public string SectionName { get; set; }

        [DisplayName("Description")]
        public string Description { get; set; }
    }

    public class OSMLogsContext : DbContext
    {
        public DbSet<OSMLogs> LogsItem { get; set; }
    }

    public class AppLogs
    {
        public void AddLogs(string sSectionName, string[] sDesriptions)        
        {
            try
            {
                OSMLogsContext logs = new OSMLogsContext();

                foreach (string sDescription in sDesriptions)
                {
                    OSMLogs item = new OSMLogs()
                    {
                        Description = sDescription,
                        SectionName = sSectionName,
                        When = DateTime.Now,
                        ID = Guid.NewGuid().ToString()
                    };
                    logs.LogsItem.Add(item);
                }

                logs.SaveChanges();
            }
            catch { }
        }

        public void AddLog(string sSectionName, string sDescription)
        {
            try
            {
                OSMLogsContext logs = new OSMLogsContext();

                OSMLogs item = new OSMLogs()
                {
                    Description = sDescription,
                    SectionName = sSectionName,
                    When = DateTime.Now,
                    ID = Guid.NewGuid().ToString()
                };

                logs.LogsItem.Add(item);
                logs.SaveChanges();
            }
            catch { }
        }

        public void DeleteAll()
        {
            OSMLogsContext context = new OSMLogsContext();

            var tempLog = context.LogsItem.ToList();
            tempLog.ForEach(item => context.LogsItem.Remove(item));            
            context.SaveChanges();
        }

        public void Trim()
        {

        }
    }
}