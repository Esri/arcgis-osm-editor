// (c) Copyright Esri, 2010 - 2016
// This source is subject to the Apache 2.0 License.
// Please see http://www.apache.org/licenses/LICENSE-2.0.html for details.
// All other rights reserved.

using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;
using System.Resources;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geoprocessing;
using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.Display;

namespace ESRI.ArcGIS.OSM.GeoProcessing
{
    [Guid("dd42d75c-abd2-4925-9886-75db3ad55d78")]
    [ClassInterface(ClassInterfaceType.None)]
    [ProgId("OSMEditor.GPCopyLayerExtensions")]
    [ComVisible(false)]
    public class GPCopyLayerExtensions : ESRI.ArcGIS.Geoprocessing.IGPFunction2
    {

        string m_DisplayName = "Copy Layer Extensions";
        int in_sourcelayerNumber, in_targetlayerNumber, out_layerNumber;
        ResourceManager resourceManager = null;
        OSMGPFactory osmGPFactory = null;

        public GPCopyLayerExtensions()
        {
            try
            {
                resourceManager = new ResourceManager("ESRI.ArcGIS.OSM.GeoProcessing.OSMGPToolsStrings", this.GetType().Assembly);
                
                m_DisplayName = String.Empty;
                osmGPFactory = new OSMGPFactory();
            }
            catch { }
        }



        #region "IGPFunction2 Implementations"
        public ESRI.ArcGIS.esriSystem.UID DialogCLSID
        {
            get
            {
                return default(ESRI.ArcGIS.esriSystem.UID);
            }
        }

        public string DisplayName
        {
            get
            {
                if (String.IsNullOrEmpty(m_DisplayName))
                {
                    m_DisplayName = osmGPFactory.GetFunctionName(OSMGPFactory.m_CopyLayerExtensionName).DisplayName;
                }

                return m_DisplayName;
            }
        }

        public void Execute(ESRI.ArcGIS.esriSystem.IArray paramvalues, ESRI.ArcGIS.esriSystem.ITrackCancel TrackCancel, ESRI.ArcGIS.Geoprocessing.IGPEnvironmentManager envMgr, ESRI.ArcGIS.Geodatabase.IGPMessages message)
        {
            try
            {
                IGPUtilities3 gpUtilities3 = new GPUtilitiesClass();

                if (TrackCancel == null)
                {
                    TrackCancel = new CancelTrackerClass();
                }

                IGPParameter inputSourceLayerParameter = paramvalues.get_Element(in_sourcelayerNumber) as IGPParameter;
                IGPValue inputSourceLayerGPValue = gpUtilities3.UnpackGPValue(inputSourceLayerParameter) as IGPValue;

                IGPParameter inputTargetLayerParameter = paramvalues.get_Element(in_targetlayerNumber) as IGPParameter;
                IGPValue inputTargetLayerGPValue = gpUtilities3.UnpackGPValue(inputTargetLayerParameter) as IGPValue;

                ILayer sourceLayer = gpUtilities3.DecodeLayer(inputSourceLayerGPValue);
                ILayer targetLayer = gpUtilities3.DecodeLayer(inputTargetLayerGPValue);

                ILayerExtensions sourceLayerExtensions = sourceLayer as ILayerExtensions;

                if (sourceLayerExtensions == null)
                {
                    message.AddWarning(resourceManager.GetString("GPTools_GPCopyLayerExtension_source_noext_support"));
                    return;
                }

                ILayerExtensions targetLayerExtensions = targetLayer as ILayerExtensions;

                if (targetLayerExtensions == null)
                {
                    message.AddWarning(resourceManager.GetString("GPTools_GPCopyLayerExtension_target_noext_support"));
                    return;
                }

                // test if the feature classes already exists, 
                // if they do and the environments settings are such that an overwrite is not allowed we need to abort at this point
                IGeoProcessorSettings gpSettings = (IGeoProcessorSettings)envMgr;
                if (gpSettings.OverwriteOutput == true)
                {
                }

                else
                {
                    if (gpUtilities3.Exists(inputTargetLayerGPValue) == true)
                    {
                        message.AddError(120003, String.Format(resourceManager.GetString("GPTools_GPCopyLayerExtension_targetlayeralreadyexists"), inputTargetLayerGPValue.GetAsText()));
                        return;
                    }
                }

                for (int targetExtensionIndex = 0; targetExtensionIndex < targetLayerExtensions.ExtensionCount; targetExtensionIndex++)
                {
                    targetLayerExtensions.RemoveExtension(targetExtensionIndex);
                }


                for (int sourceExtensionIndex = 0; sourceExtensionIndex < sourceLayerExtensions.ExtensionCount; sourceExtensionIndex++)
                {
                    targetLayerExtensions.AddExtension(sourceLayerExtensions.get_Extension(sourceExtensionIndex));
                }

            }
            catch (Exception ex)
            {
                message.AddError(120004, ex.Message);
            }
        }

        public ESRI.ArcGIS.esriSystem.IName FullName
        {
            get
            {
                IName fullName = null;

                if (osmGPFactory != null)
                {
                    fullName = osmGPFactory.GetFunctionName(OSMGPFactory.m_CopyLayerExtensionName) as IName;
                }

                return fullName;
            }
        }

        public object GetRenderer(ESRI.ArcGIS.Geoprocessing.IGPParameter pParam)
        {
            return default(object);
        }

        public int HelpContext
        {
            get
            {
                return default(int);
            }
        }

        public string HelpFile
        {
            get
            {
                return default(string);
            }
        }

        public bool IsLicensed()
        {
            return true;
        }

        public string MetadataFile
        {
            get
            {
                string metadafile = "gptransferlayerextension.xml";

                try
                {
                    string[] languageid = System.Threading.Thread.CurrentThread.CurrentUICulture.Name.Split("-".ToCharArray());

                    string ArcGISInstallationLocation = OSMGPFactory.GetArcGIS10InstallLocation();
                    string localizedMetaDataFileShort = ArcGISInstallationLocation + System.IO.Path.DirectorySeparatorChar.ToString() + "help" + System.IO.Path.DirectorySeparatorChar.ToString() + "gp" + System.IO.Path.DirectorySeparatorChar.ToString() + "gptransferlayerextension_" + languageid[0] + ".xml";
                    string localizedMetaDataFileLong = ArcGISInstallationLocation + System.IO.Path.DirectorySeparatorChar.ToString() + "help" + System.IO.Path.DirectorySeparatorChar.ToString() + "gp" + System.IO.Path.DirectorySeparatorChar.ToString() + "gptransferlayerextension_" + System.Threading.Thread.CurrentThread.CurrentUICulture.Name + ".xml";

                    if (System.IO.File.Exists(localizedMetaDataFileShort))
                    {
                        metadafile = localizedMetaDataFileShort;
                    }
                    else if (System.IO.File.Exists(localizedMetaDataFileLong))
                    {
                        metadafile = localizedMetaDataFileLong;
                    }
                }
                catch { }

                return metadafile;
            }
        }

        public string Name
        {
            get
            {
                return OSMGPFactory.m_CopyLayerExtensionName;
            }
        }

        public ESRI.ArcGIS.esriSystem.IArray ParameterInfo
        {
            get
            {
                IArray parameters = new ArrayClass();

                IGPParameterEdit3 inputSourceLayer = new GPParameterClass() as IGPParameterEdit3;
                inputSourceLayer.Name = "in_sourcelayer";
                inputSourceLayer.DisplayName = resourceManager.GetString("GPTools_GPCopyLayerExtension_inputsourcelayer_displayname");
                inputSourceLayer.ParameterType = esriGPParameterType.esriGPParameterTypeRequired;
                inputSourceLayer.Direction = esriGPParameterDirection.esriGPParameterDirectionInput;
                inputSourceLayer.DataType = new GPLayerTypeClass();

                in_sourcelayerNumber = 0;
                parameters.Add((IGPParameter)inputSourceLayer);

                IGPParameterEdit3 inputTargetLayer = new GPParameterClass() as IGPParameterEdit3;
                inputTargetLayer.Name = "in_targetlayer";
                inputTargetLayer.DisplayName = resourceManager.GetString("GPTools_GPCopyLayerExtension_inputtargetlayer_displayname");
                inputTargetLayer.ParameterType = esriGPParameterType.esriGPParameterTypeRequired;
                inputTargetLayer.Direction = esriGPParameterDirection.esriGPParameterDirectionInput;
                inputTargetLayer.DataType = new GPLayerTypeClass();

                in_targetlayerNumber = 1;
                parameters.Add((IGPParameter)inputTargetLayer);
                
                IGPParameterEdit3 outputLayer = new GPParameterClass() as IGPParameterEdit3;
                outputLayer.Name = "out_layer";
                outputLayer.DisplayName = resourceManager.GetString("GPTools_GPCopyLayerExtension_outputlayer_displayname");
                outputLayer.ParameterType = esriGPParameterType.esriGPParameterTypeDerived;
                outputLayer.Direction = esriGPParameterDirection.esriGPParameterDirectionOutput;
                outputLayer.DataType = new GPLayerTypeClass();
                outputLayer.AddDependency("in_targetlayer");


                out_layerNumber = 2;
                parameters.Add((IGPParameter)outputLayer);


                return parameters;

            }
        }

        public void UpdateMessages(ESRI.ArcGIS.esriSystem.IArray paramvalues, ESRI.ArcGIS.Geoprocessing.IGPEnvironmentManager pEnvMgr, ESRI.ArcGIS.Geodatabase.IGPMessages Messages)
        {
        }

        public void UpdateParameters(ESRI.ArcGIS.esriSystem.IArray paramvalues, ESRI.ArcGIS.Geoprocessing.IGPEnvironmentManager pEnvMgr)
        {
        }

        public ESRI.ArcGIS.Geodatabase.IGPMessages Validate(ESRI.ArcGIS.esriSystem.IArray paramvalues, bool updateValues, ESRI.ArcGIS.Geoprocessing.IGPEnvironmentManager envMgr)
        {
            return default(ESRI.ArcGIS.Geodatabase.IGPMessages);
        }
        #endregion

    }
}
