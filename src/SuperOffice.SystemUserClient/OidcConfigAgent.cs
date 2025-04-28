// SuperOffice AS licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SuperOffice.SystemUser
{
    internal class OidcConfigAgent
    {
        private static MemoryCache _cache;
        private readonly ConfigurationManager<OpenIdConnectConfiguration> _configManager;

        static OidcConfigAgent()
        {
            _cache = new MemoryCache(new MemoryCacheOptions());
        }
        public OidcConfigAgent(string uri, HttpClient client)
        {
            var retriever = new OpenIdConnectConfigurationRetriever();
            _configManager = new ConfigurationManager<OpenIdConnectConfiguration>(
                uri,
                retriever,
                client);
        }

        internal async Task<OpenIdConnectConfiguration> GetConfigurationAsync(string metadataAddress)
        {
            OpenIdConnectConfiguration configuration;

            if (_cache.TryGetValue(metadataAddress, out configuration))
            {
                return configuration;
            }
            // Retrieve the OpenID configuration
            configuration = await _configManager.GetConfigurationAsync(CancellationToken.None);
            _cache.Set(metadataAddress, configuration, new DateTimeOffset(DateTime.Now.AddSeconds(15)));
            _cache.Set(configuration.JwksUri, configuration.JsonWebKeySet, new DateTimeOffset(DateTime.Now.AddSeconds(15)));
            return configuration;

        }
    }
}