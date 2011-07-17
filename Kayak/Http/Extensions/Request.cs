
namespace Kayak.Http
{
    public static partial class Extensions
    {
        public static bool IsContinueExpected(this HttpRequestHead request)
        {
            return 
                request.Version != null && 
                request.Version.Major == 1 && 
                request.Version.Minor == 1 &&
                request.Headers != null && 
                request.Headers.ContainsKey("expect") && 
                request.Headers["expect"] == "100-continue";
        }

        static bool IsContinueProhibited(this HttpRequestHead request)
        {
            return (request.Version != null && request.Version.Major == 1 && request.Version.Minor == 0) ||
                !request.Headers.ContainsKey("expect") || request.Headers["expect"] != "100-continue";
        }

        public static bool HasBody(this HttpRequestHead request)
        {
            return request.Headers.ContainsKey("Content-Length") ||
                (request.Headers.ContainsKey("Transfer-Encoding") && request.Headers["Transfer-Encoding"] == "chunked");
        }
    }
}
