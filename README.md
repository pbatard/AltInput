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

Make sure you update the References to `Assembly-CSharp.dll` and `UnityEngine.dll`
according to your KSP installation. Once you do that, you should be able to compile
the project.

Usage
--------------------------------------

Copy `AltInput.dll`, `SharpDX.dll` and `SharpDX.DirectInput.dll` into your KSP
`Plugins` directory and play. If your joystick is properly detected, you will
get a notification about it during flight mode and you should be able to use it.

Configuration [NOT IMPLEMENTED YET]
--------------------------------------

The following DirectInput controls, if present, can be assigned to Vessel Control

* X;
* Y;
* Z;
* RotationX;
* RotationY;
* RotationZ;
* Sliders[0-1]; (the Throttle is usually mapped to one of these)
* PointOfViewControllers[0-3];
* Buttons[0-127];
* VelocityX;
* VelocityY;
* VelocityZ;
* AngularVelocityX;
* AngularVelocityY;
* AngularVelocityZ;
* VelocitySliders[0-1];
* AccelerationX;
* AccelerationY;
* AccelerationZ;
* AngularAccelerationX;
* AngularAccelerationY;
* AngularAccelerationZ;
* AccelerationSliders[0-1];
