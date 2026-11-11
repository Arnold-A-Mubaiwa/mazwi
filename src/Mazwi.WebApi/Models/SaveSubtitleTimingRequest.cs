using System.ComponentModel.DataAnnotations;

namespace Mazwi.WebApi.Models;

public sealed record SaveSubtitleTimingRequest([Required, MaxLength(2000)] IReadOnlyList<int> StartsMs, int EndMs, Guid ReviewVersion);
