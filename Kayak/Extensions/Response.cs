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

            if (!(responseArray[0] is string))
                throw new ArgumentException("response status is not string");

            var status = responseArray[0] as string;

            var spaceSplit = status.Split(new char[] { ' ' }, 2);

            if (spaceSplit.Length != 2)
                throw new ArgumentException("status must contain a space.");

            int statusCode = 0;

            if (!int.TryParse(spaceSplit[0], out statusCode))
                throw new ArgumentException("first token in status is not an integer");

            if (!(responseArray[1] is IDictionary<string, string>))
                throw new ArgumentException("response headers is not IDictionary<string, string>");

            var headers = responseArray[1] as IDictionary<string, string>;

            IEnumerable<object> body = null;

            if (responseArray.Length == 3)
            {
                var bodyObj = responseArray[2];

                if (!(bodyObj is IEnumerable<object>))
                    throw new ArgumentException("Third object in response array is not IEnumerable<object>.");

                body = bodyObj as IEnumerable<object>;
            }
                
            return new KayakResponse(status, headers, body);
        }
        
    }
}
