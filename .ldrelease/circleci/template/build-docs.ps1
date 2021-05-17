
# Standard build-docs.ps1 for .NET projects buitl on Windows, producing HTML
# documentation with DocFX. It builds a single documentation set that may include
# multiple projects.
#
# It has the following preconditions:
# - $LD_RELEASE_DOCS_TITLE is set to the displayable title of the documentation
#   (not including the version number); defaults to "Package Documentation"
# - $LD_RELEASE_DOCS_TARGET_FRAMEWORK is the target framework of the build that the
#   documentation should be based on. The default is "netstandard2.0". This must be
#   a target framework that is built by default when build.ps1 builds the solution.
# - $LD_RELEASE_DOCS_ASSEMBLIES is a space-delimited list of the names of all
#   assemblies that are to be documented (see below).
# - $LD_RELEASE_PROJECT and $LD_RELEASE_VERSION are set as usual by Releaser.
#
# The default is to document all of the assemblies whose projects are under .\src
# (like build.ps1 it assumes that the subdirectories in .\src are named the same as
# their assemblies, and so are their project files). If you specify
# $LD_RELEASE_DOCS_ASSEMBLIES, you should provide the names the projects in .\src
# that you wish to document, and you can also add names of assemblies that are in
# your project's dependencies, if they were published with XML docs.
#
# The source project can provide additional Markdown text as follows:
#
# * `docs-src/index.md`, if provided, will be included on the landing page.
# * `docs-src/namespaces/<fully qualified name of namespace>.md`, if provided, will be
# used as the description of that namespace. The first line is the summary, which
# will appear on both the landing page and the API page for the namespace; the rest
# of the file is the full description, which will appear on the API page for the
# namespace.
#
# Markdown text can include hyperlinks to namespaces, types, etc. using the DocFX
# syntax <xref:Fully.Qualified.Name.Of.Thing>.

# Terminate the script if any PowerShell command fails, or if we use an unknown variable
$ErrorActionPreference = "Stop"
set-strictmode -version latest

$docsTitle = "$env:LD_RELEASE_DOCS_TITLE"
if ($docsTitle -eq "") {
    $docsTitle = "Package Documentation"
}
$targetFramework = "$env:LD_RELEASE_DOCS_TARGET_FRAMEWORK"
if ($targetFramework -eq "") {
    $targetFramework = "netstandard2.0"
}
$assembliesParam = "$env:LD_RELEASE_DOCS_ASSEMBLIES"
if ($assembliesParam -eq "") {
    $sourceAssemblyNames = @()
} else {
    $sourceAssemblyNames = $assembliesParam -Split " "
}

# Import helper functions
$scriptDir = split-path -parent $MyInvocation.MyCommand.Definition
import-module "$scriptDir\build-docs-helpers.psm1" -force

# Set up paths
$projectDir = "$(get-location)"

$tempDir = "$env:LD_RELEASE_TEMP_DIR"
if ($tempDir -eq "") {
    $tempDir = "$HOME\temp"
}
new-item -path $tempDir -itemType "directory" -errorAction ignore | out-null

$tempDocsDir = "$tempDir/build-docs"
remove-item $tempDocsDir -recurse -force -errorAction ignore
new-item -path $tempDocsDir -itemType "directory" | out-null

# Install DocFX
if (-not (get-Command docfx -errorAction silentlyContinue))
{
    choco install docfx
}

# Find the assemblies to be documented. If one of them is a dependency rather
# than a project in this directory, its DLL should still have been copied into
# the build products of one of the projects here (as long as the project file
# specified <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>).
$projectNames = @(dir "$projectDir/src") | %{$_.Name}
$mainProjectName = $projectNames[0]
if ($sourceAssemblyNames.count -eq 0) {
    $sourceAssemblyNames = $projectNames
}
$assemblyPaths = @()
foreach ($assemblyName in $sourceAssemblyNames) {
    $dllPath = GetDocumentationInputPath -assemblyName $assemblyName `
        -projectNames $projectNames -targetFramework $targetFramework
    if (-not $dllPath) {
        throw "Could not find $assemblyName.dll in build products"
    }
    $assemblyPaths += $dllPath
}

# Create the input files for DocFX
set-content -path "$tempDocsDir/index.md" $(MakeDocsHomePage `
    -title $docsTitle -version "$env:LD_RELEASE_VERSION" -projectDir $projectDir)
set-content -path "$tempDocsDir/toc.yml" $(MakeDocsNavBarData)
set-content -path "$tempDocsDir/overwrites.md" $(MakeDocsOverwrites -projectDir $projectDir)
set-content -path "$tempDocsDir/docfx.json" $(MakeDocfxConfig `
    -projectDir $projectDir -inputPaths $assemblyPaths)

# Run the documentation generator
set-location $tempDocsDir
docfx docfx.json
set-location $projectDir

# Newer versions of Releaser will pass in $LD_RELEASE_DOCS_DIR, where we can
# put all the documentation files. Older versions which don't set that variable
# expect us to provide a "docs.zip" archive in ./artifacts.
$builtDocsDir = "$tempDocsDir/build/html"
$docsOutDir = "$env:LD_RELEASE_DOCS_DIR"
if ($docsOutDir -ne "") {
    remove-item $docsOutDir -recurse -force -errorAction ignore
    copy-item -path $builtDocsDir -destination $docsOutDir -recurse
} else {
    $artifactsDir = "$projectDir/artifacts"
    new-item -path $artifactsDir -itemType "directory" -errorAction ignore | out-null
    $zipPath = "$artifactsDir/docs.zip"
    remove-item $zipPath -force -errorAction ignore
    Zip -sourcePath $builtDocsDir -zipFile $zipPath
}
