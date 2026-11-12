// Static server for the sound comparison page: node Tools/sound/serve.js  ->  http://localhost:8770/
const http = require('http'), fs = require('fs'), path = require('path');
const ROOT = path.resolve(__dirname, '..', '..');           // NyangPunch project root (so the Jua font path works)
const TYPES = { '.html': 'text/html; charset=utf-8', '.mp4': 'video/mp4', '.mp3': 'audio/mpeg', '.wav': 'audio/wav', '.ttf': 'font/ttf', '.json': 'application/json', '.js': 'text/javascript' };
const CORS = { 'Access-Control-Allow-Origin': 'https://sound.varco.ai', 'Access-Control-Allow-Methods': 'POST, OPTIONS', 'Access-Control-Allow-Headers': 'Content-Type', 'Access-Control-Allow-Private-Network': 'true' };
http.createServer((req, res) => {
  let p = decodeURIComponent(req.url.split('?')[0]);
  // POST /save/<key>_<n>.mp3 (or manifest json) from the Varco Sound page -> Tools/sound/varco/
  if (req.method === 'OPTIONS') { res.writeHead(204, CORS); return res.end(); }
  // POST /grab  [{name, url}] -> the server downloads each file (Varco's S3 storage only) into Tools/sound/varco/
  if (req.method === 'POST' && p === '/grab') {
    const chunks = []; req.on('data', c => chunks.push(c));
    req.on('end', async () => {
      const https = require('https'); const out = [];
      for (const { name, url } of JSON.parse(Buffer.concat(chunks).toString())) {
        if (!/^[a-z]+_\d+\.mp3$/.test(name) || !/^https:\/\/[\w.-]+\.amazonaws\.com\//.test(url)) { out.push(name + ': rejected'); continue; }
        const size = await new Promise(ok => https.get(url, r => {
          if (r.statusCode !== 200) { r.resume(); return ok('HTTP ' + r.statusCode); }
          const bufs = []; r.on('data', d => bufs.push(d)); r.on('end', () => { const b = Buffer.concat(bufs); fs.writeFileSync(path.join(__dirname, 'varco', name), b); ok(b.length); });
        }).on('error', e => ok('ERR ' + e.message)));
        out.push(name + ': ' + size);
      }
      res.writeHead(200, { 'Content-Type': 'text/plain; charset=utf-8' }); res.end(out.join('\n'));
    });
    return;
  }
  // POST /cut/<name>.wav -> Tools/sound/cut/ ; POST /sfx/<key>.wav -> the game's Resources/Sfx/ (from cut.html)
  const wm = req.method === 'POST' && p.match(/^\/(cut|sfx)\/([a-z_]+(?:_\d+)?\.wav)$/);
  if (wm) {
    const dir = wm[1] === 'cut' ? path.join(__dirname, 'cut') : path.join(ROOT, 'Assets', 'NyangPunch', 'Resources', 'Sfx');
    fs.mkdirSync(dir, { recursive: true });
    const chunks = []; req.on('data', c => chunks.push(c));
    req.on('end', () => { fs.writeFileSync(path.join(dir, wm[2]), Buffer.concat(chunks)); res.writeHead(200); res.end('ok'); });
    return;
  }
  if (req.method === 'POST') {
    const m = p.match(/^\/save\/([a-z]+_\d+\.mp3|manifest[\w-]*\.json)$/);
    if (!m) { res.writeHead(400, CORS); return res.end('bad name'); }
    const chunks = []; req.on('data', c => chunks.push(c));
    req.on('end', () => { fs.writeFileSync(path.join(__dirname, 'varco', m[1]), Buffer.concat(chunks)); res.writeHead(200, CORS); res.end('ok'); });
    return;
  }
  if (p === '/') { res.writeHead(302, { Location: '/Tools/sound/index.html' }); return res.end(); }
  const file = path.join(ROOT, p);
  if (!file.startsWith(ROOT) || !fs.existsSync(file) || fs.statSync(file).isDirectory()) { res.writeHead(404); return res.end('not found'); }
  const type = TYPES[path.extname(file)] || 'application/octet-stream', size = fs.statSync(file).size;
  const range = /bytes=(\d*)-(\d*)/.exec(req.headers.range || '');
  if (range) {   // byte ranges so video/audio elements can seek
    const start = range[1] ? +range[1] : 0, end = range[2] ? Math.min(+range[2], size - 1) : size - 1;
    res.writeHead(206, { 'Content-Type': type, 'Content-Range': `bytes ${start}-${end}/${size}`, 'Accept-Ranges': 'bytes', 'Content-Length': end - start + 1, ...CORS });
    return fs.createReadStream(file, { start, end }).pipe(res);
  }
  res.writeHead(200, { 'Content-Type': type, 'Content-Length': size, 'Accept-Ranges': 'bytes', ...CORS });
  fs.createReadStream(file).pipe(res);
}).listen(8770, () => console.log('http://localhost:8770/'));
