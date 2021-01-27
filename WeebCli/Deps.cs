using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace WeebCli
{
    /// <summary>
    /// A class for managing downloaded dependencies.
    /// </summary>
    internal static class Deps
    {
        public static Dictionary<string, string> GetPaths()
        {
            var depsFolderPath = Path.Join(Path.GetDirectoryName(typeof(Program).Assembly.Location), "deps/");

            var indexPath = Path.Join(depsFolderPath, "index.weeb");
            if (!File.Exists(indexPath))
            {
                return null;
            }
            var lines = File.ReadAllLines(indexPath);

            return (from l in lines select l.Trim() into line where line.Length != 0 where line[0] != '#' && line.Contains('=') select line.Split('=') into tokens where tokens.Length == 2 where tokens[0].Trim().Length > 0 && tokens[1].Trim().Length > 0 select tokens).ToDictionary(tokens => tokens[0], tokens => tokens[1] != "[LOCAL]" ? Path.Combine(depsFolderPath, tokens[1]) : tokens[0]);
            //pog
        }
        public static string GetFilename(this Dictionary<string, string> dict, string key)
        {
            if (dict.Keys.Contains(key))
                return dict[key];
            
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("Missing dependency: {0}", key);
            Console.WriteLine("Try running weebcli update");
            Environment.Exit(-1);
            throw new FileNotFoundException(); //make sure the compiler doesnt scream at us for not returning
        }
    }
}
