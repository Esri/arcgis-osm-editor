param($rootPath, $toolsPath, $package, $project)

if ([T4Scaffolding.ScaffolderAttribute].Assembly.GetName().Version -ne "1.0.0.1") {
	# Abort installation, because we can't update the T4Scaffolding assembly while it's already loaded,
	# and logic in the installation process involves referencing types in the latest version of that assembly.
	$upgradeProcedure = @"
	 1. Uninstall the T4Scaffolding package from all projects in your solution (along with any dependent packages, such as MvcScaffolding)
	 2. Restart Visual Studio
	 ... then you can install the package you require (e.g., MvcScaffolding or T4Scaffolding)
"@
	Write-Warning "---"
	Write-Warning "Warning: A different version of T4Scaffolding is already running in this instance of Visual Studio, so installation cannot be completed at this time."
	Write-Warning "To upgrade, you must: "
	Write-Warning $upgradeProcedure
	Write-Warning "---"
	if (Get-Module T4Scaffolding) {
		# Disable scaffolding as much as possible until VS is restarted
		Remove-Module T4Scaffolding
	}
	throw "---`nInstallation cannot proceed until you follow this procedure: `n" + $upgradeProcedure + "`n---"
}

# Bail out if scaffolding is disabled (probably because you're running an incompatible version of NuGet)
if (-not (Get-Command Invoke-Scaffolder)) { return }

if ($project) { $projectName = $project.Name }
Get-ProjectItem "InstallationDummyFile.txt" -Project $projectName | %{ $_.Delete() }

Set-DefaultScaffolder -Name DbContext -Scaffolder T4Scaffolding.EFDbContext -SolutionWide -DoNotOverwriteExistingSetting
Set-DefaultScaffolder -Name Repository -Scaffolder T4Scaffolding.EFRepository -SolutionWide -DoNotOverwriteExistingSetting
Set-DefaultScaffolder -Name CustomTemplate -Scaffolder T4Scaffolding.CustomTemplate -SolutionWide -DoNotOverwriteExistingSetting
Set-DefaultScaffolder -Name CustomScaffolder -Scaffolder T4Scaffolding.CustomScaffolder -SolutionWide -DoNotOverwriteExistingSetting
