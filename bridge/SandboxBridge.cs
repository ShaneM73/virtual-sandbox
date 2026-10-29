// SandboxBridge — Windows bridge between a Kinect v1 depth camera and the
// browser-based AR Sandbox (Virtual AR Sandbox, physical mode).
//
//  * Reads 640 × 480 depth frames from a Kinect for Xbox 360 / Kinect for
//    Windows v1 through the Microsoft Kinect for Windows SDK 1.8.
//  * Serves the sandbox web pages from disk at http://localhost:8765/.
//  * Streams depth frames to the pages over a WebSocket (/depth).
//  * Saves the box layout and projector calibration (/api/config).
//
// Written in C# 5 so it compiles with the C# compiler that ships with
// Windows (.NET Framework 4.x); see build.bat. The Kinect SDK is loaded at
// run time, so the program also runs without it (use --simulate).
//
// Designed by Prof. Xin (Shane) Miao, Missouri State University.
// Part of the Virtual AR Sandbox, based on SARndbox by Oliver Kreylos (UC Davis).
// License: GNU GPL version 2 or later.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace SandboxBridge
{
    // ------------------------------------------------------------------
    // Depth sources
    // ------------------------------------------------------------------
    interface IDepthSource
    {
        string Name { get; }
        int Width { get; }
        int Height { get; }
        // Blocks until the next frame; fills depth in millimetres (0 = no reading).
        bool NextFrame(ushort[] depthMm);
        void Stop();
    }

    // Kinect v1 through the Kinect for Windows SDK 1.8, accessed by reflection
    // so this program needs no reference to Microsoft.Kinect.dll at build time.
    class KinectV1Source : IDepthSource
    {
        object sensor, depthStream;
        MethodInfo openNextFrame, stopMethod;
        short[] raw;

        public string Name { get { return "Kinect v1"; } }
        public int Width { get { return 640; } }
        public int Height { get { return 480; } }

        public KinectV1Source()
        {
            Assembly asm = LoadKinectAssembly();
            Type sensorT = asm.GetType("Microsoft.Kinect.KinectSensor", true);
            IEnumerable sensors = (IEnumerable)sensorT.GetProperty("KinectSensors").GetValue(null, null);
            foreach (object s in sensors)
            {
                string status = sensorT.GetProperty("Status").GetValue(s, null).ToString();
                if (status == "Connected") { sensor = s; break; }
                Console.WriteLine("  Found a Kinect with status: " + status);
            }
            if (sensor == null)
                throw new Exception("No Kinect is connected. Check that the Kinect has its power adapter plugged in, " +
                                    "is on a USB 2.0 or 3.0 port, and shows up as 'Kinect for Windows' in Device Manager.");

            depthStream = sensorT.GetProperty("DepthStream").GetValue(sensor, null);
            Type dsT = depthStream.GetType();
            Type fmtT = asm.GetType("Microsoft.Kinect.DepthImageFormat", true);
            object fmt = Enum.Parse(fmtT, "Resolution640x480Fps30");
            dsT.GetMethod("Enable", new Type[] { fmtT }).Invoke(depthStream, new object[] { fmt });
            sensorT.GetMethod("Start", Type.EmptyTypes).Invoke(sensor, null);
            openNextFrame = dsT.GetMethod("OpenNextFrame", new Type[] { typeof(int) });
            stopMethod = sensorT.GetMethod("Stop", Type.EmptyTypes);
            raw = new short[640 * 480];
        }

        static Assembly LoadKinectAssembly()
        {
            string[] names = {
                "Microsoft.Kinect, Version=1.8.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35",
                "Microsoft.Kinect, Version=1.7.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35"
            };
            foreach (string n in names)
            {
                try { return Assembly.Load(n); } catch { }
            }
            List<string> paths = new List<string>();
            string sdk = Environment.GetEnvironmentVariable("KINECTSDK10_DIR");
            if (!string.IsNullOrEmpty(sdk)) paths.Add(Path.Combine(Path.Combine(sdk, "Assemblies"), "Microsoft.Kinect.dll"));
            paths.Add(@"C:\Program Files\Microsoft SDKs\Kinect\v1.8\Assemblies\Microsoft.Kinect.dll");
            paths.Add(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Microsoft.Kinect.dll"));
            foreach (string p in paths)
            {
                if (File.Exists(p)) return Assembly.LoadFrom(p);
            }
            throw new Exception("The Kinect for Windows SDK 1.8 is not installed (Microsoft.Kinect.dll was not found). " +
                                "Install 'Kinect for Windows SDK v1.8' from Microsoft, then run this program again.");
        }

        public bool NextFrame(ushort[] depthMm)
        {
            object frame = openNextFrame.Invoke(depthStream, new object[] { 200 });
            if (frame == null) return false;
            try
            {
                Type fT = frame.GetType();
                int len = (int)fT.GetProperty("PixelDataLength").GetValue(frame, null);
                if (len != raw.Length) raw = new short[len];
                fT.GetMethod("CopyPixelDataTo", new Type[] { typeof(short[]) }).Invoke(frame, new object[] { raw });
            }
            finally { ((IDisposable)frame).Dispose(); }
            int n = Math.Min(raw.Length, depthMm.Length);
            for (int i = 0; i < n; i++)
            {
                int d = ((ushort)raw[i]) >> 3;            // low 3 bits are the player index
                depthMm[i] = (ushort)((d >= 400 && d < 4000) ? d : 0);   // keep 0.4–4 m readings
            }
            return true;
        }

        public void Stop()
        {
            try { stopMethod.Invoke(sensor, null); } catch { }
        }
    }

    // A synthetic Kinect for testing without hardware: a slowly changing sand
    // surface 1 m below a slightly tilted camera, with Kinect-like noise.
    class SimulatedSource : IDepthSource
    {
        readonly Random rnd = new Random(1);
        readonly Stopwatch clock = Stopwatch.StartNew();
        long lastTick;
        public string Name { get { return "Simulated Kinect"; } }
        public int Width { get { return 640; } }
        public int Height { get { return 480; } }

        public bool NextFrame(ushort[] depthMm)
        {
            long wait = 33 - (clock.ElapsedMilliseconds - lastTick);
            if (wait > 0) Thread.Sleep((int)wait);
            lastTick = clock.ElapsedMilliseconds;
            double t = clock.ElapsedMilliseconds / 1000.0;
            const double fx = 571.26, cx = 320, cy = 240, tilt = 0.12, camH = 1000;   // mm
            double ct = Math.Cos(tilt), st = Math.Sin(tilt);
            for (int y = 0; y < 480; y++)
            {
                for (int x = 0; x < 640; x++)
                {
                    double rx = (x - cx) / fx, ry = (y - cy) / fx;
                    // ray in a camera tilted about x; floor is the plane Y=camH in world
                    double wy = ct - ry * st;            // downward component of the ray per unit camera depth
                    double z = camH / wy;                // depth to the flat base plane
                    double wx = rx * z, wz = (st + ry * ct) * z;
                    double h = 60 * Math.Sin(wx / 180 + 0.3 * Math.Sin(t / 7)) * Math.Cos(wz / 220) + 25 * Math.Sin(wx / 70 + wz / 90);
                    double d = (camH - h) / wy;
                    d += (rnd.NextDouble() - 0.5) * 3;   // ±1.5 mm noise
                    bool inBox = Math.Abs(wx) < 560 && Math.Abs(wz - 120) < 430;
                    depthMm[y * 640 + x] = (ushort)(inBox ? Math.Max(0, Math.Min(4000, d)) : (camH + 300) / wy);
                }
            }
            return true;
        }
        public void Stop() { }
    }

    // ------------------------------------------------------------------
    // WebSocket client (server side). Each client keeps only the newest
    // frame, so a slow browser drops frames instead of building a backlog.
    // ------------------------------------------------------------------
    class WsClient
    {
        readonly TcpClient tcp;
        readonly NetworkStream stream;
        readonly AutoResetEvent signal = new AutoResetEvent(false);
        byte[] pending;
        public volatile bool Alive = true;

        public WsClient(TcpClient t, NetworkStream s)
        {
            tcp = t; stream = s;
            new Thread(SendLoop) { IsBackground = true, Name = "ws-send" }.Start();
            new Thread(ReadLoop) { IsBackground = true, Name = "ws-read" }.Start();
        }

        public void Post(byte[] wsFrame)
        {
            Interlocked.Exchange(ref pending, wsFrame);
            signal.Set();
        }

        void SendLoop()
        {
            try
            {
                while (Alive)
                {
                    signal.WaitOne(1000);
                    byte[] f = Interlocked.Exchange(ref pending, null);
                    if (f != null) lock (stream) stream.Write(f, 0, f.Length);
                }
            }
            catch { }
            Close();
        }

        void ReadLoop()
        {
            try
            {
                byte[] hdr = new byte[14];
                while (Alive)
                {
                    if (!ReadExact(hdr, 0, 2)) break;
                    int opcode = hdr[0] & 0x0F;
                    bool masked = (hdr[1] & 0x80) != 0;
                    long len = hdr[1] & 0x7F;
                    if (len == 126) { if (!ReadExact(hdr, 2, 2)) break; len = (hdr[2] << 8) | hdr[3]; }
                    else if (len == 127) { if (!ReadExact(hdr, 2, 8)) break; len = 0; for (int i = 2; i < 10; i++) len = (len << 8) | hdr[i]; }
                    byte[] mask = new byte[4];
                    if (masked && !ReadExact(mask, 0, 4)) break;
                    if (len > 1 << 20) break;
                    byte[] payload = new byte[len];
                    if (!ReadExact(payload, 0, (int)len)) break;
                    if (masked) for (int i = 0; i < payload.Length; i++) payload[i] ^= mask[i & 3];
                    if (opcode == 8) break;                                   // close
                    if (opcode == 9) lock (stream) { byte[] pong = Frame(payload, 0xA); stream.Write(pong, 0, pong.Length); }
                }
            }
            catch { }
            Close();
        }

        bool ReadExact(byte[] buf, int off, int n)
        {
            while (n > 0)
            {
                int r = stream.Read(buf, off, n);
                if (r <= 0) return false;
                off += r; n -= r;
            }
            return true;
        }

        public void Close()
        {
            if (!Alive) return;
            Alive = false;
            signal.Set();
            try { tcp.Close(); } catch { }
        }

        public static byte[] Frame(byte[] payload, int opcode)
        {
            int n = payload.Length;
            int hl = n < 126 ? 2 : (n <= 65535 ? 4 : 10);
            byte[] f = new byte[hl + n];
            f[0] = (byte)(0x80 | opcode);
            if (n < 126) f[1] = (byte)n;
            else if (n <= 65535) { f[1] = 126; f[2] = (byte)(n >> 8); f[3] = (byte)n; }
            else { f[1] = 127; long L = n; for (int i = 0; i < 8; i++) f[9 - i] = (byte)(L >> (8 * i)); }
            Buffer.BlockCopy(payload, 0, f, hl, n);
            return f;
        }
    }

    // ------------------------------------------------------------------
    // Program: HTTP + WebSocket server and the camera loop
    // ------------------------------------------------------------------
    static class Program
    {
        static int port = 8765;
        static string webRoot, configPath;
        static bool lan, openBrowser = true, simulate;
        static IDepthSource source;
        static string sourceError = "";
        static readonly List<WsClient> clients = new List<WsClient>();
        static volatile int framesThisSecond, fps;
        static volatile bool running = true;

        static int Main(string[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i].ToLowerInvariant();
                if (a == "--simulate") simulate = true;
                else if (a == "--lan") lan = true;
                else if (a == "--no-browser") openBrowser = false;
                else if (a == "--port" && i + 1 < args.Length) port = int.Parse(args[++i]);
                else if (a == "--www" && i + 1 < args.Length) webRoot = Path.GetFullPath(args[++i]);
                else if (a == "--help" || a == "-h" || a == "/?") { PrintHelp(); return 0; }
            }
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            if (webRoot == null)
            {
                string bundled = Path.Combine(exeDir, "www");
                webRoot = Directory.Exists(bundled) ? bundled : Path.GetFullPath(Path.Combine(exeDir, ".."));
            }
            configPath = Path.Combine(exeDir, "sandbox-config.json");

            Console.Title = "AR Sandbox bridge";
            Console.WriteLine("AR Sandbox bridge — Kinect v1 to browser");
            Console.WriteLine("  Web pages from: " + webRoot);
            Console.WriteLine("  Settings file:  " + configPath);

            try { source = simulate ? (IDepthSource)new SimulatedSource() : new KinectV1Source(); }
            catch (Exception e)
            {
                Exception inner = e is TargetInvocationException && e.InnerException != null ? e.InnerException : e;
                sourceError = inner.Message;
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine();
                Console.WriteLine("  Camera problem: " + sourceError);
                Console.WriteLine("  The web pages still work. To try them without a Kinect, run:  SandboxBridge.exe --simulate");
                Console.ResetColor();
            }
            if (source != null)
            {
                Console.WriteLine("  Camera:         " + source.Name + " (" + source.Width + " x " + source.Height + ")");
                new Thread(CameraLoop) { IsBackground = true, Name = "camera" }.Start();
            }

            TcpListener listener;
            try
            {
                listener = new TcpListener(lan ? IPAddress.Any : IPAddress.Loopback, port);
                listener.Start();
            }
            catch (Exception e)
            {
                Console.WriteLine("Could not open port " + port + ": " + e.Message + ". Is another copy already running?");
                return 1;
            }
            string url = "http://localhost:" + port + "/physical/";
            Console.WriteLine();
            Console.WriteLine("  Open the sandbox at  " + url);
            if (lan) Console.WriteLine("  (also reachable from other computers on this network)");
            Console.WriteLine("  Keep this window open while you use the sandbox. Press Ctrl+C to quit.");
            Console.WriteLine();
            Console.CancelKeyPress += delegate (object s, ConsoleCancelEventArgs e) { running = false; if (source != null) source.Stop(); };

            if (openBrowser)
            {
                try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
            }
            new Thread(StatusLoop) { IsBackground = true, Name = "status" }.Start();

            while (running)
            {
                TcpClient c;
                try { c = listener.AcceptTcpClient(); } catch { break; }
                ThreadPool.QueueUserWorkItem(HandleConnection, c);
            }
            if (source != null) source.Stop();
            return 0;
        }

        static void PrintHelp()
        {
            Console.WriteLine("SandboxBridge.exe [--simulate] [--port 8765] [--www <folder>] [--lan] [--no-browser]");
            Console.WriteLine("  --simulate    use a synthetic depth camera instead of a Kinect");
            Console.WriteLine("  --port N      HTTP port (default 8765)");
            Console.WriteLine("  --www DIR     folder holding the sandbox web pages (default: the folder above this program)");
            Console.WriteLine("  --lan         also accept connections from other computers");
            Console.WriteLine("  --no-browser  do not open the browser automatically");
        }

        static void CameraLoop()
        {
            int w = source.Width, h = source.Height;
            ushort[] depth = new ushort[w * h];
            uint frameNo = 0;
            while (running)
            {
                bool ok;
                try { ok = source.NextFrame(depth); }
                catch (Exception e)
                {
                    sourceError = (e.InnerException ?? e).Message;
                    Thread.Sleep(500);
                    continue;
                }
                if (!ok) continue;
                frameNo++;
                framesThisSecond++;
                // message: "SBX1", width, height (uint16), frame number (uint32), reserved (uint32), then uint16 depth in mm
                byte[] msg = new byte[16 + w * h * 2];
                msg[0] = (byte)'S'; msg[1] = (byte)'B'; msg[2] = (byte)'X'; msg[3] = (byte)'1';
                msg[4] = (byte)w; msg[5] = (byte)(w >> 8); msg[6] = (byte)h; msg[7] = (byte)(h >> 8);
                msg[8] = (byte)frameNo; msg[9] = (byte)(frameNo >> 8); msg[10] = (byte)(frameNo >> 16); msg[11] = (byte)(frameNo >> 24);
                Buffer.BlockCopy(depth, 0, msg, 16, w * h * 2);
                byte[] frame = WsClient.Frame(msg, 0x2);
                lock (clients)
                {
                    clients.RemoveAll(delegate (WsClient c) { return !c.Alive; });
                    foreach (WsClient c in clients) c.Post(frame);
                }
            }
        }

        static void StatusLoop()
        {
            while (running)
            {
                Thread.Sleep(1000);
                fps = framesThisSecond; framesThisSecond = 0;
                int n; lock (clients) n = clients.Count;
                string cam = source == null ? "no camera" : (source.Name + " " + fps + " fps");
                Console.Write("\r  " + cam + " | browser connections: " + n + "        ");
            }
        }

        // ---------------- HTTP ----------------
        static void HandleConnection(object state)
        {
            TcpClient tcp = (TcpClient)state;
            NetworkStream s = null;
            bool keep = false;
            try
            {
                tcp.NoDelay = true;
                s = tcp.GetStream();
                string head = ReadHead(s);
                if (head == null) return;
                string[] lines = head.Split(new string[] { "\r\n" }, StringSplitOptions.None);
                string[] req = lines[0].Split(' ');
                if (req.Length < 2) return;
                string method = req[0], path = Uri.UnescapeDataString(req[1].Split('?')[0]);
                Dictionary<string, string> hdr = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 1; i < lines.Length; i++)
                {
                    int c = lines[i].IndexOf(':');
                    if (c > 0) hdr[lines[i].Substring(0, c).Trim()] = lines[i].Substring(c + 1).Trim();
                }

                string upgrade;
                if (path == "/depth" && hdr.TryGetValue("Upgrade", out upgrade) && upgrade.ToLowerInvariant() == "websocket")
                {
                    string key = hdr["Sec-WebSocket-Key"];
                    string accept;
                    using (SHA1 sha = SHA1.Create())
                        accept = Convert.ToBase64String(sha.ComputeHash(Encoding.ASCII.GetBytes(key + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11")));
                    byte[] resp = Encoding.ASCII.GetBytes("HTTP/1.1 101 Switching Protocols\r\nUpgrade: websocket\r\nConnection: Upgrade\r\nSec-WebSocket-Accept: " + accept + "\r\n\r\n");
                    s.Write(resp, 0, resp.Length);
                    WsClient wc = new WsClient(tcp, s);
                    lock (clients) clients.Add(wc);
                    keep = true;
                    return;
                }
                if (path == "/api/status")
                {
                    string json = "{\"camera\":" + Json(source == null ? "none" : source.Name) +
                                  ",\"simulated\":" + (simulate ? "true" : "false") +
                                  ",\"fps\":" + fps +
                                  ",\"width\":" + (source == null ? 0 : source.Width) +
                                  ",\"height\":" + (source == null ? 0 : source.Height) +
                                  ",\"error\":" + Json(sourceError) + "}";
                    Send(s, 200, "application/json", Encoding.UTF8.GetBytes(json));
                    return;
                }
                if (path == "/api/config")
                {
                    if (method == "POST")
                    {
                        int len = 0; string cl;
                        if (hdr.TryGetValue("Content-Length", out cl)) int.TryParse(cl, out len);
                        if (len <= 0 || len > 2000000) { Send(s, 400, "text/plain", Encoding.UTF8.GetBytes("Bad length")); return; }
                        byte[] body = new byte[len]; int got = 0;
                        while (got < len) { int r = s.Read(body, got, len - got); if (r <= 0) break; got += r; }
                        File.WriteAllBytes(configPath, body);
                        Send(s, 200, "application/json", Encoding.UTF8.GetBytes("{\"saved\":true}"));
                    }
                    else if (File.Exists(configPath)) Send(s, 200, "application/json", File.ReadAllBytes(configPath));
                    else Send(s, 404, "application/json", Encoding.UTF8.GetBytes("{}"));
                    return;
                }
                ServeFile(s, path);
            }
            catch { }
            finally
            {
                if (!keep) { try { tcp.Close(); } catch { } }
            }
        }

        static string ReadHead(NetworkStream s)
        {
            MemoryStream ms = new MemoryStream();
            int b, state = 0;
            while (ms.Length < 32768 && (b = s.ReadByte()) >= 0)
            {
                ms.WriteByte((byte)b);
                state = (b == '\r' && (state == 0 || state == 2)) ? state + 1 : (b == '\n' && (state == 1 || state == 3)) ? state + 1 : 0;
                if (state == 4) return Encoding.ASCII.GetString(ms.ToArray());
            }
            return null;
        }

        static void ServeFile(NetworkStream s, string path)
        {
            if (path.Contains("..")) { Send(s, 403, "text/plain", Encoding.UTF8.GetBytes("Forbidden")); return; }
            string rel = path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            string full = Path.Combine(webRoot, rel);
            if (Directory.Exists(full))
            {
                if (!path.EndsWith("/")) { Redirect(s, path + "/"); return; }
                full = Path.Combine(full, "index.html");
            }
            if (!File.Exists(full)) { Send(s, 404, "text/plain", Encoding.UTF8.GetBytes("Not found: " + path)); return; }
            Send(s, 200, ContentType(full), File.ReadAllBytes(full));
        }

        static string ContentType(string f)
        {
            switch (Path.GetExtension(f).ToLowerInvariant())
            {
                case ".html": case ".htm": return "text/html; charset=utf-8";
                case ".js": return "text/javascript; charset=utf-8";
                case ".css": return "text/css; charset=utf-8";
                case ".json": return "application/json";
                case ".png": return "image/png";
                case ".jpg": case ".jpeg": return "image/jpeg";
                case ".svg": return "image/svg+xml";
                case ".ico": return "image/x-icon";
                case ".asc": case ".txt": case ".md": return "text/plain; charset=utf-8";
                default: return "application/octet-stream";
            }
        }

        static void Send(NetworkStream s, int code, string type, byte[] body)
        {
            string reason = code == 200 ? "OK" : code == 404 ? "Not Found" : code == 403 ? "Forbidden" : "Bad Request";
            byte[] h = Encoding.ASCII.GetBytes("HTTP/1.1 " + code + " " + reason + "\r\nContent-Type: " + type +
                "\r\nContent-Length: " + body.Length + "\r\nCache-Control: no-store\r\nConnection: close\r\n\r\n");
            s.Write(h, 0, h.Length);
            s.Write(body, 0, body.Length);
        }

        static void Redirect(NetworkStream s, string to)
        {
            byte[] h = Encoding.ASCII.GetBytes("HTTP/1.1 301 Moved Permanently\r\nLocation: " + to + "\r\nContent-Length: 0\r\nConnection: close\r\n\r\n");
            s.Write(h, 0, h.Length);
        }

        static string Json(string v)
        {
            StringBuilder sb = new StringBuilder("\"");
            foreach (char c in v ?? "")
            {
                if (c == '"' || c == '\\') sb.Append('\\').Append(c);
                else if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4"));
                else sb.Append(c);
            }
            return sb.Append('"').ToString();
        }
    }
}
