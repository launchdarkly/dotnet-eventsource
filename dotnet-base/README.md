# dotnet-base

This repository contains tools that are often used in LaunchDarkly projects for .NET. It is meant to be incorporated in other repositories as a subtree:

    git remote add dotnet-base git@github.com:launchdarkly/dotnet-base.git
    git subtree add --squash --prefix=dotnet-base/ dotnet-base master

To update the copy of `dotnet-base` in your repository to reflect changes in this one:

    git subtree pull --squash --prefix=dotnet-base/ dotnet-base master

## Scripts

Scripts with a `.ps1` extension are for Windows PowerShell. When running a PowerShell script, it's a good idea to start the Developer Command Prompt first and then run PowerShell from within it, so that your path includes the developer tools.

### `scripts/release.ps1`

This script builds, tests, (optionally) signs, and releases the project(s) in the current solution. It assumes that you are using a standardized directory layout where releasable projects are under `.\src\ProjectName` and test projects are under `.\test\ProjectName`.

It makes the following assumptions:

- `.\src` contains one subdirectory for each project to be released
- `.\test` contains one subdirectory for each unit test project
- The correct release version has already been set in all projects
- The Visual Studio tools are in your path (easiest way to ensure this is to run Developer Command Prompt and then run PowerShell from within that)
- If projects use strong-naming, you have downloaded the appropriate `.snk` key file in the current directory
- If you're not skipping signing, you have downloaded the certificate file in the current directory, and you have the password for the certificate

The script will do the following:

1. Perform a clean build of all projects in the Debug configuration and run unit tests (unless you specify otherwise)
2. Perform a clean build of all projects in the Release configuration
3. Sign the resulting assemblies (unless you specify otherwise)
4. Run `dotnet pack` on each project
5. Upload the package(s) to NuGet (unless you specify otherwise)

Parameters:

- `-skipTests`: if specified, the Debug build and unit tests will not be run
- `-skipSign`: if specified, code signing will not be done
- `-skipPublish`: if specified, the project(s) will only be built, not published
- `-password PASSWORD`: password for the code signing certificate; required unless `-skipSign` is set
- `-certFile FILE_PATH`: the code signing certificate; defaults to `catamorphic_code_signing_certificate.p12`
