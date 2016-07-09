using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using FileSystemScanner;

namespace UnitTest
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length == 2)
            {
                string template = string.Empty;
                if (File.Exists(args[0]))
                {
                    try
                    {
                        template = new StreamReader(args[0]).ReadToEnd();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Error reading template file " + args[0] + ". Exception: " + e.Message);
                        return;
                    }
                }

                if (Directory.Exists(args[1]))
                {
                    IniParser parser = new IniParser(template);
                    bool ignoreHidden = false, ignoreSystem = false;
                    bool.TryParse(parser.GetSetting("Format", "ignorehidden"), out ignoreHidden);
                    bool.TryParse(parser.GetSetting("Format", "ignoresystem"), out ignoreSystem);
                    Toolbox.IgnoreHidden = ignoreHidden;
                    Toolbox.IgnoreSystem = ignoreSystem;
                    Toolbox.FilesToExclude = parser.GetSection("ExcludeFiles").Replace("\r\n","");
                    Toolbox.FoldersToExclude = parser.GetSection("ExcludeFolders").Replace("\r\n", "");
                    Toolbox.StartTime = DateTime.Now;

                    // scan folder
                    long size = 0; int count = 0;
                    List<ScanObject> list = Toolbox.Scan(args[1], ref size, ref count);

                    // build output
                    string result = Toolbox.ConvertData(list, template);

                    Debug.WriteLine(result);
                    Console.WriteLine(result);
                }
                else Console.WriteLine("Invalid directory specified in arguments");
            }
            else Console.WriteLine("Use: UnitTest.exe [templateFileName] [directoryToScan]");
        }
    }
}
