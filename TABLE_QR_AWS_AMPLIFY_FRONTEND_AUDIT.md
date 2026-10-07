# TABLE QR AWS Amplify Frontend Compatibility Audit

## 1. Executive Summary
The TABLE QR frontend is a Server-Side Rendered (SSR) application built on React 19, Vite, and TanStack Start (powered by Nitro). The current repository is heavily optimized—and partially hardcoded via a proprietary configuration package—for deployment to **Cloudflare**. Deploying this exact codebase to AWS Amplify SSR is possible because Nitro supports AWS Amplify via the `aws-amplify` preset. However, doing so requires forcefully overriding the current Cloudflare-focused configuration and server entry point.

## 2. Current Frontend Architecture
- **Versions**: React 19.2.0, Vite 7.3.1, TanStack Start 1.167.50, Nitro 3.0.260603-beta.
- **Plugins**: Uses `@lovable.dev/vite-tanstack-config` (Version 2.7.1) which internally forces a `cloudflare` build plugin.
- **Architecture**: SSR (Server-Side Rendered).
- **Build Command**: `npm run build` (executes `vite build`). Outputs to `.output/server` and `.output/public`.
- **Start Script**: None exists. A standard Node production environment would lack a `npm run start` script.
- **Current Target Runtime**: Cloudflare Workers / Pages. The `src/server.ts` explicitly exports a Cloudflare `fetch(request, env, ctx)` signature. The build outputs `.wrangler/deploy/config.json` and a `nitro.json` configured for Cloudflare.

## 3. Amplify Compatibility Findings
- **Build Output Compatibility**: The current `.output` is compiled for Cloudflare. AWS Amplify SSR requires Nitro to build using the `aws-amplify` preset to generate a compatible Lambda handler / Amplify hosting structure.
- **Adapter Replacement**: The `@cloudflare/vite-plugin` and Cloudflare Nitro preset must be conditionally disabled or explicitly overridden in `vite.config.ts` for AWS deployments.
- **Build Specification**: An `amplify.yml` file must be added to instruct Amplify to run `npm run build` and point its `baseDirectory` to the `.output` folder (Amplify expects Next.js/Nitro artifacts explicitly mapped).

## 4. API URL and SignalR Findings
- **API URL**: Configured in `src/services/apiClient.ts` as `import.meta.env.VITE_API_URL || "http://localhost:5166/api"`.
- **Injection**: Because Vite embeds `import.meta.env` at build-time, `VITE_API_URL` **must** be configured as an environment variable in the AWS Amplify Console so it is baked into the SSR bundle during deployment.
- **SignalR Hub**: Constructed dynamically using `API_BASE_URL.replace(/\/api\/?$/, "/hubs/order")`. 
- **Code Changes Needed**: None. The frontend codebase will perfectly route to the Lightsail API and SignalR hub strictly by supplying the `VITE_API_URL` environment variable.

## 5. CORS, Authentication, and Image URL Findings
- **CORS**: The backend (`appsettings.json`) `AllowedOrigins` must be updated to include the generated AWS Amplify URL (e.g., `https://main.xxxxxxx.amplifyapp.com`).
- **Auth**: Uses JWT Bearer tokens retrieved from `localStorage.getItem("tablo_token")`.
- **SignalR Websockets**: The backend correctly reads the token from the query string `?access_token=` during the WebSocket handshake (`Program.cs:64`), ensuring AWS Amplify clients can connect seamlessly to Lightsail.
- **Image URLs**: The `api.ts` `getFullImageUrl` helper correctly parses absolute presigned URLs (starting with `http`). No code changes are needed for the S3 integration.
- **Image Expiry Risk**: The S3 integration uses 7-day presigned URLs. TanStack Query aggressively refetches on window focus, meaning expired URLs cached locally in the browser will quickly refresh. This risk is effectively negligible.

## 6. Build Validation
A safe, non-destructive `npm run build` was executed locally:
- **Command**: `npm run build`
- **Exit Code**: 0 (Success)
- **Output Directory**: `.output/server` and `.output/public`
- **Observations**: The build explicitly warns `[nitro] WARN [cloudflare] Wrangler config main is overridden and will be ignored` and generates `.wrangler` deployment files, confirming the deep Cloudflare coupling.

## 7. Minimum Required Changes
**Confirmed Required Changes (Configuration):**
- **File**: AWS Amplify Console (Environment Variables)
  - **Change**: Add `VITE_API_URL=https://your-lightsail-domain.com/api`.
- **File**: `Tablo.Api/appsettings.Production.json` (Backend)
  - **Change**: Add the Amplify domain to `AllowedOrigins`.
- **File**: `amplify.yml` (New File)
  - **Change**: Create a build specification mapping Nitro's `.output` to Amplify's expected artifact paths.

**Potential Required Changes (Code/Build overrides):**
- **File**: `tablo/vite.config.ts`
  - **Change**: Explicitly pass `{ nitro: { preset: "aws-amplify" } }` to override the Lovable Cloudflare default.
- **File**: `tablo/src/server.ts`
  - **Change**: Modify the export signature if the `aws-amplify` Nitro adapter fails to consume the Cloudflare-specific `fetch(request, env, ctx)` handler.

## 8. Risks and Unknowns
- The proprietary `@lovable.dev/vite-tanstack-config` plugin aggressively configures Cloudflare. It is unknown if overriding the Nitro preset via `vite.config.ts` will cleanly disable all Cloudflare edge integrations or if it will conflict and break the build.
- SignalR requires long-lived WebSockets. While AWS Amplify supports WebSockets on its SSR edge nodes, testing is required to ensure Amplify does not aggressively terminate idle SignalR connections.

## 9. Recommended Next Step
Create a testing branch to override the Nitro preset to `aws-amplify` in `vite.config.ts`, build locally, and deploy to a sandbox AWS Amplify environment to verify the handler compatibility before fully committing to Amplify Hosting over Cloudflare Pages.

**AMPLIFY REQUIRES FRONTEND BUILD/ADAPTER CHANGES**
