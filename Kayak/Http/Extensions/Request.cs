using System;
using System.Threading.Tasks;
using Owin;

namespace Kayak
{
    public static partial class Extensions
    {
        public static Task<int> ReadBodyAsync(this IRequest request, byte[] buffer, int offset, int count)
        {
            var tcs = new TaskCompletionSource<int>();

            request.BeginReadBody(buffer, offset, count, iasr =>
            {
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
