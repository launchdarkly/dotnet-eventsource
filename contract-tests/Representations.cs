using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TestService
{
    public class Status
    {
        [JsonPropertyName("capabilities")] public string[] Capabilities { get; set; }
    }

    public class StreamOptions
    {
        [JsonPropertyName("streamUrl")] public string StreamUrl { get; set; }
        [JsonPropertyName("callbackUrl")] public string CallbackUrl { get; set; }
        [JsonPropertyName("tag")] public string Tag { get; set; }
        [JsonPropertyName("headers")] public Dictionary<string, string> Headers { get; set; }
        [JsonPropertyName("initialDelayMs")] public int? InitialDelayMs { get; set; }
        [JsonPropertyName("readTimeoutMs")] public int? ReadTimeoutMs { get; set; }
        [JsonPropertyName("lastEventId")] public string LastEventId { get; set; }
        [JsonPropertyName("method")] public string Method { get; set; }
        [JsonPropertyName("body")] public string Body { get; set; }
    }

    public class Message
    {
        [JsonPropertyName("kind")] public string Kind { get; set; }
        [JsonPropertyName("event")] public EventMessage Event { get; set; }
        [JsonPropertyName("comment")] public string Comment { get; set; }
        [JsonPropertyName("error")] public string Error { get; set; }
    }

    public class EventMessage
    {
        [JsonPropertyName("type")] public string Type { get; set; }
        [JsonPropertyName("data")] public string Data { get; set; }
        [JsonPropertyName("id")] public string Id { get; set; }
    }
  
    public class CommandParams
    {
        [JsonPropertyName("command")] public string Command { get; set; }
    }
}
