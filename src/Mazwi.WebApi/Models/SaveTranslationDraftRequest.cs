using System.ComponentModel.DataAnnotations;

namespace Mazwi.WebApi.Models;

public sealed class SaveTranslationDraftRequest
{
    [Required]
    public string Text { get; init; } = string.Empty;
    public IReadOnlyList<string>? Notes { get; init; }
}
