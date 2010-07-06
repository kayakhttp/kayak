using System;
using System.Net;
using System.Runtime.InteropServices;
using Oars;
using Oars.Core;
using System.IO;

namespace Kayak.Oars
{
    /// <summary>
    /// A TCP connection that supports an eventful network stream. Don't forget to call Dispose()!
    /// </summary>
    public class OarsSocket : ISocket, IDisposable
    {
        IntPtr socket;
        EventBase eventBase;
        EventStream stream;

        public IPEndPoint RemoteEndPoint { get; private set; }
        public bool IsClosed { get; private set; }

        internal OarsSocket(EventBase eventBase, IntPtr socket, IPEndPoint remoteEndPoint)
        {
            this.eventBase = eventBase;
            this.socket = socket;
            RemoteEndPoint = remoteEndPoint;
        }

        [DllImport("libc")]
        static extern int close(IntPtr fd);

        public void Dispose()
        {
            ThrowIfDisposed();
            //Console.WriteLine("Closing fd " + socket.ToInt32());
            close(socket);
            IsClosed = true;
        }

        /// <summary>
        /// Returns an EventStream schudeled on the EventBase of the server that generated the connection.
        /// Don't forget to dispose it!
        /// </summary>
        public Stream GetStream()
        {
            ThrowIfDisposed();

            if (stream == null)
                stream = new EventStream(eventBase, socket, FileAccess.ReadWrite);

            return stream;
        }

        void ThrowIfDisposed()
        {
            if (IsClosed)
                throw new ObjectDisposedException("Connection");
        }
    }
}
