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
        /// <summary>The maximum number of device instances that can be present in a config file</summary>
        private readonly uint NumDevices = 128;
        // Good developers do NOT let end-users fiddle with XML configuration files...
        private static IniFile ini = new IniFile(Directory.GetCurrentDirectory() +
            @"\Plugins\PluginData\AltInput\config.ini");
        private DirectInput directInput = new DirectInput();
        private static readonly char[] Separators = { '[', ']', ' ', '\t' };
        public static List<AltDevice> DeviceList = new List<AltDevice>();

        private void ParseButton(String Section, String Name, ref AltButton Button)
        {
            for (var m = 0; m < GameState.NumModes; m++)
            {
                // Try to read from the common section first
                var ConfigData = ini.IniReadValue(Section, Name);
                // Then check for an override
                var Override = ini.IniReadValue(Section + "." + GameState.ModeName[m], Name);
                if (Override != "")
                    ConfigData = Override;
                try
                {
                    String[] Values = ConfigData.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
                    Button.Mapping[m] = Values[0];
                    float.TryParse(Values[1], out Button.Value[m]);
                }
                catch (Exception) { }
            }
        }

        private void ParseMapping(String Section, String Name, ref String[] Mapping)
        {
            for (var m = 0; m < GameState.NumModes; m++)
            {
                // Try to read a mapping from the common section first
                Mapping[m] = ini.IniReadValue(Section, Name);
                // Then check for an override
                var Override = ini.IniReadValue(Section + "." + GameState.ModeName[m], Name);
                if (Override != "")
                    Mapping[m] = Override;
            }
        }

        private void ParseInverted(String Section, String Name, ref Boolean[] Inverted)
        {
            for (var m = 0; m < GameState.NumModes; m++)
            {
                // Try to read the inverted attribute from the common section first
                Boolean.TryParse(ini.IniReadValue(Section, Name + ".Inverted"), out Inverted[m]);
                // Then check for an override
                var Override = ini.IniReadValue(Section + "." + GameState.ModeName[m], Name + ".Inverted");
                if (Override != "")
                    Boolean.TryParse(Override, out Inverted[m]);
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
            int.TryParse(ini.IniReadValue(Section, "DeadZone"), out Device.DeadZone);

            // Process the axes
            for (var i = 0; i < AltDirectInputDevice.AxisList.GetLength(0); i++)
            {
                // We get a DIERR_NOTFOUND/NotFound exception when probing the range for unused axes.
                // Use that to indicate if the axe is available
                try
                {
                    Range = Device.Joystick.GetObjectPropertiesByName(AltDirectInputDevice.AxisList[i, 0]).Range;
                    Device.Axis[i].Range.Minimum = Range.Minimum;
                    Device.Axis[i].Range.Maximum = Range.Maximum;
                    Device.Axis[i].Range.FloatRange = 1.0f * (Range.Maximum - Range.Minimum);
                    ParseMapping(Section, AltDirectInputDevice.AxisList[i, 1], ref Device.Axis[i].Mapping);
                    // TODO: check if mapping name is valid and if it's already been assigned
                    int.TryParse(ini.IniReadValue(Section, AltDirectInputDevice.AxisList[i, 1] + ".DeadZone"), out Device.Axis[i].DeadZone);
                    if (Device.Axis[i].DeadZone == 0)
                        // Override with global dead zone if none was specified
                        // NB: This prohibits setting a global dead zone and then an individual to 0 - oh well...
                        Device.Axis[i].DeadZone = Device.DeadZone;
                    // A slider's deadzone is special and needs to be handled separately
                    if (!AltDirectInputDevice.AxisList[i, 0].StartsWith("Slider"))
                        Device.Joystick.GetObjectPropertiesByName(AltDirectInputDevice.AxisList[i, 0]).DeadZone = Device.Axis[i].DeadZone;
                    ParseInverted(Section, AltDirectInputDevice.AxisList[i, 1], ref Device.Axis[i].Inverted);
                    Device.Axis[i].isAvailable = (Device.Axis[i].Range.FloatRange != 0.0f);
                    if (!Device.Axis[i].isAvailable)
                        print("Altinput: WARNING - Axis " + AltDirectInputDevice.AxisList[i, 1] +
                            " was disabled because its range is zero.");
                }
                // SharpDX.SharpDXException: HRESULT: [0x80070002], Module: [SharpDX.DirectInput], ApiCode: [DIERR_NOTFOUND/NotFound], Message: The system cannot find the file specified.
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
                    String Mappings = "", Inverted = "";
                    for (var m = 0; m < GameState.NumModes; m++)
                    {
                        Mappings += ", Mapping[" + GameState.ModeName[m] + "] = '" + Device.Axis[i].Mapping[m] + "'";
                        Inverted += ", Inverted[" + GameState.ModeName[m] + "] = " + Device.Axis[i].Inverted[m];
                    }
                    print("Altinput: Axis #" + (i + 1) + ": Range [" + Device.Axis[i].Range.Minimum + ", " +
                        Device.Axis[i].Range.Maximum + "], DeadZone = " + Device.Axis[i].DeadZone +
                        Mappings + Inverted);
                }
#endif
            }

            // Process the POV controls
            for (var i = 0; i < Device.Joystick.Capabilities.PovCount; i++)
            {
                for (var j = 0; j < AltDirectInputDevice.NumPOVPositions; j++)
                    ParseButton(Section, "POV" + (i + 1) + "." + AltDirectInputDevice.POVPositionName[j], ref Device.Pov[i].Button[j]);
#if (DEBUG)
                for (var m = 0; m < GameState.NumModes; m++)
                {
                    String Mappings = "";
                    for (var j = 0; j < AltDirectInputDevice.NumPOVPositions; j++)
                        Mappings += ((j != 0) ? ", " : "") + AltDirectInputDevice.POVPositionName[j] + " = " + Device.Pov[i].Button[j].Mapping[m] + ", Value = " + Device.Pov[i].Button[j].Value[m];
                    print("Altinput: POV #" + (i + 1) + " [" + GameState.ModeName[m] + "]: " + Mappings);
                }
#endif
            }

            // Process the buttons
            for (var i = 0; i < Device.Joystick.Capabilities.ButtonCount; i++)
            {
                ParseButton(Section, "Button" + (i + 1), ref Device.Button[i]);
#if (DEBUG)
                String Mappings = "";
                for (var m = 0; m < GameState.NumModes; m++)
                    Mappings += ((m != 0) ? ", " : "") + "Mapping[" + GameState.ModeName[m] + "] = '" + Device.Button[i].Mapping[m] +
                        "', Value[" + GameState.ModeName[m] + "] = " + Device.Button[i].Value[m];
                print("Altinput: Button #" + (i + 1) + ": " + Mappings);
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
                    if ((DeviceName == "") || (dev.InstanceName.StartsWith(DeviceName)))
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
