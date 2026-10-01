namespace DocMind.Tests.Api;

[CollectionDefinition(Name)]
public sealed class ApiCollectionDefinition : ICollectionFixture<DocMindApiFactory>
{
    public const string Name = "Api";
}
