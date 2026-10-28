# Virtual AR Sandbox

### 🌐 Website: **[shanem73.github.io/virtual-sandbox](https://shanem73.github.io/virtual-sandbox/)**

| | Open the app |
|---|---|
| 🖥️ **Desktop** (Windows, any browser) | **https://shanem73.github.io/virtual-sandbox/desktop/** |
| 🥽 **VR** (Meta Quest Browser) | **https://shanem73.github.io/virtual-sandbox/vr/** |

**Designer:** Xin Miao, Missouri State University

The [Augmented Reality Sandbox](https://www.cs.ucdavis.edu/~okreylos/ResDev/SARndbox/MainPage.html), moved from a physical box of sand into a virtual 3D world. You shape virtual sand, and the elevation colors, contour lines and a real-time water simulation update as you go.

## The idea

A physical AR Sandbox is a great way to teach topographic maps, contour lines and watersheds. Building one takes a sandbox, a Kinect depth camera, a projector, a **Linux** computer and a careful calibration, so most students only get to use one if their campus or a nearby museum has it.

This project keeps the part that makes the sandbox work, the original SARndbox water simulation, and drops the hardware. The sand is virtual, and the projector becomes the screen or a VR headset. Everything runs in a web browser on **Windows**, the system most students already have, so access is as easy as opening a link.

| | Physical AR Sandbox | Virtual AR Sandbox |
|---|---|---|
| Computer | Linux (Vrui toolkit) | Windows, or any OS with a modern browser |
| Sensor | Kinect depth camera | Mouse, touch, or VR controllers |
| Display | Projector over the sand | Screen or Meta Quest headset |
| Setup | Build, install, calibrate | Open a link |

The water is the original **SARndbox 2.8** solver (a Kurganov–Petrova central-upwind shallow-water scheme), ported from GLSL to WebGL2. The constants are the same as the original (θ = 1.3, g = 9.81, attenuation 127/128), and so are the units (centimetres and seconds).

| Version | Folder | Runs on |
|---|---|---|
| Desktop | [`desktop/`](desktop/) | Any current Chrome, Edge, Firefox or Safari with WebGL2 |
| VR | [`vr/`](vr/) | Meta Quest Browser (Quest 2, 3, Pro), or PC VR in Chrome or Edge |

Each version is a single self-contained HTML file. There's no build step and nothing to install.

## Run it

### Publish on GitHub Pages (needed for VR)

WebXR only works over HTTPS, and GitHub Pages provides it.

1. In the repository, open **Settings → Pages**. Under **Source**, choose **Deploy from a branch**, then select `main` and `/ (root)`.
2. After a minute, the site is live at `https://shanem73.github.io/virtual-sandbox/`.

### Use it on a Meta Quest 2

1. Put on the headset and open the **Meta Quest Browser**.
2. Go to `https://shanem73.github.io/virtual-sandbox/vr/`. Bookmark it, or send the link to the headset from your phone with the Meta Horizon app.
3. Press **Enter VR**. The sandbox appears on a table about a metre in front of you. If it's in the wrong place, press the right thumbstick to bring it back.
4. **Passthrough mode** places the sandbox in your real room. Quest 2 passthrough is black and white.

### Run it locally

You can open `desktop/index.html` straight from disk. For VR on a headset you need HTTPS, so use GitHub Pages or another HTTPS server. Opening `vr/index.html` on a computer shows a mouse-driven preview of the VR scene.

## Controls

### Desktop

| Input | Action |
|---|---|
| Left drag | Use the selected tool (add sand, dig, smooth, flatten, rain, drain, spring, sinkhole, erase) |
| Right drag / Shift + drag / scroll | Orbit / pan / zoom |
| `1`–`0` | Pick a tool |
| Hold `R` / `D` | Rain or drain over the whole box |
| `Space` · `P` · `C` · `W` | Pause water · projector view · contours · 3D water |
| `Ctrl` + `Z` | Undo the last sand stroke |

### VR (Quest Touch controllers)

| Right controller | | Left controller | |
|---|---|---|---|
| Trigger | Use the tool where you point | Trigger | Rain over the whole box |
| Stick ← → | Change tool | Grip | Drain the whole box |
| Stick ↑ ↓ | Brush size | Stick ↑ ↓ | Vertical exaggeration |
| Grip | Grab and move the sandbox | Stick ← → | Color map |
| A | Pause or resume the water | X | Tabletop or walk-in giant scale |
| B | Next landscape | Y | Contour lines on or off |
| Stick click | Put the sandbox back in front of you | Stick click | Undo the last sand stroke |

A panel above your left hand shows the current tool, brush size and water volume.

## Features

- **Sand:** add, dig, smooth and flatten tools. Sand slumps when slopes pass the angle of repose, and the total volume of sand is conserved.
- **Water:** the original SARndbox solver, with global and local rain or drain, evaporation, and box edges that either absorb water (as in the original) or act as walls.
- **Additions beyond the original:**
  - springs and sinkholes
  - Green–Ampt infiltration (desktop)
  - a flow-speed view that shows erosion potential
- **Landscapes:** river valley with lake, rolling hills, two watersheds, volcanic island, dam and reservoir, Ozark karst with sinkholes, and flat sand.
- **Display:** the SARndbox color map, hypsometric tints, grayscale and bare sand, plus contour lines, hillshading and a top-down projector view.
- **Desktop only:**
  - terrain-model (DEM) matching with a fit score, loading `.asc` grids or heightmap images
  - copying the sand surface or water depth as an ESRI ASCII grid in centimetres
- **VR only:**
  - real-scale tabletop mode, and a walk-in giant mode at 5× scale
  - passthrough mode
  - haptic feedback

## Technical notes

- **Simulation detail:** both apps let you choose the grid. Smaller cells look smoother but cost more: halving the cell size is about 8× the work.

  | App | Level | Grid | Cell size |
  |---|---|---|---|
  | Desktop | Standard (default) | 400 × 300 | 0.40 cm |
  | Desktop | High (the original SARndbox's default) | 640 × 480 | 0.25 cm |
  | Desktop | Ultra (fast graphics card) | 800 × 600 | 0.20 cm |
  | VR | Quest 2 (default) | 256 × 192 | 0.63 cm |
  | VR | Quest 2 high | 320 × 240 | 0.50 cm |
  | VR | PC VR | 400 × 300 | 0.40 cm |
  | VR | PC VR high | 640 × 480 | 0.25 cm |

  The desktop app shows the frame rate in its corner, and the VR wrist panel shows it in the headset. Quest 2 should stay near 72 fps; if it drops below about 68, pick a lower level.
- **Non-blocking readback:** both apps read data back from the GPU asynchronously, using pixel buffers and fences, so the GPU never waits on `readPixels`. That data is the CFL step size, the sand heights used for pointing, and the water totals. Like SARndbox, the simulation takes at most 30 steps per frame.
- **Floating-point textures:** both versions need `EXT_color_buffer_float`, which the Quest Browser and all current desktop browsers support.
- **Shared code:** the solver, render shaders and landscape generators are the same code in both files.

## Roadmap

- A small bridge app that streams depth frames from a real camera (Kinect v1 or v2, Azure Kinect / Orbbec Femto Bolt, RealSense) so the physical sandbox can drive these pages.
- Hand tracking in VR.

## History of the physical AR Sandbox

- **Origin:** a video of “Sandy Station,” a prototype by Czech researchers that colored sand by elevation and added simple water flow, inspired the UC Davis team.
- **2012:** Oliver Kreylos at the UC Davis W.M. Keck Center for Active Visualization in the Earth Sciences (KeckCAVES) develops the AR Sandbox. It's part of **LakeViz3D**, an NSF-funded informal science education project on freshwater lakes and watersheds (grants 1114663 and 1135588).
- **2012:** the first sandboxes open at UC Davis, the Lawrence Hall of Science, the UC Davis Tahoe Environmental Research Center, and the ECHO Lake Aquarium and Science Center.
- **2012–2021:** the SARndbox software is released as open source (GPL). It runs on Linux with the Vrui VR toolkit and a Kinect camera. Version 2.4 adds Kinect v2 and Intel RealSense support, and 2.7 adds remote streaming.
- **March 9, 2021:** SARndbox 2.8 is released. This project's water simulation is ported from it.
- **2022:** a community support forum opens for sandbox builders.
- **Since then:** more than 150 universities, research centers, government agencies and schools have adopted the AR Sandbox, according to *Eos*.
- **2026:** Xin Miao moves the sandbox into a virtual 3D environment that runs in a browser on Windows and in VR on Meta Quest.

### Links

- [Augmented Reality Sandbox project page](https://www.cs.ucdavis.edu/~okreylos/ResDev/SARndbox/MainPage.html) (Oliver Kreylos, UC Davis)
- [Build instructions for a physical sandbox](https://www.cs.ucdavis.edu/~okreylos/ResDev/SARndbox/Instructions.html)
- [SARndbox source code download](https://www.cs.ucdavis.edu/~okreylos/ResDev/SARndbox/Download.html)
- [“Augmented Reality Turns a Sandbox into a Geoscience Lesson”](https://eos.org/science-updates/augmented-reality-turns-a-sandbox-into-a-geoscience-lesson), *Eos*
- [AR Sandbox community forum](http://doc-ok.org/?forum=ar-sandbox-forum)

## Credits and license

**Designer:** Xin Miao, Missouri State University.



Based on the Augmented Reality Sandbox by Oliver Kreylos, UC Davis W.M. Keck Center for Active Visualization in the Earth Sciences (KeckCAVES). The original was supported by NSF grants 1114663 and 1135588.

SARndbox is free software under the GNU General Public License, version 2 or later. Because this project ports its water shaders, it carries the same license. See [`LICENSE`](LICENSE).
