using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;
using Oars;
using System.Runtime.InteropServices;

namespace Kayak.Oars
{
    class OarsSocket : ISocket
    {
        //public event EventHandler OnConnected;
        //public event EventHandler<DataEventArgs> OnData;
        //public event EventHandler OnEnd;
        //public event EventHandler OnTimeout;
        //public event EventHandler<ExceptionEventArgs> OnError;
        //public event EventHandler OnClose;

        public IPEndPoint RemoteEndPoint
        {
            get { throw new NotImplementedException(); }
        }

        public void SetNoDelay(bool noDelay)
        {
            throw new NotImplementedException();
        }

        public void Connect(IPEndPoint ep)
        {
            throw new NotImplementedException();
        }

        public bool Write(ArraySegment<byte> data, Action continuation)
        {
            throw new NotImplementedException();
        }

        public void End()
        {
            throw new NotImplementedException();
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }


        public OarsSocket(Event ev, IPEndPoint remoteEP, OarsServer server)
        {
        }
    }


    //class OarsSocket : ISocket
    //{
    //    [ThreadStatic]
    //    static byte[] inputBuffer;

    //    ArraySegment<byte> output;

    //    public IPEndPoint RemoteEndPoint { get; private set; }
    //    EVEvent ev;
    //    bool readEventPending, writeEventPending, writeTimeout;
        
    //    Action<ArraySegment<byte>> readCallback; Action writeCallback;
    //    Action<Exception> readFault, writeFault;

    //    public OarsSocket(EVEvent ev, IPEndPoint remoteEP, Action closed)
    //    {
    //        if (inputBuffer == null)
    //        {
    //            inputBuffer = new byte[4 * 1024];
    //        }

    //        this.ev = ev;
    //        ev.Activated = EventActivated;
    //        RemoteEndPoint = remoteEP;
    //    }

    //    void EventActivated()
    //    {
    //        bool timeout = (ev.Events & Events.EV_TIMEOUT) > 0;

    //        if ((ev.Events & Events.EV_READ) > 0)
    //        {
    //            if (readCallback == null)
    //            {
    //                readEventPending = true;
    //                return;
    //            }

    //            if (timeout)
    //            {
    //                var complete = readCallback;
    //                var fault = readFault;

    //                readCallback = null;
    //                readFault = null;

    //                fault(new Exception("Read timed out."));
    //                return;
    //            }

    //            DoRead();
    //        }

    //        if ((ev.Events & Events.EV_WRITE) > 0)
    //        {
    //            writeEventPending = true;

    //            if (timeout) writeTimeout = true;

    //            if (writeCallback == null)
    //            {
    //                return;
    //            }

    //            if (timeout)
    //            {
    //                var fault = writeFault;

    //                writeCallback = null;
    //                writeFault = null;
    //                output = default(ArraySegment<byte>);

    //                fault(new Exception("Write timed out."));
    //                return;
    //            }

    //            DoWrite(false);
    //        }
    //    }

    //    public void Write(ArraySegment<byte> data, Action complete, Action<Exception> fault)
    //    {
    //        if (writeCallback != null) throw new Exception("Already writing.");

    //        writeCallback = complete;
    //        writeFault = fault;
    //        output = data;

    //        if (writeEventPending)
    //        {
    //            if (writeTimeout)
    //            {
    //                writeEventPending = false;
    //                output = default(ArraySegment<byte>);
    //                writeCallback = null;
    //                writeFault = null;

    //                fault(new Exception("Write timed out."));
    //                return;
    //            }

    //            DoWrite(true);
    //        }
    //        else
    //        {
    //            var newbuf = new byte[output.Count];
    //            Buffer.BlockCopy(output.Array, output.Offset, newbuf, 0, output.Count);
    //            output = new ArraySegment<byte>(newbuf);
    //        }
    //    }

    //    public void Read(Action<ArraySegment<byte>> complete, Action<Exception> fault)
    //    {
    //        if (readCallback != null) throw new Exception("Already reading.");
    //        readCallback = complete;
    //        readFault = fault;

    //        if (readEventPending)
    //        {
    //            readEventPending = false;
    //            DoRead();
    //        }
    //    }

    //    void DoRead()
    //    {
    //        var complete = readCallback;
    //        var fault = readFault;
    //        readCallback = null;
    //        readFault = null;

    //        var bytesRead = ev.Socket.Recv(new ArraySegment<byte>(inputBuffer), 0);

    //        if (bytesRead == -1)
    //        {
    //            var errno = Marshal.GetLastWin32Error();
    //            fault(new Exception("Error while reading socket: " + errno));
    //        }
    //        else
    //            complete(new ArraySegment<byte>(inputBuffer, 0, bytesRead));
    //    }

    //    void DoWrite(bool copyUnwritten)
    //    {
    //        var bytesWritten = ev.Socket.Send(output, 0);

    //        if (bytesWritten == -1)
    //        {
    //            var errno = Marshal.GetLastWin32Error();

    //            if (errno != (int)Errno.EAGAIN)
    //            {
    //                var fault = writeFault;
    //                writeCallback = null;
    //                writeFault = null;
    //                fault(new Exception("Error while reading socket: " + errno));
    //                return;
    //            }
    //        }
    //        else if (bytesWritten == output.Count)
    //        {
    //            var complete = writeCallback;
    //            var fault = readFault;

    //            writeCallback = null;
    //            writeFault = null;
    //            output = default(ArraySegment<byte>);

    //            complete();
    //            return;
    //        }

    //        if (copyUnwritten)
    //        {
    //            var newbuf = new byte[output.Count - bytesWritten];
    //            Buffer.BlockCopy(output.Array, output.Offset, newbuf, 0, newbuf.Length);
    //            output = new ArraySegment<byte>(newbuf);
    //        }
    //        else
    //            output = new ArraySegment<byte>(output.Array, output.Offset + bytesWritten, output.Count - bytesWritten);
    //    }

    //    public void WriteFile(string file, Action complete, Action<Exception> fault)
    //    {
    //        throw new NotImplementedException();
    //    }

    //    public void Dispose()
    //    {
    //        throw new NotImplementedException();
    //    }
    //}

}
