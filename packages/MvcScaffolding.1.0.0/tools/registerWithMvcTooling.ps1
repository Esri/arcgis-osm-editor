# Ensure we're on the right version of the tooling. If not, there's nothing for us to do here.
$toolingExists = [System.AppDomain]::CurrentDomain.GetAssemblies() | ?{ $_.GetType("Microsoft.VisualStudio.Web.Mvc.Scaffolding.ScaffolderProviders") }
if (!$toolingExists) { return }

# Todo: Scope the following to this module if possible
function global:MvcScaffoldingHashTableToPsObject($hashOfScriptMethods) {
    $result = New-Object PSObject
	$hashOfScriptMethods.Keys | %{ Add-Member -InputObject $result -Member ScriptMethod -Name $_ -Value $hashOfScriptMethods[$_] }
	$result
}

function global:MvcScaffoldingInvokeViaScriptExecutor($scriptToExecute) {
	$completeScript = @"
		try {
			# Activate output pane
			`$packageManagerOutputPaneGuid = "{CEC55EC8-CC51-40E7-9243-57B87A6F6BEB}"
			`$dteService = [NuGet.VisualStudio.ServiceLocator].GetMethods() | ?{ `$_.Name -eq 'GetInstance' } | %{ `$_.MakeGenericMethod([Microsoft.VisualStudio.Shell.Interop.SDTE]).Invoke(`$null, [Array]`$null) }
			`$outputWindow = `$dteService.Windows.Item([EnvDTE.Constants]::vsWindowKindOutput)
			`$packageManagerOutputPane = `$outputWindow.Object.OutputWindowPanes.Item(`$packageManagerOutputPaneGuid)
			`$packageManagerOutputPane.Clear()
			`$packageManagerOutputPane.Activate()
			`$outputWindow.Activate()

			# Invoke requested script
			$scriptToExecute			
		} catch {
			[System.Windows.Forms.MessageBox]::Show("An error occurred during scaffolding:`n`n`$(`$_.Exception.ToString())`n`nYou may need to upgrade to a newer version of MvcScaffolding.", "Scaffolding error", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Error) | Out-Null
		}
"@
	try {
		# Write the script to disk
		$tempDir = Join-Path $env:Temp ([System.Guid]::NewGuid())
		$tempScriptFilename = Join-Path $tempDir "tools\tempScript.ps1"
		md $tempdir
		md ([System.IO.Path]::GetDirectoryName($tempScriptFilename))
		Set-Content -Path $tempScriptFilename -Value $completeScript

		# Ensure we're on the right NuGet version
		$scriptExecutorExists = [System.AppDomain]::CurrentDomain.GetAssemblies() | ?{ $_.GetType("NuGet.VisualStudio.IScriptExecutor") }
		if (!$scriptExecutorExists) {
			[System.Windows.Forms.MessageBox]::Show("Sorry, this operation requires NuGet 1.2 or later.", "Scaffolding error", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Error) | Out-Null
			return
		}

		# Invoke via IScriptExecutor, then clean up
		$scriptExecutor = [NuGet.VisualStudio.ServiceLocator].GetMethods() | ?{ $_.Name -eq 'GetInstance' } | %{ $_.MakeGenericMethod([NuGet.VisualStudio.IScriptExecutor]).Invoke($null, [Array]$null) }
		$scriptExecutor.Execute($tempDir, "tempScript.ps1", $null, $null, (New-Object NuGet.NullLogger))
		rmdir $tempdir -Force -Recurse
	} catch {
		[System.Windows.Forms.MessageBox]::Show("An error occurred during scaffolding:`n`n$($_.Exception.ToString())`n`nYou may need to upgrade to a newer version of MvcScaffolding.", "Scaffolding error", [System.Windows.Forms.MessageBoxButtons]::OK, [System.Windows.Forms.MessageBoxIcon]::Error) | Out-Null
	}
}

$mvcScaffoldingProvider = global:MvcScaffoldingHashTableToPsObject @{
	ID = { "{9EC893D9-B925-403C-B785-A50545149521}" };
	GetControllerScaffolders = {
		param($project)
		$allControllerScaffolders = Get-Scaffolder -Project $project.Name -IncludeHidden | ?{ $_.ScaffolderAttribute -is [T4Scaffolding.ControllerScaffolderAttribute] }
		if (!$allControllerScaffolders) { return @() }

		$result = $allControllerScaffolders | %{
			global:MvcScaffoldingHashTableToPsObject @{
				ID = { $_.Name }.GetNewClosure();
				DisplayName = { "MvcScaffolding: " + $_.ScaffolderAttribute.DisplayName }.GetNewClosure();
				SupportsModelType = { $_.ScaffolderAttribute.SupportsModelType }.GetNewClosure();
				SupportsDataContextType = { $_.ScaffolderAttribute.SupportsDataContextType }.GetNewClosure();
				ViewsScaffolders = { 
					if (!$_.ScaffolderAttribute.SupportsViewScaffolder) { return @() }
					$viewScaffolderSelector = $_.ScaffolderAttribute.ViewScaffolderSelector
					if (!$viewScaffolderSelector) { $viewScaffolderSelector = [T4Scaffolding.ViewScaffolderAttribute] }
					$viewScaffolders = Get-Scaffolder -Project $project.Name -IncludeHidden | ?{ $viewScaffolderSelector.IsAssignableFrom($_.ScaffolderAttribute.GetType()) }
						
					# Put default view engine at the top of the list so it's the default selection until you choose otherwise
					$defaultViewScaffolder = (Get-DefaultScaffolder View).ScaffolderName
					$viewScaffolders = $viewScaffolders | Sort-Object { if($_.Name -eq $defaultViewScaffolder) { "" } else { $_.Name } }
										
					$result = $viewScaffolders | %{
						global:MvcScaffoldingHashTableToPsObject @{
							ID = { $_.Name }.GetNewClosure();
							DisplayName = { $_.ScaffolderAttribute.DisplayName }.GetNewClosure();
							LayoutPageFilter = { $_.ScaffolderAttribute.LayoutPageFilter }.GetNewClosure();
						}
					}
					return ,[Array]$result
				}.GetNewClosure();
				Execute = { 
					param($container, $controllerName, $modelType, $dataContextType, $viewsScaffolder, $options)

					# Infer possible area name from container location
					$areaName = $null
					if ($container -is [EnvDTE.ProjectItem]) {
						$containerNamespace = $container.Properties.Item("DefaultNamespace").Value
						$areaMatch = [System.Text.RegularExpressions.Regex]::Match($containerNamespace, "(^|\.)Areas\.(.*)\.Controllers($|\.)")
						$areaName = if ($areaMatch.Success) { $areaMatch.Groups[2].Value }
					}

					$scriptToExecute = @"
						# These are all the args we may pass to the target scaffolder...
						`$possibleArgs = @{
							ControllerName = `"$controllerName`";
							ModelType = `"$modelType`";
							DbContextType = `"$dataContextType`";
							Project = `"$($project.Name)`";
							Area = $(if($areaName) { "`"" + $areaName + "`"" } else { "`$null" });
							ViewScaffolder = $(if($viewsScaffolder) { "`"" + $viewsScaffolder.ID + "`"" } else { "`$null" });
							Force = $(if($options.OverwriteViews -or $options.OverwriteController) { "`$true" } else { "`$false" });
							ForceMode = $(if($options.OverwriteViews -and $options.OverwriteController) { "`$null" } else { if($options.OverwriteViews) { "`"PreserveController`"" } else { "`"ControllerOnly`"" } });
							Layout = $(if($options.UseLayout) { "`"" + $options.Layout + "`"" } else { "`$null" });
							PrimarySectionName = $(if($options.PrimarySectionName) { "`"" + $options.PrimarySectionName + "`"" } else { "`$null" });
							ReferenceScriptLibraries = $(if($options.ReferenceScriptLibraries) { "`$true" } else { "`$false" });
						}
						# ... but we only pass the ones it actually accepts
						`$actualArgs = @{}
						`$acceptedParameterNames = (Get-Command Invoke-Scaffolder -ArgumentList @(`"$($_.Name)`")).Parameters.Keys
						`$acceptedParameterNames | ?{ `$possibleArgs.ContainsKey(`"`$_`") } | %{ `$actualArgs.Add(`$_, `$possibleArgs[`$_]) }
						Invoke-Scaffolder `"$($_.Name)`" @actualArgs
"@
					global:MvcScaffoldingInvokeViaScriptExecutor $scriptToExecute | Out-Null

					# Trick PowerShell into not unrolling the return collection by wrapping it in a further collection
					$result = [System.Activator]::CreateInstance(([System.Collections.Generic.List``1].MakeGenericType([System.Object])))
					$result.Add(([System.Activator]::CreateInstance(([System.Collections.Generic.List``1].MakeGenericType([EnvDTE.ProjectItem])))))
					return $result
				}.GetNewClosure();				
			}
		}
		return ,[Array]$result
	}
}

# Remove existing MvcScaffolding providers
$allProviders = [Microsoft.VisualStudio.Web.Mvc.Scaffolding.ScaffolderProviders]::Providers
$existingMvcScaffoldingProviders = $allProviders | ?{ $_.ID -eq $mvcScaffoldingProvider.ID() } 
$existingMvcScaffoldingProviders | %{ $allProviders.Remove($_) } | Out-Null

# Add new provider
$newProvider = New-Object Microsoft.VisualStudio.Web.Mvc.Scaffolding.PowerShell.PowerShellScaffolderProvider($mvcScaffoldingProvider)
$allProviders.Add($newProvider) | Out-Null