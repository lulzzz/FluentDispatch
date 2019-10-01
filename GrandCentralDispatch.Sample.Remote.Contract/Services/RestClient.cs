using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using GrandCentralDispatch.Sample.Remote.Contract.Helpers;

namespace GrandCentralDispatch.Sample.Remote.Contract.Services
{
    /// <summary>
    /// This is a custom HttpClient which works best when used as a singleton. Settings ensure to reuse ports and keep alive connections
    /// </summary>
    public sealed class RestClient : IRestClient
    {
        private static readonly int MaxConnectionPerServer = int.MaxValue;
        private static readonly TimeSpan ConnectionLifeTime = TimeSpan.FromMinutes(1);
        private readonly HttpClient _client;
        private readonly HashSet<EndpointCacheKey> _endpoints;

        static RestClient() => ConfigureServicePointManager();

        public RestClient(
            IDictionary<string, string> defaultRequestHeaders = null,
            HttpMessageHandler handler = null,
            Uri baseAddress = null,
            bool disposeHandler = true,
            TimeSpan? timeout = null,
            ulong? maxResponseContentBufferSize = null)
        {
            _client = new HttpClient(handler ?? GetHandler(), disposeHandler);
            _client.DefaultRequestHeaders.ConnectionClose = false;
            _client.DefaultRequestHeaders.Connection.Add("Keep-Alive");
            _client.DefaultRequestHeaders.Add("Keep-Alive", "600");
            _endpoints = new HashSet<EndpointCacheKey>();
            AddBaseAddress(baseAddress);
            AddDefaultHeaders(defaultRequestHeaders);
            AddRequestTimeout(timeout);
            AddMaxResponseBufferSize(maxResponseContentBufferSize);
        }

        private static HttpMessageHandler GetHandler()
        {
            return new HttpClientHandler
            {
                MaxConnectionsPerServer = MaxConnectionPerServer,
                UseDefaultCredentials = true,
                AllowAutoRedirect = false,
                UseProxy = false,
                UseCookies = false,
                PreAuthenticate = false
            };
        }

        public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cToken)
            => SendAsync(request, HttpCompletionOption.ResponseContentRead, cToken);

        public Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, HttpCompletionOption option,
            CancellationToken cToken)
        {
            AddConnectionLeaseTimeout(new Uri(request.RequestUri.GetLeftPart(UriPartial.Authority)));
            return _client.SendAsync(request, option, cToken);
        }

        public Task<HttpResponseMessage> GetAsync(Uri uri, CancellationToken cToken)
            => SendAsync(new HttpRequestMessage(HttpMethod.Get, uri), cToken);

        public HashSet<EndpointCacheKey> Endpoints => _endpoints;

        /// <summary>
        /// Cancels all pending requests on this instance.
        /// </summary>
        public void CancelPendingRequests() => _client.CancelPendingRequests();

        /// <summary>
        /// Releases the unmanaged resources and disposes of the managed resources used by the <see cref="HttpClient"/>.
        /// </summary>
        public void Dispose() => _client.Dispose();

        private static void ConfigureServicePointManager()
        {
            // Default is 2 minutes, see https://msdn.microsoft.com/en-us/library/system.net.servicepointmanager.dnsrefreshtimeout(v=vs.110).aspx
            ServicePointManager.DnsRefreshTimeout = (int) ConnectionLifeTime.TotalMilliseconds;

            // Increases the concurrent outbound connections
            ServicePointManager.UseNagleAlgorithm = false;
            ServicePointManager.Expect100Continue = false;
            ServicePointManager.EnableDnsRoundRobin = true;
            ServicePointManager.DefaultConnectionLimit = MaxConnectionPerServer;
            ServicePointManager.ReusePort = true;
            ServicePointManager.SetTcpKeepAlive(true, 60000 * 5, 3000);
        }

        private void AddBaseAddress(Uri uri)
        {
            if (uri is null)
            {
                return;
            }

            AddConnectionLeaseTimeout(uri);
            _client.BaseAddress = uri;
        }

        private void AddDefaultHeaders(IDictionary<string, string> headers)
        {
            if (headers is null)
            {
                return;
            }

            foreach (var item in headers)
            {
                _client.DefaultRequestHeaders.Add(item.Key, item.Value);
            }
        }

        private void AddRequestTimeout(TimeSpan? timeout)
            => _client.Timeout = timeout ?? Timeout.InfiniteTimeSpan;

        private void AddMaxResponseBufferSize(ulong? size)
        {
            if (!size.HasValue)
            {
                return;
            }

            _client.MaxResponseContentBufferSize = (long) size.Value;
        }

        private void AddConnectionLeaseTimeout(Uri endpoint)
        {
            if (!endpoint.IsAbsoluteUri)
            {
                return;
            }

            var key = new EndpointCacheKey(endpoint);
            lock (_endpoints)
            {
                if (_endpoints.Contains(key))
                {
                    return;
                }

                ServicePointManager.FindServicePoint(endpoint)
                    .ConnectionLeaseTimeout = (int) ConnectionLifeTime.TotalMilliseconds;
                _endpoints.Add(key);
            }
        }

        public struct EndpointCacheKey : IEquatable<EndpointCacheKey>
        {
            public EndpointCacheKey(Uri uri) => Uri = uri;

            public bool Equals(EndpointCacheKey other) => Uri == other.Uri;

            public override bool Equals(object obj) => obj is EndpointCacheKey other && Equals(other);

            public override int GetHashCode() =>
                HashHelper.GetHashCode(Uri.Scheme, Uri.DnsSafeHost, Uri.Port);

            public Uri Uri { get; }

            public static bool operator ==(EndpointCacheKey left, EndpointCacheKey right)
                => left.Equals(right);

            public static bool operator !=(EndpointCacheKey left, EndpointCacheKey right)
                => !left.Equals(right);
        }
    }
}