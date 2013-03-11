# arcgis-osm-editor

ArcGIS Editor for OpenStreetMap - Welcome
ArcGIS Editor for OpenStreetMap is a toolset for GIS users to access and contribute to OpenStreetMap through their Desktop or Server environment.  Learn more [here](http://www.esri.com/osmeditor).

## Features
* Download data from OSM into a file geodatabase – download from current extent or load from .osm file
* Create your own planet (.osm) files
* Edit OSM data in ArcGIS
* Upload edits back to OSM
* Create routing networks from OSM data
* Create custom feature services based on OSM data

## Instructions

1. Fork and then clone the repo. 
2. Read the wiki documentation for how to use the compiled code.
3. For the Server Components Only: 
	a) Please read the web.config to run the server and make sure all paths are correct.
	b) Please make sure you install Visual Studio 2010 Service Pack 1. Best way to know you got the latest of Visual Studio is to install it from here: http://www.microsoft.com/web/gallery/install.aspx?appid=VWDorVS2010SP1Pack
	c) Make sure you have ArcGIS 10.1 installed to run the Server part.
	d) The application needs a SDE connection.You can change in the web.config where the sde file is:

    <add key="DatabaseConnection" value="C:\Data\OSM\Mxds\osmdevsde.sde" />

## Requirements

* An OpenStreetMap login
* Visual Studo 2010
* ArcGIS for Desktop 10.1 (Desktop Component)
* ArcGIS Server 10.1, ArcSDE 10.1, and IIS (Server Component)

## Resources

* [OpenStreetMap](http://www.openstreetmap.org)

## Issues

Find a bug or want to request a new feature?  Please let us know by submitting an issue.

## Contributing

Anyone and everyone is welcome to contribute. 

## Licensing
Copyright 2012 Esri

Licensed under the Apache License, Version 2.0 (the "License");
you may not use this file except in compliance with the License.
You may obtain a copy of the License at

   http://www.apache.org/licenses/LICENSE-2.0

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.

A copy of the license is available in the repository's [license.txt]( https://raw.github.com/Esri/quickstart-map-js/master/license.txt) file.

[](Esri Tags: ArcGIS Editor for OpenStreetMap)
[](Esri Language: .Net)