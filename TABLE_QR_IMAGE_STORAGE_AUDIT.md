# TABLE QR Image Storage Architecture Audit

## 1. Identified Image Use Cases

### 1.1 Menu Item Image Upload (Creation)
- **Endpoint**: `POST /api/restaurants/{restaurantId}/menu-items` (`AdminMenuController`)
- **Request Contract**: `CreateMenuItemRequestDto` (FormData with `IFormFile ImageFile`)
- **Storage Location**: Local filesystem under `wwwroot/images/menu/`.
- **Image URL Generation**: Generated as `/images/menu/{Guid}{Extension}`.
- **Database**: The generated URL path is stored in the `ImageUrl` column of the `MenuItems` table (PostgreSQL).
- **Behavior**: Reads the `IFormFile`, checks if the extension is valid (jpg, jpeg, png, webp, gif), and saves it using a `FileStream`.

### 1.2 Menu Item Image Update
- **Endpoint**: `PUT /api/menu-items/{menuItemId}` (`MenuItemsController`)
- **Request Contract**: `UpdateMenuItemRequestDto` (FormData with `IFormFile ImageFile`)
- **Storage Location**: Local filesystem under `wwwroot/images/menu/`.
- **Image URL Generation**: Generated as `/images/menu/{Guid}{Extension}`.
- **Database**: The `ImageUrl` column in `MenuItems` is updated.
- **Replacement Behavior**: A new file is created. The old file is **not deleted** from the local filesystem (orphan file left behind).

### 1.3 Menu Item Deletion
- **Endpoint**: `DELETE /api/menu-items/{menuItemId}`
- **Deletion Behavior**: The `MenuService` performs a soft-delete (`SoftDeleteMenuItemAsync`) on the database record. The associated image file is **not deleted**.

### 1.4 Menu Item Image Display (Admin & Customer)
- **Endpoints**: `GET /api/restaurants/{restaurantId}/menu` and `GET /api/restaurants/{restaurantId}/menu/admin`
- **Retrieval**: Returns the relative URL stored in the DB (e.g., `/images/menu/xyz.jpg`).
- **Frontend Behavior**: The frontend (`tablo/src/services/api.ts`) uses `getFullImageUrl` to convert the relative path into a full absolute URL by prepending the `API_BASE_URL` (minus the `/api` part). If the URL already starts with `http`, it's returned as-is.

### 1.5 Other Image Uses
- No other image uploads (e.g., logos, categories, avatars) were found in the API controllers.

## 2. Summary
- All images are exclusively used for `MenuItem`.
- Stored as physical files in `wwwroot/images/menu`.
- No image deletion logic exists in the current system (neither for replacements nor for menu item deletions).
- Frontend gracefully handles full URLs (`http...`) and relative URLs (`/images...`).
