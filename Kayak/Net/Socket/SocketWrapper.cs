using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace Kayak
{
    // need to be able to get underneath the KayakSocket class for testing purposes.
    // kinda hanky, but should be able to swap it for a raw socket in the release build
    // using preprocessor macros...
    interface ISocketWrapper : IDisposable
    {
        IAsyncResult BeginConnect(IPEndPoint ep, AsyncCallback callback);
        void EndConnect(IAsyncResult iasr);

        IAsyncResult BeginReceive(byte[] buffer, int offset, int count, AsyncCallback callback);
        int EndReceive(IAsyncResult iasr);

        IAsyncResult BeginSend(List<ArraySegment<byte>> data, AsyncCallback callback);
        int EndSend(IAsyncResult iasr);

        void Shutdown();
    }

    class SocketWrapper : ISocketWrapper
    {
        Socket socket;

        public SocketWrapper(AddressFamily af)
            : this(new Socket(af, SocketType.Stream, ProtocolType.Tcp)) { }

        public SocketWrapper(Socket socket)
        {
            this.socket = socket;
        }


        // perhaps a bit heavy-handed but no mono that can compile 4.0 .net works right anyway
        static bool syncConnect = Environment.OSVersion.Platform == PlatformID.Unix;

        Action<IPEndPoint> pendingConnect;
        public IAsyncResult BeginConnect(IPEndPoint ep, AsyncCallback callback)
        {
            if (syncConnect)
            {
                // voila, BeginConnect est borken avec mono 2.8-2.10. rad.
                // whatever it's probably implemented on a native threadpool anyway.
                pendingConnect = socket.Connect;
                return pendingConnect.BeginInvoke(ep, callback, null);
            }
            else
                return socket.BeginConnect(ep, callback, null);
        }

        public void EndConnect(IAsyncResult iasr)
        {
            if (syncConnect)
            {
                pendingConnect.EndInvoke(iasr);
            }
            else
                socket.EndConnect(iasr);
        }

        public IAsyncResult BeginReceive(byte[] buffer, int offset, int count, AsyncCallback callback)
        {
            return socket.BeginReceive(buffer, offset, count, SocketFlags.None, callback, null);
        }

        public int EndReceive(IAsyncResult iasr)
        {
            return socket.EndReceive(iasr);
        }

        public IAsyncResult BeginSend(List<ArraySegment<byte>> data, AsyncCallback callback)
        {
            return socket.BeginSend(data, SocketFlags.None, callback, null);
        }

        public int EndSend(IAsyncResult iasr)
        {
            return socket.EndSend(iasr);
        }

        public void Shutdown()
        {
            socket.Shutdown(SocketShutdown.Send);
        }

        public void Dispose()
        {
            socket.Dispose();
        }
    }
}
