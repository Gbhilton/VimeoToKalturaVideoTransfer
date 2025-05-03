using Kaltura.Enums;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using VimeoToKalturaVideoTransfer.Models;
using static System.Net.Mime.MediaTypeNames;

namespace VimeoToKalturaVideoTransfer.Helper
{
    public class VideoTransferHelper
    {
        public string VimeoAccessToken { get; set; }
        public string VimeoUserId { get; set; }
        public string SchoolName { get; set; }
        public int PartnerId { get; set; } = 0; //INSERT PARTNER ID HERE
        public string SecretKey { get; set; } = "INSERT SECRET KEY HERE";
        public string KalturaUserId { get; set; } = "INSERT KALTURA USER ID HERE";
        public SessionType SessionType { get; set; } = SessionType.ADMIN;
        public int Expiry { get; set; } = 86400;
        public string Priveleges { get; set; } = string.Empty;
        public VimeoHelper VimeoHelper { get; set; }
        public int TotalFailedVideos { get; set; } = 0;

        public VideoTransferHelper(string accessToken, string vimeoUserId, string schoolName)
        {
            VimeoAccessToken = accessToken;
            VimeoUserId = vimeoUserId;
            SchoolName = schoolName;
            VimeoHelper = new VimeoHelper(accessToken, vimeoUserId);
        }

        public string TransferVideoFromVimeoToKaltura()
        {
            bool downloadSuccess;
            bool uploadSuccess = false;
            string videoUploadFailList = string.Empty;
            VimeoModel VimeoModel = new VimeoModel();

            downloadSuccess = DownloadVimeoVideos(VimeoModel);

            if (downloadSuccess)
            {
                if (ConvertVimeoToKalturaXML(VimeoModel))
                {
                    uploadSuccess = UploadVideosToKaltura();

                    if (uploadSuccess)
                    {
                        videoUploadFailList = LogInvalidQualityVideos(VimeoModel.VimeoVideos);
                    }
                }
            }

            return string.Concat("Download Success: \n", downloadSuccess, "\nUpload Success: \n", uploadSuccess, "\nTotal Videos: \n", VimeoModel.VimeoVideos.Count(), 
                "\nTotal Videos Failed to Upload: \n", TotalFailedVideos.ToString(), "\n", videoUploadFailList, "\n");
        }

        private bool DownloadVimeoVideos(VimeoModel vimeoModel)
        {
            bool downloadSuccess = false;

            try
            {
                if (VimeoHelper.DownloadVideos(vimeoModel))
                {
                    downloadSuccess = VimeoHelper.ParseJSONVimeoDownload(vimeoModel);

                    if (downloadSuccess && vimeoModel.CallAPI)
                    {
                        DownloadVimeoVideos(vimeoModel);
                    }
                }
            }
            catch (Exception e)
            {
                return downloadSuccess;
            }

            return downloadSuccess;
        }

        public bool ConvertVimeoToKalturaXML(VimeoModel vimeoModel)
        {
            bool convertToKalturaXML = false;

            try
            {       
                List<XElement> items = CreateItemList(vimeoModel);

                XElement doc = new XElement("mrss", 
                               new XElement("channel",
                               items
                ));

                var filePath = Directory.GetCurrentDirectory();
                var fileName = string.Concat("KalturaXMLImport_", SchoolName.Replace(" ", ""), ".xml");
                var fullFilePath = string.Concat(filePath, "\\", fileName);

                if (File.Exists(fullFilePath))
                {
                    File.Delete(fullFilePath);
                }

                doc.Save(fullFilePath);

                convertToKalturaXML = true;
            }
            catch (Exception e)
            {
                return convertToKalturaXML;
            }

            return convertToKalturaXML;
        }

        private List<XElement> CreateItemList(VimeoModel vimeoModel)
        {
            List<XElement> items = new List<XElement>();

            foreach (var video in vimeoModel.VimeoVideos.Where(x => !string.IsNullOrEmpty(x.VideoFileURL)))
            {
                List<XElement> thumbnails = CreateThumbnailElement(video);
                List<XElement> subTitles = CreateSubTitleElement(video);

                XElement item = new XElement("item",
                            new XElement("action", "add"),
                            new XElement("type", "1"),
                            new XElement("userId", PartnerId),
                            new XElement("name", video.Name),
                            new XElement("description", video.Description),
                            new XElement("categories", 
                                new XElement("category", string.Concat("MediaSpace>site>channels>", SchoolName)),
                                new XElement("category", string.Concat("MediaSpace>", "unlisted"))),
                            new XElement("media", 
                                new XElement("mediaType", 1)),
                            new XElement("contentAssets", 
                                new XElement("content", 
                                new XElement("urlContentResource", new XAttribute("url", video.VideoFileURL)))),
                            new XElement("thumbnails",
                                thumbnails),
                            new XElement("subTitles",
                                subTitles)
                );

                items.Add(item);
            }

            return items;
        }

        private List<XElement> CreateThumbnailElement(VimeoVideoModel video)
        {
            List<XElement> thumbnails = new List<XElement>();

            foreach (var picture in video.Pictures)
            {
                XElement item = new XElement("thumbnail", new XAttribute("isDefault", picture.IsDefault),
                    new XElement("urlContentResource", new XAttribute("url", picture.URL)));
                thumbnails.Add(item);
            }

            return thumbnails;
        }

        private List<XElement> CreateSubTitleElement(VimeoVideoModel video)
        {
            List<XElement> subTitles = new List<XElement>();

            foreach (var caption in video.Captions)
            {
                XElement item = new XElement("subTitle", new XAttribute("isDefault", caption.IsDefault), new XAttribute("format", 3), new XAttribute("lang", caption.Language),
                        new XElement("tags",
                        new XElement("tag", caption.Language)),
                        new XElement("urlContentResource", new XAttribute("url", caption.URL)));
                subTitles.Add(item);
            }

            return subTitles;
        }

        private bool UploadVideosToKaltura()
        {
            bool uploadSuccess = false;

            try
            {
                KalturaHelper kalturaHelper = new KalturaHelper(PartnerId, SecretKey, KalturaUserId, SessionType.ADMIN, Expiry, Priveleges);

                uploadSuccess = kalturaHelper.UploadVideos(SchoolName);
            }
            catch (Exception e)
            {
                return uploadSuccess;
            }

            return uploadSuccess;
        }

        private string LogInvalidQualityVideos(List<VimeoVideoModel> videos)
        {
            string videoUploadFailList = string.Empty;
            TotalFailedVideos = 0;

            var filePath = Directory.GetCurrentDirectory();
            var fileName = "InvalidVideoQualityLog.csv";
            var fullFilePath = string.Concat(filePath, "\\", fileName);

            try
            {
                if (!File.Exists(fullFilePath))
                {
                    string headers = "School Name, Video Name, Reason" + Environment.NewLine;
                    File.WriteAllText(fullFilePath, headers);
                }

                foreach (var video in videos)
                {
                    string uploadFailReason = string.Empty;

                    if (string.IsNullOrEmpty(video.VideoFileURL))
                    {
                        TotalFailedVideos++;
                        uploadFailReason = "There was no video file URL available.";
                    }
                    else if (video.Height < 720)
                    {
                        uploadFailReason = string.Concat("Highest video quality was only ", video.Quality, ".");
                    }

                    if (!string.IsNullOrEmpty(uploadFailReason))
                    {
                        File.AppendAllText(fullFilePath, string.Concat(SchoolName, ",", video.Name, ",", uploadFailReason, Environment.NewLine));
                        videoUploadFailList = string.Concat(videoUploadFailList, video.Name, ": ", uploadFailReason, "\n");
                    }
                }
            }
            catch(Exception e)
            {
            }

            return videoUploadFailList;
        }
    }
}
