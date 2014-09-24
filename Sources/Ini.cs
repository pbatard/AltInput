/*
 * AltInput: Alternate input plugin for Kerbal Space Program
 * Ini file handling, copyright © 2002 BLaZiNiX
 * http://www.codeproject.com/Articles/1966/An-INI-file-handling-class-using-C
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Collections.Generic;

namespace Ini
{
    /// <summary>
    /// Create a New INI file to store or load data
    /// </summary>
    public class IniFile
    {
        public string path;

        [DllImport("kernel32")]
        private static extern long WritePrivateProfileString(string section,
            string key, string val, string filePath);
        [DllImport("kernel32")]
        private static extern int GetPrivateProfileString(string section,
            // Do not be tempted to use a char[] array here.
            // If you do, Windows WILL FUBAR YOUR CHARACTER ENCODING!!!
            string key, string def, [In, Out] byte[] retVal,
            int size, string filePath);

        /// <summary>
        /// INIFile Constructor.
        /// </summary>
        /// <PARAM name="INIPath"></PARAM>
        public IniFile(string INIPath)
        {
            path = INIPath;
        }
        /// <summary>
        /// Write Data to the INI File
        /// </summary>
        /// <PARAM name="Section"></PARAM>
        /// Section name
        /// <PARAM name="Key"></PARAM>
        /// Key Name
        /// <PARAM name="Value"></PARAM>
        /// Value Name
        public void IniWriteValue(string Section, string Key, string Value)
        {
            WritePrivateProfileString(Section, Key, Value, this.path);
        }

        /// <summary>
        /// Read Data Value From the Ini File
        /// </summary>
        /// <PARAM name="Section"></PARAM>
        /// <PARAM name="Key"></PARAM>
        /// <PARAM name="Path"></PARAM>
        /// <returns></returns>
        public string IniReadValue(string Section, string Key)
        {
            byte[] temp = new byte[256];
            int len = GetPrivateProfileString(Section, Key, "", temp,
                                            temp.Length, this.path);
            StringBuilder sb = new StringBuilder(len);
            // I lost way too much time on account of C# lousy handling of
            // strings and character encoding => Don't take any risks.
            for (var i = 0; i < len; i++)
                sb.Append((char)temp[i]);
            return sb.ToString();
        }


        /// <summary>
        /// Read all the sections from the Ini File
        /// </summary>
        /// <returns></returns>
        public List<string> IniReadAllSections()
        {
            List<string> list = new List<string>();
            byte[] temp = new byte[4096];
            StringBuilder sb = new StringBuilder(256);
            int len = GetPrivateProfileString(null, null, "", temp,
                                            temp.Length, this.path);
            for (var i = 0; i < len; i++)
            {
                if (temp[i] == '\0')
                {
                    list.Add(sb.ToString());
                    sb.Length = 0;
                }
                else
                    sb.Append((char)temp[i]);
            }
            return list;
        }
    }
}
