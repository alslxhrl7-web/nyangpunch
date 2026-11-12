// Serves promo.html and saves the PNGs it posts into game/pub/promo.
const http = require('http'), fs = require('fs'), path = require('path');
const root = __dirname, out = path.join(__dirname, 'images');
fs.mkdirSync(out, { recursive: true });
const types = { '.html': 'text/html; charset=utf-8', '.js': 'text/javascript; charset=utf-8', '.ttf': 'font/ttf' };
http.createServer((q, s) => {
  if (q.method === 'POST') {
    let body = ''; q.on('data', c => body += c); q.on('end', () => {
      const name = path.basename(decodeURIComponent(q.url.replace('/save/', '')));
      fs.writeFileSync(path.join(out, name + '.png'), Buffer.from(body.split(',')[1], 'base64'));
      s.writeHead(200); s.end('ok');
    });
    return;
  }
  let p = q.url.split('?')[0]; if (p === '/') p = '/promo.html';
  fs.readFile(path.join(root, path.normalize(p)), (e, d) => {
    if (e) { s.writeHead(404); return s.end(); }
    s.writeHead(200, { 'content-type': types[path.extname(p)] || 'application/octet-stream' }); s.end(d);
  });
}).listen(process.env.PORT || 8774);
