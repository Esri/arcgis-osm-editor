import arcpy
from arcpy import env

# load the standard OpenStreetMap bpx as it references the core OSM tools
arcpy.ImportToolbox(r'C:\Data\OSM\OpenStreetMap Toolbox.tbx')

# define the enterprise geodatabase workspace
env.workspace = r'C:\Data\OSM\Mxds\NewOSMDEV.sde'

# get the feature set (first parameter) to extract the AOI envelope
aoi_featureset = arcpy.GetParameter(0)
inputName = arcpy.GetParameterAsText(1)

validatedTableName = arcpy.ValidateTableName(inputName, env.workspace)

nameOfTargetDataset = arcpy.os.path.join(env.workspace, validatedTableName)
nameOfPointFeatureClass = arcpy.os.path.join(env.workspace, validatedTableName, validatedTableName + r'_osm_pt')
nameOfLineFeatureClass = arcpy.os.path.join(env.workspace, validatedTableName, validatedTableName + r'_osm_ln')
nameOfPolygonFeatureClass = arcpy.os.path.join(env.workspace, validatedTableName, validatedTableName + r'_osm_ply')

# request the data from the OSM server and store it in the target feature dataset
arcpy.OSMGPDownload_osmtools(r'http://www.openstreetmap.org', aoi_featureset,'DO_NOT_INCLUDE_REFERENCES', nameOfTargetDataset, nameOfPointFeatureClass, nameOfLineFeatureClass, nameOfPolygonFeatureClass)

# Return the resulting messages as script tool output messages
#
x = 0
while x < arcpy.GetMessageCount():
    arcpy.AddReturnMessage(x)
    x = x + 1
