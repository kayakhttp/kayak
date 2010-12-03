using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Owin;
using System.Text;

namespace Kayak
{
    public static partial class Extensions
    {
        public static IObservable<Unit> WriteObject(this ISocket socket, object obj)
        {
            if (obj is FileInfo)
            {
                var fileInfo = obj as FileInfo;
                return socket.WriteFile(fileInfo.Name);
            }
            else
            {
                var chunk = default(ArraySegment<byte>);

                if (obj is ArraySegment<byte>)
                    chunk = (ArraySegment<byte>)obj;
                else if (obj is string)
                    chunk = new ArraySegment<byte>(Encoding.UTF8.GetBytes(obj as string));
                else
                    throw new ArgumentException("Invalid object of type " + obj.GetType() + " '" + obj.ToString() + "'");

                return socket.WriteAll(chunk);
            }
        }
        public static IObservable<Unit> WriteAll(this ISocket socket, ArraySegment<byte> chunk)
        {
            return socket.WriteAllInternal(chunk).AsCoroutine<Unit>();
        }

        static IEnumerable<object> WriteAllInternal(this ISocket socket, ArraySegment<byte> chunk)
        {
            int bytesWritten = 0;

            while (bytesWritten < chunk.Count)
            {
                yield return socket.Write(chunk.Array, chunk.Offset + bytesWritten, chunk.Count - bytesWritten)
                    .Do(n => bytesWritten += n);
            }
        }
    }
}
