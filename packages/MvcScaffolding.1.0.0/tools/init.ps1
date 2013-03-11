param($rootPath, $toolsPath, $package, $project)

# Note that as of NuGet 1.0, the init.ps1 scripts run in an undefined order, 
# so this script must not depend on T4Scaffolding already being initialized.

# Simplistic tab expansion
if (!$global:scaffolderTabExpansion) { $global:scaffolderTabExpansion = @{ } }
$global:scaffolderTabExpansion["MvcScaffolding.RazorView"] = $global:scaffolderTabExpansion["MvcScaffolding.AspxView"] = {
	param($filter, $allTokens)
	$secondLastToken = $allTokens[-2]
	if ($secondLastToken -eq 'Template') {
		return @("Create", "Delete", "Details", "Edit", "Index")
	}
}

. (Join-Path $toolsPath "registerWithMvcTooling.ps1")