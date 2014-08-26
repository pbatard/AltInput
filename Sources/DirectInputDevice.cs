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
    public class AltDirectInputDevice : AltDevice
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
                this.Axis[i].Mapping = new String[GameState.NumModes];
                this.Axis[i].Inverted = new Boolean[GameState.NumModes];
            }
            this.Pov = new AltPOV[this.Joystick.Capabilities.PovCount];

            for (var i = 0; i < this.Pov.Length; i++)
            {
                this.Pov[i].Button = new AltButton[NumPOVPositions];
                for (var j = 0; j < NumPOVPositions; j++)
                {
                    this.Pov[i].Button[j].Mapping = new String[GameState.NumModes];
                    this.Pov[i].Button[j].Value = new float[GameState.NumModes];
                }
            }
            this.Button = new AltButton[this.Joystick.Capabilities.ButtonCount];
            for (var i = 0; i < this.Button.Length; i++)
            {
                this.Button[i].Mapping = new String[GameState.NumModes];
                this.Button[i].Value = new float[GameState.NumModes];
            }
        }

        public override void ProcessInput()
        {
            Joystick.Poll();
            var data = Joystick.GetBufferedData();
            foreach (var state in data)
            {
                String OffsetName = Enum.GetName(typeof(JoystickOffset), state.Offset);
                if (OffsetName.StartsWith("Buttons"))
                {
                    // This call should always succeed
                    uint i = uint.Parse(OffsetName.Substring("Buttons".Length));
                    // For now we consider that a non zero value means the button is pressed
                    GameState.UpdateButton(Button[i], (state.Value != 0));
                }
                else if (OffsetName.StartsWith("PointOf"))
                {
                    uint i = uint.Parse(OffsetName.Substring("PointOfViewControllers".Length));
                    GameState.UpdatePOV(Pov[i], state.Value);
                }
                else for (var i = 0; i < AltDirectInputDevice.AxisList.GetLength(0); i++)
                {
                    uint CurrentMode = (uint)GameState.CurrentMode;
                    if ((!Axis[i].isAvailable) || (String.IsNullOrEmpty(Axis[i].Mapping[CurrentMode])))
                        continue;
                    if (OffsetName == AltDirectInputDevice.AxisList[i,0])
                    {
                        float value = ((state.Value - Axis[i].Range.Minimum) /
                            (0.5f * Axis[i].Range.FloatRange)) - 1.0f;
                        if (Axis[i].Inverted[(uint)CurrentMode])
                            value = -value;
                        // We need to handle a slider's deadzone ourselves, as it applies to
                        // the edges rather than the center
                        if (OffsetName.StartsWith("Slider"))
                        {
                            if (value < ((-10000.0f + Axis[i].DeadZone) / 10000.0f))
                                value = -1.0f;
                            if (value > ((10000.0f - Axis[i].DeadZone) / 10000.0f))
                                value = 1.0f;
                        }
                        GameState.UpdateFlightAxis(Axis[i].Mapping[CurrentMode], value, false);
                    }
                }
            }
        }

        public override void OpenDevice()
        {
            // Device may have been already acquired - release it
            Joystick.Unacquire();
            ScreenMessages.PostScreenMessage("AltInput: Using Controller '" +
                Joystick.Information.InstanceName + "': " + Joystick.Capabilities.AxeCount + " Axes, " +
                Joystick.Capabilities.ButtonCount + " Buttons, " + Joystick.Capabilities.PovCount +
                 " POV(s)", 10f, ScreenMessageStyle.UPPER_LEFT);
            // Set BufferSize in order to use buffered data.
            Joystick.Properties.BufferSize = 128;
            Joystick.Acquire();
        }

        public override void CloseDevice()
        {
            Joystick.Unacquire();
        }
    }
}
