
# Helper functions used by the dotnet-windows scripts.

$ErrorActionPreference = "Stop"

function CreateDirectoryIfNotExists {
    param(
        [Parameter(Mandatory)][string]$path
    )
    New-Item $path -ItemType "directory" -Force | Out-Null    
}

function DeleteAndRecreateDirectory {
    param(
        [Parameter(Mandatory)][string]$path
    )
    if (Test-Path $path) {
        Remove-Item -Path $path -Recurse -Force
    }
    CreateDirectoryIfNotExists -path $path
}

function Unzip {
    param(
        [Parameter(Mandatory)][string]$zipFile,
        [Parameter(Mandatory)][string]$destination
    )
    [Reflection.Assembly]::LoadWithPartialName( "System.IO.Compression.FileSystem" ) | Out-Null
    [IO.Compression.ZipFile]::ExtractToDirectory($zipFile, $destination)
    # We could also expand a .zip with the PowerShell command Expand-Archive, except it doesn't play
    # well with the CircleCI environment somehow (another "console output buffer error")
}

# see: https://stackoverflow.com/questions/27289115/system-io-compression-zipfile-net-4-5-output-zip-in-not-suitable-for-linux-mac
class FixZipFilePathEncoding : System.Text.UTF8Encoding {
    FixZipFilePathEncoding() : base($true) { }
    [byte[]] GetBytes([string] $s)
    {
        $s = $s.Replace("\", "/");
        return ([System.Text.UTF8Encoding]$this).GetBytes($s);
    }
}

function Zip {
    param(
        [Parameter(Mandatory)][string]$sourcePath,
        [Parameter(Mandatory)][string]$zipFile
    )
    [Reflection.Assembly]::LoadWithPartialName( "System.IO.Compression.FileSystem" ) | Out-Null
    [System.IO.Compression.ZipFile]::CreateFromDirectory($sourcePath, $zipFile, `
        [System.IO.Compression.CompressionLevel]::Optimal, $false,
        [FixZipFilePathEncoding]::new())
}

# This helper function is based on https://github.com/psake/psake - it allows us to terminate the
# script if any external command (like "aws" or "dotnet", rather than a PowerShell cmdlet) returns
# an error code, which PowerShell won't otherwise do.
function ExecuteOrFail {
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

function ExecuteAndCaptureOrFail {
    [CmdletBinding()]
    param(
        [Parameter(Position=0,Mandatory=1)][scriptblock]$cmd,
        [Parameter(Position=1,Mandatory=0)][string]$errorMessage = ("Error executing command {0}" -f $cmd)
    )
    $result = & $cmd
    if ($lastexitcode -ne 0) {
        throw ($errorMessage)
    }
    return $result
}

# AWS helper functions - currently we are using the "aws" command rather than the AWS PowerShell tools, since
# it is preinstalled by CircleCI. If they ever start preinstalling the AWS PowerShell tools, we should probably
# switch to using those. The AWS credentials must have already been configured in CircleCI for the current project.

function AWSHelperSetDefaultRegion {
    param(
        [Parameter(Mandatory)][string]$name
    )
    ExecuteOrFail { aws configure set default.region "$name" }
}

function AWSHelperGetPastebinFile {
    param(
        [Parameter(Mandatory)][string]$name,
        [Parameter(Mandatory)][string]$destination
    )
    $s3Base = "s3://launchdarkly-pastebin/ci/dotnet"
    ExecuteOrFail { aws s3 cp "$s3Base/$name" "$destination\$name" }
}

function AWSHelperGetEncryptedParameter {
    param(
        [Parameter(Mandatory)][string]$name
    )
    return ExecuteAndCaptureOrFail { aws ssm get-parameter --name $name --with-decryption --query "Parameter.Value" --output text }
}

# This helper function for code-signing currently uses certutil and signtool rather than the equivalent
# PowerShell 6 commands, because CircleCI only provides PowerShell 5.
function SignFilesWithPFXCertificate {
    param(
        [Parameter(Mandatory)][string]$certFile,
        [Parameter(Mandatory)][string]$certPassword,
        [Parameter(Mandatory)][string[]]$filesToSign
    )
    # We must import the certificate before signing, because even though signtool supports .p12 files,
    # signtool won't actually look in the .p12 file to get the private key.
    $localStoreName = "CA"
    $certNameSubstring = "Catamorphic Co."
    ExecuteOrFail { certutil -f -p "$certPassword" -importpfx "$localStoreName" "$certFile" }
    ExecuteOrFail { signtool sign /s "$localStoreName" /n "$certNameSubstring" $filesToSign }
}

# The following helper functions assume that the source files are laid out like this:
# ./
#   AnySolutionName.sln
#   src/
#     AssemblyName1/
#       AssemblyName1.csproj
#     AssemblyName2/
#       AssemblyName2.csproj
#   test/
#     AnyTestProjectName/
#       AnyTestProjectName.csproj

# Returns array of hashtables with name, sourceDir, projectFilePath, buildProductsDir
function GetSourceProjects {
    param(
        [Parameter()][string]$targetFramework
    )
    return dir src | %{@{
        name = $_.Name;
        sourceDir = $_.FullName;
        projectFilePath = "$($_.FullName)\$($_.Name).csproj";
        debugProductsBase = "$($_.FullName)\bin\Debug";
        releaseProductsBase = "$($_.FullName)\bin\Release";
        buildProductsDir = $(if ($targetFramework) { "$($_.FullName)\bin\Debug\$targetFramework" } else { "" });
    }}
}

function GetTestProjects {
    return dir test | %{@{
        name = $_.Name;
        sourceDir = $_.FullName;
        projectFilePath = "$($_.FullName)\$($_.Name).csproj";
    }}
}