using System;
using System.Collections.Generic;
namespace ASocket
{
    internal static class MainThreadDispatcher
    {
        private static readonly Dictionary<int, Queue<Action>> _actionQueueById = new Dictionary<int, Queue<Action>>();

        public static void RegisterDispatcher(int id)
        {
            _actionQueueById.Add(id, new Queue<Action>());
        }
        public static void UnregisterDispatcher(int id)
        {
            _actionQueueById.Remove(id);
        }
        public static int CreateDispatcherId()
        {
            return Guid.NewGuid().GetHashCode();
        }

        public static void Enqueue(int dispatcherId, Action action)
        {
            _actionQueueById[dispatcherId].Enqueue(action);
        }

        private static Queue<Action> GetActionQueue(int dispatcherId)
        {
            return _actionQueueById[dispatcherId];
        }

        public static void Update(int dispatcherId)
        {
            var queue = GetActionQueue(dispatcherId);
            while (queue.Count > 0)
            {
                queue.Dequeue().Invoke();
            }
        }
    }
}
