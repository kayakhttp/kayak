using System;

namespace Kayak
{
    public interface IKayakServer
    {
        IDisposable Start();
        Action<Action<ISocket>> GetConnection();
    }
}
