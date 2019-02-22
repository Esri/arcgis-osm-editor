# arcgis-osm-editor

ArcGIS Editor for OpenStreetMap - Welcome
ArcGIS Editor for OpenStreetMap is a toolset for GIS users to access and contribute to OpenStreetMap through their Desktop or Server environment.  Learn more [here](http://www.esri.com/osmeditor).

## Features
* Download data from OSM into a file geodatabase - download from current extent or load from .osm file
* Create your own planet (.osm) files
* Edit OSM data in ArcGIS
* Upload edits back to OSM
* Create routing networks from OSM data

## Instructions
1. Compiled setups (if you just want to install in ArcGIS and not deal with the code) can be downloaded from ArcGIS Online.  
 - For the **10.3** installer, download from here https://www.arcgis.com/home/item.html?id=75716d933f1c40a784243198e0dc11a1
 - For the **10.4** installer, download from here https://www.arcgis.com/home/item.html?id=c18d3d0d5c62465db60f89225fdd2698

2. Read documentation at http://github.com/Esri/arcgis-osm-editor/wiki/Documentation on how to use the tools

3. If you want to work with the code:
	
	a) Fork and then clone the repo. 
	
	b) See the documentation on working with the code ("Working with the code" section of this topic: http://github.com/Esri/arcgis-osm-editor/wiki/System-requirements,-installation,-&-working-with-the-code).
	
	c) *If you want to work with the Server Component code, then do the following*: 
		1) Please read the web.config to run the server and make sure all paths are correct.
		2) Please make sure you install Visual Studio 2010 Service Pack 1. Best way to know you got the latest of Visual Studio is to install it from here: http://www.microsoft.com/web/gallery/install.aspx?appid=VWDorVS2010SP1Pack
		3) Make sure you have ArcGIS 10.1 installed to run the Server part.
		4) The application needs a SDE connection.You can change in the web.config where the sde file is:
    	 	 add key="DatabaseConnection" value="C:\Data\OSM\Mxds\osmdevsde.sde"

## Requirements

* An OpenStreetMap login (create at https://www.openstreetmap.org/user/new)
* ArcGIS for Desktop 10.3 or 10.4
* Visual Studio 2010 (if you're working with the code)

## Resources

* [OpenStreetMap](http://www.openstreetmap.org)

## Issues

Find a bug or want to request a new feature?  Please let us know by submitting an issue.

## Contributing

Anyone and everyone is welcome to contribute. 

## Licensing
Copyright 2016 Esri

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

   http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.

A copy of the license is available in the repository's [license.txt]( https://github.com/Esri/arcgis-osm-editor/blob/master/license.txt) file.

[](Esri Tags: ArcGIS Editor OpenStreetMap)
[](Esri Language: C-Sharp)
