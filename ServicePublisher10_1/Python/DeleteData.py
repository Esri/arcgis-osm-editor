import arcpy
import sys

def find(f, seq):  
  for item in seq:
    if f == item.name: 
      return True

# define local variables
results = ""
sdeConn = arcpy.GetParameterAsText(0)
#sdeConn = r"C:\Data\OSM\Mxds\NewOSMDEV.sde"
datasetName = arcpy.GetParameterAsText(1)
#datasetName = r"sde.OSMUSER.SyncTest"


try:
    # Create a Describe object
    desc = arcpy.Describe(sdeConn)

    # Get table list   
    tableList = desc.children
    #print "Children:"
    #for child in tableList:
    #    print "\t%s = %s" % (child.name, child.dataType)
    
    # delete relation table
    # Check if exists first then delete
    tName = datasetName + "_osm_relation"
    table = sdeConn + "\\" + tName
    if find(tName, tableList):
        arcpy.Delete_management(table)

    # delete revision table
    # Check if exists first then delete
    tName = datasetName + "_osm_revision"
    table = sdeConn + "\\" + tName
    if find(tName, tableList):
        arcpy.Delete_management(table) 
    
    # delete dataset
    # Check if exists first then delete
    tName = datasetName
    table = sdeConn + "\\" + tName
    if find(tName, tableList):
        arcpy.Delete_management(table)   

except:
    print "Unexpected error in DeleteData:", sys.exc_info()[0]
    raise