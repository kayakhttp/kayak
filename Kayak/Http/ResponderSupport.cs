using System;
using System.Linq;
using Owin;

namespace Kayak
{
    public interface IHttpResponderSupport
    {
        IObservable<IResponse> GetResponse(IRequest request, IApplication responder);
    }

    public class HttpResponderSupport : IHttpResponderSupport
    {
        public IObservable<IResponse> GetResponse(IRequest request, IApplication responder)
        {
            object responseObj = null;

            responseObj = responder.Respond(request);

            var observable = responseObj.AsObservable();

            if (observable != null)
                return observable.Select(o => AsResponse(o));

            return new IResponse[] { AsResponse(responseObj) }.ToObservable();
        }

        public virtual IResponse AsResponse(object responseObj)
        {
            IResponse response = null;

            if (responseObj is IResponse)
                response = responseObj as IResponse;
            else if (responseObj is object[])
                response = (responseObj as object[]).ToResponse();
            else
                throw new ArgumentException("cannot convert argument " + responseObj.ToString() + " to IHttpServerResponse");

            return response;
        }
    }
}
