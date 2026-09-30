using Migration.Tool.Common.Builders;

namespace Migration.Tool.Extensions.LexonMigrations.ClassMappingRules;

/// <summary>
/// Maps source Lexon.riskattachment to target Lexon.ResourceAreaDocument (Reusable Content Item).
/// </summary>
public static class RiskAttachmentMapping
{
    public static MultiClassMapping Create()
    {
        var m = ClassRenameMapping.Reusable(
            LexonMigrationConstants.SourceClassNames.RiskAttachment,
            LexonMigrationConstants.TargetClassNames.ResourceAreaDocument,
            "Resource Area Document");

        m.BuildField("ResourceAreaDocumentID").AsPrimaryKey();

        m.BuildField(LexonMigrationConstants.FieldNames.FileName)
            .SetFrom(LexonMigrationConstants.SourceClassNames.RiskAttachment, LexonMigrationConstants.SourceFieldNames.FileName, true)
            .WithFieldPatch(f => { f.Caption = "File Name"; f.AllowEmpty = true; });

        m.BuildField(LexonMigrationConstants.FieldNames.Attachment)
            .SetFrom(LexonMigrationConstants.SourceClassNames.RiskAttachment, LexonMigrationConstants.SourceFieldNames.Attachment, true)
            .WithFieldPatch(f => { f.Caption = "Attachment"; f.AllowEmpty = true; });

        m.BuildField(LexonMigrationConstants.FieldNames.Description)
            .SetFrom(LexonMigrationConstants.SourceClassNames.RiskAttachment, LexonMigrationConstants.SourceFieldNames.Description, true)
            .WithFieldPatch(f => { f.Caption = "Description"; f.AllowEmpty = true; });

        return m;
    }
}
