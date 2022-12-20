This prefab lets players simulate ice skating by modeling momentum and the directional control of ice skates. Movement by leg trackers is included.

SPECIFICATIONS:
- See the GETTING STARTED doc for basic instructions
- The surface name defaults to require matching the string "ice" but this can be redefined on the script on the parent object of the prefab
- You can also define the physics layer of the ice by changing "Ray Mask" on the same object

RECOMMENDATIONS:
- Player voice distance should be at least 50 meters for outdoor areas.
    A simple drag & drop prefab that does this is included in "extras."
	However, indoor and close-quarter areas of a World should never have player voice above 10m! If you have separate zones you should manage them with Guribo's Better Player Audio, github.com/Guribo
- A roller skating world is possible in a limited way with this prefab. The surface must still be flat and horizontal, which means no ramps, rails, or sidewalks.

EXTENDABILITY:
- If you want to add motion effects like wind sound or particles, you can read Speed and Direction data by calling
	jokerispunk.IceSkatingUdon.Interface._GetSpeed()
	jokerispunk.IceSkatingUdon.Interface._GetDirection() on the Interface script on the parent object


This prefab is managed on github (github.com/jokerispunk) and I accept pull requests (you are encouraged to comment or talk to me first).