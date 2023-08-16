using HttpContextMapper.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;

namespace HttpContextMapper.Extensions
{
    public static class ServiceExtensions
    {
        /// <summary>
        /// <inheritdoc cref="ConfigureForwardProxyOptions"/>
        /// <para/>
        /// <inheritdoc cref="RegisterDefaultHttpClientWithForwardProxy"/>
        /// </summary>

        public static void ConfigureAndRegisterDefaultHttpClientWithForwardProxy(this WebApplicationBuilder builder)
        {
            builder.ConfigureForwardProxyOptions();
            builder.Services.RegisterDefaultHttpClientWithForwardProxy();
        }

        /// <summary>
        /// Binds an instance of <see cref="ForwardProxyOptions"/> from the <see cref="IConfiguration"/> <see cref="ForwardProxyOptions.ForwardProxy"/> root section
        /// </summary>
        public static void ConfigureForwardProxyOptions(this WebApplicationBuilder builder)
        {
            builder.Services.Configure<ForwardProxyOptions>(builder.Configuration.GetSection(ForwardProxyOptions.ForwardProxy));
        }

        /// <summary>
        /// Registers DefaultHttpClient using <see cref="IHttpClientBuilder"/>.
        /// <para>Configured to not store cookies or follow auto redirects.</para>
        /// </summary>
        public static void RegisterDefaultHttpClient(this IServiceCollection serviceCollection)
        {
            serviceCollection.AddHttpClient(Contants.DefaultHttpClient, client =>
            {
            })
            .ConfigurePrimaryHttpMessageHandler(() =>
            {
                return new HttpClientHandler()
                {
                    AllowAutoRedirect = false,
                    UseCookies = false,
                };
            });
        }

        /// <summary>
        /// Registers DefaultHttpClient using <see cref="IHttpClientBuilder"/>.
        /// <para>Configured to not store cookies or follow auto redirects. Uses web proxy configured with a <see cref="ForwardProxyOptions"/> instance.</para>
        /// </summary>
        public static void RegisterDefaultHttpClientWithForwardProxy(this IServiceCollection services)
        {
            services.AddHttpClient(Contants.DefaultHttpClient, (services, client) =>
            {
            })
            .ConfigurePrimaryHttpMessageHandler((services) =>
            {
                var forwardProxyOptions = services.GetService<IOptionsSnapshot<ForwardProxyOptions>>();
                var isValid = forwardProxyOptions?.Value.IsValid ?? false;

                var loggerFactory = services.GetRequiredService<ILoggerFactory>();
                var logger = loggerFactory.CreateLogger("ServiceExtensions");

                if (isValid)
                {
                    logger.LogInformation("Using Proxy {Host}:{Port}", forwardProxyOptions!.Value.Host, forwardProxyOptions?.Value.Port);

                    var proxy = new WebProxy(forwardProxyOptions!.Value.Host, forwardProxyOptions.Value.PortInt);
                    return new HttpClientHandler()
                    {
                        AllowAutoRedirect = false,
                        UseCookies = false,
                        Proxy = proxy,
                    };
                }
                else
                {
                    logger.LogWarning("Not using forward proxy {Host}:{Port}", forwardProxyOptions?.Value.Host, forwardProxyOptions?.Value.Port);
                    return new HttpClientHandler()
                    {
                        AllowAutoRedirect = false,
                        UseCookies = false,
                    };
                }
            });
        }

        /// <summary>
        /// Maps a catchall fallback route to the <see cref="IContextMapper"/> when there is otherwise no matching route
        /// </summary>
        public static void MapFallbackToContextMapper(this WebApplication app)
        {
            app.MapFallback("{*path}", async (s) => await s.RequestServices.GetRequiredService<IContextMapper>().Invoke(s));
        }
    }
}
