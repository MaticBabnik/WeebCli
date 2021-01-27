using System;
using System.Collections.Generic;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;

namespace WeebCli
{
    /// <summary>
    /// Code focused on splitting ffmpeg's piped image output into individual images.
    /// </summary>
    public static class PngParser
    {
        private static readonly byte[] PngHeader = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
        private static readonly byte[] PngEnd = { 73, 69, 78, 68 };
        private const ushort PngMinChunkSize = 12;
        /// <summary>
        /// Splits ffmpeg piped output into individual files
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        public static List<Image> ParseFrames(FList<byte> bytes)
        {
            var images = new List<Image>();
            var rb = bytes.array;

            Console.Write("Frames: {0}", images.Count);

            for (uint i = 0; i < rb.Length; i++) //this loop exist just in case ffmpeg puts trash between frames/files.
            {
                if (!ArrayCompare(rb, i, i + PngHeader.Length, PngHeader)) continue;
                Console.CursorLeft = 0;
                Console.Write("Frames: {0}", images.Count);

                uint currentPngStart = i;

                i += (uint)PngHeader.Length;

                while (i < rb.Length) //this loop walks the whole png untill Iend chunk
                {
                    uint chunkSize = PngMinChunkSize;

                    chunkSize += (uint)(rb[i] << 24 | rb[i + 1] << 16 | rb[i + 2] << 8 | rb[i + 3]);

                    if (chunkSize == 12 && ArrayCompare(rb, i + 4, i + 8, PngEnd))
                    {
                        images.Add(ImageFromBytes(ref rb, currentPngStart, i));

                        i += chunkSize - 1; //-1 compensates for the +1 at the end of top level for loop

                        break;
                    }
                    i += chunkSize;
                }
            }
            Console.WriteLine();
            return images;
        }
        /// <summary>
        /// Parses an image from raw byte data.
        /// </summary>
        /// <param name="bytes">Byte array containing at least one png.</param>
        /// <param name="start">First byte position of a PNG file.</param>
        /// <param name="end">Last byte position of a PNG file.</param>
        /// <returns>ImageSharp image</returns>
        private static Image ImageFromBytes(ref byte[] bytes, uint start, uint end)
        {
            var data = new byte[end - start];
            Array.Copy(bytes, start, data, 0, end - start);
            return Image.Load(data, new PngDecoder());
        }
        /// <summary>
        /// Converts 4 bytes from an array to uint
        /// </summary>
        /// <param name="data">Source array</param>
        /// <param name="offset">Most significant byte position</param>
        /// <returns>unsigned int</returns>
        public static uint BytesToUint(byte[] data, int offset = 0)
        {
            uint output = 0;
            for (int i = offset, shift = 24; i < offset + 4; i++, shift -= 8)
                output += (uint)(data[i] << shift);

            return output;
        }
        /// <summary>
        /// Compares a section of an array with another array.
        /// </summary>
        /// <typeparam name="T">The type of both arrays. Must implement IComparable</typeparam>
        /// <param name="a">First array</param>
        /// <param name="start">First array section start</param>
        /// <param name="end">First array section end</param>
        /// <param name="b">Second array</param>
        /// <returns>True if equal</returns>
        private static bool ArrayCompare<T>(T[] a, long start, long end, T[] b) where T : IComparable
        {
            for (long i = start; i < end; i++)
            {
                if (!a[i].Equals(b[i - start])) return false;
            }
            return true;
        }
    }
}
