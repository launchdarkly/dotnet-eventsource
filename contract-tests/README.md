# SSE contract-tests service

This is a small HTTP service that wraps `LaunchDarkly.EventSource` and exposes it to
the [`sse-contract-tests`](https://github.com/launchdarkly/sse-contract-tests) harness.

The service is not shipped as part of any published NuGet package. It exists purely
as a test target for the `sse-contract-tests` harness.

## Running

```
dotnet run --project TestService.csproj
```

The service listens on port 8000 by default.

## Running the harness against it

Either use the released harness binary via `sse-contract-tests`'s `downloader/run.sh`
script, or build the harness locally:

```
cd path/to/sse-contract-tests
go build -o sse-test-harness .
./sse-test-harness --url http://localhost:8000
```

To exercise only a subset of tests, pass `--run <pattern>` or `--skip <pattern>`.
