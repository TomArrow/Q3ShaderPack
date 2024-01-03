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
            int argIndex = 0;
            int folderIndex = 0;
            string bspFile = null;
            string mapFile = null;
            string shaderDirectory = null;
            string shaderExcludeDirectory = null;
            string outputDirectory = null;
            while (argIndex < args.Length)
            {
                string argument = args[argIndex++];
                if (argument.EndsWith(".bsp", StringComparison.InvariantCultureIgnoreCase))
                {
                    bspFile = argument;
                } else if (argument.EndsWith(".map", StringComparison.InvariantCultureIgnoreCase))
                {
                    mapFile = argument;
                }
                else
                {
                    int folderType = folderIndex;
                    if (argument.StartsWith("out:", StringComparison.InvariantCultureIgnoreCase))
                    {
                        argument = argument.Substring("out:".Length);
                        folderType = 2;
                    } else if (argument.StartsWith("shad:", StringComparison.InvariantCultureIgnoreCase))
                    {
                        argument = argument.Substring("shad:".Length);
                        folderType = 0;
                    } else if (argument.StartsWith("exshad:", StringComparison.InvariantCultureIgnoreCase))
                    {
                        argument = argument.Substring("exshad:".Length);
                        folderType = 1;
                    }
                    else
                    {
                        folderIndex++;
                    }
                    switch (folderType) {
                        case 0:
                            shaderDirectory = argument;
                            break;
                        case 1:
                            shaderExcludeDirectory = argument;
                            break;
                        case 2:
                            outputDirectory = argument;
                            break;
                    }
                }
            }
            List<string> shadFiles = new List<string>();
            List<string> shadExcludeFiles = new List<string>();
            shadFiles.AddRange(crawlDirectory(shaderDirectory));
            if(shaderExcludeDirectory != null) { 
                shadExcludeFiles.AddRange(crawlDirectory(shaderExcludeDirectory));
            }
            //Dictionary<string, string> shaderFiles = new Dictionary<string, string>();
            Dictionary<string, string> parsedShaders = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            Dictionary<string, string> parsedExcludeShaders = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            foreach (string file in shadFiles)
            {
                string basename = Path.GetFileNameWithoutExtension(file);
                string extension = Path.GetExtension(file).ToLowerInvariant();
                if (extension == ".shader")
                {
                    ParseShader(file, ref parsedShaders);
                    //shaderFiles[basename] = file;
                }
            }
            foreach (string file in shadExcludeFiles)
            {
                string basename = Path.GetFileNameWithoutExtension(file);
                string extension = Path.GetExtension(file).ToLowerInvariant();
                if (extension == ".shader")
                {
                    ParseShader(file, ref parsedExcludeShaders);
                    //shaderFiles[basename] = file;
                }
            }
            
            HashSet<string> usedShadersHashSet = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            if (mapFile != null)
            {
                HashSet<string> mapShaders = ParseMap(mapFile);
                foreach(string shader in mapShaders)
                {
                    usedShadersHashSet.Add(shader);
                }
            }
            if (bspFile != null)
            {
                string[] bspShaders = BSPHelper.GetShaders(bspFile);
                foreach(string shader in bspShaders)
                {
                    usedShadersHashSet.Add(shader);
                }
            }
            
            
            string[] usedShaders = new string[usedShadersHashSet.Count];
            usedShadersHashSet.CopyTo(usedShaders);
            Array.Sort(usedShaders);

            StringBuilder sb = new StringBuilder();

            foreach (string shader in usedShaders)
            {
                if (parsedShaders.ContainsKey(shader) && !parsedExcludeShaders.ContainsKey(shader))
                {
                    sb.Append("\n");
                    sb.Append(shader);
                    sb.Append("\n");
                    sb.Append(parsedShaders[shader]);
                    sb.Append("\n");
                    sb.Append("\n");
                }
            }

            string compiledShaders = sb.ToString();



            if (outputDirectory != null)
            {
                Directory.CreateDirectory(Path.Combine(outputDirectory,"shaders"));
                File.WriteAllText(Path.Combine(outputDirectory, "shaders",$"{Path.GetFileNameWithoutExtension(bspFile == null ? mapFile : bspFile)}.shader"), compiledShaders);

                // Copy special image files like lightImage and editorimage and .md3 models and audio files from .map alongside normal images
                HashSet<string> mapModels = mapFile == null ? new HashSet<string>(StringComparer.InvariantCultureIgnoreCase) : ParseMapModels(mapFile);
                HashSet<string> shaderImages = ParseShaderImages(compiledShaders);
                foreach (string shader in usedShaders)
                {
                    shaderImages.Add(shader);
                }

                HashSet<string> extensionLessShaderImages = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
                foreach (string shader in shaderImages)
                {
                    extensionLessShaderImages.Add(Path.Combine(Path.GetDirectoryName(shader),Path.GetFileNameWithoutExtension(shader)));
                }

                List<string> files = new List<string>();
                List<string> excludeFiles = new List<string>();
                files.AddRange(crawlDirectory(Path.Combine(shaderDirectory,"..")));
                if (shaderExcludeDirectory != null)
                {
                    excludeFiles.AddRange(crawlDirectory(Path.Combine(shaderExcludeDirectory, "..")));
                }

                HashSet<string> excludeFilesNormalized = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
                foreach (string file in excludeFiles)
                {
                    string normalizedPath = Path.GetRelativePath(Path.Combine(shaderExcludeDirectory, ".."), file);
                    excludeFilesNormalized.Add(normalizedPath);
                }

                HashSet<string> filesToCopy = new HashSet<string>();

                foreach(string file in files)
                {
                    string normalizedPath = Path.GetRelativePath(Path.Combine(shaderDirectory, ".."), file);
                    string extensionLessName = Path.Combine(Path.GetDirectoryName(normalizedPath), Path.GetFileNameWithoutExtension(normalizedPath));
                    if ((extensionLessShaderImages.Contains(extensionLessName) || mapModels.Contains(extensionLessName)) && !excludeFilesNormalized.Contains(normalizedPath))
                    {
                        filesToCopy.Add(file);
                    }
                }

                foreach(string file in filesToCopy)
                {
                    string normalizedPath = Path.GetRelativePath(Path.Combine(shaderDirectory, ".."), file);
                    string outPath = Path.Combine(outputDirectory,normalizedPath);
                    Directory.CreateDirectory(Path.GetDirectoryName(outPath));
                    if (File.Exists(outPath))
                    {

                        Console.WriteLine($"{outPath} already exists.");
                    } else
                    {

                        File.Copy(file, outPath);
                    }
                }

                Console.WriteLine($"{filesToCopy.Count}");
            } else
            {
                File.WriteAllText("packedShaders.shader", compiledShaders);
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




        }

        static Regex faceParseRegex = new Regex(@"(?<coordinates>(?<coordvec>\((?<vectorPart>\s*[-\d\.]+){3}\s*\)\s*){3})(?:\(\s*(\((?:\s*[-\d\.]+){3}\s*\)\s*){2}\))?\s*(?<texname>[^\s\n]+)\s*(?:\s*[-\d\.]+){3}", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static HashSet<string> ParseMap(string mapFile)
        {
            HashSet<string> shaders = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            string mapText = File.ReadAllText(mapFile);
            MatchCollection matches = faceParseRegex.Matches(mapText);
            foreach(Match match in matches)
            {
                shaders.Add("textures/"+match.Groups["texname"].Value);
            }
            return shaders;
        }

        static Regex modelParseRegex = new Regex(@"""(?:model|noise|awesomenoise)""\s*""(?<model>[^""]+)""", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static HashSet<string> ParseMapModels(string mapFile)
        {
            HashSet<string> models = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            string mapText = File.ReadAllText(mapFile);
            MatchCollection matches = modelParseRegex.Matches(mapText);
            foreach(Match match in matches)
            {
                models.Add(match.Groups["model"].Value);
            }
            return models;
        }

        static Regex shaderImageRegex = new Regex(@"\n[^\n]*?(?<paramName>(?<=\s)map|lightimage|editorimage|skyparms)[ \t]+(?<image>[^$][^\s\n]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static HashSet<string> ParseShaderImages(string shaderText)
        {
            HashSet<string> images = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            MatchCollection matches = shaderImageRegex.Matches(shaderText);
            foreach(Match match in matches)
            {
                bool isSkyParam = match.Groups["paramName"].Value.Equals("skyparms",StringComparison.InvariantCultureIgnoreCase);
                if (isSkyParam)
                {
                    string imgName = match.Groups["image"].Value.Trim();
                    images.Add($"{imgName}_ft");
                    images.Add($"{imgName}_bk");
                    images.Add($"{imgName}_lf");
                    images.Add($"{imgName}_rt");
                    images.Add($"{imgName}_up");
                    images.Add($"{imgName}_dn");
                } else
                {
                    images.Add(match.Groups["image"].Value);
                }
            }
            return images;
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
