# TABLE QR Production Readiness Audit

## 1. Executive Summary
The TABLE QR repository contains a functional .NET 8 backend API with PostgreSQL and a React/Vite/TanStack frontend. While functionally complete, the application possesses several hardcoded development conveniences (auto-migration, dummy data seeding, default admin credentials) that **must** be overridden for a safe production deployment. The Image-storage subsystem cleanly defaults to Local storage and is structurally ready for an S3 transition without requiring database changes.

## 2. Runtime Requirements
### Backend
- **Framework**: .NET 8 (`net8.0`) ASP.NET Core API.
- **Database**: PostgreSQL (requires `Npgsql.EntityFrameworkCore.PostgreSQL`).
- **Startup**: `dotnet run` (or deployed compiled binary). Auto-migrates and seeds database on start.
- **WebSocket**: SignalR enabled (`app.MapHub<OrderHub>("/hubs/order")`).

### Frontend
- **Framework**: React 19, Vite, TanStack Router, TanStack Start (SSR/CSR configuration).
- **Build**: `npm run build` (`vite build`). Note: Integrates `@cloudflare/vite-plugin`, suggesting readiness for edge-rendering or Cloudflare Pages, though standard Node hosting works.
- **Environment**: Must supply `VITE_API_URL` during build/runtime to route correctly (`src/services/apiClient.ts`).

## 3. Required Production Configuration Keys
The following keys **must** be provided securely (e.g., Environment Variables or AWS Secrets Manager):
- `ConnectionStrings:DefaultConnection` (PostgreSQL)
- `Jwt:Key` (Will crash application on startup if missing: `Program.cs:38`)
- `Jwt:Issuer`
- `Jwt:Audience`
- `SuperAdmin:Username`
- `SuperAdmin:Password`
- `AllowedOrigins` (Array of frontend domains for CORS)
- `SeedDummyData` (Should be `false` in production)

## 4. Database and Migration Considerations
- **Auto-Migration**: `Program.cs` automatically invokes `context.Database.Migrate()` on startup. While convenient, running this on multi-instance horizontal scaling can cause race conditions.
- **Auto-Seeding**: The `SeedDummyData` config defaults to `true` and injects dummy tables (`Table 11-20`) and sample restaurants. This **must** be explicitly disabled in production.

## 5. Image-Storage / S3 Readiness
- **Current State**: Defaults to `"Provider": "Local"`.
- **S3 Configuration Keys**: `ImageStorage:Provider`, `ImageStorage:S3:BucketName`, `ImageStorage:S3:Region`.
- **Migration**: Existing DB records (`/images/menu/xyz`) remain 100% compatible. Moving to S3 only requires an AWS CLI sync (`aws s3 sync wwwroot/images/menu/ s3://bucket/images/menu/`).
- **AWS APIs**: Credentials are safely pulled from the deployment environment via `AddDefaultAWSOptions()`.
- **Presigned URLs**: S3 generates 7-day expiration URLs automatically without persisting them to Postgres.

## 6. Hosting and Reverse Proxy Requirements
- **Backend Routing**: Traffic to `/api/*` must route to the .NET Kestrel server.
- **WebSocket Routing**: Traffic to `/hubs/order` requires `Connection: Upgrade` and `Upgrade: websocket` headers configured in the proxy (e.g., Nginx, ALB).
- **Static Assets**: If running in `Local` storage mode, images are served from `wwwroot/images/` automatically by `app.UseStaticFiles()`.
- **Frontend Routing**: Must serve the compiled Vite assets or run the SSR Node server depending on the chosen hosting target.

## 7. Security and Operational Checklist
- [x] **Rate Limiting**: Configured (`30 requests / min` for `PublicApi`).
- [x] **Health Checks**: Ready at `/health` (`GetHealthStatus`).
- [x] **CORS**: Securely driven by `AllowedOrigins` configuration.
- [ ] **Default Admin**: `sysadmin`/`syspassword` is hardcoded in `appsettings.json`. MUST be overridden.
- [ ] **Data Safety**: `SeedDummyData: true` MUST be overridden.

## 8. Blockers and Unresolved Questions
- **Frontend Hosting Target**: The presence of `@cloudflare/vite-plugin` implies a specific Cloudflare deployment strategy. The exact target (Vercel, AWS Amplify, Cloudflare Pages, EC2 Node) needs confirmation to define the frontend CI/CD pipeline.
- **File Upload Limits**: No explicit max request size limit is configured for the image upload endpoints, defaulting to ASP.NET Core's ~30MB limit. This may need hardening.

## 9. Items Requiring AWS Access to Verify
- Verification of actual IAM Role inheritance in the deployment environment (EC2/ECS).
- Verification of the `AWSSDK.S3` object mapping against a live bucket.

## 10. Recommended Next Actions
1. **Define Hosting Architecture**: Decide whether the frontend will be statically hosted (S3/CloudFront) or SSR hosted (ECS/Node/Cloudflare).
2. **Provision Database**: Set up an AWS RDS PostgreSQL instance.
3. **Configure Environment Variables**: Define production overrides for `SuperAdmin`, `Jwt`, and `SeedDummyData`.
4. **Provision S3**: Create the private S3 bucket and assign an IAM Role to the backend compute target.

**Final Status**: `READY FOR INFRASTRUCTURE PLANNING`
