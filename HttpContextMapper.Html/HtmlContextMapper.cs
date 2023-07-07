using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using System.IO.Compression;
using System.Net;
using System.Text;

namespace HttpContextMapper.Html
{
    public class HtmlContextMapper : ContextMapper
    {
        private readonly ILogger<HtmlContextMapper> _logger;

        /// <summary>
        /// If you know that you do not want to load Html and modify the response you should set to false for improved performance
        /// </summary>
        protected bool ShouldLoadHtml = true;

        public HtmlContextMapper(IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory) : base(httpClientFactory, loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<HtmlContextMapper>();
        }

        /// <summary>
        /// Not intended to be overriden when using the HtmlContextMapper. Instead you likely want to override the ApplyHtmlModifications method instead or use the standard ContextMapper if you do not like this implementation.
        /// </summary>
        protected override async Task MapHtmlResponseContent()
        {
            //ShouldLoadHtml= false;
            if (!ShouldLoadHtml)
            {
                await MapGenericResponseContent();
                return;
            }

            string htmlstring = null;
            var responseContentBytes = await ResponseMessage.Content.ReadAsByteArrayAsync();

            var isGzip = ResponseMessage.Content.Headers.ContentEncoding.Any(x => x.Contains("gzip"));
            if (isGzip)
            {
                using var outputStream = new MemoryStream();
                using var compressedStream = new MemoryStream(responseContentBytes);
                using var sr = new GZipStream(compressedStream, CompressionMode.Decompress);
                sr.CopyTo(outputStream);
                outputStream.Position = 0;
                var decompressed = outputStream.ToArray();

                htmlstring = Encoding.GetEncoding("utf-8").GetString(decompressed, 0, decompressed.Length - 1);

                var removedContentEncoding = HttpContext.Response.Headers.Remove("Content-Encoding");
                _logger.LogInformation("Decompressed GZIP to load Html. Removed Content-Encoding header: {removedContentEncoding}", removedContentEncoding);
            }
            else
            {
                htmlstring = Encoding.GetEncoding("utf-8").GetString(responseContentBytes, 0, responseContentBytes.Length - 1);
            }

            var htmldecoded = WebUtility.HtmlDecode(htmlstring);
            HtmlDocument document = new HtmlDocument();
            document.LoadHtml(htmldecoded);

            await ApplyHtmlModifications(document);

            using var stream = new MemoryStream();
            document.Save(stream, Encoding.UTF8);
            stream.Seek(0, System.IO.SeekOrigin.Begin);

            HttpContext.Response.ContentLength = stream.Length; // Need to set the content-length again because we are modifying the content
            HttpContext.Response.Headers.Remove("Transfer-Encoding");

            await stream.CopyToAsync(HttpContext.Response.Body);
        }

        protected virtual Task ApplyHtmlModifications(HtmlDocument document)
        {
            //var titleNode = document.DocumentNode.SelectSingleNode("//title");
            //if (titleNode is not null)
            //    titleNode.InnerHtml = "Hello World!";
            return Task.CompletedTask;
        }
    }
}