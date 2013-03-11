[T4Scaffolding.Scaffolder(Description = "Creates an action method, view model, and view")][CmdletBinding()]
param(        
	[parameter(Mandatory = $true, ValueFromPipelineByPropertyName = $true)][string]$Controller,
	[parameter(Mandatory = $true, ValueFromPipelineByPropertyName = $true)][string]$Action,
	[string]$ViewModel,
	[switch]$WithViewModel,
	[switch]$Post,
    [string]$Project,
	[string]$CodeLanguage,
	[string[]]$TemplateFolders,
	[switch]$NoChildItems = $false,
	[switch]$Force = $false
)

$Controller = [System.Text.RegularExpressions.Regex]::Replace($Controller, "Controller$", "", [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
$foundControllerType = Get-ProjectType ($Controller + "Controller") -Project $Project
if (!$foundControllerType) { return }

# Work out what area the controller is in, since we need to put viewmodels/views in the same area
$areaMatch = [System.Text.RegularExpressions.Regex]::Match($foundControllerType.Namespace.FullName, "^(.*)\.Areas\.(.*)\.Controllers")
$areaName = if ($areaMatch.Success) { $areaMatch.Groups[2].Value }

if ($ViewModel -or $WithViewModel) {
	# First try to find an existing view model class
	if ($ViewModel) {
		$foundViewModel = Get-ProjectType $ViewModel -Project $Project -ErrorAction SilentlyContinue
	}
	# If not found or specified, try to create a new one
	if (!$foundViewModel) {
		if (!$ViewModel) { $ViewModel = $Action + "ViewModel" }
		if ($ViewModel.Contains(".")) { $ViewModel = $ViewModel.Substring($ViewModel.LastIndexOf(".") + 1) }

		# Decide where to put the new view model class
		$defaultNamespace = (Get-Project $Project).Properties.Item("DefaultNamespace").Value
		$viewModelNamespace = if ($areaName) { "$defaultNamespace.Areas.$areaName.Models" } else { "$defaultNamespace.Models" }
		$viewModelOutputPath = if ($areaName) { "Areas\$areaName\Models\$ViewModel" } else { "Models\$ViewModel" }

		# Actually create the view model class
		Add-ProjectItemViaTemplate $viewModelOutputPath -Template ViewModel -Model @{ ClassName = $ViewModel; Namespace = $viewModelNamespace; DefaultNamespace = $defaultNamespace; } `
			-SuccessMessage "Added view model class at {0}" -TemplateFolders $TemplateFolders -Project $Project -CodeLanguage $CodeLanguage -Force:$Force
		$foundViewModel = Get-ProjectType ($viewModelNamespace + "." + $ViewModel) -Project $Project
	}

	# Also import the view model namespace if not already imported
	$existingImport = $foundControllerType.ProjectItem.FileCodeModel.CodeElements | ?{ ($_.Kind -eq 35) -and ($_.Namespace -eq $foundViewModel.Namespace.FullName) }
	if (!$existingImport) {
		$foundControllerType.ProjectItem.FileCodeModel.AddImport($foundViewModel.Namespace.FullName) | Out-Null
	}
} 

# Add the action method
$template = if ($Post) { "ActionPost" } else { "Action" }
$actionMethodName = if ($Post) { $Action + "Post" } else { $Action }
Add-ClassMemberViaTemplate -Name $actionMethodName -CodeClass $foundControllerType -Template $template `
	-Model @{ Action = $Action; ViewModel = [MarshalByRefObject]$foundViewModel } `
	-SuccessMessage "Added action method $Action to $($foundControllerType.Name)" `
	-TemplateFolders $TemplateFolders -Project $Project -CodeLanguage $CodeLanguage -Force:$Force

# Finally, create a view
$viewModelType = if ($foundViewModel) { $foundViewModel.FullName } else { $null }
if (!$NoChildItems) {
	Scaffold View $Controller $Action -ModelType $viewModelType -Area $areaName -Force:$Force
}

# Return info about what we did in case other scaffolders wish to consume it
return @{
	Controller = $foundControllerType;
	ActionMethod = $actionMethodName;
	ViewModel = $foundViewModel;
}