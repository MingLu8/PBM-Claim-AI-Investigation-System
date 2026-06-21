using ApiGateway.Dtos;
using System.Collections.Concurrent;

namespace ApiGateway.Services
{
    public sealed class InMemorySessionStore : ISessionStore
    {
        private readonly ConcurrentDictionary<string, SessionData> _store = new();
        public Task<bool> ExistsAsync(string id) => Task.FromResult(_store.ContainsKey(id));
        public Task<IEnumerable<SessionData>> GetAllAsync(string user)
        {
            var sessions = _store.Values
                .Where(a => a.User == user)
                .OrderByDescending(a => a.LastAccessAt)
                .AsEnumerable();

            return Task.FromResult(sessions);
        }

        public Task<SessionData?> GetAsync(string id)
            => Task.FromResult(_store.TryGetValue(id, out var sessionData) ? sessionData : null);

        public Task SetASync(SessionData sessionData)
        {
            _store[sessionData.Id] = sessionData;
            return Task.CompletedTask;
        }
    }
}
