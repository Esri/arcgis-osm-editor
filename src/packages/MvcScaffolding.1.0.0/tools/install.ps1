param($rootPath, $toolsPath, $package, $project)

# Bail out if scaffolding is disabled (probably because you're running an incompatible version of NuGet)
if (-not (Get-Command Invoke-Scaffolder)) { return }

function CountSolutionFilesByExtension($extension) {
	$files = (Get-Project).DTE.Solution `
		| ?{ $_.FileName } `
		| %{ [System.IO.Path]::GetDirectoryName($_.FileName) } `
		| %{ [System.IO.Directory]::EnumerateFiles($_, "*." + $extension, [System.IO.SearchOption]::AllDirectories) }
	($files | Measure-Object).Count
}

function InferPreferredViewEngine() {
	# Assume you want Razor except if you already have some ASPX views and no Razor ones
	if ((CountSolutionFilesByExtension aspx) -eq 0) { return "razor" }
	if (((CountSolutionFilesByExtension cshtml) -gt 0) -or ((CountSolutionFilesByExtension vbhtml) -gt 0)) { return "razor" }
	return "aspx"
}

if ($project) { $projectName = $project.Name }
Get-ProjectItem "InstallationDummyFile.txt" -Project $projectName | %{ $_.Delete() }

Set-DefaultScaffolder -Name Controller -Scaffolder MvcScaffolding.Controller -SolutionWide -DoNotOverwriteExistingSetting
Set-DefaultScaffolder -Name Views -Scaffolder MvcScaffolding.Views -SolutionWide -DoNotOverwriteExistingSetting
Set-DefaultScaffolder -Name Action -Scaffolder MvcScaffolding.Action -SolutionWide -DoNotOverwriteExistingSetting
Set-DefaultScaffolder -Name UnitTest -Scaffolder MvcScaffolding.ActionUnitTest -SolutionWide -DoNotOverwriteExistingSetting

# Infer which view engine you're using based on the files in your project
$viewScaffolder = if ([string](InferPreferredViewEngine) -eq 'aspx') { "MvcScaffolding.AspxView" } else { "MvcScaffolding.RazorView" }
Set-DefaultScaffolder -Name View -Scaffolder $viewScaffolder -SolutionWide -DoNotOverwriteExistingSetting