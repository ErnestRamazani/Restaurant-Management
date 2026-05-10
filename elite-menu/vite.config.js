import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import path from 'node:path'
import { fileURLToPath } from 'node:url'

const __dirname = path.dirname(fileURLToPath(import.meta.url))

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  base: '/',
  resolve: {
    alias: {
      '@repo-assets': path.resolve(__dirname, '../assets'),
    },
  },
  build: {
    outDir: path.resolve(__dirname, '../EliteRestaurant.Api/wwwroot'),
    emptyOutDir: false,
  },
  server: {
    host: true,
    port: 5173,
    // Browser opens to the app once the server is ready.
    open: true,
    proxy: {
      '/api': {
        target: 'http://localhost:8080',
        changeOrigin: true,
      },
      '/hubs': {
        target: 'http://localhost:8080',
        ws: true,
        changeOrigin: true,
      },
    },
  },
})
