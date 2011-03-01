
namespace Kayak.Http
{
    public static partial class Extensions
    {
        public static bool GetIsContinueExpected(this IRequest request)
        {
            return (request.Version.Major == 1 && request.Version.Minor == 1) &&
                                request.Headers.GetHeader("expect") == "100-continue";
        }

        public static bool GetIsContinueProhibited(this IRequest request)
        {
            return (request.Version.Major == 1 && request.Version.Minor == 0) ||
                                request.Headers.GetHeader("expect") != "100-continue";
        }
    }
}
