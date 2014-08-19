AltInput — Alternate Input module for Kerbal Space Program
==========================================================

Description
--------------------------------------

This module provides an alternate input method for Kerbal Space Program.

Currently it only does so for [DirectInput](httpmsdn.microsoft.com/en-us/library/windows/desktop/ee416842.aspx)
Joysticks, and as such will only work on Windows, but it could be extended to
support any other kind of input.

__IMPORTANT:__ This module provides its OWN version of the [SharpDX](http://sharpdx.org/)
DLLs, due to a [BUG](https://github.com/sharpdx/SharpDX/issues/406) in the Unity engine.
If you try to deploy with the standard version of SharpDX, it __WILL NOT WORK!__

Compilation
--------------------------------------

* Open `AltInput.csproj` with a text editor and update the following section
according to your path: 
```
  <PropertyGroup>
    <KSPPath>E:\Program Files (x86)\Steam\SteamApps\common\Kerbal Space Program</KSPPath>
  </PropertyGroup>
```
* Open the project and compile. If you edited the `KSPPath` variable above, all
  the required module files will be copied automatically.

Usage
--------------------------------------

* If needed, copy `AltInput.dll`, `SharpDX.dll` and `SharpDX.DirectInput.dll` into the KSP 
 `Plugins` directory (typically `C:\Program Files (x86)\Steam\SteamApps\common\Kerbal Space Program\Plugins`)
* Edit `PluginData\AltInput\config.ini` according to your settings and copy the 
  whole `PluginData`directory to the same directory where you copied the DLL files.
* Play. ;) If your joystick is properly detected, you will get a notification (in the 
  upper left corner) and you should be able to use it to control the ship.

Configuration
--------------------------------------

All configuration is done in `PluginData\AltInput\config.ini`

__As of version 0.2__, the following DirectInput controls, if present, can be assigned
to Vessel Control:
* `X`
* `Y`
* `Z`
* `RotationX`
* `RotationY`
* `RotationZ`
* `Sliders[0-1]` (the Throttle is usually mapped to one of these)

The following are not handled yet:
* `PointOfViewControllers[0-3]`
* `Buttons[0-127]`
* `VelocityX`
* `VelocityY`
* `VelocityZ`
* `AngularVelocityX`
* `AngularVelocityY`
* `AngularVelocityZ`
* `VelocitySliders[0-1]`
* `AccelerationX`
* `AccelerationY`
* `AccelerationZ`
* `AngularAccelerationX`
* `AngularAccelerationY`
* `AngularAccelerationZ`
* `AccelerationSliders[0-1]`

The following KSP controls can be mapped to an input:
* `yaw`
* `picth`
* `roll`
* `mainThrottle`

And the following are not handled yet:
* `fastThrottle`
* `gearDown`
* `gearUp`
* `headlight`
* `killRot`
* `yawTrim`
* `pitchTrim`
* `rollTrim`
* `wheelSteer`
* `wheelSteerTrim`
* `wheelThrottle`
* `wheelThrottleTrim`
* `X`
* `Y`
* `Z`
* `KSPActionGroup.Stage`
* `KSPActionGroup.Gear`
* `KSPActionGroup.Light`
* `KSPActionGroup.RCS`
* `KSPActionGroup.SAS`
* `KSPActionGroup.Brakes`
* `KSPActionGroup.Abort`
* `KSPActionGroup.Custom[01-10]`
