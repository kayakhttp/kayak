using System;
using System.Net.Sockets;

namespace Kayak
{
    public static partial class Extensions
    {
        internal static IObservable<int> SendAsync(this Socket s, byte[] buffer, int offset, int count)
        {
            return new AsyncOperation<int>(
                (c, st) => s.BeginSend(buffer, offset, count, SocketFlags.None, c, st),
                iasr => { return s.EndSend(iasr); });
        }

        internal static IObservable<int> SendFileAsync(this Socket s, string file)
        {
            return new AsyncOperation<int>(
                (c, st) => s.BeginSendFile(file, c, st),
                iasr => { s.EndSendFile(iasr); return 0; });
        }

        internal static IObservable<int> ReceiveAsync(this Socket s, byte[] buffer, int offset, int count)
        {
            return new AsyncOperation<int>(
                (c, st) => s.BeginReceive(buffer, offset, count, SocketFlags.None, c, st),
                iasr => { return s.EndReceive(iasr); });
        }

        internal static IObservable<Socket> AcceptSocketAsync(this TcpListener listener)
        {
            return new AsyncOperation<Socket>(
                (c, st) => listener.BeginAcceptSocket(c, st),
                iasr => listener.EndAcceptSocket(iasr));
        }
    }
}
