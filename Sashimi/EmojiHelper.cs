using Microsoft.UI.Xaml.Media.Imaging;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Windows.Web.Http.Headers;
using Windows.Web.Http;

namespace Sashimi
{
    public class Emoji
    {
        /// <value>The URI of the displayed image for the emoji.</value>
        public Uri ImagePath { get; set; }
        /// <value>The alias for the emoji.</value>
        public string Alias { get; set; }

        public Emoji(Uri imagePath, string alias)
        {
            ImagePath = imagePath;
            Alias = alias;
        }

        public BitmapImage Bitmap => new(ImagePath);

        /// <summary>
        /// Gets the list of builtin emojis. Doesn't include variations.
        /// </summary>
        /// <exception cref="HttpRequestException">If the HTTP request fails.</exception>
        public static async Task<List<Emoji>> GetBuiltins()
        {
            Uri uri = new("https://raw.githubusercontent.com/iamcal/emoji-data/master/emoji_pretty.json");

            HttpStringContent request = new("");

            HttpClient httpClient = new();
            httpClient.DefaultRequestHeaders.UserAgent.Add(new("gurtt/sashimi-win"));
            var httpResponseMessage = await httpClient.GetAsync(uri);

            httpResponseMessage.EnsureSuccessStatusCode();
            JsonArray response = JsonNode.Parse(await httpResponseMessage.Content.ReadAsStringAsync())!.AsArray();

            List<Emoji> emojiList = new();
            foreach (var emojiJson in response)
            {
                JsonObject emoji = emojiJson.AsObject();

                if (!(bool)emoji["has_img_google"]) continue;
                string pathString = $"https://raw.githubusercontent.com/iamcal/emoji-data/master/img-google-64/{emoji["image"]}";

                emojiList.Add(new Emoji(new(pathString), (string)emoji["short_name"]));
            }
            return emojiList;
        }
    }
}