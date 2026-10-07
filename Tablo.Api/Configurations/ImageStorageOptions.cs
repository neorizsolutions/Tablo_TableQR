namespace Tablo.Api.Configurations
{
    public class ImageStorageOptions
    {
        public const string ImageStorage = "ImageStorage";

        public string Provider { get; set; } = "Local";
        public S3Options S3 { get; set; } = new S3Options();
    }

    public class S3Options
    {
        public string BucketName { get; set; } = string.Empty;
        public string Region { get; set; } = "ap-south-1";
    }
}
