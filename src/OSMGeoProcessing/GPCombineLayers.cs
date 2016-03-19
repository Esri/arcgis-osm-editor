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
using ESRI.ArcGIS.DataSourcesFile;
using ESRI.ArcGIS.Carto;
using System.IO;
using ESRI.ArcGIS.Display;

namespace ESRI.ArcGIS.OSM.GeoProcessing
{
    // this function takes any combination of layers and create a group layer as the final output
    [Guid("63eee1b1-15c5-49d7-a926-5eb0575000a0")]
    [ClassInterface(ClassInterfaceType.None)]
    [ProgId("OSMEditor.GPCombineLayers")]
    public class GPCombineLayers : ESRI.ArcGIS.Geoprocessing.IGPFunction2
    {

        string m_DisplayName = "Combine Layers";
        int in_LayersNumber, out_groupLayerNumber;

        ResourceManager resourceManager = null;
        IGPFunctionFactory osmGPFactory = null;

        public GPCombineLayers()
        {
            try
            {
                resourceManager = new ResourceManager("ESRI.ArcGIS.OSM.GeoProcessing.OSMGPToolsStrings", this.GetType().Assembly);

                m_DisplayName = string.Empty;
                osmGPFactory = new OSMGPFactory();
            }
            catch { }
        }



        #region "IGPFunction2 Implementations"
        public ESRI.ArcGIS.esriSystem.UID DialogCLSID
        {
            get
            {
                return null;
            }
        }

        public string DisplayName
        {
            get
            {
                if (String.IsNullOrEmpty(m_DisplayName))
                {
                    m_DisplayName = osmGPFactory.GetFunctionName(OSMGPFactory.m_CombineLayersName).DisplayName;
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

                IGPValue inputLayersGPValue = gpUtilities3.UnpackGPValue(paramvalues.get_Element(in_LayersNumber));
                IGPMultiValue inputLayersMultiValue = gpUtilities3.UnpackGPValue(inputLayersGPValue) as IGPMultiValue;

                IGPParameter outputGroupLayerParameter = paramvalues.get_Element(out_groupLayerNumber) as IGPParameter;
                IGPValue outputGPGroupLayer = gpUtilities3.UnpackGPValue(outputGroupLayerParameter);
                IGPCompositeLayer outputCompositeLayer = outputGPGroupLayer as IGPCompositeLayer;

                if (outputCompositeLayer == null)
                {
                    message.AddError(120048, string.Format(resourceManager.GetString("GPTools_NullPointerParameterType"), outputGroupLayerParameter.Name));
                    return;
                }


                IGroupLayer groupLayer = null;
                
                // find the last position of the "\" string
                // in case we find such a thing, i.e. position is >= -1 then let's assume that we are dealing with a layer file on disk
                // otherwise let's create a new group layer instance
                string outputGPLayerNameAsString = outputGPGroupLayer.GetAsText();
                int separatorPosition = outputGPLayerNameAsString.LastIndexOf(System.IO.Path.DirectorySeparatorChar);
                string layerName = String.Empty;

                if (separatorPosition > -1)
                {
                    layerName = outputGPGroupLayer.GetAsText().Substring(separatorPosition + 1);
                }
                else
                {
                    layerName = outputGPGroupLayer.GetAsText();
                }

                ILayer foundLayer = null;
                IGPLayer existingGPLayer = gpUtilities3.FindMapLayer2(layerName, out foundLayer);

                if (foundLayer != null)
                {
                    gpUtilities3.RemoveFromMapEx((IGPValue)existingGPLayer);
                    gpUtilities3.RemoveInternalLayerEx(foundLayer);
                }
                
                groupLayer = new GroupLayer();
                ((ILayer)groupLayer).Name = layerName;

                for (int layerIndex = 0; layerIndex < inputLayersMultiValue.Count; layerIndex++)
                {
                    IGPValue gpLayerToAdd = inputLayersMultiValue.get_Value(layerIndex) as IGPValue;

                    ILayer sourceLayer = gpUtilities3.DecodeLayer(gpLayerToAdd);

                    groupLayer.Add(sourceLayer);
                }

                outputGPGroupLayer = gpUtilities3.MakeGPValueFromObject(groupLayer);
                
                if (separatorPosition > -1)
                {
                    try
                    {
                        // in the case that we are dealing with a layer file on disk
                        // let's persist the group layer information into the file
                        ILayerFile pointLayerFile = new LayerFileClass();

                        if (System.IO.Path.GetExtension(outputGPLayerNameAsString).ToUpper().Equals(".LYR"))
                        {
                            if (pointLayerFile.get_IsPresent(outputGPLayerNameAsString))
                            {
                                try
                                {
                                    gpUtilities3.Delete(outputGPGroupLayer);
                                }
                                catch (Exception ex)
                                {
                                    message.AddError(120001, ex.Message);
                                    return;
                                }
                            }

                            pointLayerFile.New(outputGPLayerNameAsString);

                            pointLayerFile.ReplaceContents(groupLayer);

                            pointLayerFile.Save();

                        }
                       
                         outputGPGroupLayer = gpUtilities3.MakeGPValueFromObject(pointLayerFile.Layer);
                    }
                    catch (Exception ex)
                    {
                        message.AddError(120002, ex.Message);
                        return;
                    }
                }

                gpUtilities3.PackGPValue(outputGPGroupLayer, outputGroupLayerParameter);

            }
            catch (Exception ex)
            {
                message.AddError(120049, ex.Message);
            }
        }

        public ESRI.ArcGIS.esriSystem.IName FullName
        {
            get
            {
                IName fullName = null;

                if (osmGPFactory != null)
                {
                    fullName = osmGPFactory.GetFunctionName(OSMGPFactory.m_CombineLayersName) as IName;
                }

                return fullName;
            }
        }

        public object GetRenderer(ESRI.ArcGIS.Geoprocessing.IGPParameter pParam)
        {
            return null;
        }

        public int HelpContext
        {
            get
            {
                return 0;
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
                string metadafile = "gpcombinelayers.xml";

                try
                {
                    string[] languageid = System.Threading.Thread.CurrentThread.CurrentUICulture.Name.Split("-".ToCharArray());

                    string ArcGISInstallationLocation = OSMGPFactory.GetArcGIS10InstallLocation();
                    string localizedMetaDataFileShort = ArcGISInstallationLocation + System.IO.Path.DirectorySeparatorChar.ToString() + "help" + System.IO.Path.DirectorySeparatorChar.ToString() + "gp" + System.IO.Path.DirectorySeparatorChar.ToString() + "gpcombinelayers_" + languageid[0] + ".xml";
                    string localizedMetaDataFileLong = ArcGISInstallationLocation + System.IO.Path.DirectorySeparatorChar.ToString() + "help" + System.IO.Path.DirectorySeparatorChar.ToString() + "gp" + System.IO.Path.DirectorySeparatorChar.ToString() + "gpcombinelayers_" + System.Threading.Thread.CurrentThread.CurrentUICulture.Name + ".xml";

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
                return OSMGPFactory.m_CombineLayersName;
            }
        }

        public ESRI.ArcGIS.esriSystem.IArray ParameterInfo
        {
            get
            {
                //
                IArray parameterArray = new ArrayClass();

                // input layers
                IGPParameterEdit3 inputLayerFiles = new GPParameterClass() as IGPParameterEdit3;

                IGPDataType inputType = new GPLayerTypeClass();

                IGPMultiValueType multiValueType = new GPMultiValueTypeClass();
                multiValueType.MemberDataType = inputType;

                IGPMultiValue multiValueValue = new GPMultiValueClass();
                multiValueValue.MemberDataType = inputType;

                inputLayerFiles.DataType = (IGPDataType)multiValueType;
                inputLayerFiles.Value = (IGPValue)multiValueValue;
                inputLayerFiles.Direction = esriGPParameterDirection.esriGPParameterDirectionInput;
                inputLayerFiles.DisplayName = resourceManager.GetString("GPTools_GPCombineLayers_inputlayers_desc");
                inputLayerFiles.Name = "in_LayerFiles";
                inputLayerFiles.ParameterType = esriGPParameterType.esriGPParameterTypeRequired;

                in_LayersNumber = 0;
                parameterArray.Add(inputLayerFiles);

                // output group layer
                IGPParameterEdit3 outputGroupLayer = new GPParameterClass() as IGPParameterEdit3;
                outputGroupLayer.DataType = new GPGroupLayerTypeClass();
                outputGroupLayer.Direction = esriGPParameterDirection.esriGPParameterDirectionOutput;
                outputGroupLayer.DisplayName = resourceManager.GetString("GPTools_GPCombineLayers_outputgrouplayer_desc");
                outputGroupLayer.Name = "out_grouplayer";

                out_groupLayerNumber = 1;
                parameterArray.Add(outputGroupLayer);

                return parameterArray;
            }
        }

        public void UpdateMessages(ESRI.ArcGIS.esriSystem.IArray paramvalues, ESRI.ArcGIS.Geoprocessing.IGPEnvironmentManager pEnvMgr, ESRI.ArcGIS.Geodatabase.IGPMessages Messages)
        {
            // no special code required - let the framework handle it
        }

        public void UpdateParameters(ESRI.ArcGIS.esriSystem.IArray paramvalues, ESRI.ArcGIS.Geoprocessing.IGPEnvironmentManager pEnvMgr)
        {
            IGPUtilities3 gpUtilities3 = new GPUtilitiesClass();

            IGPParameter3 inputLayersParameter = paramvalues.get_Element(in_LayersNumber) as IGPParameter3;
            IGPMultiValue inputLayersGPValue = gpUtilities3.UnpackGPValue(inputLayersParameter) as IGPMultiValue;

            if (inputLayersGPValue == null)
            {
                return;
            }

            // check if there are input layer provided
            if (inputLayersGPValue.Count == 0)
            {
                return;
            }

            IGPParameter3 outputLayerParameter = paramvalues.get_Element(out_groupLayerNumber) as IGPParameter3;
            IGPValue outputLayerGPValue = gpUtilities3.UnpackGPValue(outputLayerParameter);

            // if the output layer value is still empty and build a proposal for a name
            if (outputLayerGPValue.IsEmpty())
            {
                // read the proposed name from the resources
                string proposedOutputLayerName = resourceManager.GetString("GPTools_GPCombineLayers_outputgrouplayer_proposedname");

                string proposedOutputLayerNameTest = proposedOutputLayerName;
                int layerIndex = 2;

                // check if a layer with the same name already exists
                // if it does then attempt to append some index numbers to build a unique, new name
                ILayer foundLayer = gpUtilities3.FindMapLayer(proposedOutputLayerNameTest);

                while (foundLayer != null)
                {
                    proposedOutputLayerNameTest = proposedOutputLayerName + " (" + layerIndex + ")";

                    foundLayer = gpUtilities3.FindMapLayer(proposedOutputLayerNameTest);
                    layerIndex = layerIndex + 1;

                    if (layerIndex > 10000)
                    {
                        break;
                    }
                }

                outputLayerGPValue.SetAsText(proposedOutputLayerNameTest);
                gpUtilities3.PackGPValue(outputLayerGPValue, outputLayerParameter);
            }
        }

        public ESRI.ArcGIS.Geodatabase.IGPMessages Validate(ESRI.ArcGIS.esriSystem.IArray paramvalues, bool updateValues, ESRI.ArcGIS.Geoprocessing.IGPEnvironmentManager envMgr)
        {
            return default(ESRI.ArcGIS.Geodatabase.IGPMessages);
        }
        #endregion

    }
}
