/*
 * AltInput: Alternate input plugin for Kerbal Space Program
 * Copyright © 2014 Pete Batard <pete@akeo.ie>
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
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Ini;

using UnityEngine;
// IMPORTANT: To be able to work with Unity, which is *BROKEN*,
// you must have a patched version of SharpDX.DirectInput such
// as the one provided with this project (in Libraries/)
// See: https://github.com/sharpdx/SharpDX/issues/406
using SharpDX.DirectInput;

namespace AltInput
{
    /// <summary>
    /// Handles the input device configuration
    /// </summary>
    [KSPAddon(KSPAddon.Startup.MainMenu, false)]
    public class Config : MonoBehaviour
    {
        public static readonly float currentVersion = 1.3f;
        public static float iniVersion;
        /// <summary>The maximum number of device instances that can be present in a config file</summary>
        private readonly uint NumDevices = 128;
        // Good developers do NOT let end-users fiddle with XML configuration files...
        internal static IniFile ini = new IniFile(Directory.GetCurrentDirectory() +
            @"\Plugins\PluginData\AltInput\config.ini");
        private DirectInput directInput = new DirectInput();
        private static readonly char[] Separators = { '[', ']', ' ', '\t' };
        public static List<AltDevice> DeviceList = new List<AltDevice>();

        private void ParseMapping(String Section, String Name, AltMapping[] Mapping, int mode)
        {
            // Try to read a mapping from the common/.Flight section first
            var ConfigData = ini.IniReadValue(Section, Name);
            if ((mode != 0) && (ConfigData == ""))
                ConfigData = ini.IniReadValue(Section + "." + GameState.ModeName[0], Name);
            // Then check for an override
            var Override = ini.IniReadValue(Section + "." + GameState.ModeName[mode], Name);
            if (Override != "")
                ConfigData = Override;
            try
            {
                String[] MappingData = ConfigData.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
                if (MappingData[0].EndsWith(".Delta"))
                {
                    Mapping[mode].Type = MappingType.Delta;
                    Mapping[mode].Action = MappingData[0].Remove(MappingData[0].IndexOf(".Delta"));
                    float.TryParse(MappingData[1], out Mapping[mode].Value);
                }
                else if (MappingData.Length == 1)
                {
                    Mapping[mode].Type = MappingType.Range;
                    Mapping[mode].Action = MappingData[0];
                }
                else
                {
                    Mapping[mode].Type = MappingType.Absolute;
                    Mapping[mode].Action = MappingData[0];
                    float.TryParse(MappingData[1], out Mapping[mode].Value);
                }
            } catch (Exception) { }
        }

        private void ParseControl(String Section, String Name, AltControl[] Control, int mode)
        {
            String s;

            // Try to read the inverted attribute from the common/.Flight section first
            s = ini.IniReadValue(Section, Name + ".Inverted");
            Boolean.TryParse(s, out Control[mode].Inverted);
            if ((mode != 0) && (s == ""))
            {
                s = ini.IniReadValue(Section + "." + GameState.ModeName[0], Name + ".Inverted");
                if (s != "")
                    Boolean.TryParse(s, out Control[mode].Inverted);
            }
            // Then check for an override
            var Override = ini.IniReadValue(Section + "." + GameState.ModeName[mode], Name + ".Inverted");
            if (Override != "")
                Boolean.TryParse(Override, out Control[mode].Inverted);

            // Check whether we are dealing with a regular axis or one used as buttons
            if ((ini.IniReadValue(Section, Name + ".Min") == "") && (ini.IniReadValue(Section, Name + ".Max") == ""))
                Control[mode].Type = ControlType.Axis;
            else
            {
                Boolean OneShot;
                Boolean.TryParse(ini.IniReadValue(Section, Name + ".OneShot"), out OneShot);
                Control[mode].Type = OneShot ? ControlType.OneShot : ControlType.Delta;
            }
        }

        /// <summary>
        /// Parse the configuration file and fill the Direct Input device attributes
        /// </summary>
        private void SetAttributes(AltDirectInputDevice Device, String Section)
        {
            InputRange Range;

            // Parse the global dead zone attribute for this device. This is the dead zone
            // that will be applied if there isn't a specific axis override
            float.TryParse(ini.IniReadValue(Section, "DeadZone"), out Device.DeadZone);
            // Parse the global sensitivity
            float.TryParse(ini.IniReadValue(Section, "Factor"), out Device.Factor);
            if (Device.Factor == 0.0f)
                Device.Factor = 1.0f;

            // Find our which modes have been setup
            for (var m = 1; m < GameState.NumModes; m++)
                Device.enabledModes[m] = (ini.IniReadValue(Section + "." + GameState.ModeName[m], null) != "");

            // Process the axes
            for (var i = 0; i < AltDirectInputDevice.AxisList.GetLength(0); i++)
            {
                // We get a DIERR_NOTFOUND/NotFound exception when probing the range for unused axes.
                // We use this to detect if the axe is available
                try
                {
                    // Parse common axis settings, starting with the Range
                    Range = Device.Joystick.GetObjectPropertiesByName(AltDirectInputDevice.AxisList[i, 0]).Range;
                    Device.Axis[i].Range.Minimum = Range.Minimum;
                    Device.Axis[i].Range.Maximum = Range.Maximum;
                    Device.Axis[i].Range.FloatRange = 1.0f * (Range.Maximum - Range.Minimum);

                    // TODO: Check if mapping name is valid and if it's already been assigned
                    for (var m = 0; m < GameState.NumModes; m++)
                    {
                        if (!Device.enabledModes[m])
                            continue;

                        // Parse the dead zone
                        float.TryParse(ini.IniReadValue(Section, AltDirectInputDevice.AxisList[i, 1] + ".DeadZone"), out Device.Axis[i].Control[m].DeadZone);
                        if (Device.Axis[i].Control[m].DeadZone == 0.0f)
                            // Override with global dead zone if none was specified
                            // NB: This prohibits setting a global dead zone and then an individual one to 0 - oh well...
                            Device.Axis[i].Control[m].DeadZone = Device.DeadZone;
                        // A slider's dead zone is special and needs to be handled separately
                        if (!AltDirectInputDevice.AxisList[i, 0].StartsWith("Slider"))
                            Device.Joystick.GetObjectPropertiesByName(AltDirectInputDevice.AxisList[i, 0]).DeadZone = (int)(10000.0f * Device.Axis[i].Control[m].DeadZone);
                        // Parse the Factor
                        float.TryParse(ini.IniReadValue(Section, AltDirectInputDevice.AxisList[i, 1] + ".Factor"), out Device.Axis[i].Control[m].Factor);
                        if (Device.Axis[i].Control[m].Factor == 0.0f)
                            Device.Axis[i].Control[m].Factor = Device.Factor;

                        ParseControl(Section, AltDirectInputDevice.AxisList[i, 1], Device.Axis[i].Control, m);
                        if (Device.Axis[i].Control[m].Type == ControlType.Axis)
                        {
                            ParseMapping(Section, AltDirectInputDevice.AxisList[i, 1], Device.Axis[i].Mapping1, m);
                        }
                        else
                        {
                            ParseMapping(Section, AltDirectInputDevice.AxisList[i, 1] + ".Min", Device.Axis[i].Mapping1, m);
                            ParseMapping(Section, AltDirectInputDevice.AxisList[i, 1] + ".Max", Device.Axis[i].Mapping2, m);
                        }
                    }

                    Device.Axis[i].isAvailable = (Device.Axis[i].Range.FloatRange != 0.0f);
                    if (!Device.Axis[i].isAvailable)
                        print("Altinput: WARNING - Axis " + AltDirectInputDevice.AxisList[i, 1] +
                            " was disabled because its range is zero.");
                }
                // Typical exception "SharpDX.SharpDXException: HRESULT: [0x80070002], Module: [SharpDX.DirectInput],
                // ApiCode: [DIERR_NOTFOUND/NotFound], Message: The system cannot find the file specified."
                catch (SharpDX.SharpDXException ex)
                {
                    if (ex.ResultCode == 0x80070002)
                        Device.Axis[i].isAvailable = false;
                    else
                        throw ex;
                }
#if (DEBUG)
                if (Device.Axis[i].isAvailable)
                {
                    for (var m = 0; m < GameState.NumModes; m++)
                    {
                        String Mappings = "";
                        if (!Device.enabledModes[m]) continue;
                        if (Device.Axis[i].Control[m].Type == ControlType.Axis)
                        {
                            Mappings += ", Mapping = '" + Device.Axis[i].Mapping1[m].Action + "'";
                        }
                        else
                        {
                            Mappings += ", Mapping.Min = '" + Device.Axis[i].Mapping1[m].Action + "'";
                            Mappings += ", Mapping.Max = '" + Device.Axis[i].Mapping2[m].Action + "'";
                        }
                        print("Altinput: Axis #" + (i + 1) + "[" + GameState.ModeName[m] + "] ('" + 
                            AltDirectInputDevice.AxisList[i, 1] + "'): Range [" +
                            Device.Axis[i].Range.Minimum + ", " + Device.Axis[i].Range.Maximum + "]" +
                            ", DeadZone = " + Device.Axis[i].Control[m].DeadZone +
                            ", Factor = " + Device.Axis[i].Control[m].Factor + Mappings + 
                            ", Inverted = " + Device.Axis[i].Control[m].Inverted);
                    }
                }
#endif
            }

            // Process the POV controls
            for (var i = 0; i < Device.Joystick.Capabilities.PovCount; i++)
            {
                for (var j = 0; j < AltDirectInputDevice.NumPOVPositions; j++)
                {
                    for (var m = 0; m < GameState.NumModes; m++)
                    {
                        if (!Device.enabledModes[m])
                            continue;
                        ParseMapping(Section, "POV" + (i + 1) + "." +
                            AltDirectInputDevice.POVPositionName[j], Device.Pov[i].Button[j].Mapping, m);
                    }
                }
#if (DEBUG)
                for (var m = 0; m < GameState.NumModes; m++)
                {
                    if (!Device.enabledModes[m]) continue;
                    String Mappings = "";
                    for (var j = 0; j < AltDirectInputDevice.NumPOVPositions; j++)
                        Mappings += ((j != 0) ? ", " : "") + AltDirectInputDevice.POVPositionName[j] + " = '" +
                            Device.Pov[i].Button[j].Mapping[m].Action + "', Value = " +
                            Device.Pov[i].Button[j].Mapping[m].Value;
                    print("Altinput: POV #" + (i + 1) + " [" + GameState.ModeName[m] + "]: " + Mappings);
                }
#endif
            }

            // Process the buttons
            for (var i = 0; i < Device.Joystick.Capabilities.ButtonCount; i++)
            {
                for (var m = 0; m < GameState.NumModes; m++)
                {
                    if (!Device.enabledModes[m])
                        continue;
                    ParseMapping(Section, "Button" + (i + 1), Device.Button[i].Mapping, m);
                }
#if (DEBUG)
                for (var m = 0; m < GameState.NumModes; m++)
                {
                    if (!Device.enabledModes[m]) continue;
                    String Mappings = "Mapping = '" + Device.Button[i].Mapping[m].Action + "'";
                    if (Device.Button[i].Mapping[m].Value != 0.0f)
                        Mappings += ", Value = " + Device.Button[i].Mapping[m].Value;
                    print("Altinput: Button #" + (i + 1) + "[" + GameState.ModeName[m] + "]: " + Mappings);
                }
#endif
            }

        }

        /// <summary>
        /// Process each input section from the config file
        /// </summary>
        void ParseInputs()
        {
            String InterfaceName, ClassName, DeviceName;
            AltDirectInputDevice Device;
            DeviceClass InstanceClass = DeviceClass.GameControl;

            float.TryParse(ini.IniReadValue("global", "Version"), out iniVersion);
            if (iniVersion != currentVersion)
                return;

            for (var i = 1; i <= NumDevices; i++)
            {
                String InputName = "input" + i;
                InterfaceName = ini.IniReadValue(InputName, "Interface");
                if ((InterfaceName == "") || (ini.IniReadValue(InputName, "Ignore") == "true"))
                    continue;
                if (InterfaceName != "DirectInput")
                {
                    print("AltInput[" + InputName + "]: Only 'DirectInput' is supported for Interface type");
                    continue;
                }
                ClassName = ini.IniReadValue(InputName, "Class");
                if (ClassName == "")
                    ClassName = "GameControl";
                else if (ClassName != "GameControl")
                {
                    print("AltInput[" + InputName + "]: '" + ClassName + "' is not an allowed Class value");
                    continue;   // ignore the device
                }
                // Overkill for now, but may come handy if we add support for other DirectInput devices
                foreach (DeviceClass Class in Enum.GetValues(typeof(DeviceClass)))
                {
                    if (Enum.GetName(typeof(DeviceClass), Class) == ClassName)
                    {
                        InstanceClass = Class;
                        break;
                    }
                }
                DeviceName = ini.IniReadValue(InputName, "Name");

                foreach (var dev in directInput.GetDevices(InstanceClass, DeviceEnumerationFlags.AllDevices))
                {
                    if ((DeviceName == "") || (dev.InstanceName.Contains(DeviceName)))
                    {
                        // Only add this device if not already in our list
                        if (DeviceList.Where(item => ((AltDirectInputDevice)item).InstanceGuid == dev.InstanceGuid).Any())
                            continue;
                        Device = new AltDirectInputDevice(directInput, InstanceClass, dev.InstanceGuid);
                        SetAttributes(Device, InputName);
                        DeviceList.Add(Device);
                        print("AltInput: Added controller '" + dev.InstanceName + "'");
                    }
                }
            }
            if (DeviceList.Count == 0)
            {
                print("AltInput: No controller found");
                return;
            }
        }

        /// <summary>
        /// This method is the first called by the Unity engine when it instantiates
        /// the game element it is associated to (here the main game menu)
        /// </summary>
        void Awake()
        {
            ParseInputs();
        }
    }
}
