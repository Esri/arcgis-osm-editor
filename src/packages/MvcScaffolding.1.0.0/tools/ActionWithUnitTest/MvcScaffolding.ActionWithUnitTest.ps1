[T4Scaffolding.Scaffolder(Description = "Creates an action method, view model, view, and unit test stub")][CmdletBinding()]
param(        
	[parameter(Mandatory = $true, ValueFromPipelineByPropertyName = $true)][string]$Controller,
	[parameter(Mandatory = $true, ValueFromPipelineByPropertyName = $true)][string]$Action,
	[string]$ViewModel,
	[switch]$WithViewModel,
	[switch]$Post,
    [string]$Project,
	[string]$CodeLanguage,
	[string[]]$TemplateFolders,
	[switch]$Force = $false
)

$actionScaffoldResult = Scaffold MvcScaffolding.Action -Controller $Controller -Action $Action -ViewModel $ViewModel -WithViewModel:$WithViewModel -Post:$Post -Project $Project -CodeLanguage $CodeLanguage -OverrideTemplateFolders $TemplateFolders -Force:$Force
Scaffold MvcScaffolding.ActionUnitTest -Controller $actionScaffoldResult.Controller.FullName -Action $actionScaffoldResult.ActionMethod -ViewModel $actionScaffoldResult.ViewModel.FullName -Project $Project -CodeLanguage $CodeLanguage -OverrideTemplateFolders $TemplateFolders -Force:$Force
