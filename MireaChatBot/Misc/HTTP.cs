using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace MireaChatBot.Misc
{
    internal static class HTTPClient
    {
        public static string GetString(string url)
        {
            var response = sendRequest(url);
            if (!checkStatusCode(response.StatusCode)) return null;
            var result = response.Content.ReadAsStringAsync().Result;
            return result;
        }

        public static byte[] GetBytes(string url)
        {
            var response = sendRequest(url);
            if (!checkStatusCode(response.StatusCode)) return null;
            var result = response.Content.ReadAsByteArrayAsync().Result;
            return result;
        }

        private static HttpResponseMessage sendRequest(string url)
        {
            var client = new HttpClient();
            var response = client.SendAsync(new HttpRequestMessage(HttpMethod.Get, url)).Result;
            return response;
        }

        private static bool checkStatusCode(HttpStatusCode code)
        {
            return code == HttpStatusCode.OK;
        }
    }
}
