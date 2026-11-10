using System.ComponentModel.DataAnnotations;

namespace Mazwi.WebApi.Models;

public sealed class UploadTranslationRecordingRequest
{
    [Required]
    public IFormFile Recording { get; init; } = null!;
    public Guid ReviewVersion { get; init; }
    public bool Permission { get; init; }
}
