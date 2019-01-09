.NET EventSource implementation
===============================

Project Information
-------------------

This libary allows .NET developers to consume Server Sent Events from a remote API. The server sent events spec is defined here: [https://html.spec.whatwg.org/multipage/server-sent-events.html](https://html.spec.whatwg.org/multipage/server-sent-events.html#server-sent-events)

This library supports .NET 4.5+ and .NET Standard 1.4+.

Quick setup
-----------

**Nuget Restore**
1. If you have nuget package restore automatically enabled, Visual Studio will attempt to restore the nuget packages upon opening the LaunchDarkly.EventSource.sln
2. Otherwise, after you open LaunchDarkly.EventSource.sln, Right+Click on the solution and select "Restore NuGet Packages"

**Build Solution**
1. In Visual Studio, Build the LaunchDarkly.EventSource.sln solution

**Running the example Console App**

Included in the solution is a Console App that shows example usage for using the LaunchDarkly EventSource library against LaunchDarkly's streaming API.

NOTE: You'll need your LaunchDarkly SDK Key before running the Console app.

1. Compile and Run the EventSource-ConsoleApp project.
   1. Select the EventSource-ConsoleApp project in the Solution Explorer.
   2. Open the Program.cs file.
   3. Replace "Insert Auth Key" with your LaunchDarkly API key.
   4. Go to the Debug menu and select Start Debugging (or hit the F5 key).

Signing
-------

The published version of this assembly is digitally signed by LaunchDarkly and strong-named. Building the code locally in the default Debug configuration does not sign the assembly and does not require a key file. The public key file is in this repo at `LaunchDarkly.EventSource.pk` as well as here:

```
Public key (hash algorithm: sha1):
002400000480000094000000060200000024000052534131000400000100010015ba095c5a95ac
efa557867cec3f488906ec0ef6fe6728a7cfdeef861fcce49ea79357ba825d95d56d67597bc9cc
9a473438f5607908186fc477fdeafc68f387552061ebf57d6e585317d5047a57bd496034ff854a
417236776003bcba328fa8bf4a024c4d212ba4fb4033ebfb14116c12cde63d16551b9f48c20ee5
4a417deb

Public key token is 18e8c36453e3060f
```

Development notes
-----------------

This project imports the `dotnet-base` repository as a subtree. See the `README.md` file in that directory for more information.

Releases are done using the release script in `dotnet-base`. Since the published package includes a .NET Framework 4.5 build, the release must be done from Windows.

About LaunchDarkly
------------------

* LaunchDarkly is a continuous delivery platform that provides feature flags as a service and allows developers to iterate quickly and safely. We allow you to easily flag your features and manage them from the LaunchDarkly dashboard.  With LaunchDarkly, you can:
    * Roll out a new feature to a subset of your users (like a group of users who opt-in to a beta tester group), gathering feedback and bug reports from real-world use cases.
    * Gradually roll out a feature to an increasing percentage of users, and track the effect that the feature has on key metrics (for instance, how likely is a user to complete a purchase if they have feature A versus feature B?).
    * Turn off a feature that you realize is causing performance problems in production, without needing to re-deploy, or even restart the application with a changed configuration file.
    * Grant access to certain features based on user attributes, like payment plan (eg: users on the ‘gold’ plan get access to more features than users in the ‘silver’ plan). Disable parts of your application to facilitate maintenance, without taking everything offline.
* LaunchDarkly provides feature flag SDKs for
    * [Java](http://docs.launchdarkly.com/docs/java-sdk-reference "Java SDK")
    * [JavaScript](http://docs.launchdarkly.com/docs/js-sdk-reference "LaunchDarkly JavaScript SDK")
    * [PHP](http://docs.launchdarkly.com/docs/php-sdk-reference "LaunchDarkly PHP SDK")
    * [Python](http://docs.launchdarkly.com/docs/python-sdk-reference "LaunchDarkly Python SDK")
    * [Go](http://docs.launchdarkly.com/docs/go-sdk-reference "LaunchDarkly Go SDK")
    * [Node.JS](http://docs.launchdarkly.com/docs/node-sdk-reference "LaunchDarkly Node SDK")
    * [Electron](http://docs.launchdarkly.com/docs/electron-sdk-reference "LaunchDarkly Electron SDK")
    * [.NET](http://docs.launchdarkly.com/docs/dotnet-sdk-reference "LaunchDarkly .NET SDK")
    * [Ruby](http://docs.launchdarkly.com/docs/ruby-sdk-reference "LaunchDarkly Ruby SDK")
    * [iOS](http://docs.launchdarkly.com/docs/ios-sdk-reference "LaunchDarkly iOS SDK")
    * [Android](http://docs.launchdarkly.com/docs/android-sdk-reference "LaunchDarkly Android SDK")
* Explore LaunchDarkly
    * [launchdarkly.com](http://www.launchdarkly.com/ "LaunchDarkly Main Website") for more information
    * [docs.launchdarkly.com](http://docs.launchdarkly.com/  "LaunchDarkly Documentation") for our documentation and SDKs
    * [apidocs.launchdarkly.com](http://apidocs.launchdarkly.com/  "LaunchDarkly API Documentation") for our API documentation
    * [blog.launchdarkly.com](http://blog.launchdarkly.com/  "LaunchDarkly Blog Documentation") for the latest product updates
    * [Feature Flagging Guide](https://github.com/launchdarkly/featureflags/  "Feature Flagging Guide") for best practices and strategies
