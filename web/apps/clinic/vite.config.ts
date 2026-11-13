import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { VitePWA } from 'vite-plugin-pwa';

export default defineConfig({
  plugins: [
    react(),
    VitePWA({
      registerType: 'prompt',
      includeAssets: ['favicon.svg', 'icons/*.png'],
      manifest: {
        name: 'منصة إدارة العيادات المتكاملة',
        short_name: 'العيادات',
        description: 'إدارة العيادات والمواعيد والملفات السريرية والفوترة دون اتصال',
        theme_color: '#0f172a',
        background_color: '#f8fafc',
        display: 'standalone',
        dir: 'rtl',
        lang: 'ar',
        start_url: '/',
        icons: [
          {
            src: '/icons/icon-192.png',
            sizes: '192x192',
            type: 'image/png',
          },
          {
            src: '/icons/icon-512.png',
            sizes: '512x512',
            type: 'image/png',
          },
        ],
      },
      workbox: {
        globPatterns: ['**/*.{js,css,html,ico,png,svg,woff2}'],
        navigateFallback: '/index.html',
        // Explicitly never cache or serve stale service-worker shell for Patient Portal routes or APIs
        navigateFallbackDenylist: [/^\/c\/[^/]+\/portal/i, /^\/api\//i],
        runtimeCaching: [
          {
            urlPattern: /^\/api\/.*\/portal(\/|$)/i,
            handler: 'NetworkOnly',
          },
          {
            urlPattern: /^\/c\/[^/]+\/portal(\/|$)/i,
            handler: 'NetworkOnly',
          },
          {
            urlPattern: /^\/api\/v1\/clinics\/[^/]+\/(patients|scheduling|clinical|billing|sync)(\/|$)/i,
            handler: 'NetworkFirst',
            options: {
              cacheName: 'clinic-api-cache',
              networkTimeoutSeconds: 3,
            },
          },
        ],
      },
    }),
  ],
  server: {
    port: 3000,
    proxy: {
      '/api': {
        target: 'http://localhost:5000',
        changeOrigin: true,
      },
    },
  },
});
