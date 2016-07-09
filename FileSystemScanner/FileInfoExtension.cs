using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Reflection;
using SharpShell.Attributes;
using SharpShell.SharpContextMenu;
using System.Diagnostics;

namespace FileSystemScanner
{
    [ComVisible(true)]
    [COMServerAssociation(AssociationType.AllFiles)]
    [COMServerAssociation(AssociationType.Directory)]
    public class FileInfoExtension : SharpContextMenu
    {
        Dictionary<string, string> templates = new Dictionary<string, string>();

        public FileInfoExtension()
        {
            // read templates
            var startupPath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase).Substring(6);
            var files = Directory.GetFiles(startupPath, "*.template");
            foreach (var file in files)
                try { templates.Add(Path.GetFileNameWithoutExtension(file), new StreamReader(Path.Combine(startupPath, file)).ReadToEnd()); } catch { }
        }

        protected override bool CanShowMenu()
        {
            return SelectedItemPaths.Count() > 0;
        }

        protected override ContextMenuStrip CreateMenu()
        {
            // create the menu strip.
            var menu = new ContextMenuStrip();
            var topMenuItem = new ToolStripMenuItem { Text = "Scan selected files..." };

            foreach (var t in templates)
            {
                var templateMenuItem = new ToolStripMenuItem { Text = "Scan to " + t.Key, Tag = t.Key };
                templateMenuItem.Click += ScanFiles;
                topMenuItem.DropDownItems.Add(templateMenuItem);
            }

            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add(topMenuItem);
            menu.Items.Add(new ToolStripSeparator());

            //  return the menu.
            return menu;
        }

        private void ScanFiles(object sender, EventArgs e)
        {
            var template = templates[(sender as ToolStripMenuItem).Tag as string];
            if (!string.IsNullOrEmpty(template))
            {
                IniParser parser = new IniParser(template);
                bool ignoreHidden = true, ignoreSystem = true;
                bool.TryParse(parser.GetSetting("Format", "ignorehidden"), out ignoreHidden);
                bool.TryParse(parser.GetSetting("Format", "ignoresystem"), out ignoreSystem);
                Toolbox.IgnoreHidden = ignoreHidden;
                Toolbox.IgnoreSystem = ignoreSystem;
                Toolbox.FilesToExclude = parser.GetSection("ExcludeFiles").Replace("\r\n", "");
                Toolbox.FoldersToExclude = parser.GetSection("ExcludeFolders").Replace("\r\n", "");
                Toolbox.StartTime = DateTime.Now;

                // scan all selected files
                List<ScanObject> list = new List<ScanObject>();
                foreach (var filePath in SelectedItemPaths)
                {
                    long size = 0; int count = 0;
                    list.AddRange(Toolbox.Scan(filePath, ref size, ref count));
                }

                // display result
                var result = Toolbox.ConvertData(list, template);
                var tempFileName = Path.Combine(Path.GetTempPath(), string.Format("filescan_{0:MMddyy_HHmmss}.txt", DateTime.Now));
                try
                {
                    File.WriteAllText(tempFileName, result);
                    Process.Start("notepad.exe", tempFileName);
                }
                catch (Exception error)
                {
                    Debug.WriteLine(error.Message);
                }
            }
        }
    }
}
