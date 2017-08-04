using System;
using System.Collections.Generic;
using System.Text;

namespace LaunchDarkly.EventSource
{
    public class MessageEvent
    {
        private readonly String _data;
        private readonly String _lastEventId;
        private readonly Uri _origin;

        public MessageEvent(String data, String lastEventId, Uri origin)
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
            int result = _data != null ? _data.GetHashCode() : 0;
            result = 31 * result + (_lastEventId != null ? _lastEventId.GetHashCode() : 0);
            result = 31 * result + (_origin != null ? _origin.GetHashCode() : 0);
            return result;
        }

    }

}
