import arcpy
from arcpy import env

# load the standard OpenStreetMap bpx as it references the core OSM tools
arcpy.ImportToolbox(r'C:\Data\OSM\OpenStreetMap Toolbox.tbx')

# define the enterprise geodatabase workspace
env.workspace = r'C:\Data\OSM\Mxds\NewOSMDEV.sde'

# name of feature dataset to synchronize
inputName = arcpy.GetParameterAsText(0)

validatedTableName = arcpy.ValidateTableName(inputName, env.workspace)
revisionTableName = arcpy.os.path.join(env.workspace,validatedTableName + '_osm_revision')

# comment describing the upload feature dataset
upload_comment = arcpy.GetParameterAsText(1)

# OSM Server login credentials (username and password)
osm_credentials = arcpy.GetParameterAsText(2)

# retrieve the deltas from the OpenStreetMap server and load them into the local geodatabase
arcpy.OSMGPUpload_osmtools(r'http://www.openstreetmap.org',revisionTableName,upload_comment,'OSMCHANGE_FORMAT',osm_credentials)
