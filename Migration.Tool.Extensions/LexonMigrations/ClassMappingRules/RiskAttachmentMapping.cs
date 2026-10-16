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

        m.BuildField("ContentItemDataID").AsPrimaryKey();

        m.BuildField(LexonMigrationConstants.FieldNames.ResourceAreaDocumentTitle)
            .ConvertFrom(LexonMigrationConstants.SourceClassNames.RiskAttachment, LexonMigrationConstants.SourceFieldNames.FileName, true,
                (val, ctx) =>
                {
                    var title = val?.ToString()?.Trim();
                    if (string.IsNullOrWhiteSpace(title))
                    {
                        return "Untitled Document";
                    }
                    return title.Length > 500 ? title[..500] : title;
                })
            .WithFieldPatch(f => { f.Caption = "Document Title"; f.AllowEmpty = false; });

        m.BuildField(LexonMigrationConstants.FieldNames.ResourceAreaDocumentFile)
            .SetFrom(LexonMigrationConstants.SourceClassNames.RiskAttachment, LexonMigrationConstants.SourceFieldNames.Attachment, true)
            .WithFieldPatch(f => { f.Caption = "Document File"; f.AllowEmpty = true; });

        return m;
    }
}

