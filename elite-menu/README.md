# React + Vite

After clone or cleanup, run `npm install` (or `npm ci` if `package-lock.json` is present) before `npm run dev`.

## Dev (Windows)

From the repo: start the **API** in one terminal (`.\EliteRestaurant.Api\run-dev.ps1`) and the **customer menu** in another. Use **`.\elite-menu\run-dev.ps1`** (the `.\` is required) or, from the repo root, **`.\run-elite-menu-dev.ps1`**. That opens a **new** PowerShell for Vite; this one stays free. In the current window only: add **`-Foreground`**. Vite is set to open your browser to the app when the server is ready. URL: `http://localhost:5173/menu/` (proxies `/api` to the API on `:5223`).

`npm run dev` alone runs Vite in the **same** window and does not open a second console (but the browser will still open, thanks to Vite’s `server.open` setting).

---

This template provides a minimal setup to get React working in Vite with HMR and some ESLint rules.

Currently, two official plugins are available:

- [@vitejs/plugin-react](https://github.com/vitejs/vite-plugin-react/blob/main/packages/plugin-react) uses [Oxc](https://oxc.rs)
- [@vitejs/plugin-react-swc](https://github.com/vitejs/vite-plugin-react/blob/main/packages/plugin-react-swc) uses [SWC](https://swc.rs/)

## React Compiler

The React Compiler is not enabled on this template because of its impact on dev & build performances. To add it, see [this documentation](https://react.dev/learn/react-compiler/installation).

## Expanding the ESLint configuration

If you are developing a production application, we recommend using TypeScript with type-aware lint rules enabled. Check out the [TS template](https://github.com/vitejs/vite/tree/main/packages/create-vite/template-react-ts) for information on how to integrate TypeScript and [`typescript-eslint`](https://typescript-eslint.io) in your project.
