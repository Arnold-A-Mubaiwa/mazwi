using System.ComponentModel.DataAnnotations;

namespace Mazwi.WebApi.Models;

public sealed class SaveStoryTextRequest
{
    [Required]
    public string Text { get; init; } = string.Empty;
}
