[T4Scaffolding.ControllerScaffolder("Controller with read/write action and views, using repositories", HideInConsole = $true, Description = "Adds an ASP.NET MVC controller with views and data access code", SupportsModelType = $true, SupportsDataContextType = $true, SupportsViewScaffolder = $true)][CmdletBinding()]
param(     
	[parameter(Mandatory = $true, ValueFromPipelineByPropertyName = $true)][string]$ControllerName,   
	[string]$ModelType,
    [string]$Project,
    [string]$CodeLanguage,
	[string]$DbContextType,
	[string]$Area,
	[string]$ViewScaffolder = "View",
	[alias("MasterPage")]$Layout,
 	[alias("ContentPlaceholderIDs")][string[]]$SectionNames,
	[alias("PrimaryContentPlaceholderID")][string]$PrimarySectionName,
	[switch]$ReferenceScriptLibraries = $false,
	[switch]$NoChildItems = $false,
	[string[]]$TemplateFolders,
	[switch]$Force = $false,
	[string]$ForceMode
)

Scaffold MvcScaffolding.Controller `
	-ControllerName $ControllerName `
	-ModelType $ModelType `
    -Project $Project `
    -CodeLanguage $CodeLanguage `
	-DbContextType $DbContextType `
	-Area $Area `
	-ViewScaffolder $ViewScaffolder `
	-Layout $Layout `
 	-SectionNames $SectionNames `
	-PrimarySectionName $PrimarySectionName `
	-ReferenceScriptLibraries:$ReferenceScriptLibraries `
	-NoChildItems:$NoChildItems `
	-OverrideTemplateFolders $TemplateFolders `
	-Force:$Force `
	-ForceMode $ForceMode `
	-Repository
