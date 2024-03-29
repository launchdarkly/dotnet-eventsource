//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace LaunchDarkly.EventSource {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    ///   This class was generated by MSBuild using the GenerateResource task.
    ///   To add or remove a member, edit your .resx file then rerun MSBuild.
    /// </summary>
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("Microsoft.Build.Tasks.StronglyTypedResourceBuilder", "15.1.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Resources {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Resources() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("LaunchDarkly.EventSource.Resources", typeof(Resources).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Invalid attempt to call Start() while the connection state is {0}.
        /// </summary>
        internal static string ErrorAlreadyStarted {
            get {
                return ResourceManager.GetString("ErrorAlreadyStarted", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to HTTP response had no body.
        /// </summary>
        internal static string ErrorEmptyResponse {
            get {
                return ResourceManager.GetString("ErrorEmptyResponse", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Unexpected HTTP status code {0} from server.
        /// </summary>
        internal static string ErrorHttpStatus {
            get {
                return ResourceManager.GetString("ErrorHttpStatus", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Read timeout elapsed with no new data from server; connection may have been silently dropped.
        /// </summary>
        internal static string ErrorReadTimeout {
            get {
                return ResourceManager.GetString("ErrorReadTimeout", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Unexpected HTTP content type &quot;{0}&quot;; should be &quot;text/event-stream&quot;.
        /// </summary>
        internal static string ErrorWrongContentType {
            get {
                return ResourceManager.GetString("ErrorWrongContentType", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Unexpected character encoding {0}; should be UTF-8.
        /// </summary>
        internal static string ErrorWrongEncoding {
            get {
                return ResourceManager.GetString("ErrorWrongEncoding", resourceCulture);
            }
        }
    }
}
