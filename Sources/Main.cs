/*
 * AltInput: Alternate input plugin for Kerbal Space Program
 * Copyright © 2014 Pete Batard <pete@akeo.ie>
 * Thanks also go to zitronen, for KSPSerialIO, which helped figure out
 * spacecraft controls in KSP: https://github.com/zitron-git/KSPSerialIO
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
using System.Threading;
using System.IO;
using Ini;

using UnityEngine;
using KSP.IO;
// IMPORTANT: To be able to work with Unity, which is *BROKEN*,
// you must have a patched version of SharpDX.DirectInput such
// as the one provided with this project (in Libraries/)
// See: https://github.com/sharpdx/SharpDX/issues/406
using SharpDX.DirectInput;

namespace AltInput
{
    /// <summary>
    /// The range of a axis
    /// </summary>
    public struct AltRange
    {
        public int Minimum;
        public int Maximum;
        /// <summary>The range expressed as a floating point value to speed up computation</summary>
        public float FloatRange;
    }

    /// <summary>
    /// An axis on the input device
    /// </summary>
    public struct AltAxis
    {
        /// <summary>Whether this axis is available on this controller</summary>
        public Boolean isAvailable;
        /// <summary>The range of this axis</summary>
        public AltRange Range;
        /// <summary>Default dead zone of the axes, in the range 0 through 10,000,
        /// where 0 indicates that there is no dead zone, 5,000 indicates that the dead
        /// zone extends over 50 percent of the physical range of the axis and 10,000
        /// indicates that the entire physical range of the axis is dead. For regular
        /// axes, the dead zone applies to the center or, for sliders, to the edges</summary>
        public int DeadZone;
        /// <summary>Whether this axis should be inverted</summary>
        public Boolean[] Inverted;
        /// <summary>Names of the KSP FlightCtrlState attribute this axis should map to in each mode</summary>
        public String[] Mapping;
    }

    /// <summary>
    /// A button on the input device
    /// </summary>
    public struct AltButton
    {
        /// <summary>Names of the KSP FlightCtrlState attribute this button should map to in each mode</summary>
        public String[] Mapping;
        /// <summary>For an axis, the value we pass to KSP when the button is pressed</summary>
        public float[] Value;
    }

    /// <summary>
    /// A Point Of View control on the input device
    /// </summary>
    public struct AltPOV
    {
        // We consider that a POV is a set of 4 buttons
        public AltButton[] Button;  // Gotta wonder what the heck is wrong with these "high level" languages
        // when you can't do something as elementary as declaring a BLOODY FIXED SIZE ARRAY IN A STRUCT...
    }

    // TODO: we may derive this from a parent class when we support more than DI
    /// <summary>
    /// A Direct Input Device (typically a game controller)
    /// </summary>
    public class AltDirectInputDevice
    {
        /// <summary>POV positions</summary>
        public enum POVPosition
        {
            Up = 0,
            Right,
            Down,
            Left
        };
        public readonly static String[] POVPositionName = Enum.GetNames(typeof(POVPosition));
        public readonly static int NumPOVPositions = POVPositionName.Length;

        /// <summary>Names for the axes. Using a double string array allows to map not so
        /// user-friendly DirectInput names to more user-friendly config counterparts.</summary>
        public readonly static String[,] AxisList = new String[,] {
            { "X", "AxisX" }, { "Y", "AxisY" }, { "Z", "AxisZ" },
            { "RotationX", "RotationX" }, { "RotationY", "RotationY" }, { "RotationZ", "RotationZ" },
            { "Sliders0", "Slider1" }, { "Sliders1", "Slider2" } };
        public DeviceClass Class;
        public Guid InstanceGuid;
        /// <summary>Default dead zone of the axes, in the range 0 through 10,000,
        /// where 0 indicates that there is no dead zone, 5,000 indicates that the dead
        /// zone extends over 50 percent of the physical range of the axis on both sides
        /// of center, and 10,000 indicates that the entire physical range of the axis
        /// is dead.</summary>
        public int DeadZone;
        public AltAxis[] Axis;
        public AltPOV[] Pov;
        public AltButton[] Button;
        public Joystick Joystick;

        public AltDirectInputDevice(DirectInput directInput, DeviceClass deviceClass, Guid instanceGUID)
        {
            if (deviceClass != DeviceClass.GameControl)
                throw new ArgumentException("Class must be 'GameControl'");
            this.InstanceGuid = instanceGUID;
            this.Joystick = new Joystick(directInput, instanceGUID);
            this.Axis = new AltAxis[AltDirectInputDevice.AxisList.GetLength(0)];
            for (var i = 0; i < this.Axis.Length; i++)
            {
                this.Axis[i].Mapping = new String[Config.NumGameModes];
                this.Axis[i].Inverted = new Boolean[Config.NumGameModes];
            }
            this.Pov = new AltPOV[this.Joystick.Capabilities.PovCount];

            for (var i = 0; i < this.Pov.Length; i++)
            {
                this.Pov[i].Button = new AltButton[NumPOVPositions];
                for (var j = 0; j < NumPOVPositions; j++)
                {
                    this.Pov[i].Button[j].Mapping = new String[Config.NumGameModes];
                    this.Pov[i].Button[j].Value = new float[Config.NumGameModes];
                }
            }
            this.Button = new AltButton[this.Joystick.Capabilities.ButtonCount];
            for (var i = 0; i < this.Button.Length; i++)
            {
                this.Button[i].Mapping = new String[Config.NumGameModes];
                this.Button[i].Value = new float[Config.NumGameModes];
            }
        }
    }

    /// <summary>
    /// Handles the input device configuration
    /// </summary>
    [KSPAddon(KSPAddon.Startup.MainMenu, false)]
    public class Config : MonoBehaviour
    {
        /// <summary>The game modes we support</summary>
        public enum GameMode
        {
            Flight = 0,
            EVA,
            Rover
        };
        public readonly static String[] GameModeName = Enum.GetNames(typeof(GameMode));
        public readonly static int NumGameModes = GameModeName.Length;

        /// <summary>The maximum number of device instances that can be present in a config file</summary>
        private readonly uint NumDevices = 128;
        /// <summary>The KSP axes we support</summary>
        public readonly String[] KSPAxisList = { "yaw", "pitch", "roll", "X", "Y", "Z", "mainThrottle" };
        /// <summary>The KSP actions we support</summary>
        public readonly String[] KSPActionList = { "ActivateNextStage", "KSPActionGroup.Stage", "KSPActionGroup.Gear",
            "KSPActionGroup.Light", "KSPActionGroup.RCS", "KSPActionGroup.SAS", "KSPActionGroup.Brakes",
            "KSPActionGroup.Abort", "KSPActionGroup.Custom01", "KSPActionGroup.Custom02", "KSPActionGroup.Custom03",
            "KSPActionGroup.Custom04", "KSPActionGroup.Custom05", "KSPActionGroup.Custom06", "KSPActionGroup.Custom07",
            "KSPActionGroup.Custom08", "KSPActionGroup.Custom09", "KSPActionGroup.Custom10" };
        // Good developers do NOT let end-users fiddle with XML configuration files...
        private static IniFile ini = new IniFile(Directory.GetCurrentDirectory() +
            @"\Plugins\PluginData\AltInput\config.ini");
        private DirectInput directInput = new DirectInput();
        private static readonly String[] Separators = { "[", "]", " ", "\t" };
        public static List<AltDirectInputDevice> DeviceList = new List<AltDirectInputDevice>();

        private void ParseButton(String Section, String Name, ref AltButton Button)
        {
            for (var m = 0; m < NumGameModes; m++)
            {
                // Try to read from the common section first
                var ConfigData = ini.IniReadValue(Section, Name);
                // Then check for an override
                var Override = ini.IniReadValue(Section + "." + GameModeName[m], Name);
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
            for (var m = 0; m < NumGameModes; m++)
            {
                // Try to read a mapping from the common section first
                Mapping[m] = ini.IniReadValue(Section, Name);
                // Then check for an override
                var Override = ini.IniReadValue(Section + "." + GameModeName[m], Name);
                if (Override != "")
                    Mapping[m] = Override;
            }
        }

        private void ParseInverted(String Section, String Name, ref Boolean[] Inverted)
        {
            for (var m = 0; m < NumGameModes; m++)
            {
                // Try to read the inverted attribute from the common section first
                Boolean.TryParse(ini.IniReadValue(Section, Name + ".Inverted"), out Inverted[m]);
                // Then check for an override
                var Override = ini.IniReadValue(Section + "." + GameModeName[m], Name + ".Inverted");
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
                    for (var m = 0; m < NumGameModes; m++)
                    {
                        Mappings += ", Mapping[" + GameModeName[m] + "] = '" + Device.Axis[i].Mapping[m] + "'";
                        Inverted += ", Inverted[" + GameModeName[m] + "] = " + Device.Axis[i].Inverted[m];
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
                    ParseButton(Section, "POV" + (i+1) + "." + AltDirectInputDevice.POVPositionName[j], ref Device.Pov[i].Button[j]);
#if (DEBUG)
                for (var m = 0; m < NumGameModes; m++)
                {
                    String Mappings = "";
                    for (var j = 0; j < AltDirectInputDevice.NumPOVPositions; j++)
                        Mappings += ((j != 0)?", ":"") + AltDirectInputDevice.POVPositionName[j] + " = " + Device.Pov[i].Button[j].Mapping[m] + ", Value = " + Device.Pov[i].Button[j].Value[m];
                    print("Altinput: POV #" + (i + 1) + " [" + GameModeName[m] + "]: " + Mappings);
                }
#endif
            }

            // Process the buttons
            for (var i = 0; i < Device.Joystick.Capabilities.ButtonCount; i++)
            {
                ParseButton(Section, "Button" + (i+1), ref Device.Button[i]);
#if (DEBUG)
                String Mappings = "";
                for (var m = 0; m < NumGameModes; m++)
                    Mappings += ((m != 0)?", ":"") + "Mapping[" + GameModeName[m] + "] = '" + Device.Button[i].Mapping[m] +
                        "', Value[" + GameModeName[m] + "] = " + Device.Button[i].Value[m];
                print("Altinput: Button #" + (i+1) + ": " + Mappings);
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
                if (InterfaceName != "DirectInput") {
                    print("AltInput[" + InputName + "]: Only 'DirectInput' is supported for Interface type");
                    continue;
                }
                ClassName = ini.IniReadValue(InputName, "Class");
                if (ClassName == "")
                    ClassName = "GameControl";
                else if (ClassName != "GameControl") {
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
                    if ((DeviceName == "") || (dev.InstanceName.StartsWith(DeviceName))) {
                        // Only add this device if not already in our list
                        if (DeviceList.Where(item => item.InstanceGuid == dev.InstanceGuid).Any())
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

    /// <summary>
    /// Handles the input device in-flight actions
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class ProcessInput : MonoBehaviour
    {
        private static FlightCtrlState UpdatedState = null;
        private static readonly uint CurrentMode = (uint)Config.GameMode.Flight;

        /// <summary>
        /// Update a Flight Vessel axis control (including throttle) by name
        /// </summary>
        /// <param name="Name">The name of the axis to update</param>
        /// <param name="value">The value to set</param>
        private void UpdateAxis(String Name, float value)
        {
            // TODO: Something more elegant (possibly using reflection or HashTable)
            switch (Name)
            {
                // TODO: Add the other supported axes such as wheelSteer
                case "yaw":
                    UpdatedState.yaw = value;
                    break;
                case "pitch":
                    UpdatedState.pitch = value;
                    break;
                case "roll":
                    UpdatedState.roll = value;
                    break;
                case "X":
                    UpdatedState.X = value;
                    break;
                case "Y":
                    UpdatedState.Y = value;
                    break;
                case "Z":
                    UpdatedState.Z = value;
                    break;
                case "mainThrottle":
                    UpdatedState.mainThrottle = (value + 1.0f) / 2.0f;
                    break;
            }
        }

        /// <summary>
        /// Update a Flight Vessel button control (including KSPActionGroups) by name
        /// </summary>
        /// <param name="Name">The name of the axis to update</param>
        /// <param name="value">The value to set</param>
        private void UpdateButton(AltButton Button, Boolean Pressed)
        {
            // TODO: Something more elegant (possibly using reflection or HashTable)
            switch (Button.Mapping[CurrentMode])
            {
                case "yaw":
                case "pitch":
                case "roll":
                case "X":
                case "Y":
                case "Z":
                case "mainThrottle":
                    UpdateAxis(Button.Mapping[CurrentMode], Pressed ? Button.Value[CurrentMode] : 0.0f);
                    break;
                case "ActivateNextStage":
                    if (Pressed)
                        Staging.ActivateNextStage();
                    break;
                case "KSPActionGroup.Stage":
                    // What does this do???
                    if (Pressed)
                        FlightGlobals.ActiveVessel.ActionGroups.ToggleGroup(KSPActionGroup.Stage);
                    break;
                case "KSPActionGroup.Gear":
                    if (Pressed)
                        FlightGlobals.ActiveVessel.ActionGroups.ToggleGroup(KSPActionGroup.Gear);
                    break;
                case "KSPActionGroup.Light":
                    if (Pressed)
                        FlightGlobals.ActiveVessel.ActionGroups.ToggleGroup(KSPActionGroup.Light);
                    break;
                case "KSPActionGroup.RCS":
                    if (Pressed)
                        FlightGlobals.ActiveVessel.ActionGroups.ToggleGroup(KSPActionGroup.RCS);
                    break;
                case "KSPActionGroup.SAS":
                    if (Pressed)
                        FlightGlobals.ActiveVessel.ActionGroups.ToggleGroup(KSPActionGroup.SAS);
                    break;
                case "KSPActionGroup.Brakes":
                    if (Pressed)
                        FlightGlobals.ActiveVessel.ActionGroups.ToggleGroup(KSPActionGroup.Brakes);
                    break;
                case "KSPActionGroup.Abort":
                    if (Pressed)
                        FlightGlobals.ActiveVessel.ActionGroups.ToggleGroup(KSPActionGroup.Abort);
                    break;
                case "KSPActionGroup.Custom01":
                case "KSPActionGroup.Custom02":
                case "KSPActionGroup.Custom03":
                case "KSPActionGroup.Custom04":
                case "KSPActionGroup.Custom05":
                case "KSPActionGroup.Custom06":
                case "KSPActionGroup.Custom07":
                case "KSPActionGroup.Custom08":
                case "KSPActionGroup.Custom09":
                case "KSPActionGroup.Custom10":
                    int i;
                    int.TryParse(Button.Mapping[CurrentMode].Substring("KSPActionGroup.Custom##".Length), out i);
                    if (i > 0)
                        FlightGlobals.ActiveVessel.ActionGroups.ToggleGroup((KSPActionGroup)(128 << i));
                    break;
            }
        }

        private void UpdatePOV(AltPOV Pov, int Angle)
        {
            // Angle in degrees * 100, or -1 at rest.
            // Start by resetting all positions
            for (var i = 0; i < AltDirectInputDevice.NumPOVPositions; i++)
                UpdateButton(Pov.Button[i], false);
            if (Angle < 0)
                return;
            Angle += 36000;
            const int tolerance = 6000; // +/- 60 degrees
            // If our value is less than tolerance degrees apart from a position, we activate it
            UpdateButton(Pov.Button[((Angle - tolerance + 8999) / 9000) % 4], true);
            UpdateButton(Pov.Button[((Angle + tolerance) / 9000) % 4], true);
        }

        /// <summary>
        /// Update the current state of the spacecraft according to all inputs
        /// </summary>
        /// <param name="CurrentState">The current flight control state</param>
        private void UpdateState(FlightCtrlState CurrentState)
        {
            if (Math.Abs(CurrentState.yaw) < Math.Abs(UpdatedState.yaw))
                CurrentState.yaw = UpdatedState.yaw;
            if (Math.Abs(CurrentState.pitch) < Math.Abs(UpdatedState.pitch))
                CurrentState.pitch = UpdatedState.pitch;
            if (Math.Abs(CurrentState.roll) < Math.Abs(UpdatedState.roll))
                CurrentState.roll = UpdatedState.roll;
            if (Math.Abs(CurrentState.X) < Math.Abs(UpdatedState.X))
                CurrentState.X = UpdatedState.X;
            if (Math.Abs(CurrentState.Y) < Math.Abs(UpdatedState.Y))
                CurrentState.Y = UpdatedState.Y;
            if (Math.Abs(CurrentState.Z) < Math.Abs(UpdatedState.Z))
                CurrentState.Z = UpdatedState.Z;
            if (CurrentState.mainThrottle < UpdatedState.mainThrottle)
                CurrentState.mainThrottle = UpdatedState.mainThrottle;

            // If SAS is on, we need to override it or else our changes are ignored
            VesselSAS VesselSAS = FlightGlobals.ActiveVessel.vesselSAS;
            Boolean overrideSAS = (Math.Abs(CurrentState.pitch) > VesselSAS.controlDetectionThreshold) ||
                                  (Math.Abs(CurrentState.yaw) > VesselSAS.controlDetectionThreshold) ||
                                  (Math.Abs(CurrentState.roll) > VesselSAS.controlDetectionThreshold);
            VesselSAS.ManualOverride(overrideSAS);
        }

        /// <summary>
        /// Update the flight control state according to our inputs
        /// </summary>
        /// <param name="CurrentState">The current control state for the active vessel</param>
        private void ControllerInput(FlightCtrlState CurrentState)
        {
            // Does this ever occur?
            if (FlightGlobals.ActiveVessel == null)
            {
                UpdatedState = null;
                // No need to do anything
                return;
            }
            if (Config.DeviceList.Count != 0)
            {
                ConfigNode dupe = new ConfigNode();
                if (UpdatedState == null)
                {
                    // This is the first time we have a state => create a copy
                    // which we'll use to keep track of our Joystick state.
                    // We need to do this as GetBufferedData() only returns
                    // a status if there was a change since last call.
                    UpdatedState = new FlightCtrlState();
                    UpdatedState.CopyFrom(CurrentState);
                }
                foreach (var Device in Config.DeviceList)
                {
                    Device.Joystick.Poll();
                    var data = Device.Joystick.GetBufferedData();
                    foreach (var state in data)
                    {
                        String OffsetName = Enum.GetName(typeof(JoystickOffset), state.Offset);
                        if (OffsetName.StartsWith("Buttons"))
                        {
                            // This call should always succeed
                            uint i = uint.Parse(OffsetName.Substring("Buttons".Length));
                            // For now we consider that a non zero value means the button is pressed
                            UpdateButton(Device.Button[i], (state.Value != 0));
                        }
                        else if (OffsetName.StartsWith("PointOf"))
                        {
                            uint i = uint.Parse(OffsetName.Substring("PointOfViewControllers".Length));
                            UpdatePOV(Device.Pov[i], state.Value);
                        }
                        else for (var i = 0; i < AltDirectInputDevice.AxisList.GetLength(0); i++)
                        {
                            if ((!Device.Axis[i].isAvailable) || (String.IsNullOrEmpty(Device.Axis[i].Mapping[CurrentMode])))
                                continue;
                            if (OffsetName == AltDirectInputDevice.AxisList[i,0])
                            {
                                float value = ((state.Value - Device.Axis[i].Range.Minimum) /
                                    (0.5f * Device.Axis[i].Range.FloatRange)) - 1.0f;
                                if (Device.Axis[i].Inverted[CurrentMode])
                                    value = -value;
                                // We need to handle a slider's deadzone ourselves, as it applies to
                                // the edges rather than the center
                                if (OffsetName.StartsWith("Slider"))
                                {
                                    if (value < ((-10000.0f + Device.Axis[i].DeadZone) / 10000.0f))
                                        value = -1.0f;
                                    if (value > ((10000.0f - Device.Axis[i].DeadZone) / 10000.0f))
                                        value = 1.0f;
                                }
                                UpdateAxis(Device.Axis[i].Mapping[CurrentMode], value);
                            }
                        }
                    }
                    UpdateState(CurrentState);
                }
            }
        }

        public void Start()
        {
#if (DEBUG)
            print("AltInput: ProcessInput.Start()");
#endif
            // TODO: only list/acquire controller if we have some mapping assigned
            foreach (var Device in Config.DeviceList)
            {
                // Device may have been already acquired - release it
                Device.Joystick.Unacquire();
                ScreenMessages.PostScreenMessage("AltInput: Using Controller '" +
                    Device.Joystick.Information.InstanceName + "' (Axes: " + 
                    Device.Joystick.Capabilities.AxeCount + ", Buttons: " + 
                    Device.Joystick.Capabilities.ButtonCount + ", POVs: " +
                    Device.Joystick.Capabilities.PovCount + ")", 10f, ScreenMessageStyle.UPPER_LEFT);
                // Set BufferSize in order to use buffered data.
                Device.Joystick.Properties.BufferSize = 128;
                Device.Joystick.Acquire();
            }
            if (Config.DeviceList.Count == 0)
                ScreenMessages.PostScreenMessage("AltInput: No controller detected", 10f,
                    ScreenMessageStyle.UPPER_LEFT);
            // Add our handler
            Vessel ActiveVessel = FlightGlobals.ActiveVessel;
            if (ActiveVessel != null)
                ActiveVessel.OnFlyByWire += new FlightInputCallback(ControllerInput);
        }

        void OnDestroy()
        {
#if (DEBUG)
            print("AltInput: ProcessInput.OnDestroy()");
#endif
            Vessel ActiveVessel = FlightGlobals.ActiveVessel;
            if (ActiveVessel != null)
                ActiveVessel.OnFlyByWire -= new FlightInputCallback(ControllerInput);
            foreach (var Device in Config.DeviceList)
                Device.Joystick.Unacquire();
        }

    }

}
