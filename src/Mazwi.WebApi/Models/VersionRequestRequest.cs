using System.ComponentModel.DataAnnotations;

namespace Mazwi.WebApi.Models;

public sealed record VersionRequestRequest([Required, StringLength(2, MinimumLength = 2)] string Language);
