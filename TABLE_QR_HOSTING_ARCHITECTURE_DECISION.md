# TABLE QR Production Hosting Architecture Decision Audit

## 1. Verified Frontend Runtime
- **Type**: SSR (Server-Side Rendered) Application. The presence of `@tanstack/react-start`, a custom `server.ts` exporting a `fetch` handler, and `nitro` confirms this is not a purely static application.
- **Build Command**: `npm run build` (which executes `vite build`).
- **Start Command**: A production `start` script is crucially **missing** from `package.json`.
- **Output Directory**: By default, Nitro/TanStack Start outputs to `.output/server/index.mjs` (or similar).
- **Node.js Requirement**: Yes, the production build requires a server runtime (Node.js or a JS worker environment) to handle the SSR lifecycle and API requests.
- **VITE_API_URL**: It is accessed via `import.meta.env.VITE_API_URL` in `src/services/apiClient.ts`. Vite statically replaces `import.meta.env` at **build time**. Therefore, the backend URL must be known and injected during the CI/CD build step.
- **URL Construction**: Base URL is parsed; SignalR URLs (`/hubs/order`) are dynamically constructed via regex replacing `/api/?$` on the `API_BASE_URL`.
- **Hardcoded URLs**: `http://localhost:5166/api` is heavily hardcoded as the fallback in `apiClient.ts`.
- **Cloudflare Impact**: The `@cloudflare/vite-plugin` and `server.ts` export signature (`fetch(request, env, ctx)`) indicates this app is heavily optimized for a Cloudflare Pages/Workers target. Running this natively via Node on Ubuntu requires ensuring Nitro builds a compatible Node server output rather than a Cloudflare Worker output.

## 2. Verified Backend Runtime
- **Framework**: .NET 8 ASP.NET Core API. Requires the .NET 8 runtime to execute.
- **Binding**: Uses default ASP.NET Core binding (typically Kestrel on port 8080/5000).
- **Environment Variables**: Essential configurations (`Jwt:Key`, `ConnectionStrings:DefaultConnection`, `SuperAdmin:Password`, `AllowedOrigins`) must be supplied.
- **Routing**: API requires `/api/*`. SignalR requires `/hubs/order` with WebSocket upgrade capability.
- **Systemd**: The compiled .NET binary can securely and easily run under a systemd daemon on Linux.
- **Migration & Seeding**: The app forcefully runs `context.Database.Migrate()` on startup. The seeding can be disabled via the `SeedDummyData: false` configuration key.
- **Persistent Storage**: If relying on `"ImageStorage": "Local"`, the deployment **must** mount a persistent volume at the `wwwroot` directory; otherwise, ephemeral containers or Lightsail instances will lose all uploaded menu images on every redeploy.

## 3. Hosting Options Comparison

### A. Single Ubuntu AWS Lightsail Instance
- **Compatibility**: High. You can run Nginx (Proxy), .NET API (Systemd), and Node SSR (PM2/Systemd) concurrently on one machine.
- **Required Services**: Nginx, .NET 8 Runtime, Node.js, PM2/Systemd, PostgreSQL (if hosted locally, though external RDS is safer).
- **Complexity**: Moderate. Requires manual Linux administration and proxy setup.
- **Persistent Storage**: Solves the `Local` storage problem cleanly since disk is inherently persistent.
- **Verdict**: A good fit if the budget is highly constrained, but manual setup is required for the SSR Node server.

### B. AWS Lightsail / EC2 Backend + Separately Hosted Frontend (e.g., Cloudflare Pages)
- **Compatibility**: Excellent. The frontend is visibly optimized for Cloudflare via `@cloudflare/vite-plugin` and the `server.ts` fetch signature.
- **Complexity**: Low. Cloudflare handles the SSR edge deployment perfectly out-of-the-box. The EC2 instance merely hosts the robust .NET API.
- **Persistent Storage**: To decouple the frontend and backend efficiently, migrating images to AWS S3 (already implemented via `S3ImageStorageService`) is strongly advised to keep the API server stateless.
- **Verdict**: Optimal architecture for this specific codebase.

### C. AWS Container-Based Hosting (ECS / Fargate)
- **Compatibility**: High. 
- **Complexity**: Very High. Requires Dockerizing both the .NET API and the Nitro Node SSR build.
- **Persistent Storage**: Necessitates fully abandoning `Local` storage in favor of S3, as Fargate tasks are entirely ephemeral.

## 4. Database and Image-Storage Dependencies
- **PostgreSQL**: The .NET API requires a running Postgres instance. A managed RDS or Lightsail Managed Database is strongly recommended to handle automated backups.
- **S3 Migration**: The codebase is 100% prepared to switch to S3. Existing paths map cleanly to S3 keys. S3 removes the critical disk-persistence dependency from the compute tier.
- **S3 Bucket**: Needs to be Private. Needs an IAM policy granting `s3:PutObject`, `s3:GetObject`, and `s3:DeleteObject` to the backend.

## 5. Required Production Configuration
- Backend: `Jwt:Key`, `Jwt:Issuer`, `Jwt:Audience`, `SuperAdmin:Password`, `ConnectionStrings:DefaultConnection`, `AllowedOrigins`, `SeedDummyData=false`, `ImageStorage:Provider=S3`, `ImageStorage:S3:BucketName`.
- Frontend: `VITE_API_URL` (injected explicitly during the build step).

## 6. Risks and Unresolved Questions
- **Node Server Adapter**: If deploying the frontend to Ubuntu (Option A), does the current Vite/Nitro configuration cleanly output a Node.js listener, or is it hard-locked to Cloudflare's `fetch` signature? (This requires user confirmation or a test build to inspect `.output`).
- **Start Script**: A `start` script must be added to `package.json` to define how the production build is spun up.

## 7. Recommended Architecture
**Architecture B is highly recommended.**

1. **Frontend**: Deploy to **Cloudflare Pages**. The codebase (`@cloudflare/vite-plugin`, `server.ts` fetch wrapper) is heavily tailored for this.
2. **Backend**: Deploy to **AWS Lightsail (Ubuntu)** or **AWS App Runner** running the .NET 8 API.
3. **Database**: Use a **Managed PostgreSQL** instance (RDS or Lightsail DB) for backup safety.
4. **Storage**: Finalize the switch to **AWS S3** (`S3ImageStorageService`) to make the backend entirely stateless, removing the risk of losing menu images on backend redeployments.

*Disclaimer: No code, AWS resources, databases, or deployment environments were changed or provisioned during this audit.*

**Final Status**: `ARCHITECTURE READY FOR USER DECISION`
