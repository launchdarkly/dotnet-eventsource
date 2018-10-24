using Common.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;

namespace LaunchDarkly.EventSource
{
    /// <summary>
    /// A standard Builder pattern for constructing a <see cref="Configuration"/> instance.
    /// 
    /// Initialize a builder by calling <c>new ConfigurationBuilder(uri)</c> or
    /// <c>Configuration.Builder(uri)</c>. The URI is always required; all other properties
    /// are set to defaults. Use the builder's setter methods to modify any desired properties;
    /// setter methods can be chained. Then call <c>Build()</c> to construct the final immutable
    /// <c>Configuration</c>.
    /// 
    /// All setter methods will throw <c>ArgumentException</c> if called with an invalid value,
    /// so it is never possible for <c>Build()</c> to fail.
    /// </summary>
    public class ConfigurationBuilder
    {
        #region Private Fields

        private readonly Uri _uri;
        private TimeSpan _connectionTimeout = Configuration.DefaultConnectionTimeout;
        private TimeSpan _delayRetryDuration = Configuration.DefaultDelayRetryDuration;
        private TimeSpan _backoffResetThreshold = Configuration.DefaultBackoffResetThreshold;
        private TimeSpan _readTimeout = Configuration.DefaultReadTimeout;
        private string _lastEventId;
        private ILog _logger;
        private IDictionary<string, string> _requestHeaders = new Dictionary<string, string>();
        private HttpMessageHandler _messageHandler;
        private HttpMethod _method = HttpMethod.Get;
        private Configuration.HttpContentFactory _requestBodyFactory;

        #endregion

        #region Constructor

        public ConfigurationBuilder(Uri uri)
        {
            if (uri == null)
            {
                throw new ArgumentNullException(nameof(uri));
            }
            this._uri = uri;
        }

        #endregion

        #region Public Methods

        public Configuration Build()
        {
            return new Configuration(_uri, _messageHandler, _connectionTimeout, _delayRetryDuration, _readTimeout,
                _requestHeaders, _lastEventId, _logger, _method, _requestBodyFactory);
        }
        
        public ConfigurationBuilder ConnectionTimeout(TimeSpan connectionTimeout)
        {
            Configuration.CheckConnectionTimeout(connectionTimeout);
            _connectionTimeout = connectionTimeout;
            return this;
        }

        public ConfigurationBuilder DelayRetryDuration(TimeSpan delayRetryDuration)
        {
            Configuration.CheckDelayRetryDuration(delayRetryDuration);
            _delayRetryDuration = delayRetryDuration;
            return this;
        }

        public ConfigurationBuilder BackoffResetThreshold(TimeSpan backoffResetThreshold)
        {
            _backoffResetThreshold = backoffResetThreshold;
            return this;
        }

        public ConfigurationBuilder ReadTimeout(TimeSpan readTimeout)
        {
            Configuration.CheckReadTimeout(readTimeout);
            _readTimeout = readTimeout;
            return this;
        }

        public ConfigurationBuilder LastEventId(string lastEventId)
        {
            _lastEventId = lastEventId;
            return this;
        }

        public ConfigurationBuilder Logger(ILog logger)
        {
            _logger = logger;
            return this;
        }

        public ConfigurationBuilder RequestHeaders(IDictionary<string, string> headers)
        {
            if (headers == null)
            {
                throw new ArgumentNullException(nameof(headers));
            }
            _requestHeaders = headers;
            return this;
        }
        
        public ConfigurationBuilder RequestHeader(string name, string value)
        {
            _requestHeaders[name] = value;
            return this;
        }

        public ConfigurationBuilder MessageHandler(HttpMessageHandler handler)
        {
            this._messageHandler = handler;
            return this;
        }

        public ConfigurationBuilder Method(HttpMethod method)
        {
            if (method == null)
            {
                throw new ArgumentNullException(nameof(method));
            }
            this._method = method;
            return this;
        }

        public ConfigurationBuilder RequestBodyFactory(Configuration.HttpContentFactory factory)
        {
            this._requestBodyFactory = factory;
            return this;
        }

        public ConfigurationBuilder RequestBody(string bodyString, string contentType)
        {
            return RequestBodyFactory(() => new StringContent(bodyString, Encoding.UTF8, contentType));
        }

        #endregion
    }
}
