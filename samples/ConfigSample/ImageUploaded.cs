namespace ConfigSample;

internal class ImageUploaded
{
    public string? ImageId { get; set; }
    public string? Url { get; set; }
    public long SizeBytes { get; set; }
}

internal class VideoUploaded
{
    public string? VideoId { get; set; }
    public string? Url { get; set; }
    public long SizeBytes { get; set; }
}
