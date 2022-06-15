using System.Net.Http.Headers;

using IdentityModel.Jwk;
using IdentityModel.OidcClient;

namespace WarhornReporting
{
    internal class OidcHelper
    {
        /// <summary>
        /// Gets the OAuth token and returns a usable HttpClient.
        /// </summary>
        /// <param name="clientId">Client ID created when registering the app.</param>
        /// <returns>Client with Auth token set to make API calls with.</returns>
        /// <see cref="https://github.com/IdentityModel" />
        public static async Task<HttpClient> GetHttpClientAsync(string clientId)
        {
            var browser = new SystemBrowser();

            var provider = new ProviderInformation
            {
                IssuerName = IssuerName,
                KeySet = new JsonWebKeySet(),
                TokenEndpoint = TokenEndpoint,
                AuthorizeEndpoint = AuthorizeEndpoint
            };

            var options = new OidcClientOptions
            {
                ProviderInformation = provider,
                ClientId = clientId,
                RedirectUri = LoopbackHttpListener.RedirectUri,
                Scope = Scope,
                FilterClaims = false,
                LoadProfile = false,

                Browser = browser
            };

            var oidcClient = new OidcClient(options);
            var result = await oidcClient.LoginAsync(new LoginRequest());

            var apiClient = new HttpClient()
            {
                BaseAddress = new Uri(Host)
            };
            apiClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);

            return apiClient;
        }

        private const string IssuerName = "Warhorn";
        private const string Host = "https://warhorn.net";
        private const string Scope = "openid";

        private static readonly string AuthorizeEndpoint = $"{Host}/oauth/authorize";
        private static readonly string TokenEndpoint = $"{Host}/oauth/token";
    }
}
