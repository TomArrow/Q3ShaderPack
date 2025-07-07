using PCRE;
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
    // TODO dont resize stuff that is just an editor image
    // TODO for obj do .mtl
    // TODO do music? (sl1k-remember2)
    // TODO collect shaders that exist in exclude dirs but are different...
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
        //converts surface and content flags
        static void convertQ3FlagsToJK2Flags(string inFile, string outFile)
        {// no checks for errors here etc cuz im lazy
            if (File.Exists(outFile))
            {
                File.Delete(outFile);
            }
            using (FileStream inStream = File.OpenRead(inFile))
            {
                using (MemoryStream ms = new MemoryStream())
                {
                    BSPHelper.ConvertQ3FlagsToJK2Flags(inStream, ms);
                    using(FileStream outStream = File.OpenWrite(outFile))
                    {
                        ms.Seek(0, SeekOrigin.Begin);
                        outStream.Seek(0, SeekOrigin.Begin);
                        ms.CopyTo(outStream);
                    }
                }
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

            List<string> shaderDirectoriesForBasePath = new List<string>();
            List<string> shaderDirectories = new List<string>();
            List<string> shaderExcludeDirectories = new List<string>();
            //HashSet<string> baseDirs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            string outputDirectory = null;
            bool ignoreShaderList = false;
            bool dontChangeImageSize = false;
            bool q3ToJk2Conversion = false;
            bool keepRawFiles = false;
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
                if(argument.Equals("-keepCollectedFiles", StringComparison.InvariantCultureIgnoreCase))
                {
                    keepRawFiles = true;
                    continue;
                }
                else if (argument.EndsWith(".bsp", StringComparison.InvariantCultureIgnoreCase))
                {
                    Console.WriteLine($"Bsp input file added: {argument}");
                    fs.AddBaseFolder(Path.GetDirectoryName(Path.GetFullPath(argument)));
                    bspFiles.Add(argument);
                    continue;
                } else if (argument.EndsWith(".map", StringComparison.InvariantCultureIgnoreCase))
                {
                    Console.WriteLine($"Map input file added: {argument}");
                    fs.AddBaseFolder(Path.GetDirectoryName(Path.GetFullPath(argument)));
                    mapFiles.Add(argument);
                    continue;
                }else if (argument.EndsWith(".pk3", StringComparison.InvariantCultureIgnoreCase))
                {
                    Console.WriteLine($"Pk3 input file added: {argument}");
                    string basePath = Path.GetDirectoryName(Path.GetFullPath(argument));
                    //fs.AddBaseFolder(basePath);
                    fs.AddPk3(Path.GetFullPath(argument));
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
                            shaderDirectoriesForBasePath.Add(argument);
                            Console.WriteLine($"Shader dir added: {argument}");
                            fs.AddBaseFolder(Path.Combine(argument,".."));
                            break;
                        case 1:
                            shaderExcludeDirectories.Add(argument);
                            Console.WriteLine($"Shader exclude dir added: {argument}");
                            fs.AddBaseFolder(Path.Combine(argument, ".."));
                            break;
                        case 2:
                            Console.WriteLine($"Output dir: {argument}");
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
                                string shaderDirToChoose = shaderDirectoriesForBasePath.Count > 0 ? shaderDirectoriesForBasePath[0] : (shaderDirectories.Count > 0 ? shaderDirectories[0] : null);
                                if (shaderDirToChoose == null)
                                {
                                    Console.WriteLine(".bsp conversion failed. No suitable shader directories provided. Need at least 1.");
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
                                string baseAssetsPath = "."; // MEH.
                                if (shaderDirectoriesForBasePath.Count > 0)
                                {
                                    baseAssetsPath = Path.GetFullPath(Path.Combine(shaderDirectoriesForBasePath[0], "../../"));
                                    baseAssetsPath = baseAssetsPath.Trim('\\');
                                }
                                if (!convertQ3ToJk2Bsp(outPath, baseAssetsPath))
                                {
                                    Console.WriteLine($"{outPath} conversion failed. Exiting.");
                                    return;
                                }
                                File.Delete(outPath);
                                //File.Move(convertedname, outPath);
                                convertQ3FlagsToJK2Flags(convertedname,outPath);
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

            HashSet<string> writtenShaders = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);


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

        foundMoreShaders:
            usedShadersHashSet.CopyTo(usedShaders);

            int iFoundMoreShaders = 0;

            // look for other referenced shaders inside existing shaders
            foreach (string shader in usedShaders)
            {

                if (parsedShaders.ContainsKey(shader) && !parsedExcludeShaders.ContainsKey(shader))
                {
                    // Check if : variants (:q3map) exist.
                    string q3mapVariant = $"{shader}:q3map";
                    if (parsedShaders.ContainsKey(q3mapVariant) && !parsedExcludeShaders.ContainsKey(q3mapVariant))
                    {
                       
                        HashSet<string> newFoundShaders = ParseShaderReferencedShaders(parsedShaders[q3mapVariant]);
                        foreach (string newShader in newFoundShaders)
                        {
                            if (usedShadersHashSet.Add(newShader))
                            {
                                iFoundMoreShaders++;
                            }
                        }
                    }

                    HashSet<string> newFoundShaders2 = ParseShaderReferencedShaders(parsedShaders[shader]);
                    foreach (string newShader in newFoundShaders2)
                    {
                        if (usedShadersHashSet.Add(newShader))
                        {
                            iFoundMoreShaders++;
                        }
                    }

                    writtenShaders.Add(shader);
                }
            }

            if (iFoundMoreShaders > 0)
            {
                Console.WriteLine($"Found {iFoundMoreShaders} more shaders referenced in used shaders. Looking again.");
                goto foundMoreShaders;
            }

            Array.Sort(usedShaders);


            Console.WriteLine($"Parsed shader count: {parsedShaders.Count}");
            Console.WriteLine($"Parsed exclude shader count: {parsedExcludeShaders.Count}");

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
                        sb.Append(FixShaderHackLightmapIfNeeded(parsedShaders[q3mapVariant]));
                        sb.Append("\n");
                        sb.Append("\n");
                        shaderDuplicates[q3mapVariant].used = true;
                    }

                    sb.Append("\n");
                    sb.Append(shader);
                    sb.Append("\n");
                    sb.Append(FixShaderHackLightmapIfNeeded(parsedShaders[shader]));
                    sb.Append("\n");
                    sb.Append("\n");
                    shaderDuplicates[shader].used = true;

                    writtenShaders.Add(shader);
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

                foreach (string bsp in bspFiles)
                {
                    string levelShotName = Path.Combine("levelshots",Path.GetFileNameWithoutExtension(bsp));
                    shaderImages.Add(levelShotName);
                }
                foreach (string map in mapFiles)
                {
                    string levelShotName = Path.Combine("levelshots", Path.GetFileNameWithoutExtension(map));
                    shaderImages.Add(levelShotName);
                }

                HashSet<string> extensionLessFiles = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
                HashSet<string> extensionLessFilesUsed = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
                foreach (string shader in shaderImages)
                {
                    extensionLessFiles.Add(Path.Combine(Path.GetDirectoryName(shader),Path.GetFileNameWithoutExtension(shader)));
                }
                foreach (string file in mapModels)
                {
                    extensionLessFiles.Add(Path.Combine(Path.GetDirectoryName(file),Path.GetFileNameWithoutExtension(file)));
                }

                Console.WriteLine("Files used by map: ");
                foreach (string extensionLessFile in extensionLessFiles)
                {
                    Console.WriteLine(extensionLessFile);
                }

                Console.WriteLine();

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

                foreach (string shaderDirectory in shaderDirectories)
                {
                    List<string> files = new List<string>();
                    HashSet<string> filesToCopy = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
                        if ((extensionLessFiles.Contains(extensionLessName)))
                        {
                            if (!excludeFilesNormalized.Contains(normalizedPath))
                            {
                                Console.WriteLine($"Queueing {normalizedPath} for copy");
                                filesToCopy.Add(file);
                            }
                            extensionLessFilesUsed.Add(extensionLessName);
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
                                string magickOptions = "";
                                bool isIllegalTGA = false;
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
                                        magickOptions += " -orient BottomLeft";
                                        using (Targa img = (Targa)Pfimage.FromFile(outPath))
                                        {
                                            if (img.Header.ImageType == TargaHeader.TargaImageType.RunLengthTrueColor && (img.Header.Orientation != TargaHeader.TargaOrientation.BottomLeft))
                                            {
                                                isIllegalTGA = true; // jk2 will refuse to load these tgas
                                            }
                                            if (img.Header.ImageType == TargaHeader.TargaImageType.RunLengthTrueColor && (img.Header.PixelDepthBits != 24 && img.Header.PixelDepthBits != 32) || img.Header.ImageType == TargaHeader.TargaImageType.RunLengthColorMap || img.Header.ImageType == TargaHeader.TargaImageType.RunLengthBW || img.Header.ImageType == TargaHeader.TargaImageType.UncompressedBW || img.Header.ImageType == TargaHeader.TargaImageType.UncompressedColorMap )
                                            {
                                                if ((img.Header.ImageType == TargaHeader.TargaImageType.RunLengthTrueColor || img.Header.ImageType == TargaHeader.TargaImageType.UncompressedTrueColor) && img.Header.PixelDepthBits == 24)
                                                {
                                                    // should do this also for colormap if there is no alpha? but how to tell? idk.
                                                    magickOptions += " -type TrueColor";
                                                }
                                                else
                                                {
                                                    magickOptions += " -type TrueColorAlpha";
                                                }
                                                isIllegalTGA = true; // not ALL of these are illegal but good chance imagemagick, if used, will force RLE on them and break them otherwise.
                                            } else if ((img.Header.ImageType == TargaHeader.TargaImageType.RunLengthTrueColor || img.Header.ImageType == TargaHeader.TargaImageType.UncompressedTrueColor) && img.Header.PixelDepthBits == 24)
                                            {
                                                // this is so fucking disgusting but we have to do this because imagemagick decides that we must be forced to create greyscale images even if the original image was RGB as long as it doesn't contain any non-greyscale pixels.
                                                magickOptions += " -type TrueColor";
                                            }
                                            else
                                            { // this is so fucking disgusting but we have to do this because imagemagick decides that we must be forced to create greyscale images even if the original image was RGB as long as it doesn't contain any non-greyscale pixels.
                                                magickOptions += " -type TrueColorAlpha";
                                            }

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
                                    if(width != goodWidth || height != goodHeight || isIllegalTGA)
                                    {
                                        string resizeCmd = (width != goodWidth || height != goodHeight) ? $"-resize {goodWidth}x{goodHeight}!" : "";
                                        //string changeOrientationCmd = isIllegalTGA ? "-orient BottomLeft" : "";
                                        Console.WriteLine($"{outPath} resolution is not power of 2 ({width}x{height}) or other issue, mogrifying (needs imagemagick) to {goodWidth}x{goodHeight}");
                                        File.Copy(outPath,$"{outPath}_backup_orig_res");
                                        try
                                        {
                                            var proc = new Process()
                                            {
                                                StartInfo = new ProcessStartInfo()
                                                {
                                                    Arguments = $"mogrify {resizeCmd} {magickOptions} -define colorspace:auto-grayscale=false \"{outPath}\"",
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
                    if (File.Exists(outPath))
                    {
                        Console.WriteLine($"{outPath} already exists.");
                        continue;
                    }
                    fs.Copy(bsp, outPath);
                }
                foreach(string map in mapFiles)
                {
                    string outPath = Path.Combine(mapsDir, Path.GetFileName(map));
                    if (File.Exists(outPath))
                    {
                        Console.WriteLine($"{outPath} already exists.");
                        continue;
                    }
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
                    string sdelMaybe = keepRawFiles ? "" : "-sdel";
                    try
                    {

                    var proc = new Process()
                    {
                        StartInfo = new ProcessStartInfo()
                        {
                            Arguments = $"a -tzip \"{pk3name}\" {sdelMaybe} -x!*.pk3 *",
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

                foreach(string path in extensionLessFiles)
                {
                    if (!extensionLessFilesUsed.Contains(path))
                    {
                        if (writtenShaders.Contains(path))
                        {
                            Console.ForegroundColor = ConsoleColor.Yellow;
                            Console.WriteLine($"Shader/file not found (but shader of this name written): {path}");
                        } else
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"Shader/file not found: {path}");
                        }
                    }
                }
                Console.ForegroundColor = ConsoleColor.White;


                //Console.WriteLine($"{filesToCopy.Count}");
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
            if (!File.Exists(mapFile))
            {
                Console.WriteLine($"File not found: {mapFile}");
            }
            string mapText = fs.ReadAllText(mapFile);

            if (mapText == null)
            {
                Console.WriteLine($"Read file contents are null WTF: {mapFile}");
            }
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
                string name = match.Groups["model"].Value.Trim();
                models.Add(name);
                if (name.EndsWith(".obj", StringComparison.OrdinalIgnoreCase))
                {
                    // may not exist technically but whatever just add it, it will just be ignored if it doesnt exist
                    models.Add(name.Substring(0,name.Length-".obj".Length)+".mtl"); // TODO parse the .mtl file too
                }
            }
            return models;
        }

        static Regex referencedShader = new Regex(@"\n[^\n]*?(?<paramName>backShader|baseShader|cloneShader|remapShader)[ \t]+(?<image>[^$][^\s\n]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static Regex shaderImageRegex = new Regex(@"\n[^\n]*?(?<paramName>(?<=\s)map|lightimage|editorimage|skyparms|clampmap)[ \t]+(?<image>[^$][^\s\n]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static Regex shaderImageAnimMapRegex = new Regex(@"\n[^\n]*?(?<paramName>(?<=\s)animMap)[ \t]+(?<durationthing>[^\s\n$]+)(?<images>([ \t]+(?<image>[^$][^\s\n]+))+)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static HashSet<string> ParseShaderImages(string shaderText)
        {
            HashSet<string> images = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            Console.WriteLine($"ParseShaderImages: {shaderText.Length} bytes of input.");
            MatchCollection matches = shaderImageRegex.Matches(shaderText);
            Console.WriteLine($"ParseShaderImages: {matches.Count} matches");
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
            matches = shaderImageAnimMapRegex.Matches(shaderText); 
            foreach (Match match in matches)
            {
                string[] imgs = match.Groups["images"].Value.Trim().Split(new char[] { ' ','\t'},StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
                foreach(string img in imgs)
                {
                    images.Add(img);
                }
            }
            return images;
        }
        
        private static HashSet<string> ParseShaderReferencedShaders(string shaderText)
        {
            HashSet<string> images = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);
            MatchCollection matches = referencedShader.Matches(shaderText);
            Console.WriteLine($"ParseShaderReferencedShaders: {shaderText.Length} bytes of input, {matches.Count} matches.");
            foreach(Match match in matches)
            {
                images.Add(match.Groups["image"].Value);
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

        static Regex tcgenLightmapMatch = new Regex(@"\n[^\n]*?(?<tcgenPart>(?<=\s)tcGen)[ \t]+(?<lightmapPart>lightmap)", RegexOptions.Compiled|RegexOptions.IgnoreCase);
        static string FixShaderHackLightmapIfNeeded(string shaderText)
        {
            string originalShader = shaderText;

            int openBracket = shaderText.IndexOf("{");
            int closeBracket = shaderText.LastIndexOf("}");
            int lenInner = closeBracket - openBracket - 1;
            if (openBracket==-1 || closeBracket == -1 || lenInner <= 0)
            {
                return originalShader;
            }
            shaderText = shaderText.Substring(openBracket + 1, lenInner);
            int index = -1;
            shaderText = PcreRegex.Replace(shaderText, @"(?<shaderStage>(?:\{)(?:(?:\/\/[^\n\r]*+)|[^\{\}\/]|(?<!\/)\/(?!\/)|(?R))*(?:\}))",(a)=> {
                index++;
                if (false)// (index == 0 && tcgenLightmapMatch.Match(a.Value).Success)
                {
                    //return $"{{\n\t\tmap $lightmap\n\t\tblendFunc GL_ZERO GL_ONE\n\t}}\n\t{a.Value}";
                    return $"{{\n\t\tmap $lightmap\n\t}}\n\t{a.Value}";
                }
                else
                {
                    return a.Value;
                }
            }); 

            return $"{{{shaderText}}}";
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
