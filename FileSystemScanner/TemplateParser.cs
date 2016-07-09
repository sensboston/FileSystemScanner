using System;
using System.IO;
using System.Collections;
using System.Text;
using System.Collections.Generic;

namespace FileSystemScanner
{

    public class IniParser
    {
        private Dictionary<SectionPair, string> keyPairs = new Dictionary<SectionPair, string>();

        private struct SectionPair
        {
            public string Section;
            public string Key;
        }

        /// <summary>
        /// Opens the INI file at the given path and enumerates the values in the IniParser.
        /// </summary>
        /// <param name="iniPath">Full path to INI file.</param>
        public IniParser(string iniContent)
        {
            string currentRoot = null;
            string[] keyPair = null;

            StringReader reader = new StringReader(iniContent);
            string strLine = reader.ReadLine();

            while (strLine != null )
            {
                if (strLine != "")
                {
                    if (strLine.StartsWith("[") && strLine.EndsWith("]"))
                    {
                        currentRoot = strLine.Substring(1, strLine.Length - 2);
                    }
                    else
                    {
                        keyPair = strLine.Split(new char[] { '=' }, 2);

                        SectionPair sectionPair;
                        string value = null;

                        if (currentRoot == null)
                            currentRoot = "ROOT";

                        sectionPair.Section = currentRoot;

                        if (keyPair.Length == 2 && keyPair[0] != "" && !strLine.StartsWith("<"))
                        {
                            sectionPair.Key = keyPair[0];
                            value = keyPair[1];
                        }
                        else
                        {
                            sectionPair.Key = Guid.NewGuid().ToString();
                            value = strLine;
                        }

                        keyPairs.Add(sectionPair, value);
                    }
                }
                strLine = reader.ReadLine();
            }
        }

        /// <summary>
        /// Returns the value for the given section, key pair.
        /// </summary>
        /// <param name="sectionName">Section name.</param>
        /// <param name="settingName">Key name.</param>
        public string GetSetting(string sectionName, string settingName = "")
        {
            SectionPair sectionPair;
            sectionPair.Section = sectionName;
            sectionPair.Key = settingName;
            return keyPairs.ContainsKey(sectionPair) ? keyPairs[sectionPair] as string : "";
        }

        /// <summary>
        /// Returns section data as string
        /// </summary>
        /// <param name="sectionName"></param>
        /// <returns></returns>
        public string GetSection(string sectionName, string ident = "")
        {
            StringBuilder builder = new StringBuilder();
            string[] secStrs = EnumSectionValues(sectionName);
            foreach (string value in secStrs) builder.Append(ident + value + "\r\n");
            return builder.ToString();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sectionName"></param>
        /// <returns></returns>
        public string[] EnumSectionValues(string sectionName)
        {
            ArrayList tmpArray = new ArrayList();

            foreach (SectionPair pair in keyPairs.Keys)
            {
                if (pair.Section.Equals(sectionName, StringComparison.InvariantCultureIgnoreCase)) 
                    tmpArray.Add(keyPairs[pair]);
            }
            return (string[])tmpArray.ToArray(typeof(string));
        }

        /// <summary>
        /// Enumerates all lines for given section.
        /// </summary>
        /// <param name="sectionName">Section to enum.</param>
        public string[] EnumSection(string sectionName)
        {
            ArrayList tmpArray = new ArrayList();

            foreach (SectionPair pair in keyPairs.Keys)
            {
                if (pair.Section.Equals(sectionName, StringComparison.InvariantCultureIgnoreCase))
                    tmpArray.Add(pair.Key);
            }

            return (string[])tmpArray.ToArray(typeof(string));
        }

        /// <summary>
        /// Adds or replaces a setting to the table to be saved.
        /// </summary>
        /// <param name="sectionName">Section to add under.</param>
        /// <param name="settingName">Key name to add.</param>
        /// <param name="settingValue">Value of key.</param>
        public void AddSetting(string sectionName, string settingName, string settingValue)
        {
            SectionPair sectionPair;
            sectionPair.Section = sectionName;
            sectionPair.Key = settingName;

            if (keyPairs.ContainsKey(sectionPair))
                keyPairs.Remove(sectionPair);

            keyPairs.Add(sectionPair, settingValue);
        }

        /// <summary>
        /// Adds or replaces a setting to the table to be saved with a null value.
        /// </summary>
        /// <param name="sectionName">Section to add under.</param>
        /// <param name="settingName">Key name to add.</param>
        public void AddSetting(string sectionName, string settingName)
        {
            AddSetting(sectionName, settingName, null);
        }

        /// <summary>
        /// Remove a setting.
        /// </summary>
        /// <param name="sectionName">Section to add under.</param>
        /// <param name="settingName">Key name to add.</param>
        public void DeleteSetting(string sectionName, string settingName)
        {
            SectionPair sectionPair;
            sectionPair.Section = sectionName;
            sectionPair.Key = settingName;

            if (keyPairs.ContainsKey(sectionPair))
                keyPairs.Remove(sectionPair);
        }
    }
}