using PCRE;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Q3ShaderPack
{
    class Program
    {
        static void Main(string[] args)
        {
            string mapFile = args[0];
            string shaderDirectory = args[1];
            List<string> shadFiles = new List<string>();
            shadFiles.AddRange(crawlDirectory(shaderDirectory));
            Dictionary<string, string> shaderFiles = new Dictionary<string, string>();
            Dictionary<string, string> parsedShaders = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            foreach (string file in shadFiles)
            {
                string basename = Path.GetFileNameWithoutExtension(file);
                string extension = Path.GetExtension(file).ToLowerInvariant();
                if (extension == ".shader")
                {
                    ParseShader(file, ref parsedShaders);
                    shaderFiles[basename] = file;
                }
            }

            HashSet<string> usedShadersHashSet = ParseMap(mapFile);
            string[] usedShaders = new string[usedShadersHashSet.Count];
            usedShadersHashSet.CopyTo(usedShaders);
            Array.Sort(usedShaders);

            StringBuilder sb = new StringBuilder();

            foreach (string shader in usedShaders)
            {
                if (parsedShaders.ContainsKey(shader))
                {
                    sb.Append("\n");
                    sb.Append(shader);
                    sb.Append("\n");
                    sb.Append(parsedShaders[shader]);
                    sb.Append("\n");
                    sb.Append("\n");
                }
            }



            //foreach (var kvp in processedShaders)
            //{
            //    sb.Append("\n");
            //    sb.Append(kvp.Key);
            //    sb.Append("\n");
            //    sb.Append(kvp.Value);
            //    sb.Append("\n");
            //    sb.Append("\n");
            //}

            File.WriteAllText("packedShaders.shader", sb.ToString());



        }

        static Regex faceParseRegex = new Regex(@"(?<coordinates>(?<coordvec>\((?<vectorPart>\s*[-\d\.]+){3}\s*\)\s*){3})(?:\(\s*(\((?:\s*[-\d\.]+){3}\s*\)\s*){2}\))?\s*(?<texname>[^\s\n]+)\s*(?:\s*[-\d\.]+){3}", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static HashSet<string> ParseMap(string mapFile)
        {
            HashSet<string> shaders = new HashSet<string>();
            string mapText = File.ReadAllText(mapFile);
            MatchCollection matches = faceParseRegex.Matches(mapText);
            foreach(Match match in matches)
            {
                shaders.Add("textures/"+match.Groups["texname"].Value);
            }
            return shaders;
        }

        private static void ParseShader(string file, ref Dictionary<string, string> shaderData)
        {
            string shaderText = File.ReadAllText(file);
            var otherMatches = PcreRegex.Matches(shaderText, @"(?<shaderName>[-_\w\d\/]+)?\s+(?:\/\/[^\n]+\s*)?(?<shaderBody>\{(?:[^\{\}]|(?R))*\})");
            foreach (var match in otherMatches)
            {
                shaderData[match.Groups["shaderName"].Value] = match.Groups["shaderBody"].Value;
            }

        }



        static string[] crawlDirectory(string dir)
        {
            if (!Directory.Exists(dir))
            {
                return new string[0];
            }
            List<string> filesAll = new List<string>();
            filesAll.AddRange(Directory.GetFiles(dir));
            string[] dirs = Directory.GetDirectories(dir);
            foreach (string subdir in dirs)
            {
                filesAll.AddRange(crawlDirectory(subdir));
            }

            return filesAll.ToArray();
        }
    }
}
