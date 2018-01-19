# Change log

All notable changes to the LaunchDarkly's EventSource implementation for C# will be documented in this file. This project adheres to [Semantic Versioning](http://semver.org).

## [2.2.0] - 2018-01-19
### Added
- Exposed `EventSourceServiceCancelledException` as a public class.

### Changed
- Removed unused and transitive dependencies.
- Added a reference to the Apache 2.0 license in `LaunchDarkly.EventSource.csproj`
- Improved logging. Thanks @JeffAshton!

## [2.1.1] - 2017-11-29
### Changed
- Move from .NET Standard 1.6 to 1.4.

## [2.1.0] - 2017-11-16
### Added
- Exposed the `ExponentialBackoffWithDecorrelation` as a public class. This class may be used to calculate exponential backoff with jitter.

### Changed
- Reconnects to EventSource are now handled inline, rather than using [Polly](https://github.com/App-vNext/Polly) for managing retry policies.

## [2.0.0] - 2017-10-11
### Changed
- Removed the `closeOnEndOfStream` property.

## [1.1.0] 2017-10-02
### Added
- `ConnectToEventSourceAsync` now takes in a new boolean parameter, `closeOnEndOfStream`, which, if true, will close the EventSource connection when the end of the stream is reached.

### Changed
- Fixed a bug causing causing read timeouts to never propogate.

## [1.0.1] 2017-09-27
### Added
- Signed EventSource Assembly.

### Changed
- Change dependency on Polly to Polly-Signed.

## [1.0.0] 2017-09-22
Hello world!
