using Microsoft.Extensions.DependencyInjection;
using Migration.Tool.Common.Abstractions;
using Migration.Tool.Common.Builders;
using Migration.Tool.Extensions.LexonMigrations.ClassMappingRules;

namespace Migration.Tool.Extensions.LexonMigrations;

/// <summary>
/// Registers all Lexon class mapping rules (IClassMapping), associated wrapper page
/// rules (IWrapperPageRuleProvider), and child-link rules (IChildLinkRuleProvider).
/// Rules are co-located with their class mapping definitions and collected here.
/// </summary>
public static class LexonClassMappingRules
{
    public static IServiceCollection AddClassMappingRules(this IServiceCollection services)
    {
        // ── Class mappings (IClassMapping) ───────────────────────────────────
        services.AddSingleton<IClassMapping>(NavigationSectionMapping.Create());
        services.AddSingleton<IClassMapping>(RiskItemSetMapping.Create());
        services.AddSingleton<IClassMapping>(RiskItemMapping.Create());
        services.AddSingleton<IClassMapping>(RiskAttachmentSetMapping.Create());
        services.AddSingleton<IClassMapping>(RiskAttachmentMapping.Create());
        services.AddSingleton<IClassMapping>(BulletinMapping.Create());

        // ── Wrapper page rules (IWrapperPageRuleProvider) ────────────────────
        // Each wrapper rule is defined on the class mapping it belongs to.
        // Collected here into one provider so the framework can resolve them.
        services.AddSingleton<IWrapperPageRuleProvider>(new WrapperPageRuleProvider(WrapperRules));

        // ── Child-link rules (IChildLinkRuleProvider) ────────────────────────
        // Each child link rule is defined on the class mapping it belongs to.
        // Collected here into one provider so MigratePageTypesCommand can ensure fields.
        services.AddSingleton<IChildLinkRuleProvider>(new ChildLinkRuleProvider(LexonChildLinkRules.Rules));

        return services;
    }

    /// <summary>
    /// All wrapper page rules, gathered from the class mapping definitions that define them.
    /// </summary>
    public static readonly IReadOnlyList<WrapperPageRule> WrapperRules =
    [
        RiskItemMapping.WrapperRule,
    ];

    /// <summary>
    /// All child-linking rules, gathered from the class mapping definitions that define them.
    /// </summary>
    public static readonly IReadOnlyList<LexonChildLinkRule> ChildLinkRules = LexonChildLinkRules.Rules;
}

