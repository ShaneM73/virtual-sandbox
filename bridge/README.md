# Physical AR Sandbox on Windows 11

This folder turns a real sandbox (a Kinect v1 and a projector) into an AR Sandbox on an ordinary **Windows 11** PC, with no Linux needed. It does the same job as the original Linux SARndbox.

**Designer:** Prof. Xin (Shane) Miao, Missouri State University.
**Based on:** SARndbox 2.8 by Oliver Kreylos, UC Davis. **License:** GPL-2.0-or-later.

## How it works

```
Kinect v1 ──USB──► SandboxBridge.exe ──localhost──► Edge / Chrome
                   reads depth (Kinect SDK 1.8)      control page  (main monitor)
                   serves the web pages              projector page (full screen on the projector)
```

- **`SandboxBridge.exe`** is a small console program. It reads 640 × 480 depth frames from the Kinect and streams them to the browser. It also serves the sandbox pages at `http://localhost:8765/`. It only listens on this computer, and it works without internet.
- **Control page** (`/physical/`): setup steps, live settings and statistics.
- **Projector page** (`/physical/projector.html`): it filters the depth, turns it into terrain, runs the SARndbox water simulation, and draws the calibrated map with contour lines and water onto the sand.

## What you need

| Item | Notes |
|---|---|
| Kinect for Xbox 360 (or Kinect for Windows v1) | Needs its **power/USB adapter**: the Xbox 360 S model plugs into a proprietary port. |
| Projector | Short-throw works best. Connect it as a second display and set Windows to **Extend** these displays. |
| Windows 11 PC | Any recent graphics card. Use Edge or Chrome. |
| Sandbox | About 100 × 75 cm, filled 10–15 cm deep with light-colored sand. |
| Calibration disk | A CD, or a 12 cm paper disk, taped to the end of a thin stick. |

Mount the Kinect and projector about **1 m above the sand**, both looking down at the box.

## Install (once)

1. **Install the Kinect for Windows SDK v1.8** from Microsoft: <https://www.microsoft.com/download/details.aspx?id=40278>.
   - Microsoft's page lists Windows 7 to 8.1. The SDK is widely used on Windows 10 and 11, but it isn't officially listed for them.
   - The smaller [Kinect for Windows Runtime v1.8](https://www.microsoft.com/download/details.aspx?id=40277) is also enough.
2. **Turn off Memory integrity (Windows 11).** The Kinect v1 driver is from 2013, and Windows 11's Core isolation blocks it: Windows shows a warning and the bridge reports that no Kinect is connected.
   1. Open **Windows Security → Device security → Core isolation details**.
   2. Switch **Memory integrity** to **Off**.
   3. **Restart the PC.** The setting only takes effect after a full restart.

   Memory integrity is a security protection, so we recommend a dedicated lab PC. Turning it back on blocks the Kinect driver again.
3. Plug in the Kinect (power adapter first, then USB). In **Device Manager** it should appear under *Kinect for Windows*, with no yellow warning icon.
4. Copy this whole repository folder somewhere you can write to, such as `Documents\virtual-sandbox`. Avoid `Program Files`, because the bridge saves its settings next to itself.

## Run

1. Double-click **`Start Sandbox.bat`**. The bridge opens and your browser goes to the control page. Keep the black console window open while you use the sandbox.
   - If Windows SmartScreen warns about the program, click **More info → Run anyway**.
   - If you'd rather not run a downloaded program, double-click **`build.bat`** first. It rebuilds `SandboxBridge.exe` from `SandboxBridge.cs`, using the C# compiler that ships with Windows.
2. On the control page, click **Open projector window**. The window moves to the projector if the browser can tell where it is; otherwise drag it there. Then click **Go full screen** on it.

## Set up (once per installation, or after moving anything)

1. **Camera:** check that the camera view shows the sandbox.
2. **Box:**
   1. Flatten the sand to the level you want as sea level, then click **Capture base plane**.
   2. Click the four inside corners of the box in order: front-left, front-right, back-left, back-right.
   3. Click **Save box setup**.
3. **Projector:**
   1. Open the projector window on the projector's screen, full screen.
   2. Set **Disk radius** to your target's radius (a CD is 6 cm), then click **Start calibration**. Keep the box empty while it measures the sand.
   3. A white crosshair with a green circle appears on the sand. Hold the disk flat so the crosshair lands on the center of the disk.
   4. Keep it still. The circle around the disk in the camera view turns green, and it captures automatically after about a second.
   5. Do spots 1–12 **low** (about 5 cm above the sand), then spots 13–24 **high** (about 25 cm up, re-centering as you lift).
   6. An average error under about 4 px is good. **Skip this spot** handles spots you can't reach; at least 6 spots are needed.

Settings are saved in `bridge\sandbox-config.json`, so the sandbox is ready the next time you start it.

## Use

- **Shape the sand.** Hold a hand anywhere over the sand and it rains straight below it, even when the hand is closer than the Kinect can measure (about 80 cm from the camera). Hands are kept out of the terrain, so a still hand doesn't become a hill.
- The **Run** step has rain and drain buttons, removing all water, color maps, contour interval, sea level, water speed and evaporation. It also shows live statistics.
- If the sand shimmers, raise **Jitter filter**. If parts of the sand never appear, raise **Stability limit**. Both are under *Camera filter and detail*.

## Try it without hardware

Double-click **`Start Simulated.bat`**. It opens `http://localhost:8765/physical/?sim=1`, where the control page simulates both a Kinect and a projector. You can go through the whole setup, including calibration, and see the result in the projector window.

## Troubleshooting

| Problem | What to try |
|---|---|
| Windows warns about Memory integrity, or no Kinect is found after installing the SDK | Turn off Memory integrity, then restart (see Install, step 2). |
| "The Kinect for Windows SDK 1.8 is not installed" | Install the SDK or the runtime (see above), then restart the bridge. |
| "No Kinect is connected" | Check the power adapter, and try another USB port (not a hub). Check Device Manager. |
| Camera view is black at the edges | The Kinect reads 0.8–4 m. Raise it if the sand is closer than 80 cm. |
| Projected colors are shifted | Calibrate the projector again (step 3). Make sure neither the Kinect nor the projector has moved. |
| Projector window has a frame | Click **Go full screen** in the projector window, or double-click it. |
| Port 8765 is in use | Run `SandboxBridge.exe --port 8766` and open `http://localhost:8766/physical/`. |

### Command line

```
SandboxBridge.exe [--simulate] [--port 8765] [--www <folder>] [--lan] [--no-browser]
  --simulate    synthetic depth camera instead of a Kinect (for testing the bridge itself)
  --lan         also accept other computers on the network (Windows will ask about the firewall)
```

## Differences from the Linux SARndbox

| Linux SARndbox | This version |
|---|---|
| `RawKinectViewer` to measure the base plane and box corners | **Box** step on the control page |
| `CalibrateProjector` (disk target, 12 points) | **Projector** step: the same disk method, 24 points, automatic capture |
| `FrameFilter` (30-frame averaging, stability test, hysteresis, spatial filter) | Same algorithm, in a background browser thread |
| `RainMaker` (rain under hands above a set height) | Rain under any hand over the sand, up to 4 at once, including hands too close for the Kinect to measure |
| `WaterTable2` water simulation | Same Kurganov–Petrova shader code, ported to WebGL2 |
| Vrui + Linux + Kinect 3D Video package | Windows bridge + Edge/Chrome |
