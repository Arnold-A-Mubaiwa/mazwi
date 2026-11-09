using System.ComponentModel.DataAnnotations;

namespace Mazwi.WebApi.Models;

public sealed record CreateTaskItemRequest([Required] string Title);
