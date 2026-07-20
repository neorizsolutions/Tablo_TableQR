# Tablo

Tablo is a modern restaurant QR ordering and operations platform. It allows restaurants to streamline their ordering process, manage staff, and provide a seamless digital menu experience for their customers.

## Features

### Customer Experience
- **Scan & Order**: Customers scan a QR code at their table to instantly access the menu. No app download required.
- **Digital Menu**: View categories, items, prices, and descriptions.
- **Cart & Checkout**: Add items to the cart and place orders directly to the kitchen.
- **Live Order Tracking**: Track the status of the active order (Placed -> Preparing -> Ready -> Served).

### Kitchen & Waiter Operations
- **Order Management**: Receive new orders in real-time.
- **Status Updates**: Update order statuses as they progress through the kitchen and are served to the table.
- **Role-Based Access**: Staff log in with secure, role-specific accounts (`Kitchen`, `Waiter`).

### Admin Dashboard
- **Menu Management**: Create, edit, and organize menu items and categories.
- **Table Management**: Manage restaurant tables and generate unique QR codes for each table.
- **Staff Management**: Create employee accounts, assign roles, and activate/deactivate access.

## System Architecture & Tech Stack

This project is separated into a frontend client and a backend API.

### Frontend
- **Framework**: React 19 / Vite
- **Language**: TypeScript
- **Styling**: Tailwind CSS, class-variance-authority, clsx, tailwind-merge
- **Routing**: TanStack Router
- **Data Fetching**: TanStack React Query
- **Components**: Radix UI, Lucide React icons, shadcn/ui inspired components

### Backend
- **Framework**: .NET 8 (ASP.NET Core Web API)
- **Language**: C#
- **ORM**: Entity Framework Core
- **Database**: PostgreSQL (Npgsql)
- **Authentication**: JWT Bearer authentication & BCrypt password hashing

## Setup Instructions

### Prerequisites
- Node.js (v18+)
- .NET 8 SDK
- PostgreSQL Server

### Backend Setup
1. Navigate to the backend directory:
   ```bash
   cd Tablo.Api
   ```
2. Configure your database connection and secrets:
   - Copy `appsettings.example.json` to `appsettings.json`.
   - Update the `DefaultConnection` string with your PostgreSQL credentials.
   - Update the `Jwt:Key` with a strong, 32+ character secret key.
3. Apply database migrations:
   ```bash
   dotnet ef database update
   ```
4. Run the API:
   ```bash
   dotnet run
   ```
   The API will start at `http://localhost:5166`.

### Frontend Setup
1. Navigate to the frontend directory:
   ```bash
   cd tablo
   ```
2. Install dependencies:
   ```bash
   npm install
   ```
3. Set up environment variables:
   - Ensure `.env.development` contains `VITE_API_URL=http://localhost:5166/api` (matching your backend URL).
4. Run the development server:
   ```bash
   npm run dev
   ```

## Development Status
This is a prototype/MVP demonstrating a full-stack SaaS for restaurant operations.
