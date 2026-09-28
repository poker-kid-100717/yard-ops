namespace Portfolio.Yard.Api.Integrations.Ltl;

public sealed class LtlOptions
{
    public const string Section = "Ltl";
    public string BaseUrl { get; set; } = "http://localhost:5102/";
    public string SigningKey { get; set; } = "portfolio-dev-only-change-me";
}
