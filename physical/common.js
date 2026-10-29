/* Physical AR Sandbox — code shared by the control page and the projector page.
   Units: depth arrives in millimetres from the Kinect; everything else is in
   centimetres, like SARndbox. Camera space: x right, y down, z out of the lens.
   Designed by Prof. Xin (Shane) Miao, Missouri State University. GPL-2.0-or-later. */
"use strict";
const SBX = (() => {
  const CH_CONTROL = 'sandbox-control', CH_SIM = 'sandbox-sim';
  const KINECT = {w: 640, h: 480, fx: 571.26, fy: 571.26, cx: 320, cy: 240};   // Kinect v1 nominal depth intrinsics

  /* ---------- small vector / matrix helpers (matrices are column-major Float32Array(16)) ---------- */
  const V = {
    add: (a, b) => [a[0] + b[0], a[1] + b[1], a[2] + b[2]],
    sub: (a, b) => [a[0] - b[0], a[1] - b[1], a[2] - b[2]],
    scale: (a, s) => [a[0] * s, a[1] * s, a[2] * s],
    dot: (a, b) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2],
    cross: (a, b) => [a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0]],
    len: a => Math.hypot(a[0], a[1], a[2]),
    norm: a => { const l = Math.hypot(a[0], a[1], a[2]) || 1; return [a[0] / l, a[1] / l, a[2] / l]; }
  };
  const M4 = {
    mul(a, b) { const r = new Float32Array(16); for (let i = 0; i < 4; i++) for (let j = 0; j < 4; j++) { let s = 0; for (let k = 0; k < 4; k++) s += a[k * 4 + j] * b[i * 4 + k]; r[i * 4 + j] = s; } return r; },
    inv(a) { const r = new Float32Array(16);
      const b00 = a[0] * a[5] - a[1] * a[4], b01 = a[0] * a[6] - a[2] * a[4], b02 = a[0] * a[7] - a[3] * a[4], b03 = a[1] * a[6] - a[2] * a[5], b04 = a[1] * a[7] - a[3] * a[5], b05 = a[2] * a[7] - a[3] * a[6],
        b06 = a[8] * a[13] - a[9] * a[12], b07 = a[8] * a[14] - a[10] * a[12], b08 = a[8] * a[15] - a[11] * a[12], b09 = a[9] * a[14] - a[10] * a[13], b10 = a[9] * a[15] - a[11] * a[13], b11 = a[10] * a[15] - a[11] * a[14];
      let d = b00 * b11 - b01 * b10 + b02 * b09 + b03 * b08 - b04 * b07 + b05 * b06; if (!d) return r; d = 1 / d;
      r[0] = (a[5] * b11 - a[6] * b10 + a[7] * b09) * d; r[1] = (a[2] * b10 - a[1] * b11 - a[3] * b09) * d; r[2] = (a[13] * b05 - a[14] * b04 + a[15] * b03) * d; r[3] = (a[10] * b04 - a[9] * b05 - a[11] * b03) * d;
      r[4] = (a[6] * b08 - a[4] * b11 - a[7] * b07) * d; r[5] = (a[0] * b11 - a[2] * b08 + a[3] * b07) * d; r[6] = (a[14] * b02 - a[12] * b05 - a[15] * b01) * d; r[7] = (a[8] * b05 - a[10] * b02 + a[11] * b01) * d;
      r[8] = (a[4] * b10 - a[5] * b08 + a[7] * b06) * d; r[9] = (a[1] * b08 - a[0] * b10 - a[3] * b06) * d; r[10] = (a[12] * b04 - a[13] * b02 + a[15] * b00) * d; r[11] = (a[9] * b02 - a[8] * b04 - a[11] * b00) * d;
      r[12] = (a[5] * b07 - a[4] * b09 - a[6] * b06) * d; r[13] = (a[0] * b09 - a[1] * b07 + a[2] * b06) * d; r[14] = (a[13] * b01 - a[12] * b03 - a[14] * b00) * d; r[15] = (a[8] * b03 - a[9] * b01 + a[10] * b00) * d; return r; },
    xf(m, p, w) { return [m[0] * p[0] + m[4] * p[1] + m[8] * p[2] + m[12] * w, m[1] * p[0] + m[5] * p[1] + m[9] * p[2] + m[13] * w, m[2] * p[0] + m[6] * p[1] + m[10] * p[2] + m[14] * w]; },
    ortho(l, r, b, t, n, f) { const m = new Float32Array(16); m[0] = 2 / (r - l); m[5] = 2 / (t - b); m[10] = -2 / (f - n); m[12] = -(r + l) / (r - l); m[13] = -(t + b) / (t - b); m[14] = -(f + n) / (f - n); m[15] = 1; return m; }
  };

  /* Symmetric eigen-decomposition (cyclic Jacobi). A is n×n row-major, overwritten.
     Returns {values, vectors} with eigenvector k in column k of `vectors` (row-major n×n). */
  function jacobiEig(A, n) {
    const Vm = new Float64Array(n * n); for (let i = 0; i < n; i++) Vm[i * n + i] = 1;
    for (let sweep = 0; sweep < 100; sweep++) {
      let off = 0; for (let i = 0; i < n; i++) for (let j = i + 1; j < n; j++) off += A[i * n + j] * A[i * n + j];
      if (off < 1e-22) break;
      for (let p = 0; p < n; p++) for (let q = p + 1; q < n; q++) {
        const apq = A[p * n + q]; if (Math.abs(apq) < 1e-300) continue;
        const app = A[p * n + p], aqq = A[q * n + q], th = (aqq - app) / (2 * apq);
        const t = Math.sign(th || 1) / (Math.abs(th) + Math.sqrt(th * th + 1)), c = 1 / Math.sqrt(t * t + 1), s = t * c;
        for (let k = 0; k < n; k++) { const akp = A[k * n + p], akq = A[k * n + q]; A[k * n + p] = c * akp - s * akq; A[k * n + q] = s * akp + c * akq; }
        for (let k = 0; k < n; k++) { const apk = A[p * n + k], aqk = A[q * n + k]; A[p * n + k] = c * apk - s * aqk; A[q * n + k] = s * apk + c * aqk; }
        for (let k = 0; k < n; k++) { const vkp = Vm[k * n + p], vkq = Vm[k * n + q]; Vm[k * n + p] = c * vkp - s * vkq; Vm[k * n + q] = s * vkp + c * vkq; }
      }
    }
    const values = []; for (let i = 0; i < n; i++) values.push(A[i * n + i]);
    return {values, vectors: Vm};
  }
  function smallestEigvec(A, n) { const {values, vectors} = jacobiEig(A, n); let k = 0; for (let i = 1; i < n; i++) if (values[i] < values[k]) k = i; const v = []; for (let i = 0; i < n; i++) v.push(vectors[i * n + k]); return v; }

  /* ---------- camera geometry ---------- */
  function point(cam, x, y, zcm) { return [(x + 0.5 - cam.cx) / cam.fx * zcm, (y + 0.5 - cam.cy) / cam.fy * zcm, zcm]; }
  function rayPlane(cam, plane, x, y) {       // camera ray through pixel (x,y) meets the base plane
    const r = [(x + 0.5 - cam.cx) / cam.fx, (y + 0.5 - cam.cy) / cam.fy, 1]; const den = V.dot(plane.n, r);
    if (Math.abs(den) < 1e-9) return null; const t = -plane.d / den; return t > 0 ? V.scale(r, t) : null;
  }
  function project(cam, P) { return [P[0] / P[2] * cam.fx + cam.cx - 0.5, P[1] / P[2] * cam.fy + cam.cy - 0.5]; }

  /* Least-squares plane through points (array of [x,y,z]); normal faces the camera. */
  function fitPlane(pts) {
    let c = [0, 0, 0]; for (const p of pts) c = V.add(c, p); c = V.scale(c, 1 / pts.length);
    const A = new Float64Array(9);
    for (const p of pts) { const d = V.sub(p, c); for (let i = 0; i < 3; i++) for (let j = 0; j < 3; j++) A[i * 3 + j] += d[i] * d[j]; }
    let n = V.norm(smallestEigvec(A, 3)); if (V.dot(n, c) > 0) n = V.scale(n, -1);
    return {n, d: -V.dot(n, c)};
  }

  /* Box frame from the base plane and four corners (front-left, front-right, back-left, back-right).
     Box coordinates: x along the front edge, y toward the back, z = height above the base plane. */
  function boxFrame(plane, corners) {
    const n = plane.n, onPlane = p => V.sub(p, V.scale(n, V.dot(n, p) + plane.d));
    const c = corners.map(onPlane);
    let ex = V.sub(c[1], c[0]); ex = V.norm(V.sub(ex, V.scale(n, V.dot(ex, n))));
    let ey = V.cross(n, ex), origin = c[0];
    if (V.dot(V.sub(c[2], c[0]), ey) < 0) { ex = V.scale(ex, -1); ey = V.cross(n, ex); origin = c[1]; }   // corners were given mirrored
    const xs = c.map(p => V.dot(V.sub(p, origin), ex)), ys = c.map(p => V.dot(V.sub(p, origin), ey));
    const x0 = Math.min(...xs), x1 = Math.max(...xs), y0 = Math.min(...ys), y1 = Math.max(...ys);
    origin = V.add(origin, V.add(V.scale(ex, x0), V.scale(ey, y0)));
    const ez = n, M = new Float32Array([ex[0], ey[0], ez[0], 0, ex[1], ey[1], ez[1], 0, ex[2], ey[2], ez[2], 0, -V.dot(ex, origin), -V.dot(ey, origin), -V.dot(ez, origin), 1]);
    return {M, Minv: M4.inv(M), LX: x1 - x0, LY: y1 - y0};
  }

  /* Projector calibration: direct linear transform from camera-space points (cm) to
     projector pixels, with Hartley normalisation. pairs: [{P:[x,y,z], u, v}] */
  function solveProjector(pairs) {
    const n = pairs.length; if (n < 6) return null;
    let c3 = [0, 0, 0], c2 = [0, 0]; for (const q of pairs) { c3 = V.add(c3, q.P); c2[0] += q.u; c2[1] += q.v; }
    c3 = V.scale(c3, 1 / n); c2 = [c2[0] / n, c2[1] / n];
    let s3 = 0, s2 = 0; for (const q of pairs) { s3 += V.len(V.sub(q.P, c3)); s2 += Math.hypot(q.u - c2[0], q.v - c2[1]); }
    s3 = Math.sqrt(3) / (s3 / n || 1); s2 = Math.SQRT2 / (s2 / n || 1);
    const AtA = new Float64Array(144);
    const addRow = r => { for (let i = 0; i < 12; i++) for (let j = 0; j < 12; j++) AtA[i * 12 + j] += r[i] * r[j]; };
    for (const q of pairs) {
      const X = V.scale(V.sub(q.P, c3), s3), u = (q.u - c2[0]) * s2, v = (q.v - c2[1]) * s2;
      addRow([X[0], X[1], X[2], 1, 0, 0, 0, 0, -u * X[0], -u * X[1], -u * X[2], -u]);
      addRow([0, 0, 0, 0, X[0], X[1], X[2], 1, -v * X[0], -v * X[1], -v * X[2], -v]);
    }
    const p = smallestEigvec(AtA, 12);
    // denormalise: P = T2^-1 · Pn · T3
    const Pn = [[p[0], p[1], p[2], p[3]], [p[4], p[5], p[6], p[7]], [p[8], p[9], p[10], p[11]]];
    const T3 = [[s3, 0, 0, -s3 * c3[0]], [0, s3, 0, -s3 * c3[1]], [0, 0, s3, -s3 * c3[2]], [0, 0, 0, 1]];
    const T2i = [[1 / s2, 0, c2[0]], [0, 1 / s2, c2[1]], [0, 0, 1]];
    const A = Pn.map(row => [0, 1, 2, 3].map(j => row.reduce((s, x, k) => s + x * T3[k][j], 0)));
    let P = T2i.map(row => [0, 1, 2, 3].map(j => row.reduce((s, x, k) => s + x * A[k][j], 0)));
    if (P[2][0] * c3[0] + P[2][1] * c3[1] + P[2][2] * c3[2] + P[2][3] < 0) P = P.map(r => r.map(x => -x));   // points in front: w > 0
    const flat = [].concat(...P);
    let se = 0; for (const q of pairs) { const [u, v] = applyProjector(flat, q.P); se += (u - q.u) ** 2 + (v - q.v) ** 2; }
    return {P: flat, rms: Math.sqrt(se / n)};
  }
  function applyProjector(P, X) { const w = P[8] * X[0] + P[9] * X[1] + P[10] * X[2] + P[11]; return [(P[0] * X[0] + P[1] * X[1] + P[2] * X[2] + P[3]) / w, (P[4] * X[0] + P[5] * X[1] + P[6] * X[2] + P[7]) / w]; }
  /* Clip-space matrix for WebGL from the 3×4 projector matrix (pixels of a Wp×Hp window). */
  function projectorClip(P, Wp, Hp) {
    const r0 = P.slice(0, 4), r1 = P.slice(4, 8), r2 = P.slice(8, 12), m = new Float32Array(16);
    for (let c = 0; c < 4; c++) { m[c * 4] = 2 / Wp * r0[c] - r2[c]; m[c * 4 + 1] = r2[c] - 2 / Hp * r1[c]; m[c * 4 + 2] = 0; m[c * 4 + 3] = r2[c]; }
    return m;
  }

  /* ---------- configuration (bridge file, with browser storage as a fallback) ---------- */
  function defaults() {
    return {version: 1, camera: Object.assign({}, KINECT), plane: null, corners: null, cornerPx: null, box: null, projector: null,
      filter: {slots: 30, minSamples: 10, maxVariance: 16, hysteresis: 0.5, elevMin: -15, elevMax: 20, spatial: true},
      rain: {enabled: true, above: 4, height: 70, minArea: 30}, detail: 'standard'};
  }
  function merge(c) { const d = defaults(); return Object.assign(d, c, {camera: Object.assign(d.camera, c.camera || {}), filter: Object.assign(d.filter, c.filter || {}), rain: Object.assign(d.rain, c.rain || {})}); }
  async function loadConfig() {
    try { const r = await fetch('/api/config', {cache: 'no-store'}); if (r.ok) { const c = await r.json(); if (c && c.version) return merge(c); } } catch (e) { }
    try { const t = localStorage.getItem('sbxConfig'); if (t) return merge(JSON.parse(t)); } catch (e) { }
    return defaults();
  }
  async function saveConfig(c) {
    let where = null;
    try { localStorage.setItem('sbxConfig', JSON.stringify(c)); where = 'this browser'; } catch (e) { }
    try { const r = await fetch('/api/config', {method: 'POST', headers: {'Content-Type': 'application/json'}, body: JSON.stringify(c, null, 1)}); if (r.ok) where = 'sandbox-config.json next to the bridge'; } catch (e) { }
    return where;
  }
  async function bridgeStatus() { try { const r = await fetch('/api/status', {cache: 'no-store'}); if (r.ok) return await r.json(); } catch (e) { } return null; }
  const isSim = () => new URLSearchParams(location.search).get('sim') === '1';

  /* ---------- depth frames: from the bridge's WebSocket, or the simulator over a BroadcastChannel ---------- */
  function openDepth(onFrame, onStatus) {
    if (isSim()) {
      const bc = new BroadcastChannel(CH_SIM);
      bc.onmessage = e => { const m = e.data; if (m && m.depth) onFrame(m.depth, m.w, m.h, m.n); };
      onStatus && onStatus('sim');
      return {close() { bc.close(); }};
    }
    let ws, closed = false;
    const connect = () => {
      ws = new WebSocket((location.protocol === 'https:' ? 'wss://' : 'ws://') + location.host + '/depth');
      ws.binaryType = 'arraybuffer';
      ws.onopen = () => onStatus && onStatus('open');
      ws.onclose = () => { onStatus && onStatus('closed'); if (!closed) setTimeout(connect, 2000); };
      ws.onmessage = e => {
        const b = e.data; if (!(b instanceof ArrayBuffer) || b.byteLength < 16) return;
        const dv = new DataView(b); if (dv.getUint32(0, true) !== 0x31584253) return;   // "SBX1"
        const w = dv.getUint16(4, true), h = dv.getUint16(6, true);
        onFrame(new Uint16Array(b, 16, w * h), w, h, dv.getUint32(8, true));
      };
    };
    connect();
    return {close() { closed = true; try { ws.close(); } catch (e) { } }};
  }

  return {CH_CONTROL, CH_SIM, KINECT, V, M4, jacobiEig, point, rayPlane, project, fitPlane, boxFrame, solveProjector, applyProjector, projectorClip,
    defaults, merge, loadConfig, saveConfig, bridgeStatus, isSim, openDepth};
})();
