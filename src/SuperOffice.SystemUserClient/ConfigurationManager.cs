// SuperOffice AS licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.IdentityModel.Tokens;
using SuperOffice.SystemUser.Client.Exceptions;
using SuperOffice.SystemUser.Helpers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace SuperOffice.SystemUser
{
    /// <summary>
    /// Gets CRM online OpenID Connect configuration and security keys.
    /// </summary>
    public class ConfigurationManager
    {
        private OidcConfigAgent _configAgent;
        private string _subdomain;

        /// <summary>
        /// Gets or sets the Authority to use when making OpenId Connect calls.
        /// </summary>
        public string Authority { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the ClaimsIssuer to use when making OpenId Connect calls.
        /// </summary>
        public string ClaimsIssuer { get; set; } = string.Empty;

        /// <summary>
        /// OpenId Connect configuration settings.
        /// </summary>
        public OpenIdConnectConfiguration Configuration { get; private set; } = null;

        /// <summary>
        /// Gets or sets the target online subdomain to either sod, qaonline or online (for dev, stage, and production).
        /// The default value is "sod".
        /// </summary>
        public string SubDomain
        {
            get
            {
                return _subdomain;
            }

            set
            {
                _subdomain = value;
                UpdateEndpoints();
            }
        }

        /// <summary>
        /// Gets or sets the MetadataAddress Uri to use when making getting OpenId Connect configuration.
        /// </summary>
        public string MetadataAddress { get; internal set; } = string.Empty;

        /// <summary>
        /// Constructor 
        /// </summary>
        /// <param name="client">HttpClient instance.</param>
        /// <param name="subdomain">Online subdomain (sod, qaonline, online)</param>
        public ConfigurationManager(HttpClient client, string subdomain)
        {
            if (!subdomain.IsValidSubDomain())
                throw new InvalidSubDomainException("Invalid SuperOffice CRM online subdomain specified. Use 'sod' or 'qaonline' or 'online'.");

            SubDomain = subdomain;
  
            // Have to look into avoiding WebApiOptions parameter here
            _configAgent =
                new OidcConfigAgent(MetadataAddress, client);
        }

        /// <summary>
        /// Gets OpenID metadata from metadata Uri. 
        /// </summary>
        /// <returns></returns>
        public async Task<OpenIdConnectConfiguration> GetConfigurationAsync()
        {
            // WebApiOptions messes with base url...
            //if(MetadataAddress.EndsWith("api/", StringComparison.InvariantCultureIgnoreCase))
            //    MetadataAddress = MetadataAddress.Substring(0, MetadataAddress.Length - 3);

            Configuration = await _configAgent.GetConfigurationAsync(MetadataAddress).ConfigureAwait(false);
            
            // Sometimes need to retry
            
            if(Configuration == null || string.IsNullOrEmpty(Configuration.AuthorizationEndpoint))
                Configuration = await _configAgent.GetConfigurationAsync(MetadataAddress).ConfigureAwait(false);

            return Configuration;
        }


        /// <summary>
        /// Updates configuration settings.
        /// </summary>
        private void UpdateEndpoints()
        {
            var urlHelper = new UrlHelper(_subdomain);
            MetadataAddress = urlHelper.GetMetadataAddress();
        }
    }
}
