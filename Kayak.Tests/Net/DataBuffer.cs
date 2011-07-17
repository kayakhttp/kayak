using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kayak.Tests.Net
{
    public class DataBuffer
    {
        List<byte[]> buffer = new List<byte[]>();

        public string GetString()
        {
            return buffer.Aggregate("", (acc, next) => acc + Encoding.UTF8.GetString(next));
        }

        public int GetCount()
        {
            return buffer.Aggregate(0, (c, d) => c + d.Length);
        }

        public void Add(ArraySegment<byte> d)
        {
            byte[] b = new byte[d.Count];
            System.Buffer.BlockCopy(d.Array, d.Offset, b, 0, d.Count);
            buffer.Add(b);
        }

        public void Each(Action<byte[]> each)
        {
            buffer.ForEach(each);
        }
    }
}
