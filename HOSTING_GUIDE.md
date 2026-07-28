# Tablo Hosting & Deployment Guide

When you are ready to host the Tablo application on a live server (such as Azure, AWS, Vercel, or DigitalOcean), you must update specific configurations so the application uses real, secure production values instead of your local development defaults.

## 1. Backend Configuration (API)

When deploying the `.NET` backend, you must provide secure production values for the database and authentication. 

**File Location:** `Tablo.Api/appsettings.json` (or set as Environment Variables on your hosting provider).

### Database Connection String
- **Exact JSON Path:** `"ConnectionStrings": { "DefaultConnection": "..." }`
- **What to Change:** Replace the localhost connection string with your live PostgreSQL connection string provided by your host (e.g., Supabase, Neon, AWS RDS).
- **Example:**
  ```json
  "ConnectionStrings": {
    "DefaultConnection": "Host=live-db-server.com;Database=TabloDb;Username=real_username;Password=real_secure_password"
  }
  ```

### JWT Secret Key
- **Exact JSON Path:** `"Jwt": { "Key": "..." }`
- **What to Change:** Replace the placeholder key with a completely random, secure string that is **at least 32 characters long**. This key signs your staff login tokens. Do not share this key.
- **Example:**
  ```json
  "Jwt": {
    "Key": "A_Very_Long_And_Random_Secure_String_987654321!",
    "Issuer": "TabloApi",
    "Audience": "TabloApp"
  }
  ```

> [!TIP]
> **Best Practice:** Instead of writing these secrets directly into the `appsettings.json` file on the server, modern hosting providers allow you to set them as **Environment Variables**. For example, setting an environment variable named `ConnectionStrings__DefaultConnection` will automatically override the value in the JSON file securely.

---

## 2. Frontend Configuration (React/Vite)

The React frontend needs to know the URL of your live backend API. Currently, it points to `localhost`.

**File Location:** `tablo/.env.production` (You may need to create this file, or set it in your host like Vercel/Netlify).

### API URL
- **Exact Variable Name:** `VITE_API_URL`
- **What to Change:** Set this to the public URL where your backend API is hosted.
- **Example File Content:**
  ```env
  VITE_API_URL=https://api.your-restaurant-domain.com/api
  ```
- **Note:** You must rebuild the frontend (`npm run build`) after changing this value so it gets baked into the production code.

---

## 3. Database Migrations

When you deploy your live database, it will be completely empty. You must create the tables (Restaurants, Employees, Orders, etc.) before the app will work.

- **How to run:** 
  1. Temporarily point your local `appsettings.json` to the live database connection string.
  2. Open your terminal in the `Tablo.Api` folder.
  3. Run the migration command:
     ```bash
     dotnet ef database update
     ```
  4. (Alternative): Generate an SQL script using `dotnet ef migrations script` and run that script directly on your live database console.
