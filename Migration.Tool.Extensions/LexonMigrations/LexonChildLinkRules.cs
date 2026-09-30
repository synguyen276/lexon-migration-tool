using Migration.Tool.Common.Abstractions;
using Migration.Tool.Extensions.LexonMigrations.ClassMappingRules;

namespace Migration.Tool.Extensions.LexonMigrations;

/// <summary>
/// Child-link rule definition for Lexon migrations, extending the common <see cref="ChildLinkRule"/>.
/// </summary>
public record LexonChildLinkRule(
    string ParentClassName,
    string TargetFieldName,
    string ChildClassName,
    string? IntermediateClassName = null,
    string? FieldCaption = null
) : ChildLinkRule(ParentClassName, TargetFieldName, ChildClassName, IntermediateClassName, FieldCaption);

public static class LexonChildLinkRules
{
    /// <summary>
    /// All child-linking rules for the Area of Practice / Resource Area model,
    /// gathered from the class mapping definitions that define them.
    /// </summary>
    public static readonly IReadOnlyList<LexonChildLinkRule> Rules =
    [
        ..RiskItemMapping.ChildLinkRules,
        ..RiskAttachmentSetMapping.ChildLinkRules
    ];
}


