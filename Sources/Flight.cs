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
using System.IO;

using UnityEngine;
using KSP.IO;

namespace AltInput
{
    /// <summary>
    /// Handles the input device in-flight actions
    /// </summary>
    [KSPAddon(KSPAddon.Startup.Flight, false)]
    public class ProcessInput : MonoBehaviour
    {
        /// <summary>
        /// Update the flight control state according to our inputs
        /// </summary>
        /// <param name="CurrentState">The current control state for the active vessel</param>
        private void ControllerInput(FlightCtrlState CurrentState)
        {
            if (FlightGlobals.ActiveVessel == null)
            {
                GameState.UpdatedState = null;
                // No need to do anything
                return;
            }
            if (Config.DeviceList.Count != 0)
            {
                if (GameState.UpdatedState == null)
                {
                    GameState.UpdatedState = new FlightCtrlState();
                    GameState.UpdatedState.CopyFrom(CurrentState);
                    GameState.CurrentMode = GameState.Mode.Flight;
                }

                foreach (var Device in Config.DeviceList)
                {
                    GameState.CurrentDevice = Device;
                    Device.ProcessInput();
                }

                GameState.UpdateState(CurrentState);
            }
        }

        public void Start()
        {
            // List detected devices and inform the user of any issue
            if (Config.DetectedList.Count == 0)
            {
                var msg = "AltInput: No Controller detected...";
                print(msg);
                ScreenMessages.PostScreenMessage(msg, 10f, ScreenMessageStyle.UPPER_LEFT);
                return;
            }
            foreach (var msg in Config.DetectedList)
            {
                print(msg);
                ScreenMessages.PostScreenMessage(msg, 10f, ScreenMessageStyle.UPPER_LEFT);
            }
            if (Config.ini == null)
            {
                var msg = "AltInput: Config file '" + Config.ini_path + "' was not found";
                print(msg);
                ScreenMessages.PostScreenMessage(msg, 10f, ScreenMessageStyle.UPPER_LEFT);
                return;
            }
            if (Config.iniVersion != Config.currentVersion) {
                var msg = "AltInput: Config file ignored due to Version mismatch (Got " +
                    Config.iniVersion + ", required " + Config.currentVersion + ")";
                print(msg);
                ScreenMessages.PostScreenMessage(msg, 10f, ScreenMessageStyle.UPPER_LEFT);
                return;
            }
            if (Config.DeviceList.Count == 0)
            {
                var msg = "AltInput: No controller configured";
                print(msg);
                ScreenMessages.PostScreenMessage(msg, 5f, ScreenMessageStyle.UPPER_LEFT);
                return;
            }

            // Open the devices that were configured
            foreach (var Device in Config.DeviceList)
                Device.OpenDevice();

            // Add our handler
            if (FlightGlobals.ActiveVessel != null)
                FlightGlobals.ActiveVessel.OnFlyByWire += new FlightInputCallback(ControllerInput);
        }

        // OnFlyByWire isn't invoked when time warp is in use, so we need to add a custom
        // repeated call to check if one of the actions we allow during warp actions was invoked.
        public void FixedUpdate()
        {
            // If we're not at warp, don't do anything (we'll pick our actions from the regular callback)
            if (TimeWarp.CurrentRate == 1)
                return;

            foreach (var Device in Config.DeviceList)
            {
                GameState.CurrentDevice = Device;
                Device.ProcessInput();
            }
        }

        void OnDestroy()
        {
            foreach (var Device in Config.DeviceList)
                Device.CloseDevice();
        }
    }
}
