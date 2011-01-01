using System;

namespace Kayak
{
    public interface IKayakServer
    {
        Action<Action<ISocket>> GetConnection();
    }
}
