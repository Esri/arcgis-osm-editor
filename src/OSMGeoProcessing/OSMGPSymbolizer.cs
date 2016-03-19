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

using System.Xml;
using System.Xml.Serialization;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.DataSourcesGDB;
using System.IO;
using System.Reflection;
using ESRI.ArcGIS.DataSourcesFile;
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.Display;

namespace ESRI.ArcGIS.OSM.GeoProcessing
{
    [Guid("d43c5053-1a03-46f3-b746-b7a92a9c46a1")]
    [ClassInterface(ClassInterfaceType.None)]
    [ProgId("OSMEditor.OSMGPSymbolizer")]
    [ComVisible(false)]
    public class OSMGPSymbolizer : ESRI.ArcGIS.Geoprocessing.IGPFunction2
    {

        string m_DisplayName = String.Empty;

        int in_osmFeatureDatasetNumber, in_osmPointLayerNumber, in_osmLineLayerNumber, in_osmPolygonLayerNumber, out_osmPointLayerNumber, out_osmLineLayerNumber, out_osmPolygonLayerNumber;
        ResourceManager resourceManager = null;
        OSMGPFactory osmGPFactory = null;

        public OSMGPSymbolizer()
        {
            resourceManager = new ResourceManager("ESRI.ArcGIS.OSM.GeoProcessing.OSMGPToolsStrings", this.GetType().Assembly);
            osmGPFactory = new OSMGPFactory();
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
                    m_DisplayName = osmGPFactory.GetFunctionName(OSMGPFactory.m_FeatureSymbolizerName).DisplayName;
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

                // find feature class inside the given feature dataset
                IGPParameter osmFeatureDatasetParameter = paramvalues.get_Element(in_osmFeatureDatasetNumber) as IGPParameter;
                IDEFeatureDataset osmFeatureDataset = gpUtilities3.UnpackGPValue(osmFeatureDatasetParameter) as IDEFeatureDataset;

                string osmPointFeatureClassString = ((IDataElement)osmFeatureDataset).Name + "_osm_pt";
                string osmLineFeatureClassString = ((IDataElement)osmFeatureDataset).Name + "_osm_ln";
                string osmPolygonFeatureClassString = ((IDataElement)osmFeatureDataset).Name + "_osm_ply";

                IFeatureClass osmPointFeatureClass = gpUtilities3.OpenFeatureClassFromString(((IDataElement)osmFeatureDataset).CatalogPath + "/" + osmPointFeatureClassString);
                IFeatureClass osmLineFeatureClass = gpUtilities3.OpenFeatureClassFromString(((IDataElement)osmFeatureDataset).CatalogPath + "/" + osmLineFeatureClassString);
                IFeatureClass osmPoylgonFeatureClass = gpUtilities3.OpenFeatureClassFromString(((IDataElement)osmFeatureDataset).CatalogPath + "/" + osmPolygonFeatureClassString);

                // open the specified layers holding the symbology and editing templates
                IGPParameter osmPointSymbolTemplateParameter = paramvalues.get_Element(in_osmPointLayerNumber) as IGPParameter;
                IGPValue osmGPPointLayerValue = gpUtilities3.UnpackGPValue(osmPointSymbolTemplateParameter);

                IGPParameter outputPointGPParameter = paramvalues.get_Element(out_osmPointLayerNumber) as IGPParameter;
                IGPValue outputPointLayerGPValue = gpUtilities3.UnpackGPValue(outputPointGPParameter);

                bool isLayerOnDisk = false;

                // create a clone of the source layer 
                // we will then go ahead and adjust the data source (dataset) of the cloned layer
                IObjectCopy objectCopy = new ObjectCopyClass();
                ICompositeLayer adjustedPointTemplateLayer = objectCopy.Copy(osmGPPointLayerValue) as ICompositeLayer;

                IGPGroupLayer osmPointGroupTemplateLayer = adjustedPointTemplateLayer as IGPGroupLayer;

                ICompositeLayer compositeLayer = gpUtilities3.Open((IGPValue)osmPointGroupTemplateLayer) as ICompositeLayer;
                //ICompositeLayer adjustedPointTemplateLayer = osmGPPointLayerValue as ICompositeLayer;
                //IGPGroupLayer osmPointGroupTemplateLayer = osmGPPointLayerValue as IGPGroupLayer;
                //IClone cloneSource = osmPointGroupTemplateLayer as IClone;
                //ICompositeLayer compositeLayer = m_gpUtilities3.Open((IGPValue)cloneSource.Clone()) as ICompositeLayer;

                if (compositeLayer == null)
                {
                    ILayerFactoryHelper layerFactoryHelper = new LayerFactoryHelperClass();
                    IFileName layerFileName = new FileNameClass();

                    layerFileName.Path = osmGPPointLayerValue.GetAsText();
                    IEnumLayer enumLayer = layerFactoryHelper.CreateLayersFromName((IName)layerFileName);
                    enumLayer.Reset();

                    compositeLayer = enumLayer.Next() as ICompositeLayer;

                    isLayerOnDisk = true;
                }

                IFeatureLayerDefinition2 featureLayerDefinition2 = null;
                ISQLSyntax sqlSyntax = null;

                IGPLayer adjustedPointGPLayer = null;

                if (compositeLayer != null)
                {
                    for (int layerIndex = 0; layerIndex < compositeLayer.Count; layerIndex++)
                    {

                        IFeatureLayer2 geoFeatureLayer = compositeLayer.get_Layer(layerIndex) as IFeatureLayer2;

                        if (geoFeatureLayer != null)
                        {
                            if (geoFeatureLayer.ShapeType == osmPointFeatureClass.ShapeType)
                            {
                                try
                                {
                                    ((IDataLayer2)geoFeatureLayer).Disconnect();
                                }
                                catch { }

                                ((IDataLayer2)geoFeatureLayer).DataSourceName = ((IDataset)osmPointFeatureClass).FullName;

                                ((IDataLayer2)geoFeatureLayer).Connect(((IDataset)osmPointFeatureClass).FullName);

                                featureLayerDefinition2 = geoFeatureLayer as IFeatureLayerDefinition2;
                                if (featureLayerDefinition2 != null)
                                {
                                    string queryDefinition = featureLayerDefinition2.DefinitionExpression;

                                    sqlSyntax = ((IDataset)osmPointFeatureClass).Workspace as ISQLSyntax;
                                    string delimiterIdentifier = sqlSyntax.GetSpecialCharacter(esriSQLSpecialCharacters.esriSQL_DelimitedIdentifierPrefix);

                                    if (String.IsNullOrEmpty(queryDefinition) == false)
                                    {
                                        string stringToReplace = queryDefinition.Substring(0, 1);
                                        queryDefinition = queryDefinition.Replace(stringToReplace, delimiterIdentifier);
                                    }

                                    featureLayerDefinition2.DefinitionExpression = queryDefinition;
                                }
                            }
                        }
                    }

                    adjustedPointGPLayer = gpUtilities3.MakeGPLayerFromLayer((ILayer)compositeLayer);

                    // save the newly adjusted layer information to disk
                    if (isLayerOnDisk == true)
                    {
                        ILayerFile pointLayerFile = new LayerFileClass();
                        if (pointLayerFile.get_IsPresent(outputPointLayerGPValue.GetAsText()))
                        {
                            try
                            {
                                File.Delete(outputPointLayerGPValue.GetAsText());
                            }
                            catch (Exception ex)
                            {
                                message.AddError(120041,ex.Message);
                                return;
                            }
                        }

                        pointLayerFile.New(outputPointLayerGPValue.GetAsText());

                        pointLayerFile.ReplaceContents((ILayer)compositeLayer);

                        pointLayerFile.Save();

                        adjustedPointGPLayer = gpUtilities3.MakeGPLayerFromLayer((ILayer)pointLayerFile.Layer);
                    }

                 //   IGPLayer adjustedPointGPLayer = gpUtilities3.MakeGPLayerFromLayer((ILayer)compositeLayer);
                    gpUtilities3.AddInternalLayer2((ILayer)compositeLayer, adjustedPointGPLayer);
                    gpUtilities3.PackGPValue((IGPValue)adjustedPointGPLayer, outputPointGPParameter);

                }


                isLayerOnDisk = false;

                IGPParameter osmLineSymbolTemplateParameter = paramvalues.get_Element(in_osmLineLayerNumber) as IGPParameter;
                IGPValue osmGPLineLayerValue = gpUtilities3.UnpackGPValue(osmLineSymbolTemplateParameter) as IGPValue;

                IGPParameter outputLineGPParameter = paramvalues.get_Element(out_osmLineLayerNumber) as IGPParameter;
                IGPValue outputLineLayerGPValue = gpUtilities3.UnpackGPValue(outputLineGPParameter);

                IGPValue adjustedLineTemplateLayer = objectCopy.Copy(osmGPLineLayerValue) as IGPValue;

                IGPGroupLayer osmLineGroupTemplateLayer = adjustedLineTemplateLayer as IGPGroupLayer;

                compositeLayer = gpUtilities3.Open((IGPValue)osmLineGroupTemplateLayer) as ICompositeLayer;

                if (compositeLayer == null)
                {
                    ILayerFactoryHelper layerFactoryHelper = new LayerFactoryHelperClass();
                    IFileName layerFileName = new FileNameClass();

                    layerFileName.Path = osmGPLineLayerValue.GetAsText();
                    IEnumLayer enumLayer = layerFactoryHelper.CreateLayersFromName((IName)layerFileName);
                    enumLayer.Reset();

                    compositeLayer = enumLayer.Next() as ICompositeLayer;

                    isLayerOnDisk = true;
                }


                if (compositeLayer != null)
                {
                    for (int layerIndex = 0; layerIndex < compositeLayer.Count; layerIndex++)
                    {
                        IFeatureLayer2 geoFeatureLayer = compositeLayer.get_Layer(layerIndex) as IFeatureLayer2;
                        if (geoFeatureLayer.ShapeType == osmLineFeatureClass.ShapeType)
                        {
                            try
                            {
                                ((IDataLayer2)geoFeatureLayer).Disconnect();
                            }
                            catch { }
                            ((IDataLayer2)geoFeatureLayer).DataSourceName = ((IDataset)osmLineFeatureClass).FullName;
                            ((IDataLayer2)geoFeatureLayer).Connect(((IDataset)osmLineFeatureClass).FullName);

                            featureLayerDefinition2 = geoFeatureLayer as IFeatureLayerDefinition2;
                            if (featureLayerDefinition2 != null)
                            {
                                string queryDefinition = featureLayerDefinition2.DefinitionExpression;

                                sqlSyntax = ((IDataset)osmLineFeatureClass).Workspace as ISQLSyntax;
                                string delimiterIdentifier = sqlSyntax.GetSpecialCharacter(esriSQLSpecialCharacters.esriSQL_DelimitedIdentifierPrefix);

                                if (string.IsNullOrEmpty(queryDefinition) == false)
                                {
                                    string stringToReplace = queryDefinition.Substring(0, 1);
                                    queryDefinition = queryDefinition.Replace(stringToReplace, delimiterIdentifier);
                                }

                                featureLayerDefinition2.DefinitionExpression = queryDefinition;
                            }
                        }
                    }

                    // save the newly adjusted layer information to disk
                    if (isLayerOnDisk == true)
                    {
                        ILayerFile lineLayerFile = new LayerFileClass();
                        if (lineLayerFile.get_IsPresent(outputLineLayerGPValue.GetAsText()))
                        {
                            try
                            {
                                File.Delete(outputLineLayerGPValue.GetAsText());
                            }
                            catch (Exception ex)
                            {
                                message.AddError(120042, ex.Message);
                                return;
                            }
                        }

                        lineLayerFile.New(outputLineLayerGPValue.GetAsText());

                        lineLayerFile.ReplaceContents((ILayer)compositeLayer);

                        lineLayerFile.Save();
                    }

                    IGPLayer adjustLineGPLayer = gpUtilities3.MakeGPLayerFromLayer((ILayer)compositeLayer);

                    gpUtilities3.AddInternalLayer2((ILayer)compositeLayer, adjustLineGPLayer);
                    gpUtilities3.PackGPValue((IGPValue)adjustLineGPLayer, outputLineGPParameter);
                }


                isLayerOnDisk = false;
                IGPParameter osmPolygonSymbolTemplateParameter = paramvalues.get_Element(in_osmPolygonLayerNumber) as IGPParameter;
                IGPValue osmGPPolygonLayerValue = gpUtilities3.UnpackGPValue(osmPolygonSymbolTemplateParameter);

                IGPParameter outputPolygonGPParameter = paramvalues.get_Element(out_osmPolygonLayerNumber) as IGPParameter;
                IGPValue outputPolygonLayerGPValue = gpUtilities3.UnpackGPValue(outputPolygonGPParameter);

                IGPValue adjustedPolygonTemplateLayer = objectCopy.Copy(osmGPPolygonLayerValue) as IGPValue;

                IGPGroupLayer osmPolygonGroupTemplateLayer = adjustedPolygonTemplateLayer as IGPGroupLayer;
                compositeLayer = gpUtilities3.Open((IGPValue)osmPolygonGroupTemplateLayer) as ICompositeLayer;

                if (compositeLayer == null)
                {
                    ILayerFactoryHelper layerFactoryHelper = new LayerFactoryHelperClass();
                    IFileName layerFileName = new FileNameClass();

                    layerFileName.Path = osmGPPolygonLayerValue.GetAsText();
                    IEnumLayer enumLayer = layerFactoryHelper.CreateLayersFromName((IName)layerFileName);
                    enumLayer.Reset();

                    compositeLayer = enumLayer.Next() as ICompositeLayer;

                    isLayerOnDisk = true;
                }

                if (compositeLayer != null)
                {
                    for (int layerIndex = 0; layerIndex < compositeLayer.Count; layerIndex++)
                    {
                        IFeatureLayer2 geoFeatureLayer = compositeLayer.get_Layer(layerIndex) as IFeatureLayer2;

                        if (geoFeatureLayer.ShapeType == osmPoylgonFeatureClass.ShapeType)
                        {
                            try
                            {
                                ((IDataLayer2)geoFeatureLayer).Disconnect();
                            }
                            catch { }
                            ((IDataLayer2)geoFeatureLayer).DataSourceName = ((IDataset)osmPoylgonFeatureClass).FullName;
                            ((IDataLayer2)geoFeatureLayer).Connect(((IDataset)osmPoylgonFeatureClass).FullName);

                            featureLayerDefinition2 = geoFeatureLayer as IFeatureLayerDefinition2;
                            if (featureLayerDefinition2 != null)
                            {
                                string queryDefinition = featureLayerDefinition2.DefinitionExpression;

                                sqlSyntax = ((IDataset)osmPoylgonFeatureClass).Workspace as ISQLSyntax;
                                string delimiterIdentifier = sqlSyntax.GetSpecialCharacter(esriSQLSpecialCharacters.esriSQL_DelimitedIdentifierPrefix);

                                if (String.IsNullOrEmpty(queryDefinition) == false)
                                {
                                    string stringToReplace = queryDefinition.Substring(0, 1);
                                    queryDefinition = queryDefinition.Replace(stringToReplace, delimiterIdentifier);
                                }

                                featureLayerDefinition2.DefinitionExpression = queryDefinition;
                            }
                        }
                    }

                    // save the newly adjusted layer information to disk
                    if (isLayerOnDisk == true)
                    {
                        ILayerFile polygonLayerFile = new LayerFileClass();
                        if (polygonLayerFile.get_IsPresent(outputPolygonLayerGPValue.GetAsText()))
                        {
                            try
                            {
                                File.Delete(outputPolygonLayerGPValue.GetAsText());
                            }
                            catch (Exception ex)
                            {
                                message.AddError(120043, ex.Message);
                                return;
                            }
                        }

                        polygonLayerFile.New(outputPolygonLayerGPValue.GetAsText());

                        polygonLayerFile.ReplaceContents((ILayer)compositeLayer);

                        polygonLayerFile.Save();
                    }

                    IGPLayer adjustedPolygonGPLayer = gpUtilities3.MakeGPLayerFromLayer((ILayer)compositeLayer);
                    gpUtilities3.AddInternalLayer2((ILayer)compositeLayer, adjustedPolygonGPLayer);

                    gpUtilities3.PackGPValue((IGPValue)adjustedPolygonGPLayer, outputPolygonGPParameter);
                }
            }
            catch (Exception ex)
            {
                message.AddError(-10,ex.Message);
            }
        }

        public ESRI.ArcGIS.esriSystem.IName FullName
        {
            get
            {
                IName fullName = null;

                if (osmGPFactory != null)
                {
                    fullName = osmGPFactory.GetFunctionName(OSMGPFactory.m_FeatureSymbolizerName) as IName;
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
                return "";
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
                string metadafile = "osmgpsymbolizer.xml";

                try
                {
                    string[] languageid = System.Threading.Thread.CurrentThread.CurrentUICulture.Name.Split("-".ToCharArray());

                    string ArcGISInstallationLocation = OSMGPFactory.GetArcGIS10InstallLocation();
                    string localizedMetaDataFileShort = ArcGISInstallationLocation + System.IO.Path.DirectorySeparatorChar.ToString() + "help" + System.IO.Path.DirectorySeparatorChar.ToString() + "gp" + System.IO.Path.DirectorySeparatorChar.ToString() + "osmgpsymbolizer_" + languageid[0] + ".xml";
                    string localizedMetaDataFileLong = ArcGISInstallationLocation + System.IO.Path.DirectorySeparatorChar.ToString() + "help" + System.IO.Path.DirectorySeparatorChar.ToString() + "gp" + System.IO.Path.DirectorySeparatorChar.ToString() + "osmgpsymbolizer_" + System.Threading.Thread.CurrentThread.CurrentUICulture.Name + ".xml";

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
                return OSMGPFactory.m_FeatureSymbolizerName;
            }
        }

        public ESRI.ArcGIS.esriSystem.IArray ParameterInfo
        {
            get
            {
                IArray parameterArray = new ArrayClass();

                string path = Assembly.GetExecutingAssembly().Location;
                FileInfo executingAssembly = new FileInfo(path);

                string dataDirectory = executingAssembly.Directory.Parent.FullName + System.IO.Path.DirectorySeparatorChar + "data";

                // input feature dataset
                IGPParameterEdit3 inputFeatureDataset = new GPParameterClass() as IGPParameterEdit3;
                inputFeatureDataset.ParameterType = esriGPParameterType.esriGPParameterTypeRequired;
                inputFeatureDataset.Direction = esriGPParameterDirection.esriGPParameterDirectionInput;
                inputFeatureDataset.DataType = new DEFeatureDatasetTypeClass();
                inputFeatureDataset.DisplayName = resourceManager.GetString("GPTools_OSMGPSymbolizer_inosmds_displayName");
                inputFeatureDataset.Name = "in_osmfeaturedataset";

                in_osmFeatureDatasetNumber = 0;
                parameterArray.Add((IGPParameter)inputFeatureDataset);

                // input point feature layer (group layer)
                IGPParameterEdit3 inputPointLayer = new GPParameterClass() as IGPParameterEdit3;
                inputPointLayer.ParameterType = esriGPParameterType.esriGPParameterTypeRequired;
                inputPointLayer.Direction = esriGPParameterDirection.esriGPParameterDirectionInput;
                inputPointLayer.DataType = new GPLayerTypeClass();
                inputPointLayer.DisplayName = resourceManager.GetString("GPTools_OSMGPSymbolizer_inptlayer_displayName");
                inputPointLayer.Name = "in_osmpoints_layer";

                // if a file exists then populate the corresponding GP value
                if (File.Exists(dataDirectory + System.IO.Path.DirectorySeparatorChar + "Points.lyr"))
                {
                    IDELayer deLayer = new DELayerClass();
                    ((IGPValue)deLayer).SetAsText(dataDirectory + System.IO.Path.DirectorySeparatorChar + "Points.lyr");
                    inputPointLayer.Value = (IGPValue)deLayer;
                }

                in_osmPointLayerNumber = 1;
                parameterArray.Add((IGPParameter)inputPointLayer);

                // input line feature layer (group layer)
                IGPParameterEdit3 inputLineLayer = new GPParameterClass() as IGPParameterEdit3;
                inputLineLayer.ParameterType = esriGPParameterType.esriGPParameterTypeRequired;
                inputLineLayer.Direction = esriGPParameterDirection.esriGPParameterDirectionInput;
                inputLineLayer.DataType = new GPLayerTypeClass();
                inputLineLayer.DisplayName = resourceManager.GetString("GPTools_OSMGPSymbolizer_inlnlayer_displayName");
                inputLineLayer.Name = "in_osmlines_layer";

                // if a file exists then populate the corresponding GP value
                if (File.Exists(dataDirectory + System.IO.Path.DirectorySeparatorChar + "Lines.lyr"))
                {
                    IDELayer deLayer = new DELayerClass();
                    ((IGPValue)deLayer).SetAsText(dataDirectory + System.IO.Path.DirectorySeparatorChar + "Lines.lyr");
                    inputLineLayer.Value = (IGPValue)deLayer;
                }

                in_osmLineLayerNumber = 2;
                parameterArray.Add((IGPParameter)inputLineLayer);

                // input polygon feature layer (group layer)
                IGPParameterEdit3 inputPolygonLayer = new GPParameterClass() as IGPParameterEdit3;
                inputPolygonLayer.ParameterType = esriGPParameterType.esriGPParameterTypeRequired;
                inputPolygonLayer.Direction = esriGPParameterDirection.esriGPParameterDirectionInput;
                inputPolygonLayer.DataType = new GPLayerTypeClass();
                inputPolygonLayer.DisplayName = resourceManager.GetString("GPTools_OSMGPSymbolizer_inplylayer_displayName");
                inputPolygonLayer.Name = "in_osmpolygons_layer";

                // if a file exists then populate the corresponding GP value
                if (File.Exists(dataDirectory + System.IO.Path.DirectorySeparatorChar + "Polygons.lyr"))
                {
                    IDELayer deLayer = new DELayerClass();
                    ((IGPValue)deLayer).SetAsText(dataDirectory + System.IO.Path.DirectorySeparatorChar + "Polygons.lyr");
                    inputPolygonLayer.Value = (IGPValue)deLayer;
                }

                in_osmPolygonLayerNumber = 3;
                parameterArray.Add((IGPParameter)inputPolygonLayer);

                // output adjusted point feature layer (group layer)
                IGPParameterEdit3 outputPointLayer = new GPParameterClass() as IGPParameterEdit3;
                outputPointLayer.ParameterType = esriGPParameterType.esriGPParameterTypeRequired;
                outputPointLayer.Direction = esriGPParameterDirection.esriGPParameterDirectionOutput;
                outputPointLayer.DataType = new GPLayerTypeClass();
                outputPointLayer.DisplayName = resourceManager.GetString("GPTools_OSMGPSymbolizer_outptlayer_displayName");
                outputPointLayer.Name = "out_osmpoints_layer";

                outputPointLayer.AddDependency("in_osmpoints_layer");

                out_osmPointLayerNumber = 4;
                parameterArray.Add((IGPParameter)outputPointLayer);

                // output adjusted line feature layer (group layer)
                IGPParameterEdit3 outputLineLayer = new GPParameterClass() as IGPParameterEdit3;
                outputLineLayer.ParameterType = esriGPParameterType.esriGPParameterTypeRequired;
                outputLineLayer.Direction = esriGPParameterDirection.esriGPParameterDirectionOutput;
                outputLineLayer.DataType = new GPLayerTypeClass();
                outputLineLayer.DisplayName = resourceManager.GetString("GPTools_OSMGPSymbolizer_outlnlayer_displayName");
                outputLineLayer.Name = "out_osmlines_layer";

                outputLineLayer.AddDependency("in_osmlines_layer");

                out_osmLineLayerNumber = 5;
                parameterArray.Add((IGPParameter)outputLineLayer);

                // output adjusted polygon feature layer (group layer)
                IGPParameterEdit3 outputPolygonLayer = new GPParameterClass() as IGPParameterEdit3;
                outputPolygonLayer.ParameterType = esriGPParameterType.esriGPParameterTypeRequired;
                outputPolygonLayer.Direction = esriGPParameterDirection.esriGPParameterDirectionOutput;
                outputPolygonLayer.DataType = new GPLayerTypeClass();
                outputPolygonLayer.DisplayName = resourceManager.GetString("GPTools_OSMGPSymbolizer_outplylayer_displayName");
                outputPolygonLayer.Name = "out_osmpolygons_layer";

                outputPolygonLayer.AddDependency("in_osmpolygons_layer");

                out_osmPolygonLayerNumber = 6;
                parameterArray.Add((IGPParameter)outputPolygonLayer);

                return parameterArray;
            }
        }

        public void UpdateMessages(ESRI.ArcGIS.esriSystem.IArray paramvalues, ESRI.ArcGIS.Geoprocessing.IGPEnvironmentManager pEnvMgr, ESRI.ArcGIS.Geodatabase.IGPMessages Messages)
        {
        }

        public void UpdateParameters(ESRI.ArcGIS.esriSystem.IArray paramvalues, ESRI.ArcGIS.Geoprocessing.IGPEnvironmentManager pEnvMgr)
        {
            IGPUtilities3 gpUtilities3 = new GPUtilitiesClass();

            #region update the output point parameter based on the input of template point layer
            IGPParameter inputPointLayerParameter = paramvalues.get_Element(in_osmPointLayerNumber) as IGPParameter;
            IGPValue inputPointLayer = gpUtilities3.UnpackGPValue(inputPointLayerParameter);

            IGPParameter3 outputPointLayerParameter = paramvalues.get_Element(out_osmPointLayerNumber) as IGPParameter3;
            if (((inputPointLayer).IsEmpty() == false) && (outputPointLayerParameter.Altered == false))
            {
                IGPValue outputPointGPValue = gpUtilities3.UnpackGPValue(outputPointLayerParameter);
                if (outputPointGPValue.IsEmpty())
                {
                    IClone clonedObject = inputPointLayer as IClone;
                    IGPValue clonedGPValue = clonedObject.Clone() as IGPValue;

                    // if it is an internal group layer
                    IGPGroupLayer inputPointGroupLayer = clonedObject as IGPGroupLayer;

                    if (inputPointGroupLayer != null)
                    {
                        string proposedLayerName = "Points";
                        string tempLayerName = proposedLayerName;
                        int index = 1;
                        ILayer currentMapLayer = gpUtilities3.FindMapLayer(proposedLayerName);

                        while (currentMapLayer != null)
                        {
                            tempLayerName = proposedLayerName + "_" + index.ToString();

                            currentMapLayer = gpUtilities3.FindMapLayer(tempLayerName);
                            index = index + 1;
                        }

                        clonedGPValue.SetAsText(tempLayerName);
                        gpUtilities3.PackGPValue(clonedGPValue, outputPointLayerParameter);
                    }
                    else
                    {

                        IDELayer deLayer = clonedGPValue as IDELayer;

                        if (deLayer != null)
                        {
                            FileInfo sourceLyrFileInfo = new FileInfo(clonedGPValue.GetAsText());

                            // check the output location of the file with respect to the gp environment settings
                            sourceLyrFileInfo = new FileInfo(DetermineLyrLocation(pEnvMgr.GetEnvironments(), sourceLyrFileInfo));

                            if (sourceLyrFileInfo.Exists)
                            {
                                int layerFileIndex = 1;
                                string tempFileLyrName = sourceLyrFileInfo.DirectoryName + System.IO.Path.DirectorySeparatorChar + sourceLyrFileInfo.Name.Substring(0, sourceLyrFileInfo.Name.Length - sourceLyrFileInfo.Extension.Length) + "_" + layerFileIndex.ToString() + sourceLyrFileInfo.Extension;

                                while (File.Exists(tempFileLyrName))
                                {
                                    tempFileLyrName = sourceLyrFileInfo.DirectoryName + System.IO.Path.DirectorySeparatorChar + sourceLyrFileInfo.Name.Substring(0, sourceLyrFileInfo.Name.Length - sourceLyrFileInfo.Extension.Length) + "_" + layerFileIndex.ToString() + sourceLyrFileInfo.Extension;
                                    layerFileIndex = layerFileIndex + 1;
                                }

                                clonedGPValue.SetAsText(tempFileLyrName);
                                gpUtilities3.PackGPValue(clonedGPValue, outputPointLayerParameter);

                            }
                            else
                            {
                                clonedGPValue.SetAsText(sourceLyrFileInfo.FullName);
                                gpUtilities3.PackGPValue(clonedGPValue, outputPointLayerParameter);
                            }
                        }
                    }
                }
            }
            #endregion

            #region update the output line parameter based on the input of template line layer
            IGPParameter inputLineLayerParameter = paramvalues.get_Element(in_osmLineLayerNumber) as IGPParameter;
            IGPValue inputLineLayer = gpUtilities3.UnpackGPValue(inputLineLayerParameter);

            IGPParameter3 outputLineLayerParameter = paramvalues.get_Element(out_osmLineLayerNumber) as IGPParameter3;
            if (((inputLineLayer).IsEmpty() == false) && (outputLineLayerParameter.Altered == false))
            {
                IGPValue outputLineGPValue = gpUtilities3.UnpackGPValue(outputLineLayerParameter);
                if (outputLineGPValue.IsEmpty())
                {

                    IClone clonedObject = inputLineLayer as IClone;
                    IGPValue clonedGPValue = clonedObject.Clone() as IGPValue;

                    // if it is an internal group layer
                    IGPGroupLayer inputLineGroupLayer = clonedObject as IGPGroupLayer;

                    if (inputLineGroupLayer != null)
                    {
                        string proposedLayerName = "Lines";
                        string tempLayerName = proposedLayerName;
                        int index = 1;
                        ILayer currentMapLayer = gpUtilities3.FindMapLayer(proposedLayerName);

                        while (currentMapLayer != null)
                        {
                            tempLayerName = proposedLayerName + "_" + index.ToString();

                            currentMapLayer = gpUtilities3.FindMapLayer(tempLayerName);
                            index = index + 1;
                        }

                        clonedGPValue.SetAsText(tempLayerName);
                        gpUtilities3.PackGPValue(clonedGPValue, outputLineLayerParameter);
                    }
                    else
                    {
                        IDELayer deLayer = clonedGPValue as IDELayer;

                        if (deLayer != null)
                        {
                            FileInfo sourceLyrFileInfo = new FileInfo(clonedGPValue.GetAsText());

                            // check the output location of the file with respect to the gp environment settings
                            sourceLyrFileInfo = new FileInfo(DetermineLyrLocation(pEnvMgr.GetEnvironments(), sourceLyrFileInfo));

                            if (sourceLyrFileInfo.Exists)
                            {
                                int layerFileIndex = 1;
                                string tempFileLyrName = sourceLyrFileInfo.DirectoryName + System.IO.Path.DirectorySeparatorChar + sourceLyrFileInfo.Name.Substring(0, sourceLyrFileInfo.Name.Length - sourceLyrFileInfo.Extension.Length) + "_" + layerFileIndex.ToString() + sourceLyrFileInfo.Extension;

                                while (File.Exists(tempFileLyrName))
                                {
                                    tempFileLyrName = sourceLyrFileInfo.DirectoryName + System.IO.Path.DirectorySeparatorChar + sourceLyrFileInfo.Name.Substring(0, sourceLyrFileInfo.Name.Length - sourceLyrFileInfo.Extension.Length) + "_" + layerFileIndex.ToString() + sourceLyrFileInfo.Extension;
                                    layerFileIndex = layerFileIndex + 1;
                                }

                                clonedGPValue.SetAsText(tempFileLyrName);
                                gpUtilities3.PackGPValue(clonedGPValue, outputLineLayerParameter);

                            }
                            else
                            {
                                clonedGPValue.SetAsText(sourceLyrFileInfo.FullName);
                                gpUtilities3.PackGPValue(clonedGPValue, outputLineLayerParameter);
                            }
                        }
                    }
                }
            }
            #endregion

            #region update the output polygon parameter based on the input of template polygon layer
            IGPParameter inputPolygonLayerParameter = paramvalues.get_Element(in_osmPolygonLayerNumber) as IGPParameter;
            IGPValue inputPolygonLayer = gpUtilities3.UnpackGPValue(inputPolygonLayerParameter);

            IGPParameter3 outputPolygonLayerParameter = paramvalues.get_Element(out_osmPolygonLayerNumber) as IGPParameter3;
            if (((inputPolygonLayer).IsEmpty() == false) && (outputPolygonLayerParameter.Altered == false))
            {
                IGPValue outputPolygonGPValue = gpUtilities3.UnpackGPValue(outputPolygonLayerParameter);
                if (outputPolygonGPValue.IsEmpty())
                {

                    IClone clonedObject = inputPolygonLayer as IClone;
                    IGPValue clonedGPValue = clonedObject.Clone() as IGPValue;

                    // if it is an internal group layer
                    IGPGroupLayer inputPolygonGroupLayer = clonedObject as IGPGroupLayer;

                    if (inputPolygonGroupLayer != null)
                    {
                        string proposedLayerName = "Polygons";
                        string tempLayerName = proposedLayerName;
                        int index = 1;
                        ILayer currentMapLayer = gpUtilities3.FindMapLayer(proposedLayerName);

                        while (currentMapLayer != null)
                        {
                            tempLayerName = proposedLayerName + "_" + index.ToString();

                            currentMapLayer = gpUtilities3.FindMapLayer(tempLayerName);
                            index = index + 1;
                        }

                        clonedGPValue.SetAsText(tempLayerName);
                        gpUtilities3.PackGPValue(clonedGPValue, outputPolygonLayerParameter);
                    }
                    else
                    {

                        IDELayer deLayer = clonedGPValue as IDELayer;

                        if (deLayer != null)
                        {
                            FileInfo sourceLyrFileInfo = new FileInfo(clonedGPValue.GetAsText());

                            // check the output location of the file with respect to the gp environment settings
                            sourceLyrFileInfo = new FileInfo(DetermineLyrLocation(pEnvMgr.GetEnvironments(), sourceLyrFileInfo));

                            if (sourceLyrFileInfo.Exists)
                            {
                                int layerFileIndex = 1;
                                string tempFileLyrName = sourceLyrFileInfo.DirectoryName + System.IO.Path.DirectorySeparatorChar + sourceLyrFileInfo.Name.Substring(0, sourceLyrFileInfo.Name.Length - sourceLyrFileInfo.Extension.Length) + "_" + layerFileIndex.ToString() + sourceLyrFileInfo.Extension;

                                while (File.Exists(tempFileLyrName))
                                {
                                    tempFileLyrName = sourceLyrFileInfo.DirectoryName + System.IO.Path.DirectorySeparatorChar + sourceLyrFileInfo.Name.Substring(0, sourceLyrFileInfo.Name.Length - sourceLyrFileInfo.Extension.Length) + "_" + layerFileIndex.ToString() + sourceLyrFileInfo.Extension;
                                    layerFileIndex = layerFileIndex + 1;
                                }

                                clonedGPValue.SetAsText(tempFileLyrName);
                                gpUtilities3.PackGPValue(clonedGPValue, outputPolygonLayerParameter);

                            }
                            else
                            {
                                clonedGPValue.SetAsText(sourceLyrFileInfo.FullName);
                                gpUtilities3.PackGPValue(clonedGPValue, outputPolygonLayerParameter);
                            }
                        }
                    }
                }
            }
            #endregion

        }

        private string DetermineLyrLocation(IArray environments, FileInfo initialLyrLocation)
        {
            string outputLyrLocation = initialLyrLocation.FullName;

            try
            {
                IGPUtilities3 gpUtilities3 = new GPUtilitiesClass();

                // first check for the scratch workspace
                // next for the current workspace
                // and as a last resort pick the user's temp workspace
                IGPEnvironment scratchworkspaceGPEnvironment = gpUtilities3.GetEnvironment(environments, "scratchworkspace");
                if (scratchworkspaceGPEnvironment != null)
                {
                    IDEWorkspace scratchworkspace = scratchworkspaceGPEnvironment.Value as IDEWorkspace;

                    if (scratchworkspace != null)
                    {
                        if (scratchworkspace.WorkspaceType == esriWorkspaceType.esriFileSystemWorkspace)
                        {
                            if (((IGPValue)scratchworkspace).IsEmpty() == false)
                            {
                                outputLyrLocation = ((IGPValue)scratchworkspace).GetAsText() + System.IO.Path.DirectorySeparatorChar + initialLyrLocation.Name;
                                return outputLyrLocation;
                            }
                        }
                        else if (scratchworkspace.WorkspaceType == esriWorkspaceType.esriLocalDatabaseWorkspace)
                        {
                            if (((IGPValue)scratchworkspace).IsEmpty() == false)
                            {
                                int slashIndexPosition = ((IGPValue)scratchworkspace).GetAsText().LastIndexOf("\\");
                                string potentialFolderLocation = ((IGPValue)scratchworkspace).GetAsText().Substring(0, slashIndexPosition);

                                if (Directory.Exists(potentialFolderLocation))
                                {
                                    outputLyrLocation = potentialFolderLocation + System.IO.Path.DirectorySeparatorChar + initialLyrLocation.Name;
                                    return outputLyrLocation;
                                }
                            }
                        }
                        else
                        {
                            string localTempPath = System.IO.Path.GetTempPath();
                            outputLyrLocation = localTempPath + initialLyrLocation.Name;
                            return outputLyrLocation;
                        }
                    }
                }


                IGPEnvironment currentworkspaceGPEnvironment = gpUtilities3.GetEnvironment(environments, "workspace");

                if (currentworkspaceGPEnvironment != null)
                {
                    IDEWorkspace currentWorkspace = currentworkspaceGPEnvironment.Value as IDEWorkspace;

                    if (currentWorkspace != null)
                    {
                        if (currentWorkspace.WorkspaceType == esriWorkspaceType.esriFileSystemWorkspace)
                        {
                            if (((IGPValue)currentWorkspace).IsEmpty() == false)
                            {
                                outputLyrLocation = ((IGPValue)currentWorkspace).GetAsText() + System.IO.Path.DirectorySeparatorChar + initialLyrLocation.Name;
                                return outputLyrLocation;
                            }
                        }
                    }
                }

                // combine temp directory path with the name of the incoming lyr file
                string tempPath = System.IO.Path.GetTempPath();
                outputLyrLocation = tempPath + initialLyrLocation.Name;
            }
            catch { }

            return outputLyrLocation;
        }

        public ESRI.ArcGIS.Geodatabase.IGPMessages Validate(ESRI.ArcGIS.esriSystem.IArray paramvalues, bool updateValues, ESRI.ArcGIS.Geoprocessing.IGPEnvironmentManager envMgr)
        {
            return default(ESRI.ArcGIS.Geodatabase.IGPMessages);
        }
        #endregion

    }
}
