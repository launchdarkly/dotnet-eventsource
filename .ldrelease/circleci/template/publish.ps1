
# Standard publish.ps1 for .NET projects built on Windows

# Terminate the script if any PowerShell command fails
$ErrorActionPreference = "Stop"

# Disable PowerShell progress bars, which cause problems in CircleCI ("Access is denied ... while
# reading the console output buffer")
$ProgressPreference = "SilentlyContinue"

$shouldSign = $true
if ("$env:LD_RELEASE_SKIP_SIGNING" -ne "") {
    $shouldSign = $false
    Write-Host "Will skip code signing because LD_RELEASE_SKIP_SIGNING was set"
}

$scriptDir = split-path -parent $MyInvocation.MyCommand.Definition
Import-Module "$scriptDir\helpers.psm1" -Force

$env:Path += ";C:\Program Files (x86)\Windows Kits\10\bin\10.0.17763.0\x86"  # for signtool

$repoDir = Get-Location

AWSHelperSetDefaultRegion -name "us-east-1"  # from helpers
$nugetApiKey = AWSHelperGetEncryptedParameter -name "/production/common/services/nuget/api_key"  # from helpers

if ($shouldSign) {
    # Get the code signing certificate and password
    $certFile = "catamorphic_code_signing_certificate.p12"
    AWSHelperGetPastebinFile -name $certFile -destination $repoDir  # from helpers
    $certPassword = AWSHelperGetEncryptedParameter -name "/production/common/security/ssl-certificates/code-signing/password"
}

Write-Host
Write-Host "Cleaning before release build"
ExecuteOrFail { dotnet clean } | Out-Null

$projects = GetSourceProjects
Write-Host
Write-Host "Projects to be built: $($projects | %{$_.name})"

# For any projects that use strong-naming, get the base filename of the key file and download it.
foreach ($project in $projects) {
    $m = Select-String -Path $project.projectFilePath -Pattern "<AssemblyOriginatorKeyFile> *[./\\]*([^ <]*) *</AssemblyOriginatorKeyFile>"
    if ($m.Matches.Length -gt 0) {
        $keyFileName = $m.Matches[0].Groups[1].Value
        AWSHelperGetPastebinFile -name $keyFileName -destination $repoDir  # from helpers
    }
}

foreach ($project in $projects) {
    $name = $project.name
    Write-Host
    Write-Host "[$name]: building in Release configuration"
    ExecuteOrFail { dotnet build -c Release $project.projectFilePath }

    if ($shouldSign) {
        $dlls = dir -Path $project.releaseProductsBase -Filter "$name.dll" -Recurse | %{$_.FullName}
        Write-Host
        Write-Host "[$name]: signing assemblies"
        SignFilesWithPFXCertificate -certFile "$repoDir\$certFile" -certPassword $certPassword -filesToSign $dlls  # from helpers
    }

    Write-Host
    Write-Host "[$name]: creating package"
    del "$($project.releaseProductsBase)\*.nupkg"
    ExecuteOrFail { dotnet pack -c Release --no-build $project.projectFilePath }
}

foreach ($project in $projects) {
    $name = $project.name
    $pkg = dir -Path "$($project.releaseProductsBase)\*.nupkg" | %{$_.FullName}
    $sourcePkg = dir -Path "$($project.releaseProductsBase)\*.snupkg" | %{$_.FullName}
    Write-Host
    Write-Host "[$name]: publishing $pkg"
    ExecuteOrFail { dotnet nuget push $pkg --source "https://www.nuget.org" --api-key "$nugetApiKey" }
    Write-Host "[$name]: published $pkg"
    if ($sourcePkg) {
        Write-Host
        Write-Host "[$name]: publishing $sourcePkg"
        ExecuteOrFail { dotnet nuget push $sourcePkg --source "https://www.nuget.org" --api-key "$nugetApiKey" }
        Write-Host "[$name]: published $sourcePkg"    
    }
}
