using System;
using System.Threading.Tasks;
using Owin;

namespace Kayak.Http.Extensions
{
    public static partial class Extensions
    {
        public static Task<int> ReadBodyAsync(this IRequest request, byte[] buffer, int offset, int count)
        {
            var tcs = new TaskCompletionSource<int>();

            request.BeginReadBody(buffer, offset, count, iasr =>
            {
                Console.WriteLine("Got asynccallback");
                try
                {
                    tcs.SetResult(request.EndReadBody(iasr));
                }
                catch (Exception e)
                {
                    tcs.SetException(e);
                }
            }, null);

            return tcs.Task;
        }
    }
}
