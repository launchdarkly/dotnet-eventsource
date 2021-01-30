using System;

namespace LaunchDarkly.EventSource
{
    /// <summary>
    /// Represents the Server-Sent Event message received from a stream.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An SSE event consists of an event name (defaulting to "message" if not specified),
    /// a data string, and an optional "ID" string that the server may provide.
    /// </para>
    /// <para>
    /// The event name and ID properties are always stored as strings. By default, the
    /// data property is also stored as a string. However, in some applications, it may
    /// be desirable to represent the data as a UTF-8 byte array (for instance, if you are
    /// using the <c>System.Text.Json</c> API to parse JSON data).
    /// </para>
    /// <para>
    /// Since strings in .NET use two-byte UTF-16 characters, if you have a large block of
    /// UTF-8 data it is considerably more efficient to process it in its original form
    /// rather than converting it to or from a string. <c>MessageEvent</c> converts
    /// transparently between these types depending on the original character encoding of
    /// the stream; the <see cref="Configuration"/> properties <see cref="Configuration.DefaultEncoding"/>
    /// and <see cref="Configuration.PreferDataAsUtf8Bytes"/>; and whether the caller reads
    /// from the property <see cref="MessageEvent.Data"/> or <see cref="MessageEvent.DataUtf8Bytes"/>.
    /// If you intend to process the data as UTF-8 bytes, and if you expect that the server
    /// will provide UTF-8, you should set 
    /// </para>
    /// <list type="bullet">
    /// <item> If the stream encoding is UTF-8, and you read the event data with the
    /// <see cref="MessageEvent.DataUtf8Bytes"/> property, the event data is stored as a
    /// UTF-8 byte array when it is first read from the stream and it returns the same
    /// array, without any further copying and without creating a <c>string</c>. </item>
    /// <item> If the stream encoding is UTF-8, but you read the event data with the
    /// <see cref="MessageEvent.Data"/> property, the event data is originally read from
    /// the stream as a UTF-8 byte array but is then converted to a <c>string</c>. </item>
    /// <item> If the stream encoding is not UTF-8, the event data is originally read from
    /// the stream as a <c>string</c>. <see cref="MessageEvent.Data"/> will return the
    /// same <c>string</c>; <see cref="MessageEvent.DataUtf8Bytes"/> will create a new
    /// UTF-8 byte array from it.</item>
    /// </list>
    /// </remarks>
    public struct MessageEvent
    {
        #region Private Fields

        private readonly string _name;
        private readonly string _dataString;
        private readonly Utf8ByteSpan _dataUtf8Bytes;
        private readonly string _lastEventId;
        private readonly Uri _origin;

        #endregion

        #region Public Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageEvent"/> class.
        /// </summary>
        /// <param name="name">the event name</param>
        /// <param name="data">the data received in the server-sent event</param>
        /// <param name="lastEventId">the last event identifier, or null</param>
        /// <param name="origin">the origin URI of the stream</param>
        public MessageEvent(string name, string data, string lastEventId, Uri origin)
        {
            _name = name;
            _dataString = data;
            _dataUtf8Bytes = new Utf8ByteSpan();
            _lastEventId = lastEventId;
            _origin = origin;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageEvent"/> class,
        /// providing the data as a UTF-8 byte span.
        /// </summary>
        /// <param name="name">the event name</param>
        /// <param name="dataUtf8Bytes">the data received in the server-sent event;
        ///   the <c>MessageEvent</c> will store a reference to the byte array, rather than
        ///   copying it, so it should not be modified afterward by the caller
        /// </param>
        /// <param name="lastEventId">the last event identifier, or null</param>
        /// <param name="origin">the origin URI of the stream</param>
        public MessageEvent(string name, Utf8ByteSpan dataUtf8Bytes, string lastEventId, Uri origin)
        {
            _name = name;
            _dataString = null;
            _dataUtf8Bytes = dataUtf8Bytes;
            _lastEventId = lastEventId;
            _origin = origin;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageEvent" /> class.
        /// </summary>
        /// <param name="name">the event name</param>
        /// <param name="data">the data received in the server-sent event</param>
        /// <param name="origin">the origin URI of the stream</param>
        /// <remarks>
        /// The <see cref="LastEventId" /> will be initialized to null.
        /// </remarks>
        public MessageEvent(string name, string data, Uri origin) : this(name, data, null, origin) { }

        #endregion

        #region Public Properties

        /// <summary>
        /// The event name.
        /// </summary>
        /// <remarks>
        /// This can be specified by the server in the <c>event:</c> field in the SSE data, as in
        /// <c>event: my-event-name</c>. If there is no <c>event:</c> field, the default name
        /// is "message".
        /// </remarks>
        public string Name => _name;

        /// <summary>
        /// Gets the data received in the event as a string.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is the value of the <c>data:</c> field in the SSE data; if there are multiple
        /// <c>data:</c> lines for a single event, they are concatenated with <c>"\n"</c>.
        /// </para>
        /// <para>
        /// If the data was originally stored as a string, the same string is returned.
        /// If it was stored as a UTF-8 byte array, the bytes are copied to a new string.
        /// </para>
        /// </remarks>
        public string Data => _dataString is null ? _dataUtf8Bytes.GetString() : _dataString;

        /// <summary>
        /// Gets the data received in the event as a UTF-8 byte span.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is the value of the <c>data:</c> field in the SSE data; if there are multiple
        /// <c>data:</c> lines for a single event, they are concatenated with <c>"\n"</c>.
        /// </para>
        /// <para>
        /// If the data was originally stored as UTF-8 bytes, the returned value refers to
        /// the same array, offset, and length (it is the caller's responsibility not to
        /// modify the byte array). If it was originally stored as a string, the string
        /// is copied to a new byte array.
        /// </para>
        /// </remarks>
        public Utf8ByteSpan DataUtf8Bytes => _dataString is null ? _dataUtf8Bytes :
            new Utf8ByteSpan(_dataString);

        /// <summary>
        /// Gets the last event identifier received in the server-sent event.
        /// </summary>
        /// <remarks>
        /// This is the value of the <c>id:</c> field in the SSE data. If there is no such
        /// field, it is null. You can use a previously received <see cref="LastEventId"/>
        /// value with <see cref="ConfigurationBuilder.LastEventId(string)"/> when starting
        /// a new <see cref="EventSource"/> to tell the server what the last event you
        /// received was, although not all servers support this.
        /// </remarks>
        public string LastEventId => _lastEventId;

        /// <summary>
        /// Gets the origin URI of the stream that generated the server-sent event.
        /// </summary>
        public Uri Origin => _origin;

        /// <summary>
        /// True if the event data is stored internally as UTF-8 bytes.
        /// </summary>
        /// <remarks>
        /// The data can be accessed with either <see cref="Data"/> or <see cref="DataUtf8Bytes"/>
        /// regardless of the value of this property. The property only indicates the <i>original</i>
        /// format of the data, so, for instance, if it is <see langword="true"/> then
        /// reading <see cref="Data"/> will have more overhead (due to copying) than
        /// reading <see cref="DataUtf8Bytes"/>.
        /// </remarks>
        public bool IsDataUtf8Bytes => _dataString is null;

        #endregion

        #region Public Methods

        /// <summary>
        /// Determines whether the specified <see cref="System.Object" /> is equal to this instance.
        /// </summary>
        /// <remarks>
        /// This method is potentially inefficient and should be used only in testing.
        /// </remarks>
        /// <param name="obj">the <see cref="System.Object" /> to compare with this instance</param>
        /// <returns><see langword="true"/> if the instances are equal</returns>
        public override bool Equals(object obj)
        {
            if (!(obj is MessageEvent that) ||
                !object.Equals(Name, that.Name) ||
                !object.Equals(LastEventId, that.LastEventId) ||
                !object.Equals(Origin, that.Origin))
            {
                return false;
            }
            if (this._dataString != null)
            {
                return that._dataString != null ?
                    this._dataString == that._dataString :
                    that._dataUtf8Bytes.Equals(this._dataString);

            }
            return that._dataString != null ?
                this._dataUtf8Bytes.Equals(that._dataString) :
                this._dataUtf8Bytes.Equals(that._dataUtf8Bytes);
        }

        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        /// <returns>
        /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table.
        /// This method is potentially inefficient and should be used only in testing.
        /// </returns>
        public override int GetHashCode()
        {
            int hash = 17;

            hash = hash * 31 + (_dataString != null ? _dataString.GetHashCode() : _dataUtf8Bytes.GetString().GetHashCode());
            hash = hash * 31 + (_lastEventId != null ? _lastEventId.GetHashCode() : 0);
            hash = hash * 31 + (_origin != null ? _origin.GetHashCode() : 0);
            return hash;
        }

        #endregion
    }
}
