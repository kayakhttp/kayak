using System;
using System.Collections.Generic;

namespace Kayak
{
    class OutputBuffer
    {
        public List<ArraySegment<byte>> Data;
        public int Size;

        public OutputBuffer()
        {
            Data = new List<ArraySegment<byte>>();
        }

        public void Add(ArraySegment<byte> data)
        {
            var d = new byte[data.Count];
            Buffer.BlockCopy(data.Array, data.Offset, d, 0, d.Length);

            Size += data.Count;
            Data.Add(new ArraySegment<byte>(d));
        }

        public void Remove(int howmuch)
        {
            if (howmuch > Size) throw new ArgumentOutOfRangeException("howmuch > size");

            Size -= howmuch;

            int remaining = howmuch;

            while (remaining > 0)
            {
                var first = Data[0];

                int count = first.Count;
                if (count <= remaining)
                {
                    remaining -= count;
                    Data.RemoveAt(0);
                }
                else
                {
                    Data[0] = new ArraySegment<byte>(first.Array, first.Offset + remaining, count - remaining);
                    remaining = 0;
                }
            }
        }
    }
}
