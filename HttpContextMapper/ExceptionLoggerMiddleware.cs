using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace HttpContextMapper
{
    public class ExceptionLoggerMiddleware
    {
        private readonly RequestDelegate _nextMiddleware;
        private readonly ILogger<ExceptionLoggerMiddleware> _logger;

        public ExceptionLoggerMiddleware(RequestDelegate nextMiddleware, ILoggerFactory loggerFactory)
        {
            _nextMiddleware = nextMiddleware;
            _logger = loggerFactory.CreateLogger<ExceptionLoggerMiddleware>();
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _nextMiddleware.Invoke(context);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Exception captured in middleware");

                throw;
            }
        }
    }
}
