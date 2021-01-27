using System.Text.Json;

namespace WeebCli
{
    internal static class JsonToFfmpeg
    {
        public static VideoSettings GetFfmpegArgs(string json)
        {
            var a = JsonSerializer.Deserialize<VideoSettings>(json);
            return a;
        }
    }
        public class VideoSettings
        {
            public string Name { get; set; }
            public string Quality { get; set; }
            public string Bitrate {get;set;}
        }
}
