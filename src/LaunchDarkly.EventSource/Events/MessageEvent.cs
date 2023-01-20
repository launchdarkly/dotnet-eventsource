using System;
using System.IO;
using System.Text;

namespace LaunchDarkly.EventSource.Events
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
    /// The event name and ID properties are always stored as strings. The data property
    /// can be read as a string, but you can choose to consume it as a stream instead;
    /// see <see cref="DataStream"/>.
    /// </para>
    /// </remarks>
    public class MessageEvent : IEvent
    {
        /// <summary>
        /// The default value of <see cref="Name"/> if the SSE stream did not specify an
        /// <c>event:</c> field.
        /// </summary>
        public const string DefaultName = "message";

        #region Private Fields

        private readonly string _name;
        private volatile string _dataString;
        private volatile Stream _dataStream;
        private readonly string _lastEventId;
        private readonly Uri _origin;
        
        #endregion

        #region Public Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageEvent"/> class.
        /// </summary>
        /// <remarks>
        /// This constructor assumes that the event data has been fully read into memory as a
        /// string.
        /// </remarks>
        /// <param name="name">the event name</param>
        /// <param name="data">the data received in the server-sent event</param>
        /// <param name="lastEventId">the last event identifier, or null</param>
        /// <param name="origin">the origin URI of the stream</param>
        public MessageEvent(string name, string data, string lastEventId, Uri origin)
        {
            if (name is null)
            {
                throw new ArgumentNullException(nameof(name));
            }
            _name = name;
            _dataString = data ?? "";
            _dataStream = null;
            _lastEventId = lastEventId;
            _origin = origin;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageEvent" /> class.
        /// </summary>
        /// <remarks>
        /// The <see cref="LastEventId" /> will be initialized to null. This constructor assumes
        /// that the event data has been fully read into memory as a string.
        /// </remarks>
        /// <param name="name">the event name</param>
        /// <param name="data">the data received in the server-sent event</param>
        /// <param name="origin">the origin URI of the stream</param>
        public MessageEvent(string name, string data, Uri origin) : this(name, data, null, origin) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageEvent"/> class
        /// with lazy-loading behavior.
        /// </summary>
        /// <param name="name">the event name</param>
        /// <param name="dataStream">a <see cref="Stream"/> for consuming the event data</param>
        /// <param name="lastEventId">the last event identifier, or null</param>
        /// <param name="origin">the origin URI of the stream</param>
        public MessageEvent(string name, Stream dataStream, string lastEventId, Uri origin)
        {
            if (name is null)
            {
                throw new ArgumentNullException(nameof(name));
            }
            if (dataStream is null)
            {
                _dataString = "";
                _dataStream = null;
            }
            else
            {
                _dataStream = dataStream;
                _dataString = null;
            }
            _name = name;
            _lastEventId = lastEventId;
            _origin = origin;
        }

        #endregion

        #region Public Properties

        /// <summary>
        /// The event name.
        /// </summary>
        /// <remarks>
        /// This can be specified by the server in the <c>event:</c> field in the SSE data, as in
        /// <c>event: my-event-name</c>. If there is no <c>event:</c> field, the default name
        /// is <see cref="DefaultName"/>.
        /// </remarks>
        public string Name => _name;

        /// <summary>
        /// Returns the event data as a string.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The format of event data is described in the SSE specification. Every event has at least one
        /// line with a <c>data</c> or <c>data:</c> prefix. After removing the prefix, multiple lines
        /// are concatenated with a separator of <c>\n</c> (ASCII 10).
        /// </para>
        /// <para>
        /// If you have set the <see cref="ConfigurationBuilder.StreamEventData(bool)"/> option to
        /// <see langword="true"/> to enable streaming delivery of event data to your handler without
        /// buffering the entire event, you should use <see cref="DataStream"/> instead of
        /// <see cref="Data"/>. Reading <see cref="Data"/> in this mode would defeat the purpose by
        /// causing all of the data to be read at once. However, if you do this, <see cref="Data"/>
        /// memoizes the result so that calling it repeatedly does not try to read the stream again.
        /// Also, be aware that doing this is a synchronous operation that will block the calling
        /// thread until all of the data is available.
        /// </para>
        /// <para>
        /// The method will never return <see langword="null"/>; every event has data, even if the
        /// data is empty (zero length).
        /// </para>
        /// </remarks>
        public string Data
        {
            get
            {
                lock (this)
                {
                    if (_dataString != null)
                    {
                        return _dataString;
                    }
                }
                return ReadFullyInternal();
            }
        }

        /// <summary>
        /// Returns a single-use <see cref="Stream"/> for consuming the event data.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This stream only supports reading. Calling <c>Write</c> or <c>Seek</c>, or
        /// trying to access <c>Position</c> or <c>Length</c>, will throw an exception.
        /// It is intended for asynchronous use with <c>ReadAsync</c>; doing a synchronous
        /// read with <c>Read</c> is possible, but should only be done from synchronous
        /// code that is not running on a <c>Task</c> thread, since otherwise it may
        /// cause a deadlock due to blocking that thread.
        /// </para>
        /// </remarks>
        public Stream DataStream
        {
            get
            {
                lock (this)
                {
                    if (_dataStream is null)
                    {
                        _dataStream = new MemoryStream(Encoding.UTF8.GetBytes(_dataString));
                    }
                    return _dataStream;
                }
            }
        }

        /// <summary>
        /// True if the event data is being provided as a stream, rather than having been
        /// read fully as a string.
        /// </summary>
        /// <remarks>
        /// This is initially true for every event if you are using the
        /// <see cref="ConfigurationBuilder.StreamEventData(bool)"/> mode, meaning that the
        /// data is being provided to you via <see cref="DataStream"/> incrementally as it
        /// arrives. If you instead read the <see cref="Data"/> property, causing it to be
        /// read all at once, then this property becomes false.
        /// </remarks>
        public bool IsStreamingData => _dataString is null;

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
        
        #endregion

        #region Public Methods

        /// <summary>
        /// Determines whether the specified object is equal to this instance.
        /// </summary>
        /// <remarks>
        /// This method is potentially inefficient and should be used only in testing.
        /// Also, if the event contains a <see cref="DataStream"/>, it is not possible to
        /// compare streams so this method will ignore the data field.
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
            if (this._dataStream != null || that._dataStream != null)
            {
                return true;
            }
            return this._dataString == that._dataString;
        }

        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        /// <returns>
        /// A hash code for this instance, suitable for use in hashing algorithms and data
        /// structures like a hash table. This method is potentially inefficient and should be
        /// used only in testing; also, you should not try to use a MessageEvent as a hash key
        /// or store it in a Set if it has a <see cref="DataStream"/>, since then it is mutable.
        /// </returns>
        public override int GetHashCode()
        {
            int hash = 17;

            hash = hash * 31 + (_dataString != null ? _dataString.GetHashCode() : 0);
            hash = hash * 31 + (_lastEventId != null ? _lastEventId.GetHashCode() : 0);
            hash = hash * 31 + (_origin != null ? _origin.GetHashCode() : 0);
            return hash;
        }

        /// <summary>
        /// Returns a simple string representation of the MessageEvent. Do not rely on
        /// the exact format of this string; it is intended for debugging.
        /// </summary>
        /// <returns>a string representation</returns>
        public override string ToString()
        {
            var sb = new StringBuilder("MessageEvent(Name=")
                .Append(_name)
                .Append(",Data=");
            lock (this)
            {
                sb.Append(_dataString ?? "<streaming>");
            }
            if (_lastEventId != null)
            {
                sb.Append(",Id=").Append(_lastEventId);
            }
            sb.Append(",Origin=").Append(_origin).Append(')');
            return sb.ToString();
        }

        #endregion

        #region Private Methods

        private string ReadFullyInternal()
        {
            string s;
            using (var reader = new StreamReader(_dataStream, Encoding.UTF8))
            {
                s = reader.ReadToEnd();
            }
            lock (this)
            {
                _dataString = s;
            }
            return s;
        }

        #endregion
    }
}
