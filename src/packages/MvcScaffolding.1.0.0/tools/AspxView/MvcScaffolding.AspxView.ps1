[T4Scaffolding.ViewScaffolder("ASPX", Description = "Adds an ASP.NET MVC view using the ASPX view engine", LayoutPageFilter = "*.master|*.master")][CmdletBinding()]
param(        
	[parameter(Mandatory = $true, ValueFromPipelineByPropertyName = $true, Position = 0)][string]$Controller,
	[parameter(Mandatory = $true, ValueFromPipelineByPropertyName = $true, Position = 1)][string]$ViewName,
	[string]$ModelType,
	[string]$Template = "Empty",
	[string]$Area,
	[alias("Layout")]$MasterPage = "",
	[alias("SectionNames")][string[]]$ContentPlaceholderIDs,
	[alias("PrimarySectionName")][string]$PrimaryContentPlaceholderID,
	[switch]$ReferenceScriptLibraries = $false,
    [string]$Project,
	[string]$CodeLanguage,
	[string[]]$TemplateFolders,
	[switch]$Force = $false
)

# Populate masterpage-related args with defaults based on standard MVC 3 site template where not specified.
# If you haven't passed a -MasterPage argument but it looks like you are using a standard master page, assume
# you do want to use that master page. If you really don't want any master, explicitly pass -MasterPage $null.
$defaultMasterPage = "/Views/Shared/Site.Master"
if ($MasterPage -eq "") {
	$MasterPage = if (Get-ProjectItem $defaultMasterPage) { $defaultMasterPage } else { $null }
}
if (!$ContentPlaceholderIDs) { $ContentPlaceholderIDs = @("TitleContent", "MainContent") }
if (!$PrimaryContentPlaceholderID) { $PrimaryContentPlaceholderID = "MainContent" }

# In the case of view names with a leading underscore, this is a Razor convention that Aspx doesn't follow
# so we just strip off any leading underscore
if ($Template.StartsWith("_") -and ($Template.Length -gt 1)) { $Template = $Template.Substring(1) }
if ($ViewName.StartsWith("_") -and ($ViewName.Length -gt 1)) { $ViewName = $ViewName.Substring(1) }

# In the case of master page names with a leading tilde, strip it off, because the view templates 
# automatically prefix the master name with a tilde
if ($MasterPage -and $MasterPage.StartsWith("~")) {
	$MasterPage = $MasterPage.Substring(1)
}

# Inherit all logic from MvcScaffolding.RazorView (merely override the templates)
Scaffold MvcScaffolding.RazorView -Controller $Controller -ViewName $ViewName -ModelType $ModelType -Template $Template -Area $Area -Layout $MasterPage -SectionNames $ContentPlaceholderIDs -PrimarySectionName $PrimaryContentPlaceholderID -ReferenceScriptLibraries:$ReferenceScriptLibraries -Project $Project -CodeLanguage $CodeLanguage -OverrideTemplateFolders $TemplateFolders -Force:$Force