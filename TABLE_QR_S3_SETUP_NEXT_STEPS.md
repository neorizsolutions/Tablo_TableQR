# TABLE QR S3 Setup Next Steps

## 1. Required AWS Bucket Settings
- Create a private S3 bucket.
- **Block Public Access**: Ensure "Block all public access" is enabled. The integration relies strictly on signed URLs for secure access to objects.
- **CORS (Cross-Origin Resource Sharing)**: Configure CORS if the frontend will ever request images via XHR/fetch directly from the S3 URL (though standard `<img>` tags typically bypass this).
  ```json
  [
      {
          "AllowedHeaders": ["*"],
          "AllowedMethods": ["GET"],
          "AllowedOrigins": ["*"],
          "ExposeHeaders": []
      }
  ]
  ```

## 2. Required IAM Permissions
The application requires an IAM User or IAM Role with the following minimal permissions restricted to the specific bucket:
```json
{
    "Version": "2012-10-17",
    "Statement": [
        {
            "Sid": "TableQRS3Access",
            "Effect": "Allow",
            "Action": [
                "s3:PutObject",
                "s3:GetObject",
                "s3:DeleteObject"
            ],
            "Resource": "arn:aws:s3:::<your-bucket-name>/*"
        }
    ]
}
```

## 3. Required Environment Variables
Set the following environment variables (or updating `appsettings.Production.json`) for the ASP.NET Core environment:
- `ImageStorage__Provider=S3`
- `ImageStorage__S3__BucketName=<your-bucket-name>`
- `ImageStorage__S3__Region=<your-aws-region>`

## 4. How to Configure Credentials
The application leverages the default AWS SDK credential chain. Once AWS S3 is enabled:
- **Local Testing**: Run `aws configure` via the AWS CLI to set your credentials locally. The application will automatically pick them up.
- **Production (AWS Hosting)**: Use an IAM Role attached to the EC2 instance, ECS task, or EKS pod. The application will automatically retrieve temporary credentials.
- **Production (Non-AWS Hosting)**: Set the `AWS_ACCESS_KEY_ID` and `AWS_SECRET_ACCESS_KEY` environment variables.

## 5. How to Test S3 Safely
1. Ensure your local `appsettings.Development.json` retains `Provider: "Local"`.
2. Configure AWS credentials via `aws configure` locally.
3. Start the application by overriding the provider via command line:
   `dotnet run --ImageStorage:Provider=S3 --ImageStorage:S3:BucketName=my-test-bucket`
4. Upload an image in the Admin Dashboard and verify that a presigned URL is correctly generated and displays the image.

## 6. Existing-Image Migration Plan
To seamlessly migrate existing local images to S3:
1. Sync local images to S3 using the AWS CLI, preserving the directory structure:
   `aws s3 sync ./wwwroot/images/menu/ s3://<your-bucket-name>/images/menu/`
2. Update the environment variables to switch the provider to `S3`.
3. Because the `S3ImageStorageService` expects the exact database paths (`/images/menu/{fileName}`), the transition requires **no database updates**. All existing entries will automatically be mapped to S3 presigned URLs.

## 7. Rollback Procedure
If the S3 integration causes issues in production:
1. Change `ImageStorage__Provider` back to `Local` in the environment configuration.
2. Restart the API service.
3. Newly uploaded images will immediately start saving to the local `wwwroot` directory again.
