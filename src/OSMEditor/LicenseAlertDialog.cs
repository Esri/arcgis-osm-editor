// (c) Copyright Esri, 2010 - 2016
// This source is subject to the Apache 2.0 License.
// Please see http://www.apache.org/licenses/LICENSE-2.0.html for details.
// All other rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Resources;
using System.Runtime.InteropServices;

namespace ESRI.ArcGIS.OSM.Editor
{
    [ComVisible(false)]
    [ProgId("OSMEditor.LicenseAlertDialog")]
    [Guid("D4FE086B-2E31-4BC5-82E9-8F8B6F4A2478")]
    public partial class LicenseAlertDialog : Form
    {
        private ResourceManager resourceManager;

        public LicenseAlertDialog()
        {
            InitializeComponent();

            resourceManager = new ResourceManager("ESRI.ArcGIS.OSM.Editor.OSMFeatureInspectorStrings", this.GetType().Assembly);

            btnOK.Text = resourceManager.GetString("OSMEditor_LicenseAlertDialog_OK");
            btnCancel.Text = resourceManager.GetString("OSMEditor_LicenseAlertDialog_Cancel");
            this.Text = resourceManager.GetString("OSMEditor_LicenseAlertDialog_Title");
            txtLicenseAlert.Text = resourceManager.GetString("OSMEditor_LicenseAlertDialog_LicenseAlert");
            linkLabel1.Text = resourceManager.GetString("OSMEditor_LicenseAlertDialog_LicenseAlert");
            linkLabel1.LinkClicked += new LinkLabelLinkClickedEventHandler(linkLabel1_LinkClicked);

            string dd = resourceManager.GetString("OSMEditor_LicenseAlertDialog_LicenseAlertLink");

            string[] linkblock = dd.Split(";".ToCharArray());

            for (int linkBlockIndex = 0; linkBlockIndex < linkblock.Length; linkBlockIndex++)
            {
                string[] linkElements = linkblock[linkBlockIndex].Split(",".ToCharArray());

                linkLabel1.Links.Add(Convert.ToInt32(linkElements[0]), Convert.ToInt32(linkElements[1]), linkElements[2]);
            }
        }

        void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            // Determine which link was clicked within the LinkLabel.
            this.linkLabel1.Links[linkLabel1.Links.IndexOf(e.Link)].Visited = true;

            // Display the appropriate link based on the value of the 
            // LinkData property of the Link object.
            string target = e.Link.LinkData as string;

            // If the value looks like a URL, navigate to it.
            if (null != target && target.StartsWith("http://"))
            {
                System.Diagnostics.Process.Start(target);
            }
        }
    }
}
