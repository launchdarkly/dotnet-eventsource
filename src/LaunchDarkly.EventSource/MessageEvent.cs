using System;
using System.Collections.Generic;
using System.Text;

namespace LaunchDarkly.EventSource
{
    public class MessageEvent
    {
        private readonly string _data;
        private readonly string _lastEventId;
        private readonly Uri _origin;

        public MessageEvent(string data, string lastEventId, Uri origin)
        {
            _data = data;
            _lastEventId = lastEventId;
            _origin = origin;
        }

        public MessageEvent(string data) : this(data, null, null)
        {
        }

        public string Data
        {
            get
            {
                return _data;
            }
        }

        public string LastEventId
        {
            get
            {
                return _lastEventId;
            }
        }

        public Uri Origin
        {
            get
            {
                return _origin;
            }
        }

        public override bool Equals(object obj)
        {
            if (this == obj) return true;
            if (obj == null || GetType() != obj.GetType()) return false;

            MessageEvent that = (MessageEvent)obj;

            if (!_data?.Equals(that._data) ?? that._data != null) return false;
            if (!_lastEventId?.Equals(that._lastEventId) ?? that._lastEventId != null) return false;
            return !(_origin != null ? !_origin.Equals(that._origin) : that._origin != null);
        }

        public override int GetHashCode()
        {
            int hash = 17;

            hash = hash * 31 + (_data != null ? _data.GetHashCode() : 0);
            hash = hash * 31 + (_lastEventId != null ? _lastEventId.GetHashCode() : 0);
            hash = hash * 31 + (_origin != null ? _origin.GetHashCode() : 0);
            return hash;
        }

    }

}
