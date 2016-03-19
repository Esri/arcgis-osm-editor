// (c) Copyright Esri, 2010 - 2016
// This source is subject to the Apache 2.0 License.
// Please see http://www.apache.org/licenses/LICENSE-2.0.html for details.
// All other rights reserved.


using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using ESRI.ArcGIS.GeoprocessingUI;
using System.Security.Permissions;
using System.Resources;
using System.Security;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.OSM.GeoProcessing;

namespace ESRI.ArcGIS.OSM.Editor
{
    [ClassInterface(ClassInterfaceType.None)]
    [Guid("F483BDB7-DBA1-45CA-A31E-665422FD2460")]
    [ProgId("OSMEditor.BasicAuthenticationCtrl")]
    public partial class BasicAuthenticationCtrl : UserControl, IMdElementCtrl
    {
        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        private static extern int SetParent(int hWndChild, int hWndNewParent);

        #region ActiveX Control Registration

        // These routines perform the additional COM registration needed by 
        // ActiveX controls

        [EditorBrowsable(EditorBrowsableState.Never)]
        [ComRegisterFunction()]
        public static void Register(Type t)
        {
            try
            {
                ActiveXCtrlHelper.RegasmRegisterControl(t);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message); // Log the error
                throw ex; // Re-throw the exception
            }
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        [ComUnregisterFunction()]
        public static void Unregister(Type t)
        {
            try
            {
                ActiveXCtrlHelper.RegasmUnregisterControl(t);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message); // Log the error
                throw ex; // Re-throw the exception
            }
        }

        #endregion

        ResourceManager resourceManager = null;
        string m_propname = null;
        string m_dependencies = null;
        IHttpBasicGPValue m_httpBasicGPValue = null;
        string m_username = null;
        string m_password = null;

        public BasicAuthenticationCtrl()
        {
            try
            {
                this.Font = SystemFonts.MessageBoxFont;
                InitializeComponent();

                resourceManager = new ResourceManager("ESRI.ArcGIS.OSM.Editor.OSMFeatureInspectorStrings", this.GetType().Assembly);

                lblUserName.Text = resourceManager.GetString("OSMEditor_Authentication_UI_labelusername");
                lblPassword.Text = resourceManager.GetString("OSMEditor_Authentication_UI_labelpassword");

                txtUserName.LostFocus += new EventHandler(txtUserName_LostFocus);
                txtUserName.Refresh();
                txtPassword.LostFocus += new EventHandler(txtPassword_LostFocus);
                txtPassword.Refresh();

                m_password = String.Empty;
                m_username = String.Empty;

                this.Refresh();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
            }
        }

        IMdElementEditor m_MdElementEditor = null;

        protected override void OnLocationChanged(EventArgs e)
        {
            // not sure why this is required and/or why the height of the control is set to 0
            //
            int requiredHeight = 0;
            foreach (Control c in this.Controls)
            {
                requiredHeight = requiredHeight + c.Height + c.Margin.Bottom + c.Margin.Top;
            }
            this.Height = requiredHeight;

            base.OnLocationChanged(e);
        }

        [SecurityPermission(SecurityAction.LinkDemand, Flags = SecurityPermissionFlag.UnmanagedCode)]
        protected override void WndProc(ref System.Windows.Forms.Message m)
        {
            try
            {
                const int WM_SETFOCUS = 0x7;
                const int WM_PARENTNOTIFY = 0x210;
                const int WM_DESTROY = 0x2;
                const int WM_LBUTTONDOWN = 0x201;
                const int WM_RBUTTONDOWN = 0x204;
                const int WM_CREATE = 0x0001;
                const int WM_SHOWWINDOW = 0x0018;

                if (m.Msg == WM_CREATE)
                {

                }
                else if (m.Msg == WM_SHOWWINDOW && m.WParam.ToInt32() == 0)
                {
                }
                else if (m.Msg == WM_SETFOCUS)
                {
                    // Raise Enter event
                    this.OnEnter(System.EventArgs.Empty);
                }
                else if (m.Msg == WM_PARENTNOTIFY && (
                    m.WParam.ToInt32() == WM_LBUTTONDOWN ||
                    m.WParam.ToInt32() == WM_RBUTTONDOWN))
                {
                    if (!this.ContainsFocus)
                    {
                        // Raise Enter event
                        this.OnEnter(System.EventArgs.Empty);
                    }
                }
                else if (m.Msg == WM_DESTROY &&
                    !this.IsDisposed && !this.Disposing)
                {
                    // Used to ensure the cleanup of the control
                    this.Dispose();
                }

                base.WndProc(ref m);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
            }
        }

        private void ApplyUIChanges()
        {
            try
            {
                if (m_httpBasicGPValue != null)
                {
                    m_httpBasicGPValue.UserName = m_username;
                    m_httpBasicGPValue.PassWord = m_password;
                }

                if (m_MdElementEditor != null)
                {
                    m_MdElementEditor.SetValue(m_propname, (IGPValue)m_httpBasicGPValue);
                    m_MdElementEditor.OnElementChanged();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
            }
        }

        void txtUserName_LostFocus(object sender, EventArgs e)
        {
            try
            {
                m_username = txtUserName.Text;

                ApplyUIChanges();
            }
            catch
            {
            }
        }

        void txtPassword_LostFocus(object sender, EventArgs e)
        {
            try
            {
                m_password = txtPassword.Text;

                ApplyUIChanges();
            }
            catch
            {
            }
        }


        
        private void DecodeUserNamePassword(string value)
        {
            if (value == null)
                return;

            try
            {
                string authInfo = Encoding.Default.GetString(Convert.FromBase64String(value));
                string[] splitAuthInfo = authInfo.Split(":".ToCharArray());

                m_username = splitAuthInfo[0];
                m_password = splitAuthInfo[1];

                splitAuthInfo = null;
                authInfo = null;
            }
            catch
            {
                m_username = String.Empty;
                m_password = String.Empty;
            }
        }

        private string EncodeUserAuthentication(string m_username, string m_password)
        {
            string encodedAuthenticatioString = String.Empty;

            try
            {
                string authInfo = m_username + ":" + m_password;
                authInfo = Convert.ToBase64String(Encoding.Default.GetBytes(authInfo));

                encodedAuthenticatioString = authInfo;
            }
            catch { }

            return encodedAuthenticatioString;
        }

        #region IMdElementCtrl Members

        void IMdElementCtrl.Initialize(IMdElementEditor editor, string propname, string dependencies)
        {
            try
            {
                m_MdElementEditor = editor;
                m_propname = propname;
                m_dependencies = dependencies;

                ((IMdElementCtrl)this).Refresh();

                Show();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
            }
        }

        void IMdElementCtrl.Refresh()
        {
            try
            {
                if (m_MdElementEditor == null)
                    return;

                if (m_MdElementEditor.GetEnabled(m_propname) == false)
                {
                    this.Enabled = false;
                    return;
                }

                m_httpBasicGPValue = m_MdElementEditor.GetValue(m_propname) as IHttpBasicGPValue;

                if (m_httpBasicGPValue == null)
                {
                    return;
                }

                string authenticationString = m_httpBasicGPValue.EncodedUserNamePassWord;

                DecodeUserNamePassword(authenticationString);

                if (m_username != null)
                {
                    txtUserName.Text = m_username;
                }

                if (m_password != null)
                {
                    txtPassword.Text = m_password;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
            }
        }

        void IMdElementCtrl.Uninitialize()
        {
            try
            {
                m_MdElementEditor = null;
                m_httpBasicGPValue = null;
                m_propname = null;
                m_dependencies = null;
                m_password = String.Empty;
                m_username = String.Empty;

                Hide();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.Message);
                System.Diagnostics.Debug.WriteLine(ex.StackTrace);
            }
        }

        #endregion

        private void BasicAuthenticationCtrl_Leave(object sender, EventArgs e)
        {

        }

    }
}
