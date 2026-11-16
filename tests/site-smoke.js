const fs = require('node:fs');
const http = require('node:http');
const path = require('node:path');

const root = path.resolve(__dirname, '..');
const siteRoot = path.join(root, 'site');
const prefix = '/noel-christmas-screensaver/';
const expectedDownloads = [
  'https://github.com/cybereun/noel-christmas-screensaver/releases/latest/download/Noel.exe',
  'https://github.com/cybereun/noel-christmas-screensaver/releases/latest/download/Noel.scr',
  'https://github.com/cybereun/noel-christmas-screensaver/releases/latest/download/SHA256SUMS.txt',
];
const expectedRelease = 'https://github.com/cybereun/noel-christmas-screensaver/releases/latest';

function assert(condition, message) {
  if (!condition) throw new Error(message);
}

function request(port, requestPath) {
  return new Promise((resolve, reject) => {
    http.get({ host: '127.0.0.1', port, path: requestPath }, (response) => {
      const chunks = [];
      response.on('data', (chunk) => chunks.push(chunk));
      response.on('end', () => resolve({
        status: response.statusCode,
        contentType: response.headers['content-type'] || '',
        body: Buffer.concat(chunks),
      }));
    }).on('error', reject);
  });
}

async function main() {
  const indexPath = path.join(siteRoot, 'index.html');
  const html = fs.readFileSync(indexPath, 'utf8');
  assert(/<html\b[^>]*\blang="ko"/i.test(html), 'The page must declare Korean as its language.');
  assert(/<h1\b/i.test(html), 'The page needs one visible primary headline.');
  for (const url of expectedDownloads) {
    assert(html.includes(url), `The page must expose the latest release asset: ${url}`);
  }
  assert(html.includes(expectedRelease), 'The page must link to release notes and the complete release file list.');
  assert(html.includes('버전 v1.0.0'), 'The download section must identify the shipped version.');
  assert(html.includes('다시 시작할 때 잠금'), 'The Windows guide must explain where to configure lock preferences.');

  const imageTags = Array.from(html.matchAll(/<img\b[^>]*>/gi), (match) => match[0]);
  for (let scene = 1; scene <= 5; scene += 1) {
    const filename = `theme-${scene}.png`;
    const tag = imageTags.find((candidate) => candidate.includes(`assets/${filename}`));
    assert(tag, `The page must show ${filename}.`);
    assert(/\balt="[^"\s][^"]*"/i.test(tag), `${filename} needs descriptive alt text.`);
    assert(fs.existsSync(path.join(siteRoot, 'assets', filename)), `${filename} must exist under site/assets/.`);
  }
  assert(fs.existsSync(path.join(siteRoot, 'assets', 'favicon.ico')), 'The site favicon is missing.');

  const stylesheetMatch = html.match(/<link\b[^>]*rel="stylesheet"[^>]*href="([^"]+)"/i);
  assert(stylesheetMatch, 'The page must load a local stylesheet.');
  const css = fs.readFileSync(path.join(siteRoot, stylesheetMatch[1]), 'utf8');
  assert(/:focus-visible\b/.test(css), 'Keyboard focus must be visible.');
  assert(/prefers-reduced-motion:\s*reduce/i.test(css), 'The page must respect reduced-motion preferences.');
  const heroShades = Array.from(css.matchAll(/\.hero-shade\s*\{[^}]*\}/gi), (match) => match[0]);
  assert(heroShades.length >= 2 && /rgba\(7,\s*20,\s*22,\s*1\) 0%, rgba\(7,\s*20,\s*22,\s*1\) 42%/i.test(heroShades[0]), 'The desktop hero must mask the artwork lettering beneath the Korean headline.');
  assert(/linear-gradient\(90deg,\s*rgba\(7,\s*20,\s*22,\s*1\) 0%,\s*rgba\(7,\s*20,\s*22,\s*1\) 12%/i.test(heroShades[1]), 'The mobile hero must mask poster lettering near the narrow viewport edge.');

  const server = http.createServer((req, res) => {
    let requestPath;
    try { requestPath = decodeURIComponent(new URL(req.url, 'http://localhost').pathname); }
    catch { res.writeHead(400).end(); return; }
    if (!requestPath.startsWith(prefix)) { res.writeHead(404).end(); return; }
    let relative = requestPath.slice(prefix.length);
    if (!relative || relative.endsWith('/')) relative += 'index.html';
    const file = path.resolve(siteRoot, relative);
    if (!file.startsWith(siteRoot + path.sep) || !fs.existsSync(file)) { res.writeHead(404).end(); return; }
    const contentType = file.endsWith('.css') ? 'text/css; charset=utf-8'
      : file.endsWith('.png') ? 'image/png'
        : file.endsWith('.ico') ? 'image/x-icon'
          : 'text/html; charset=utf-8';
    res.writeHead(200, { 'Content-Type': contentType });
    res.end(fs.readFileSync(file));
  });
  await new Promise((resolve) => server.listen(0, '127.0.0.1', resolve));
  try {
    const port = server.address().port;
    const page = await request(port, `${prefix}`);
    assert(page.status === 200 && page.body.toString('utf8').includes('<h1'), 'The project page must serve at its repository path.');
    for (let scene = 1; scene <= 5; scene += 1) {
      const image = await request(port, `${prefix}assets/theme-${scene}.png`);
      assert(image.status === 200 && image.contentType.startsWith('image/png'), `theme-${scene}.png must load below the repository path.`);
      assert(image.body.length > 1000, `theme-${scene}.png must not be empty.`);
    }
    const favicon = await request(port, `${prefix}assets/favicon.ico`);
    assert(favicon.status === 200 && favicon.body.length > 100, 'The favicon must load below the repository path.');
    console.log('PASS: Korean page, legible responsive hero, v1.0.0/release overview, three latest-release downloads, Windows lock guidance, five accessible scenes, favicon, keyboard focus, reduced motion, and project-path asset serving.');
  } finally {
    await new Promise((resolve, reject) => server.close((error) => error ? reject(error) : resolve()));
  }
}

main().catch((error) => {
  console.error(error.message);
  process.exitCode = 1;
});
