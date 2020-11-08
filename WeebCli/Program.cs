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



namespace WeebCli
{
    class Program
    {

        private static readonly string AppRoot = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
        private static readonly string[] AcceptedExtensions = { "mkv", "mp4", "m4v" };

        /// <summary>
        /// Returns "\ffmpeg.exe" on windows and "\ffmpeg" on unix
        /// </summary>
        public static string FfmpegName
        {
            get
            {
                return RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? @"ffmpeg\ffw.exe" : RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? @"ffmpeg/ffl" : @"ffmpeg/ffm";
            }
        }

        [Verb("dashgen", HelpText = "Generates a DASH video")]
        class DashGenOptions
        {
            [Option('i', "input", Required = true, HelpText = "File to generate the DASH video from")]
            public string Input { get; set; }

            [Value(0, Min = 1, Max = 10)]
            public IEnumerable<string> Formats { get; set; }

            [Option('o', "output-dir", HelpText = "Directory in which to output",Default = "")]
            public string OutputDir { get; set; }

            [Option('n', "nvenc", HelpText = "Use NVENC for encoding")]
            public bool Accel { get; set; }

        }

        [Verb("pregen", HelpText = "Generates previews")]
        class PreGenOptions
        {

            [Option('o', "output",Default ="pre%n.jpg", HelpText = "Output filename. Format must be \"path/to/folder/file%n.jpg\" %n gets replaced with the frame number.")]
            public string Output { get; set; }

            [Option('q', "jpeg-quality",Default =20,HelpText ="Sets the output jpeg quality.")]
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

        static int Main(string[] args)
        {

            #region splash
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("_________________________________");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("_ _ _ ____ ____ ___  ____ _    _ \n| | | |___ |___ |__] |    |    | \n|_|_| |___ |___ |__] |___ |___ | ");
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("_________________________________\n");
            Console.ResetColor();
            #endregion

            if (!File.Exists(Path.Combine(AppRoot, FfmpegName)))
            {
                PrintAndExit("No FFMPEG", ConsoleColor.Red, -1);
            }

            return CommandLine.Parser.Default.ParseArguments<PreGenOptions, DashGenOptions>(args).MapResult(
                (PreGenOptions opts) => PreviewGen(opts),
                (DashGenOptions opts) => DashGen(opts),
                errs => 1
            );
        }
        static int PreviewGen(PreGenOptions opts)
        {
            Console.WriteLine(opts.Input);
            if ( !File.Exists(opts.Input) ) { Console.WriteLine("File does not exist"); return 2; }
            if (!AcceptedExtensions.Contains(opts.Input.Split('.').Last())) { Console.WriteLine("File format not supported"); return 3; }

            var ffmpeg = Process.Start(
               new ProcessStartInfo()
               {
                   WorkingDirectory = Environment.CurrentDirectory,
                   UseShellExecute = false,
                   FileName = Path.Combine(AppRoot, FfmpegName),
                   RedirectStandardOutput = true,
                   Arguments = $"-hide_banner -loglevel error -stats -i {opts.Input.PathFix()} -vf \"scale={opts.Width}:{opts.Height}, fps={opts.Frequency}\" -c:v png -f image2pipe -"
               });

            var bytes = ReadStdOut(ffmpeg);

            var imgs = PngParser.ParseFrames(bytes);

            WriteOutput(imgs,opts);

            return 0;
        }
        static int DashGen(DashGenOptions opts)
        {
            Console.WriteLine("DASHGEN");
            throw new NotImplementedException(nameof(DashGen));
            return 0;
        }
        static FList<byte> ReadStdOut(Process p)
        {
            FList<byte> bytes = new FList<byte>();
            while (true)
            {
                int a = p.StandardOutput.BaseStream.ReadByte();
                if (a != -1) bytes.Add((byte)a);
                if (a == -1 && p.HasExited) break;
            }
            return bytes;
        }

        static void WriteOutput(List<Image> images,PreGenOptions opts)
        {
            int w = images[0].Width, h = images[0].Height;
            JpegEncoder enc = new JpegEncoder() { Quality = opts.Quality };
            for (int i = 0; i < images.Count; i += 100)
            {
                var img = new Image<Rgb24>(w * 10, h * 10);

                for (int y = 0; y < 10 && i + y * 10 < images.Count; y++)
                    for (int x = 0; x < 10 && i + y * 10 + x < images.Count; x++)
                    {
                        img.Mutate(im => im.DrawImage(images[i + y * 10 + x], new Point(x * w, y * h), 1f));
                    }
                img.SaveAsJpeg(opts.Output.Replace("%n",i.ToString()),enc);
                Console.WriteLine("Wrote map: {0}", opts.Output.Replace("%n", i.ToString()));
            }
        }

        /// <summary>
        /// Prints colored text to stdout and exits.
        /// </summary>
        /// <param name="text">The text to display</param>
        /// <param name="color">Text color</param>
        /// <param name="code">Return code</param>
        static void PrintAndExit(string text, ConsoleColor color = ConsoleColor.White, int code = 0)
        {
            Console.ForegroundColor = color;
            Console.WriteLine(text);
            Console.ResetColor();
            Environment.Exit(code);

        }

    }
    static class Extensions
    {
        public static string PathFix(this string a)
        {
            if (!a.Contains(' ')) return a;
            if (a.StartsWith('"') && a.EndsWith('"')) return a;
            return '"' + a + '"';
        }
    }

}
