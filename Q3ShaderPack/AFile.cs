using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.IO.Compression;

namespace Q3ShaderPack
{
    // It's a file.
    class AFile
    {
        bool _inpk3 = false;
        string _pk3name = null;
        string _path = null;
        string _fullPath = null;

        public string Path => _path;
        public string FullPath => _fullPath;
        public bool InPk3 => _inpk3;
        public AFile(string fullpath, string path, string pk3name)
        {
            if (path == null)
            {
                throw new InvalidOperationException("Cannot create AFile instance without a path");
            }
            if (fullpath == null)
            {
                throw new InvalidOperationException("Cannot create AFile instance without a fullpath");
            }
            _inpk3 = pk3name != null;
            _pk3name = pk3name;
            _path = path;
            _fullPath = fullpath;
        }

        public string[] ReadAllLines()
        {
            if (_inpk3)
            {
                using (ZipArchive zip = ZipFile.OpenRead(_pk3name))
                {
                    ZipArchiveEntry entry = zip.GetEntry(_path);
                    using(Stream stream = entry.Open())
                    {
                        using(MemoryStream ms = new MemoryStream())
                        {
                            stream.CopyTo(ms);
                            byte[] data = ms.ToArray();
                            string content = Encoding.Latin1.GetString(data);
                            return content.Split(new char[] { '\n', '\r' },StringSplitOptions.RemoveEmptyEntries);
                        }
                    }
                }

            }
            else
            {
                return File.ReadAllLines(_path);
            }
        }
        public string ReadAllText()
        {
            if (_inpk3)
            {
                using (ZipArchive zip = ZipFile.OpenRead(_pk3name))
                {
                    ZipArchiveEntry entry = zip.GetEntry(_path);
                    using(Stream stream = entry.Open())
                    {
                        using(MemoryStream ms = new MemoryStream())
                        {
                            stream.CopyTo(ms);
                            byte[] data = ms.ToArray();
                            string content = Encoding.Latin1.GetString(data);
                            return content;
                        }
                    }
                }

            }
            else
            {
                return File.ReadAllText(_path);
            }
        }
        public byte[] ReadAllBytes()
        {
            if (_inpk3)
            {
                using (ZipArchive zip = ZipFile.OpenRead(_pk3name))
                {
                    ZipArchiveEntry entry = zip.GetEntry(_path);
                    using(Stream stream = entry.Open())
                    {
                        using(MemoryStream ms = new MemoryStream())
                        {
                            stream.CopyTo(ms);
                            byte[] data = ms.ToArray();
                            return data;
                        }
                    }
                }

            }
            else
            {
                return File.ReadAllBytes(_path);
            }
        }
    }



    class Q3FileSystem {
        Dictionary<string, AFile> pathToFile = new Dictionary<string, AFile>(StringComparer.OrdinalIgnoreCase);
        HashSet<string> folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, HashSet<string>> folderFiles = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, HashSet<string>> folderDirs = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        public void AddFileToPath(string fullPath, AFile file)
        {
            string folder = Path.GetDirectoryName(fullPath);
            folders.Add(folder);
            pathToFile[fullPath] = file;
            if (!folderFiles.ContainsKey(folder))
            {
                folderFiles.Add(folder, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            }
            folderFiles[folder].Add(fullPath);


            string oldFolder = folder;
            while ((folder = Path.GetDirectoryName(folder)) != null)
            {
                folders.Add(folder);
                if (!folderDirs.ContainsKey(folder))
                {
                    folderDirs.Add(folder, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
                }
                folderDirs[folder].Add(oldFolder);
                oldFolder = folder;
            }
        }
        public void AddBaseFolder(string dir)
        {
            if (!Directory.Exists(dir))
            {
                return;
            }
            string[] filesAll = Directory.GetFiles(dir);
            Array.Sort(filesAll);
            foreach(string file in filesAll)
            {
                string fullPath = Path.GetFullPath(file);
                if (Path.GetExtension(fullPath).Equals(".pk3", StringComparison.OrdinalIgnoreCase))
                {
                    AddPk3(fullPath);
                    continue;
                }
                AFile afile = new AFile(fullPath, fullPath, null);
                AddFileToPath(fullPath,afile);
            }

            string[] dirs = Directory.GetDirectories(dir);
            foreach (string subdir in dirs)
            {
                AddFolder(subdir);
            }

        }

        public void AddPk3(string fullPath)
        {
            if (!File.Exists(fullPath)) return;

            string basePath = Path.GetDirectoryName(fullPath);

            using (ZipArchive zip = ZipFile.OpenRead(fullPath))
            {
                foreach(ZipArchiveEntry entry in zip.Entries)
                {
                    string fullPathFile = Path.GetFullPath( Path.Combine(basePath, entry.FullName));
                    AFile file = new AFile(fullPathFile, entry.FullName, fullPath);
                    AddFileToPath(fullPathFile, file);
                }
            }
        }

        public void AddFolder(string dir)
        {
            if (!Directory.Exists(dir))
            {
                return;
            }
            string[] filesAll = Directory.GetFiles(dir);
            foreach (string file in filesAll)
            {
                string fullPath = Path.GetFullPath(file);
                AFile afile = new AFile(fullPath, fullPath, null);
                AddFileToPath(fullPath, afile);
            }

            string[] dirs = Directory.GetDirectories(dir);
            foreach (string subdir in dirs)
            {
                AddFolder(subdir);
            }
        }
        public bool FileExists(string path)
        {
            string fullPath = Path.GetFullPath(path);
            return pathToFile.ContainsKey(fullPath);
        }
        public bool DirectoryExists(string path)
        {
            string fullPath = Path.GetFullPath(path);
            return folders.Contains(fullPath);
        }
        public string[] GetFiles(string path)
        {
            string fullPath = Path.GetFullPath(path);
            if (folderFiles.ContainsKey(fullPath))
            {
                return folderFiles[fullPath].ToArray();
            }
            if (folders.Contains(fullPath))
            {
                return new string[] { };
            }
            return null;
        }
        public string[] GetDirectories(string path)
        {
            string fullPath = Path.GetFullPath(path);
            if (folderDirs.ContainsKey(fullPath))
            {
                return folderDirs[fullPath].ToArray();
            }
            if (folders.Contains(fullPath))
            {
                return new string[] { };
            }
            return null;
        }
        public string[] ReadAllLines(string path)
        {
            string fullPath = Path.GetFullPath(path);
            if (pathToFile.ContainsKey(fullPath))
            {
                return pathToFile[fullPath].ReadAllLines();
            }
            return null;
        }
        public string ReadAllText(string path)
        {
            string fullPath = Path.GetFullPath(path);
            if (pathToFile.ContainsKey(fullPath))
            {
                return pathToFile[fullPath].ReadAllText();
            }
            return null;
        }
        public byte[] ReadAllBytes(string path)
        {
            string fullPath = Path.GetFullPath(path);
            if (pathToFile.ContainsKey(fullPath))
            {
                return pathToFile[fullPath].ReadAllBytes();
            }
            return null;
        }

        public void Copy(string path,string outPath)
        {
            string fullPath = Path.GetFullPath(path);
            if (pathToFile.ContainsKey(fullPath))
            {
                if (pathToFile[fullPath].InPk3)
                {
                    byte[] data = pathToFile[fullPath].ReadAllBytes();
                    File.WriteAllBytes(outPath, data);
                }
                else
                {
                    File.Copy(fullPath,outPath);
                }
            }
        }
    }

}
