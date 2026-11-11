using System.ComponentModel.DataAnnotations;

namespace Mazwi.WebApi.Models;

public sealed record ReportStoryRequest([Required, MaxLength(20)] string Reason, [MaxLength(500)] string? Note);
