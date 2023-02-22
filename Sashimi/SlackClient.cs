using System;
using Windows.Web.Http;

public class SlackClient
{
	public struct SlackStatus
	{
		string status_emoji;
		int? status_expiration;
		string status_text;
	}

	public struct SlackProfile
	{
		SlackStatus profile;
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

}
