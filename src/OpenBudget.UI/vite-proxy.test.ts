// @vitest-environment node
import { createServer as createHttpServer, request } from 'node:http';
import type { AddressInfo } from 'node:net';
import { afterAll, beforeAll, expect, it } from 'vitest';
import { createServer, type ProxyOptions, type ViteDevServer } from 'vite';
import config from './vite.config';

// Exercise Vite's real proxy with a browser Origin header. API-only clients
// omit that header and therefore cannot catch a proxy-induced origin mismatch.
const api = createHttpServer((request, response) => {
  response.statusCode = request.headers.origin === `http://${request.headers.host}` ? 200 : 403;
  response.end();
});
let ui: ViteDevServer;
let uiUrl: string;

function login(host: string, origin: string): Promise<number> {
  return new Promise((resolve, reject) => {
    const req = request(`${uiUrl}/api/auth/login`, { method: 'POST', headers: { Host: host, Origin: origin } }, response => {
      response.resume();
      resolve(response.statusCode!);
    });
    req.on('error', reject);
    req.end();
  });
}

beforeAll(async () => {
  await new Promise<void>(resolve => api.listen(0, '127.0.0.1', resolve));
  const target = `http://127.0.0.1:${(api.address() as AddressInfo).port}`;
  const proxyOptions = config.server!.proxy!['/api'];
  // Preserve shorthand behavior too, so reverting the config reproduces the bug.
  const proxy = typeof proxyOptions === 'string' ? target : { ...proxyOptions as ProxyOptions, target };
  ui = await createServer({ configFile: false, server: { host: '127.0.0.1', port: 0, proxy: { '/api': proxy } } });
  await ui.listen();
  uiUrl = `http://127.0.0.1:${(ui.httpServer!.address() as AddressInfo).port}`;
});

afterAll(async () => {
  await ui?.close();
  await new Promise<void>((resolve, reject) => api.close(error => error ? reject(error) : resolve()));
});

it.each(['localhost:5173', '127.0.0.1:5173'])('preserves browser origin through the login proxy for %s', async host => {
  expect(await login(host, `http://${host}`)).toBe(200);
});

it('keeps foreign origins rejected by the upstream API', async () => {
  expect(await login('localhost:5173', 'https://attacker.example')).toBe(403);
});
