using HtmlAgilityPack;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic.CompilerServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.ServiceModel.Syndication;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using VimeoToKalturaVideoTransfer.Controllers;
using VimeoToKalturaVideoTransfer.Models;

namespace VimeoToKalturaVideoTransfer.Helper
{
    public class VimeoHelper
    {
        public string AccessToken { get; set; }
        public string VimeoUserId { get; set; }
        private string VimeoAPIURL { get; set; } = "https://api.vimeo.com";

        public VimeoHelper(string accessToken, string vimeoUserId)
        {
            AccessToken = accessToken;
            VimeoUserId = vimeoUserId;
        }

        public bool DownloadVideos(VimeoModel vimeoModel)
        {
            bool apiCallSuccess = false;

            try
            {
                APIHelper apiHelper = new APIHelper("", string.Concat(VimeoAPIURL, "/users/", VimeoUserId, "/videos?per_page=100&page=", vimeoModel.CurrentPageNumber), AccessToken);
                vimeoModel.JSON = apiHelper.SendWithBearer();
                apiCallSuccess = true;
            }
            catch (Exception e)
            {
                return apiCallSuccess;
            }

            return apiCallSuccess;
        }

        public bool ParseJSONVimeoDownload(VimeoModel vimeoModel)
        {
            bool parseSuccess = false;

            try
            {
                var jsonDictionary = JsonConvert.DeserializeObject<IDictionary<string, object>>(vimeoModel.JSON);
                var data = jsonDictionary.Where(x => x.Key.ToLower() == "data");

                foreach (var dataRow in data)
                {
                    foreach (var jToken in (JArray)dataRow.Value)
                    {
                        VimeoVideoModel vimeoVideoModel = new VimeoVideoModel();
                        vimeoVideoModel.Captions = new List<Caption>();
                        vimeoVideoModel.Pictures = new List<Picture>();

                        if (!PopulateVimeoVideoModel(vimeoVideoModel, jToken))
                        {
                            return parseSuccess;
                        }

                        vimeoModel.VimeoVideos.Add(vimeoVideoModel);
                    }
                }

                int totalVideos = Convert.ToInt32(jsonDictionary.Where(x => x.Key.ToLower().Contains("total")).FirstOrDefault().Value);
                int perPage = Convert.ToInt32(jsonDictionary.Where(x => x.Key.ToLower().Contains("per_page")).FirstOrDefault().Value);
                decimal totalPages = Math.Ceiling(Decimal.Divide((decimal)totalVideos,(decimal)perPage));
                bool isMaxPageReached = vimeoModel.CurrentPageNumber >= totalPages ? true : false;
                
                if (isMaxPageReached)
                {
                    vimeoModel.CallAPI = false;
                }

                vimeoModel.CurrentPageNumber++;
                parseSuccess = true;
            }
            catch (Exception e)
            {
                return parseSuccess;
            }

            return parseSuccess;
        }

        public bool PopulateVimeoVideoModel(VimeoVideoModel vimeoVideoModel, JToken data)
        {
            bool populateModelSuccess = false;
            string videoDownloadURL = string.Empty;

            try
            {
                var innerHtmlJson = GenerateInnerHtml(data);
                
                string cleanedInnerHtml = CleanInnerHtml(innerHtmlJson);

                if (!string.IsNullOrEmpty(cleanedInnerHtml))
                {
                    videoDownloadURL = GetVideoDownloadUrl(cleanedInnerHtml, vimeoVideoModel);
                }

                vimeoVideoModel.VideoFileURL = videoDownloadURL;
                string videoName = data.SelectToken("name").Value<string>();
                string videoVimeoId = data.SelectToken("uri").Value<string>().Split('/')[2];
                vimeoVideoModel.Name = string.Concat(videoName, " (", videoVimeoId, ')');
                vimeoVideoModel.Height = data.SelectToken("height").Value<int>();
                vimeoVideoModel.Width = data.SelectToken("width").Value<int>();
                vimeoVideoModel.Duration = data.SelectToken("duration").Value<int>();
                vimeoVideoModel.Description = data.SelectToken("description").Value<string>();

                if (string.IsNullOrEmpty(vimeoVideoModel.Description))
                {
                    vimeoVideoModel.Description = videoName;
                }

                if (CallVimeoCaptionAPIs(vimeoVideoModel, data) && ParsePicturesFromVimeoJSON(vimeoVideoModel, data))
                {
                    populateModelSuccess = true;
                }
            }
            catch (Exception e)
            {
                return populateModelSuccess;
            }
            
            return populateModelSuccess;
        }

        public bool CallVimeoCaptionAPIs(VimeoVideoModel vimeoVideoModel, JToken data)
        {
            List<Caption> captions = new List<Caption>();
            string textTrackApiReturn = string.Empty;
            bool captionAPISuccess = false;

            try
            {
                vimeoVideoModel.CaptionURI = data.SelectToken("metadata.connections.texttracks.uri").Value<string>();
                APIHelper apiHelper = new APIHelper("", string.Concat(VimeoAPIURL, vimeoVideoModel.CaptionURI), AccessToken);
                textTrackApiReturn = apiHelper.SendWithBearer();

                var jsonDictionary = JsonConvert.DeserializeObject<IDictionary<string, object>>(textTrackApiReturn);
                var captionData = jsonDictionary.Where(x => x.Key.ToLower() == "data");
                int totalCaptions = Convert.ToInt32(jsonDictionary.Where(x => x.Key.ToLower().Contains("total")).FirstOrDefault().Value);

                foreach (var dataRow in captionData)
                {
                    foreach (var jToken in (JArray)dataRow.Value)
                    {
                        if (!jToken.SelectToken("active").Value<string>().Equals("true", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }

                        Caption caption = new Caption()
                        {
                            URL = jToken.SelectToken("link").Value<string>().Replace("&", ";"),
                            Name = jToken.SelectToken("name").Value<string>(),
                            Language = jToken.SelectToken("display_language").Value<string>().Split(" ").FirstOrDefault(),
                            DisplayLanguage = jToken.SelectToken("display_language").Value<string>(),
                            Active = jToken.SelectToken("active").Value<string>()
                        };

                        captions.Add(caption);
                    }
                }

                if (captions.Count == 1)
                {
                    captions.FirstOrDefault().IsDefault = true;
                }
                else if (captions.Count > 1)
                {
                    var englishCaptions = captions.Where(x => x.Language.Equals("english", StringComparison.OrdinalIgnoreCase));

                    if (englishCaptions.Count() > 1)
                    {
                        captions.RemoveAll(x => x.DisplayLanguage.Contains("auto-generated") && x.Language.Equals("english", StringComparison.OrdinalIgnoreCase));
                    }

                    captions.Where(x => x.Language.Equals("english", StringComparison.OrdinalIgnoreCase)).FirstOrDefault().IsDefault = true;
                }

                vimeoVideoModel.Captions = captions;

                captionAPISuccess = true;
            }
            catch (Exception e)
            {
                return captionAPISuccess;
            }

            return captionAPISuccess;
        }

        public bool ParsePicturesFromVimeoJSON(VimeoVideoModel vimeoVideoModel, JToken data)
        {
            bool parsePicturesSuccess = false;
            var maxPictureHeight = 0;
            Picture maxPicture = new Picture();

            try
            {
                var pictures = data.SelectToken("pictures.sizes");

                if (pictures == null)
                {
                    return parsePicturesSuccess;
                }

                foreach (var picture in pictures)
                {
                    var currentPictureHeight = picture.SelectToken("height").Value<int>();

                    if (maxPictureHeight < currentPictureHeight)
                    {
                        string url = picture.SelectToken("link").Value<string>();
                        int width = picture.SelectToken("width").Value<int>();
                        int height = currentPictureHeight;

                        maxPicture = new Picture(url, width, height);
                        maxPictureHeight = currentPictureHeight;
                    }
                }

                vimeoVideoModel.Pictures.Add(maxPicture);

                parsePicturesSuccess = true;
            }
            catch (Exception e)
            {
                return parsePicturesSuccess;
            }

            return parsePicturesSuccess;
        }

        private IDictionary<string, object> GenerateInnerHtml(JToken data)
        {
            IDictionary<string, object> innerHtmlJson = null;

            try
            {
                HtmlWeb web = new HtmlWeb();
                var document = web.Load(data.SelectToken("player_embed_url").Value<string>());

                StringReader reader = new StringReader(document.DocumentNode.InnerHtml);

                Sgml.SgmlReader sgmlReader = new Sgml.SgmlReader()
                {
                    DocType = "HTML",
                    WhitespaceHandling = WhitespaceHandling.All,
                    CaseFolding = Sgml.CaseFolding.ToLower,
                    InputStream = reader
                };

                XmlDocument doc = new XmlDocument()
                {
                    PreserveWhitespace = true,
                    XmlResolver = null
                };

                doc.Load(sgmlReader);
                var xmlString = JsonConvert.SerializeXmlNode(doc);
                innerHtmlJson = JsonConvert.DeserializeObject<IDictionary<string, object>>(xmlString);
            }
            catch (Exception e)
            {
                return innerHtmlJson;
            }

            return innerHtmlJson;
        }

        private string CleanInnerHtml(IDictionary<string, object> json)
        {
            //string trimStartText = " window.playerConfig=";
            //string editEndPoint = "; var hasRequest";
            //string editEndPointBackUp = "    var fullscreenSupported";
            string trimStartText = "window.playerConfig=";
            string editEndPoint = ";varhasRequest";
            string editEndPointBackUp = "varfullscreenSupported";
            string cleanedInnerHtml = string.Empty;

            try
            {
                var html = (JObject)json["html"];
                foreach (var htmlSection in html.Children())
                {
                    if (htmlSection.Path.Equals("body", StringComparison.OrdinalIgnoreCase))
                    {
                        var dataSection = htmlSection.Children()["script"].Children()["#cdata-section"];

                        foreach (var data in dataSection)
                        {
                            var dataWithoutSpaces = data.ToString().Replace(" ", "");
                            if (dataWithoutSpaces.Contains(trimStartText))
                            {
                                cleanedInnerHtml = dataWithoutSpaces;
                                int trimIndex = cleanedInnerHtml.IndexOf(editEndPoint);
                                int trimIndexBackup = cleanedInnerHtml.IndexOf(editEndPointBackUp);
                                if (trimIndex > 0)
                                {
                                    cleanedInnerHtml = cleanedInnerHtml.Remove(trimIndex).Replace(trimStartText, "").Trim();
                                }
                                else if (trimIndexBackup > 0)
                                {
                                    cleanedInnerHtml = cleanedInnerHtml.Remove(trimIndexBackup).Replace(trimStartText, "").Trim();
                                }
                                else
                                {
                                    cleanedInnerHtml = cleanedInnerHtml.Replace(trimStartText, "").Trim();
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                return cleanedInnerHtml;
            }

            return cleanedInnerHtml;
        }

        private string GetVideoDownloadUrl(string innerHtml, VimeoVideoModel vimeoVideoModel)
        {
            string videoFileUrl = string.Empty;

            var innerHtmlJson = JsonConvert.DeserializeObject<JObject>(innerHtml);

            try
            {
                foreach (var section in innerHtmlJson.Children())
                {
                    if (section.Path.Equals("request", StringComparison.OrdinalIgnoreCase))
                    {
                        var sections = section.Children()["files"]["progressive"].Children();
                        var highestQuality = 0;

                        foreach (var profile in sections)
                        {
                            var quality = profile["height"].Value<int>();

                            if (quality < highestQuality)
                            {
                                continue;
                            }

                            videoFileUrl = profile["url"].Value<string>();
                            vimeoVideoModel.Quality = profile["quality"].Value<string>();
                            highestQuality = quality;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                return videoFileUrl;
            }

            return videoFileUrl;
        }
    }
}
