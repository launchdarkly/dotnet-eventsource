
$docsSrcPath = "docs-src"

function MakeDocsHomePage {
    param(
        [Parameter(Mandatory)][string]$title,
        [Parameter(Mandatory)][string]$version,
        [Parameter(Mandatory)][string]$projectDir
    )

    $markdown = "# $title`n`n**(version $version)**`n`n"
    $customPagePath = "$projectDir/$docsSrcPath/index.md"
    if (test-path $customPagePath) {
        $markdown += get-content -path $customPagePath -raw
    }

    # There's no single entry point for the API documentation, and the sidebar of
    # namespaces doesn't show up on the home page in DocFX's default template, so we'll
    # add a list of them here if they were documented under ./docs-src/namespaces.
    $namespaces = GetNamespaceDocsData -projectDir $projectDir
    if ($namespaces.length -ne 0) {
        $markdown += "`n`n## Namespaces`n`n"
        foreach ($namespace in $namespaces) {
            $markdown += "**<xref:$($namespace.name)>**: $($namespace.summary)`n`n"
        }
    }

    return $Markdown
}

function MakeDocsNavBarData {
    # Create a minimal navigation list that just points to the root of the API. The path
    # "build/api" does not exist in the built HTML docs-- it refers to the intermediate
    # build metadata; when DocFX resolves this link during HTML generation, it will become
    # a link to the first namespace in the API. Unfortunately, that link resolution appears
    # to only work in the navbar and not in Markdown pages.
    return @"
    - name: API Documentation
      href: build/api/
"@
}

function MakeDocsOverwrites {
    # Create a YAML document containing any "overwrite" data that we want to inject into
    # the generated docs, based on the files (if any) that are in the project under docs-src/.
    # See comments in build-docs.ps1 for the format of these files.
    param(
        [Parameter(Mandatory)][string]$projectDir
    )

    $yml = ""
    foreach ($namespace in (GetNamespaceDocsData -projectDir $projectDir)) {
        $yml += "---`n"
        $yml += "uid: $($namespace.name)`n"
        $yml += "summary: *content`n"
        $yml += "---`n"
        $yml += "$($namespace.summary)`n`n"
        $yml += "---`n"
        $yml += "uid: $($namespace.name)`n"
        $yml += "remarks: *content`n"
        $yml += "---`n"
        $yml += "$($namespace.remarks)`n`n"
    }

    return $yml
}

function GetNamespaceDocsData {
    param(
        [Parameter(Mandatory)][string]$projectDir
    )

    $ret = @()
    $namespacesDir = "$projectDir/$docsSrcPath/namespaces"
    if (test-path $namespacesDir) {
        $namespaceFiles = @(dir $namespacesDir) | %{$_.Name}
        foreach ($namespaceFile in $namespaceFiles) {
            $namespace = $namespaceFile -replace ".md"
            $content = get-content -path "$namespacesDir/$namespaceFile" -raw
            $lines = $content.Split([string[]]"`r`n", [StringSplitOptions]::None)
            $summary = $lines[0]
            if ($lines.length -gt 1) {
                $remarks = $lines[1..($lines.length-1)] -join "`n"
            } else {
                $remarks = ""
            }
            $item = @{
                name = $namespace
                summary = $summary
                remarks = $remarks
            }
            $ret += $item
        }
    }
    return $ret | sort-object { $_.name }
}

function MakeDocfxConfig {
    param(
        [Parameter(Mandatory)][string]$projectDir,
        [Parameter(Mandatory)][string[]]$inputPaths
    )
    return @"
{
  "metadata": [
    {
      "src": [
        {
          "src": $(convertTo-json -inputObject $projectDir),
          "files": $(convertTo-json -inputObject $inputPaths)
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
        "files": [ "toc.yml", "index.md" ]
      }
    ],
    "overwrite": [
      "overwrites.md"
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
}

function GetDocumentationInputPath {
    param(
        [Parameter(Mandatory)][string]$assemblyName,
        [Parameter(Mandatory)][string[]]$projectNames,
        [Parameter(Mandatory)][string]$targetFramework
    )

    $nugetCacheDir = "$HOME\.nuget\packages"
    foreach ($projectName in $projectNames) {
        $binPath = "src/$projectName/bin/Debug/$targetFramework"
        $dllPath = "$binPath/$assemblyName.dll"
        if (test-path $dllPath) {
            $xmlPath = "$binPath/$assemblyName.xml"
            if (-not (test-path $xmlPath)) {
                # If this is a dependency and not something we just built, then
                # we may have to get its XML metadata out of the NuGet cache.
                # Unfortunately, that'll require us to find the version of the
                # dependency first, since the cache is versioned.
                $dependencyVersion = FindDependencyVersion `
                    -dependencyPackage $assemblyName -projectName $projectName
                if (-not $dependencyVersion) {
                    throw "Could not find dependency version for $assemblyName"
                }
                $cachedXmlPath = "$nugetCacheDir\$assemblyName\$dependencyVersion\lib\$targetFramework\$assemblyName.xml"
                if (-not (test-path $cachedXmlPath)) {
                    throw "Could not find $assemblyName.xml in build products or NuGet cache"
                }
                copy-item -path $cachedXmlPath -destination $xmlPath
            }
            return $dllPath
        }
    }
    return $null
}

function FindDependencyVersion {
    param(
        [Parameter(Mandatory)][string]$dependencyPackage,
        [Parameter(Mandatory)][string[]]$projectName
    )

    $projectFilePath = "src/$projectName/$projectName.csproj"
    $match = select-string -path $projectFilePath `
        -pattern "<PackageReference.*""$dependencyPackage"".*""([^""]*)"""
    if ($match.Matches.Length -eq 1) {
        return $match.Matches[0].Groups[1].Value
    }
    return $null
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
