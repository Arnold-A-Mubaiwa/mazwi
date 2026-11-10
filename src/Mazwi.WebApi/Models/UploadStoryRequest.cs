using System.ComponentModel.DataAnnotations;

namespace Mazwi.WebApi.Models;

public sealed class UploadStoryRequest
{
    [Required]
    public IFormFile Recording { get; init; } = null!;
    [Required]
    public string Title { get; init; } = string.Empty;
    [Required]
    public string ArtistName { get; init; } = string.Empty;
    [Required]
    public string Community { get; init; } = string.Empty;
    [Required]
    public string Country { get; init; } = string.Empty;
    [Required]
    public string Type { get; init; } = string.Empty;
    [Required]
    public string Language { get; init; } = string.Empty;
    public bool Permission { get; init; }
}
