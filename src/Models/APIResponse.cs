using System.Net;

namespace AJP.ElasticBand.API.Models
{
    public class APIResponse
    {
        public string Request { get; set; }
        public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;
        public bool Ok { get; set; }
        public string Result { get; set; }
        public string Error { get; set; }
        public object Data { get; set; }

        public static APIResponse NotOk(string request, string error, HttpStatusCode statusCode, object data = null)
        {
            return new APIResponse
            {
                Request = request,
                Error = error,
                Ok = false,
                StatusCode = statusCode,
                Result = "Failed",
                Data = data
            };
        }
    }
}
