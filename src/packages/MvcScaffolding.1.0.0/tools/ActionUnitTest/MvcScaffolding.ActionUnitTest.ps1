[T4Scaffolding.Scaffolder(Description = "Creates a unit test stub for an action method")][CmdletBinding()]
param(        
	[parameter(Mandatory = $true, ValueFromPipelineByPropertyName = $true)][string]$Controller,
	[parameter(Mandatory = $true, ValueFromPipelineByPropertyName = $true)][string]$Action,
	[string]$ViewModel,
	[switch]$WithViewModel,
    [string]$Project,
	[string]$CodeLanguage,
	[string[]]$TemplateFolders,
	[switch]$Force = $false
)

# Ensure the controller name ends with "Controller"
$Controller = [System.Text.RegularExpressions.Regex]::Replace($Controller, "Controller$", "", [System.Text.RegularExpressions.RegexOptions]::IgnoreCase) + "Controller"

# Find the unit test project, or abort
$unitTestProject = Get-Project ($Project + ".Test") -ErrorAction SilentlyContinue
if (!$unitTestProject) { $unitTestProject = Get-Project ($Project + ".Tests") -ErrorAction SilentlyContinue }
if (!$unitTestProject) { throw "Cannot find a unit test project corresponding to project '$Project'" }

# Ensure you've referenced System.Web.Mvc
$unitTestProject.Object.References.Add("System.Web.Mvc") | Out-Null

# Create the test fixture class
$foundControllerClass = Get-ProjectType $Controller -Project $Project
if (!$foundControllerClass) { return }
$testClassName = $foundControllerClass.Name + "Test"
$testClassNamespace = $unitTestProject.Properties.Item("DefaultNamespace").Value
$defaultNamespace = $unitTestProject.Properties.Item("DefaultNamespace").Value
Add-ProjectItemViaTemplate $testClassName -Template TestClass -Model @{ ClassName = $testClassName; Namespace = $testClassNamespace; DefaultNamespace = $defaultNamespace; Controller = [MarshalByRefObject]$foundControllerClass } `
	-SuccessMessage "Added unit test class at {0}" -TemplateFolders $TemplateFolders -Project $unitTestProject.Name -CodeLanguage $CodeLanguage -Force:$Force

# Add a test method to it
if ($WithViewModel -and !$ViewModel) { $ViewModel = $Action + "ViewModel" }
$foundViewModel = if ($ViewModel) { Get-ProjectType $ViewModel -Project $Project }
if ($ViewModel -and !$foundViewModel) { return }
$testClass = Get-ProjectType $testClassName -Project $unitTestProject.Name
$testMethodName = $Action + "Test"
Add-ClassMemberViaTemplate -Name $testMethodName -CodeClass $testClass -Template TestMethod -Model @{ 
		TestMethod = $testMethodName; 
		ActionMethod = $Action; 
		Controller = [MarshalByRefObject]$foundControllerClass;
		ViewModel = [MarshalByRefObject]$foundViewModel;
	} -SuccessMessage "Added unit test method $testMethodName to $($testClass.Name)" `
	-TemplateFolders $TemplateFolders -Project $unitTestProject.Name -CodeLanguage $CodeLanguage -Force:$Force

# Also import the view model namespace if not already imported
if ($foundViewModel) {
	$existingImport = $testClass.ProjectItem.FileCodeModel.CodeElements | ?{ ($_.Kind -eq 35) -and ($_.Namespace -eq $foundViewModel.Namespace.FullName) }
	if (!$existingImport) {
		$testClass.ProjectItem.FileCodeModel.AddImport($foundViewModel.Namespace.FullName) | Out-Null
	}
}