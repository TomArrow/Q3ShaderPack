﻿using PCRE;
using Pfim;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
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

        static bool convertQ3ToJk2Bsp(string fullPath, string basePath)
        {
            try
            {
                var proc = new Process()
                {
                    StartInfo = new ProcessStartInfo()
                    {
                        Arguments = $"-fs_basepath \"{basePath}\" -convert -format jk2 -game quake3 \"{Path.GetFileName(fullPath)}\"",
                        FileName = "q3map2",
                        RedirectStandardError = true,
                        RedirectStandardOutput = true,
                        WorkingDirectory = Path.GetDirectoryName(fullPath)
                    }
                };
                string debug = proc.StartInfo.Arguments.ToString();
                Console.WriteLine(debug);
                proc.Start();
                string error = proc.StandardError.ReadToEnd();
                Console.WriteLine(error);
                string output = proc.StandardOutput.ReadToEnd();
                Console.WriteLine(output);
                proc.WaitForExit();
                Console.WriteLine($"q3map2 conversion exited with code {proc.ExitCode}");
                return proc.ExitCode == 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error converting bsp. Probably, q3map2 binary isn't in PATH. Error: {ex.ToString()}");
                return false;
            }
        }

        // TODO Detect non power of 2 tex
        // TODO Detect not found files.
        static void Main(string[] args)
        {
            if(args.Length == 0)
            {
                Console.WriteLine("Howto convert map from pk3: Q3ShaderPack <path_to_pk3> <path_to_q3_shader_folder> <path_to_shader_exclude_folder> out:out -ignoreShaderList -q32jk2");
                Console.ReadKey();
            }

            // Todo show shader dupes
            int argIndex = 0;
            int folderIndex = 0;
            List<string> bspFiles = new List<string>();
            List<string> mapFiles = new List<string>();
            List<string> sourcePk3Files = new List<string>();
            //string bspFile = null;
            //string mapFile = null;
            //string shaderDirectory = null;
            //string shaderExcludeDirectory = null;
            Q3FileSystem fs = new Q3FileSystem();

            List<string> shaderDirectories = new List<string>();
            List<string> shaderExcludeDirectories = new List<string>();
            //HashSet<string> baseDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string outputDirectory = null;
            bool ignoreShaderList = false;
            bool dontChangeImageSize = false;
            bool q3ToJk2Conversion = false;
            while (argIndex < args.Length)
            {
                string argument = args[argIndex++];
                if(argument.Equals("-ignoreShaderList",StringComparison.InvariantCultureIgnoreCase))
                {
                    ignoreShaderList = true;
                    continue;
                }
                if(argument.Equals("-ignoreImageSize",StringComparison.InvariantCultureIgnoreCase))
                {
                    dontChangeImageSize = true;
                    continue;
                }
                if(argument.Equals("-q32jk2",StringComparison.InvariantCultureIgnoreCase))
                {
                    q3ToJk2Conversion = true;
                    continue;
                }
                else if (argument.EndsWith(".bsp", StringComparison.InvariantCultureIgnoreCase))
                {
                    fs.AddBaseFolder(Path.GetDirectoryName(argument));
                    bspFiles.Add(argument);
                    continue;
                } else if (argument.EndsWith(".map", StringComparison.InvariantCultureIgnoreCase))
                {
                    fs.AddBaseFolder(Path.GetDirectoryName(argument));
                    mapFiles.Add(argument);
                    continue;
                }else if (argument.EndsWith(".pk3", StringComparison.InvariantCultureIgnoreCase))
                {
                    string basePath = Path.GetDirectoryName(argument);
                    fs.AddBaseFolder(basePath);
                    shaderDirectories.Add(Path.Combine(basePath, "scripts"));
                    shaderDirectories.Add(Path.Combine(basePath, "shaders"));
                    sourcePk3Files.Add(argument);
                    continue;
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
                            shaderDirectories.Add(argument);
                            fs.AddBaseFolder(Path.Combine(argument,".."));
                            break;
                        case 1:
                            shaderExcludeDirectories.Add(argument);
                            fs.AddBaseFolder(Path.Combine(argument, ".."));
                            break;
                        case 2:
                            outputDirectory = argument;
                            break;
                    }
                }
            }

            foreach(string pk3file in sourcePk3Files)
            {
                string basePath =  Path.GetDirectoryName(Path.GetFullPath(pk3file));
                using (ZipArchive zip = ZipFile.OpenRead(pk3file))
                {
                    foreach (ZipArchiveEntry entry in zip.Entries)
                    {
                        string fullPathFile = Path.GetFullPath(Path.Combine(basePath, entry.FullName));
                        if (Path.GetExtension(entry.Name).Equals(".bsp", StringComparison.OrdinalIgnoreCase))
                        {
                            if (q3ToJk2Conversion)
                            {
                                if(shaderDirectories.Count == 0)
                                {
                                    Console.WriteLine(".bsp conversion failed. No shader directories provided. Need at least 1.");
                                    return;
                                }
                                string workDirName = "_tmp_mapconvert";
                                Directory.CreateDirectory(workDirName);
                                string outPath = Path.GetFullPath(Path.Combine(workDirName, entry.Name));
                                fs.Copy(fullPathFile, outPath);
                                string convertedname = Path.GetFullPath(Path.Combine(workDirName, $"{Path.GetFileNameWithoutExtension(entry.Name)}_c{Path.GetExtension(entry.Name)}"));
                                if (File.Exists(convertedname))
                                {
                                    File.Delete(convertedname);
                                }
                                string baseAssetsPath = Path.GetFullPath(Path.Combine(shaderDirectories[0], "../../"));
                                baseAssetsPath = baseAssetsPath.Trim('\\');
                                if (!convertQ3ToJk2Bsp(outPath, baseAssetsPath))
                                {
                                    Console.WriteLine($"{outPath} conversion failed. Exiting.");
                                    return;
                                }
                                File.Delete(outPath);
                                File.Move(convertedname, outPath);
                                bspFiles.Add(outPath);
                                fs.AddFolder(workDirName);
                            }
                            else
                            {
                                bspFiles.Add(fullPathFile);
                            }
                        }
                        else if (Path.GetExtension(entry.Name).Equals(".map", StringComparison.OrdinalIgnoreCase))
                        {
                            mapFiles.Add(fullPathFile);
                        }
                    }
                }
            }

            Dictionary<string, ShaderDupe> shaderDuplicates = new Dictionary<string, ShaderDupe>(StringComparer.InvariantCultureIgnoreCase);
            Dictionary<string, string> parsedShaders = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            Dictionary<string, string> parsedExcludeShaders = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);


            List<string> shadExcludeFiles = new List<string>();
            foreach (string shaderExcludeDirectory in shaderExcludeDirectories)
            {
                if (shaderExcludeDirectory != null)
                {
                    shadExcludeFiles.AddRange(crawlDirectory(shaderExcludeDirectory,fs));
                }
            }

            foreach (string shaderDirectory in shaderDirectories)
            {
                List<string> shaderListWhitelist = new List<string>();
                List<string> shadFiles = new List<string>();

                shadFiles.AddRange(crawlDirectory(shaderDirectory,fs));

                // find shaderlist
                if (!ignoreShaderList)
                {
                    foreach (string file in shadFiles)
                    {
                        string basename = Path.GetFileNameWithoutExtension(file);
                        string extension = Path.GetExtension(file).ToLowerInvariant();
                        if (basename.ToLowerInvariant().Trim() == "shaderlist" && extension == ".txt")
                        {
                            string[] allowedShaderFiles = fs.ReadAllLines(file);
                            foreach (string allowedShaderFile in allowedShaderFiles)
                            {
                                shaderListWhitelist.Add(allowedShaderFile.Trim());
                            }

                        }
                    }
                }

                shadFiles.Sort(); // Sort shaders alphabetically

                //Dictionary<string, string> shaderFiles = new Dictionary<string, string>();
                if (shaderListWhitelist.Count > 0 && !ignoreShaderList)
                {
                    // We want stuff to be read in the same order as shaderlist
                    // First shader found = kept.
                    foreach (string whitelistedShader in shaderListWhitelist)
                    {

                        foreach (string file in shadFiles)
                        {
                            string basename = Path.GetFileNameWithoutExtension(file);
                            string extension = Path.GetExtension(file).ToLowerInvariant();
                            if (extension == ".shader" && basename.Equals(whitelistedShader, StringComparison.InvariantCultureIgnoreCase))
                            {
                                ParseShader(file, ref parsedShaders, fs, shaderDuplicates);
                            }
                        }
                    }
                }

                foreach (string file in shadFiles)
                {
                    string basename = Path.GetFileNameWithoutExtension(file);
                    string extension = Path.GetExtension(file).ToLowerInvariant();
                    if (extension == ".shader")
                    {
                        if ((ignoreShaderList || shaderListWhitelist.Count == 0))
                        {
                            ParseShader(file, ref parsedShaders, fs, shaderDuplicates);
                        }
                        else if (shaderListWhitelist.Contains(basename))
                        {
                            // nuthin, already done above
                        }
                        else
                        {
                            Console.WriteLine($"Skipping {file}, not in shaderlist.txt");
                        }
                        //shaderFiles[basename] = file;
                    }
                }
            }
            foreach (string file in shadExcludeFiles)
            {
                string basename = Path.GetFileNameWithoutExtension(file);
                string extension = Path.GetExtension(file).ToLowerInvariant();
                if (extension == ".shader")
                {
                    ParseShader(file, ref parsedExcludeShaders, fs);
                    //shaderFiles[basename] = file;
                }
            }
            
            HashSet<string> usedShadersHashSet = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            foreach (string mapFile in mapFiles)
            {
                HashSet<string> mapShaders = ParseMap(mapFile, fs);
                foreach(string shader in mapShaders)
                {
                    usedShadersHashSet.Add(shader);
                }
            }
            foreach (string bspFile in bspFiles)
            {
                string[] bspShaders = BSPHelper.GetShaders(bspFile,fs);
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
                string mainName = Path.GetFileNameWithoutExtension(bspFiles.Count == 0 ? mapFiles[0] : bspFiles[0]);
                Directory.CreateDirectory(Path.Combine(outputDirectory,"shaders"));
                if (!string.IsNullOrWhiteSpace(compiledShaders))
                {
                    File.WriteAllText(Path.Combine(outputDirectory, "shaders", $"{mainName}.shader"), compiledShaders);
                }

                // Copy special image files like lightImage and editorimage and .md3 models and audio files from .map alongside normal images
                HashSet<string> mapModels = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
                foreach(string mapFile in mapFiles)
                {
                    var mapModelsHere = ParseMapModels(mapFile, fs);
                    foreach(string mapModelHere in mapModelsHere)
                    {
                        mapModels.Add(mapModelHere);
                    }
                }
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


                HashSet<string> excludeFilesNormalized = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
                foreach (string shaderExcludeDirectory in shaderExcludeDirectories)
                {
                    List<string> excludeFiles = new List<string>();
                    if (shaderExcludeDirectory != null)
                    {
                        excludeFiles.AddRange(crawlDirectory(Path.Combine(shaderExcludeDirectory, ".."), fs));
                    }
                    foreach (string file in excludeFiles)
                    {
                        string normalizedPath = Path.GetRelativePath(Path.Combine(shaderExcludeDirectory, ".."), file);
                        excludeFilesNormalized.Add(normalizedPath);
                    }
                }

                HashSet<string> foldersProcessed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                HashSet<string> filesToCopy = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (string shaderDirectory in shaderDirectories)
                {
                    List<string> files = new List<string>();
                    string thePath = Path.Combine(shaderDirectory, "..");
                    string thePathAbs = Path.GetFullPath(thePath);
                    if (foldersProcessed.Contains(thePathAbs))
                    {
                        continue;
                    }
                    foldersProcessed.Add(thePathAbs);
                    files.AddRange(crawlDirectory(thePath, fs));
                    foreach (string file in files)
                    {
                        string normalizedPath = Path.GetRelativePath(Path.Combine(shaderDirectory, ".."), file);
                        string extensionLessName = Path.Combine(Path.GetDirectoryName(normalizedPath), Path.GetFileNameWithoutExtension(normalizedPath));
                        if ((extensionLessFiles.Contains(extensionLessName)) && !excludeFilesNormalized.Contains(normalizedPath))
                        {
                            filesToCopy.Add(file);
                        }
                    }
                    foreach (string file in filesToCopy)
                    {
                        string normalizedPath = Path.GetRelativePath(Path.Combine(shaderDirectory, ".."), file);
                        string outPath = Path.Combine(outputDirectory, normalizedPath);
                        Directory.CreateDirectory(Path.GetDirectoryName(outPath));
                        if (File.Exists(outPath))
                        {

                            Console.WriteLine($"{outPath} already exists.");
                        }
                        else
                        {

                            fs.Copy(file, outPath);
                            if (!dontChangeImageSize)
                            {
                                string extension = Path.GetExtension(outPath).ToLowerInvariant();
                                bool isImage = false;
                                int width = 0;
                                int height = 0;
                                switch (extension)
                                {
                                    case ".jpg":
                                    case ".jpeg":
                                    case ".png":
                                        using (SKImage img = SKImage.FromEncodedData(outPath))
                                        {
                                            width = img.Width;
                                            height = img.Height;
                                        }
                                        isImage = true;
                                        break;
                                    case ".tga":
                                        using (var img = Pfimage.FromFile(outPath))
                                        {
                                            width = img.Width;
                                            height = img.Height;
                                        }
                                        isImage = true;
                                        break;
                                }
                                if (isImage)
                                {
                                    int goodWidth = getClosestPowerOf2(width);
                                    int goodHeight = getClosestPowerOf2(height);
                                    if(width != goodWidth || height != goodHeight)
                                    {

                                        Console.WriteLine($"{outPath} resolution is not power of 2 ({width}x{height}), mogrifying (needs imagemagick) to {goodWidth}x{goodHeight}");
                                        File.Copy(outPath,$"{outPath}_backup_orig_res");
                                        try
                                        {
                                            var proc = new Process()
                                            {
                                                StartInfo = new ProcessStartInfo()
                                                {
                                                    Arguments = $"mogrify -resize {goodWidth}x{goodHeight}! \"{outPath}\"",
                                                    FileName = "magick",
                                                    RedirectStandardError = true,
                                                    RedirectStandardOutput = true
                                                }
                                            };
                                            proc.Start();
                                            string error = proc.StandardError.ReadToEnd();
                                            Console.WriteLine(error);
                                            string output = proc.StandardOutput.ReadToEnd();
                                            Console.WriteLine(output);
                                            proc.WaitForExit();
                                            Console.WriteLine($"Magick mogrify exited with code {proc.ExitCode}");
                                        } catch(Exception ex)
                                        {
                                            Console.WriteLine($"Error mogrifying image. Probably, magick binary isn't in PATH. Error: {ex.ToString()}");
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                string mapsDir = Path.Combine(outputDirectory, "maps");
                Directory.CreateDirectory(mapsDir);

                foreach(string bsp in bspFiles)
                {
                    string outPath = Path.Combine(mapsDir, Path.GetFileName(bsp));
                    fs.Copy(bsp, outPath);
                }
                foreach(string map in mapFiles)
                {
                    string outPath = Path.Combine(mapsDir, Path.GetFileName(map));
                    fs.Copy(map, outPath);
                }

                string pk3name = $"{mainName}.pk3";
                string pk3path = Path.Combine(outputDirectory,pk3name);
                if (File.Exists(pk3path))
                {
                    Console.WriteLine($"Error creating pk3. Already exists.");
                }
                else
                {
                    try
                    {

                    var proc = new Process()
                    {
                        StartInfo = new ProcessStartInfo()
                        {
                            Arguments = $"a -tzip \"{pk3name}\" -x!*.pk3 *",
                            FileName = "7za",
                            RedirectStandardError = true,
                            RedirectStandardOutput = true,
                            WorkingDirectory = outputDirectory
                        }
                    };
                    proc.Start();
                    string error = proc.StandardError.ReadToEnd();
                    Console.WriteLine(error);
                    string output = proc.StandardOutput.ReadToEnd();
                    Console.WriteLine(output);
                    proc.WaitForExit();
                    Console.WriteLine($"7za exited with code {proc.ExitCode}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error creating pk3. Probably, 7za binary isn't in PATH. Error: {ex.ToString()}");
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

        private  static int getClosestPowerOf2(int num)
        {
            if (num == 0) return 0;
            int num2 = 1;
            while(num2 < num)
            {
                num2 *= 2;
            }
            return num2;
        }
        private static HashSet<string> ParseMap(string mapFile, Q3FileSystem fs)
        {
            HashSet<string> shaders = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            string mapText = fs.ReadAllText(mapFile);
            MatchCollection matches = faceParseRegex.Matches(mapText);
            foreach(Match match in matches)
            {
                shaders.Add("textures/"+match.Groups["texname"].Value);
            }
            return shaders;
        }

        static Regex modelParseRegex = new Regex(@"""(?:model|noise|awesomenoise)""\s*""(?<model>[^""]+)""", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static HashSet<string> ParseMapModels(string mapFile,Q3FileSystem fs)
        {
            HashSet<string> models = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            string mapText = fs.ReadAllText(mapFile);
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

        private static void ParseShader(string file, ref Dictionary<string, string> shaderData, Q3FileSystem fs, Dictionary<string, ShaderDupe> shaderDuplicates = null)
        {
            string shaderText = fs.ReadAllText(file);

            // WIP: (?:^|\n)\s*(?<shaderName>(?:[-_\w\d:]|(?<!\/)\/)+)?\s*(?:\/\/[^\n]+\s*)*+(?<shaderBody>(?:\n\s*\{)(?:(?:\/\/[^\n]*)|[^\{\}]|(?R))*(?:\n\s*\}))
            // WIP: (?:^|\n)\s*(?<shaderName>(?:[-_\w\d:]|(?<!\/)\/)+)?\s*(?:\/\/[^\n]+\s*)*+(?<shaderBody>(?:[\n\r]+\s*\{)(?:(?:\/\/[^\n\r]*)|[^\{\}\/]|(?<!\/)\/(?!\/)|(?R))*(?:[\n\r]+\s*\}))
            // WIP: (?:^|\n)\s*(?<shaderName>(?:[-_\w\d:]|(?<!\/)\/)+)?\s*(?:\/\/[^\n]+\s*)*+(?<shaderBody>(?:\{)(?:(?:\/\/[^\n\r]*+)|[^\{\}\/]|(?<!\/)\/(?!\/)|(?R))*(?:\}))
            // old : var otherMatches = PcreRegex.Matches(shaderText, @"(?:^|\n)\s*(?<shaderName>(?:[-_\w\d:]|(?<!\/)\/)+)?\s*(?:\/\/[^\n]+\s*)*+(?<shaderBody>\{(?:[^\{\}]|(?R))*\})");
            var matches = PcreRegex.Matches(shaderText, @"(?:^|\n)\s*(?<shaderName>(?:[-_\w\d:]|(?<!\/)\/)+)?\s*(?:\/\/[^\n]+\s*)*+(?<shaderBody>(?:\{)(?:(?:\/\/[^\n\r]*+)|[^\{\}\/]|(?<!\/)\/(?!\/)|(?R))*(?:\}))");
            foreach (var match in matches)
            {
                string shaderName = match.Groups["shaderName"].Value;
                string shaderBody = match.Groups["shaderBody"].Value;
                if(shaderDuplicates != null)
                {
                    if (!shaderDuplicates.ContainsKey(shaderName))
                    {
                        shaderDuplicates[shaderName] = new ShaderDupe();
                    }
                    if(shaderDuplicates[shaderName].files.Count == 0)
                    {
                        // The main shader we use is the first we find.
                        shaderData[shaderName] = shaderBody;
                        shaderDuplicates[shaderName].chosenFile = file;
                    }
                    shaderDuplicates[shaderName].files.Add(file);
                    shaderDuplicates[shaderName].bodies.Add(shaderBody);
                } else
                {
                    shaderData[shaderName] = shaderBody;
                }
            }

        }



        static string[] crawlDirectory(string dir,Q3FileSystem fs)
        {
            if (!fs.DirectoryExists(dir))
            {
                return new string[0];
            }
            List<string> filesAll = new List<string>();
            filesAll.AddRange(fs.GetFiles(dir));
            string[] dirs = fs.GetDirectories(dir);
            foreach (string subdir in dirs)
            {
                filesAll.AddRange(crawlDirectory(subdir,fs));
            }

            return filesAll.ToArray();
        }
    }
}
