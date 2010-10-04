using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Kayak.Core;
using System.IO;

namespace Kayak
{
    public static partial class Extensions
    {
        public static IHttpServerResponse ToResponse(this object[] responseArray)
        {
            if (responseArray.Length != 2 && responseArray.Length != 3)
                throw new ArgumentException("response array is not of valid length.");

            if (!(responseArray[0] is int))
                throw new ArgumentException("response status is not int");

            var statusCode = (int)responseArray[0];

            var statusLine = new HttpStatusLine
            {
                StatusCode = statusCode,
                ReasonPhrase = HttpStatusLine.PhraseForCode(statusCode),
                HttpVersion = "HTTP/1.0"
            };

            if (!(responseArray[1] is IDictionary<string, string>))
                throw new ArgumentException("response headers is not IDictionary<string, string>");

            var headers = responseArray[1] as IDictionary<string, string>;
            

            if (responseArray.Length == 2)
                return new KayakResponse(statusLine, headers);

            var body = responseArray[2];

            if (body is FileInfo)
                return new KayakResponse(statusLine, headers, (body as FileInfo).Name);

            var generator = body as IEnumerable<IObservable<ArraySegment<byte>>>;
            
            if (generator == null) 
                generator = AsGenerator(body);

            if (generator != null)
                return new KayakResponse(statusLine, headers, generator);

            throw new ArgumentException("Third object in response array could not be interpreted as a response body generator.");
        }

        public static IEnumerable<IObservable<ArraySegment<byte>>> AsGenerator(object obj)
        {
            if (obj is string)
                yield return new ArraySegment<byte>[] { new ArraySegment<byte>(Encoding.UTF8.GetBytes(obj as string)) }.ToObservable();
        }
        
    }
}
