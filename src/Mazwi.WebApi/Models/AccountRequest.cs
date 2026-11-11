using System.ComponentModel.DataAnnotations;

namespace Mazwi.WebApi.Models;

public sealed record AccountRequest([Required, MaxLength(254)] string Email, [Required, MaxLength(128)] string Password);
