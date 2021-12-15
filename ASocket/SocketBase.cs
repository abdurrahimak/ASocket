using System;
namespace ASocket
{
    public abstract class SocketBase
    {
        private int _dispatcherId;
        private bool _hasUpdater = false;
        private ISocketUpdater _socketUpdater;
        
        internal SocketBase(ISocketUpdater socketUpdater)
        {
            _dispatcherId = MainThreadDispatcher.CreateDispatcherId();
            MainThreadDispatcher.RegisterDispatcher(_dispatcherId);

            if (socketUpdater != null)
            {
                _hasUpdater = true;
                _socketUpdater = socketUpdater;
                _socketUpdater.LoopEvent += OnLoop;
            }
        }

        protected void AddDispatcherQueue(Action action)
        {
            if (_hasUpdater)
            {
                MainThreadDispatcher.Enqueue(_dispatcherId, action);
            }
            else
            {
                action.Invoke();
            }
        }

        public virtual void Destroy()
        {
            if (_hasUpdater)
            {
                _socketUpdater.LoopEvent -= OnLoop;
            }
            MainThreadDispatcher.UnregisterDispatcher(_dispatcherId);
        }
        
        private void OnLoop()
        {
            MainThreadDispatcher.Update(_dispatcherId);
        }
    }
}
