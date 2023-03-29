
using System;
using System.Diagnostics.CodeAnalysis;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Windows.System;
using Windows.Web.Http;
using Windows.Web.Http.Headers;
using HttpClient = Windows.Web.Http.HttpClient;

namespace Sashimi;

public class SlackClient
{
    private struct ProfileOperationResponse
    {
        #nullable enable
        public string ok;
        public string? error;
        public string? profile;
        #nullable restore
    }
    public struct SlackStatus
    {
        public SlackStatus(string statusEmoji, string statusText, int statusExpiration = 0)
        {
            status_emoji = statusEmoji;
            status_expiration = statusExpiration;
            status_text = statusText;
        }

#pragma warning disable IDE1006
        /// <value>The displayed emoji that is enabled for the Slack team, such as <c>:train:</c>.</value>
        public string status_emoji { get; set; }
        /// <value>The Unix Timestamp of when the status will expire.Providing <c>0</c> or omitting this field results in a custom status that will not expire.</value>
        public int status_expiration { get; set; }
        /// <value>The displayed text of up to 100 characters. We strongly encourage brevity.</value>
        public string status_text { get; set; }
        #pragma warning restore IDE1006
    }

    private struct SlackProfile
    {
        public SlackProfile(SlackStatus status)
        {
            profile = status;
        }

        /// <value>Collection of key:value pairs presented as a URL-encoded JSON hash. At most 50 fields may be set. Each field name is limited to 255 characters.</value>
        public SlackStatus profile { get; set; }
    }

    /// <value>Client ID used to identify the app when starting OAuth flows. Used in <see cref="Authorise"/></value>
    private readonly string _clientId;
    /// <value>Authorisation token to make API requests. Starts with <c>xoxp-</c>.</value>
    private string _token;
    public bool HasToken => _token != null;

    public SlackClient(string clientId, string token = null)
    {
        _clientId = clientId;
        _token = token;
    }

    /// <summary>
    /// Opens a browser to request an authorisation code from Slack.
    /// </summary>
    /// <param name="scope">The Slack scopes to request access to.</param>
    public async void Authorise(string scope)
    {
        Uri uri = new($"https://slack.com/oauth/authorize?client_id={_clientId}&scope={scope}");
        await Launcher.LaunchUriAsync(uri);
    }

    /// <inheritdoc cref="SetStatus"/>
    /// <summary>
    /// Clears the user status.
    /// </summary>
    public void ClearStatus()
    {
        SetStatus(new SlackStatus { status_emoji = "", status_text = "" });
    }

    /// <summary>
    /// Revokes the current token from Slack and removes it from the client.
    /// </summary>
    /// <exception cref="HttpRequestException">If the HTTP request fails. The token will not be cleared if this exception is thrown.</exception>
    public async Task Deauthorise()
    {
        Uri uri = new("https://slack.com/api/auth.revoke");

        HttpStringContent request = new($"{{token: {_token}}}");
        request.Headers["Content-type"] = "application/x-www-form-urlencoded; charset=utf-8";

        HttpClient httpClient = new();
        httpClient.DefaultRequestHeaders.Authorization = new HttpCredentialsHeaderValue("Bearer", _token);

        var httpResponseMessage = await httpClient.PostAsync(uri, request);
        httpResponseMessage.EnsureSuccessStatusCode();

        _token = null;
    }

    /// <summary>
    /// Sets the user status.
    /// </summary>
    /// <param name="status">The status to set.</param>
    /// <exception cref="HttpRequestException">If the HTTP request fails.</exception>
    /// <exception cref="Exception">If the HTTP request succeeds, but setting the status fails.</exception>
    public async void SetStatus(SlackStatus status)
    {
        Uri uri = new("https://slack.com/api/users.profile.set");

        HttpStringContent request = new(JsonSerializer.Serialize(new SlackProfile(status)));
        request.Headers["Content-type"] = "application/json; charset=utf-8";

        HttpClient httpClient = new();
        httpClient.DefaultRequestHeaders.Authorization = new HttpCredentialsHeaderValue("Bearer", _token);
        var httpResponseMessage = await httpClient.PostAsync(uri, request);

        // Recursively wait if rate-limited
        if (httpResponseMessage.StatusCode == HttpStatusCode.TooManyRequests)
        {
            int retryAfter = int.TryParse(httpResponseMessage.Headers["Retry-After"], out retryAfter) ? retryAfter : 10;
            await Task.Delay(retryAfter).ContinueWith(_ => {
                SetStatus(status);
            });
        }

        httpResponseMessage.EnsureSuccessStatusCode();
        ProfileOperationResponse response = JsonSerializer.Deserialize<ProfileOperationResponse>(await httpResponseMessage.Content.ReadAsStringAsync());
        if (response.ok != "true")
        {
            throw new Exception(response.error);
        }
    }

    /// <summary>
    /// Gets the user status.
    /// </summary>
    /// <exception cref="HttpRequestException">If the HTTP request fails.</exception>
    /// <exception cref="Exception">If the HTTP request succeeds, but getting the status fails.</exception>
    public async Task<string> GetStatus()
    {
        Uri uri = new("https://slack.com/api/users.profile.get");

        HttpStringContent request = new("");
        request.Headers["Content-type"] = "application/json; charset=utf-8";

        HttpClient httpClient = new();
        httpClient.DefaultRequestHeaders.Authorization = new HttpCredentialsHeaderValue("Bearer", _token);
        var httpResponseMessage = await httpClient.PostAsync(uri, request);
        
        // TODO: Add rate-limit handling
        //// Recursively wait if rate-limited
        //if (httpResponseMessage.StatusCode == HttpStatusCode.TooManyRequests)
        //{
        //    int retryAfter = int.TryParse(httpResponseMessage.Headers["Retry-After"], out retryAfter) ? retryAfter : 10;
        //    await Task.Delay(retryAfter).ContinueWith(_ => {
        //        GetStatus();
        //    });
        //}

        httpResponseMessage.EnsureSuccessStatusCode();
        ProfileOperationResponse response = JsonSerializer.Deserialize<ProfileOperationResponse>(await httpResponseMessage.Content.ReadAsStringAsync());
        if (response.ok != "true")
        {
            throw new Exception(response.error);
        }

        // TODO: Deserialise to SlackStatus
        return response.profile;
    }

    /// <summary>
    /// Sets the token to use for API requests.
    /// </summary>
    /// <param name="token">The token to use.</param>
    public void SetToken(string token)
    {
        _token = token;
    }
}
