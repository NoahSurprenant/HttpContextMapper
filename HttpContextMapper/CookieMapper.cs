using Microsoft.AspNetCore.Http;

namespace HttpContextMapper;

public class CookieWithOptions
{
    public CookieOptions Options { get; set; } = new CookieOptions();
    public string Key { get; set; } = default!;
    public string Value { get; set; } = default!;
}

public class CookieMapper
{
    public CookieWithOptions ExtractCookie(string cookieString)
    {
        CookieWithOptions cookie = new();

        var parts = cookieString!.Split("; ");
        var claims = new Dictionary<string, string>();

        foreach (var part in parts)
        {
            var keyValuePair = part.Split('=');
            claims.Add(keyValuePair.First(), keyValuePair.Last());
        }

        SetCookieOptions(claims, cookie);

        return cookie;
    }
    
    private void SetCookieOptions(
        Dictionary<string, string> claims,
        CookieWithOptions cookie)
    {
        foreach (var claim in claims)
        {
            switch (claim.Key.ToLower())
            {
                case CookieProperties.Domain:
                    cookie.Options.Domain = claim.Value;
                    break;
                case CookieProperties.Path:
                    cookie.Options.Path = claim.Value;
                    break;
                case CookieProperties.Expires:
                    var dt = DateTime.Parse(claim.Value);
                    cookie.Options.Expires = dt;
                    break;
                case CookieProperties.Secure:
                    cookie.Options.Secure = parseAsBoolOrString(claim.Value, CookieProperties.Secure);
                    break;
                case CookieProperties.SameSite:
                    cookie.Options.SameSite = Enum.Parse<SameSiteMode>(claim.Value);
                    break;
                case CookieProperties.HttpOnly:
                    cookie.Options.HttpOnly = parseAsBoolOrString(claim.Value, CookieProperties.HttpOnly);
                    break;
                case CookieProperties.MaxAge:
                    cookie.Options.MaxAge = TimeSpan.Parse(claim.Value);
                    break;
                case CookieProperties.IsEssential:
                    cookie.Options.IsEssential = parseAsBoolOrString(claim.Value, CookieProperties.IsEssential);
                    break;
                default:
                    cookie.Key = claim.Key;
                    cookie.Value = claim.Value;
                    break;
            }
        }
    }
    
    private bool parseAsBoolOrString(string valueToParse, string stringToParseAgainstIfBoolParseFails)
    {
        var success = bool.TryParse(valueToParse, out var result);
        if (success)
        {
            return result;
        }
        else
        {
            if (valueToParse.ToLower() == stringToParseAgainstIfBoolParseFails.ToLower())
                return true;
            else
            {
                return false; // I think this is right?, not sure
            }
        }
    }
    
    public static class CookieProperties
    {
        // a lot of these values are wrong so I am just doing lowercase and will compare key.ToLower()
        public const string Domain = "domain";
        public const string Path = "path";
        public const string Expires = "expires";
        public const string Secure = "secure";
        public const string SameSite = "samesite";
        public const string HttpOnly = "httponly";
        public const string MaxAge = "maxage";
        public const string IsEssential = "isessential";
    }
}