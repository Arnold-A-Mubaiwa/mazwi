using Mazwi.Domain;

namespace Mazwi.Application.Tests;

public sealed class LanguageServiceTests
{
    [Fact]
    public void List_Catalogue_ReturnsEveryLanguageInCatalogueOrder()
    {
        // Act
        IReadOnlyList<LanguageDto> languages = new LanguageService().List();
        // Assert
        Assert.Equal(StoryLanguages.All.Select(language => language.Code), languages.Select(language => language.Code));
        Assert.Equal(new LanguageDto("yo", "Yoruba", "Yorùbá", "Ẹ n lẹ", false), languages.Single(language => language.Code == "yo"));
        Assert.Equal(new LanguageDto("sn", "Shona", "chiShona", "Mhoro", true), languages[0]);
    }
}
