using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Configuration;

namespace OSMWeb.Utils
{
    public class Config
    {
        public static List<SelectListItem> GetListFromConfig(string key)
        {
            string[] sRet = ConfigurationManager.AppSettings[key].Split(',');

            var mxdList = new List<SelectListItem>();

            foreach (string sItem in sRet)
            {
                string[] sTemp = sItem.Split('|');
                mxdList.Add(new SelectListItem
                {
                    Text = sTemp[0],
                    Value = sTemp[1]
                });
            }
            
            return mxdList;
        }
    }
}