using ApiGateway.Dtos;
using System.Security.Claims;

namespace ApiGateway.Services
{
    public interface ISessionStore
    {
        Task<SessionData?> GetAsync(string id);
        Task DeleteAsync(string id);
        Task<bool> ExistsAsync(string id);
        Task SetASync(SessionData sessionData);
        Task<IEnumerable<SessionData>> GetAllAsync(string user);
    }

    public record UserGroups(bool User, bool Admin)
    {
        public bool HasAnyAccess => User || Admin;
    }

    public sealed class CurrentUser : ICurrentUser
    {
        private readonly ClaimsPrincipal _principal;
        public CurrentUser(IHttpContextAccessor http)
        {
            _principal = http.HttpContext?.User ?? new ClaimsPrincipal(new ClaimsIdentity());
        }

        public bool IsAuthenticated => _principal.Identity?.IsAuthenticated ?? false;
        public string? Sub => _principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? _principal.FindFirstValue("sub");
        public string Email => _principal.FindFirstValue(ClaimTypes.Email) ?? _principal.FindFirstValue("email") ?? string.Empty;
        public string Name => _principal.FindFirstValue("name") ?? _principal.FindFirstValue(ClaimTypes.Name) ?? Email;
        public string UserId => Sub ?? Email;
        public UserGroups Groups
        {
            get
            {
                var g = _principal.FindAll("groups").Select(a => a.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
                return new UserGroups(g.Contains("user"), g.Contains("admin"));
            }
        }
    }
}
