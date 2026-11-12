# Virtual AR Sandbox

### 🌐 Website: **[shanem73.github.io/virtual-sandbox](https://shanem73.github.io/virtual-sandbox/)**

| | Open the app |
|---|---|
| 🖥️ **Desktop** (Windows, any browser) | **https://shanem73.github.io/virtual-sandbox/desktop/** |
| 🥽 **VR** (Meta Quest Browser) | **https://shanem73.github.io/virtual-sandbox/vr/** |
| 🏖️ **Physical sandbox** (Windows 11 + Kinect v1 + projector) | Runs locally: see [setup and calibration](#physical-sandbox-on-windows-11) |

**Designer:** Prof. Xin (Shane) Miao, Missouri State University

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
| Physical | [`physical/`](physical/) + [`bridge/`](bridge/) | Windows 11 with a Kinect v1 and a projector, in Edge or Chrome |

The desktop and VR versions are single self-contained HTML files, with no build step and nothing to install. The physical version adds a small Windows program for the Kinect.

## Physical sandbox on Windows 11

Run a **real** AR Sandbox from an ordinary Windows 11 PC, with no Linux needed. A Kinect v1 reads the sand, and a projector paints the colored map, contour lines and flowing water back onto it. This replaces the Linux SARndbox installation.

> **Why Windows 11.** *(from the designer, Prof. Xin (Shane) Miao)*
>
> The original AR Sandbox software, SARndbox, is excellent, but it was written for Linux. Running it means installing a Linux computer, compiling the Vrui VR toolkit and the Kinect 3D Video package from source, setting up graphics drivers, and calibrating the box and the projector with command-line tools. That's routine for a computer scientist, but it puts the sandbox out of reach for most geography and geoscience departments, K-12 schools and museums, whose computers run Windows and are managed by campus IT. I moved the software to Windows 11 so that anyone who can install a program can set up a sandbox: install Microsoft's Kinect driver, unzip a folder, double-click to start, and follow a guided, on-screen calibration. The parts that make the sandbox a good teaching tool stay the same, including the SARndbox water simulation, the depth filtering, and rain under students' hands. What changes is that it runs in a familiar web browser on the computers schools already own. The same code also powers the fully virtual desktop and VR versions, so students without access to a physical sandbox can still learn from it.

```
Kinect v1 ──USB──► SandboxBridge.exe ──localhost──► Edge / Chrome
                   reads depth                      control page   (your monitor)
                   serves the pages                 projector page (full screen on the projector)
```

| Part | What it does |
|---|---|
| [`bridge/SandboxBridge.exe`](bridge/) | Reads the Kinect through Microsoft's Kinect SDK 1.8 and serves the sandbox pages at `http://localhost:8765/`. Works offline. |
| [`physical/`](physical/) control page | Step-by-step setup (camera, box, projector calibration), then live settings and statistics. |
| [`physical/projector.html`](physical/projector.html) | Filters the depth, builds the terrain, runs the SARndbox water simulation, and draws the calibrated image. |

### What you need

- **Kinect for Xbox 360 or Kinect for Windows v1,** with its **power/USB adapter** (the Xbox 360 S model needs it).
- **A projector,** connected as a second display. In Windows, open **Settings → System → Display** and choose **Extend these displays**.
- **A Windows 11 PC** with Edge or Chrome.
- **A sandbox** about 100 × 75 cm, filled 10–15 cm deep with light-colored sand.
- **The Kinect and projector mounted about 1 m above the sand,** both looking down at the box.
- **A calibration target:** a CD, or a 12 cm circle of stiff paper, taped flat onto the end of a thin stick (a ruler or chopstick).

### Install (once)

1. **Install the [Kinect for Windows SDK v1.8](https://www.microsoft.com/download/details.aspx?id=40278)** from Microsoft. The smaller [Kinect for Windows Runtime v1.8](https://www.microsoft.com/download/details.aspx?id=40277) is also enough.
2. **Turn off Memory integrity (required on Windows 11).** The Kinect v1 driver dates from 2013, and Windows 11's Core isolation blocks it. Windows shows a warning and the Kinect isn't found.
   1. Open **Windows Security → Device security → Core isolation details**.
   2. Switch **Memory integrity** to **Off**.
   3. **Restart the PC.** The change only takes effect after a full restart.

   > ⚠️ Memory integrity is a security protection. We recommend a dedicated lab or classroom PC for the sandbox. If you turn it back on, Windows blocks the Kinect driver again.
3. **Plug in the Kinect:** power adapter first, then USB, directly into the PC rather than a hub. In **Device Manager** it should appear under **Kinect for Windows** with no yellow warning icon.
4. **Download this repository** (green **Code** button → **Download ZIP**) and unzip it somewhere you can write to, such as `Documents\virtual-sandbox`. Avoid `Program Files`: the bridge saves its settings next to itself.

### Start the sandbox

Double-click **`bridge\Start Sandbox.bat`**. A black console window opens (keep it open), and your browser opens the control page at `http://localhost:8765/physical/`.

- **If SmartScreen warns about the program,** click **More info → Run anyway**. Or rebuild it yourself first with `bridge\build.bat`, which uses the C# compiler built into Windows.
- **To try everything without hardware,** use **`bridge\Start Simulated.bat`**. It simulates both the Kinect and the projector.

### First-time setup

The control page has four tabs: **1 Camera → 2 Box → 3 Projector → 4 Run**. Steps 1–3 are done once; the results are saved in `bridge\sandbox-config.json`.

**Step 1 — Camera.** The camera view should show the sandbox. Near things are bright and far things are dark.

**Step 2 — Box**
1. **Flatten the sand** to the level you want as sea level.
2. Click **Capture base plane** and keep hands out of the box for about a second. You should see "Flatness" under about 1 cm.
3. **Click the four inside corners** of the box on the camera view, in this order: **front-left, front-right, back-left, back-right**. "Front" is the side where people stand.
4. Check the measured size, for example "100 × 75 cm", then click **Save box setup**.

**Step 3 — Projector calibration**

Calibration teaches the computer how a point the Kinect sees lines up with a pixel the projector draws, so the colors land exactly on the sand.

**Make the target first.** Cover a CD with **plain white paper** (or cut a 12 cm disk of stiff white card) and tape it **flat** to the end of a thin stick about 40 cm long. The Kinect measures distance with infrared light, and a bare, shiny CD reflects it away, so the disk shows up as a hole instead of a solid circle. Hold the stick by its far end so your hand and arm stay away from the disk.

1. **Open the projector window:** click **Open projector window** (top right of the control page). If it opens on your monitor, drag it onto the projector's screen. Then click **Go full screen** in that window.
2. **Set the disk size:** on the **Projector** tab, set **Disk radius** to your target's radius (a CD is 6 cm).
3. **Start:** click **Start calibration**. For about a second, keep the box empty while it measures the sand.
4. **Follow the crosshair:**
   - A white crosshair with a green circle appears on the sand.
   - Hold the disk **flat**, so the **crosshair lands on the center of the disk**. Look at the disk: the crosshair lines are drawn across it.
   - Hold it **still**. In the camera view, the circle around the disk turns from yellow to **green**.
   - After about a second it **captures automatically** and the crosshair moves to the next spot.
5. **Do two rounds:**
   - **Spots 1–12: low.** Hold the disk about **5 cm** above the sand (at least 3 cm, or it can't be told apart from the sand).
   - **Spots 13–24: high.** The page shows the right height for your setup, usually **15–22 cm** above the sand. It keeps the disk more than 80 cm from the Kinect, which can't measure anything closer. Re-center it as you lift. The two heights let the projection follow hills and valleys.
6. **Check the result:** after the last spot it calculates and shows an **average error**. Under about 4 px is good. The projector window restarts, and the map now sits right on the sand.

| During calibration | What to do |
|---|---|
| **Start calibration** is grayed out | Open the projector window, and finish step 2 first. |
| No crosshair on the sand | The projector window isn't on the projector, or isn't full screen. |
| The page explains what it sees under the progress bar | Follow that message. The common ones are below. |
| "No disk seen" | Hold the disk at least 3 cm above the sand, right under the crosshair. |
| "Only N% of a 6 cm disk is visible" | Hold it flat. Cover a shiny CD with white paper. Check that **Disk radius** matches your disk. |
| "It doesn't look like a 6 cm disk" | Your hand or arm is touching the disk in the camera's view. Hold the stick by its far end. |
| "Too close for the Kinect to measure" | Hold the disk lower. Anything closer than 80 cm to the camera has no depth. |
| The circle stays yellow ("hold still") | Steady your arm, or click **Capture now** when the crosshair is centered. |
| A spot can't be reached | Click **Skip this spot**. At least 6 spots are needed; 12 or more is better. |
| Error above about 8 px, or colors are off | Calibrate again with the disk flatter and steadier. Make sure the Kinect and projector didn't move. |
| Want to start over | At the bottom of the **Projector** tab, click **Erase projector calibration**, or **Erase all setup (box and projector)**. Click twice to confirm. |

### Everyday use

1. Double-click **`Start Sandbox.bat`**.
2. Click **Open projector window**, then **Go full screen**. No recalibration is needed.
3. **Shape the sand.** The map and contour lines follow it within about a second.
4. **Make rain:** hold a hand anywhere over the sand and it rains straight below it, even when the hand is too close for the Kinect to measure. Hands are kept out of the terrain, so a still hand doesn't become a hill.
5. **Run tab:**
   - buttons to rain or drain over the whole box, remove all water, or pause the water
   - color map, contour interval, sea level, water speed and evaporation
   - live stats: frame rates, water volume, and hands detected

**Redo calibration only when something moves:**

| What changed | Redo |
|---|---|
| The Kinect was bumped or remounted | Steps 2 and 3 |
| The projector moved, or its zoom, focus or resolution changed | Step 3 |
| The box moved, or the sand level changed a lot | Step 2 |

To start fresh, use the **Erase** buttons at the bottom of the **Projector** tab: one clears just the projector calibration, the other also clears the base plane and box corners.

When you download a new version of this repository, copy your **`bridge\sandbox-config.json`** into the new `bridge` folder to keep your calibration.

### Quick demo without projector calibration

Do step 2 only, then open the projector window. It shows a live top-down map of the real sand (colors, contours, water, rain) that isn't fitted to the sand. Put it on a monitor next to the sandbox, or aim and zoom the projector by hand so the image covers the box.

### Troubleshooting

| Problem | What to try |
|---|---|
| Windows warns about **Memory integrity**, or the Kinect isn't found after the SDK install | Turn off Memory integrity, then **restart** (see Install, step 2). |
| "The Kinect for Windows SDK 1.8 is not installed" | Install the SDK or runtime, then restart the bridge. |
| "No Kinect is connected" | Check the power adapter. Try another USB port (no hub). Check Device Manager. |
| The sand shimmers, or parts never appear | **Run → Camera filter and detail**: raise *Jitter filter* or *Stability limit*. |
| Port 8765 is already in use | Run `SandboxBridge.exe --port 8766` and open `http://localhost:8766/physical/`. |

More detail, command-line options, and how this maps onto the Linux SARndbox tools: **[bridge/README.md](bridge/README.md)**.

## Run the desktop and VR versions

### Publish on GitHub Pages (needed for VR)

WebXR only works over HTTPS, and GitHub Pages provides it.

1. In the repository, open **Settings → Pages**. Under **Source**, choose **Deploy from a branch**, then select `main` and `/ (root)`.
2. After a minute, the site is live at `https://shanem73.github.io/virtual-sandbox/`.

### Use it on a Meta Quest 2

1. Put on the headset and open the **Meta Quest Browser**.
2. Go to `https://shanem73.github.io/virtual-sandbox/vr/`. Bookmark it, or send the link to the headset from your phone with the Meta Horizon app.
3. Press **Enter VR**. The sandbox appears on a table about a metre in front of you. If it's in the wrong place, press the right thumbstick to bring it back.
4. **Passthrough mode** places the sandbox in your real room. Quest 2 passthrough is black and white.
5. **Surroundings in VR** (on the page, before entering VR, or the `V` key) picks what is around you in Enter VR:
   - **Field station:** an open wooden pergola in a grassy valley, with trees, distant hills and a sunny sky.
   - **360° photo:** a real grassy field at sunrise.

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

### VR keyboard

Works in the desktop preview of the VR page, in PC VR, and with a Bluetooth keyboard paired to the Quest.

| Key | Action | Key | Action |
|---|---|---|---|
| `W` `A` `S` `D` / arrows | Walk | `1`–`9` | Pick a tool |
| `Q` / `E` | Turn (30° steps in the headset) | Hold `R` / `F` | Rain / drain over the whole box |
| `PgUp` / `PgDn` | Fly up / down | `Space` | Pause or resume the water |
| `Shift` | Move faster | `N` · `G` · `C` · `M` | Next landscape · giant scale · contours · color map |
| `I` `J` `K` `L` | Move the sandbox | `−` / `=` | Vertical exaggeration |
| `U` / `O` | Lower / raise the sandbox | `[` / `]` | Brush size |
| `,` / `.` | Rotate the sandbox | `Z` | Undo the last sand stroke |
| `H` | Reset the view and the sandbox | `V` | Switch surroundings (field station or 360° photo) |

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
- **2026:** Prof. Xin (Shane) Miao moves the sandbox into a virtual 3D environment that runs in a browser on Windows and in VR on Meta Quest.

### Links

- [Augmented Reality Sandbox project page](https://www.cs.ucdavis.edu/~okreylos/ResDev/SARndbox/MainPage.html) (Oliver Kreylos, UC Davis)
- [Build instructions for a physical sandbox](https://www.cs.ucdavis.edu/~okreylos/ResDev/SARndbox/Instructions.html)
- [SARndbox source code download](https://www.cs.ucdavis.edu/~okreylos/ResDev/SARndbox/Download.html)
- [“Augmented Reality Turns a Sandbox into a Geoscience Lesson”](https://eos.org/science-updates/augmented-reality-turns-a-sandbox-into-a-geoscience-lesson), *Eos*
- [AR Sandbox community forum](http://doc-ok.org/?forum=ar-sandbox-forum)

## Credits and license

**Designer:** Prof. Xin (Shane) Miao, Missouri State University.



Based on the Augmented Reality Sandbox by Oliver Kreylos, UC Davis W.M. Keck Center for Active Visualization in the Earth Sciences (KeckCAVES). The original was supported by NSF grants 1114663 and 1135588.

The 360° photo is "Spruit Sunrise" from [Poly Haven](https://polyhaven.com/a/spruit_sunrise), released as CC0 (public domain).

SARndbox is free software under the GNU General Public License, version 2 or later. Because this project ports its water shaders, it carries the same license. See [`LICENSE`](LICENSE).
