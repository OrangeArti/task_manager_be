using Microsoft.Extensions.Configuration;

namespace TaskManager.Shared.Security;

public static class JwtConfigurationExtensions
{
    public static JwtOptions GetJwtOptions(this IConfiguration configuration, string sectionName = JwtOptions.SectionName)
    {
        var options = configuration.GetSection(sectionName).Get<JwtOptions>();
        return options ?? new JwtOptions();
    }
}

