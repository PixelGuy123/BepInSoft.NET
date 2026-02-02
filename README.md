# BepInSerializer.Mono
### WIP WARNING
The README is still a WORK-IN-PROGRESS, which means it is still lacking information or it may be outdated. Likewise, no release is available yet, but the project itself is essentially finished.

During the development of a plugin to be used with a code-injector for Unity, there are many times the developer will need to create some sort of data structure to wrap up many basic types for its customized components.

In [BepInEx](https://github.com/BepInEx), it's known that attempting to create a data structure (classes, structs) marked as _serializable_ that is _actually serialized_ is virtually impossible through Unity alone.

**BepInSerializer**, _restricted to the Mono builds (LINK HERE)_, is an **universal plugin** — a mod **expected** to work with most Unity games —, which aims to fix the afromentioned issue by acting as a intermediate bridge in the serialization process that handles all the `UnityEngine.Object` instantiation calls, and properly serialize/deserialize their fields using a custom conversion system.

This plugin does **not** add any other meaningful elements to the gameplay, meaning there isn't much else this does aside from serialization. Unless you manage to find bugs; _then,_ you might want to report that in issues (LINK HERE/REMINDER TO MAKE ISSUES INSTRUCTIONS).

## Installation

> BepInSerializer is a **plugin** made for **BepInEx 5**.
Here's the step-by-step to install this plugin into your game:

1. Install [BepInEx](https://docs.bepinex.dev/articles/user_guide/installation/index.html) into the game you're wishing to play with mods.
2. Once BepInEx is installed, download this plugin through the [Releases](https://github.com/PixelGuy123/BepInSerializer.Mono/releases/latest) page.
3. With the binary downloaded, your last task is simply put that inside the `BepInEx/plugins` folder.
