# Change log

All notable changes to the LaunchDarkly's EventSource implementation for C# will be documented in this file. This project adheres to [Semantic Versioning](http://semver.org).

## [4.1.1] - 2021-05-04
(The 4.1.0 release was an accidental duplicate of 4.0.0 and did not include these changes.)

### Added:
- `ConfigurationBuilder.ResponseStartTimeout`-- added as a more accurately named replacement for `ConnectionTimeout`.

### Deprecated:
- `ConfigurationBuilder.ConnectionTimeout`


## [4.1.0] - 2021-05-04
### Added:
- `ConfigurationBuilder.ResponseStartTimeout`-- added as a more accurately named replacement for `ConnectionTimeout`.

### Deprecated:
- `ConfigurationBuilder.ConnectionTimeout`

## [4.0.0] - 2021-02-22
Starting with this version, the API documentation for the latest release is viewable at https://launchdarkly.github.io/dotnet-eventsource.

The 4.x major version of `LaunchDarkly.EventSource` will be used in the upcoming major version releases of LaunchDarkly&#39;s .NET-based SDKs. This release streamlines the API, updates platform compatibility, and improves performance.

### Added:
- `EventSource.Restart`: this method allows the caller to trigger a connection restart even if there was not an error on the stream.
- `MessageEvent.Name`: previously, the event name was only available as a property of `MessageReceivedEventArgs`.
- `ConfigurationBuilder.DefaultEncoding`: sets the character encoding to use for parsing the stream data if the server did not specify an encoding in its `Content-Type`.
- `ConfigurationBuilder.PreferDataAsUtf8Bytes`: an optional mode in which `EventSource` will read the `data:` property of events as a raw UTF-8 byte array (assuming the stream encoding is UTF-8) to avoid unnecessarily allocating a UTF-16 `string`. This is meant as an optimization for applications that will be processing the data with a UTF-8-based API such as `System.Text.Json`.
- Simple `EventSource(Uri)` constructor for cases where you do not need any other configuration options.
- Standard .NET XML documentation of all public types and methods is now produced in the build.
- The release now publishes a [Source Link](https://github.com/dotnet/sourcelink/blob/master/README.md) package.

### Changed:
- The minimum .NET Standard compatibility version is now 2.0.
- The minimum .NET Framework compatibility version is now 4.5.2.
- Logging now works through the new [`LaunchDarkly.Logging`](https://github.com/launchdarkly/dotnet-logging) facade which is used by LaunchDarkly .NET-based SDKs, instead of `Common.Logging`.
- `ConfigurationBuilder` methods no longer throw any exceptions, except for one case: passing `null` for the stream URI. In all cases where a property has a maximum and/or minimum value, if you try to set a value outside that range it will simply be changed to the closest valid value. If two properties do not make sense to use together (such as a non-null `HttpClient` and a non-null `HttpMessageHandler`), one will simply override the other as described in the API documentation.
- `ConfigurationBuilder.DelayRetryDuration` is named to `InitialRetryDelay`, which more accurately describes its meaning.
- `ConfigurationBuilder.MessageHandler` is renamed to `HttpMessageHandler`, since that&#39;s the name of its type (also, &#34;message&#34; has other meanings in this API).
- The type of `Configuration.HttpContentFactory` is now simply `Func&lt;HttpContent&gt;` rather than using a custom delegate type.
- If you pass a `Dictionary` of headers to `ConfigurationBuilder.RequestHeaders`, it will now be copied. Previously the configuration retained a reference to the original `Dictionary`, which could cause problems if it was later modified.
- `EventSource` now implements the stream read timeout in a way that uses the task scheduler more efficiently. Previously, it executed an additional task for each read with `Task.Delay`; now, it uses a timed cancellation token.
- Exception messages have been rewritten for clarity and style.

### Fixed:
- `ConfigurationBuilder.BackoffResetThreshold` had no effect; the default value was being used regardless of what you set it to.
- When reading the SSE stream, `EventSource` was using a case-insensitive comparison to recognize field names like `event` and `data`. The [SSE specification](https://html.spec.whatwg.org/multipage/server-sent-events.html#event-stream-interpretation) states that field name &#34;must be compared literally, with no case folding performed,&#34; so a field name like `EVENT` is invalid.

### Removed:
- Removed all types and methods that were marked as deprecated/obsolete in the last 3.x version.
- `Configuration` no longer has a public constructor. Instances must now be created with `Configuration.Builder`.
- The helper class `ExponentialBackoffWithDecorrelation` is no longer public.

## [3.4.0] - 2020-12-03
### Added:
- You can now tell EventSource to use an `HttpClient` instance that you provide by calling `ConfigurationBuilder.HttpClient`. (Thanks, [thorstenfleischmann](https://github.com/launchdarkly/dotnet-eventsource/pull/61)!)

## [3.3.3] - 2020-11-05
### Added:
- The package now has a specific target for .NET Standard 2.0, in addition to the previous targets of .NET Standard 1.4 and .NET Framework 4.5. There is no functional difference between the 2.0 and 1.4 builds, but it may be desirable for .NET Core/.NET Standard code to be able to avoid linking to any .NET Standard 1.x assemblies.

## [3.3.2] - 2019-10-23
### Fixed:
- If a stream connection is restarted, `EventSource` should send a `Last-Event-Id` header if it previously received an event with an `id` field. (Thanks, [fracek](https://github.com/launchdarkly/dotnet-eventsource/pull/53)!)

## [3.3.1] - 2019-08-08
### Fixed:
- If you don't explicitly provide an `HttpMessageHandler` in the configuration, EventSource will now use the default `HttpClient` constructor; previously, it would create an instance of the standard .NET `HttpMessageHandler` implementation and tell the `HttpClient` to use that. In .NET Framework and .NET Standard, this makes no difference, but in Xamarin, calling the default `HttpClient` constructor may allow it to use a better HTTP implementation based on native APIs.
- Expanded and improved documentation comments.

## [3.3.0] - 2019-03-26
### Added:
- The `EventSource` now implements `IDisposable`. Calling `Dispose()` has the same effect as calling `Close()`.

### Fixed:
- Under some circumstances, a `CancellationTokenSource` might not be disposed of after making an HTTP request, which could cause a timer object to be leaked.

## [3.2.3] - 2019-01-14
### Fixed:
- The assemblies in this package now have Authenticode signatures. The release note for 3.2.1 was an error; that release did not include signatures.

## [3.2.2] - 2019-01-09
### Removed
- The console applications for manual testing have been removed (because the unit tests now include full end-to-end tests against a stub HTTP server).

## [3.2.1] - 2019-01-09
### Changed
- The published assemblies are now digitally signed as well as strong-named. Also, they are now built in Release mode and do not contain debug information.

## [3.2.0] - 2018-10-24
### Added
- Previously, the delay before reconnect attempts would increase exponentially only if the previous connection could not be made at all or returned an HTTP error; if it received an HTTP 200 status, the delay would be reset to the minimum even if the connection then immediately failed. Now, the new configuration property `BackoffResetThreshold` (default: 1 minute) specifies the length of time that a connection must stay active in order for the reconnect delay to be reset. ([#37](https://github.com/launchdarkly/dotnet-eventsource/issues/37))

### Fixed
- Fixed an [unobserved exception](https://blogs.msdn.microsoft.com/pfxteam/2011/09/28/task-exception-handling-in-net-4-5/) that could occur following a stream timeout, which could cause a crash in .NET 4.0.

- A `NullReferenceException` could sometimes be logged if a stream connection failed. ([#24](https://github.com/launchdarkly/dotnet-eventsource/issues/24))

## [3.1.5] - 2018-08-29
Duplicate of 3.1.4, created due to a problem in the release process.

## [3.1.4] - 2018-08-29
### Fixed
- Fixed a bug that prevented the event source from reconnecting to the stream if it received an HTTP error status from the server (as opposed to simply losing the connection).

## [3.1.3] - 2018-08-13
### Fixed
- The reconnection attempt counter is no longer shared among all EventSource instances. Previously, if you connected to more than one stream, all but the first would behave as if they were reconnecting and would have a backoff delay.

## [3.1.2] - 2018-08-02
### Changed
- The SDK was referencing some system assemblies via `<PackageReference>`, which could cause dependency conflicts. These have been changed to framework `<Reference>`s. A redundant reference to `System.Runtime` was removed.

### Fixed
- If the stream connection fails, there should be an increasing backoff interval before each reconnect attempt. Previously, it would log a message about waiting some number of milliseconds, but then not actually wait.

## [3.1.1] - 2018-06-28
### Removed
- Removed an unused dependency on Newtonsoft.Json.

## [3.1.0] - 2018-06-01
### Added
- The new class `ConfigurationBuilder` provides a validated fluent builder pattern for `Configuration` instances.
- The HTTP method and request body can now be specified in `ConfigurationBuilder` or in the `Configuration` constructor. The default is still to use `GET` and not send a request body.

## [3.0.0] - 2018-02-23
### Changed
- Logging is now done via `Common.Logging`.

### Added
- `EventSource` now uses the interface `IEventSource`.

## [2.2.1] - 2018-02-05
- Downgrade Microsoft.Extensions.Logging to 1.0.2 to reduce dependencies brought in when building against .NET Framework.

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
