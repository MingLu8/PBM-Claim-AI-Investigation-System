namespace ApiGateway.Extensions
{
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Options;

    public static class DependencyInjectionExtensions
    {
        public static T AddAppSettings<T>(
            this IServiceCollection services,
            IConfiguration configuration,
            string sectionName) where T : class, new()
        {
            // 1. Get the configuration section
            var section = configuration.GetSection(sectionName);

            // 2. Register IOptions<T>, IOptionsSnapshot<T>, etc., into DI
            services.Configure<T>(section);

            // 3. Bind and construct the concrete instance to return immediately
            var settings = new T();
            section.Bind(settings);

            // 4. Register the concrete instance directly for services that skip IOptions<T>
            services.AddSingleton(settings);

            return settings;
        }
    }
}
