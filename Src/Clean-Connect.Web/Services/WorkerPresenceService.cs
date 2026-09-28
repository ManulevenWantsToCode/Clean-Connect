using System.Collections.Concurrent;

namespace Clean_Connect.Web.Services
{
    public interface IWorkerPresenceService
    {
        void MarkOnline(string email);

        void MarkOffline(string email);

        bool IsOnline(string email);
    }

    public sealed class WorkerPresenceService : IWorkerPresenceService
    {
        private readonly ConcurrentDictionary<string, int> _connections = new(StringComparer.OrdinalIgnoreCase);

        public void MarkOnline(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return;

            _connections.AddOrUpdate(email.Trim(), 1, (_, count) => count + 1);
        }

        public void MarkOffline(string email)
        {
            if (string.IsNullOrWhiteSpace(email)) return;

            var normalized = email.Trim();

            if (_connections.TryGetValue(normalized, out var count))
            {
                if (count <= 1)
                {
                    _connections.TryRemove(normalized, out _);
                }
                else
                {
                    _connections.TryUpdate(normalized, count - 1, count);
                }
            }
        }

        public bool IsOnline(string email) => !string.IsNullOrWhiteSpace(email) && _connections.ContainsKey(email.Trim());
    }
}