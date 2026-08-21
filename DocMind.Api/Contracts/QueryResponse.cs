namespace DocMind.Api.Contracts;

public record QueryResponse(string Answer, List<SourceResult> Sources);
