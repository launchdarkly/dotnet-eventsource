
# Standard PowerShell script for generating HTML documentation with DocFX from any
# .NET project that uses LaunchDarkly's standard project layout.
#
# The variables LD_RELEASE_PROJECT, LD_RELEASE_VERSION, and LD_RELEASE_DOCS_TITLE
# are set when the release job is configured. The expected output of the build is
# an archive file called "docs.zip" in "./artifacts".

if ("$env:LD_RELEASE_DOCS_TITLE" -eq "") {
    Write-Host "Not generating documentation because LD_RELEASE_DOCS_TITLE was not set"
    exit
}

# Terminate the script if any PowerShell command fails, or if we use an unknown variable
$ErrorActionPreference = "Stop"
set-strictmode -version latest

# Import helper functions and set up paths (helpers.psm1 comes from Releaser's
# built-in scripts)
$projectDir = get-location
$tempDir = "$HOME\temp"
$scriptDir = split-path -parent $MyInvocation.MyCommand.Definition
import-module "$scriptDir\circleci\template\helpers.psm1" -force

# Install DocFX
if (-not (get-Command docfx -errorAction silentlyContinue))
{
    choco install docfx
}

$projectName = dir "$projectDir/src" | %{$_.Name}

$tempDocsDir = "$tempDir/build-docs"
remove-item $tempDocsDir -recurse -force -errorAction ignore
new-item -path $tempDocsDir -itemType "directory" | out-null
set-location $tempDocsDir

# Create a minimal home page
set-content -path ./index.md @"
# ${env:LD_RELEASE_DOCS_TITLE}

This site contains the full API reference for ``$projectName`` (version ${env:LD_RELEASE_VERSION}).
Click "API Documentation" above to see all namespaces and types.

For source code, see the [GitHub repository](https://github.com/launchdarkly/${env:LD_RELEASE_PROJECT}).
"@

# Create a minimal navigation list that just points to the root of the API. The path
# "build/api" does not exist in the built HTML docs-- it refers to the intermediate
# build metadata; when DocFX resolves this link during HTML generation, it will become
# a link to the first namespace in the API. Unfortunately, that link resolution appears
# to only work in the navbar and not in Markdown pages, which is why the "index.md"
# text above tells people to click on the navbar link.
set-content -path ./toc.yml @"
- name: API Documentation
  href: build/api/
"@

# Create the docfx.json config file
$jsonEscapedSrcPath = convertTo-json -inputObject "$projectDir/src"
set-content -path docfx.json @"
{
  "metadata": [
    {
      "src": [
        {
          "src": $jsonEscapedSrcPath,
          "files": "**/*.csproj"
        }
      ],
      "dest": "build/api",
      "disableGitFeatures": true,
      "disableDefaultFilter": false
    }
  ],
  "build": {
    "content": [
      {
        "src": "build/api",
        "files": [ "**.yml" ],
        "dest": "api"
      },
      {
        "src": ".",
        "files": [ "toc.yml", "*.md" ]
      }
    ],
    "dest": "build/html",
    "globalMetadata": {
      "_disableContribution": true
    },
    "template": [ "default" ],
    "disableGitFeatures": true
  }
}
"@

# Run the documentation generator
docfx docfx.json

# Make an archive of the output and store it as a single artifact. The
# built-in CompressArchive command in PowerShell 5 produces archives that
# aren't valid on other platforms, so we're using a helper from helpers.psm1
Zip -sourcePath "$tempDocsDir/build/html" -zipFile "$projectDir/artifacts/docs.zip"

set-location $projectDir
