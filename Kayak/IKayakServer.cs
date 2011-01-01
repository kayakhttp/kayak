using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Kayak
{
    public interface IKayakServer
    {
        Action<Action<ISocket>> GetConnection();
    }
}
