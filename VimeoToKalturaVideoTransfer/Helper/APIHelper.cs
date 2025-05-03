using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace VimeoToKalturaVideoTransfer.Helper
{
    public class APIHelper
    {
        private readonly string Message;
        private readonly string DestinationURL;
        private readonly string Bearer;

        public APIHelper(string message, string destinationURL, string bearer = "")
        {
            this.Message = message;
            this.DestinationURL = destinationURL;
            this.Bearer = bearer;
        }

        public string SendWithBearer()
        {
            Dictionary<string, string> headers = new Dictionary<string, string>();
            headers.Add("Authorization", $"Bearer {Bearer}");
            headers.Add("Content-Type", "application/json");
            return Get(headers);
        }

        public string Get(Dictionary<string, string> headers = null)
        {
            string rtn = null;

            using (var httpClient = new HttpClient())
            {
                // default header values //
                if (headers != null && !headers.ContainsKey("Accept"))
                    httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
                // default header values //

                // add the head values that has been passed //
                if (headers != null && headers.Count > 0)
                    foreach (var header in headers)
                        httpClient.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
                // add the head values that has been passed //

                var received = httpClient.GetAsync(DestinationURL + (string.IsNullOrWhiteSpace(Message) ? string.Empty : "/?" + Message));
                rtn = received.Result.Content.ReadAsStringAsync().Result;
            }

            return rtn;
        }
    }
}
