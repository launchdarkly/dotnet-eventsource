using System;

namespace LaunchDarkly.EventSource
{
    /// <summary>
    /// Represents the Server Sent Event message received by an EventSource API.
    /// </summary>
    public sealed class MessageEvent
    {
        #region Private Fields

        private readonly string _data;
        private readonly string _lastEventId;
        private readonly Uri _origin;

        #endregion

        #region Public Constructors

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageEvent"/> class.
        /// </summary>
        /// <param name="data">The data received in the server sent event.</param>
        /// <param name="lastEventId">The last event identifier.</param>
        /// <param name="origin">The origin URI in the server sent event.</param>
        public MessageEvent(string data, string lastEventId, Uri origin)
        {
            _data = data;
            _lastEventId = lastEventId;
            _origin = origin;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MessageEvent" /> class.
        /// </summary>
        /// <param name="data">The data received in the server sent event.</param>
        /// <param name="origin">The origin URI in the server sent event.</param>
        /// <remarks>
        /// The <see cref="LastEventId" /> will be initialized to null.
        /// </remarks>
        public MessageEvent(string data, Uri origin) : this(data, null, origin)
        {
        }

        #endregion

        #region Public Properties
        /// <summary>
        /// Gets the data received in the server sent event.
        /// </summary>
        /// <value>
        /// The data.
        /// </value>
        public string Data
        {
            get
            {
                return _data;
            }
        }

        /// <summary>
        /// Gets the last event identifier received in the server sent event. This may be null if not provided by the API.
        /// </summary>
        /// <value>
        /// The last event identifier.
        /// </value>
        public string LastEventId
        {
            get
            {
                return _lastEventId;
            }
        }

        /// <summary>
        /// Gets the origin URI of the server sent event.
        /// </summary>
        /// <value>
        /// The origin.
        /// </value>
        public Uri Origin
        {
            get
            {
                return _origin;
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Determines whether the specified <see cref="System.Object" />, is equal to this instance.
        /// </summary>
        /// <param name="obj">The <see cref="System.Object" /> to compare with this instance.</param>
        /// <returns>
        ///   <c>true</c> if the specified <see cref="System.Object" /> is equal to this instance; otherwise, <c>false</c>.
        /// </returns>
        public override bool Equals(object obj)
        {
            if (this == obj) return true;
            if (obj == null || GetType() != obj.GetType()) return false;

            MessageEvent that = (MessageEvent)obj;

            if (!_data?.Equals(that._data) ?? that._data != null) return false;
            if (!_lastEventId?.Equals(that._lastEventId) ?? that._lastEventId != null) return false;
            return !(_origin != null ? !_origin.Equals(that._origin) : that._origin != null);
        }

        /// <summary>
        /// Returns a hash code for this instance.
        /// </summary>
        /// <returns>
        /// A hash code for this instance, suitable for use in hashing algorithms and data structures like a hash table. 
        /// </returns>
        public override int GetHashCode()
        {
            int hash = 17;

            hash = hash * 31 + (_data != null ? _data.GetHashCode() : 0);
            hash = hash * 31 + (_lastEventId != null ? _lastEventId.GetHashCode() : 0);
            hash = hash * 31 + (_origin != null ? _origin.GetHashCode() : 0);
            return hash;
        }

        #endregion
    }

}
