using ApiGateway.Dtos;
using System.Text.Json;

namespace ApiGateway.Services
{

    public sealed class RedisSessionStore : ISessionStore
    {
        private static TimeSpan _ttl = TimeSpan.FromDays(365);
        private readonly IDatabase _db;
        private static string Key(string id) => $"dads:session:{id}";
        private static string UserIndex(string user) => $"dads:user:{user}:sessions";

        public RedisSessionStore(IConnectionMultiplexer connection)
        {
            _db = connection.GetDatabase();
        }

        public Task<bool> ExistsAsync(string id) => _db.KeyExistsAsync(id);

        public async Task<IEnumerable<SessionData>> GetAllAsync(string user)
        {
            var ids = await _db.SetMembersAsync(UserIndex(user));
            var sessions = new List<SessionData>();
            foreach (var id in ids)
            {
                var s = await GetAsync(id);
                if (s != null)
                    sessions.Add(s);
                else
                    await _db.SetRemoveAsync(UserIndex(user), id);
            }

            return sessions.OrderByDescending(a => a.LastAccessAt).AsEnumerable();
        }


        public async Task<SessionData?> GetAsync(string id)
        {
            var raw = await _db.StringGetAsync(Key(id));
            return raw.IsNullOrEmpty ? null : JsonSerializer.Deserialize<SessionData>((string)raw!);
        }

        public async Task SetASync(SessionData sessionData)
        {
            await _db.StringSetAsync(Key(sessionData.Id), JsonSerializer.Serialize(sessionData), _ttl);
            if (!string.IsNullOrEmpty(sessionData.User))
                await _db.SetAddAsync(UserIndex(sessionData.User), sessionData.Id);
        }

        public async Task DeleteAsync(string id)
        {
            var s = await GetAsync(id);
            await _db.KeyDeleteAsync(Key(id));
            if (!s?.User.IsWhiteSpace() ?? false)
                await _db.SetRemoveAsync(UserIndex(s!.User!), id);
        }
    }
}
