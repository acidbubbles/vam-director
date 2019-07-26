# Virt-A-Mate Director

Controls where the "camera" is. Compatible with [Passenger](https://github.com/acidbubbles/vam-passenger) for moving in and out of POV mode.

## How to use

1. Add the `Director.cs` file to an `Animation Pattern`.
2. Check the `Active` checkbox in the plugin options to move the camera to the animation step each time a new one is activated.
3. You can also use the `WindowCamera` option to link the camera, allowing you to place the camera yourself.
4. Optionally activate using a button, while resetting the animation, and stopping at the end of the animation for a one-time serie.

## Next steps

1. Mark steps as Passenger steps, which would activate and de-activate a target atom's Passenger plugin (maybe another plugin just for that?)
2. Make it so that Passenger automatically enable ImprovedPoV if it's present

## License

[MIT](LICENSE.md)
