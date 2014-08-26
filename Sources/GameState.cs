/*
 * AltInput: Alternate input plugin for Kerbal Space Program
 * Copyright © 2014 Pete Batard <pete@akeo.ie>
 * Thanks go to zitronen for KSPSerialIO, which helped figure out
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
            Landed,
            EVA
        };
        public readonly static String[] ModeName = Enum.GetNames(typeof(Mode));
        public readonly static int NumModes = ModeName.Length;

        public static FlightCtrlState UpdatedState = null;
        public static Mode CurrentMode = Mode.Flight;
        // Build a static list of all FlightCtrlState attributes that are of type float
        // and don't contain "Trim" in their name. These are the axes we will handle.
        public readonly static FieldInfo[] AxisFields =
            typeof(FlightCtrlState).GetFields().Where(item =>
                (item.FieldType == typeof(float)) && (!item.Name.Contains("Trim"))).ToArray();

        private static KerbalEVA Eva = null;

        /// <summary>
        /// Update a Flight Vessel axis control (including throttle) by name
        /// </summary>
        /// <param name="AxisName">The name of the axis to update</param>
        /// <param name="value">The value to set</param>
        public static void UpdateFlightAxis(String AxisName, float value, Boolean isDelta)
        {
            FieldInfo field = typeof(FlightCtrlState).GetField(AxisName);
            Boolean isThrottle = AxisName.EndsWith("Throttle");

            if (isDelta)
                value += (float)field.GetValue(UpdatedState);
            else if (isThrottle)
                value = (value + 1.0f) / 2.0f;
                
            value = Mathf.Clamp(value, isThrottle?0.0f:-1.0f, +1.0f);

            field.SetValue(UpdatedState, value);
        }

        /// <summary>
        /// Update a Flight Vessel button control (including KSPActionGroups) by name
        /// </summary>
        /// <param name="Name">The name of the axis to update</param>
        /// <param name="value">The value to set</param>
        public static void UpdateButton(AltButton Button, Boolean isPressed)
        {
            int i;

            String Mapping = Button.Mapping[(uint)CurrentMode];
            if (Mapping == null)
                return;

            // Check if we have a delta rather than an absolute value
            Boolean isDelta = (Mapping.EndsWith(".Delta"));
            if (isDelta)
                Mapping = Mapping.Split('.')[0];

            // Check if our mapping is a FlightCtrlState axis
            if (AxisFields.Where(item => item.Name == Mapping).Any())
                UpdateFlightAxis(Mapping, isPressed ? Button.Value[(uint)CurrentMode] : 0.0f, isDelta);

            // TODO: Something more elegant (possibly using reflection and a class with all these attributes)
            switch (Mapping)
            {
                case "activateLanded":
                    if (isPressed && (CurrentMode == Mode.Flight))
                    {
                        if (FlightGlobals.ActiveVessel.LandedOrSplashed)
                        {
                            CurrentMode = Mode.Landed;
                            ScreenMessages.PostScreenMessage("Landed mode",
                                5f, ScreenMessageStyle.UPPER_CENTER);
                        }
                        else
                        {
                            ScreenMessages.PostScreenMessage("Vessel is not on the ground!", 
                                5f, ScreenMessageStyle.UPPER_CENTER);
                        }
                    }
                    break;
                case "activateFlight":
                    if (isPressed && (CurrentMode == Mode.Landed))
                    {
                        CurrentMode = Mode.Flight;
                        ScreenMessages.PostScreenMessage("Flight mode",
                                5f, ScreenMessageStyle.UPPER_CENTER);
                    }
                    break;
                case "toggleJetpack":
                    if (isPressed && (CurrentMode == Mode.EVA))
                        Eva.JetpackDeployed = !Eva.JetpackDeployed;
                    break;
                case "switchView":
                    if (isPressed)
                    {
                        FlightCamera fc = FlightCamera.fetch;
                        fc.SetNextMode();
                    }
                    break;
                case "toggleMapView":
                    if (isPressed)
                    {
                        if (MapView.MapIsEnabled)
                            MapView.ExitMapView();
                        else
                            MapView.EnterMapView();
                    }
                    break;
                case "activateStaging":
                    if (isPressed)
                        Staging.ActivateNextStage();
                    break;
                case "toggleGears":
                    if (isPressed)
                        FlightGlobals.ActiveVessel.ActionGroups.ToggleGroup(KSPActionGroup.Gear);
                    break;
                case "toggleLights":
                    if (isPressed)
                        FlightGlobals.ActiveVessel.ActionGroups.ToggleGroup(KSPActionGroup.Light);
                    break;
                case "overrideRCS":
                    FlightGlobals.ActiveVessel.ActionGroups.ToggleGroup(KSPActionGroup.RCS);
                    break;
                case "toggleRCS":
                    if (isPressed)
                        FlightGlobals.ActiveVessel.ActionGroups.ToggleGroup(KSPActionGroup.RCS);
                    break;
                case "overrideSAS":
                    FlightGlobals.ActiveVessel.ActionGroups.ToggleGroup(KSPActionGroup.SAS);
                    break;
                case "toggleSAS":
                    if (isPressed)
                        FlightGlobals.ActiveVessel.ActionGroups.ToggleGroup(KSPActionGroup.SAS);
                    break;
                case "toggleAbort":
                    if (isPressed)
                        FlightGlobals.ActiveVessel.ActionGroups.ToggleGroup(KSPActionGroup.Abort);
                    break;
                case "activateBrakes":
                    FlightGlobals.ActiveVessel.ActionGroups.SetGroup(KSPActionGroup.Brakes, isPressed);
                    break;
                case "toggleBrakes":
                    FlightGlobals.ActiveVessel.ActionGroups.ToggleGroup(KSPActionGroup.Brakes);
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
                    int.TryParse(Mapping.Substring("toggleCustom##".Length), out i);
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
                    int.TryParse(Mapping.Substring("activateCustom##".Length), out i);
                    if (i > 0)
                        FlightGlobals.ActiveVessel.ActionGroups.SetGroup((KSPActionGroup)(128 << i), isPressed);
                    break;
            }
        }

        public static void UpdatePOV(AltPOV Pov, int Angle)
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

        public static void UpdateMode()
        {
            if (FlightGlobals.ActiveVessel.isEVA)
            {
                if (GameState.CurrentMode != GameState.Mode.EVA)
                    GameState.CurrentMode = GameState.Mode.EVA;
                Eva = FlightGlobals.ActiveVessel.rootPart.gameObject.GetComponent<KerbalEVA>();
            }
            else if (Eva != null)
            {
                Eva = null;
            }
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
                    field.SetValue(CurrentState, (float)field.GetValue(UpdatedState));
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
