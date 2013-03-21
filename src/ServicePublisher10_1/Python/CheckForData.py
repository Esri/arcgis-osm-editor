# Name: CreateDatabaseConnection.py
# Description: Connects to a database using Easy Connect string and operating system authentication.

# Import system modules
import arcpy, sys
from arcpy import env

# Get variables
workspace = arcpy.GetParameterAsText(0) # Connection to data source 
#workspace = r'C:\Data\OSM\Mxds\NewOSMDEV.sde'
dataset = arcpy.GetParameterAsText(1) # Dataset name
#dataset = 'Test0718_1334'
haveData = False


try:
    # Create a Describe object from the shapefile
    #
    desc = arcpy.Describe(workspace + "\\" + dataset)

    # Print dataset properties
    #
    print("Dataset Type: {0}".format(desc.datasetType))
    print("Extent:\n  XMin: {0}, XMax: {1}, YMin: {2}, YMax: {3}".format(
        desc.extent.XMin, desc.extent.XMax, desc.extent.YMin, desc.extent.YMax))
    print("Spatial reference name: {0}:".format(desc.spatialReference.name))
except:
    sys.exit("Error: Could not find dataset " + dataset)

try:
    # Create a Describe object from the feature class
    # sde.OSMUSER.Test0718_1334\sde.OSMUSER.Test0718_1334_osm_ln
    osmln = "_osm_ln"
    fc = workspace + "\\" + dataset + "\\" + dataset + osmln
    desc = arcpy.Describe(fc)

    # Print some feature class properties
    #
    print "Feature Type:  " + desc.featureType
    print "Shape Type :   " + desc.shapeType
    print "Spatial Index: " + str(desc.hasSpatialIndex)
 
    env.workspace = workspace + "\\" + dataset
    result = int(arcpy.GetCount_management(fc).getOutput(0)) 
    print result
    if (result > 0):
        haveData = True

except:
    print "Could not find feature class " + fc

osmply = "_osm_ply"
try:
    # Create a Describe object from the feature class
    # sde.OSMUSER.Test0718_1334\sde.OSMUSER.Test0718_1334_osm_ln
    osmln = "_osm_ln"
    fc = workspace + "\\" + dataset + "\\" + dataset + osmply
    desc = arcpy.Describe(fc)

    # Print some feature class properties
    #
    print "Feature Type:  " + desc.featureType
    print "Shape Type :   " + desc.shapeType
    print "Spatial Index: " + str(desc.hasSpatialIndex)
 
    env.workspace = workspace + "\\" + dataset
    result = int(arcpy.GetCount_management(fc).getOutput(0)) 
    print result
    if (result > 0):
        haveData = True

except:
    print "Could not find feature class " + fc


osmpt = "_osm_pt"
try:
    # Create a Describe object from the feature class
    # sde.OSMUSER.Test0718_1334\sde.OSMUSER.Test0718_1334_osm_ln
    osmln = "_osm_ln"
    fc = workspace + "\\" + dataset + "\\" + dataset + osmpt
    desc = arcpy.Describe(fc)

    # Print some feature class properties
    #
    print "Feature Type:  " + desc.featureType
    print "Shape Type :   " + desc.shapeType
    print "Spatial Index: " + str(desc.hasSpatialIndex)
 
    env.workspace = workspace + "\\" + dataset
    result = int(arcpy.GetCount_management(fc).getOutput(0)) 
    print result
    if (result > 0):
        haveData = True

except:
    print "Could not find feature class " + fc

if not haveData:
    sys.exit("Error: No data found in dataset " + dataset)