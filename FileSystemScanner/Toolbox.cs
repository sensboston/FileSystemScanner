using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace FileSystemScanner
{
    public class ScanObject
    {
        public object Item;
        public int Level;
        public long Size;
        public int Count;
        public ScanObject(object item, int level, long size, int count)
        {
            Item = item;
            Level = level;
            Size = size;
            Count = count;
        }
    }

    public class Toolbox
    {
        public static bool IgnoreHidden { get; set; }
        public static bool IgnoreSystem { get; set; }
        public static string FilesToExclude { get; set; }
        public static string FoldersToExclude { get; set; }
        public static DateTime StartTime { get; set; }

        private static bool FitsMask(string fileName, string fileMask)
        {
            Regex mask = new Regex(
                '^' +
                fileMask
                    .Replace(".", "[.]")
                    .Replace("*", ".*")
                    .Replace("?", ".")
                + '$',
                RegexOptions.IgnoreCase);
            return mask.IsMatch(fileName);
        }

        private static bool ItemIsValid(string itemName, FileAttributes attrs, string excludeMask)
        {
            if ((IgnoreHidden && attrs.HasFlag(FileAttributes.Hidden)) || (IgnoreSystem && attrs.HasFlag(FileAttributes.System)))
            {
                return false;
            }
            else if (!string.IsNullOrEmpty(excludeMask))
            {
                return !(excludeMask
                    .Split(new string[] { "\r\n", "\n", ",", "|", " " }, StringSplitOptions.RemoveEmptyEntries)
                    .Any(fileMask => FitsMask(itemName, fileMask)));
            }
            return true;
        }

        public static List<ScanObject> Scan(string path, ref long size, ref int count, int level = 0)
        {
            List<ScanObject> list = new List<ScanObject>();

            // if it's a file, skip tree scanning
            if (File.Exists(path))
            {
                var fi = new FileInfo(path);
                if (ItemIsValid(fi.Name, fi.Attributes, FilesToExclude)) list.Add(new ScanObject(fi, level, fi.Length, 1));
                return list;
            }

            // otherwise scan directory tree
            ScanObject thisDir = null;
            var di = new DirectoryInfo(path);
            if (ItemIsValid(di.Name, di.Attributes, FoldersToExclude))
            {
                thisDir = new ScanObject(di, level, 0, 1);

                // add directories first
                try
                {
                    var dirs = di.GetDirectories();
                    foreach (var d in dirs)
                    {
                        long thisSize = 0; int thisCount = 0;
                        list.AddRange(Scan(d.FullName, ref thisSize, ref thisCount, level + 1));
                        size += thisSize;
                        count += thisCount;
                        thisDir.Size += thisSize;
                        thisDir.Count += thisCount;
                    }

                    // than files
                    var files = di.GetFiles();
                    foreach (var fi in files)
                    {
                        if (ItemIsValid(fi.Name, fi.Attributes, FilesToExclude))
                        {
                            list.Add(new ScanObject(fi, level + 1, fi.Length, 1));
                            size += fi.Length;
                            count++;
                            thisDir.Size += fi.Length;
                            thisDir.Count++;
                        }
                    }
                    if (thisDir != null) list.Insert(0, thisDir);
                }
                catch { }
            }

            return list;
        }

        public static string ConvertData(List<ScanObject> items, string template)
        {
            StringBuilder result = new StringBuilder();
            IniParser parser = new IniParser(template);

            var files = items.Where(s => s.Item is FileInfo).Select(s => s).ToList();
            var folders = items.Where(s => s.Item is DirectoryInfo).Select(s => s).ToList();

            // Get formatting settings
            string timeFormat = parser.GetSetting("Format", "timeformat");
            if (!string.IsNullOrEmpty(timeFormat)) try { var test = DateTime.Now.ToString(timeFormat); } catch { timeFormat = ""; }
            char identChar = '\t';
            string ics = parser.GetSetting("Format", "identchar");
            if (!string.IsNullOrEmpty(ics))
            {
                if (ics == "\t") identChar = '\t'; else if (ics == "\n") identChar = '\n'; else if (ics == "\r") identChar = '\r';
                else if (!char.TryParse(ics, out identChar)) identChar = '\t';
            }
            int identCount = 1;
            int.TryParse(parser.GetSetting("Format", "identcount"), out identCount);

            // Work on file header
            string header = parser.GetSection("Header");
            header = header.Replace("{%scandate%}", DateTime.Now.ToString());

            if (folders.Count > 0 || files.Count > 0)
            {
                var driveInfo = new DriveInfo(folders.Count > 0 ? (folders.First().Item as DirectoryInfo).FullName[0].ToString() : (files.First().Item as FileInfo).FullName[0].ToString());
                header = header.Replace("{%volumelabel%}", driveInfo.VolumeLabel);
                header = header.Replace("{%filesystem%}", driveInfo.DriveFormat);
            }
            header = header.Replace("{%itemcount%}", items.Count.ToString());
            header = header.Replace("{%foldercount%}", folders.Count.ToString());
            header = header.Replace("{%filecount%}", files.Count.ToString());

            // get file types
            var fileTypes = parser.EnumSectionValues("FileTypes").Zip(parser.EnumSection("FileTypes"), (first, second) => new { first, second }).ToDictionary(val => val.first, val => val.second);

            // get templates
            var fileTemplate = parser.GetSection("FileItem");
            var dirTemplate = parser.GetSection("DirectoryItem");
            int prevLevel = 0, dirLevelCount = 0;

            foreach (ScanObject item in items)
            {
                FileInfo fileInfo = item.Item as FileInfo;
                DirectoryInfo dirInfo = item.Item as DirectoryInfo;
                string ident = new string(identChar, item.Level * identCount);

                // close directory tag
                if (item.Level < prevLevel)
                {
                    for (int i = dirLevelCount; i > item.Level; i--)
                    {
                        result.Append(parser.GetSection("DirectoryItemFooter", new string(identChar, (i - 1) * identCount)));
                        dirLevelCount--;
                    }
                }

                // get item template
                var itemBody = fileInfo != null ? fileTemplate : dirTemplate;
                itemBody = ident + itemBody.Replace("\r\n", "\r\n" + ident);
                itemBody = itemBody.TrimEnd(identChar);

                string fileType = "";
                if (dirInfo != null)
                {
                    dirLevelCount++;
                }
                // Get file type
                else if (!string.IsNullOrEmpty(fileInfo.Extension))
                {
                    var key = fileTypes.Keys.FirstOrDefault(k => k.Contains(fileInfo.Extension.Substring(1)));
                    if (key != null) fileType = fileTypes[key];
                }

                // Do the job: just do a simple replace of variables by data
                itemBody = itemBody.Replace("{%name%}", fileInfo != null ? fileInfo.Name : dirInfo.Name);
                itemBody = itemBody.Replace("{%fullname%}", fileInfo != null ? fileInfo.FullName : dirInfo.FullName);
                itemBody = itemBody.Replace("{%path%}", fileInfo != null ? Path.GetDirectoryName(fileInfo.FullName) : Path.GetDirectoryName(dirInfo.FullName));
                itemBody = itemBody.Replace("{%itemcount%}", fileInfo != null ? "" : item.Count.ToString());
                itemBody = itemBody.Replace("{%bytesize%}", fileInfo != null ? fileInfo.Length.ToString() : item.Size.ToString());
                itemBody = itemBody.Replace("{%size%}", fileInfo != null ? BytesToString(fileInfo.Length) : BytesToString(item.Size));
                itemBody = itemBody.Replace("{%created%}", fileInfo != null ? fileInfo.CreationTime.ToString(timeFormat) : dirInfo.CreationTime.ToString(timeFormat));
                itemBody = itemBody.Replace("{%modified%}", fileInfo != null ? fileInfo.LastAccessTime.ToString(timeFormat) : dirInfo.LastAccessTime.ToString(timeFormat));
                itemBody = itemBody.Replace("{%extension%}", fileInfo != null ? fileInfo.Extension : dirInfo.Extension);
                itemBody = itemBody.Replace("{%type%}", fileType);

                result.Append(itemBody);

                prevLevel = item.Level;
            }

            // close opened dir tags
            for (int i=dirLevelCount; i>0; i--)
                result.Append(parser.GetSection("DirectoryItemFooter", new string(identChar, (i-1) * identCount)));

            result.Append(parser.GetSection("Footer"));

            // for the performance testing, process "elapsed" variable
            header = header.Replace("{%elapsed%}", DateTime.Now.Subtract(StartTime).TotalSeconds.ToString("0.0"));
            result.Insert(0, header);

            return result.ToString();
        }

        static string BytesToString(long byteCount)
        {
            string[] suf = { "B", "KB", "MB", "GB", "TB", "PB" };
            if (byteCount == 0) return "0" + suf[0];
            long bytes = Math.Abs(byteCount);
            int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            double num = Math.Round(bytes / Math.Pow(1024, place), 1);
            return (Math.Sign(byteCount) * num).ToString() + suf[place];
        }
    }
}
