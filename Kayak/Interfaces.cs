using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

namespace Kayak
{
    /// <summary>
    /// Represents a socket which supports asynchronous IO operations.
    /// </summary>
    public interface ISocket : IDisposable
    {
        IPEndPoint RemoteEndPoint { get; }
        IObservable<int> Write(byte[] buffer, int offset, int count);
        IObservable<int> WriteFile(string file, int offset, int count);
        IObservable<int> Read(byte[] buffer, int offset, int count);
    }

    public interface IKayakContext
    {
        ISocket Socket { get; }
        IKayakServerRequest Request { get; }
        IKayakServerResponse Response { get; }
        IDictionary<object, object> Items { get; }
    }

    public interface IKayakServerRequest
    {
        string Verb { get; }
        string RequestUri { get; }
        string HttpVersion { get; }
        IDictionary<string, string> Headers { get; }
        RequestStream Body { get; }

        #region Derived properties

        string Path { get; }
        IDictionary<string, string> QueryString { get; }

        #endregion
    }

    public interface IKayakServerResponse
    {
        int StatusCode { get; set; }
        string ReasonPhrase { get; set; }
        string HttpVersion { get; set; }
        IDictionary<string, string> Headers { get; set; }
        ResponseStream Body { get; }
    }
}
