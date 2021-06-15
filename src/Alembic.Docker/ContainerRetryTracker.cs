using System.Collections.Concurrent;

namespace Alembic.Docker
{
    public interface IContainerRetryTracker
    {
        int GetRetryCount(string id);

        void Add(string id);

        void Remove(string id);
    }

    public class ContainerRetryTracker : IContainerRetryTracker
    {
        private readonly ConcurrentDictionary<string, int> _containerRetries = new ConcurrentDictionary<string, int>();

        public void Add(string id)
        {
            if (!_containerRetries.TryGetValue(id, out var count))
                _containerRetries.TryAdd(id, 1);
            else
                _containerRetries.TryUpdate(id, count + 1, count);
        }

        public void Remove(string id)
        {
            _containerRetries.TryRemove(id, out _);
        }

        public int GetRetryCount(string id)
        {
            if (!_containerRetries.ContainsKey(id))
                return 0;

            _containerRetries.TryGetValue(id, out var count);

            return count;
        }
    }
}
