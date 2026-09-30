using Migration.Tool.Common.Builders;

namespace Migration.Tool.Extensions.LexonMigrations.ClassMappingRules;

/// <summary>
/// Maps source Lexon.Bulletin to target Lexon.ResourceAreaUpdate (Reusable Content Item).
/// </summary>
public static class BulletinMapping
{
    public static MultiClassMapping Create()
    {
        var m = ClassRenameMapping.Reusable(
            LexonMigrationConstants.SourceClassNames.Bulletin,
            LexonMigrationConstants.TargetClassNames.ResourceAreaUpdate,
            "Resource Area Update");

        m.BuildField("ResourceAreaUpdateID").AsPrimaryKey();

        m.BuildField(LexonMigrationConstants.FieldNames.Title)
            .SetFrom(LexonMigrationConstants.SourceClassNames.Bulletin, LexonMigrationConstants.SourceFieldNames.Title, true)
            .WithFieldPatch(f => { f.Caption = "Title"; f.AllowEmpty = false; });

        m.BuildField(LexonMigrationConstants.FieldNames.Summary)
            .SetFrom(LexonMigrationConstants.SourceClassNames.Bulletin, LexonMigrationConstants.SourceFieldNames.Summary, true)
            .WithFieldPatch(f => { f.Caption = "Summary"; f.AllowEmpty = true; });

        m.BuildField(LexonMigrationConstants.FieldNames.Text)
            .SetFrom(LexonMigrationConstants.SourceClassNames.Bulletin, LexonMigrationConstants.SourceFieldNames.Body, true)
            .WithFieldPatch(f => { f.Caption = "Text"; f.AllowEmpty = true; });

        m.BuildField(LexonMigrationConstants.FieldNames.Date)
            .SetFrom(LexonMigrationConstants.SourceClassNames.Bulletin, LexonMigrationConstants.SourceFieldNames.DateOfItem, true)
            .WithFieldPatch(f => { f.Caption = "Date"; f.AllowEmpty = true; });

        m.BuildField(LexonMigrationConstants.FieldNames.Image)
            .SetFrom(LexonMigrationConstants.SourceClassNames.Bulletin, LexonMigrationConstants.SourceFieldNames.TeaserImage, true)
            .WithFieldPatch(f => { f.Caption = "Image"; f.AllowEmpty = true; });

        m.BuildField(LexonMigrationConstants.FieldNames.Attachment)
            .SetFrom(LexonMigrationConstants.SourceClassNames.Bulletin, LexonMigrationConstants.SourceFieldNames.Attachments, true)
            .WithFieldPatch(f => { f.Caption = "Attachment"; f.AllowEmpty = true; });

        return m;
    }
}
