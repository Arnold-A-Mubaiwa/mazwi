using System.ComponentModel.DataAnnotations;

namespace Mazwi.WebApi.Models;

public sealed record TwinsAllowedRequest([Required] bool? Allowed);
