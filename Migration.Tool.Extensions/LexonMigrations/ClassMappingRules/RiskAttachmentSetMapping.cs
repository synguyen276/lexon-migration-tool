using Migration.Tool.Common.Builders;

namespace Migration.Tool.Extensions.LexonMigrations.ClassMappingRules;

/// <summary>
/// Maps source Lexon.riskattachmentset to target Lexon.ResourceAreaDocumentGroup (Reusable Content Item).
/// Also defines child-link rules linking documents (Lexon.riskattachment -> Lexon.ResourceAreaDocument).
/// </summary>
public static class RiskAttachmentSetMapping
{
    /// <summary>
    /// Child-linking rules originating from Lexon.riskattachmentset:
    /// - Documents (Lexon.riskattachment -> Lexon.ResourceAreaDocument)
    /// </summary>
    public static readonly IReadOnlyList<LexonChildLinkRule> ChildLinkRules =
    [
        new(
            ParentClassName: LexonMigrationConstants.SourceClassNames.RiskAttachmentSet,
            TargetFieldName: LexonMigrationConstants.FieldNames.ResourceAreaDocumentGroupDocuments,
            ChildClassName: LexonMigrationConstants.SourceClassNames.RiskAttachment,
            FieldCaption: "Documents"
        )
    ];

    public static MultiClassMapping Create()
    {
        var m = ClassRenameMapping.Reusable(
            LexonMigrationConstants.SourceClassNames.RiskAttachmentSet,
            LexonMigrationConstants.TargetClassNames.ResourceAreaDocumentGroup,
            "Resource Area Document Group");

        m.BuildField("ContentItemDataID").AsPrimaryKey();

        m.BuildField(LexonMigrationConstants.FieldNames.ResourceAreaDocumentGroupTitle)
            .ConvertFrom(LexonMigrationConstants.SourceClassNames.RiskAttachmentSet, LexonMigrationConstants.SourceFieldNames.RiskSetName, true,
                (val, ctx) =>
                {
                    var title = val?.ToString()?.Trim();
                    if (string.IsNullOrWhiteSpace(title))
                    {
                        return "Untitled Document Group";
                    }
                    return title.Length > 500 ? title[..500] : title;
                })
            .WithFieldPatch(f => { f.Caption = "Title"; f.AllowEmpty = false; });

        // ── Child-link reference fields (kept during mapping so they are not stripped) ──
        m.AddChildLinkField(LexonMigrationConstants.FieldNames.ResourceAreaDocumentGroupDocuments, "Documents");

        return m;
    }
}


