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
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.Display;
using ESRI.ArcGIS.OSM.OSMClassExtension;

namespace ESRI.ArcGIS.OSM.GeoProcessing
{
    [Guid("b0878ae8-a85d-4fee-8cce-4ed015e03e4e")]
    [ClassInterface(ClassInterfaceType.None)]
    [ProgId("OSMEditor.OSMGPFeatureComparison")]
    public class OSMGPFeatureComparison : ESRI.ArcGIS.Geoprocessing.IGPFunction2
    {
        string m_DisplayName = String.Empty;

        int in_sourceFeatureClassNumber, in_sourceIntersectionFieldNumber, in_sourceRefIDsFieldNumber, in_MatchFeatureClassNumber, out_FeatureClassNumber;
        ResourceManager resourceManager = null;
        OSMGPFactory osmGPFactory = null;

        public OSMGPFeatureComparison()
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
                    m_DisplayName = osmGPFactory.GetFunctionName(OSMGPFactory.m_FeatureComparisonName).DisplayName;
                }

                return m_DisplayName;
            }
        }

        public void Execute(ESRI.ArcGIS.esriSystem.IArray paramvalues, ESRI.ArcGIS.esriSystem.ITrackCancel TrackCancel, ESRI.ArcGIS.Geoprocessing.IGPEnvironmentManager envMgr, ESRI.ArcGIS.Geodatabase.IGPMessages message)
        {

            try
            {
                IGPUtilities3 gpUtilities3 = new GPUtilitiesClass() as IGPUtilities3;

                if (TrackCancel == null)
                {
                    TrackCancel = new CancelTrackerClass();
                }

                // decode in the input layers
                IGPParameter in_SourceFeatureClassParameter = paramvalues.get_Element(in_sourceFeatureClassNumber) as IGPParameter;
                IGPValue in_SourceFeatureGPValue = gpUtilities3.UnpackGPValue(in_SourceFeatureClassParameter) as IGPValue;

                IFeatureClass sourceFeatureClass = null;
                IQueryFilter queryFilter = null;

                gpUtilities3.DecodeFeatureLayer((IGPValue)in_SourceFeatureGPValue, out sourceFeatureClass, out queryFilter);

                if (sourceFeatureClass == null)
                {
                    message.AddError(120027, resourceManager.GetString("GPTools_OSMGPFeatureComparison_source_nullpointer"));
                    return;
                }

                IGPParameter in_NumberOfIntersectionsFieldParameter = paramvalues.get_Element(in_sourceIntersectionFieldNumber) as IGPParameter;
                IGPValue in_NumberOfIntersectionsFieldGPValue = gpUtilities3.UnpackGPValue(in_NumberOfIntersectionsFieldParameter) as IGPValue;

                IGPParameter in_SourceRefIDFieldParameter = paramvalues.get_Element(in_sourceRefIDsFieldNumber) as IGPParameter;
                IGPValue in_SourceRefIDFieldGPValue = gpUtilities3.UnpackGPValue(in_SourceRefIDFieldParameter) as IGPValue;

                IGPParameter in_MatchFeatureClassParameter = paramvalues.get_Element(in_MatchFeatureClassNumber) as IGPParameter;
                IGPValue in_MatchFeatureGPValue = gpUtilities3.UnpackGPValue(in_MatchFeatureClassParameter) as IGPValue;

                IFeatureClass matchFeatureClass = null;
                IQueryFilter matchQueryFilter = null;

                gpUtilities3.DecodeFeatureLayer((IGPValue)in_MatchFeatureGPValue, out matchFeatureClass, out matchQueryFilter);


                if (matchFeatureClass == null)
                {
                    message.AddError(120028, resourceManager.GetString("GPTools_OSMGPFeatureComparison_match_nullpointer"));
                    return;
                }

                if (queryFilter != null)
                {
                    if (((IGeoDataset)matchFeatureClass).SpatialReference != null)
                    {
                        queryFilter.set_OutputSpatialReference(sourceFeatureClass.ShapeFieldName, ((IGeoDataset)matchFeatureClass).SpatialReference);
                    }
                }


                IWorkspace sourceWorkspace = ((IDataset) sourceFeatureClass).Workspace;
                IWorkspaceEdit sourceWorkspaceEdit = sourceWorkspace as IWorkspaceEdit;

                if (sourceWorkspace.Type == esriWorkspaceType.esriRemoteDatabaseWorkspace)
                {
                    sourceWorkspaceEdit = sourceWorkspace as IWorkspaceEdit;
                    sourceWorkspaceEdit.StartEditing(false);
                    sourceWorkspaceEdit.StartEditOperation();
                }

                // get an overall feature count as that determines the progress indicator
                int featureCount = ((ITable)sourceFeatureClass).RowCount(queryFilter);

                // set up the progress indicator
                IStepProgressor stepProgressor = TrackCancel as IStepProgressor;

                if (stepProgressor != null)
                {
                    stepProgressor.MinRange = 0;
                    stepProgressor.MaxRange = featureCount;
                    stepProgressor.Position = 0;
                    stepProgressor.Message = resourceManager.GetString("GPTools_OSMGPFeatureComparison_progressMessage");
                    stepProgressor.StepValue = 1;
                    stepProgressor.Show();
                }

                int numberOfIntersectionsFieldIndex = sourceFeatureClass.FindField(in_NumberOfIntersectionsFieldGPValue.GetAsText());
                int sourceRefIDFieldIndex = sourceFeatureClass.FindField(in_SourceRefIDFieldGPValue.GetAsText());

                ISpatialFilter matchFCSpatialFilter = new SpatialFilter();
                matchFCSpatialFilter.GeometryField = matchFeatureClass.ShapeFieldName;
                matchFCSpatialFilter.SpatialRel = esriSpatialRelEnum.esriSpatialRelIntersects;
                matchFCSpatialFilter.WhereClause = matchQueryFilter.WhereClause;

                using (ComReleaser comReleaser = new ComReleaser())
                {
                    IFeatureCursor sourceFeatureCursor = sourceFeatureClass.Search(queryFilter, false);
                    comReleaser.ManageLifetime(sourceFeatureCursor);

                    IFeature sourceFeature = null;

                    while ((sourceFeature = sourceFeatureCursor.NextFeature()) != null)
                    {
                        int numberOfIntersections = 0;
                        string intersectedFeatures = String.Empty;

                        IPolyline sourceLine = sourceFeature.Shape as IPolyline;

                        matchFCSpatialFilter.Geometry = sourceLine;

                        using (ComReleaser innerReleaser = new ComReleaser())
                        {
                            IFeatureCursor matchFeatureCursor = matchFeatureClass.Search(matchFCSpatialFilter, false);
                            innerReleaser.ManageLifetime(matchFeatureCursor);

                            IFeature matchFeature = null;

                            while ((matchFeature = matchFeatureCursor.NextFeature()) != null)
                            {
                                IPointCollection intersectionPointCollection = null;
                                try
                                {
                                    ITopologicalOperator topoOperator = sourceLine as ITopologicalOperator;

                                    if (topoOperator.IsSimple == false)
                                    {
                                        ((ITopologicalOperator2)topoOperator).IsKnownSimple_2 = false;
                                        topoOperator.Simplify();
                                    }

                                    IPolyline matchPolyline = matchFeature.Shape as IPolyline;

                                    if (queryFilter != null)
                                    {
                                        matchPolyline.Project(sourceLine.SpatialReference);
                                    }

                                    if (((ITopologicalOperator)matchPolyline).IsSimple == false)
                                    {
                                        ((ITopologicalOperator2)matchPolyline).IsKnownSimple_2 = false;
                                        ((ITopologicalOperator)matchPolyline).Simplify();
                                    }

                                    intersectionPointCollection = topoOperator.Intersect(matchPolyline, esriGeometryDimension.esriGeometry0Dimension) as IPointCollection;
                                }
                                catch (Exception ex)
                                {
                                    message.AddWarning(ex.Message);
                                    continue;
                                }

                                if (intersectionPointCollection != null && intersectionPointCollection.PointCount > 0)
                                {
                                    numberOfIntersections = numberOfIntersections + intersectionPointCollection.PointCount;

                                    if (String.IsNullOrEmpty(intersectedFeatures))
                                    {
                                        intersectedFeatures = matchFeature.OID.ToString();
                                    }
                                    else
                                    {
                                        intersectedFeatures = intersectedFeatures + "," + matchFeature.OID.ToString();
                                    }
                                }
                            }

                            if (numberOfIntersectionsFieldIndex > -1)
                            {
                                sourceFeature.set_Value(numberOfIntersectionsFieldIndex, numberOfIntersections);
                            }

                            if (sourceRefIDFieldIndex > -1)
                            {
                                if (intersectedFeatures.Length > sourceFeatureClass.Fields.get_Field(sourceRefIDFieldIndex).Length)
                                {
                                    sourceFeature.set_Value(sourceRefIDFieldIndex, intersectedFeatures.Substring(0, sourceFeatureClass.Fields.get_Field(sourceRefIDFieldIndex).Length));
                                }
                                else
                                {
                                    sourceFeature.set_Value(sourceRefIDFieldIndex, intersectedFeatures);
                                }
                            }
                        }

                        try
                        {
                            sourceFeature.Store();
                        }
                        catch (Exception ex)
                        {
                            message.AddWarning(ex.Message);
                        }

                        if (stepProgressor != null)
                        {
                            // update the progress UI
                            stepProgressor.Step();
                        }

                        // check for user cancellation
                        if (TrackCancel.Continue() == false)
                        {
                            return;
                        }

                    }
                }

                if (sourceWorkspaceEdit != null)
                {
                    sourceWorkspaceEdit.StopEditOperation();
                    sourceWorkspaceEdit.StopEditing(true);
                }

                if (stepProgressor != null)
                {
                    stepProgressor.Hide();
                }

            }
            catch (Exception ex)
            {
                message.AddAbort(ex.Message);
            }

        }

        public ESRI.ArcGIS.esriSystem.IName FullName
        {
            get
            {
                IName fullName = null;

                if (osmGPFactory != null)
                {
                    fullName = osmGPFactory.GetFunctionName(OSMGPFactory.m_FeatureComparisonName) as IName;
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
                return String.Empty;
            }
        }

        public bool IsLicensed()
        {
            // as an open-source tool it will also be licensed
            return true;
        }

        public string MetadataFile
        {
            get
            {
                string metadafile = "gpfeaturecomparison.xml";

                try
                {
                    string[] languageid = System.Threading.Thread.CurrentThread.CurrentUICulture.Name.Split("-".ToCharArray());

                    string ArcGISInstallationLocation = OSMGPFactory.GetArcGIS10InstallLocation();
                    string localizedMetaDataFileShort = ArcGISInstallationLocation + System.IO.Path.DirectorySeparatorChar.ToString() + "help" + System.IO.Path.DirectorySeparatorChar.ToString() + "gp" + System.IO.Path.DirectorySeparatorChar.ToString() + "osmgpupload_" + languageid[0] + ".xml";
                    string localizedMetaDataFileLong = ArcGISInstallationLocation + System.IO.Path.DirectorySeparatorChar.ToString() + "help" + System.IO.Path.DirectorySeparatorChar.ToString() + "gp" + System.IO.Path.DirectorySeparatorChar.ToString() + "osmgpupload_" + System.Threading.Thread.CurrentThread.CurrentUICulture.Name + ".xml";

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
                return OSMGPFactory.m_FeatureComparisonName;
            }
        }

        public ESRI.ArcGIS.esriSystem.IArray ParameterInfo
        {
            get
            {
                IArray parameters = new ArrayClass();

                // source feature class 
                IGPParameterEdit3 in_sourceFeaturesParameter = new GPParameterClass() as IGPParameterEdit3;
                in_sourceFeaturesParameter.DataType = new GPFeatureLayerTypeClass();
                in_sourceFeaturesParameter.Direction = esriGPParameterDirection.esriGPParameterDirectionInput;
                in_sourceFeaturesParameter.DisplayName = resourceManager.GetString("GPTools_OSMGPFeatureComparison_inputsourcefeatures_displayname");
                in_sourceFeaturesParameter.Name = "in_sourceFeatures";
                in_sourceFeaturesParameter.ParameterType = esriGPParameterType.esriGPParameterTypeRequired;

                IGPFeatureClassDomain sourceFeatureClassDomain = new GPFeatureClassDomainClass();
                sourceFeatureClassDomain.AddType(esriGeometryType.esriGeometryPolyline);
                in_sourceFeaturesParameter.Domain = (IGPDomain)sourceFeatureClassDomain;

                in_sourceFeatureClassNumber = 0;
                parameters.Add((IGPParameter3)in_sourceFeaturesParameter);

                // input field to hold the number of intersections
                IGPParameterEdit3 in_sourceIntersectionFieldParameter = new GPParameterClass() as IGPParameterEdit3;
                in_sourceIntersectionFieldParameter.DataType = new FieldTypeClass();
                in_sourceIntersectionFieldParameter.Direction = esriGPParameterDirection.esriGPParameterDirectionInput;
                in_sourceIntersectionFieldParameter.DisplayName = resourceManager.GetString("GPTools_OSMGPFeatureComparison_inputintersectnumber_displayname");
                in_sourceIntersectionFieldParameter.Name = "in_number_of_intersections_field";
                in_sourceIntersectionFieldParameter.ParameterType = esriGPParameterType.esriGPParameterTypeRequired;
                in_sourceIntersectionFieldParameter.AddDependency("in_sourceFeatures");

                IGPFieldDomain fieldDomain = new GPFieldDomainClass();
                fieldDomain.AddType(esriFieldType.esriFieldTypeInteger);
                in_sourceIntersectionFieldParameter.Domain = fieldDomain as IGPDomain;

                in_sourceIntersectionFieldNumber = 1;
                parameters.Add((IGPParameter3)in_sourceIntersectionFieldParameter);

                // input field to hold the intersected OIDs (comma separated)
                IGPParameterEdit3 in_sourceRefIDsFieldParameter = new GPParameterClass() as IGPParameterEdit3;
                in_sourceRefIDsFieldParameter.DataType = new FieldTypeClass();
                in_sourceRefIDsFieldParameter.Direction = esriGPParameterDirection.esriGPParameterDirectionInput;
                in_sourceRefIDsFieldParameter.DisplayName = resourceManager.GetString("GPTools_OSMGPFeatureComparison_inputintersectids_displayname");
                in_sourceRefIDsFieldParameter.Name = "in_intersected_ids_field";
                in_sourceRefIDsFieldParameter.ParameterType = esriGPParameterType.esriGPParameterTypeRequired;
                in_sourceRefIDsFieldParameter.AddDependency("in_sourceFeatures");

                IGPFieldDomain id_fieldDomain = new GPFieldDomainClass();
                id_fieldDomain.AddType(esriFieldType.esriFieldTypeString);
                in_sourceRefIDsFieldParameter.Domain = id_fieldDomain as IGPDomain;

                in_sourceRefIDsFieldNumber = 2;
                parameters.Add((IGPParameter3)in_sourceRefIDsFieldParameter);


                // input feature class
                IGPParameterEdit3 in_MatchFeatureClassParameter = new GPParameterClass() as IGPParameterEdit3;
                in_MatchFeatureClassParameter.DataType = new GPFeatureLayerTypeClass();
                in_MatchFeatureClassParameter.Direction = esriGPParameterDirection.esriGPParameterDirectionInput;
                in_MatchFeatureClassParameter.DisplayName = resourceManager.GetString("GPTools_OSMGPFeatureComparison_inputmatchfeatures_displayname");
                in_MatchFeatureClassParameter.Name = "in_matchFeatures";
                in_MatchFeatureClassParameter.ParameterType = esriGPParameterType.esriGPParameterTypeRequired;

                IGPFeatureClassDomain matchFeatureClassDomain = new GPFeatureClassDomainClass();
                matchFeatureClassDomain.AddType(esriGeometryType.esriGeometryPolyline);
                in_MatchFeatureClassParameter.Domain = (IGPDomain)matchFeatureClassDomain;

                in_MatchFeatureClassNumber = 3;
                parameters.Add((IGPParameter3)in_MatchFeatureClassParameter);

                // output feature class
                IGPParameterEdit3 out_FeatureClassParameter = new GPParameterClass() as IGPParameterEdit3;
                out_FeatureClassParameter.DataType = new GPFeatureLayerTypeClass();
                out_FeatureClassParameter.Direction = esriGPParameterDirection.esriGPParameterDirectionOutput;
                out_FeatureClassParameter.DisplayName = resourceManager.GetString("GPTools_OSMGPFeatureComparison_outputfeatures_displayname");
                out_FeatureClassParameter.Name = "out_sourceFeatures";
                out_FeatureClassParameter.ParameterType = esriGPParameterType.esriGPParameterTypeDerived;
                out_FeatureClassParameter.AddDependency("in_sourceFeatures");

                out_FeatureClassNumber = 4;
                parameters.Add((IGPParameter3)out_FeatureClassParameter);

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
