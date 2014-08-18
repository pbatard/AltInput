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
using System.Threading;

using UnityEngine;
using KSP.IO;
// IMPORTANT: To be able to work with Unity, which is *BROKEN*,
// you must have a patched version of SharpDX.DirectInput such
// as the one provided with this project (in Libraries/)
// See: https://github.com/sharpdx/SharpDX/issues/406
using SharpDX.DirectInput;

namespace AltInput
{

    [KSPAddon(KSPAddon.Startup.MainMenu, false)]
    public class Config : MonoBehaviour
    {
        public static PluginConfiguration cfg = PluginConfiguration.CreateForType<Config>();

        void Awake()
        {
            print("AltInput: Saving configuration...");
            cfg.SetValue("Test", "value");
            cfg.save();
        }

    }

    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class ProcessInput : MonoBehaviour
    {
        public static Vessel ActiveVessel = null;
        private ScreenMessageStyle MessageStyle = ScreenMessageStyle.UPPER_LEFT;
        private Joystick joystick = null;
        private DirectInput directInput = null;
        private Guid joystickGuid = Guid.Empty;
        private static FlightCtrlState UpdatedState = null;
        // TODO: read this from the config file
        const float Threshold = 0.005f;

        private void AxisInput(FlightCtrlState ActiveState)
        {
            ActiveVessel = FlightGlobals.ActiveVessel;
            if (ActiveVessel == null)
            {
                UpdatedState = null;
                // No need to do anything
                return;
            }
            if (joystick != null)
            {
                ConfigNode dupe = new ConfigNode();
                if (UpdatedState == null)
                {
                    UpdatedState = new FlightCtrlState();
                    ActiveState.Save(dupe);
                    UpdatedState.Load(dupe);
                }

                joystick.Poll();
                var data = joystick.GetBufferedData();
                foreach (var state in data)
                {
                    // TODO: pick the range and deal with inversions dynamically
                    if (state.Offset == JoystickOffset.X)
                        UpdatedState.yaw = (state.Value - 32768.0f) / 32768.0f;
                    if (state.Offset == JoystickOffset.Y)
                        UpdatedState.pitch = (state.Value - 32768.0f) / 32768.0f;
                    if (state.Offset == JoystickOffset.RotationZ)
                        UpdatedState.roll = (state.Value - 32768.0f) / 32768.0f;
                    if (state.Offset == JoystickOffset.Sliders0)
                    {
                        UpdatedState.mainThrottle = (65536.0f - state.Value) / 65536.0f;
                        if (UpdatedState.mainThrottle < Threshold)
                            UpdatedState.mainThrottle = 0.0f;
                    }
                }
                // If SAS is on, we need to override it or else our changes are ignored
                Boolean overrideSAS = (Math.Abs(UpdatedState.pitch) > Threshold) ||
                    (Math.Abs(UpdatedState.yaw) > Threshold) || (Math.Abs(UpdatedState.roll) > Threshold);
                ActiveVessel.vesselSAS.ManualOverride(overrideSAS);
                UpdatedState.Save(dupe);
                ActiveState.Load(dupe);
            }
        }

        public void Awake()
        {
            ScreenMessages.PostScreenMessage("AltInput Awake", 10f, MessageStyle);
        }

        public void Start()
        {
            // https://github.com/sharpdx/SharpDX-Samples/blob/master/WindowsDesktop/DirectInput/JoystickApp/Program.cs
            // Initialize DirectInput
            directInput = new DirectInput();
            // Find a Joystick Guid
            joystickGuid = Guid.Empty;
            foreach (var deviceInstance in directInput.GetDevices(SharpDX.DirectInput.DeviceType.Gamepad,
                DeviceEnumerationFlags.AllDevices))
                joystickGuid = deviceInstance.InstanceGuid;
            // If Gamepad not found, look for a Joystick
            if (joystickGuid == Guid.Empty)
                foreach (var deviceInstance in directInput.GetDevices(SharpDX.DirectInput.DeviceType.Joystick,
                    DeviceEnumerationFlags.AllDevices))
                    joystickGuid = deviceInstance.InstanceGuid;
            // If Joystick not found, throws an error
            if (joystickGuid == Guid.Empty)
            {
                ScreenMessages.PostScreenMessage("AltInput: No joystick found.", 10f, MessageStyle);
                return;
            }

            // Instantiate the joystick
            joystick = new Joystick(directInput, joystickGuid);
            ScreenMessages.PostScreenMessage("AltInput: Found " + joystick.Information.InstanceName, 10f, MessageStyle);
            ScreenMessages.PostScreenMessage("AltInput: Axes: " + joystick.Capabilities.AxeCount + ", Buttons: " +
                joystick.Capabilities.ButtonCount + ", POVs: " + joystick.Capabilities.PovCount, 10f, MessageStyle);
            // Set BufferSize in order to use buffered data.
            joystick.Properties.BufferSize = 128;
            // Acquire the joystick
            joystick.Acquire();
            // Set our deadzones
            // TODO: read those from the config file
            foreach (var name in new string[] { "X", "Y", "RotationZ" })
            {
                var prop = joystick.GetObjectPropertiesByName(name);
                prop.DeadZone = 1000;
            }

            // Add our handler
            ActiveVessel = FlightGlobals.ActiveVessel;
            if (ActiveVessel != null)
                ActiveVessel.OnFlyByWire += new FlightInputCallback(AxisInput);
        }
    }

}
