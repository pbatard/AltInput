AltInput — Alternate Input plugin for Kerbal Space Program
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

The following DirectInput controls, if present, can be assigned to Vessel Control.
These are the names you should use for `Control` when adding a 
`Control = Mapping` line in your config file:
* `AxisX`
* `AxisY`
* `AxisZ`
* `RotationX`
* `RotationY`
* `RotationZ`
* `Slider[1-2]`
* `Button[1-128]`
* `POV[1-4]`

Anything that isn't in the list above is not handled.

The following KSP controls can be mapped to an input. These are the names you should
use for `Mapping` when adding a `Control = Mapping` line in your config file:
* `yaw`
* `pitch`
* `roll`
* `mainThrottle`
* `wheelSteer`
* `wheelThrottle`
* `X` - Translation X
* `Y` - Translation Y
* `Z` - Translation Z
* `toggleAbort`
* `activateLanded` - switch from 'Flight' to 'Landed' input modes
* `activateFlight` - switch from 'Landed' to 'Flight' input modes
   __NB:__ If you map both of the above to the same button, you can effectively 
   toggle between 'Flight' and 'Landed' modes.
* `activateBrakes`
* `toggleBrakes`
* `activateCustom[01-10]`
* `toggleCustom[01-10]`
* `toggleGears`
* `toggleLights`
* `overrideRCS`
* `toggleRCS`
* `overrideSAS`
* `toggleSAS`
* `activateStaging`
* `switchView` - Switch camera view (auto/free/orbital/chase)
* `toggleMapView` - Switch between Map view and Staging/Docking view

Anything that isn't in the list above is not handled.


How do I find the names I should use for my controls?
--------------------------------------

The control names should follow what Windows report when checking your game
controller properties. For instance, using Windows 8, if you go to _Device and
Printers_ you should be able to access the _Game controller settings_ menu by
right clicking on your device. Then, if you select _Properties_, you will be
presented with a dialog where all the buttons and axes your controller provides
are labelled. Matching these names with the ones from the first list above is
then a trivial matter.

__Hint:__ You can also easily identify which button is which in this dialog by
pressing them and writing down the number that gets highlighted then.