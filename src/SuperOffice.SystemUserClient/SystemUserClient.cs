// SuperOffice AS licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.IdentityModel.Tokens;
using SuperOffice.SystemUser.Tokens;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SuperOffice.SystemUser
{
    /// <summary>
    /// Provides a class to obtain a System User Ticket or System User JWT.
    /// </summary>
    public class SystemUserClient : ISystemUserClient
    {
        private readonly SystemUserInfo _systemUserInfo;
        private readonly HttpClient _httpClient;

        /// <inheritdoc />
        public ClaimsIdentity ClaimsIdentity { get; private set; }

        /// <inheritdoc />
        public bool UseDefaultCredentials { get; set; } = false;


        /// <summary>
        /// Initializes a new instance of the SystemUserClient class.
        /// </summary>
        /// <param name="systemUserInfo">Required details to send to the system user endpoint.</param>
        /// <param name="client">Optional HttpClient, to call the system user web service.</param>
        /// <exception cref="ArgumentNullException"></exception>
        public SystemUserClient(SystemUserInfo systemUserInfo, HttpClient client = null)
        {
            if (systemUserInfo is null)
            {
                throw new ArgumentNullException(nameof(systemUserInfo));
            }

            if (client == null)
            {
                UseDefaultCredentials = false;
            }

            this._systemUserInfo = systemUserInfo;
            this._httpClient = client;
        }

        /// <inheritdoc />
        public async Task<string> GetSystemUserTicketAsync()
        {
            try
            {
                var systemUserResult = await GetSystemUserJwtAsync();
                var validationResult = await ValidateSystemUserResultAsync(systemUserResult);

                if (!validationResult.IsValid)
                {
                    throw new SecurityTokenValidationException(Constants.Error.FailedSystemUserTokenValidation + $" Token {_systemUserInfo.SystemUserToken}, in {_systemUserInfo.SubDomain}", validationResult.Exception);
                }


                // get => set the system user ticket for reuse
                return validationResult.ClaimsIdentity.Claims.First(c => c.Type.Equals(Constants.ClaimNames.Ticket, StringComparison.OrdinalIgnoreCase)).Value;
            }
            catch
            {
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<TokenValidationResult> ValidateSystemUserResultAsync(SystemUserResult systemUserResult)
        {
            if (!systemUserResult.IsSuccessful)
            {
                throw new Exception("Unable to retreive System User JWT from SuperOffice PartnerSystemUserService endpoint. " + systemUserResult.ErrorMessage);
            }

            try
            {
                var tokenHandler = new SystemUserTokenHandler(GetClient(UseDefaultCredentials), _systemUserInfo.SubDomain);

                var validationResult = await tokenHandler.ValidateAsync(systemUserResult.Token).ConfigureAwait(false);

                ClaimsIdentity = validationResult?.ClaimsIdentity;

                return validationResult;

            }
            catch
            {
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<SystemUserResult> GetSystemUserJwtAsync()
        {
            try
            {
                var request = new HttpRequestMessage()
                {
                    RequestUri = new Uri(GetSystemUserUrl()),
                    Method = HttpMethod.Post
                };
                request.Content = new StringContent(GetSystemUserRequestBody(), Encoding.UTF8, Constants.ContentType.Xml);
                request.Headers.Clear();
                request.Content.Headers.ContentType = new MediaTypeHeaderValue(Constants.ContentType.Json);

                var client = GetClient(UseDefaultCredentials);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(Constants.ContentType.Json));

                HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    throw new HttpRequestException($"Request to PartnerSystemUserService endpoint failed with status code {response.StatusCode}. Reason: {response.ReasonPhrase}. Verify all system user information is correct.");
                }

                var result = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

                return JsonSerializer.Deserialize<SystemUserResult>(result);
            }
            catch (AggregateException ex)
            {
                if (ex.InnerException is TaskCanceledException)
                {
                    throw ex.InnerException;
                }
                else
                {
                    throw ex;
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private string GetSystemUserRequestBody()
        {
            var result = JsonSerializer.Serialize(_systemUserInfo);
            return result;
        }

        private string GetSystemUserUrl()
        {
            return Constants.SystemUser.SystemUserEndpoint.FormatWith(_systemUserInfo.SubDomain);
        }

        /// <summary>
        /// Gets a basic HttpClient with minimal handler settings.
        /// </summary>
        /// <param name="setDefaultCredentials">Determines whether the handler is used to set default credentials and proxy settings.</param>
        /// <returns></returns>
        private HttpClient GetClient(bool setDefaultCredentials = false)
        {
            if (_httpClient != null)
                return _httpClient;

            if (!setDefaultCredentials)
                return new HttpClient();

            var handler = new HttpClientHandler();
            {
                handler.UseDefaultCredentials = true;
                handler.Credentials = System.Net.CredentialCache.DefaultNetworkCredentials; // current user
                handler.AutomaticDecompression = DecompressionMethods.Deflate | DecompressionMethods.GZip;

                // Turns out to be ignored by NTLM/Negotiate auth - it only works with current user. 
                // Need to impersonate first if you want to use different identity

                handler.PreAuthenticate = true;
                handler.AllowAutoRedirect = true; // Handle Negotiate automatically
                handler.Proxy = WebRequest.DefaultWebProxy;
                handler.Proxy.Credentials = WebRequest.DefaultWebProxy.Credentials;

            };

            var client = new HttpClient(handler);
            // client timeout 30 seconds
            client.Timeout = new TimeSpan(0, 0, 30);
            return client;
        }
    }
}
