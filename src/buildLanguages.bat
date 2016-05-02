rem build the default languages first

cd OSMGeoProcessing

resgen OSMGPToolsStrings.txt OSMGPToolsStrings.resx

cd ..

cd OSMEditor

resgen OSMFeatureInspectorStrings.txt OSMFeatureInspectorStrings.resx

cd ..

cd OSMClassExtension

resgen OSMClassExtensionStrings.txt OSMClassExtensionStrings.resx

cd ..

rem then let's go build all aditional languages

set OSMEditorVersion=2.4.0.0

cd OSMGeoProcessing\languages

call build.bat %OSMEditorVersion%

cd ..\..

cd OSMEditor\languages

call build.bat %OSMEditorVersion%

cd ..\..

