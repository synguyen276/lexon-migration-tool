using Migration.Tool.Common.Abstractions;
using Migration.Tool.Common.Builders;

namespace Migration.Tool.Extensions.LexonMigrations.ClassMappingRules;

/// <summary>
/// Maps source Lexon.riskitemset to target Lexon.AreaOfPractice (Reusable Content Item).
/// Also defines the companion wrapper page rule: Lexon.AreaOfPracticePage wraps the reusable item.
/// Source fields: RiskSetName, ImagePath, Description.
/// </summary>
public static class RiskItemSetMapping
{
    /// <summary>
    /// Wrapper page rule: creates the Lexon.AreaOfPracticePage website-page wrapper
    /// that holds a ContentItemReference to the Lexon.AreaOfPractice reusable item.
    /// </summary>
    public static readonly WrapperPageRule WrapperRule = new(
        SourceClassName: LexonMigrationConstants.SourceClassNames.RiskItemSet,
        ReusableContentTypeName: LexonMigrationConstants.TargetClassNames.AreaOfPractice,
        WrapperClassName: LexonMigrationConstants.TargetClassNames.AreaOfPracticePage,
        WrapperDisplayName: "Area of Practice Page",
        ContentItemReferenceField: LexonMigrationConstants.FieldNames.ContentItem,
        IncludeLegacyMetadata: false
    );

    public static MultiClassMapping Create()
    {
        var m = ClassRenameMapping.Reusable(
            LexonMigrationConstants.SourceClassNames.RiskItemSet,
            LexonMigrationConstants.TargetClassNames.AreaOfPractice,
            "Area of Practice");

        m.BuildField("AreaOfPracticeID").AsPrimaryKey();

        m.BuildField(LexonMigrationConstants.FieldNames.Name)
            .SetFrom(LexonMigrationConstants.SourceClassNames.RiskItemSet, LexonMigrationConstants.SourceFieldNames.RiskSetName, true)
            .WithFieldPatch(f => { f.Caption = "Name"; f.AllowEmpty = false; });

        m.BuildField(LexonMigrationConstants.FieldNames.Description)
            .SetFrom(LexonMigrationConstants.SourceClassNames.RiskItemSet, LexonMigrationConstants.SourceFieldNames.Description, true)
            .WithFieldPatch(f => { f.Caption = "Description"; f.AllowEmpty = true; });

        m.BuildField(LexonMigrationConstants.FieldNames.Image)
            .SetFrom(LexonMigrationConstants.SourceClassNames.RiskItemSet, LexonMigrationConstants.SourceFieldNames.ImagePath, true)
            .WithFieldPatch(f => { f.Caption = "Image"; f.AllowEmpty = true; });

        return m;
    }
}
