using System;
using System.Net;
using System.Text;
using System.Threading;

namespace WeebCli
{
    internal static class Downloader
    {
        private static int _bytesRec, _bytesTotal;
        private static bool _done;
        
        public static void DownloadFile(string url, string filename)
        {
            _bytesRec = 0;
            _bytesTotal = 1;
            _done = false;
            using var wc = new WebClient();
            Thread downloadThread = new Thread(() =>
            {
                wc.DownloadFileCompleted += (sender, args) =>
                {
                    _done = true;
                };
                wc.DownloadProgressChanged += (sender, args) =>
                {
                    _bytesTotal = (int) args.TotalBytesToReceive;
                    _bytesRec = (int) args.BytesReceived;
                };
                wc.DownloadFileAsync(new Uri(url), filename);
            });
            
            downloadThread.Start();

            while (!_done)
            {
                Thread.Sleep(100);
                Console.CursorLeft = 0;
                Console.Write(Utill.ProgressBar(_bytesRec,_bytesTotal));
            }
            Console.WriteLine();
        }
    }

    internal static class Utill
    {
        public static string ProgressBar(int val, int max)
        {
            int i = (int) ((double) val / max * 100);
            
            var sb = new StringBuilder(22);
            sb.Append('[');
            int fullChars = i / 5;
            for (int j = 0; j < fullChars; j++)
                sb.Append('█');
            if (20 - fullChars != 0)
            {
                if (i % 5 > 2)
                    sb.Append('▌');
                while (sb.Length < 21)
                {
                    sb.Append(' ');
                }
            }

            sb.Append(']');
            return $"{sb} {i,3}% ({val}/{max})";
        }
    }
}