﻿using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Globalization;
using Flurl;
using Flurl.Http;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using JohnsonControls.Metasys.BasicServices.Interfaces;
using JohnsonControls.Metasys.BasicServices.Models;
using System.Resources;
using System.Reflection;
using System.Threading;


namespace JohnsonControls.Metasys.BasicServices
{
    public class MetasysClient : IMetasysClient
    {
        protected FlurlClient Client;

        protected AccessToken AccessToken;

        protected bool RefreshToken;

        protected const int MAX_PAGE_SIZE = 1000;

        // Init Resource Manager to provide translations
        protected static ResourceManager Resource =
            new ResourceManager("JohnsonControls.Metasys.BasicServices.Resources.MetasysResources", typeof(MetasysClient).Assembly);

        /// <summary>
        /// The current Culture Used for Metasys client localization
        /// </summary>
        public CultureInfo Culture { get; set; }

        /// <summary>
        /// Creates a new TraditionalClient.
        /// </summary>
        /// <remarks>
        /// Takes an optional CultureInfo which is useful for formatting numbers and localization of strings. If not specified,
        /// the user's current culture is used.
        /// </remarks>
        /// <param name="hostname"></param>
        /// <param name="ignoreCertificateErrors"></param>
        /// <param name="version"></param>
        /// <param name="cultureInfo"></param>
        public MetasysClient(string hostname, bool ignoreCertificateErrors = false, ApiVersion version = ApiVersion.V2, CultureInfo cultureInfo = null)
        {
            // Set Metasys client if specified, otherwise use Current Culture
            Culture = cultureInfo ?? CultureInfo.CurrentCulture;

            // Init HTTP client
            AccessToken = new AccessToken(null, DateTime.UtcNow);
            FlurlHttp.Configure(settings => settings.OnErrorAsync = HandleFlurlErrorAsync);

            if (ignoreCertificateErrors)
            {
                HttpClientHandler httpClientHandler = new HttpClientHandler();
                httpClientHandler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true;
                HttpClient httpClient = new HttpClient(httpClientHandler);
                httpClient.BaseAddress = new Uri($"https://{hostname}"
                    .AppendPathSegments("api", version));
                Client = new FlurlClient(httpClient);
            }
            else
            {
                Client = new FlurlClient($"https://{hostname}"
                    .AppendPathSegments("api", version));
            }
        }

        /// <summary>
        /// Returns localized string for the current Metasys client locale or specified culture as optional parameters.
        /// </summary>
        /// <param name="resource"></param>
        /// <param name="cultureInfo"></param>
        /// <returns></returns>
        public string Localize(string resource, CultureInfo cultureInfo = null)
        {
            return StaticLocalize(resource, cultureInfo ?? Culture);
        }

        /// <summary>
        /// Returns localized string for the specified culture or fallbacks to en-US localization.
        /// Static method for exposure to outside classes.
        /// </summary>
        /// <param name="resource"></param>
        /// <param name="cultureInfo"></param>
        /// <returns></returns>
        public static string StaticLocalize(string resource, CultureInfo cultureInfo)
        {
            try
            {
                // Priority is the cultureInfo  parameter if available, otherwise Metasys client culture
                return Resource.GetString(resource, cultureInfo);
            }
            catch (MissingManifestResourceException)
            {
                try
                {
                    // Fallback to en-US language if no resource found
                    return Resource.GetString(resource, new CultureInfo(1033));
                }
                catch (MissingManifestResourceException)
                {
                    // Just return resource placeholder when no translation found
                    return resource;
                }
            }
        }

        /// <summary>
        /// Throws MetasysExceptions when a Flurl exception occurs
        /// </summary>
        /// <returns>
        /// <exception cref="MetasysHttpParsingException"></exception>
        /// <exception cref="MetasysHttpTimeoutException"></exception>
        /// <exception cref="MetasysHttpException"></exception>
        private async Task HandleFlurlErrorAsync(HttpCall call)
        {
            if (call.Exception.GetType() == typeof(Flurl.Http.FlurlParsingException))
            {
                throw new MetasysHttpParsingException(call, "JSON", call.Response.Content.ToString(), call.Exception.InnerException);
            }
            else if (call.Exception.GetType() == typeof(Flurl.Http.FlurlHttpTimeoutException))
            {
                throw new MetasysHttpTimeoutException(call, call.Exception.InnerException);
            }
            else
            {
                throw new MetasysHttpException(call, call.Exception.Message, call.Response.Content.ToString(), call.Exception.InnerException);
            }
        }

        private async Task LogErrorAsync(String message)
        {
            await Console.Error.WriteLineAsync(message).ConfigureAwait(false);
        }

        /// <summary>
        /// Attempts to login to the given host.
        /// </summary>
        public AccessToken TryLogin(string username, string password, bool refresh = true)
        {
            return TryLoginAsync(username, password, refresh).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Attempts to login to the given host asynchronously.
        /// </summary>
        /// <returns>
        /// <exception cref="Flurl.Http.FlurlHttpException"></exception>
        public async Task<AccessToken> TryLoginAsync(string username, string password, bool refresh = true)
        {
            this.RefreshToken = refresh;

            var response = await Client.Request("login")
                .PostJsonAsync(new { username, password })
                .ReceiveJson<JToken>()
                .ConfigureAwait(false);

            CreateAccessToken(response);
            return this.AccessToken;
        }

        /// <summary>
        /// Requests a new access token before current token expires.
        /// </summary>
        public AccessToken Refresh()
        {
            return RefreshAsync().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Requests a new access token before current token expires asynchronously.
        /// </summary>
        /// <exception cref="Flurl.Http.FlurlHttpException"></exception>
        public async Task<AccessToken> RefreshAsync()
        {
            var response = await Client.Request("refreshToken")
                .GetJsonAsync<JToken>()
                .ConfigureAwait(false);

            CreateAccessToken(response);
            return this.AccessToken;
        }

        /// <summary>
        /// Creates a new AccessToken from a JToken and sets the client's authorization header if successful.
        /// On failure sets the AccessToken value to null and leaves authorization header in previous state.
        /// </summary>
        /// <exception cref="MetasysTokenException"></exception>
        private void CreateAccessToken(JToken token)
        {
            try
            {
                var accessTokenValue = token["accessToken"];
                var expires = token["expires"];
                var accessToken = $"Bearer {accessTokenValue.Value<string>()}";
                var date = expires.Value<DateTime>();
                this.AccessToken = new AccessToken(accessToken, date);
                Client.Headers.Remove("Authorization");
                Client.Headers.Add("Authorization", this.AccessToken.Token);
                if (RefreshToken)
                {
                    ScheduleRefresh();
                }
            }
            catch (System.NullReferenceException)
            {
                throw new MetasysTokenException(token.ToString());
            }
        }

        /// <summary>
        /// Will call Refresh() a minute before the token expires.
        /// </summary>
        private void ScheduleRefresh()
        {
            DateTime now = DateTime.UtcNow;
            TimeSpan delay = AccessToken.Expires - now;
            delay.Subtract(new TimeSpan(0, 1, 0));

            if (delay <= TimeSpan.Zero)
            {
                delay = TimeSpan.Zero;
            }

            int delayms = (int)delay.TotalMilliseconds;

            // If the time in milliseconds is greater than max int delayms will be negative and will not schedule a refresh.
            if (delayms >= 0)
            {
                System.Threading.Tasks.Task.Delay(delayms).ContinueWith(_ => Refresh());
            }
        }

        /// <summary>
        /// Returns the current access token and it's expiration date.
        /// </summary>
        public AccessToken GetAccessToken()
        {
            return this.AccessToken;
        }

        /// <summary>
        /// Returns the object identifier (id) of the specified object.
        /// </summary>
        public Guid GetObjectIdentifier(string itemReference)
        {
            return GetObjectIdentifierAsync(itemReference).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Returns the object identifier (id) of the specified object asynchronously.
        /// </summary>
        /// <exception cref="Flurl.Http.FlurlHttpException"></exception>
        /// <exception cref="MetasysGuidException"></exception>
        public async Task<Guid> GetObjectIdentifierAsync(string itemReference)
        {
            var response = await Client.Request("objectIdentifiers")
                .SetQueryParam("fqr", itemReference)
                .GetJsonAsync<JToken>()
                .ConfigureAwait(false);

            string str = null;
            try
            {
                str = response.Value<string>();
                var id = new Guid(str);
                return id;
            }
            catch (System.ArgumentNullException)
            {
                throw new MetasysGuidException("Argument Null");
            }
            catch (System.ArgumentException)
            {
                throw new MetasysGuidException("Bad Argument", str);
            }
        }

        /// <summary>
        /// Read one attribute value given the Guid of the object.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="attributeName"></param>
        public Variant ReadProperty(Guid id, string attributeName)
        {
            return ReadPropertyAsync(id, attributeName).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Read one attribute value given the Guid of the object asynchronously.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="attributeName"></param>
        /// <exception cref="Flurl.Http.FlurlHttpException"></exception>
        /// <exception cref="System.NullReferenceException"></exception>
        public async Task<Variant> ReadPropertyAsync(Guid id, string attributeName)
        {
            var response = await Client.Request(new Url("objects")
                .AppendPathSegments(id, "attributes", attributeName))
                .GetJsonAsync<JToken>()
                .ConfigureAwait(false);

            try
            {
                var attribute = response["item"][attributeName];
                return new Variant(id, attribute, attributeName, Culture);
            }
            catch (System.NullReferenceException)
            {
                throw new MetasysPropertyException(response.ToString());
            }
        }

        /// <summary>
        /// Read many attribute values given the Guids of the objects.
        /// </summary>
        /// <param name="ids"></param>
        /// <param name="attributeNames"></param>
        public IEnumerable<VariantMultiple> ReadPropertyMultiple(IEnumerable<Guid> ids,
            IEnumerable<string> attributeNames)
        {
            return ReadPropertyMultipleAsync(ids, attributeNames).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Read many attribute values given the Guids of the objects asynchronously.
        /// </summary>
        /// <param name="ids"></param>
        /// <param name="attributeNames"></param>
        public async Task<IEnumerable<VariantMultiple>> ReadPropertyMultipleAsync(IEnumerable<Guid> ids,
            IEnumerable<string> attributeNames)
        {
            if (ids == null || attributeNames == null)
            {
                return null;
            }
            List<VariantMultiple> results = new List<VariantMultiple>();
            var taskList = new List<Task<Variant>>();
            // Prepare Tasks to Read attributes list. In Metasys 11 this will be implemented server side
            foreach (var id in ids)
            {
                foreach (string attributeName in attributeNames)
                {
                    // Much faster reading single property than the entire object, even though we have more server calls
                    taskList.Add(ReadPropertyAsync(id, attributeName));
                }
            }

            try 
            {
                await Task.WhenAll(taskList).ConfigureAwait(false);
            }
            catch (Exception e) 
            {
                // Do not throw exceptions
                await LogErrorAsync(e.Message).ConfigureAwait(false);
            }

            foreach (var id in ids)
            {
                // Get attributes of the specific Id
                List<Task<Variant>> attributeList = taskList.Where(w => w.Result.Id == id).ToList();
                List<Variant> variants = new List<Variant>();
                foreach (var t in attributeList)
                {
                    if (t.Status != TaskStatus.Faulted)
                    {
                        variants.Add(t.Result); // Prepare variants list
                    }
                }
                // Aggregate results
                results.Add(new VariantMultiple(id, variants));
            }
            return results.AsEnumerable();
        }

        /// <summary>
        /// Read entire object given the Guid of the object asynchronously.
        /// </summary>
        /// <param name="id"></param>
        /// <exception cref="Flurl.Http.FlurlHttpException"></exception>
        private async Task<(Guid Id, JToken Token)> ReadObjectAsync(Guid id)
        {
            var response = await Client.Request(new Url("objects")
                .AppendPathSegment(id))
                .GetJsonAsync<JToken>()
                .ConfigureAwait(false);
            return (Id: id, Token: response);
        }
    }
}
