# LaunchDarkly EventSource SSE Client for .NET

[![NuGet](https://img.shields.io/nuget/v/LaunchDarkly.EventSource.svg?style=flat-square)](https://www.nuget.org/packages/LaunchDarkly.EventSource/)
[![CircleCI](https://circleci.com/gh/launchdarkly/dotnet-eventsource.svg?style=shield)](https://circleci.com/gh/launchdarkly/dotnet-eventsource)
[![Documentation](https://img.shields.io/static/v1?label=GitHub+Pages&message=API+reference&color=00add8)](https://launchdarkly.github.io/dotnet-eventsource)

## Overview

The `LaunchDarkly.EventSource` package allows .NET developers to consume Server-Sent-Events (SSE) from a remote API. The SSE specification is defined here: [https://html.spec.whatwg.org/multipage/server-sent-events.html](https://html.spec.whatwg.org/multipage/server-sent-events.html#server-sent-events)

## Supported .NET versions

This version of the library is built for the following targets:

* .NET Framework 4.5.2: runs on .NET Framework 4.5.x and above.
* .NET Core 2.1: runs on .NET Core 2.x and 3.x, or .NET 5. This target provides an adapter to the standard .NET Core logging framework, `Logs.CoreLogging`, which is not available in .NET Framework.
* .NET Standard 2.0: runs on .NET Core 2.x and 3.x or .NET 5, or within a library that is targeted to .NET Standard 2.x.

The .NET build tools should automatically load the most appropriate build of the library for whatever platform your application or library is targeted to.

## Signing

The published version of this assembly is digitally signed with Authenticode and [strong-named](https://docs.microsoft.com/en-us/dotnet/framework/app-domains/strong-named-assemblies). Building the code locally in the default Debug configuration does not use strong-naming and does not require a key file. The public key file is in this repository at `LaunchDarkly.EventSource.pk` as well as here:

```
Public key (hash algorithm: sha1):
002400000480000094000000060200000024000052534131000400000100010015ba095c5a95ac
efa557867cec3f488906ec0ef6fe6728a7cfdeef861fcce49ea79357ba825d95d56d67597bc9cc
9a473438f5607908186fc477fdeafc68f387552061ebf57d6e585317d5047a57bd496034ff854a
417236776003bcba328fa8bf4a024c4d212ba4fb4033ebfb14116c12cde63d16551b9f48c20ee5
4a417deb

Public key token is 18e8c36453e3060f
```

## Contributing

We encourage pull requests and other contributions from the community. Check out our [contributing guidelines](CONTRIBUTING.md) for instructions on how to contribute to this project.

## About LaunchDarkly
 
* LaunchDarkly is a continuous delivery platform that provides feature flags as a service and allows developers to iterate quickly and safely. We allow you to easily flag your features and manage them from the LaunchDarkly dashboard.  With LaunchDarkly, you can:
    * Roll out a new feature to a subset of your users (like a group of users who opt-in to a beta tester group), gathering feedback and bug reports from real-world use cases.
    * Gradually roll out a feature to an increasing percentage of users, and track the effect that the feature has on key metrics (for instance, how likely is a user to complete a purchase if they have feature A versus feature B?).
    * Turn off a feature that you realize is causing performance problems in production, without needing to re-deploy, or even restart the application with a changed configuration file.
    * Grant access to certain features based on user attributes, like payment plan (eg: users on the ‘gold’ plan get access to more features than users in the ‘silver’ plan). Disable parts of your application to facilitate maintenance, without taking everything offline.
* LaunchDarkly provides feature flag SDKs for a wide variety of languages and technologies. Check out [our documentation](https://docs.launchdarkly.com/docs) for a complete list.
* Explore LaunchDarkly
    * [launchdarkly.com](https://www.launchdarkly.com/ "LaunchDarkly Main Website") for more information
    * [docs.launchdarkly.com](https://docs.launchdarkly.com/  "LaunchDarkly Documentation") for our documentation and SDK reference guides
    * [apidocs.launchdarkly.com](https://apidocs.launchdarkly.com/  "LaunchDarkly API Documentation") for our API documentation
    * [blog.launchdarkly.com](https://blog.launchdarkly.com/  "LaunchDarkly Blog Documentation") for the latest product updates
