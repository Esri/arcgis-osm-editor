import arcpy, os, os.path, xml.dom.minidom as DOM

# define local variables
results = ""
mapDoc = arcpy.GetParameterAsText(0)
wrkspc = arcpy.GetParameterAsText(1)
# con = r'GIS Servers\arcgis on MyServer_6080 (publisher).ags'
con = arcpy.GetParameterAsText(2)
service = arcpy.GetParameterAsText(3)
pubFolder = arcpy.GetParameterAsText(4)
sddraft = os.path.join(wrkspc, service + r'_tmp.sddraft')
sd = os.path.join(wrkspc, service + r'.sd')
summary = 'n/a'
tags = 'n/a'

# create service definition draft
if pubFolder == "":
    arcpy.mapping.CreateMapSDDraft(mapDoc, sddraft, service, 'ARCGIS_SERVER', con, False, None, summary, tags)
else:
    arcpy.mapping.CreateMapSDDraft(mapDoc, sddraft, service, 'ARCGIS_SERVER', con, False, pubFolder, summary, tags)
    
# add capabilities
doc = DOM.parse(sddraft)

soe = 'FeatureServer'
typeNames = doc.getElementsByTagName('TypeName')
for typeName in typeNames:
    if typeName.firstChild.data == soe:
        extention = typeName.parentNode
        for extElement in extention.childNodes:
            # enabled soe
            if extElement.tagName == 'Enabled':
                extElement.firstChild.data = 'true'
          
# output to a new sddraft
outXml = os.path.join(wrkspc, service + r'.sddraft')     
f = open(outXml, 'w')     
doc.writexml( f )     
f.close()       

# analyze the service definition draft
analysis = arcpy.mapping.AnalyzeForSD(outXml)

# stage and upload the service if the sddraft analysis did not contain errors
if analysis['errors'] == {}:
    # Delete temp sddraft
    os.remove(sddraft)
    # Execute StageService
    arcpy.StageService_server(outXml, sd)
    # Execute UploadServiceDefinition
    arcpy.UploadServiceDefinition_server(sd, con)
else: 
    # if the sddraft analysis contained errors, display them
    
    vars = analysis['errors']
    for ((message, code), layerlist) in vars.iteritems():
        results =  message + " (CODE %i)" % code
        results = results + " Applies to: "
        print "    ", message, " (CODE %i)" % code
        print "       applies to:",
        for layer in layerlist:
            results = results + layer.name,
            print layer.name,
        print  

    # print analysis['errors']
    
arcpy.AddMessage(results)
arcpy.SetParameterAsText(5, results)
    
