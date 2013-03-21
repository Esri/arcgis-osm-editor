import arcpy

# get passed in arguments
mapDoc = arcpy.GetParameterAsText(0)
wrkspc = arcpy.GetParameterAsText(1)
datasetName = arcpy.GetParameterAsText(2)
#wrkspc = r"C:\Data\OSM\Mxds\NewOSMDEV.sde\sde.SDE.TempTest08"

# set mxd
mxd = arcpy.mapping.MapDocument(mapDoc)

# change data source locations
for lyr in arcpy.mapping.ListLayers(mxd):
    if lyr.supports("DATASOURCE"):
        print lyr.dataSource
        lyrDs = lyr.dataSource
        i = lyrDs.rfind("_osm_")
        if i > 0:
            fCNameExt = lyrDs[i:]
            newFCName = datasetName + fCNameExt
            print newFCName
            lyr.replaceDataSource(wrkspc, "SDE_WORKSPACE", newFCName)
            print lyr.dataSource

# find any broken data sources and delete layer
for df in arcpy.mapping.ListDataFrames(mxd):
    for lyr in arcpy.mapping.ListLayers(mxd, "", df):
        for brklyr in arcpy.mapping.ListBrokenDataSources(lyr):
            print 'Removing layer ' + brklyr.name + ' due to broken data source. '
            arcpy.mapping.RemoveLayer(df, brklyr)

# Set data frame extent
df = arcpy.mapping.ListDataFrames(mxd)[0]
desc = arcpy.Describe(wrkspc + '\\' + datasetName)
df.extent = desc.extent

# Save mxd
mxd.save()