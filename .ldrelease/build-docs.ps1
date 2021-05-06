
if (-not (get-Command docfx -errorAction silentlyContinue))
{
    choco install docfx
}

$projectDir = $(get-location)
$tempDir = "$HOME\temp"

$projectName = dir "${env:LD_RELEASE_PROJECT_DIR}/src" | %{$_.Name}

$tempDocsDir = "$tempDir/build-docs"
remove-item $tempDocsDir -recurse -force -errorAction ignore
new-item -path $tempDocsDir -itemType "directory" | out-null
set-location $tempDocsDir

# Create a minimal home page
set-content -path ./index.md @"
# ${env:LD_RELEASE_DOCS_TITLE}

This site contains the full [API documentation](./api) for `$projectName` (version ${env:LD_RELEASE_VERSION}).

For source code, see the [GitHub repository](https://github.com/launchdarkly/${env:LD_RELEASE_PROJECT}).
"@

# Create a minimal navigation list that just points to the root of the API
set-content -path ./toc.yml @"
- name: API Documentation
  href: build/api/
"@

# Create the docfx.json config file
$docfxConfig = @{
  metadata = ,
    @{
      src = ,
        @{
          src = "$projectDir/src"
          files = "**/*.csproj"
        }
      dest = "build/api"
      disableGitFeatures = $false
      disableDefaultFilter = $false
    }
  build = @{
    content = @{
        src = "build/api"
        files = , "**.yml"
        dest = "api"
      },
      @{
        src = "."
        files = "toc.yml", "*.md"
      }
    overwrite = ,
      @{
        files = , "apidoc/**.md"
        exclude = "obj/**", "html/**"
      }
    dest = "build/html"
    globalMetadata = @{
      _disableContribution = $true
    }
    template = , "default"
    disableGitFeatures = $true
  }
}
convertTo-json -inputObject $docfxConfig -depth 10 | set-content -path docfx.json 

# Run the documentation generator
docfx docfx.json

# Make an archive of the output and store it as a single artifact
# (currently that's what Releaser expects)
set-location "$tempDocsDir/build/html"
tar -cvzf "$projectDir/artifacts/docs.zip" *

set-location $projectDir
