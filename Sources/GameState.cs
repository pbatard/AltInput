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

using UnityEngine;
using KSP.IO;

namespace AltInput
{
    public class GameState
    {
        private static FlightCtrlState UpdatedState = null;
        private static Config.GameMode CurrentMode = Config.GameMode.Flight;

        public GameState(FlightCtrlState CurrentState)
        {
            UpdatedState = new FlightCtrlState();
            UpdatedState.CopyFrom(CurrentState);
        }

        public Config.GameMode GetGameMode()
        {
            return CurrentMode;
        }

        /// <summary>
        /// Update a Flight Vessel axis control (including throttle) by name
        /// </summary>
        /// <param name="Name">The name of the axis to update</param>
        /// <param name="value">The value to set</param>
        public void UpdateAxis(String Name, float value)
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
        public void UpdateButton(AltButton Button, Boolean Pressed)
        {
            // TODO: Something more elegant (possibly using reflection or HashTable)
            switch (Button.Mapping[(uint)CurrentMode])
            {
                case "yaw":
                case "pitch":
                case "roll":
                case "X":
                case "Y":
                case "Z":
                case "mainThrottle":
                    UpdateAxis(Button.Mapping[(uint)CurrentMode], Pressed ? Button.Value[(uint)CurrentMode] : 0.0f);
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
                    int.TryParse(Button.Mapping[(uint)CurrentMode].Substring("KSPActionGroup.Custom##".Length), out i);
                    if (i > 0)
                        FlightGlobals.ActiveVessel.ActionGroups.ToggleGroup((KSPActionGroup)(128 << i));
                    break;
            }
        }

        public void UpdatePOV(AltPOV Pov, int Angle)
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
        public void UpdateState(FlightCtrlState CurrentState)
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
    }
}
