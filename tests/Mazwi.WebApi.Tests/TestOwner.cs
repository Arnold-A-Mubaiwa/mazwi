namespace Mazwi.WebApi.Tests;

// The artist who owns stories created by tests. Repository tests insert this artist before using it.
internal static class TestOwner
{
    public static readonly Guid Id = Guid.Parse("7e57a2d1-0000-4000-8000-00000000a271");
}
