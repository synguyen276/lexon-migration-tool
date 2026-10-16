namespace Migration.Tool.Common.Abstractions;

/// <summary>
/// Defines a rule for linking child pages as referenced content items
/// on a parent content item in XbyK Content Hub.
/// </summary>
public record ChildLinkRule(
    string ParentClassName,
    string TargetFieldName,
    string ChildClassName,
    string? IntermediateClassName = null,
    string? FieldCaption = null,
    bool IsAncestorLink = false
);

/// <summary>
/// Provides child-linking rules so that content item reference fields
/// can be ensured on target content types during --page-types migration
/// and linked during --pages migration.
/// </summary>
public interface IChildLinkRuleProvider
{
    IReadOnlyList<ChildLinkRule> GetRules();
    IReadOnlyList<ChildLinkRule> GetRulesForParent(string parentClassName);
}

/// <summary>
/// Default implementation of <see cref="IChildLinkRuleProvider"/>.
/// </summary>
public class ChildLinkRuleProvider : IChildLinkRuleProvider
{
    private readonly IReadOnlyList<ChildLinkRule> rules;
    private readonly ILookup<string, ChildLinkRule> rulesByParent;

    public ChildLinkRuleProvider(IReadOnlyList<ChildLinkRule> rules)
    {
        this.rules = rules;
        rulesByParent = rules.ToLookup(r => r.ParentClassName, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<ChildLinkRule> GetRules() => rules;

    public IReadOnlyList<ChildLinkRule> GetRulesForParent(string parentClassName) =>
        rulesByParent[parentClassName].ToList();
}
