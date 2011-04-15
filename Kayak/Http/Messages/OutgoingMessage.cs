using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kayak.Http
{
    interface IOutputStreamDelegate
    {
        void OnFlushed(IOutputStream message);
    }

    interface IOutputStream
    {
        void Attach(ISocket socket);
        void Detach(ISocket socket);
        bool Write(ArraySegment<byte> data, Action continuation);
        void End();
    }
}
