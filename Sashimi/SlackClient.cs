using System;
using System.Diagnostics;
using System.Text.Json;
using Windows.Storage.Streams;
using Windows.Web.Http;

public class SlackClient
{
	public struct SlackStatus
	{
        #pragma warning disable IDE1006
        public string status_emoji;
        public int status_expiration;
        public string status_text;
        #pragma warning restore IDE1006
    }

    public struct SlackProfile
	{
        public SlackStatus profile;
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
	public bool HasToken()
	{
		return token != null;
	}

    /// <summary>
    /// Opens a browser to request an authorisation code from Slack.
    /// </summary>
    /// <param name="scope">The Slack scopes to request access to.</param>
	public async void Authorise(string scope)
	{
        Uri uri = new($"https://slack.com/oauth/authorize?client_id=${clientId}&scope=${scope}");
        await Windows.System.Launcher.LaunchUriAsync(uri);
    }

    /// <summary>
    /// Sets the user status.
    /// </summary>
    /// <param name="emoji">The emoji string to set the status to. A nil value uses the Slack default status emoji.</param>
    /// <param name="text">The text to set the status to.</param>
    public async void SetStatus(SlackStatus status)
    {
        try
        {
            Uri uri = new("https://slack.com/api/users.profile.set");

            HttpStringContent request = new(JsonSerializer.Serialize(new SlackProfile {  profile = status }));
            request.Headers["Content-type"] = "application/json; charset=utf-8";
            request.Headers["Authorization"] = $"Bearer ${token}";

            HttpClient httpClient = new HttpClient();
            HttpResponseMessage httpResponseMessage = await httpClient.PostAsync(uri, request);
            httpResponseMessage.EnsureSuccessStatusCode();

        } catch (Exception ex)
        {
            Debug.WriteLine(ex);
        }
    }

    /// <summary>
    /// Clears the user status.
    /// </summary>
    public void ClearStatus()
    {
        SetStatus(new SlackStatus { status_emoji = "", status_text = "" });
    }

}
