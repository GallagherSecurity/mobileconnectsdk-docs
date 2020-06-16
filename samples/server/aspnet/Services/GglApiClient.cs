using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

#nullable enable

namespace GallagherUniversityStudentPortalSampleSite.Services
{
    public class GglApiClientFactory
    {
        private readonly HttpClient _httpClient;

        public GglApiClientFactory(string host, string apiKey, string? clientCertificatePfx = null, string? clientCertificatePfxPassword = null)
        {
            // create a single long-lived HttpClient, that's what microsoft's documentation says to do
            var handler = new HttpClientHandler();
            
            // the gallagher server will have a self-signed certificate which won't validate against any certificate authorities. You should pin it's public key instead
            handler.ServerCertificateCustomValidationCallback = (message, certificate, chain, policyErrors) => true;

            var client = new HttpClient(handler) {
                BaseAddress = new Uri(host)
            };
            client.DefaultRequestHeaders.Add("Accept", "application/json");
            client.DefaultRequestHeaders.Add("Authorization", $"GGL-API-KEY {apiKey}");
            
            if(clientCertificatePfx != null && clientCertificatePfxPassword != null)
            {
                var cert = new X509Certificate2(clientCertificatePfx, clientCertificatePfxPassword);
                handler.ClientCertificates.Add(cert);
            }

            _httpClient = client;
        }

        public GglApiClient CreateClient() => new GglApiClient(_httpClient);

    }

    // A small wrapper around HttpClient
    //
    // It adds the Authorization: GGL-API-KEY header to authenticate with Command Centre.
    // It configures the TLS client certificate.
    // It also turns off SSL server certificate validation (Command Centre will generally run with a self-signed certificate)
    //
    public class GglApiClient
    {
        readonly HttpClient _httpClient;

        public GglApiClient(HttpClient httpClient) => _httpClient = httpClient;

        public Task<TResponse> GetAsync<TResponse>(string url)
            => RequestAsync<int, TResponse>(HttpMethod.Get, url, 0);

        public Task<TResponse> PostAsync<TRequest, TResponse>(string url, TRequest body)
            => RequestAsync<TRequest, TResponse>(HttpMethod.Post, url, body);

        public Task<TResponse> PatchAsync<TRequest, TResponse>(string url, TRequest body)
            => RequestAsync<TRequest, TResponse>(HttpMethod.Patch, url, body);

        public Task<HttpResponseMessage> DeleteAsync(string requestUri) => _httpClient.DeleteAsync(requestUri);

        public async Task<TResponse> RequestAsync<TRequest, TResponse>(HttpMethod method, string requestUri, TRequest request)
        {
            var message = new HttpRequestMessage(method, requestUri);

            if (request != default)
            {
                if (typeof(HttpContent).IsAssignableFrom(typeof(TRequest))) // allow send of a raw HttpContent if people want to do that
                {
                    message.Content = (HttpContent)(object)request;
                }
                else // if there is a request, JSONify it
                {
                    var requestContent = JsonConvert.SerializeObject(request);
                    var sc = new StringContent(requestContent);
                    sc.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");

                    message.Content = sc;
                }
            }

            var response = await _httpClient.SendAsync(message);

            if (typeof(TResponse) == typeof(HttpResponseMessage)) // if someone doesn't want to deserialise the response they can bypass by asking for HttpResponseMessage
                return (TResponse)(object)response;

            var responseString = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<TResponse>(responseString);
        }
    }

    static class GglApiClientExtensions
    {
        // helper so you don't have to specify TRequest when submitting a plain old dictionary
        public static Task<TResponse> PostAsync<TResponse>(this GglApiClient apiClient, string url, Dictionary<string, object> body)
            => apiClient.RequestAsync<Dictionary<string, object>, TResponse>(HttpMethod.Post, url, body);

        public static Task<TResponse> PatchAsync<TResponse>(this GglApiClient apiClient, string url, Dictionary<string, object> body)
            => apiClient.RequestAsync<Dictionary<string, object>, TResponse>(HttpMethod.Patch, url, body);
    }
}
