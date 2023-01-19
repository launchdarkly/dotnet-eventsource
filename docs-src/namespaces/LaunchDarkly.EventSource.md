The basic API for SSE client functionality.

Normal usage is to construct a <xref:LaunchDarkly.EventSource.Configuration>, pass it to the <xref:LaunchDarkly.EventSource.EventSource> constructor, and then read from the EventSource with <xref:LaunchDarkly.EventSource.EventSource.ReadMessageAsync> or <xref:LaunchDarkly.EventSource.EventSource.ReadAnyEventAsync>.

If you prefer to use a push model where events are delivered to event handlers that you specify, see <xref:LaunchDarkly.EventSource.Background>.
