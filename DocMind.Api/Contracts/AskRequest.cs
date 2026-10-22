namespace DocMind.Api.Contracts;

public record AskRequest(string Question, int? TopK);
