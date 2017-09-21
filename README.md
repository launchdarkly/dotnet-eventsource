LaunchDarkly Event Source SDK for .NET
===========================

Project Information
-----------

The LaunchDarkly EventSource SDK is a .NET library that allows developers to consume Server Sent Events from a remote API. The server sent events spec is defined here: [https://html.spec.whatwg.org/multipage/server-sent-events.html](http://example.com)

This library supports .NET 4.5+ and .NET Standard 1.6+.

Quick setup
-----------

**Nuget Restore**
1. If you have nuget package restore automatically enabled, Visual Studio will attempt to restore the nuget packages upon opening the LaunchDarkly.EventSource.sln
2. Otherwise, after you open LaunchDarkly.EventSource.sln, Right+Click on the solution and select "Restore NuGet Packages"

**Build Solution**
1. In Visual Studio, Build the LaunchDarkly.EventSource.sln solution

**Running the example Console App**

Included in the solution is a Console App that shows example usage for using the LaunchDarkly EventSource library.

NOTE: You'll need your LaunchDarkly API Key before running the Console app.

1. Compile and Run the EventSource-ConsoleApp project. 
   1. Select the EventSource-ConsoleApp project in the Solution Explorer.
   2. Open the Program.cs file.
   3. Replace "Insert Auth Key" with your LaunchDarkly API key.
   4. Go to the Debug menu and select Start Debugging (or hit the F5 key). 


About LaunchDarkly
-----------

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
    * [Python Twisted](http://docs.launchdarkly.com/docs/python-twisted-sdk-reference "LaunchDarkly Python Twisted SDK")
    * [Go](http://docs.launchdarkly.com/docs/go-sdk-reference "LaunchDarkly Go SDK")
    * [Node.JS](http://docs.launchdarkly.com/docs/node-sdk-reference "LaunchDarkly Node SDK")
    * [.NET](http://docs.launchdarkly.com/docs/dotnet-sdk-reference "LaunchDarkly .Net SDK")
    * [Ruby](http://docs.launchdarkly.com/docs/ruby-sdk-reference "LaunchDarkly Ruby SDK")
    * [iOS](http://docs.launchdarkly.com/docs/ios-sdk-reference "LaunchDarkly iOS SDK")
    * [Android](http://docs.launchdarkly.com/docs/android-sdk-reference "LaunchDarkly Android SDK")
* Explore LaunchDarkly
    * [launchdarkly.com](http://www.launchdarkly.com/ "LaunchDarkly Main Website") for more information
    * [docs.launchdarkly.com](http://docs.launchdarkly.com/  "LaunchDarkly Documentation") for our documentation and SDKs
    * [apidocs.launchdarkly.com](http://apidocs.launchdarkly.com/  "LaunchDarkly API Documentation") for our API documentation
    * [blog.launchdarkly.com](http://blog.launchdarkly.com/  "LaunchDarkly Blog Documentation") for the latest product updates
    * [Feature Flagging Guide](https://github.com/launchdarkly/featureflags/  "Feature Flagging Guide") for best practices and strategies
