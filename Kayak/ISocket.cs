using System;
using System.Net;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Kayak
{
    public interface IServer
    {
        IServerDelegate Delegate { get; set; }

        void Listen(IPEndPoint ep);
        IPEndPoint ListenEndPoint { get; }
        void Close();
    }

    public interface IServerDelegate
    {
        void OnConnection(IServer server, ISocket socket);
        void OnClose(IServer server);
    }

    public interface ISocket : IDisposable
    {
        ISocketDelegate Delegate { get; set; }

        IPEndPoint RemoteEndPoint { get; }
        void SetNoDelay(bool noDelay);
        //void SetKeepAlive(bool keepAlive, int delay);

        //void Connect(IPEndPoint ep, Action callback);
        bool Write(ArraySegment<byte> data, Action continuation);
        void End(); // send FIN
    }

    public interface ISocketDelegate
    {
        void OnConnected(ISocket socket);
        bool OnData(ISocket socket, ArraySegment<byte> data, Action continuation);
        void OnEnd(ISocket socket); // received FIN
        void OnTimeout(ISocket socket);
        void OnError(ISocket socket, Exception e);
        void OnClose(ISocket socket);
    }

    public interface IStream : IDisposable
    {
        IStreamDelegate Delegate { get; set; }

        bool CanRead { get; }
        bool CanWrite { get; }

        void End(); 
        bool Write(ArraySegment<byte> data, Action continuation);
    }

    public interface IStreamDelegate
    {
        bool OnData(IStream stream, ArraySegment<byte> data, Action continuation);
        void OnEnd(IStream stream);
        void OnError(IStream stream, Exception exception);
        void OnClose(IStream stream);
    }
}
