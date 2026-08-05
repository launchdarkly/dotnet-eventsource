using System.Collections.Generic;

// Note, in order for System.Text.Json serialization/deserialization to work correctly, the members of
// these classes must be properties with get/set, rather than fields. The property names are automatically
// camelCased by System.Text.Json.

namespace TestService
{
    public class Status
    {
        public string[] Capabilities { get; set; }
    }

    public class StreamOptions
    {
        public string StreamUrl { get; set; }
        public string CallbackUrl { get; set; }
        public string Tag { get; set; }
        public Dictionary<string, string> Headers { get; set; }
        public int? InitialDelayMs { get; set; }
        public int? ReadTimeoutMs { get; set; }
        public string LastEventId { get; set; }
        public string Method { get; set; }
        public string Body { get; set; }
    }

    public class Message
    {
        public string Kind { get; set; }
        public EventMessage Event { get; set; }
        public string Comment { get; set; }
        public string Error { get; set; }
    }

    public class EventMessage
    {
        public string Type { get; set; }
        public string Data { get; set; }
        public string Id { get; set; }
    }

    public class CommandParams
    {
        public string Command { get; set; }
    }
}
