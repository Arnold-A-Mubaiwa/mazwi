using System.ComponentModel.DataAnnotations;

namespace Mazwi.WebApi.Models;

public sealed record DeleteAccountRequest([Required, MaxLength(128)] string Password);
