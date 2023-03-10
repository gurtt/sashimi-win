using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Windows.System;
using Windows.Web.Http;
using Windows.Web.Http.Headers;

namespace Sashimi;

public class SlackClient
{
    public struct SlackStatus
    {

        public SlackStatus(string statusEmoji, string statusText)
        {
            status_emoji = statusEmoji;
            status_text = statusText;
        }

        #pragma warning disable IDE1006
        public string status_emoji { get; set; }
        public int status_expiration { get; set; } = 0;
        public string status_text { get; set; }
        #pragma warning restore IDE1006
    }

    private struct SlackProfile
    {
        public SlackProfile(SlackStatus status)
        {
            profile = status;
        }

        private SlackStatus profile { get; set; }
    }

    private readonly string _clientId;
    private string _token;

    public SlackClient(string clientId, string token = null)
    {
        _clientId = clientId;
        _token = token;
    }

    /// <summary>
    /// Sets the token to use for API requests.
    /// </summary>
    /// <param name="token">The token to use.</param>
    public void SetToken(string token)
    {
        _token = token;
    }

    public bool HasToken => _token != null;

    /// <summary>
    /// Opens a browser to request an authorisation code from Slack.
    /// </summary>
    /// <param name="scope">The Slack scopes to request access to.</param>
    public async void Authorise(string scope)
    {
        Uri uri = new($"https://slack.com/oauth/authorize?client_id={_clientId}&scope={scope}");
        await Launcher.LaunchUriAsync(uri);
    }

    /// <summary>
    /// Sets the user status.
    /// </summary>
    /// <param name="emoji">The emoji string to set the status to. A nil value uses the Slack default status emoji.</param>
    /// <param name="text">The text to set the status to.</param>
    public async void SetStatus(SlackStatus status)
    {
        Uri uri = new("https://slack.com/api/users.profile.set");

        HttpStringContent request = new(JsonSerializer.Serialize(new SlackProfile(status)));
        request.Headers["Content-type"] = "application/json; charset=utf-8";

        HttpClient httpClient = new();
        httpClient.DefaultRequestHeaders.Authorization = new HttpCredentialsHeaderValue("Bearer", _token);
        var httpResponseMessage = await httpClient.PostAsync(uri, request);
        httpResponseMessage.EnsureSuccessStatusCode();

        
    }

    /// <summary>
    /// Clears the user status.
    /// </summary>
    public void ClearStatus()
    {
        SetStatus(new SlackStatus { status_emoji = "", status_text = "" });
    }

}