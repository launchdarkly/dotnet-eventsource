
# Standard test.ps1 for .NET projects built on Windows

# Note that for projects that support older versions of .NET Standard, we usually run the regular
# CI tests for netcoreapp1.1 as well as netcoreapp2.0. But 1.1 is not available by default on the
# CircleCI host, and we can assume that we've already done the usual CI tests before a release -
# this is just a quick sanity check, so we can just run 2.0.

$ErrorActionPreference = "Stop"

$scriptDir = split-path -parent $MyInvocation.MyCommand.Definition
Import-Module "$scriptDir\helpers.psm1" -Force

# Some of our tests use WireMock.Net, which uses the ASP.NET Core web server, which outputs a lot
# of annoying status messages unless we set this:
[Environment]::SetEnvironmentVariable("ASPNETCORE_SUPPRESSSTATUSMESSAGES", "true", "Process")

$testProjects = GetTestProjects

Write-Host
if ($testProjects.count -eq 0) {
    Write-Host "No projects were found under .\test; skipping test step"
} else {
    $targetFramework = "$env:LD_RELEASE_TEST_TARGET_FRAMEWORK"
    if ($targetFramework -eq "") {
        $targetFramework = "net45"
    }    
    Write-Host "Running tests"
    Write-Host "Test projects: $($testProjects | %{$_.name})"
    Write-Host
	foreach ($project in $testProjects) {
		ExecuteOrFail { dotnet test -c Debug $project.projectFilePath -f $targetFramework }
	}
}
