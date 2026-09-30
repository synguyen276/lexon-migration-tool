using Migration.Tool.Common.Abstractions;
using Migration.Tool.Common.Builders;

namespace Migration.Tool.Extensions.LexonMigrations.ClassMappingRules;

/// <summary>
/// Maps source Lexon.RiskItem to target Lexon.ResourceArea (Reusable Content Item).
/// Also defines the companion wrapper page rule: Lexon.ResourceAreaPage wraps the reusable item,
/// and child-link rules linking document groups, related resources, and updates.
/// </summary>
public static class RiskItemMapping
{
    /// <summary>
    /// Wrapper page rule: creates the Lexon.ResourceAreaPage website-page wrapper
    /// that holds a ContentItemReference to the Lexon.ResourceArea reusable item.
    /// </summary>
    public static readonly WrapperPageRule WrapperRule = new(
        SourceClassName: LexonMigrationConstants.SourceClassNames.RiskItem,
        ReusableContentTypeName: LexonMigrationConstants.TargetClassNames.ResourceArea,
        WrapperClassName: LexonMigrationConstants.TargetClassNames.ResourceAreaPage,
        WrapperDisplayName: "Resource Area Page",
        ContentItemReferenceField: LexonMigrationConstants.FieldNames.ContentItem,
        IncludeLegacyMetadata: false
    );

    /// <summary>
    /// Child-linking rules originating from Lexon.RiskItem:
    /// - Document Groups (Lexon.riskattachmentset -> Lexon.ResourceAreaDocumentGroup)
    /// - Related Resources (Lexon.RiskItem via risklinkset -> Lexon.ResourceArea)
    /// - Updates (Lexon.Bulletin via BulletinSet -> Lexon.ResourceAreaUpdate)
    /// </summary>
    public static readonly IReadOnlyList<LexonChildLinkRule> ChildLinkRules =
    [
        new(
            ParentClassName: LexonMigrationConstants.SourceClassNames.RiskItem,
            TargetFieldName: LexonMigrationConstants.FieldNames.ResourceAreaDocumentGroups,
            ChildClassName: LexonMigrationConstants.SourceClassNames.RiskAttachmentSet,
            FieldCaption: "Document Groups"
        ),
        new(
            ParentClassName: LexonMigrationConstants.SourceClassNames.RiskItem,
            TargetFieldName: LexonMigrationConstants.FieldNames.ResourceAreaRelatedResources,
            ChildClassName: LexonMigrationConstants.SourceClassNames.RiskItem,
            IntermediateClassName: LexonMigrationConstants.SourceClassNames.RiskLinkSet,
            FieldCaption: "Related Resources"
        ),
        new(
            ParentClassName: LexonMigrationConstants.SourceClassNames.RiskItem,
            TargetFieldName: LexonMigrationConstants.FieldNames.ResourceAreaUpdates,
            ChildClassName: LexonMigrationConstants.SourceClassNames.Bulletin,
            IntermediateClassName: LexonMigrationConstants.SourceClassNames.BulletinSet,
            FieldCaption: "Updates"
        )
    ];

    public static MultiClassMapping Create()
    {
        var m = ClassRenameMapping.Reusable(
            LexonMigrationConstants.SourceClassNames.RiskItem,
            LexonMigrationConstants.TargetClassNames.ResourceArea,
            "Resource Area");

        m.BuildField("ResourceAreaID").AsPrimaryKey();

        m.BuildField(LexonMigrationConstants.FieldNames.Title)
            .SetFrom(LexonMigrationConstants.SourceClassNames.RiskItem, LexonMigrationConstants.SourceFieldNames.ArticleTitle, true)
            .WithFieldPatch(f => { f.Caption = "Title"; f.AllowEmpty = false; });

        m.BuildField(LexonMigrationConstants.FieldNames.Summary)
            .SetFrom(LexonMigrationConstants.SourceClassNames.RiskItem, LexonMigrationConstants.SourceFieldNames.ArticleSummary, true)
            .WithFieldPatch(f => { f.Caption = "Summary"; f.AllowEmpty = true; });

        m.BuildField(LexonMigrationConstants.FieldNames.Text)
            .SetFrom(LexonMigrationConstants.SourceClassNames.RiskItem, LexonMigrationConstants.SourceFieldNames.ArticleText, true)
            .WithFieldPatch(f => { f.Caption = "Text"; f.AllowEmpty = true; });

        m.BuildField(LexonMigrationConstants.FieldNames.Teaser)
            .SetFrom(LexonMigrationConstants.SourceClassNames.RiskItem, LexonMigrationConstants.SourceFieldNames.ArticleTeaser, true)
            .WithFieldPatch(f => { f.Caption = "Teaser"; f.AllowEmpty = true; });

        m.BuildField(LexonMigrationConstants.FieldNames.Image)
            .SetFrom(LexonMigrationConstants.SourceClassNames.RiskItem, LexonMigrationConstants.SourceFieldNames.ImagePath, true)
            .WithFieldPatch(f => { f.Caption = "Image"; f.AllowEmpty = true; });

        // ── Child-link reference fields (kept during mapping so they are not stripped) ──
        m.AddChildLinkField(LexonMigrationConstants.FieldNames.ResourceAreaDocumentGroups, "Document Groups");
        m.AddChildLinkField(LexonMigrationConstants.FieldNames.ResourceAreaRelatedResources, "Related Resources");
        m.AddChildLinkField(LexonMigrationConstants.FieldNames.ResourceAreaUpdates, "Updates");

        return m;
    }
}

