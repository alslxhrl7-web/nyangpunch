const INK = '#2b2233', MINT = '#5fbf98', TOMATO = '#e8505b', BUTTER = '#ffd23f', PAPER = '#fffaf2';
const CARD = '#d9a066', CARD_D = '#b67c43', CARD_L = '#ecc08a', TAPE = '#f3e2b3', HOLE = '#5a3d26';
const PI2 = Math.PI * 2;
let ctx = null;
/* ---------- drawing helpers ---------- */
function circle(x, y, r) { ctx.beginPath(); ctx.arc(x, y, r, 0, PI2); }
function ell(x, y, rx, ry, rot) { ctx.beginPath(); ctx.ellipse(x, y, rx, ry, rot || 0, 0, PI2); }
function rrect(x, y, w, h, r) { ctx.beginPath(); if (ctx.roundRect) ctx.roundRect(x, y, w, h, r); else ctx.rect(x, y, w, h); }
function fs(fill, lw) { ctx.fillStyle = fill; ctx.fill(); if (lw !== 0) { ctx.lineWidth = lw || 3; ctx.strokeStyle = INK; ctx.stroke(); } }
function shadow(w, h, y) { ctx.fillStyle = 'rgba(43,34,51,.16)'; ell(0, y, w, h); ctx.fill(); }
function otext(str, x, y, size, fill, align) {
  ctx.font = `${size}px Jua, 'Malgun Gothic', sans-serif`;
  ctx.textAlign = align || 'center'; ctx.textBaseline = 'middle';
  ctx.lineJoin = 'round'; ctx.lineWidth = Math.max(3, size / 5); ctx.strokeStyle = INK;
  ctx.strokeText(str, x, y); ctx.fillStyle = fill || '#fff'; ctx.fillText(str, x, y);
}
function churuShape(x, y, s, rot) {
  ctx.save(); ctx.translate(x, y); ctx.rotate(rot || 0); ctx.scale(s, s);
  rrect(-2.5, -13, 5, 5, 1.5); fs('#fff', 1.8);
  rrect(-4, -9, 8, 19, 3); fs('#ff8fb1', 2.2);
  ctx.beginPath(); ctx.moveTo(-4, -2); ctx.lineTo(4, -2); ctx.lineWidth = 1.6; ctx.strokeStyle = INK; ctx.stroke();
  ctx.restore();
}
function pawShape(x, y, s, pad) {
  ctx.save(); ctx.translate(x, y); ctx.scale(s, s);
  ctx.fillStyle = pad; ell(0, 4, 7, 5.5); ctx.fill();
  [[-7.5, -3.5], [-2.7, -8], [2.7, -8], [7.5, -3.5]].forEach(([a, b]) => { circle(a, b, 2.8); ctx.fill(); });
  ctx.restore();
}

const CAT = { body: '#f5a142', stripe: '#cf7420', inner: '#f59a9a', belly: '#ffd49a' };
function drawCat(x, y, face, walk, moving, mood, time, sc) {
  ctx.save(); ctx.translate(x, y); ctx.scale(sc * face, sc);
  ctx.lineJoin = 'round'; ctx.lineCap = 'round';
  shadow(21, 6, 19);
  const bob = moving ? Math.abs(Math.sin(walk * 11)) * 4 : 0;
  const breathe = moving ? 0 : Math.sin(time * 2.4) * 0.025;
  ctx.translate(0, -bob);
  const tw = Math.sin(time * (moving ? 9 : 3));
  ctx.beginPath(); ctx.moveTo(-15, 10); ctx.bezierCurveTo(-30, 12, -34, -2, -28 + tw * 3, -16);
  ctx.lineWidth = 12; ctx.strokeStyle = INK; ctx.stroke(); ctx.lineWidth = 6.5; ctx.strokeStyle = CAT.body; ctx.stroke();
  const st = moving ? Math.sin(walk * 22) * 4 : 0;
  ell(-9 + st, 17, 6.5, 4.5); fs(CAT.body, 2.5);
  ell(10 - st, 17, 6.5, 4.5); fs(CAT.body, 2.5);
  ctx.save(); ctx.translate(0, 16); ctx.scale(1 + breathe, 1 - breathe); ctx.translate(0, -16);
  ctx.beginPath(); ctx.moveTo(-18, -6); ctx.lineTo(-14, -27); ctx.lineTo(-2, -16); ctx.closePath(); fs(CAT.body);
  ctx.beginPath(); ctx.moveTo(6, -16); ctx.lineTo(17, -27); ctx.lineTo(21, -5); ctx.closePath(); fs(CAT.body);
  ctx.fillStyle = CAT.inner;
  ctx.beginPath(); ctx.moveTo(-14.5, -11); ctx.lineTo(-13, -21); ctx.lineTo(-7, -15); ctx.closePath(); ctx.fill();
  ctx.beginPath(); ctx.moveTo(10, -15); ctx.lineTo(16, -21); ctx.lineTo(17.5, -10); ctx.closePath(); ctx.fill();
  ell(1, 0, 23, 19.5); fs(CAT.body, 3.2);
  ell(6, 9, 12, 7.5); ctx.fillStyle = CAT.belly; ctx.fill();
  ctx.strokeStyle = CAT.stripe; ctx.lineWidth = 3; ctx.beginPath();
  ctx.moveTo(-3, -18); ctx.lineTo(-2, -12); ctx.moveTo(2, -19); ctx.lineTo(2.5, -13); ctx.moveTo(7, -18); ctx.lineTo(7, -12);
  ctx.moveTo(-21, -2); ctx.lineTo(-15, -1); ctx.moveTo(-21, 4); ctx.lineTo(-15, 4); ctx.stroke();
  ctx.strokeStyle = INK; ctx.fillStyle = INK; ctx.lineWidth = 2.4;
  if (mood === 'hurt') {
    ctx.beginPath(); ctx.moveTo(2, -6.5); ctx.lineTo(6.5, -3.5); ctx.lineTo(2, -0.5);
    ctx.moveTo(17, -6.5); ctx.lineTo(12.5, -3.5); ctx.lineTo(17, -0.5); ctx.stroke();
  } else if (mood === 'ko') {
    ctx.beginPath();
    ctx.moveTo(2, -6); ctx.lineTo(7, -1); ctx.moveTo(7, -6); ctx.lineTo(2, -1);
    ctx.moveTo(12, -6); ctx.lineTo(17, -1); ctx.moveTo(17, -6); ctx.lineTo(12, -1); ctx.stroke();
  } else if (mood === 'blink') {
    ctx.beginPath(); ctx.moveTo(2, -3); ctx.lineTo(7, -3); ctx.moveTo(12, -3); ctx.lineTo(17, -3); ctx.stroke();
  } else {
    circle(4.5, -3.5, 2.7); ctx.fill(); circle(14.5, -3.5, 2.7); ctx.fill();
  }
  ctx.lineWidth = 2;
  ctx.beginPath(); ctx.arc(7.4, 2.6, 2.4, 0, Math.PI); ctx.stroke();
  ctx.beginPath(); ctx.arc(12.2, 2.6, 2.4, 0, Math.PI); ctx.stroke();
  ctx.fillStyle = 'rgba(232,80,91,.35)'; ell(0, 4.5, 3.5, 2); ctx.fill(); ell(19.5, 4.5, 2.8, 2); ctx.fill();
  ctx.restore();
  ctx.restore();
}

function drawMouse(e, time, brown) {
  const fl = e.flash > 0;
  const col = fl ? '#fff' : brown ? '#8d6e5a' : '#aaa4b8';
  ctx.save(); ctx.scale(e.face, 1);
  shadow(14, 4, 12);
  const w = Math.sin(time * 14 + e.seed);
  ctx.translate(0, -Math.abs(w) * 2);
  ctx.beginPath(); ctx.moveTo(-13, 5); ctx.quadraticCurveTo(-24, 12 + w * 4, -30, 2);
  ctx.lineWidth = 4.5; ctx.strokeStyle = INK; ctx.stroke(); ctx.lineWidth = 2; ctx.strokeStyle = '#f2a0b0'; ctx.stroke();
  ell(0, 2, 15, 11); fs(col);
  circle(3, -9, 6.5); fs(col); circle(3, -9, 3.2); ctx.fillStyle = '#f2a0b0'; ctx.fill();
  circle(9, -1, 2.1); ctx.fillStyle = INK; ctx.fill();
  if (brown) { ctx.beginPath(); ctx.moveTo(4.5, -6.5); ctx.lineTo(12, -3.5); ctx.lineWidth = 2.6; ctx.strokeStyle = INK; ctx.stroke(); }
  circle(15.5, 2.5, 2.6); fs('#f2a0b0', 2);
  ctx.restore();
}
function drawFly(e, time) {
  const fl = e.flash > 0;
  ctx.save(); ctx.scale(e.face, 1);
  shadow(8, 3, 14);
  ctx.translate(0, -4 + Math.sin(time * 6 + e.seed) * 2);
  const f = Math.abs(Math.sin(time * 40 + e.seed));
  ctx.globalAlpha = 0.85;
  ell(-4, -9, 5, 3 + f * 6, -0.5); fs('#eef5ff', 2);
  ell(4, -9, 5, 3 + f * 6, 0.5); fs('#eef5ff', 2);
  ctx.globalAlpha = 1;
  circle(0, 0, 9); fs(fl ? '#fff' : '#4d4660', 2.6);
  circle(3, -2.5, 2.6); ctx.fillStyle = '#ff7a7a'; ctx.fill();
  circle(7.2, 0.5, 2.1); ctx.fill();
  ctx.restore();
}
function drawCucumber(e, time) {
  const fl = e.flash > 0;
  ctx.save(); ctx.scale(e.face, 1);
  shadow(14, 4, 25);
  const wob = Math.sin(time * 7 + e.seed);
  ctx.lineCap = 'round';
  ctx.beginPath(); ctx.moveTo(-5, 18); ctx.lineTo(-6 - wob * 3, 27); ctx.moveTo(5, 18); ctx.lineTo(6 + wob * 3, 27);
  ctx.lineWidth = 3; ctx.strokeStyle = INK; ctx.stroke();
  ctx.rotate(wob * 0.1);
  rrect(-3, -27, 6, 7, 2); fs('#8a6a3d', 2);
  rrect(-11, -21, 22, 42, 11); fs(fl ? '#fff' : '#67b356', 3);
  ctx.fillStyle = '#4b8f3e';
  [[-5, -13], [4, -15], [-4, 7], [5, 13], [-1, 15], [6, 3]].forEach(([x, y]) => { circle(x, y, 1.6); ctx.fill(); });
  ctx.fillStyle = INK; circle(-1, -6, 2.3); ctx.fill(); circle(6, -6, 2.3); ctx.fill();
  ctx.beginPath(); ctx.moveTo(-4.5, -11.5); ctx.lineTo(1, -9); ctx.moveTo(9.5, -11.5); ctx.lineTo(4, -9);
  ctx.moveTo(-1, 2.5); ctx.quadraticCurveTo(2.5, -0.5, 6, 2.5);
  ctx.lineWidth = 2.4; ctx.stroke();
  ctx.restore();
}
function drawVacuum(e, time) {
  const fl = e.flash > 0;
  ctx.save(); ctx.scale(e.face, 1);
  shadow(44, 10, 34);
  for (const sx of [-30, 30]) {
    ctx.save(); ctx.translate(sx, 26); ctx.rotate(time * 18 * (sx > 0 ? 1 : -1));
    ctx.beginPath(); for (let k = 0; k < 3; k++) { const a = k * PI2 / 3; ctx.moveTo(0, 0); ctx.lineTo(Math.cos(a) * 12, Math.sin(a) * 12); }
    ctx.lineWidth = 3; ctx.strokeStyle = INK; ctx.lineCap = 'round'; ctx.stroke(); ctx.restore();
  }
  ell(0, 8, 46, 32); fs('#8f8aa0', 4);
  ell(0, 0, 46, 32); fs(fl ? '#fff' : '#dcd9e4', 4);
  ell(0, -2, 31, 20); fs('#c4bfd0', 3);
  ell(-10, -2, 6.5, 5.5); fs('#fff', 2.5); ell(10, -2, 6.5, 5.5); fs('#fff', 2.5);
  ctx.fillStyle = INK; circle(-8, -1, 2.6); ctx.fill(); circle(12, -1, 2.6); ctx.fill();
  ctx.beginPath(); ctx.moveTo(-18, -11); ctx.lineTo(-5, -7); ctx.moveTo(18, -11); ctx.lineTo(5, -7);
  ctx.lineWidth = 3.5; ctx.lineCap = 'round'; ctx.strokeStyle = INK; ctx.stroke();
  circle(0, -15, 3.5); fs(Math.sin(time * 10) > 0 ? '#ff4d4d' : '#7a2b2b', 2);
  ctx.restore();
}
function drawTub(e, time) {
  const fl = e.flash > 0, b = Math.sin(time * 3) * 2;
  ctx.save(); ctx.scale(e.face, 1);
  shadow(52, 10, 38);
  circle(-38, 31, 6); fs('#c9a14a', 3); circle(38, 31, 6); fs('#c9a14a', 3);
  rrect(-52, -18, 104, 50, 22); fs(fl ? '#fff' : '#eef3fb', 4);
  rrect(-58, -26, 116, 14, 7); fs('#fff', 4);
  for (let i = 0; i < 6; i++) { circle(-36 + i * 13, -30 - (i % 2) * 7 + b * (i % 2 ? 1 : -1), 8 + (i % 3) * 2); fs('#e3f1ff', 2.5); }
  ell(-14, 4, 7, 6); fs('#fff', 2.5); ell(14, 4, 7, 6); fs('#fff', 2.5);
  ctx.fillStyle = INK; circle(-12, 5, 3); ctx.fill(); circle(16, 5, 3); ctx.fill();
  ctx.beginPath(); ctx.moveTo(-23, -5); ctx.lineTo(-8, 0); ctx.moveTo(23, -5); ctx.lineTo(8, 0);
  ctx.moveTo(-8, 22); ctx.quadraticCurveTo(0, 15, 8, 22);
  ctx.lineWidth = 3.5; ctx.lineCap = 'round'; ctx.strokeStyle = INK; ctx.stroke();
  ctx.restore();
}

/* ---------- boxes & hideout cats ---------- */
const KITTY = {
  cheese:   { body: '#f5a142', stripe: '#cf7420' },
  mackerel: { body: '#a39d93', stripe: '#5f5a52' },
  tuxedo:   { body: '#3a3340', muzzle: '#fff' },
  calico:   { body: '#fff7ea', patchA: '#f5a142', patchB: '#3a3340' },
  black:    { body: '#2f2a36', eye: BUTTER },
};
function catHead(x, y, s, kind, time, seed) {
  const k = KITTY[kind];
  const blink = ((time + seed * 1.7) % 4.2) < 0.13;
  ctx.save(); ctx.translate(x, y); ctx.scale(s, s); ctx.lineJoin = 'round'; ctx.lineCap = 'round';
  ctx.beginPath(); ctx.moveTo(-13, -3); ctx.lineTo(-11, -17); ctx.lineTo(-3, -9); ctx.closePath(); fs(k.body, 2.5);
  ctx.beginPath(); ctx.moveTo(13, -3); ctx.lineTo(11, -17); ctx.lineTo(3, -9); ctx.closePath(); fs(k.body, 2.5);
  ell(0, 0, 15, 11.5); fs(k.body, 2.8);
  if (k.patchA) {
    ctx.save(); ell(0, 0, 15, 11.5); ctx.clip();
    ctx.fillStyle = k.patchA; ell(-10, -7, 9, 7); ctx.fill();
    ctx.fillStyle = k.patchB; ell(11, -8, 8, 6); ctx.fill();
    ctx.restore(); ell(0, 0, 15, 11.5); ctx.lineWidth = 2.8; ctx.strokeStyle = INK; ctx.stroke();
  }
  if (k.stripe) { ctx.strokeStyle = k.stripe; ctx.lineWidth = 2.4; ctx.beginPath(); ctx.moveTo(-4, -10); ctx.lineTo(-3.5, -6); ctx.moveTo(0, -11); ctx.lineTo(0, -7); ctx.moveTo(4, -10); ctx.lineTo(3.5, -6); ctx.stroke(); }
  if (k.muzzle) { ell(0, 4, 7, 5); ctx.fillStyle = k.muzzle; ctx.fill(); }
  const dark = kind === 'tuxedo' || kind === 'black';
  if (blink) {
    ctx.strokeStyle = dark ? '#fff' : INK; ctx.lineWidth = 2; ctx.beginPath(); ctx.moveTo(-8, -1); ctx.lineTo(-4, -1); ctx.moveTo(4, -1); ctx.lineTo(8, -1); ctx.stroke();
  } else if (k.eye) {
    circle(-6, -1, 2.6); fs(k.eye, 1.4); circle(6, -1, 2.6); fs(k.eye, 1.4);
  } else {
    ctx.fillStyle = dark ? '#fff' : INK; circle(-6, -1, 2.2); ctx.fill(); circle(6, -1, 2.2); ctx.fill();
  }
  ctx.strokeStyle = (kind === 'black') ? '#fff' : INK; ctx.lineWidth = 1.7;
  ctx.beginPath(); ctx.arc(-2, 3.5, 2, 0, Math.PI); ctx.stroke();
  ctx.beginPath(); ctx.arc(2, 3.5, 2, 0, Math.PI); ctx.stroke();
  ctx.restore();
}
function crate(x, y, w, h, o) {
  o = o || {};
  if (o.open) {
    ctx.beginPath(); ctx.moveTo(x - w / 2, y - h); ctx.lineTo(x - w / 2 - w * 0.16, y - h - h * 0.32); ctx.lineTo(x - w * 0.08, y - h - h * 0.26); ctx.lineTo(x - w * 0.04, y - h); ctx.closePath(); fs(CARD_L, 2.5);
    ctx.beginPath(); ctx.moveTo(x + w / 2, y - h); ctx.lineTo(x + w / 2 + w * 0.16, y - h - h * 0.32); ctx.lineTo(x + w * 0.08, y - h - h * 0.26); ctx.lineTo(x + w * 0.04, y - h); ctx.closePath(); fs(CARD_L, 2.5);
  }
  if (o.peek) catHead(x, y - h + 1, o.peekS || 1, o.peek, o.time || 0, o.seed || 0);
  rrect(x - w / 2, y - h, w, h, 4); fs(o.gold ? '#e8b75a' : CARD, 3);
  ctx.strokeStyle = CARD_D; ctx.lineWidth = 2; ctx.beginPath(); ctx.moveTo(x - w / 2 + 4, y - h + 6); ctx.lineTo(x + w / 2 - 4, y - h + 6); ctx.stroke();
  if (o.tape) { rrect(x - w * 0.08, y - h, w * 0.16, h * 0.5, 1); fs(TAPE, 2); }
  if (o.label) { rrect(x + w * 0.14, y - h * 0.48, w * 0.24, h * 0.24, 2); fs('#fff', 2); }
}
function crenels(x, y, w, n) {
  const cw = w / (n * 2 - 1);
  for (let i = 0; i < n; i++) { rrect(x - w / 2 + i * cw * 2, y - cw * 0.9, cw, cw * 0.9 + 2, 1.5); fs(CARD, 2.5); }
}
function flag(x, y, h, col) {
  ctx.beginPath(); ctx.moveTo(x, y); ctx.lineTo(x, y - h); ctx.lineWidth = 2.5; ctx.strokeStyle = INK; ctx.stroke();
  ctx.beginPath(); ctx.moveTo(x, y - h); ctx.lineTo(x + 14, y - h + 5); ctx.lineTo(x, y - h + 10); ctx.closePath(); fs(col, 2);
}
function roof(x, y, w, h) { ctx.beginPath(); ctx.moveTo(x - w / 2, y); ctx.lineTo(x, y - h); ctx.lineTo(x + w / 2, y); ctx.closePath(); fs(CARD_D, 3); }
function drawBox(t, time, seed) {
  ctx.lineJoin = 'round'; ctx.lineCap = 'round';
  const o = { time, seed };
  switch (t) {
    case 1: crate(0, 0, 34, 24, { open: true }); break;
    case 2: crate(0, 0, 46, 32, { tape: true, label: true }); break;
    case 3: crate(0, 0, 56, 38, { open: true, peek: 'cheese', peekS: 1, ...o }); break;
    case 4:
      crate(0, 0, 56, 38, {}); roof(0, -37, 70, 28);
      rrect(-12, -26, 24, 26, [10, 10, 0, 0]); fs(HOLE, 2.5);
      ctx.save(); rrect(-12, -26, 24, 26, [10, 10, 0, 0]); ctx.clip(); catHead(0, -10, 0.78, 'mackerel', time, seed); ctx.restore();
      break;
    case 5:
      crate(0, 0, 60, 32, { tape: true }); crate(0, -31, 48, 28, {}); roof(0, -58, 60, 22);
      circle(0, -45, 10); fs(HOLE, 2.5);
      ctx.save(); circle(0, -45, 10); ctx.clip(); catHead(0, -41, 0.62, 'tuxedo', time, seed); ctx.restore();
      break;
    case 6: case 8: {
      const gold = t === 8;
      crate(-27, 0, 18, 56, { gold }); crenels(-27, -56, 18, 2);
      crate(27, 0, 18, 56, { gold }); crenels(27, -56, 18, 2);
      crate(0, 0, 50, 40, { gold }); crenels(0, -40, 50, 4);
      rrect(-11, -24, 22, 24, [11, 11, 0, 0]); fs(HOLE, 2.5);
      ctx.save(); rrect(-11, -24, 22, 24, [11, 11, 0, 0]); ctx.clip(); catHead(0, -9, 0.72, 'calico', time, seed); ctx.restore();
      flag(27, -62, 22, gold ? BUTTER : TOMATO);
      if (gold) {
        catHead(-27, -66, 0.6, 'cheese', time, seed + 1);
        ctx.beginPath(); ctx.moveTo(-10, -50); ctx.lineTo(-10, -60); ctx.lineTo(-5, -55); ctx.lineTo(0, -63); ctx.lineTo(5, -55); ctx.lineTo(10, -60); ctx.lineTo(10, -50); ctx.closePath(); fs(BUTTER, 2.5);
      }
      break;
    }
    case 7:
      crate(0, 0, 52, 26, { tape: true }); crate(0, -26, 42, 24, { label: true }); crate(0, -50, 32, 22, {});
      flag(12, -72, 18, MINT);
      catHead(-4, -76, 0.7, 'black', time, seed);
      break;
  }
}


function drawObstacle(kind, cx, cy, cs) {
  const s = cs / 70;
  ctx.save(); ctx.translate(cx, cy); ctx.scale(s, s); ctx.lineJoin = 'round';
  shadow(24, 6, 22);
  if (kind === 1) { // cushion
    rrect(-25, -16, 50, 38, 14); fs('#7c9fe6', 3);
    ctx.fillStyle = '#9db7f0'; rrect(-18, -10, 36, 24, 10); ctx.fill();
    circle(0, 3, 2.4); ctx.fillStyle = INK; ctx.fill();
  } else { // plant pot
    ctx.fillStyle = MINT;
    for (const [x, y, r] of [[-10, -18, 11], [9, -20, 12], [0, -30, 11]]) { ell(x, y, r * 0.75, r, x * 0.03); fs(MINT, 2.6); }
    ctx.beginPath(); ctx.moveTo(-17, -6); ctx.lineTo(17, -6); ctx.lineTo(12, 22); ctx.lineTo(-12, 22); ctx.closePath(); fs('#d9774a', 3);
    rrect(-20, -10, 40, 9, 3); fs('#e88c5e', 3);
  }
  ctx.restore();
}
