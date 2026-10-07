# TABLE QR Menu Image Deletion Review

## 1. Trace Menu Item Deletion
- **Call Path**: 
  1. `Tablo.Api/Controllers/MenuItemsController.cs` -> `DeleteMenuItem` (Line 100)
  2. `Tablo.Api/Services/MenuService.cs` -> `DeleteMenuItemAsync` (Line 262)
  3. `Tablo.Api/Repositories/MenuRepository.cs` -> `SoftDeleteMenuItemAsync` (Line 106)
- **Database Behavior**: The menu item is **soft-deleted**. `IsDeleted = true` is set and saved to the database.
- **Image URL**: The `ImageUrl` database field is **retained** and not cleared.
- **Storage Deletion**: `IImageStorageService.DeleteImageAsync` is **not called** during this flow. 
- **Physical File/Object Deletion**: Neither local image files nor S3 objects are deleted. 

## 2. Trace ImageStorageService.DeleteImageAsync
- **Call Sites**: There are **zero** call sites for `DeleteImageAsync` in the codebase. It is implemented but entirely unused.
- **Local Implementation**: If it were called, `LocalImageStorageService` identifies the file by stripping the leading slash, appending it to the `wwwroot` directory, checking `File.Exists()`, and executing `File.Delete()`. Missing files are silently ignored.
- **S3 Implementation**: If it were called, `S3ImageStorageService` maps the path to an S3 object key (stripping the leading slash) and invokes `DeleteObjectAsync`. It uses a `catch (AmazonS3Exception)` block to silently ignore missing objects or soft errors.
- **Risk Assessment**: Because it is never invoked, a storage deletion failure is impossible and cannot interrupt any existing product workflow.

## 3. Compare Against Existing Behavior
- **Pre-S3 Baseline**: The `git diff` confirms that neither `MenuRepository.cs` nor the deletion endpoints in `MenuItemsController.cs` were modified by the S3 integration.
- **Soft-Delete**: The application has historically relied entirely on soft-deletes (`IsDeleted = true`) without clearing `ImageUrl`.
- **Local Retention**: The previous implementation intentionally orphaned replaced/deleted local image files (no `File.Delete` or `System.IO` cleanup code existed in the old controllers).
- **Result**: The S3 integration strictly inherits and preserves this original, non-destructive behavior.

## Final Outcome
**CONFIRMED — EXISTING IMAGE RETENTION BEHAVIOR PRESERVED**
