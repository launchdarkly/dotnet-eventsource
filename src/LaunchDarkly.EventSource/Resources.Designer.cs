﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:4.0.30319.42000
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace LaunchDarkly.EventSource {
    using System;
    using System.Reflection;
    
    
    [System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "4.0.0.0")]
    [System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class Resources {
        
        private static System.Resources.ResourceManager resourceMan;
        
        private static System.Globalization.CultureInfo resourceCulture;
        
        [System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal Resources() {
        }
        
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static System.Resources.ResourceManager ResourceManager {
            get {
                if (object.Equals(null, resourceMan)) {
                    System.Resources.ResourceManager temp = new System.Resources.ResourceManager("LaunchDarkly.EventSource.Resources", typeof(Resources).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        [System.ComponentModel.EditorBrowsableAttribute(System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        internal static string EventSourceService_Read_Timeout {
            get {
                return ResourceManager.GetString("EventSourceService_Read_Timeout", resourceCulture);
            }
        }
        
        internal static string EventSource_204_Response {
            get {
                return ResourceManager.GetString("EventSource_204_Response", resourceCulture);
            }
        }
        
        internal static string EventSource_Already_Started {
            get {
                return ResourceManager.GetString("EventSource_Already_Started", resourceCulture);
            }
        }
        
        internal static string EventSource_HttpResponse_Not_Successful {
            get {
                return ResourceManager.GetString("EventSource_HttpResponse_Not_Successful", resourceCulture);
            }
        }
        
        internal static string EventSource_Invalid_MediaType {
            get {
                return ResourceManager.GetString("EventSource_Invalid_MediaType", resourceCulture);
            }
        }
        
        internal static string EventSource_Logger_Closed {
            get {
                return ResourceManager.GetString("EventSource_Logger_Closed", resourceCulture);
            }
        }
        
        internal static string EventSource_Logger_Connection_Error {
            get {
                return ResourceManager.GetString("EventSource_Logger_Connection_Error", resourceCulture);
            }
        }
        
        internal static string EventSource_Logger_Disconnected {
            get {
                return ResourceManager.GetString("EventSource_Logger_Disconnected", resourceCulture);
            }
        }
        
        internal static string EventSource_Response_Content_Empty {
            get {
                return ResourceManager.GetString("EventSource_Response_Content_Empty", resourceCulture);
            }
        }
    }
}
