param(
    [string]$password = "",
    [string]$certFile = "catamorphic_code_signing_certificate.p12",
    [switch]$skipPublish = $false,
    [switch]$skipSign = $false,
    [switch]$skipTests = $false
)

#
# This PowerShell script builds and uploads NuGet packages for the current solution.
# See README.md for more details.
#

# This helper function comes from https://github.com/psake/psake - it allows us to terminate the
# script if any executable returns an error code, which PowerShell won't otherwise do
function Exec {
    [CmdletBinding()]
    param(
        [Parameter(Position=0,Mandatory=1)][scriptblock]$cmd,
        [Parameter(Position=1,Mandatory=0)][string]$errorMessage = ("Error executing command {0}" -f $cmd)
    )
    & $cmd
    if ($lastexitcode -ne 0) {
        throw ($errorMessage)
    }
}

function ProgressMessage([string]$text) {
    Write-Host $text -ForegroundColor cyan
}

function WarningMessage([string]$text) {
    Write-Host $text -ForegroundColor black -BackgroundColor yellow
}

function SuccessMessage([string]$text) {
    Write-Host $text -ForegroundColor black -BackgroundColor green
}

if ((-not $skipSign) -and (-not (Test-Path $certFile))) {
    throw ("Certificate file $certfile must be in the current directory. Download it from s3://launchdarkly-pastebin/ci/dotnet/")
}

if ((-not $skipSign) -and ($password -eq "")) {
    throw "-password is required unless you specify -skipSign"
}

if ((-not $skipPublish) -and $skipSign) {
    throw "Cannot publish without signing"
}

$projects = dir src | %{$_.Name}
$testProjects = dir test | %{$_.Name}

if ($projects.count -eq 0) {
    throw "No projects were found under .\src; does this solution use the standard directory layout?"
}

ProgressMessage ""
ProgressMessage "Projects to be released: $projects"
ProgressMessage "Test projects: $testProjects"
ProgressMessage ""

Exec { dotnet clean }

if ($skipTests) {
    WarningMessage "Skipping debug build and unit tests"
} else {
    ProgressMessage "Building all projects in Debug configuration"
    Exec { dotnet build -c Debug }
    ProgressMessage "Running tests"
    Exec { dotnet test -c Debug ($testProjects | %{"test\$_"}) }
    Exec { dotnet clean }
}

foreach ($project in $projects) {
    ProgressMessage "[$project]: building in Release configuration"
    Exec { dotnet build -c Release "src\$project" }

    $dlls = dir -Path "src\$project\bin\Release" -Filter "$project.dll" -Recurse | %{$_.FullName}
    if ($skipSign) {
        WarningMessage "[$project]: skipping signing of assemblies"
    } else {
        ProgressMessage "[$project]: signing assemblies"
        Exec { signtool sign /f $certFile /p $password $dlls }
    }

    ProgressMessage "[$project]: creating package"
    del "src\$project\bin\Release\*.nupkg"
    Exec { dotnet pack -c Release "src\$project" }
}

foreach ($project in $projects) {
    $pkg = dir -Path "src\$project\bin\Release\*.nupkg" | %{$_.FullName}
    if ($skipPublish) {
        WarningMessage "[$project]: skipping publishing"
    } else {
        Exec { dotnet nuget push $pkg -s https://www.nuget.org }
        SuccessMessage "[$project]: published $pkg"
    }
}
