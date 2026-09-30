namespace Migration.Tool.Common.Abstractions;

public record WrapperPageRule(
    string SourceClassName,
    string ReusableContentTypeName,
    string WrapperClassName,
    string WrapperDisplayName,
    string ContentItemReferenceField = "ContentItem",
    bool IncludeLegacyMetadata = true
);

public interface IWrapperPageRuleProvider
{
    bool TryGetRule(string sourceClassName, out WrapperPageRule? rule);
    bool TryGetRuleByWrapper(string wrapperClassName, out WrapperPageRule? rule);
    IReadOnlyList<WrapperPageRule> GetRules();
}

public class WrapperPageRuleProvider : IWrapperPageRuleProvider
{
    private readonly IReadOnlyList<WrapperPageRule> rules;
    private readonly Dictionary<string, WrapperPageRule> rulesBySource;
    private readonly Dictionary<string, WrapperPageRule> rulesByWrapper;

    public WrapperPageRuleProvider(IReadOnlyList<WrapperPageRule> rules)
    {
        this.rules = rules;
        rulesBySource = rules.ToDictionary(r => r.SourceClassName, StringComparer.OrdinalIgnoreCase);
        rulesByWrapper = rules.ToDictionary(r => r.WrapperClassName, StringComparer.OrdinalIgnoreCase);
    }

    public bool TryGetRule(string sourceClassName, out WrapperPageRule? rule) =>
        rulesBySource.TryGetValue(sourceClassName, out rule);

    public bool TryGetRuleByWrapper(string wrapperClassName, out WrapperPageRule? rule) =>
        rulesByWrapper.TryGetValue(wrapperClassName, out rule);

    public IReadOnlyList<WrapperPageRule> GetRules() => rules;
}
