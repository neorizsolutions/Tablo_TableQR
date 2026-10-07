# TABLE QR S3 Integration Report

## 1. Files Changed
- **Added**:
  - `Tablo.Api/Interfaces/IImageStorageService.cs`
  - `Tablo.Api/Services/LocalImageStorageService.cs`
  - `Tablo.Api/Services/S3ImageStorageService.cs`
  - `Tablo.Api/Configurations/ImageStorageOptions.cs`
  - `Tablo.Api.Tests/ImageStorageTests.cs`
- **Modified**:
  - `Tablo.Api/Program.cs` (Registered `IImageStorageService` and AWSSDK options)
  - `Tablo.Api/Controllers/AdminMenuController.cs` (Used DI for storage)
  - `Tablo.Api/Controllers/MenuItemsController.cs` (Used DI for storage)
  - `Tablo.Api/Services/MenuService.cs` (Resolves image URL in DTO mapping)
  - `Tablo.Api/appsettings.json` and `appsettings.example.json` (Added `ImageStorage` section)
  - `Tablo.Api/Tablo.Api.csproj` (Added AWS SDK S3 packages)

## 2. Storage Abstraction Design
The abstraction uses an `IImageStorageService` interface with two implementations: `LocalImageStorageService` and `S3ImageStorageService`.
- **UploadImageAsync**: Uploads the file and returns a relative identifier path (e.g. `/images/menu/{fileName}`).
- **DeleteImageAsync**: Deletes the image by its identifier.
- **GetImageUrl**: Takes the identifier path and maps it to an accessible URL (e.g., presigned S3 url or relative URL for local storage).

## 3. S3 Configuration Requirements
S3 mode is configured in `appsettings.json` (or via environment variables).
```json
"ImageStorage": {
  "Provider": "S3",
  "S3": {
    "BucketName": "tablo-menu-images",
    "Region": "ap-south-1"
  }
}
```

## 4. Image Retrieval Strategy
The system uses **Presigned URLs** with an expiration time of 7 days for S3.
This strategy avoids caching issues on the frontend and keeps the bucket entirely private.
By storing the original relative paths in the DB (e.g. `/images/menu/xyz.ext`), existing records do not need any database migration—the `S3ImageStorageService` correctly identifies them and generates presigned URLs for matching keys.

## 5. Existing API Contract Compatibility
- API endpoints `POST` and `PUT` `menu-items` continue accepting `IFormFile` inputs.
- API endpoints returning `MenuItemDto` return the resolved `ImageUrl`. For Local it remains a relative path (which frontend prepends `API_BASE_URL` to). For S3, it returns an absolute `https://...` presigned URL (which frontend naturally handles without modifying).
- The contract is fully preserved, requiring **zero** frontend code changes.

## 6. Local Development Behavior
By default, the `ImageStorage:Provider` is set to `Local`. This ensures that local development works flawlessly out of the box without requiring AWS credentials or S3 buckets.
