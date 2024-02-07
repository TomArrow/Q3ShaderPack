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
        class ShaderDupe
        {
            public List<string> files = new List<string>();
            public List<string> bodies = new List<string>();
            public string chosenFile = null;
            public bool used = false;
        }

        // TODO Detect non power of 2 tex
        // TODO Detect not found files.
        static void Main(string[] args)
        {
            // Todo show shader dupes
            int argIndex = 0;
            int folderIndex = 0;
            string bspFile = null;
            string mapFile = null;
            string shaderDirectory = null;
            string shaderExcludeDirectory = null;
            string outputDirectory = null;
            bool ignoreShaderList = false;
            while (argIndex < args.Length)
            {
                string argument = args[argIndex++];
                if(argument.Equals("-ignoreShaderList",StringComparison.InvariantCultureIgnoreCase))
                {
                    ignoreShaderList = true;
                }
                else if (argument.EndsWith(".bsp", StringComparison.InvariantCultureIgnoreCase))
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

            Dictionary<string, ShaderDupe> shaderDuplicates = new Dictionary<string, ShaderDupe>(StringComparer.InvariantCultureIgnoreCase);

            HashSet<string> shaderListWhitelist = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            List<string> shadFiles = new List<string>();
            List<string> shadExcludeFiles = new List<string>();

            shadFiles.AddRange(crawlDirectory(shaderDirectory));
            if(shaderExcludeDirectory != null) { 
                shadExcludeFiles.AddRange(crawlDirectory(shaderExcludeDirectory));
            }

            // find shaderlist
            if (!ignoreShaderList)
            {
                foreach (string file in shadFiles)
                {
                    string basename = Path.GetFileNameWithoutExtension(file);
                    string extension = Path.GetExtension(file).ToLowerInvariant();
                    if (basename.ToLowerInvariant().Trim() == "shaderlist" && extension == ".txt")
                    {
                        string[] allowedShaderFiles = File.ReadAllLines(file);
                        foreach(string allowedShaderFile in allowedShaderFiles)
                        {
                            shaderListWhitelist.Add(allowedShaderFile);
                        }

                    }
                }
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
                    if((ignoreShaderList || shaderListWhitelist.Count == 0 || shaderListWhitelist.Contains(basename)))
                    {
                        ParseShader(file, ref parsedShaders, shaderDuplicates);
                    } else
                    {
                        Console.WriteLine($"Skipping {file}, not in shaderlist.txt");
                    }
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
                    // Check if : variants (:q3map) exist.
                    string q3mapVariant = $"{shader}:q3map";
                    if (parsedShaders.ContainsKey(q3mapVariant) && !parsedExcludeShaders.ContainsKey(q3mapVariant))
                    {
                        sb.Append("\n");
                        sb.Append(q3mapVariant);
                        sb.Append("\n");
                        sb.Append(parsedShaders[q3mapVariant]);
                        sb.Append("\n");
                        sb.Append("\n");
                        shaderDuplicates[q3mapVariant].used = true;
                    }

                    sb.Append("\n");
                    sb.Append(shader);
                    sb.Append("\n");
                    sb.Append(parsedShaders[shader]);
                    sb.Append("\n");
                    sb.Append("\n");
                    shaderDuplicates[shader].used = true;
                }
            }

            string compiledShaders = sb.ToString();

            StringBuilder dupesInfoString = new StringBuilder();
            int dupeCount = 0;
            foreach(var kvp in shaderDuplicates)
            {
                if(kvp.Value.used && kvp.Value.files.Count > 1)
                {
                    bool allSame = true;
                    int hashCompare = kvp.Value.bodies[0].GetHashCode();
                    for (int i = 1; i < kvp.Value.bodies.Count; i++)
                    {
                        if (kvp.Value.bodies[i].GetHashCode() != hashCompare)
                        {
                            allSame = false;
                            break;
                        }
                    }

                    if (!allSame)
                    {
                        dupesInfoString.Append($"\n\n\n\n\n{kvp.Key}:\n");
                        for (int i = 0; i < kvp.Value.files.Count; i++)
                        {
                            int bodyHash = kvp.Value.bodies[i].GetHashCode();
                            dupesInfoString.Append($"\n\n{kvp.Value.files[i]} (hash {bodyHash}, used: {kvp.Value.used}):\n{kvp.Value.bodies[i]}\n");
                        }
                        dupeCount++;
                    }
                }
            }
            if(dupesInfoString.Length > 0)
            {
                Console.WriteLine($"{dupeCount} shader dupes found.");
                File.WriteAllText("shaderDupesDebug.txt",dupesInfoString.ToString());
            }

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

                HashSet<string> extensionLessFiles = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
                foreach (string shader in shaderImages)
                {
                    extensionLessFiles.Add(Path.Combine(Path.GetDirectoryName(shader),Path.GetFileNameWithoutExtension(shader)));
                }
                foreach (string file in mapModels)
                {
                    extensionLessFiles.Add(Path.Combine(Path.GetDirectoryName(file),Path.GetFileNameWithoutExtension(file)));
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
                    if ((extensionLessFiles.Contains(extensionLessName)) && !excludeFilesNormalized.Contains(normalizedPath))
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

        private static void ParseShader(string file, ref Dictionary<string, string> shaderData, Dictionary<string, ShaderDupe> shaderDuplicates = null)
        {
            string shaderText = File.ReadAllText(file);

            // WIP: (?:^|\n)\s*(?<shaderName>(?:[-_\w\d:]|(?<!\/)\/)+)?\s*(?:\/\/[^\n]+\s*)*+(?<shaderBody>(?:\n\s*\{)(?:(?:\/\/[^\n]*)|[^\{\}]|(?R))*(?:\n\s*\}))
            // WIP: (?:^|\n)\s*(?<shaderName>(?:[-_\w\d:]|(?<!\/)\/)+)?\s*(?:\/\/[^\n]+\s*)*+(?<shaderBody>(?:[\n\r]+\s*\{)(?:(?:\/\/[^\n\r]*)|[^\{\}\/]|(?<!\/)\/(?!\/)|(?R))*(?:[\n\r]+\s*\}))
            // WIP: (?:^|\n)\s*(?<shaderName>(?:[-_\w\d:]|(?<!\/)\/)+)?\s*(?:\/\/[^\n]+\s*)*+(?<shaderBody>(?:\{)(?:(?:\/\/[^\n\r]*+)|[^\{\}\/]|(?<!\/)\/(?!\/)|(?R))*(?:\}))
            // old : var otherMatches = PcreRegex.Matches(shaderText, @"(?:^|\n)\s*(?<shaderName>(?:[-_\w\d:]|(?<!\/)\/)+)?\s*(?:\/\/[^\n]+\s*)*+(?<shaderBody>\{(?:[^\{\}]|(?R))*\})");
            var otherMatches = PcreRegex.Matches(shaderText, @"(?:^|\n)\s*(?<shaderName>(?:[-_\w\d:]|(?<!\/)\/)+)?\s*(?:\/\/[^\n]+\s*)*+(?<shaderBody>(?:\{)(?:(?:\/\/[^\n\r]*+)|[^\{\}\/]|(?<!\/)\/(?!\/)|(?R))*(?:\}))");
            foreach (var match in otherMatches)
            {
                string shaderName = match.Groups["shaderName"].Value;
                string shaderBody = match.Groups["shaderBody"].Value;
                shaderData[shaderName] = shaderBody;
                if(shaderDuplicates != null)
                {
                    if (!shaderDuplicates.ContainsKey(shaderName))
                    {
                        shaderDuplicates[shaderName] = new ShaderDupe();
                    }
                    shaderDuplicates[shaderName].chosenFile = file;
                    shaderDuplicates[shaderName].files.Add(file);
                    shaderDuplicates[shaderName].bodies.Add(shaderBody);
                }
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
