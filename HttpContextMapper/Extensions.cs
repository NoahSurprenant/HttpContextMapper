using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace HttpContextMapper;

public static class Extensions
{
    public static void RegisterDefaultReverseProxy(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddHttpClient("DefaultReverseProxy", client =>
        {
        })
        .ConfigurePrimaryHttpMessageHandler(() =>
        {
            return new HttpClientHandler()
            {
                AllowAutoRedirect = false,
                UseCookies = false,
                //Proxy = null,
            };
        });
    }

    public static void MapFallbackToContextMapper(this WebApplication app)
    {
        app.MapFallback("{*path}", async (s) => await s.RequestServices.GetRequiredService<IContextMapper>().Invoke(s));
    }
}