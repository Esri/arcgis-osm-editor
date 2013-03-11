namespace ESRI.ArcGIS.OSM.Editor
{
    partial class OSMEditorPropertyPage
    {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(OSMEditorPropertyPage));
            this.txtOSMBaseURL = new System.Windows.Forms.TextBox();
            this.lblBaseURL = new System.Windows.Forms.Label();
            this.txtOSMDomainFileLocation = new System.Windows.Forms.TextBox();
            this.txtOSMFeaturePropertiesFileLocation = new System.Windows.Forms.TextBox();
            this.btnFeatureDomains = new System.Windows.Forms.Button();
            this.btnFeatureProperties = new System.Windows.Forms.Button();
            this.lblFeatureDomains = new System.Windows.Forms.Label();
            this.lblFeatureProperties = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // txtOSMBaseURL
            // 
            this.txtOSMBaseURL.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.txtOSMBaseURL.Location = new System.Drawing.Point(108, 23);
            this.txtOSMBaseURL.Name = "txtOSMBaseURL";
            this.txtOSMBaseURL.Size = new System.Drawing.Size(342, 20);
            this.txtOSMBaseURL.TabIndex = 0;
            this.txtOSMBaseURL.TextChanged += new System.EventHandler(this.textBox1_TextChanged);
            // 
            // lblBaseURL
            // 
            this.lblBaseURL.AutoSize = true;
            this.lblBaseURL.Location = new System.Drawing.Point(3, 26);
            this.lblBaseURL.Name = "lblBaseURL";
            this.lblBaseURL.Size = new System.Drawing.Size(85, 13);
            this.lblBaseURL.TabIndex = 1;
            this.lblBaseURL.Text = "OSM base URL:";
            // 
            // txtOSMDomainFileLocation
            // 
            this.txtOSMDomainFileLocation.Location = new System.Drawing.Point(108, 66);
            this.txtOSMDomainFileLocation.Name = "txtOSMDomainFileLocation";
            this.txtOSMDomainFileLocation.Size = new System.Drawing.Size(310, 20);
            this.txtOSMDomainFileLocation.TabIndex = 1;
            this.txtOSMDomainFileLocation.TextChanged += new System.EventHandler(this.FileLocation_TextChanged);
            // 
            // txtOSMFeaturePropertiesFileLocation
            // 
            this.txtOSMFeaturePropertiesFileLocation.Location = new System.Drawing.Point(108, 111);
            this.txtOSMFeaturePropertiesFileLocation.Name = "txtOSMFeaturePropertiesFileLocation";
            this.txtOSMFeaturePropertiesFileLocation.Size = new System.Drawing.Size(310, 20);
            this.txtOSMFeaturePropertiesFileLocation.TabIndex = 3;
            this.txtOSMFeaturePropertiesFileLocation.TextChanged += new System.EventHandler(this.FileLocation_TextChanged);
            // 
            // btnFeatureDomains
            // 
            this.btnFeatureDomains.Image = ((System.Drawing.Image)(resources.GetObject("btnFeatureDomains.Image")));
            this.btnFeatureDomains.Location = new System.Drawing.Point(426, 62);
            this.btnFeatureDomains.Name = "btnFeatureDomains";
            this.btnFeatureDomains.Size = new System.Drawing.Size(24, 24);
            this.btnFeatureDomains.TabIndex = 2;
            this.btnFeatureDomains.UseVisualStyleBackColor = true;
            this.btnFeatureDomains.Click += new System.EventHandler(this.btnFeatureDomains_Click);
            // 
            // btnFeatureProperties
            // 
            this.btnFeatureProperties.Image = ((System.Drawing.Image)(resources.GetObject("btnFeatureProperties.Image")));
            this.btnFeatureProperties.Location = new System.Drawing.Point(426, 108);
            this.btnFeatureProperties.Name = "btnFeatureProperties";
            this.btnFeatureProperties.Size = new System.Drawing.Size(24, 24);
            this.btnFeatureProperties.TabIndex = 4;
            this.btnFeatureProperties.UseVisualStyleBackColor = true;
            this.btnFeatureProperties.Click += new System.EventHandler(this.btnFeatureDomains_Click);
            // 
            // lblFeatureDomains
            // 
            this.lblFeatureDomains.AutoSize = true;
            this.lblFeatureDomains.Location = new System.Drawing.Point(3, 69);
            this.lblFeatureDomains.Name = "lblFeatureDomains";
            this.lblFeatureDomains.Size = new System.Drawing.Size(78, 13);
            this.lblFeatureDomains.TabIndex = 6;
            this.lblFeatureDomains.Text = "OSM Domains:";
            // 
            // lblFeatureProperties
            // 
            this.lblFeatureProperties.AutoSize = true;
            this.lblFeatureProperties.Location = new System.Drawing.Point(3, 114);
            this.lblFeatureProperties.Name = "lblFeatureProperties";
            this.lblFeatureProperties.Size = new System.Drawing.Size(78, 13);
            this.lblFeatureProperties.TabIndex = 7;
            this.lblFeatureProperties.Text = "OSM Features:";
            // 
            // OSMEditorPropertyPage
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.SystemColors.Window;
            this.Controls.Add(this.lblFeatureProperties);
            this.Controls.Add(this.lblFeatureDomains);
            this.Controls.Add(this.lblBaseURL);
            this.Controls.Add(this.txtOSMBaseURL);
            this.Controls.Add(this.txtOSMDomainFileLocation);
            this.Controls.Add(this.btnFeatureDomains);
            this.Controls.Add(this.txtOSMFeaturePropertiesFileLocation);
            this.Controls.Add(this.btnFeatureProperties);
            this.Name = "OSMEditorPropertyPage";
            this.Size = new System.Drawing.Size(464, 169);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox txtOSMBaseURL;
        private System.Windows.Forms.Label lblBaseURL;
        private System.Windows.Forms.TextBox txtOSMDomainFileLocation;
        private System.Windows.Forms.TextBox txtOSMFeaturePropertiesFileLocation;
        private System.Windows.Forms.Button btnFeatureDomains;
        private System.Windows.Forms.Button btnFeatureProperties;
        private System.Windows.Forms.Label lblFeatureDomains;
        private System.Windows.Forms.Label lblFeatureProperties;




    }
}
