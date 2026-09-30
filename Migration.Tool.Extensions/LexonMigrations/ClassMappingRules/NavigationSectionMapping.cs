using Migration.Tool.Common.Builders;

namespace Migration.Tool.Extensions.LexonMigrations.ClassMappingRules;

/// <summary>
/// Maps source Lexon.NavigationSection to target Lexon.AreaOfPracticeLandingPage (Website Page).
/// </summary>
public static class NavigationSectionMapping
{
    public static MultiClassMapping Create()
    {
        var m = ClassRenameMapping.WebsitePage(
            LexonMigrationConstants.SourceClassNames.NavigationSection,
            LexonMigrationConstants.TargetClassNames.AreaOfPracticeLandingPage,
            "Area of Practice Landing Page");

        m.BuildField("AreaOfPracticeLandingPageID").AsPrimaryKey();

        return m;
    }
}
