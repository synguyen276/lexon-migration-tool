using Migration.Tool.Common.Abstractions;
using Migration.Tool.Common.Builders;

namespace Migration.Tool.Extensions.LexonMigrations.ClassMappingRules;

/// <summary>
/// Maps source Lexon.RiskItem to target Lexon.ResourceArea (Reusable Content Item).
/// Also defines the companion wrapper page rule: Lexon.ResourceAreaPage wraps the reusable item,
/// and child-link rules linking document groups and related resources.
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
        ContentItemReferenceField: LexonMigrationConstants.FieldNames.ResourceAreaPageResource,
        IncludeLegacyMetadata: false
    );

    /// <summary>
    /// Child-linking rules originating from Lexon.RiskItem:
    /// - Document Groups (Lexon.riskattachmentset -> Lexon.ResourceAreaDocumentGroup)
    /// - Related Resources (Lexon.RiskItem via risklinkset -> Lexon.ResourceArea)
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
        )
    ];

    public static MultiClassMapping Create()
    {
        var m = ClassRenameMapping.Reusable(
            LexonMigrationConstants.SourceClassNames.RiskItem,
            LexonMigrationConstants.TargetClassNames.ResourceArea,
            "Resource Area");

        m.BuildField("ContentItemDataID").AsPrimaryKey();

        m.BuildField(LexonMigrationConstants.FieldNames.ResourceAreaTitle)
            .ConvertFrom(LexonMigrationConstants.SourceClassNames.RiskItem, LexonMigrationConstants.SourceFieldNames.ArticleTitle, true,
                (val, ctx) =>
                {
                    var title = val?.ToString()?.Trim();
                    if (string.IsNullOrWhiteSpace(title))
                    {
                        return "Untitled Resource Area";
                    }
                    return title.Length > 500 ? title[..500] : title;
                })
            .WithFieldPatch(f => { f.Caption = "Title"; f.AllowEmpty = false; });

        m.BuildField(LexonMigrationConstants.FieldNames.ResourceAreaSummary)
            .ConvertFrom(LexonMigrationConstants.SourceClassNames.RiskItem, LexonMigrationConstants.SourceFieldNames.ArticleSummary, true,
                (val, ctx) =>
                {
                    var summary = val?.ToString()?.Trim() ?? string.Empty;
                    return summary.Length > 400 ? summary[..400] : summary;
                })
            .WithFieldPatch(f => { f.Caption = "Summary"; f.AllowEmpty = false; });

        m.BuildField(LexonMigrationConstants.FieldNames.ResourceAreaLastUpdated)
            .ConvertFrom(LexonMigrationConstants.SourceClassNames.RiskItem, LexonMigrationConstants.SourceFieldNames.ArticleText, true,
                (val, ctx) => LexonTaxonomyService.ExtractLastUpdated(val?.ToString(), ctx))
            .WithFieldPatch(f => { f.Caption = "Last Updated"; f.AllowEmpty = false; });

        m.BuildField(LexonMigrationConstants.FieldNames.ResourceAreaBody)
            .ConvertFrom(LexonMigrationConstants.SourceClassNames.RiskItem, LexonMigrationConstants.SourceFieldNames.ArticleText, true,
                (val, ctx) => val?.ToString() ?? string.Empty)
            .WithFieldPatch(f => { f.Caption = "Body"; f.AllowEmpty = false; });

        // ── Child-link reference fields (kept during mapping so they are not stripped) ──
        m.AddChildLinkField(LexonMigrationConstants.FieldNames.ResourceAreaDocumentGroups, "Document Groups");
        m.AddChildLinkField(LexonMigrationConstants.FieldNames.ResourceAreaRelatedResources, "Related Resources");

        return m;
    }
}


