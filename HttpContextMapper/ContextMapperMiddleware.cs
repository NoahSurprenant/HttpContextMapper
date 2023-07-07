using Microsoft.AspNetCore.Http;

namespace HttpContextMapper;

public class ContextMapperMiddleware
{
    private readonly RequestDelegate _nextMiddleware;

    public ContextMapperMiddleware(RequestDelegate nextMiddleware)
    {
        _nextMiddleware = nextMiddleware;
    }

    public async Task Invoke(HttpContext context, IContextMapper contextMapper)
    {
        await contextMapper.Invoke(context);
    }
}