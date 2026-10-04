import { resolve } from "node:path";
import { defineConfig } from "vite";

export default defineConfig({
  build: {
    // O SheetJS (~500 kB) já é carregado sob demanda, só na tela de importação.
    chunkSizeWarningLimit: 600,
    rollupOptions: {
      input: {
        main: resolve(import.meta.dirname, "index.html"),
        login: resolve(import.meta.dirname, "login.html"),
      },
    },
  },
  server: {
    port: 5173,
    // Em desenvolvimento a API roda em http://localhost:5080; o proxy evita CORS.
    proxy: { "/api": "http://localhost:5080" },
  },
});
