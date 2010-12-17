using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace Kayak
{
    public static partial class Extensions
    {
        public static Task WriteObject(this ISocket socket, object obj)
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
                else if (obj is byte[])
                    chunk = new ArraySegment<byte>(obj as byte[]);
                else
                    throw new ArgumentException("Invalid object of type " + obj.GetType() + " '" + obj.ToString() + "'");

                return socket.WriteAll(chunk);
            }
        }

        public static Task WriteAll(this ISocket socket, ArraySegment<byte> chunk)
        {
            return socket.WriteAllInternal(chunk).AsCoroutine<Unit>();
        }

        static IEnumerable<object> WriteAllInternal(this ISocket socket, ArraySegment<byte> chunk)
        {
            int bytesWritten = 0;

            while (bytesWritten < chunk.Count)
            {
                var write = socket.Write(chunk.Array, chunk.Offset + bytesWritten, chunk.Count - bytesWritten);
                yield return write;

                if (write.Exception != null)
                    throw write.Exception;

                bytesWritten += write.Result;
            }
        }
    }
}
