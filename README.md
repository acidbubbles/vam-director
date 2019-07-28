# Virt-A-Mate Director

Controls where the "camera" is by moving it to sequential Animation Pattern steps. Compatible with [Passenger](https://github.com/acidbubbles/vam-passenger) for moving in and out of POV mode.

## How to use

1. Add the `Director.cs` file to an `Animation Pattern`. I suggest renaming the atom to `Director`.
2. Set the mode to `WindowCamera`, this will allow you to place the camera exactly where you want it.
3. Set the mode to `NavigationRig` to make the VR camera follow instead.

You can trigger this with a `UIButton`, which can also reset the Animation Pattern time to zero, and then set the mode to WindowCamera. When the animation is complete, you can trigger it off. This will make a "Play" button that behaves more like a video control.

You can also add the `DirectorStep.cs` file to an `Animation Step`, so when the animation reaches it, it will trigger the `Passenger.cs` plugin on another atom. You can use this to trigger a person's point of view during the animation.

## License

[MIT](LICENSE.md)
