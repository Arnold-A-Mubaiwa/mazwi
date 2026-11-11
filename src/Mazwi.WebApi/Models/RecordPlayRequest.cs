using System.ComponentModel.DataAnnotations;

namespace Mazwi.WebApi.Models;

public sealed record RecordPlayRequest([Required, StringLength(2, MinimumLength = 2)] string Language, Guid Version);
