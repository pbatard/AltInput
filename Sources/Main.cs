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
        /// <summary>Name of the KSP FlightCtrlState attribute this axis should map to</summary>
        public String Mapping;
        /// <summary>Whether this axis should be inverted</summary>
        public Boolean Inverted;
        /// <summary>The range of this axis</summary>
        // TODO: remove this as we can get it from the Joystick instance
        public AltRange Range;
    }

    /// <summary>
    /// A button on the input device
    /// </summary>
    public struct AltButton
    {
        /// <summary>Name of the KSP FlightCtrlState attribute this button should map to</summary>
        public String Mapping;
        /// <summary>For an axis, the value we pass to KSP when the button is pressed</summary>
        public float Value;
        /// <summary>Whether this button action should be inverted</summary>
        public Boolean Inverted;
    }

    /// <summary>
    /// A Point Of View control on the input device
    /// </summary>
    public struct AltPOV
    {
        public AltButton Up;
        public AltButton Down;
        public AltButton Right;
        public AltButton Left;
    }

    // TODO: we may derive this from a parent class when we support more than DI
    /// <summary>
    /// A Direct Input Device (typically a game controller)
    /// </summary>
    public class AltDirectInputDevice
    {
        // Names for the axes. Using a double string array allows to map not so
        // user-friendly DirectInput names to more user-friendly config counterparts.
        public readonly static String[,] AxisList = new String[,] {
            { "X", "X" }, { "Y", "Y" }, { "Z", "Z" },
            { "RotationX", "RotationX" }, { "RotationY", "RotationY" }, { "RotationZ", "RotationZ" },
            { "Sliders0", "Slider1" }, { "Sliders1", "Slider2" } };
        public readonly String[] KSPAxisList = { "yaw", "pitch", "roll", "mainThrottle", "X", "Y", "Z" };
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
            // The values below are the maximum items from directInput
            this.Axis = new AltAxis[AltDirectInputDevice.AxisList.GetLength(0)];
            this.Pov = new AltPOV[this.Joystick.Capabilities.PovCount];
            this.Button = new AltButton[this.Joystick.Capabilities.ButtonCount];
        }
    }

    /// <summary>
    /// Handles the input device configuration
    /// </summary>
    [KSPAddon(KSPAddon.Startup.MainMenu, false)]
    public class Config : MonoBehaviour
    {
        // Good developers do NOT let end-users fiddle with XML configuration files...
        private static IniFile ini = new IniFile(Directory.GetCurrentDirectory() +
            @"\Plugins\PluginData\AltInput\config.ini");
        private DirectInput directInput = new DirectInput();
        private static readonly String[] Separators = { "[", "]", " ", "\t" };
        public static List<AltDirectInputDevice> DeviceList = new List<AltDirectInputDevice>();
        public static float Threshold;

        private void ParseButton(ref AltButton Button, String ConfigData)
        {
            try
            {
                String[] Values = ConfigData.Split(Separators, StringSplitOptions.RemoveEmptyEntries);
                Button.Mapping = Values[0];
                float.TryParse(Values[1], out Button.Value);
            }
            catch (Exception) { }
        }

        /// <summary>
        /// Parse the configuration file and fill the Direct Input device attributes
        /// </summary>
        private void SetAttributes(AltDirectInputDevice Device, String Section)
        {
            InputRange Range;
            int DeadZone = 0;

            // Parse the global dead zone attribute for this device. This is the dead zone
            // that will be applied if there isn't a specific axis override
            int.TryParse(ini.IniReadValue(Section, "DeadZone"), out Device.DeadZone);

            // Process the axes
            for (var i = 0; i < AltDirectInputDevice.AxisList.GetLength(0); i++)
            {
                // We get a NOTFOUND exception when probing the range for unused axes.
                // Use that to indicate if the axe is available
                try
                {
                    Range = Device.Joystick.GetObjectPropertiesByName(AltDirectInputDevice.AxisList[i, 0]).Range;
                    Device.Axis[i].Range.Minimum = Range.Minimum;
                    Device.Axis[i].Range.Maximum = Range.Maximum;
                    Device.Axis[i].Range.FloatRange = 1.0f * (Range.Maximum - Range.Minimum);

                    // TODO: check if mapping name is valid and if it's already been assigned
                    Device.Axis[i].Mapping = ini.IniReadValue(Section, AltDirectInputDevice.AxisList[i, 1]);
                    int.TryParse(ini.IniReadValue(Section, AltDirectInputDevice.AxisList[i, 1] + ".DeadZone"), out DeadZone);
                    if (DeadZone == 0)
                        // Override with global dead zone if none was specified
                        // NB: This prohibits setting a global dead zone and then an individual to 0 - oh well...
                        DeadZone = Device.DeadZone;
                    Device.Joystick.GetObjectPropertiesByName(AltDirectInputDevice.AxisList[i, 0]).DeadZone = DeadZone;
                    Boolean.TryParse(ini.IniReadValue(Section, AltDirectInputDevice.AxisList[i, 1] + ".Inverted"),
                        out Device.Axis[i].Inverted);
                    Device.Axis[i].isAvailable = (Device.Axis[i].Range.FloatRange != 0.0f);
                    if (!Device.Axis[i].isAvailable)
                        print("Altinput: WARNING - Axis " + AltDirectInputDevice.AxisList[i, 1] +
                            " was disabled because its range is zero.");

                }
                catch (Exception)
                {
                    Device.Axis[i].isAvailable = false;
                }
#if (DEBUG)
                print("Altinput: Axis #" + (i + 1) + ": isPresent = " + Device.Axis[i].isAvailable +
                    ", Mapping = " + Device.Axis[i].Mapping +
                    ", DeadZone = " + DeadZone +
                    ", Inverted = " + Device.Axis[i].Inverted +
                    ", RangeMin = " + Device.Axis[i].Range.Minimum +
                    ", RangeMax = " + Device.Axis[i].Range.Maximum);
#endif
            }

            // Process the POV controls
            for (var i = 1; i <= Device.Joystick.Capabilities.PovCount; i++)
            {
                ParseButton(ref Device.Pov[i-1].Up, ini.IniReadValue(Section, "POV" + i + ".Up"));
                ParseButton(ref Device.Pov[i-1].Down, ini.IniReadValue(Section, "POV" + i + ".Down"));
                ParseButton(ref Device.Pov[i-1].Left, ini.IniReadValue(Section, "POV" + i + ".Left"));
                ParseButton(ref Device.Pov[i-1].Right, ini.IniReadValue(Section, "POV" + i + ".Right"));
#if (DEBUG)
                print("Altinput: POV #" + i + ": Up = " + Device.Pov[i-1].Up.Mapping + ", Value = " + Device.Pov[i-1].Up.Value +
                    ", Down = " + Device.Pov[i-1].Down.Mapping + ", Value = " + Device.Pov[i-1].Down.Value +
                    ", Left = " + Device.Pov[i-1].Left.Mapping + ", Value = " + Device.Pov[i-1].Left.Value +
                    ", Right = " + Device.Pov[i-1].Right.Mapping + ", Value = " + Device.Pov[i-1].Right.Value);
#endif
            }

            // Process the buttons
            for (var i = 1; i <= Device.Joystick.Capabilities.ButtonCount; i++)
            {
                ParseButton(ref Device.Button[i-1], ini.IniReadValue(Section, "Button" + i));
#if (DEBUG)
                print("Altinput: Button #" + i + ": Mapping = " + Device.Button[i-1].Mapping +
                    ", Value = " + Device.Button[i-1].Value);
#endif
            }

        }

        /// <summary>
        /// This method is the first called by the Unity engine when it instantiates
        /// the game element it is associated to (here the main game menu)
        /// </summary>
        void Awake()
        {
            // Try to match the devices we find with our config file
            int NumDevices = 0;
            String InterfaceName, ClassName, DeviceName;
            AltDirectInputDevice Device;
            DeviceClass InstanceClass = DeviceClass.GameControl;
            Boolean found;

            Int32.TryParse(ini.IniReadValue("global", "NumDevices"), out NumDevices);
            if (NumDevices == 0)
                NumDevices = 4;
            float.TryParse(ini.IniReadValue("global", "Threshold"), out Threshold);

            for (var i = 1; i <= NumDevices; i++)
            {
                String InputName = "input" + i;
                InterfaceName = ini.IniReadValue(InputName, "Interface");
                if ((InterfaceName != "") && (InterfaceName != "DirectInput")) {
                    print("AltInput[" + InputName + "]: Only 'DirectInput' is supported for Interface type");
                } else {
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
                        if ((DeviceName == "") || (dev.InstanceName == DeviceName)) {
                            found = false;
                            foreach (var Instance in DeviceList) {
                                if (Instance.InstanceGuid == dev.InstanceGuid) {
                                    found = true;
                                    break;
                                }
                            }
                            if (found)
                                continue;
                            // Only add this device if not already present in our list
                            Device = new AltDirectInputDevice(directInput, InstanceClass, dev.InstanceGuid);
                            SetAttributes(Device, InputName);
                            DeviceList.Add(Device);
                            print("AltInput: Added controller '" + dev.InstanceName + "'");
                        }
                    }
                }
            }
            if (DeviceList.Count == 0)
            {
                print("AltInput: No controller found");
                return;
            }
        }

    }

    /// <summary>
    /// Handles the input device in-flight actions
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class ProcessInput : MonoBehaviour
    {
        private static FlightCtrlState UpdatedState = null;

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
                    if (UpdatedState.mainThrottle < Config.Threshold)
                        UpdatedState.mainThrottle = 0.0f;
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
            switch (Button.Mapping)
            {
                case "yaw":
                case "pitch":
                case "roll":
                case "X":
                case "Y":
                case "Z":
                case "mainThrottle":
                    UpdateAxis(Button.Mapping, Pressed ? Button.Value : 0.0f);
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
                    int.TryParse(Button.Mapping.Substring("KSPActionGroup.Custom##".Length), out i);
                    if (i > 0)
                        FlightGlobals.ActiveVessel.ActionGroups.ToggleGroup((KSPActionGroup)(128 << i));
                    break;
            }
        }

        private void UpdatePOV(AltPOV Pov, int Value)
        {
            // Value is an angle in degrees * 100, or -1 at rest.
            // Now, since my POV has only 8 discrete positions, and I don't care that
            // much about trying to sin'ing and cos'ing the angle to send a proportional
            // axis value, I'm gonna use a plain old switch with hardcoded angles.
            // If you're unhappy about this, you can send a patch!
            switch (Value)
            {
                // Be mindful that, if you use an axis for say Left and Right, the
                // "true" action(s) need to comes last, else you might end up setting
                // your axis to 1.0, and then undoing it to 0.0 on false...
                case 0: // Top
                    UpdateButton(Pov.Down, false);
                    UpdateButton(Pov.Left, false);
                    UpdateButton(Pov.Right, false);
                    UpdateButton(Pov.Up, true);
                    break;
                case 4500: // Top-Right
                    UpdateButton(Pov.Down, false);
                    UpdateButton(Pov.Left, false);
                    UpdateButton(Pov.Up, true);
                    UpdateButton(Pov.Right, true);
                    break;
                case 9000: // Right
                    UpdateButton(Pov.Up, false);
                    UpdateButton(Pov.Down, false);
                    UpdateButton(Pov.Left, false);
                    UpdateButton(Pov.Right, true);
                    break;
                case 13500: // Down-Right
                    UpdateButton(Pov.Up, false);
                    UpdateButton(Pov.Left, false);
                    UpdateButton(Pov.Down, true);
                    UpdateButton(Pov.Right, true);
                    break;
                case 18000: // Down
                    UpdateButton(Pov.Up, false);
                    UpdateButton(Pov.Left, false);
                    UpdateButton(Pov.Right, false);
                    UpdateButton(Pov.Down, true);
                    break;
                case 22500: // Down-Left
                    UpdateButton(Pov.Up, false);
                    UpdateButton(Pov.Right, false);
                    UpdateButton(Pov.Down, true);
                    UpdateButton(Pov.Left, true);
                    break;
                case 27000: // Left
                    UpdateButton(Pov.Up, false);
                    UpdateButton(Pov.Down, false);
                    UpdateButton(Pov.Right, false);
                    UpdateButton(Pov.Left, true);
                    break;
                case 31500: // Up-Left
                    UpdateButton(Pov.Down, false);
                    UpdateButton(Pov.Right, false);
                    UpdateButton(Pov.Up, true);
                    UpdateButton(Pov.Left, true);
                    break;
                default:    // Rest
                    UpdateButton(Pov.Up, false);
                    UpdateButton(Pov.Down, false);
                    UpdateButton(Pov.Left, false);
                    UpdateButton(Pov.Right, false);
                    break;
            }
        }

        /// <summary>
        /// Update the current state of the spacecraft according to all inputs
        /// </summary>
        /// <param name="CurrentState">The current flight control state</param>
        private void UpdateState(ref FlightCtrlState CurrentState)
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
            Boolean overrideSAS = (Math.Abs(CurrentState.pitch) > Config.Threshold) ||
                                  (Math.Abs(CurrentState.yaw) > Config.Threshold) || 
                                  (Math.Abs(CurrentState.roll) > Config.Threshold);
            FlightGlobals.ActiveVessel.vesselSAS.ManualOverride(overrideSAS);
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
                            if ((!Device.Axis[i].isAvailable) || (String.IsNullOrEmpty(Device.Axis[i].Mapping)))
                                continue;
                            if (OffsetName == AltDirectInputDevice.AxisList[i,0])
                            {
                                float value = ((state.Value - Device.Axis[i].Range.Minimum) / (0.5f * Device.Axis[i].Range.FloatRange)) - 1.0f;
                                if (Device.Axis[i].Inverted)
                                    value = -value;
                                UpdateAxis(Device.Axis[i].Mapping, value);
                            }
                        }
                    }
                    UpdateState(ref CurrentState);
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
    }

}
