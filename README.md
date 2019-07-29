# Virt-A-Mate Director

Controls where the "camera" is by moving it to sequential Animation Pattern steps. Compatible with [Passenger](https://github.com/acidbubbles/vam-passenger) for moving in and out of POV mode.

## How to use

1. Add the `Director.cs` file to an `Animation Pattern`. I suggest renaming the atom to `Director`.
2. Set the mode to `WindowCamera`, this will allow you to place the camera exactly where you want it.
3. Set the mode to `NavigationRig` to make the VR camera follow instead.

## Tips

You can trigger this with a `UIButton`, which can also reset the Animation Pattern time to zero, and then set the mode to WindowCamera. When the animation is complete, you can trigger it off. This will make a "Play" button that behaves more like a video control.

You can also add the `DirectorStep.cs` file to an `Animation Step`, so when the animation reaches it, it will trigger the `Passenger.cs` plugin on another atom. You can use this to trigger a person's point of view during the animation.

To make the animation play once, make the last step OnActive event disable Director (set `Mode` to `None`).

## License

[MIT](LICENSE.md)

## TODO

- Automatically add the DirectorStep plugin on every step (make a .cslist, and inject a copy of the other script)
- Transitions
  - Fade, Black, None
  - Duration
  - Extend (stays black for a little while after fade)
  - Begin / End
  - Per step override
- Exit automatically when
  - Option A) The menu is open, stop immediately
  - Option B) Make a follow button, aligned with the actual UI
- Built-in "animation complete" handling (returns to None)
- In Director, make a Select Step button, and in DirectorStep, make a Select Director button
  - Can we automatically switch to the right plugin UI?
  - Button to Teleport To Step in both Director and DirectorStep
- Step options
  - Set camera angle, Follow, Nothing
  - Continue with another Director
- Show duration in time, and instead of speed change time (will affect speed instead)
  - Add step without changing existing step time
