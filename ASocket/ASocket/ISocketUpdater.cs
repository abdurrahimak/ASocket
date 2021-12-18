using System;
namespace ASocket
{
    public interface ISocketUpdater
    {
        public event Action LoopEvent;
    }
}
