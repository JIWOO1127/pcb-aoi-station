import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

const repoRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const configPath = path.join(repoRoot, 'service-config.properties');
const serviceConfig = Object.fromEntries(
  fs.readFileSync(configPath, 'utf8')
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter((line) => line && !line.startsWith('#'))
    .map((line) => {
      const separator = line.indexOf('=');
      return [line.slice(0, separator).trim(), line.slice(separator + 1).trim()];
    }),
);

const mesApiBaseUrl = `http://${serviceConfig['mes.host']}:${serviceConfig['mes.port']}/api`;

export default defineConfig({
  plugins: [react()],
  define: {
    __MES_API_BASE_URL__: JSON.stringify(mesApiBaseUrl),
  },
  server: {
    host: serviceConfig['web.host'],
    port: Number(serviceConfig['web.port']),
  },
});
