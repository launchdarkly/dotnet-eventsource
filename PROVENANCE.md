## Verifying SDK build provenance with GitHub artifact attestations

LaunchDarkly uses [GitHub artifact attestations](https://docs.github.com/en/actions/security-for-github-actions/using-artifact-attestations/using-artifact-attestations-to-establish-provenance-for-builds) to help developers make their supply chain more secure by ensuring the authenticity and build integrity of our published SDK packages.

LaunchDarkly publishes provenance about our SDK package builds using [GitHub's `actions/attest` action](https://github.com/actions/attest). These attestations are stored in GitHub's attestation API and can be verified using the [GitHub CLI](https://cli.github.com/).

To verify build provenance attestations, we recommend using the [GitHub CLI `attestation verify` command](https://cli.github.com/manual/gh_attestation_verify). Example usage for verifying SDK packages is included below:

<!-- x-release-please-start-version -->
```
# Set the version of the SDK to verify
SDK_VERSION=5.3.0
```
<!-- x-release-please-end -->

```
# Download the nupkg from NuGet
$ nuget install LaunchDarkly.EventSource -Version $SDK_VERSION -OutputDirectory ./packages

# Verify provenance using the GitHub CLI
$ gh attestation verify ./packages/LaunchDarkly.EventSource.${SDK_VERSION}/LaunchDarkly.EventSource.${SDK_VERSION}.nupkg --owner launchdarkly
```

Below is a sample of expected output.

```
Loaded digest sha256:... for file://LaunchDarkly.EventSource.5.3.0.nupkg
Loaded 1 attestation from GitHub API

The following policy criteria will be enforced:
- Predicate type must match:................ https://slsa.dev/provenance/v1
- Source Repository Owner URI must match:... https://github.com/launchdarkly
- Subject Alternative Name must match regex: (?i)^https://github.com/launchdarkly/
- OIDC Issuer must match:................... https://token.actions.githubusercontent.com

✓ Verification succeeded!

The following 1 attestation matched the policy criteria

- Attestation #1
  - Build repo:..... launchdarkly/dotnet-eventsource
  - Build workflow:. .github/workflows/release-please.yml
  - Signer repo:.... launchdarkly/dotnet-eventsource
  - Signer workflow: .github/workflows/release-please.yml
```

For more information, see [GitHub's documentation on verifying artifact attestations](https://docs.github.com/en/actions/security-for-github-actions/using-artifact-attestations/using-artifact-attestations-to-establish-provenance-for-builds#verifying-artifact-attestations-with-the-github-cli).

**Note:** These instructions do not apply when building our SDKs from source.
