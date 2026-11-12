// Writes the game's current synth sounds (Sfx.cs recipes) as WAV files, for side-by-side listening.
// usage: node make_current.js   -> current/<key>.wav
const fs = require('fs'), path = require('path');
const RATE = 44100;
const N = (f, d, w, v, slide = 0, delay = 0) => ({ f, d, w, v, slide, delay });
const R = {
  click: [N(600, 0.04, 'square', 0.025)],
  pop: [N(800, 0.07, 'triangle', 0.035, 1.8)],
  drop: [N(260, 0.1, 'triangle', 0.04, 0.6)],
  merge: [N(523, 0.1, 'triangle', 0.045), N(784, 0.1, 'triangle', 0.045, 0, 0.06), N(1047, 0.1, 'triangle', 0.045, 0, 0.12)],
  level: [N(523, 0.12, 'triangle', 0.045), N(659, 0.12, 'triangle', 0.045, 0, 0.07), N(784, 0.12, 'triangle', 0.045, 0, 0.14), N(1047, 0.12, 'triangle', 0.045, 0, 0.21)],
  step: [N(330, 0.04, 'sine', 0.025, 1.3)],
  punch: [N(180, 0.12, 'sawtooth', 0.04, 0.4)],
  bump: [N(120, 0.1, 'square', 0.04, 0.5)],
  hiss: [N(900, 0.25, 'sawtooth', 0.025, 0.3)],
  throw: [N(330, 0.08, 'sine', 0.03, 1.8)],
  hurt: [N(220, 0.18, 'square', 0.05, 0.5)],
  nope: [N(150, 0.08, 'square', 0.03)],
  churu: [N(1300, 0.08, 'triangle', 0.04, 1.3), N(1700, 0.1, 'triangle', 0.035, 0, 0.07)],
  boss: [N(110, 0.6, 'sawtooth', 0.05, 0.7)],
};
const out = path.join(__dirname, 'current'); fs.mkdirSync(out, { recursive: true });
for (const [key, notes] of Object.entries(R)) {
  let total = 0; for (const n of notes) total = Math.max(total, n.delay + n.d + 0.02);
  const data = new Float32Array(Math.ceil(total * RATE));
  for (const n of notes) {
    const start = Math.round(n.delay * RATE), len = Math.round(n.d * RATE); let phase = 0;
    for (let i = 0; i < len && start + i < data.length; i++) {
      const t = i / len, f = n.slide > 0 ? n.f * Math.pow(Math.max(40 / n.f, n.slide), t) : n.f;
      phase += f / RATE; const p = phase - Math.floor(phase);
      const s = n.w === 'square' ? (p < 0.5 ? 1 : -1) : n.w === 'triangle' ? 4 * Math.abs(p - 0.5) - 1 : n.w === 'sawtooth' ? 2 * p - 1 : Math.sin(p * Math.PI * 2);
      data[start + i] += s * n.v * Math.pow(0.0001 / n.v, t) * 6;
    }
  }
  const buf = Buffer.alloc(44 + data.length * 2);
  buf.write('RIFF', 0); buf.writeUInt32LE(36 + data.length * 2, 4); buf.write('WAVE', 8); buf.write('fmt ', 12);
  buf.writeUInt32LE(16, 16); buf.writeUInt16LE(1, 20); buf.writeUInt16LE(1, 22); buf.writeUInt32LE(RATE, 24);
  buf.writeUInt32LE(RATE * 2, 28); buf.writeUInt16LE(2, 32); buf.writeUInt16LE(16, 34); buf.write('data', 36); buf.writeUInt32LE(data.length * 2, 40);
  for (let i = 0; i < data.length; i++) buf.writeInt16LE(Math.max(-32768, Math.min(32767, Math.round(data[i] * 32767))), 44 + i * 2);
  fs.writeFileSync(path.join(out, key + '.wav'), buf);
}
console.log('wrote', Object.keys(R).length, 'files to', out);
