using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kayak.Oars
{
    interface IOarsBuffer
    {
        void Add(ArraySegment<byte> data);
        ArraySegment<byte> Remove();

        void Read(IntPtr fd, int count);
        void Write(IntPtr fd);
    }
}
