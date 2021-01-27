using System;
using System.IO;
using CommandLine;
using System.Linq;
using System.Diagnostics;
using SixLabors.ImageSharp;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Formats.Jpeg;
using System.Net;
using System.Text.RegularExpressions;
using System.Globalization;

namespace WeebCli
{
    class Program
    {
        static readonly Dictionary<string, string> WeebCliDeps = Deps.GetPaths();
        private static readonly string AppRoot = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly()?.Location);
        private static readonly string[] AcceptedExtensions = { "mkv", "mp4", "m4v" };
        
        // ReSharper disable UnusedAutoPropertyAccessor.Local

        [Verb("update", HelpText = "Installs ffmpeg and mp4box")]
        private class InstallOptions
        {

        }
        
        private enum DashFormat
        {
            X264,
            VP9
        }
        [Verb("dashgen", HelpText = "Generates a DASH video")]
        private class DashGenOptions
        {
            [Option('i', "input", Required = true, HelpText = "File to generate the DASH video from")]
            public string Input { get; set; }

            /*
            [Option('m',"mode", Required = true,HelpText = "What kind of DASH to generate. H264 supports NVenc, but vp9 can be more efficient.")]
            public DashFormat Mode { get; set; }
            */

            [Value(0, Min = 1, Max = 10)]
            public IEnumerable<int> Formats { get; set; }

            [Option('o', "output-dir", HelpText = "Directory in which to output", Default = "")]
            public string OutputDir { get; set; }

            
            [Option('n', "nvenc", HelpText = "Use NVENC for encoding")]
            public bool Accel { get; set; }
            

            private int _concurrency;
            [Option('c', "concurrency", HelpText = "How many instances of ffmpeg to run at once. Nvenc only allows 2 streams at once on consumer GPUs.", Default = 1)]
            public int Concurrency
            {
                get => _concurrency;
                // ReSharper disable UnusedMember.Local
                set
                // ReSharper restore UnusedMember.Local
                {
                    if (value < 1 || value > 10)
                    {
                        Console.WriteLine("Value out of range: Concurrency must be atleast 1 and smaller than 10");
                        Environment.Exit(4);
                    }
                    _concurrency = value;
                }
            }
        }

        [Verb("pregen", HelpText = "Generates previews")]
        private class PreGenOptions
        {

            [Option('o', "output", Default = "pre%n.jpg", HelpText = "Output filename. Format must be \"path/to/folder/file%n.jpg\" %n gets replaced with the frame number.")]
            public string Output { get; set; }

            [Option('q', "jpeg-quality", Default = 20, HelpText = "Sets the output jpeg quality.")]
            public int Quality { get; set; }
            [Option('i', "input", Required = true, HelpText = "File to open/generate previews for")]
            public string Input { get; set; }

            [Option('f', "frequency", Default = "1", Required = false, HelpText = "Preview frequency. -f 1/2 means that one frame will be previewed for 2 seconds.")]
            public string Frequency { get; set; }

            [Option('w', "width", Default = -2, Required = false, HelpText = "Preview frame width. Use -2 to keep aspect ratio.")]
            public int Width { get; set; }

            [Option('h', "height", Default = 60, Required = false, HelpText = "Preview frame height. Use -1 to keep aspect ratio.")]
            public int Height { get; set; }
        }
        // ReSharper restore UnusedAutoPropertyAccessor.Local

        static int Main(string[] args)
        {

            #region splash
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.Write("┬ ┬┌─┐┌─┐┌┐ ");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╔═╗╦  ╦");
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.Write("│││├┤ ├┤ ├┴┐");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("║  ║  ║");
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.Write("└┴┘└─┘└─┘└─┘");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("╚═╝╩═╝╩");
            Console.ResetColor();
            #endregion

            return Parser.Default.ParseArguments<PreGenOptions, DashGenOptions, InstallOptions>(args).MapResult(
                (PreGenOptions opts) => PreviewGen(opts),
                (DashGenOptions opts) => DashGen(opts),
                (InstallOptions opts) => Install(opts),
                errs => 1
            );
        }

        private static int PreviewGen(PreGenOptions opts)
        {
            //Console.WriteLine(opts.Input);
            if (!File.Exists(opts.Input)) { Console.WriteLine("File does not exist"); return 2; }
            if (!AcceptedExtensions.Contains(opts.Input.Split('.').Last())) { Console.WriteLine("File format not supported"); return 3; }

            var ffmpeg = Process.Start(
               new ProcessStartInfo()
               {
                   WorkingDirectory = Environment.CurrentDirectory,
                   UseShellExecute = false,
                   FileName = WeebCliDeps.GetFilename("ffmpeg"),
                   RedirectStandardOutput = true,
                   Arguments = $"-hide_banner -loglevel error -stats -i {opts.Input.PathFix()} -vf \"scale={opts.Width}:{opts.Height}, fps={opts.Frequency}\" -c:v png -f image2pipe -"
               });

            var bytes = ReadStdOut(ffmpeg);

            var images = PngParser.ParseFrames(bytes);

            WriteOutput(images, opts);

            return 0;
        }

        private static int DashGen(DashGenOptions opts)
        {
            Console.WriteLine("DASHGEN");
            if (!File.Exists(opts.Input)) { Console.WriteLine("File does not exist"); return 2; }
            if (!AcceptedExtensions.Contains(opts.Input.Split('.').Last())) { Console.WriteLine("File format not supported"); return 3; }
            /* FFPROBE stuff (currently not used)
            var ffprobeInfo = Process.Start(
                new ProcessStartInfo()
                {
                    WorkingDirectory = Environment.CurrentDirectory,
                    UseShellExecute = false,
                    FileName = WeebCliDeps.GetFilename("ffprobe"),
                    RedirectStandardError = true,
                    Arguments = $"-hide_banner \"{opts.Input}\""
                });
            string info = ffprobeInfo.StandardError.ReadToEnd();

            ffprobeInfo.WaitForExit();

            var rgx = new Regex(@"(\d{1,3}\.\d{0,2}) fps");
            double fps = double.Parse(rgx.Match(info).Groups[1].Value, CultureInfo.InvariantCulture);
            Console.WriteLine("framerate: {0} fps", fps);
            */
            int takePos = 0;
            Process[] activeEncodes = new Process[opts.Concurrency];

            while (takePos < opts.Formats.Count())
            {
                for (int i = 0; i < activeEncodes.Length; i++)
                {
                    if (activeEncodes[i] != null && !activeEncodes[i].HasExited)
                    {
                        //do nothing
                    }
                    else
                    {
                        //var copts = JsonToFfmpeg.GetFfmpegArgs(opts.Formats.ElementAt(takePos));
                        var quality = opts.Formats.ElementAt(takePos);
                        var outName = (opts.Input.Split('.')?[0] ?? "unk") + quality.ToString();
                        activeEncodes[i] = Process.Start(new ProcessStartInfo()
                        {
                            WorkingDirectory = Environment.CurrentDirectory,
                            UseShellExecute = false,
                            FileName = WeebCliDeps.GetFilename("ffmpeg"),
                            //Arguments = $"-i \"{opts.Input}\" -strict -2 -c:a opus -vf \"scale=-2:{copts.Quality}\" -c:v libvpx-vp9 -profile:v 0 -threads 16 -quality realtime -tile-columns 8 -speed 10 -keyint_min 72 -g 72 -tile-columns 4 -frame-parallel 1 -speed 1 -auto-alt-ref 1 -lag-in-frames 25 -b:v {copts.Bitrate} -y {copts.Name}.webm",
                            //Arguments = $"-vaapi_device -y -hide_banner -stats -i \"{opts.Input}\" -s {copts.Size} -c:v vp9_vaapi"
                            //--crf 18 --level 3.1 --preset veryslow --tune anime --keyint 240 --min-keyint 24 --ref 6 --bframes 9
                            //Arguments = $"-y -hide_banner -stats -i \"{opts.Input}\" -s {copts.Size} -c:v {(opts.Accel ? "h264_nvenc" : "libx264")} -f mp4 -crf 18 -level 3.1 -preset veryslow -tune anime -keyint 240 -min-keyint 24 -ref 6 -bframes 9 -bf 2 -g 90 {(copts.Audio ? $"-c:a aac -b:a {copts.AudioBitrate} -ar {copts.AudioRate}" : "-an")} {Path.Join(opts.OutputDir, copts.Name + ".mp4")}"
                            Arguments = $"-y -hide_banner -stats -i \"{opts.Input}\" -strict -2 -c:a opus -c:v {(opts.Accel ? "h264_nvenc" : "libx264")} -f matroska -vf \"scale=-2:{quality}\" -crf 18 -level 3.1 -preset veryslow -tune anime -bf 2 -g 90 {Path.Join(opts.OutputDir, outName + ".mp4")}"
                        });
                        Console.WriteLine(activeEncodes[i].StartInfo.Arguments);
                        takePos++;
                    }

                }
            }
            while (true)
            {                        //wait for all ffmpegs to finish
                var exited = 0;
                foreach (var t in activeEncodes)
                {
                    if (t == null) exited++;
                    else if (t.HasExited)
                        exited++;
                }
                if (exited == activeEncodes.Length)
                    break;
            }
            //run shaka

            return 0;
        }

        private static int Install(InstallOptions opts)
        {
            var depsPath = Path.Combine(AppRoot, "deps/");
            if (Directory.Exists(depsPath))
            {
                Console.WriteLine("Deleting old dependencies...");
                Directory.Delete(depsPath, true);
            }
            Console.WriteLine("Created deps directory...");
            Directory.CreateDirectory(depsPath);
            string platform = null;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) platform = "windows";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) platform = "linux";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) platform = "macos";

            if (platform == null) return 0;
            string download = $"http://ya.yeet.si/weebcli-deps-{platform}.zip",
                zipPath = Path.Combine(depsPath, "deps.zip");

            Console.WriteLine("Downloading {0} ...", download);
            Downloader.DownloadFile(download,zipPath);
            Console.WriteLine("Decompressing deps.zip ...");
            System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, depsPath);

            Console.WriteLine("Updated!");

            var deps = Deps.GetPaths();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Executables:");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            foreach (var (key, value) in deps)
            {
                Console.WriteLine("{0,12} : {1}", key, value);
            }
            Console.ResetColor();

            return 0;
        }

        private static FList<byte> ReadStdOut(Process p)
        {
            var bytes = new FList<byte>();
            while (true)
            {
                var a = p.StandardOutput.BaseStream.ReadByte();
                if (a != -1) bytes.Add((byte)a);
                if (a == -1 && p.HasExited) break;
            }
            return bytes;
        }

        private static void WriteOutput(List<Image> images, PreGenOptions opts)
        {
            int w = images[0].Width, h = images[0].Height;
            var enc = new JpegEncoder() { Quality = opts.Quality };
            for (int i = 0; i < images.Count; i += 100)
            {
                var img = new Image<Rgb24>(w * 10, h * 10);

                for (int y = 0; y < 10 && i + y * 10 < images.Count; y++)
                    for (int x = 0; x < 10 && i + y * 10 + x < images.Count; x++)
                    {
                        img.Mutate(im => im.DrawImage(images[i + y * 10 + x], new Point(x * w, y * h), 1f));
                    }
                img.SaveAsJpeg(opts.Output.Replace("%n", i.ToString()), enc);
                Console.WriteLine("Wrote map: {0}", opts.Output.Replace("%n", i.ToString()));
            }
        }
    }

    internal static class Extensions
    {
        public static string PathFix(this string a)
        {
            if (!a.Contains(' ')) return a;
            if (a.StartsWith('"') && a.EndsWith('"')) return a;
            return '"' + a + '"';
        }
    }

}
