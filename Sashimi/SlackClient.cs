using System;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text.Json;
using Windows.Storage.Streams;
using Windows.Web.Http;

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

    public struct SlackProfile
	{
        public SlackProfile(SlackStatus status)
        {
            profile = status;
        }
        public SlackStatus profile { get; set; }
    }

	private readonly string clientId;
	private string token;

	public SlackClient(string clientId, string token = null)
	{
		this.clientId = clientId;
		this.token = token;
	}

    /// <summary>
    /// Sets the token to use for API requests.
    /// </summary>
    /// <param name="token">The token to use.</param>
    public void SetToken(string token)
	{
		this.token = token;
	}

    /// <summary>
    /// Checks if the client has an access token.
    /// </summary>
    /// <returns>Wether or not the client has an access token.</param>
	public bool HasToken => token != null;

    /// <summary>
    /// Opens a browser to request an authorisation code from Slack.
    /// </summary>
    /// <param name="scope">The Slack scopes to request access to.</param>
	public async void Authorise(string scope)
	{
        Uri uri = new($"https://slack.com/oauth/authorize?client_id={clientId}&scope={scope}");
        await Windows.System.Launcher.LaunchUriAsync(uri);
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

            HttpClient httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new("Bearer", token);
        HttpResponseMessage httpResponseMessage = await httpClient.PostAsync(uri, request);
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
