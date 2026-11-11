using Mazwi.Domain;

namespace Mazwi.Application;

public sealed class LanguageService : ILanguageService
{
    public IReadOnlyList<LanguageDto> List() =>
        StoryLanguages.All.Select(language => new LanguageDto(language.Code, language.Name, language.NativeName, language.Greeting, language.AiChecked)).ToList();
}
