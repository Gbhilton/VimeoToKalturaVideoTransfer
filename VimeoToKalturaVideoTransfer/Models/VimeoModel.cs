using System.Collections.Generic;
using System.Xml.Serialization;

namespace VimeoToKalturaVideoTransfer.Models
{
    public class VimeoModel
    {
        public string JSON { get; set; }
        public int Total { get; set; }
        public int CurrentPageNumber { get; set; } = 1;
        public bool CallAPI { get; set; } = true;
        public List<VimeoVideoModel> VimeoVideos { get; set; } = new List<VimeoVideoModel>();
    }

    public class VimeoVideoModel
    {
        public string Name { get; set; }
        public int Duration { get; set; }
        public string Description { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string Quality { get; set; }
        public string VideoFileURL { get; set; }
        public string CaptionURI { get; set; }
        public List<Caption> Captions { get; set; }
        public List<Picture> Pictures { get; set; }

        public VimeoVideoModel() { }

        public VimeoVideoModel(string name, int duration, int width, int height, string videoFileURL, string description)
        {
            Name = name;
            Duration = duration;
            Width = width;
            Height = height;
            VideoFileURL = videoFileURL;
            Description = description;
        }
    }

    public class Caption
    {
        public string URI { get; set; }
        public string URL { get; set; }
        public string Name { get; set; }
        public string Language { get; set; }
        public string DisplayLanguage { get; set; }
        public string Active { get; set; }
        public bool IsDefault { get; set; }

        public Caption() { }
    }

    public class Picture
    {
        public string URL { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public bool IsDefault { get; set; }

        public Picture() { }

        public Picture(string url, int width, int height, bool isDefault = true)
        {
            URL = url;
            Width = width;
            Height = height;
            IsDefault = isDefault;
        }
    }
}
