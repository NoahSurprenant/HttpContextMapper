#nullable disable
using System.Collections.Specialized;
using System.Text;
using System.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace HttpContextMapper;

public interface IContextMapper
{
    Task Invoke(HttpContext context);
}

public class ContextMapper : IContextMapper
{
    private const string DefaultTarget = "https://github.com";

    private string _targetUrlWithProtocol;
    protected string TargetUrlWithProtocol
    {
        get => _targetUrlWithProtocol ?? DefaultTarget;
        set => _targetUrlWithProtocol = value;
    }

    protected string TargetUrlNoProtocol => TargetUrlWithProtocol.Replace("https://", "").Replace("http://", "");
    protected string ProxyProtocolString => HttpContext.Request.IsHttps ? "https://" : "http://";
    protected string ProxyUrlWithProtocol => ProxyProtocolString + ProxyUrlNoProtocol;
    protected string ProxyUrlNoProtocol => HttpContext.Request.Host.ToUriComponent();
    
    protected HttpContext HttpContext;
    protected HttpRequestMessage RequestMessage;
    protected HttpResponseMessage ResponseMessage;
    protected readonly IHttpClientFactory HttpClientFactory;
    private readonly ILogger<ContextMapper> _logger;

    public ContextMapper(IHttpClientFactory httpClientFactory, ILoggerFactory loggerFactory)
    {
        HttpClientFactory = httpClientFactory;
        _logger = loggerFactory.CreateLogger<ContextMapper>();
        UriBuilder = new UriBuilder(TargetUrlWithProtocol);
    }

    protected List<string> DoNotMapHeaders = new List<string>()
    {
        "priority",
        "cf-ray",
        "cdn-loop",
        "cf-connecting-ip",
        "cf-ipcountry",
        "cf-visitor",
        "x-forwarded-proto",
        "x-forwarded-server",
        "x-forwarded-port",
        "x-real-ip",
        "x-forwarded-host",
        "x-forwarded-for",
    };

    protected virtual HttpClient CreateClient()
    {
        return HttpClientFactory.CreateClient("DefaultReverseProxy");
    }

    public void SetTarget(string target) => TargetUrlWithProtocol = target;

    public virtual async Task Invoke(HttpContext context)
    {
        HttpContext = context;
        
        await MapToHttpRequestMessage();

        var client = CreateClient();

        ResponseMessage = await client.SendAsync(RequestMessage, HttpCompletionOption.ResponseContentRead,
            HttpContext.RequestAborted);

        await MapFromResponseMessage();
    }

    #region MapContextToRequestMessage
    protected virtual async Task MapToHttpRequestMessage()
    {
        RequestMessage = new HttpRequestMessage();

        if (HttpContext.Request.Method == HttpMethods.Post)
        {
            await MapRequestContent();
        }
        
        // TODO: this really depends on the mapped content... If the size or type changes then this is wrong
        if (!string.IsNullOrEmpty(HttpContext.Request.ContentType))
        {
            //RequestMessage.Headers.Append("Content-Type", HttpContext.Request.ContentType);
            RequestMessage.Headers.Append(new KeyValuePair<string, IEnumerable<string>>("Content-Type", new List<string>() { HttpContext.Request.ContentType }));
            //var success = RequestMessage.Content.Headers.TryAddWithoutValidation("Content-Type", HttpContext.Request.ContentType);
        }

        // Moved this call into functions that map the content
        //if (HttpContext.Request.ContentLength is not null)
        //{
        //    var success = RequestMessage.Content.Headers.TryAddWithoutValidation("Content-Length", HttpContext.Request.ContentLength.Value.ToString());
        //}
        await MapToRequestHeaders();


        await SetRequestUri();
        RequestMessage.Headers.Host = RequestMessage.RequestUri!.Host;
        RequestMessage.Method = new HttpMethod(HttpContext.Request.Method);
    }

    protected UriBuilder UriBuilder;

    protected virtual async Task SetRequestUri()
    {
        var originalQuery = HttpContext.Request.QueryString.Value;
        var parsedQuery = HttpUtility.ParseQueryString(originalQuery!);

        await SetUriBuilderPathAndQuery(HttpContext.Request.Path, parsedQuery);

        RequestMessage.RequestUri = UriBuilder.Uri;
    }

    protected virtual Task SetUriBuilderPathAndQuery(PathString path, NameValueCollection query)
    {
        UriBuilder.Path = path;
        UriBuilder.Query = query.ToString();
        return Task.CompletedTask;
    }

    protected virtual async Task MapRequestContent()
    {
        if (HttpContext.Request.Method != HttpMethods.Post)
            throw new InvalidOperationException("Tried to map http content when HttpRequest was not Post");

        if (HttpContext.Request.HasFormContentType)
        {
            await MapFormRequestContent();
        }
        else if (HttpContext.Request.ContentType?.Contains("application/json") ?? false)
        {
            await MapJsonRequestContent();
        }
        else
        {
            await MapGenericRequestContent();
        }
    }

    protected virtual async Task MapFormRequestContent()
    {
        var form = HttpContext.Request.Form;
        Dictionary<string, string> formDictionary = new();
        foreach (var entry in form)
            formDictionary.Add(entry.Key, entry.Value!);

        await FormRequestContent(formDictionary);
        RequestMessage.Content = new FormUrlEncodedContent(formDictionary);

        var s = await RequestMessage.Content.ReadAsStreamAsync();
        RequestMessage.Content.Headers.ContentLength = s.Length;
    }

    protected virtual Task FormRequestContent(Dictionary<string, string> formDictionary)
    {
        return Task.CompletedTask;
    }

    protected virtual async Task MapJsonRequestContent()
    {
        // Example of overriden method:
        //if (HttpContext.Request.Path == "UpdateEndpoint")
        //{
        //    using var stream = HttpContext.Request.Body;

        //    var dto = await JsonSerializer.DeserializeAsync<TestDto>(stream);
        //    dto.Test = false;

        //    var ms = new MemoryStream();
        //    await JsonSerializer.SerializeAsync<TestDto>(ms, dto);
        //    SetRequestContentToMemoryStream(ms);
        //}
        //else
        //{
        //    await MapGenericRequestContent();
        //}

        await MapGenericRequestContent();
    }

    protected virtual async Task MapGenericRequestContent()
    {
        using var stream = HttpContext.Request.Body;
        var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        SetRequestContentToMemoryStream(ms);
    }

    protected virtual void SetRequestContentToMemoryStream(MemoryStream ms)
    {
        ms.Position = 0;
        RequestMessage.Content = new StreamContent(ms);
        RequestMessage.Content.Headers.ContentLength = ms.Length;
    }

    protected virtual async Task MapToRequestHeaders()
    {
        var dictionary = new Dictionary<string, string>();

        foreach (var header in HttpContext.Request.Headers)
        {
            if (DoNotMapHeaders.Contains(header.Key))
                continue;
            
            if (header.Value.Count > 1)
            {
                // Convert into comma seperated list.
                // https://stackoverflow.com/questions/4371328/are-duplicate-http-response-headers-acceptable
                // Not sure why HttpContext seems to allow duplicates but HttpRequestMessage does not?
                var values = header.Value;
                var mappedValueFirst = await MapRequestHeader(header.Key, header.Value.First()!);
                var stringBuilder = new StringBuilder(mappedValueFirst);
                for (var i = 1; i < values.Count(); i++)
                {
                    stringBuilder.Append("; ");
                    var mappedValue = await MapRequestHeader(header.Key, values.ElementAt(i)!);
                    stringBuilder.Append(mappedValue);
                }

                dictionary.Add(header.Key, stringBuilder.ToString());
            }
            else
            {
                var mappedValue = await MapRequestHeader(header.Key, header.Value.First()!);
                dictionary.Add(header.Key, mappedValue);
            }
        }
        
        foreach (var header in dictionary)
        {
            //RequestMessage.Headers.Add(header.Key, header.Value);
            RequestMessage.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
    }

    protected virtual Task<string> MapRequestHeader(string key, string value)
    {
        if (key.ToLower() is "referrer" || key.ToLower() is "referer" || key.ToLower() is "origin")
        {
            return Task.FromResult(ReplaceProxyWithTarget(ref value));
        }
        
        return Task.FromResult(value);
    }

    protected virtual string ReplaceProxyWithTarget(ref string stringContainingProxy)
    {
        return stringContainingProxy
            .Replace(ProxyUrlWithProtocol, TargetUrlWithProtocol)
            .Replace(ProxyUrlNoProtocol, TargetUrlNoProtocol);
    }
    

    #endregion MapContextToRequestMessage

    #region MapResponseMessageToContext
    protected virtual async Task MapFromResponseMessage()
    {
        HttpContext.Response.StatusCode = (int)ResponseMessage.StatusCode;
        
        await MapResponseHeaders();
        
        // Content headers ex: Content-Type, Content-Length, Last-Modified, Expires, Content-Encoding, Content-Language
        await MapResponseContentHeaders();
        
        if (ResponseMessage.Content.Headers.ContentLength > 0)
            await MapResponseContent();
    }

    protected virtual async Task MapResponseHeaders()
    {
        foreach (var header in ResponseMessage.Headers)
        {
            foreach (var value in header.Value)
            {
                await MapResponseHeader(header.Key, value);
            }
        }
    }

    protected bool DisableSetCookieEncoding = false;

    protected virtual Task MapSetCookieHeader(string value)
    {
        if (DisableSetCookieEncoding)
        {
            var cookieMapper = new CookieMapper();
            var cookieWithOptions = cookieMapper.ExtractCookie(value);

            var options = cookieWithOptions.Options;
            var setCookieHeaderValue = new SetCookieHeaderValue(
                    cookieWithOptions.Key,
                    Uri.EscapeDataString(cookieWithOptions.Value))
            {
                Domain = options.Domain,
                Path = options.Path,
                Expires = options.Expires,
                MaxAge = options.MaxAge,
                Secure = options.Secure,
                SameSite = (Microsoft.Net.Http.Headers.SameSiteMode)options.SameSite,
                HttpOnly = options.HttpOnly
            }.ToString();

            var unescapedCookie = Uri.UnescapeDataString(setCookieHeaderValue);

            HttpContext.Response.Headers[HeaderNames.SetCookie] = StringValues.Concat(HttpContext.Response.Headers[HeaderNames.SetCookie], unescapedCookie);
        }
        else
        {
            var cookieMapper = new CookieMapper();
            var cookieWithOptions = cookieMapper.ExtractCookie(value);
            HttpContext.Response.Cookies.Append(cookieWithOptions.Key, cookieWithOptions.Value, cookieWithOptions.Options);
        }
        
        return Task.CompletedTask;
    }

    protected virtual async Task MapResponseHeader(string key, string value)
    {
        // Is is important to use Append because Add will throw an exception if the header already exists with the same key
        if (key is "Set-Cookie")
        {
            await MapSetCookieHeader(value);
        }
        // If there is a status 301 (redirection) then there will be a header with Key "Location"
        // This tells the client where to go next, you'd expect this to be a relative path but it is not always so I am going to make sure we replace any
        // absolute paths to the target with absolute path to our proxy. Otherwise the client will end up on the real website
        else if (key is "Location")
        {
            HttpContext.Response.Headers.Append(key, ReplaceTargetWithProxy(ref value));
        }
        else
        {
            HttpContext.Response.Headers.Append(key, value);
        }
    }


    protected virtual async Task MapResponseContentHeaders()
    {
        foreach (var header in ResponseMessage.Content.Headers)
        {
            foreach (var value in header.Value)
            {
                await MapResponseContentHeader(header.Key, value);
            }
        }
    }

    protected virtual Task MapResponseContentHeader(string key, string value)
    {
        HttpContext.Response.Headers.Append(key, value);
        return Task.CompletedTask;
    }
    
    protected virtual async Task MapResponseContent()
    {
        ResponseMessage.Content.Headers.TryGetValues("Content-Type", out var contentTypes);
        if (contentTypes?.Any(x => x.Contains("text/html")) ?? false)
        {
            await MapHtmlResponseContent();
        }
        else if (contentTypes?.Any(x => x.Contains("application/json")) ?? false)
        {
            await MapJsonResponseContent();
        }
        else
        {
            await MapGenericResponseContent();
        }
    }

    protected virtual async Task MapHtmlResponseContent()
    {
        await MapGenericResponseContent();
    }

    protected virtual async Task MapJsonResponseContent()
    {
        // Example of overriden method:
        //if (ResponseMessage.StatusCode is System.Net.HttpStatusCode.OK && RequestMessage.RequestUri.PathAndQuery.Equals("SomeEndpoint"))
        //{
        //    var stream = await ResponseMessage.Content.ReadAsStreamAsync();
        //    var dto = await JsonSerializer.DeserializeAsync<TestDto>(stream);
        //    dto.Count = 2;

        //    var ms = new MemoryStream();
        //    await JsonSerializer.SerializeAsync<TestDto>(ms, dto);
        //    ResponseMessage.Content.Headers.ContentLength = ms.Length;
        //    await ms.CopyToAsync(HttpContext.Response.Body);
        //}
        //else
        //{
        //    await MapGenericResponseContent();
        //}

        await MapGenericResponseContent();
    }

    protected virtual async Task MapGenericResponseContent()
    {
        // Not sure why we have to do this
        if (ResponseMessage.StatusCode is System.Net.HttpStatusCode.NotModified) return;
        
        await ResponseMessage.Content.CopyToAsync(HttpContext.Response.Body);
    }
    
    protected virtual string ReplaceTargetWithProxy(ref string stringContainingTarget)
    {
        return stringContainingTarget
            .Replace(TargetUrlWithProtocol, ProxyUrlWithProtocol)
            .Replace(TargetUrlNoProtocol, ProxyUrlNoProtocol);
    }
    

    #endregion MapResponseMessageToContext
}