// Serves the sprite export page and writes the PNGs it posts into the Unity project.
const http = require('http'), fs = require('fs'), path = require('path');
const root = __dirname;
const out = require('path').resolve(__dirname, '../../Assets/NyangPunch/Resources/Sprites');
fs.mkdirSync(out, { recursive: true });
http.createServer((q, s) => {
  if (q.method === 'POST') {
    let body = ''; q.on('data', c => body += c); q.on('end', () => {
      if (q.url === '/meta') fs.writeFileSync(path.join(out, 'sprite_meta.json'), body);
      else {
        const name = path.basename(decodeURIComponent(q.url.replace('/save/', '')));
        fs.writeFileSync(path.join(out, name + '.png'), Buffer.from(body.split(',')[1], 'base64'));
      }
      s.writeHead(200); s.end('ok');
    });
    return;
  }
  let p = q.url.split('?')[0]; if (p === '/') p = '/index.html';
  fs.readFile(path.join(root, path.normalize(p)), (e, d) => {
    if (e) { s.writeHead(404); return s.end(); }
    s.writeHead(200, { 'content-type': p.endsWith('.html') ? 'text/html; charset=utf-8' : 'text/javascript; charset=utf-8' }); s.end(d);
  });
}).listen(process.env.PORT || 8772);
