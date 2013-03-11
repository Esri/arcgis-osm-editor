;ArcGIS Editor for OpenStreetMap Installer Script
;Written by Thomas Emge, June 1st, 2010

;--------------------------------
;Include Modern UI

  !include "MUI2.nsh"
  !include "LogicLib.nsh"
  !include "FileFunc.nsh"
  !include "Library.nsh"

;--------------------------------
;Languages
 
  !insertmacro MUI_LANGUAGE "English" ;first language is the default language ID = 1033
;  !insertmacro MUI_LANGUAGE "German" ; ID = 1031
;  !insertmacro MUI_LANGUAGE "Japanese" ; ID = 1041
;  !insertmacro MUI_LANGUAGE "French" ; ID = 1036
  !insertmacro un.DirState
  !insertmacro un.GetParent

;--------------------------------
;General

  ;Name and file
  Name "ArcGIS Editor for OpenStreetMap"
  OutFile "ArcGISEditorforOSM_1.1.exe"
  Icon "ArcGISEditorforOSM.ico"
  UninstallIcon "ArcGISEditorforOSM.ico"

;--------------------------------
;Version Information

  VIProductVersion 1.1.1079.0
  VIAddVersionKey /LANG=${LANG_ENGLISH} "ProductName" "ArcGIS Editor for OpenStreetMap"
  VIAddVersionKey /LANG=${LANG_ENGLISH} "Comments" "Editor extension to allow for editing of OpenStreetMap data in ArcGIS desktop."
  VIAddVersionKey /LANG=${LANG_ENGLISH} "CompanyName" "ESRI"
  VIAddVersionKey /LANG=${LANG_ENGLISH} "LegalTrademarks" "ESRI"
  VIAddVersionKey /LANG=${LANG_ENGLISH} "LegalCopyright" "© 2010 - 2011 ESRI"
  VIAddVersionKey /LANG=${LANG_ENGLISH} "FileDescription" "Tools to allow for the interaction of OpenStreetMap data within ArcGIS."
  VIAddVersionKey /LANG=${LANG_ENGLISH} "FileVersion" 1.1.1079.0
  VIAddVersionKey /LANG=${LANG_ENGLISH} "Manufacturer" "ESRI"

  BrandingText "ArcGIS Editor for OpenStreetMap v1.1"
  LicenseForceSelection checkbox

;--------------------------------

  ;Default installation folder
  InstallDir "$PROGRAMFILES32\ESRI\OSMEditor"
  
  ;Request application privileges for Windows Vista
  RequestExecutionLevel admin

;--------------------------------
;Interface Settings

  !define MUI_ABORTWARNING
  
  
;--------------------------------------
; Language Strings

  LangString Err_DOTNET ${LANG_ENGLISH} "No .NET framework found."
  LangString Err_ESRIDOTNET ${LANG_ENGLISH} "Please install the ESRI .Net assemblies to your machine."
  LangString Err_EXISTOSMEDITOR ${LANG_ENGLISH} "You have a different version of ArcGIS Editor for OpenStreetMap installed. Please uninstall any previous version and install all components of the same build (1.1.1079.0)."
  LangString SEC_CORE_TITLE ${LANG_ENGLISH} "Core Components"
  LangString SEC_CORE_DESC ${LANG_ENGLISH} "Installs the core components (editor extension and geoprocessing tools) for the OSM Editor."
  LangString SEC_DOC_TITLE ${LANG_ENGLISH} "Documentation"
  LangString SEC_DOC_DESC ${LANG_ENGLISH} "Installs the Documentation and How-To instructions."
  LangString SEC_CODE_TITLE ${LANG_ENGLISH} "Source Code"
  LangString SEC_CODE_DESC ${LANG_ENGLISH} "Installs the source code for all the components."
  LangString MUI_INNERTEXT_LICENSE_BOTTOM ${LANG_ENGLISH} "You must accept the license to install the ArcGIS Editor for OpenStreetMap."
  LangString MUI_TEXT_LICENSE_TITLE ${LANG_ENGLISH} "ArcGIS Editor for OpenSteetMap License"
  LangString MUI_TEXT_LICENSE_SUBTITLE ${LANG_ENGLISH} "Licensing Terms"
  LangString MUI_INNERTEXT_LICENSE_TOP ${LANG_ENGLISH} "Press Page Down to see the rest of the license."
  LangString MUI_TEXT_COMPONENTS_TITLE ${LANG_ENGLISH} "ArcGIS Editor for OpenStreetMap Installation Components"
  LangString MUI_TEXT_COMPONENTS_SUBTITLE ${LANG_ENGLISH} "Select the ArcGIS Editor for OpenStreetMap Components you would like to install"
  LangString MUI_INNERTEXT_COMPONENTS_DESCRIPTION_TITLE ${LANG_ENGLISH} "Component"
  LangString MUI_INNERTEXT_COMPONENTS_DESCRIPTION_INFO ${LANG_ENGLISH} "Hover over each item for a more detailed description."
  LangString MUI_TEXT_DIRECTORY_TITLE ${LANG_ENGLISH} "ArcGIS Editor for OpenStreetMap Installation Folder"
  LangString MUI_TEXT_DIRECTORY_SUBTITLE ${LANG_ENGLISH} "Determine the ArcGIS Editor for OSM installation folder"
  LangString MUI_TEXT_INSTALLING_TITLE ${LANG_ENGLISH} "Installing the ArcGIS Editor for OpenStreetMap"
  LangString MUI_TEXT_INSTALLING_SUBTITLE ${LANG_ENGLISH} "Currently installing the selected components"
  LangString MUI_TEXT_FINISH_TITLE ${LANG_ENGLISH} "ArcGIS Editor for OpenStreetMap Installation Complete"
  LangString MUI_TEXT_FINISH_SUBTITLE ${LANG_ENGLISH} "Successfully completed the installation."
  LangString MUI_TEXT_ABORT_TITLE ${LANG_ENGLISH} "ArcGIS Editor for OpenStreetMap Installation Abort"
  LangString MUI_TEXT_ABORT_SUBTITLE ${LANG_ENGLISH} "The installation is not yet complete. Are you sure you want to exit?"
  LangString MUI_UNTEXT_CONFIRM_TITLE ${LANG_ENGLISH} "ArcGIS Editor for OpenStreetMap Uninstall"
  LangString MUI_UNTEXT_CONFIRM_SUBTITLE ${LANG_ENGLISH} "Are you sure you want to uninstall ArcGIS Editor for OpenStreetMap?"
  LangString MUI_UNTEXT_UNINSTALLING_TITLE ${LANG_ENGLISH} "Uninstalling ArcGIS Editor for OpenStreetMap Components"
  LangString MUI_UNTEXT_UNINSTALLING_SUBTITLE ${LANG_ENGLISH} "Uninstalling ArcGIS Editor for OpenStreetMap Components"
  LangString MUI_UNTEXT_FINISH_TITLE ${LANG_ENGLISH} "ArcGIS Editor for OpenStreetMap Uninstall Complete"
  LangString MUI_UNTEXT_FINISH_SUBTITLE ${LANG_ENGLISH} "The ArcGIS Editor for OpenStreetMap components are now uninstalled from your machine."
  LangString MUI_UNTEXT_ABORT_TITLE ${LANG_ENGLISH} "ArcGIS Editor for OpenStreetMap Uninstall Abort"
  LangString MUI_UNTEXT_ABORT_SUBTITLE ${LANG_ENGLISH} "The uninstall is not yet complete. Are you sure you want to exit?"
  LangString STARTREADMETITLE ${LANG_ENGLISH} "ReadMe Document"
  LangString STARTREADMECOMMENT ${LANG_ENGLISH} "Document describing ArcGIS Editor for OpenStreetMap functionality and the demo workflow." 
  LangString Err_CloseArcGIS ${LANG_ENGLISH} "Please close all ArcGIS documents before uninstalling the ArcGIS Editor for OpenStreetMap." 
  
;--------------------------------

; License data
  LicenseLangString myLicenseData ${LANG_ENGLISH} "english.rtf"

  LicenseData $(myLicenseData)
  
;--------------------------------
;Pages

  !insertmacro MUI_PAGE_LICENSE $(myLicenseData)
  !insertmacro MUI_PAGE_COMPONENTS
  !insertmacro MUI_PAGE_DIRECTORY
  !insertmacro MUI_PAGE_INSTFILES
  
  !insertmacro MUI_UNPAGE_CONFIRM
  !insertmacro MUI_UNPAGE_INSTFILES
  
;Installer Sections
;--------------------------------------
Section $(SEC_CORE_TITLE) SecCore

  SetOutPath "$INSTDIR\bin"
  
  ;add the binary files into the bin folder...
  !insertmacro InstallLib DLL NOTSHARED NOREBOOT_NOTPROTECTED "..\OSMEditor\bin\Debug\Microsoft.Http.dll" Microsoft.Http.dll LIBRARY_IGNORE_VERSION
  !insertmacro InstallLib DLL NOTSHARED NOREBOOT_NOTPROTECTED "..\OSMEditor\bin\Debug\Microsoft.Http.Extensions.dll" Microsoft.Http.Extensions.dll LIBRARY_IGNORE_VERSION
  !insertmacro InstallLib DLL NOTSHARED NOREBOOT_NOTPROTECTED "..\OSMEditor\bin\Debug\Microsoft.ServiceModel.Web.dll" Microsoft.ServiceModel.Web.dll LIBRARY_IGNORE_VERSION
  !insertmacro InstallLib DLL NOTSHARED NOREBOOT_NOTPROTECTED "..\OSMEditor\bin\Debug\OSMEditor.dll" OSMEditor.dll LIBRARY_IGNORE_VERSION
  !insertmacro InstallLib DLL NOTSHARED NOREBOOT_NOTPROTECTED "..\OSMEditor\bin\Debug\OSMGeoProcessing.dll" OSMGeoProcessing.dll LIBRARY_IGNORE_VERSION
  !insertmacro InstallLib DLL NOTSHARED NOREBOOT_NOTPROTECTED "..\OSMClassExtension\bin\Debug\OSMClassExtension.dll" OSMClassExtension.dll LIBRARY_IGNORE_VERSION
  File "..\OSMEditor\bin\Debug\osm_domains.xml"
  File "..\OSMEditor\bin\Debug\OSMFeaturesProperties.xml"
  ;File "ArcGISEditorforOSM.ico"
  
  ; add the fonts and the lyr files
  SetOutPath "$INSTDIR\data"
  File "..\data\Points.lyr"
  File "..\data\Lines.lyr"
  File "..\data\Polygons.lyr"
  
  ${Switch} $LANGUAGE
    ${Case} '1033' ; English
      File "..\data\OpenStreetMap Toolbox.tbx"
      ${Break}
    ${Default}
  ${EndSwitch}  
  
  ReadRegStr $0 HKLM "SOFTWARE\ESRI\Desktop10.0" "InstallDir"
  SetOutPath "$0\help\gp"
  ; xml toolbox documentation, english as default and selected language
  ; the english help files will always be there because there are the fallback for missing languages
  File "..\OSMGeoProcessing\gp_documentation\osmgpaddextension.xml"
  File "..\OSMGeoProcessing\gp_documentation\osmgpremoveextension.xml"
  File "..\OSMGeoProcessing\gp_documentation\osmgpattributeselector.xml"
  File "..\OSMGeoProcessing\gp_documentation\osmgpdownload.xml"
  File "..\OSMGeoProcessing\gp_documentation\osmgpsymbolizer.xml"
  File "..\OSMGeoProcessing\gp_documentation\osmgpupload.xml"
  File "..\OSMGeoProcessing\gp_documentation\osmgpfileloader.xml"
  File "..\OSMGeoProcessing\gp_documentation\gpcombinelayers.xml"
  File "..\OSMGeoProcessing\gp_documentation\osmgpcombineattributes.xml"
  File "..\OSMGeoProcessing\gp_documentation\osmgpdiffloader.xml"
  File "..\OSMGeoProcessing\gp_documentation\gpfeaturecomparison.xml"

  ; install registry entries for the binaries
  ;-------------------------------------------------------
  
  ;-------------------------------------------------------
  
  ; installation routines for ArcGIS 10
  ExecWait '"$COMMONFILES\ArcGIS\bin\ESRIRegAsm.exe" "$INSTDIR\bin\OSMEditor.dll" /p:Desktop /s' $0
  ExecWait '"$COMMONFILES\ArcGIS\bin\ESRIRegAsm.exe" "$INSTDIR\bin\OSMGeoProcessing.dll" /p:Desktop /s' $0
  ExecWait '"$COMMONFILES\ArcGIS\bin\ESRIRegAsm.exe" "$INSTDIR\bin\OSMClassExtension.dll" /p:Desktop /s' $0

  ;Store installation folder
  WriteRegStr HKCU "Software\ESRI\ArcGIS\OSMEditor" "" $INSTDIR

  ; Write the installation path into the registry
  WriteRegStr HKLM "SOFTWARE\ESRI\ArcGIS\OSMEditor" "Install_Dir" "$INSTDIR"
  
  ; Write the uninstall keys for Windows
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\OSMEditor" "DisplayName" "ArcGIS Editor for OpenStreetMap"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\OSMEditor" "DisplayVersion" "1.1.1079.0"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\OSMEditor" "DisplayIcon" "$INSTDIR\uninstall.exe"
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\OSMEditor" "Publisher" "Environmental Systems Research Institute, Inc."
  WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\OSMEditor" "VersionMajor" 1
  WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\OSMEditor" "VersionMinor" 0
  WriteRegStr HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\OSMEditor" "UninstallString" '"$INSTDIR\uninstall.exe"'
  WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\OSMEditor" "NoModify" 1
  WriteRegDWORD HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\OSMEditor" "NoRepair" 1
  
  WriteUninstaller "uninstall.exe"
  

SectionEnd
;--------------------------------

Section $(SEC_DOC_TITLE) SecDoc

  ; documentation depending on language
  SetOutPath "$INSTDIR\Documentation"
;  ${Switch} $LANGUAGE
;    ${Case} '1041' ; Japanese
;      ${Break}
;    ${Case} '1031' ; German
;      ${Break}
;    ${Case} '1036' ; French
;      ${Break}
;    ${Default}
      File "..\Documentation\ArcGIS Editor for OpenStreetMap.html"
      File /r /x *.scc /x .svn "..\Documentation\ArcGIS Editor for OpenStreetMap_files"
      SetShellVarContext all
      CreateDirectory "$SMPROGRAMS\ArcGIS"
      CreateDirectory "$SMPROGRAMS\ArcGIS\ArcGIS Editor for OpenStreetMap"
      CreateShortCut "$SMPROGRAMS\ArcGIS\ArcGIS Editor for OpenStreetMap\Read Me.lnk" "$INSTDIR\Documentation\ArcGIS Editor for OpenStreetMap.html" "" "" 0 SW_SHOWNORMAL "" "$(STARTREADMECOMMENT)" ; use defaults for parameters, icon, etc.
 ; ${EndSwitch}  

SectionEnd

;--------------------------------
;Descriptions

  ;Assign language strings to sections
  !insertmacro MUI_FUNCTION_DESCRIPTION_BEGIN
    !insertmacro MUI_DESCRIPTION_TEXT ${SecCore} $(SEC_CORE_DESC)
    !insertmacro MUI_DESCRIPTION_TEXT ${SecDoc} $(SEC_DOC_DESC)
  !insertmacro MUI_FUNCTION_DESCRIPTION_END
 

Section "Uninstall"


  SetOutPath $INSTDIR
  !insertmacro UnInstallLib DLL NOTSHARED NOREBOOT_NOTPROTECTED "$INSTDIR\bin\OSMEditor.dll"
  IfErrors 0 +2
    Call un.CloseArcGISError
  !insertmacro UnInstallLib DLL NOTSHARED NOREBOOT_NOTPROTECTED "$INSTDIR\bin\OSMGeoProcessing.dll"
  IfErrors 0 +2
    Call un.CloseArcGISError
  !insertmacro UnInstallLib DLL NOTSHARED NOREBOOT_NOTPROTECTED "$INSTDIR\bin\OSMClassExtension.dll"
  IfErrors 0 +2
    Call un.CloseArcGISError


  ; delete shortcuts and registry entries
  ; Remove registry keys
  DeleteRegKey HKLM "Software\Microsoft\Windows\CurrentVersion\Uninstall\OSMEditor"
  DeleteRegKey HKLM "SOFTWARE\ESRI\ArcGIS\OSMEditor"
  DeleteRegKey /ifempty HKLM "SOFTWARE\ESRI\ArcGIS"
  DeleteRegKey HKCU "SOFTWARE\ESRI\ArcGIS\OSMEditor"
  DeleteRegKey /ifempty HKCU "SOFTWARE\ESRI\ArcGIS"

DeleteRegKey HKCR "OSMEditor.HttpBasicDataType"
DeleteRegKey HKCR "CLSID\{6594937A-3199-42E9-AF63-A20D5CC2B211}"
DeleteRegKey HKCR "OSMEditor.OSMGPSymbolizer"
DeleteRegKey HKCR "CLSID\{D43C5053-1A03-46F3-B746-B7A92A9C46A1}"
DeleteRegKey HKCR "OSMEditor.HttpBasicDataTypeFactory" 
DeleteRegKey HKCR "CLSID\{1B7B1666-5BDA-49AC-B899-CFF8B1BFB6D9}"
DeleteRegKey HKCR "Record\{8BF4656E-103F-3CF2-9A5D-457E595F7D70}"
DeleteRegKey HKCR "Record\{B256F4E4-97AC-38AD-AC2E-A99B1371EA44}"
DeleteRegKey HKCR "Record\{2117A4BC-33E0-3545-9FD1-AED070FF47CD}"
DeleteRegKey HKCR "Record\{E424D470-8880-32DA-AB47-2D55C147BC66}"
DeleteRegKey HKCR "Record\{CA8E0DE4-7956-3E84-AC08-3CB6329C5E3A}"
DeleteRegKey HKCR "Record\{BEA8AAB9-4BAF-3813-86D4-5B10B1FFC26F}"
DeleteRegKey HKCR "Record\{3C039D4D-C601-30BA-89E6-32BB524D7BBA}"
DeleteRegKey HKCR "Record\{E3A7770A-C417-3518-A92F-F9A8DEA38815}"
DeleteRegKey HKCR "Record\{9C06F4B1-8606-3762-A85C-A966776DC6B3}"
DeleteRegKey HKCR "Record\{9AD6BA56-712B-3F2C-8E5C-59DEA9323326}"
DeleteRegKey HKCR "Record\{45D788AB-DA5B-3C40-ACF0-2A88B27C6E51}"
DeleteRegKey HKCR "Record\{E85D8A1A-A2D5-3649-ACE3-66ABB39A5806}"
DeleteRegKey HKCR "Record\{83065E50-44E2-3323-9694-7F6EF9CC92AC}"
DeleteRegKey HKCR "OSMEditor.HttpBasicGPValue"
DeleteRegKey HKCR "CLSID\{FAB65CE5-775B-47B2-BC0B-87D93D3EBE94}"
DeleteRegKey HKCR "OSMEditor.OSMGPDownload"
DeleteRegKey HKCR "CLSID\{B1653CBD-4023-4640-A48C-5AC8D21B4A71}"
DeleteRegKey HKCR "OSMEditor.OSMGPUpload"
DeleteRegKey HKCR "CLSID\{D3956B0D-A2B0-45DE-82FE-0EA43AA86F46}"
DeleteRegKey HKCR "OSMEditor.OSMGPAttributeSelector"
DeleteRegKey HKCR "CLSID\{EC0D7B3E-D73D-4D60-86A3-66C40D1BD39A}"
DeleteRegKey HKCR "OSMEditor.OSMGPFactory"
DeleteRegKey HKCR "CLSID\{5C1A4CC1-5CF3-474D-A209-D6CD05E0EFFC}"
DeleteRegKey HKCR "OSMEditor.OSMGPRemoveExtension"
DeleteRegKey HKCR "CLSID\{75E2C8F2-C54E-4BC6-84D8-B52A853F5FDA}"
DeleteRegKey HKCR "OSMEditor.BasicAuthenticationControlUI"
DeleteRegKey HKCR "CLSID\{F483BDB7-DBA1-45CA-A31E-665422FD2460}"
DeleteRegKey HKCR "OSMEditor.OSMGPAddExtension"
DeleteRegKey HKCR "CLSID\{4613B4CA-22D1-4BFC-9EE1-03AD63D20B2F}"
DeleteRegKey HKCR "OSMEditor.OSMFeatureInspector"
DeleteRegKey HKCR "CLSID\{65CA4847-8661-45EB-8E1E-B2985CA17C78}"
DeleteRegKey HKCR "OSMEditor.OSMEditorToolbar"
DeleteRegKey HKCR "CLSID\{980AE216-F181-4FE7-95B7-6C0A7342F71B}"
DeleteRegKey HKCR "OSMEditor.OSMEditorToolbarCmd"
DeleteRegKey HKCR "CLSID\{E5AA39C1-DED2-4EB5-8B57-0D09E7413B31}"
DeleteRegKey HKCR "OSMEditor.OSMEditorPropertyPage"
DeleteRegKey HKCR "CLSID\{6C210462-106A-4CA9-8B3E-FD190468FE6B}"
DeleteRegKey HKCR "OSMEditor.OSMConflictEditorUI"
DeleteRegKey HKCR "CLSID\{336454C6-AAE4-3672-B8CE-E823047FAE48}"
DeleteRegKey HKCR "OSMEditor.OSMEditorExtension"
DeleteRegKey HKCR "CLSID\{FAA799F0-BDC7-4CA4-AF0C-A8D591C22058}"
DeleteRegKey HKCR "ESRI.ArcGIS.OSM.Editor.conflictTag"
DeleteRegKey HKCR "CLSID\{B677C509-3F12-39F1-84F8-94472D373872}"
DeleteRegKey HKCR "Record\{DEBCF4C5-F5CC-3724-B9D7-548F32B7C4E8}"
DeleteRegKey HKCR "OSMEditor.OSMConflictEditor"
DeleteRegKey HKCR "CLSID\{93E976C7-B3A2-4B0F-A575-6626358614E3}"
DeleteRegKey HKCR "OSMEditor.GxFilterXmlFiles"
DeleteRegKey HKCR "CLSID\{87883C5A-FC7B-49C7-8445-D8F63F3E7A97}"
DeleteRegKey HKCR "OSMEditor.OSMUtility"
DeleteRegKey HKCR "CLSID\{50E9A9C0-6401-4E34-A2E4-AF3A35E489E6}"
DeleteRegKey HKCR "OSMEditor.OSMClassExtension"
DeleteRegKey HKCR "CLSID\{65CA4847-8661-45EB-8E1E-B2985CA17C78}"
DeleteRegKey HKCR "OSMEditor.OSMFeatureInspectorUI"
DeleteRegKey HKCR "CLSID\{703FD76D-8556-4447-A77D-7BF6B5DCF157}"
DeleteRegKey HKCR "OSMEditor.GPCombineLayers"
DeleteRegKey HKCR "CLSID\{63EEE1B1-15C5-49D7-A926-5EB0575000A0}"
DeleteRegKey HKCR "OSMEditor.BasicAuthenticationCtrl"
DeleteRegKey HKCR "CLSID\{F483BDB7-DBA1-45CA-A31E-665422FD2460}"
DeleteRegKey HKCR "OSMEditor.OSMGPFileLoader"
DeleteRegKey HKCR "CLSID\{86211E63-2DBB-4D56-95CA-25B3D2A0A0AC}"
DeleteRegKey HKCR "OSMEditor.OSMGPCombineAttributes"
DeleteRegKey HKCR "CLSID\{091C4384-8AA5-4D4D-8C30-FAFB1CA81B86}"

; installation routines for ArcGIS 10
  IfFileExists $COMMONFILES\ArcGIS\bin\ESRIRegAsm.exe 0 +3
    ExecWait '"$COMMONFILES\ArcGIS\bin\ESRIRegAsm.exe" $INSTDIR\bin\OSMEditor.dll /v:10 /u /s' $0
    ExecWait '"$COMMONFILES\ArcGIS\bin\ESRIRegAsm.exe" $INSTDIR\bin\OSMGeoProcessing.dll /v:10 /u /s' $0
    ExecWait '"$COMMONFILES\ArcGIS\bin\ESRIRegAsm.exe" $INSTDIR\bin\OSMClassExtension.dll /v:10 /u /s' $0

  ; delete the geoprocessing metadata xml files
  ReadRegStr $0 HKLM "SOFTWARE\ESRI\Desktop10.0" "InstallDir"
  SetOutPath "$0\help\gp"
  ; xml toolbox documentation, english as default and selected language
  ; the english help files will always be there because there are the fallback for missing languages
  Delete "$0\help\gp\osmgpaddextension.xml"
  Delete "$0\help\gp\osmgpattributeselector.xml"
  Delete "$0\help\gp\osmgpdownload.xml"
  Delete "$0\help\gp\osmgpremoveextension.xml"
  Delete "$0\help\gp\osmgpsymbolizer.xml"
  Delete "$0\help\gp\osmgpupload.xml"
  Delete "$0\help\gp\osmgpfileloader.xml"
  Delete "$0\help\gp\gpcombinelayers.xml"
  Delete "$0\help\gp\osmgpdiffloader.xml"
  Delete "$0\help\gp\gpfeaturecomparison.xml"

  Delete "$INSTDIR\Uninstall.exe"

  SetShellVarContext all
  ; Remove shortcuts, if any
  Delete "$SMPROGRAMS\ArcGIS\ArcGIS Editor for OpenStreetMap\*.*"

  ; Remove directories used
  SetOutPath "$SMPROGRAMS"
  delete "$SMPROGRAMS\ArcGIS\ArcGIS Editor for OpenStreetMap\Read Me.lnk" 
  RMDir "$SMPROGRAMS\ArcGIS\ArcGIS Editor for OpenStreetMap"
  
  ${un.DirState} "$SMPROGRAMS\ArcGIS" $R0
  ${If} $R0 == 0   ; if the directory is empty then delete it
    SetOutPath "$SMPROGRAMS"
    RMDir "$SMPROGRAMS\ArcGIS"
  ${EndIf}  

  SetOutPath $INSTDIR
  RMDIR /r "$INSTDIR\bin"
  RMDIR /r "$INSTDIR\data"
  RMDIR /r "$INSTDIR\Documentation"

  ${un.DirState} "$INSTDIR" $R0
  ${If} $R0 == 0   ; if the directory is empty then delete it
    ${un.GetParent} $INSTDIR $R0
    SetOutPath $R0
    RMDir "$INSTDIR"
  ${EndIf}  
  
SectionEnd

;--------------------------------
;Installer Functions

Function .onInit

  Call IsDotNETInstalled
  Pop $0
  StrCmp $0 1 +3 +1
    MessageBox MB_OK $(Err_DOTNET)
    Abort
    
;  Call IsESRIDotNETInstalled
;  Pop $0
;  StrCmp $0 "" +1 +3
;    MessageBox MB_OK $(Err_ESRIDOTNET)
;    Abort

  Call CheckInstalledOSMEditorVersion
  IfErrors +5 +1
  Pop $0
  StrCmp $0 "OSMGeoProcessing, Version=1.1.1079.0, Culture=neutral, PublicKeyToken=null" +3 +1
    MessageBox MB_OK $(Err_EXISTOSMEDITOR)
    Abort
    
  UAC_Elevate:
    UAC::RunElevated 
    StrCmp 1223 $0 UAC_ElevationAborted ; UAC dialog aborted by user?
    StrCmp 0 $0 0 UAC_Err ; Error?
    StrCmp 1 $1 0 UAC_Success ;Are we the real deal or just the wrapper?
    Quit
 
  UAC_Err:
    MessageBox mb_iconstop "Unable to elevate, error $0"
    Abort
 
  UAC_ElevationAborted:
    # elevation was aborted, run as normal?
    MessageBox mb_iconstop "This installer requires admin access, aborting!"
    Abort
 
  UAC_Success:
    StrCmp 1 $3 +4 ;Admin?
    StrCmp 3 $1 0 UAC_ElevationAborted ;Try again?
    MessageBox mb_iconstop "This installer requires admin access, try again"
    goto UAC_Elevate 
  
  !insertmacro MUI_LANGDLL_DISPLAY

FunctionEnd

Function .OnInstFailed
    UAC::Unload ;Must call unload!
FunctionEnd
 
Function .OnInstSuccess
    UAC::Unload ;Must call unload!
FunctionEnd

;--------------------------------
;Uninstaller Functions

Function un.onInit

  !insertmacro MUI_UNGETLANGUAGE
  
FunctionEnd

Function un.CloseArcGISError

  MessageBox MB_OK $(Err_CloseArcGIS)
  Abort

FunctionEnd

; Misc installer functions to check preinstall conditions
;---------------------------------------------------------
 ; IsDotNETInstalled
 ;
 ; Based on GetDotNETVersion
 ;   http://nsis.sourceforge.net/Get_.NET_Version
 ;
 ; Usage:
 ;   Call IsDotNETInstalled
 ;   Pop $0
 ;   StrCmp $0 1 found.NETFramework no.NETFramework

 Function IsDotNETInstalled
   Push $0
   Push $1

   StrCpy $0 1
   System::Call "mscoree::GetCORVersion(w, i ${NSIS_MAX_STRLEN}, *i) i .r1"
   StrCmp $1 0 +2
     StrCpy $0 0

   Pop $1
   Exch $0
 FunctionEnd

 Function IsESRIDotNETInstalled
   ReadRegStr $0 HKLM "SOFTWARE\Microsoft\.NETFramework\AssemblyFolders\ArcGIS Desktop" ""

   Push $0
   
 FunctionEnd
 
 Function CheckInstalledOSMEditorVersion
   
   ReadRegStr $0 HKCR "CLSID\{B1653CBD-4023-4640-A48C-5AC8D21B4A71}\InprocServer32" "Assembly"

   Push $0

 FunctionEnd
 