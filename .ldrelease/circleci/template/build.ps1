
# Standard build.ps1 for .NET projects built on Windows

$ErrorActionPreference = "Stop"

$scriptDir = split-path -parent $MyInvocation.MyCommand.Definition
Import-Module "$scriptDir\helpers.psm1" -Force

$projects = GetSourceProjects

if ($projects.count -eq 0) {
    throw "No projects were found under .\src; does this solution use the standard directory layout?"
}

Write-Host "Building all projects in Debug configuration"
Write-Host "Projects to be built: $($projects | %{$_.name})"
Write-Host

# Suppress the "Welcome to .NET Core!" message that appears the first time you run dotnet
dotnet help > $null

foreach ($project in $projects) {
    ExecuteOrFail { dotnet build -c Debug $project.projectFilePath }
}
