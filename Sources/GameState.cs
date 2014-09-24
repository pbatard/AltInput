/*
 * AltInput: Alternate input plugin for Kerbal Space Program
 * Copyright © 2014 Pete Batard <pete@akeo.ie>
 * Thanks go to zitronen for KSPSerialIO, which helped figure out
 * spacecraft controls in KSP: https://github.com/zitron-git/KSPSerialIO
 * TimeWarp handling from MechJeb2 by BloodyRain2k:
 * https://github.com/MuMech/MechJeb2/blob/master/MechJeb2/MechJebModuleWarpController.cs
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
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using UnityEngine;
using KSP.IO;

namespace AltInput
{
    public class GameState : MonoBehaviour
    {
        /// <summary>The game modes we support</summary>
        public enum Mode
        {
            Flight = 0,
            AltFlight,
            Ground,
        };
        public readonly static String[] ModeName = Enum.GetNames(typeof(Mode));
        public readonly static int NumModes = ModeName.Length;
        public static AltDevice CurrentDevice;

        public static FlightCtrlState UpdatedState = null;
        public static Mode CurrentMode = Mode.Flight;
        // Build a static list of all FlightCtrlState attributes that are of type float
        // and don't contain "Trim" in their name. These are the axes we will handle.
        public readonly static FieldInfo[] AxisFields =
            typeof(FlightCtrlState).GetFields().Where(item =>
                (item.FieldType == typeof(float)) && (!item.Name.Contains("Trim"))).ToArray();

        /// <summary>
        /// Update a Flight Vessel axis control (including throttle) by name
        /// </summary>
        /// <param name="Mapping">The mapping for the axis to update</param>
        /// <param name="value">The value to set, if override</param>
        /// <param name="factor">The factor for the computed value</param>
        public static void UpdateAxis(AltMapping Mapping, float value, float factor)
        {
            FieldInfo field = typeof(FlightCtrlState).GetField(Mapping.Action);
            if (field == null)
            {
                print("AltInput: '" + Mapping.Action + "' is not a valid Axis name");
                return;
            }
            Boolean isThrottle = Mapping.Action.EndsWith("Throttle");
            if (Mapping.Type == MappingType.Delta)
                value += (float)field.GetValue(UpdatedState);
            else if (isThrottle)
                value = (value + 1.0f) / 2.0f;

            value *= factor;
            value = Mathf.Clamp(value, isThrottle?0.0f:-1.0f, +1.0f);
            field.SetValue(UpdatedState, value);
        }

        private static Mode GetNextMode()
        {
            int mode = (int)CurrentMode;
            do
                mode = (mode + 1) % NumModes;
            while ( (!CurrentDevice.enabledModes[mode]) ||
                (((Mode)mode == Mode.Ground) && (!FlightGlobals.ActiveVessel.LandedOrSplashed)) );
            return (Mode)mode;
        }

        /// <summary>
        /// Update a Flight Vessel button control (including KSPActionGroups) by name
        /// </summary>
        /// <param name="Name">The name of the axis to update</param>
        /// <param name="value">The value to set</param>
        public static void UpdateButton(AltMapping Mapping, float value)
        {
            int i;

            if (Mapping.Action == null)
                return;

            // If we are in a time warp, drop all actions besides the ones we authorise below
            if (TimeWarp.CurrentRate != 1)
            {
                switch (Mapping.Action)
                {
                    case "increaseWarp":
                    case "decreaseWarp":
                    case "switchMode":
                    case "switchView":
                    case "toggleMapView":
                        break;
                    default:
                        return;
                }
            }

            // Check if our mapping is a FlightCtrlState axis
            if (AxisFields.Where(item => item.Name == Mapping.Action).Any())
            {
                UpdateAxis(Mapping, value * Mapping.Value, 1.0f);
                return;
            }

            // Most actions only need to occur when the button state is pressed.
            // We handle the few that don't here
            if (value < 0.5)
            {
                switch (Mapping.Action)
                {
                    case "overrideRCS":
                        FlightGlobals.ActiveVessel.ActionGroups.SetGroup(KSPActionGroup.RCS, false);
                        break;
                    case "activateBrakes":
                        FlightGlobals.ActiveVessel.ActionGroups.SetGroup(KSPActionGroup.Brakes, false);
                        break;
                    case "overrideSAS":
                        FlightGlobals.ActiveVessel.ActionGroups.SetGroup(KSPActionGroup.SAS, false);
                        break;
                    case "activateCustom01":
                    case "activateCustom02":
                    case "activateCustom03":
                    case "activateCustom04":
                    case "activateCustom05":
                    case "activateCustom06":
                    case "activateCustom07":
                    case "activateCustom08":
                    case "activateCustom09":
                    case "activateCustom10":
                        int.TryParse(Mapping.Action.Substring("activateCustom##".Length), out i);
                        if (i > 0)
                            FlightGlobals.ActiveVessel.ActionGroups.SetGroup((KSPActionGroup)(128 << i), true);
                        break;
                }
                return;
            }

            switch (Mapping.Action)
            {
                case "increaseWarp":
                    // Do a bunch of checks to see if we can increase the warp rate:
                    if (TimeWarp.CurrentRateIndex + 1 == TimeWarp.fetch.warpRates.Length)
                        break; // Already at max warp
                    if (!FlightGlobals.ActiveVessel.LandedOrSplashed)
                    {
                        CelestialBody mainBody = FlightGlobals.getMainBody();
                        double instantAltitudeASL = (FlightGlobals.ActiveVessel.CoM - mainBody.position).magnitude - mainBody.Radius;
                        if (TimeWarp.fetch.GetAltitudeLimit(TimeWarp.CurrentRateIndex + 1, mainBody) > instantAltitudeASL)
                            break; // Altitude too low to increase warp
                    }
                    if (TimeWarp.fetch.warpRates[TimeWarp.CurrentRateIndex] != TimeWarp.CurrentRate)
                        break; // Most recent warp change is not yet complete
                    TimeWarp.SetRate(TimeWarp.CurrentRateIndex + 1, false);
                    break;
                case "decreaseWarp":
                    if (TimeWarp.CurrentRateIndex == 0)
                        break; // Already at minimum warp
                    TimeWarp.SetRate(TimeWarp.CurrentRateIndex - 1, false);
                    break;
                case "switchMode":
                    Mode NextMode = GetNextMode();
                    if (NextMode != CurrentMode)
                    {
                        // Ensure that we reset our controls and buttons before switching
                        // TODO: Drop ResetDevice() and check that no buttons/POVs are active besides mode switch instead
                        CurrentDevice.ResetDevice();
                        CurrentMode = NextMode;
                        ScreenMessages.PostScreenMessage("Input Mode: " + ModeName[(int)CurrentMode],
                            1f, ScreenMessageStyle.UPPER_CENTER);
                    }
                    break;
                case "switchView":
                    FlightCamera fc = FlightCamera.fetch;
                    fc.SetNextMode();
                    break;
                case "toggleMapView":
                    if (MapView.MapIsEnabled)
                        MapView.ExitMapView();
                    else
                        MapView.EnterMapView();
                    break;
                case "toggleNavBall":
                    // MapView.MapCollapse_navBall is not currently exposed by the API
                    // so we need to use reflection
                    ScreenSafeUISlideTab navball = (ScreenSafeUISlideTab)typeof(MapView).
                        GetField("MapCollapse_navBall").GetValue(MapView.fetch);
                    if (navball.expanded)
                        navball.Collapse();
                    else
                        navball.Expand();
                    break;
                case "activateStaging":
                    Staging.ActivateNextStage();
                    break;
                case "toggleGears":
                    FlightGlobals.ActiveVessel.ActionGroups.ToggleGroup(KSPActionGroup.Gear);
                    break;
                case "toggleLights":
                    FlightGlobals.ActiveVessel.ActionGroups.ToggleGroup(KSPActionGroup.Light);
                    break;
                case "overrideRCS":
                    FlightGlobals.ActiveVessel.ActionGroups.SetGroup(KSPActionGroup.RCS, true);
                    break;
                case "toggleRCS":
                    FlightGlobals.ActiveVessel.ActionGroups.ToggleGroup(KSPActionGroup.RCS);
                    break;
                case "overrideSAS":
                    FlightGlobals.ActiveVessel.ActionGroups.SetGroup(KSPActionGroup.SAS, true);
                    break;
                case "toggleSAS":
                    FlightGlobals.ActiveVessel.ActionGroups.ToggleGroup(KSPActionGroup.SAS);
                    break;
                case "toggleAbort":
                    FlightGlobals.ActiveVessel.ActionGroups.ToggleGroup(KSPActionGroup.Abort);
                    break;
                case "activateBrakes":
                    FlightGlobals.ActiveVessel.ActionGroups.SetGroup(KSPActionGroup.Brakes, true);
                    break;
                case "toggleBrakes":
                    FlightGlobals.ActiveVessel.ActionGroups.ToggleGroup(KSPActionGroup.Brakes);
                    break;
                case "killThrottle":
                    UpdatedState.mainThrottle = 0.0f;
                    break;
                case "fullThrottle":
                    UpdatedState.mainThrottle = 1.0f;
                    break;
                case "toggleCustom01":
                case "toggleCustom02":
                case "toggleCustom03":
                case "toggleCustom04":
                case "toggleCustom05":
                case "toggleCustom06":
                case "toggleCustom07":
                case "toggleCustom08":
                case "toggleCustom09":
                case "toggleCustom10":
                    int.TryParse(Mapping.Action.Substring("toggleCustom##".Length), out i);
                    if (i > 0)
                        FlightGlobals.ActiveVessel.ActionGroups.ToggleGroup((KSPActionGroup)(128 << i));
                    break;
                case "activateCustom01":
                case "activateCustom02":
                case "activateCustom03":
                case "activateCustom04":
                case "activateCustom05":
                case "activateCustom06":
                case "activateCustom07":
                case "activateCustom08":
                case "activateCustom09":
                case "activateCustom10":
                    int.TryParse(Mapping.Action.Substring("activateCustom##".Length), out i);
                    if (i > 0)
                        FlightGlobals.ActiveVessel.ActionGroups.SetGroup((KSPActionGroup)(128 << i), true);
                    break;
                default:
                    print("AltInput: Unhandled action '" + Mapping.Action + "'");
                    break;
            }
        }

        public static void UpdatePov(AltPOV Pov, int Angle, uint mode, Boolean onlyContinuous)
        {
            // Angle in degrees * 100, or -1 at rest.
            // Start by resetting all positions if needed
            if (Angle != Pov.LastValue)
            {
                for (var i = 0; i < AltDirectInputDevice.NumPOVPositions; i++)
                    UpdateButton(Pov.Button[i].Mapping[mode], 0.0f);
            }
            if (Angle < 0)
                return;
            Angle += 36000;
            const int tolerance = 6000; // +/- 60 degrees
            int B1 = ((Angle - tolerance + 8999) / 9000) % 4;
            int B2 = ((Angle + tolerance) / 9000) % 4;
            // If our value is less than tolerance degrees apart from a position, we activate it
            if ((!onlyContinuous) || (Pov.Button[B1].Continuous[mode]))
                UpdateButton(Pov.Button[B1].Mapping[mode], 1.0f);
            if ((!onlyContinuous) || (Pov.Button[B2].Continuous[mode]))
                UpdateButton(Pov.Button[B2].Mapping[mode], 1.0f);
        }

        /// <summary>
        /// Update the current state of the spacecraft according to all inputs
        /// </summary>
        /// <param name="CurrentState">The current flight control state</param>
        public static void UpdateState(FlightCtrlState CurrentState)
        {
            // Go through all our axes to find the ones we need to update
            foreach (FieldInfo field in AxisFields)
            {
                if (Math.Abs((float)field.GetValue(CurrentState)) < Math.Abs((float)field.GetValue(UpdatedState)))
                {
                    field.SetValue(CurrentState, (float)field.GetValue(UpdatedState));
                    // The throttles are a real PITA to override
                    if (field.Name == "mainThrottle")
                        FlightInputHandler.state.mainThrottle = 0.0f;
                    else if (field.Name == "wheelThrottle")
                        FlightInputHandler.state.wheelThrottle = 0.0f;
                }
            }

            // If SAS is on, we need to override it or else our changes are ignored
            VesselSAS VesselSAS = FlightGlobals.ActiveVessel.vesselSAS;
            Boolean overrideSAS = (Math.Abs(CurrentState.pitch) > VesselSAS.controlDetectionThreshold) ||
                                    (Math.Abs(CurrentState.yaw) > VesselSAS.controlDetectionThreshold) ||
                                    (Math.Abs(CurrentState.roll) > VesselSAS.controlDetectionThreshold);
            VesselSAS.ManualOverride(overrideSAS);
        }
    }
}
