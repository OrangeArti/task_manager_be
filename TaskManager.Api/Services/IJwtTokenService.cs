using System.Collections.Generic;
using TaskManager.Api.Models;

namespace TaskManager.Api.Services
{
    public interface IJwtTokenService
    {
        string CreateToken(ApplicationUser user, IList<string> roles);
    }
}