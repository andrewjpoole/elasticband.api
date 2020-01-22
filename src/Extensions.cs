using Microsoft.AspNetCore.Http;

// From answer here https://stackoverflow.com/a/11308879

namespace System
{
    public static class ObjectExtensions
    {
        public static string ToRequestString(this HttpRequest request) 
        {
            return $"[{request.Method}]{request.Host}{request.Path}{request.QueryString}";
        }
    }
}
