namespace LlmObservabilityLab.Api.Teams;

public sealed class TeamContext
{
    public const string HeaderName = "X-Team-Id";
    public const string PropertyName = "team.id";

    public static readonly IReadOnlySet<string> KnownTeams =
        new HashSet<string>(StringComparer.Ordinal) { "engineering", "support" };

    public string TeamId { get; internal set; } = string.Empty;
}
