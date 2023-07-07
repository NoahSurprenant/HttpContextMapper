using HtmlAgilityPack;
using HttpContextMapper;
using HttpContextMapper.Html;
using Microsoft.AspNetCore.Html;
using System.IO.Compression;
using System.Net;
using System.Text;

namespace ExampleWebApplication
{
    public class CustomHttpContextMapper : HtmlContextMapper
    {
        private readonly ILogger<CustomHttpContextMapper> _logger;

        public CustomHttpContextMapper(IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory) : base(httpClientFactory, loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<CustomHttpContextMapper>();
        }

        protected override Task ApplyHtmlModifications(HtmlDocument document)
        {
            var titleNode = document.DocumentNode.SelectSingleNode("//title");
            if (titleNode is not null)
                titleNode.InnerHtml = "Hello World!";
            return Task.CompletedTask;
        }
    }
}
