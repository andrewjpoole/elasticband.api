using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Security;
using System.Threading.Tasks;

namespace AJP.ElasticBand.API
{
    public class DefaultHttpClientFactory : IHttpClientFactory
    {
        private string _thumbprintOfCertToSkipVerify = "";

        public DefaultHttpClientFactory(IConfiguration config)
        {
            _thumbprintOfCertToSkipVerify = config["thumbprintOfCertToSkipVerify"];
        }

        public HttpClient CreateClient(string name)
        {
            var httpClientHandler = new HttpClientHandler();
            httpClientHandler.ServerCertificateCustomValidationCallback = (message, cert, chain, sslPolicyErrors) => {
                if (sslPolicyErrors == SslPolicyErrors.None)
                {
                    return true;   //Is valid
                }

                if (!string.IsNullOrEmpty(_thumbprintOfCertToSkipVerify) &&  cert.GetCertHashString().ToLower() == _thumbprintOfCertToSkipVerify)
                {
                    return true;
                }
                return false;
            };

            return new HttpClient(httpClientHandler);
        }
    }
}
