import arcpy
from arcpy import env

# load the standard OpenStreetMap bpx as it references the core OSM tools
arcpy.ImportToolbox(r'C:\Data\OSM\OpenStreetMap Toolbox.tbx')

# define the enterprise geodatabase workspace
env.workspace = r'C:\Data\OSM\Mxds\NewOSMDEV.sde'

# get the start date/time for synchronization (first parameter)
start_diff_time = arcpy.GetParameterAsText(0)

# load only diffs inside the AOI
load_inside_aoi = arcpy.GetParameter(1)

# name of feature dataset to synchronize
inputName = arcpy.GetParameterAsText(2)

validatedTableName = arcpy.ValidateTableName(inputName, env.workspace)
# combine name of workspace and dataset
syncDatasetName = arcpy.os.path.join(env.workspace, inputName)

arcpy.AddMessage(syncDatasetName)

try:
    # retrieve the deltas from the OpenStreetMap server and load them into the local geodatabase
    arcpy.OSMGPDiffLoader_osmtools(r'http://planet.openstreetmap.org/replication',syncDatasetName,start_diff_time,load_inside_aoi,'NORMAL_LOGGING')

except:
    pass

# Return the resulting messages as script tool output messages
#
x = 0
while x < arcpy.GetMessageCount():
    arcpy.AddReturnMessage(x)
    x = x + 1
