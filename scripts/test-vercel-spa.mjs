import http from 'node:http';
import fs from 'node:fs';
import path from 'node:path';

const distDir = path.resolve('tablo/dist');

if (!fs.existsSync(distDir)) {
  console.error('dist directory does not exist. Run npm run build first.');
  process.exit(1);
}

const mimeTypes = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'application/javascript',
  '.css': 'text/css',
  '.json': 'application/json',
  '.png': 'image/png',
  '.jpg': 'image/jpeg',
  '.jpeg': 'image/jpeg',
  '.svg': 'image/svg+xml',
  '.ico': 'image/x-icon',
};

const server = http.createServer((req, res) => {
  const urlPath = req.url.split('?')[0];
  const filePath = path.join(distDir, urlPath);

  // If exact file exists and is not a directory, serve it
  if (fs.existsSync(filePath) && fs.statSync(filePath).isFile()) {
    const ext = path.extname(filePath).toLowerCase();
    const contentType = mimeTypes[ext] || 'application/octet-stream';
    const headers = { 'Content-Type': contentType };
    if (urlPath.startsWith('/assets/')) {
      headers['Cache-Control'] = 'public, max-age=31536000, immutable';
    }
    res.writeHead(200, headers);
    fs.createReadStream(filePath).pipe(res);
    return;
  }

  // Otherwise, SPA rewrite to /index.html (as defined in vercel.json rewrites)
  const indexPath = path.join(distDir, 'index.html');
  res.writeHead(200, { 'Content-Type': 'text/html; charset=utf-8' });
  fs.createReadStream(indexPath).pipe(res);
});

const PORT = 5055;
server.listen(PORT, async () => {
  console.log(`Vercel-style static server running on http://127.0.0.1:${PORT}`);
  
  const testRoutes = [
    { path: '/', expectHtml: true },
    { path: '/order', expectHtml: true },
    { path: '/admin', expectHtml: true },
    { path: '/dashboard', expectHtml: true },
    { path: '/t/qd4x8f2tp', expectHtml: true },
    { path: '/inventory', expectHtml: true },
  ];

  let allPass = true;

  for (const t of testRoutes) {
    try {
      const resp = await fetch(`http://127.0.0.1:${PORT}${t.path}`);
      const text = await resp.text();
      const statusOk = resp.status === 200;
      const isHtml = text.includes('<div id="app">') || text.includes('<!doctype html>') || text.includes('<!DOCTYPE html>');
      if (statusOk && isHtml) {
        console.log(`✔ [SPA Route] ${t.path} -> 200 OK (rewritten to index.html)`);
      } else {
        console.error(`✖ [SPA Route] ${t.path} -> Status: ${resp.status}, isHtml: ${isHtml}`);
        allPass = false;
      }
    } catch (err) {
      console.error(`✖ [SPA Route] ${t.path} fetch failed:`, err.message);
      allPass = false;
    }
  }

  // Test static asset
  const assets = fs.readdirSync(path.join(distDir, 'assets'));
  const jsAsset = assets.find(f => f.endsWith('.js'));
  if (jsAsset) {
    try {
      const assetUrl = `http://127.0.0.1:${PORT}/assets/${jsAsset}`;
      const resp = await fetch(assetUrl);
      const isJs = resp.headers.get('content-type')?.includes('javascript');
      const cacheControl = resp.headers.get('cache-control');
      if (resp.status === 200 && isJs && cacheControl?.includes('immutable')) {
        console.log(`✔ [Static Asset] /assets/${jsAsset} -> 200 OK (${resp.headers.get('content-type')}), Cache-Control: ${cacheControl}`);
      } else {
        console.error(`✖ [Static Asset] /assets/${jsAsset} failed: Status: ${resp.status}, Content-Type: ${resp.headers.get('content-type')}`);
        allPass = false;
      }
    } catch (err) {
      console.error(`✖ [Static Asset] test failed:`, err.message);
      allPass = false;
    }
  }

  server.close();
  if (!allPass) {
    process.exit(1);
  }
  console.log('All Vercel SPA routing tests passed successfully!');
});
