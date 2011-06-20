using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kayak.Tests.Net
{
    class DataBuffer
    {
        public List<byte[]> Buffer = new List<byte[]>();

        public new string ToString()
        {
            return Buffer.Aggregate("", (acc, next) => acc + Encoding.UTF8.GetString(next));
        }

        public void AddToBuffer(ArraySegment<byte> d)
        {
            byte[] b = new byte[d.Count];
            System.Buffer.BlockCopy(d.Array, d.Offset, b, 0, d.Count);
            Buffer.Add(b);
        }
    }
}
