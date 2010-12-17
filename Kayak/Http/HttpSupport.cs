using System;
using System.Collections.Generic;
using System.Linq;
using Owin;
using System.Threading.Tasks;

namespace Kayak
{
    public interface IHttpSupport
    {
        Task<IRequest> BeginRequest(ISocket socket);
        Task BeginResponse(ISocket socket, IResponse response);
    }

    class HttpSupport : IHttpSupport
    {
        public Task<IRequest> BeginRequest(ISocket socket)
        {
            return socket.BufferHeaders().ContinueWith(t => KayakRequest.CreateRequest(socket, t.Result));
        }

        public Task BeginResponse(ISocket socket, IResponse response)
        {
            return socket.WriteAll(new ArraySegment<byte>(response.WriteStatusLineAndHeaders()));
        }
    }
}
