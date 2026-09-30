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
            TargetFieldName: LexonMigrationConstants.FieldNames.ResourceAreaDocuments,
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

        m.BuildField("ResourceAreaDocumentGroupID").AsPrimaryKey();

        m.BuildField(LexonMigrationConstants.FieldNames.Name)
            .SetFrom(LexonMigrationConstants.SourceClassNames.RiskAttachmentSet, LexonMigrationConstants.SourceFieldNames.RiskSetName, true)
            .WithFieldPatch(f => { f.Caption = "Name"; f.AllowEmpty = false; });

        m.BuildField(LexonMigrationConstants.FieldNames.Description)
            .SetFrom(LexonMigrationConstants.SourceClassNames.RiskAttachmentSet, LexonMigrationConstants.SourceFieldNames.Description, true)
            .WithFieldPatch(f => { f.Caption = "Description"; f.AllowEmpty = true; });

        m.BuildField(LexonMigrationConstants.FieldNames.MediaLibraryFolder)
            .SetFrom(LexonMigrationConstants.SourceClassNames.RiskAttachmentSet, LexonMigrationConstants.SourceFieldNames.MLFolder, true)
            .WithFieldPatch(f => { f.Caption = "Media Library Folder"; f.AllowEmpty = true; });

        // ── Child-link reference fields (kept during mapping so they are not stripped) ──
        m.AddChildLinkField(LexonMigrationConstants.FieldNames.ResourceAreaDocuments, "Documents");

        return m;
    }
}

