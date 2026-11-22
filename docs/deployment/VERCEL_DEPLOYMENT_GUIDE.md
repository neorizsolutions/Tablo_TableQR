# TABLO — Vercel Frontend Deployment Guide

This guide provides the exact configuration required to deploy the TABLO Vite React Single Page Application (SPA) to Vercel.

---

## 1. Project Import Settings in Vercel

When importing the repository into Vercel, configure the project settings as follows:

| Setting | Value | Notes |
| :--- | :--- | :--- |
| **Git Repository** | `https://github.com/neorizsolutions/Tablo_TableQR.git` | Select your authorized GitHub account / organization |
| **Production Branch** | `production` | Stable production branch containing the reviewed release |
| **Root Directory** | `tablo` | **Crucial:** Must point to the frontend folder `tablo`, not repo root |
| **Framework Preset** | `Vite` | Auto-detects Vite configuration |
| **Build Command** | `npm run build` | Executes Vite production bundle (`vite build`) |
| **Output Directory** | `dist` | Generated static bundle directory |
| **Install Command** | `npm ci` | Strict, reproducible install from `tablo/package-lock.json` |

---

## 2. Environment Variables

Configure the following environment variable in the Vercel dashboard under **Settings > Environment Variables** (Scope: **Production**, **Preview**, **Development**):

| Variable Name | Environment | Value | Description |
| :--- | :---: | :--- | :--- |
| `VITE_API_URL` | Production | `https://13-126-116-187.nip.io/api` | Live HTTPS backend API endpoint on AWS Lightsail |

> **Security Note:** Do not include secrets, database credentials, or server keys in `VITE_*` variables. `VITE_*` variables are embedded into public client bundles by design.

---

## 3. SPA Routing & Clean URLs (`tablo/vercel.json`)

The repository includes `tablo/vercel.json` to handle client-side routing for TanStack Router:

```json
{
  "$schema": "https://openapi.vercel.sh/vercel.json",
  "cleanUrls": true,
  "rewrites": [
    {
      "source": "/(.*)",
      "destination": "/index.html"
    }
  ],
  "headers": [
    {
      "source": "/assets/(.*)",
      "headers": [
        {
          "key": "Cache-Control",
          "value": "public, max-age=31536000, immutable"
        }
      ]
    }
  ]
}
```

This guarantees that direct navigation and browser refreshes on routes such as `/order`, `/admin`, `/dashboard`, `/inventory`, and `/t/:qrToken` resolve directly to `index.html` with HTTP 200 without throwing 404s, while static hashed assets in `/assets/` are served with long-term immutable caching.

---

## 4. Backend CORS Configuration (Post-Deployment Step)

Once Vercel assigns your project its live URL (e.g., `https://tablo-app.vercel.app` or your custom domain):

1. Connect to the AWS Lightsail backend host:
   ```bash
   ssh -i ~/.ssh/lightsail_key.pem ubuntu@13.126.116.187
   ```
2. Edit `/home/ubuntu/tablo-api.env`:
   Add the new Vercel origin while preserving the existing Amplify origin:
   ```bash
   AllowedOrigins__0=https://production.d25iywxxvasd5l.amplifyapp.com
   AllowedOrigins__1=https://<your-project>.vercel.app
   AllowedOrigins__2=http://localhost:5173
   ```
3. Restart the backend container to apply the updated CORS policy:
   ```bash
   sudo docker restart tablo-api
   ```
4. Verify backend health and CORS response:
   ```bash
   curl -I -X OPTIONS https://13-126-116-187.nip.io/api/health \
     -H "Origin: https://<your-project>.vercel.app" \
     -H "Access-Control-Request-Method: GET"
   ```
