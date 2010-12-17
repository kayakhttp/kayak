using System;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace Kayak
{
    public static partial class Extensions
    {
        internal static Task<int> SendAsync(this Socket s, byte[] buffer, int offset, int count)
        {
            var tcs = new TaskCompletionSource<int>();

            s.BeginSend(buffer, offset, count, SocketFlags.None, iasr =>
            {
                try
                {
                    tcs.SetResult(s.EndSend(iasr));
                }
                catch (Exception e)
                {
                    tcs.SetException(e);
                }
            }, null);

            return tcs.Task;
        }

        internal static Task SendFileAsync(this Socket s, string file)
        {
            var tcs = new TaskCompletionSource<int>();

            s.BeginSendFile(file, iasr =>
            {
                try
                {
                    s.EndSendFile(iasr);
                }
                catch (Exception e)
                {
                    tcs.SetException(e);
                }
            }, null);

            return tcs.Task;
        }

        internal static Task<int> ReceiveAsync(this Socket s, byte[] buffer, int offset, int count)
        {
            var tcs = new TaskCompletionSource<int>();

            s.BeginReceive(buffer, offset, count, SocketFlags.None, iasr =>
                {
                    try
                    {
                        tcs.SetResult(s.EndReceive(iasr));
                    }
                    catch (Exception e)
                    {
                        tcs.SetException(e);
                    }
                }, null);

            return tcs.Task;
        }

        internal static Task<Socket> AcceptSocketAsync(this TcpListener listener)
        {
            var tcs = new TaskCompletionSource<Socket>();

            listener.BeginAcceptSocket(iasr =>
                {
                    try
                    {
                        tcs.SetResult(listener.EndAcceptSocket(iasr));
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
