namespace DocMind.Api.Contracts;

public record QueryRequest(string Question, int? TopK);
