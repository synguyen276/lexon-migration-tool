using Migration.Tool.Common.Builders;

namespace Migration.Tool.Extensions.LexonMigrations.ClassMappingRules;

/// <summary>
/// Maps source Lexon.Bulletin to target Lexon.ResourceAreaUpdate (Reusable Content Item).
/// Also defines the child-link rule linking its ancestor Lexon.RiskItem via Lexon.BulletinSet.
/// </summary>
public static class BulletinMapping
{
    /// <summary>
    /// Child-linking rules originating from Lexon.Bulletin:
    /// - Related Resources (Lexon.RiskItem ancestor via Lexon.BulletinSet -> Lexon.ResourceArea)
    /// </summary>
    public static readonly IReadOnlyList<LexonChildLinkRule> ChildLinkRules =
    [
        new(
            ParentClassName: LexonMigrationConstants.SourceClassNames.Bulletin,
            TargetFieldName: LexonMigrationConstants.FieldNames.RiskUpdateRelatedResources,
            ChildClassName: LexonMigrationConstants.SourceClassNames.RiskItem,
            IntermediateClassName: LexonMigrationConstants.SourceClassNames.BulletinSet,
            FieldCaption: "Related Resources",
            IsAncestorLink: true
        )
    ];

    public static MultiClassMapping Create()
    {
        var m = ClassRenameMapping.Reusable(
            LexonMigrationConstants.SourceClassNames.Bulletin,
            LexonMigrationConstants.TargetClassNames.ResourceAreaUpdate,
            "Resource Area Update");

        m.BuildField("ContentItemDataID").AsPrimaryKey();

        m.BuildField(LexonMigrationConstants.FieldNames.RiskUpdateTitle)
            .ConvertFrom(LexonMigrationConstants.SourceClassNames.Bulletin, LexonMigrationConstants.SourceFieldNames.Title, true,
                (val, ctx) =>
                {
                    var title = val?.ToString()?.Trim();
                    if (string.IsNullOrWhiteSpace(title))
                    {
                        return "Untitled Update";
                    }
                    return title.Length > 500 ? title[..500] : title;
                })
            .WithFieldPatch(f => { f.Caption = "Title"; f.AllowEmpty = false; });

        m.BuildField(LexonMigrationConstants.FieldNames.RiskUpdateDate)
            .ConvertFrom(LexonMigrationConstants.SourceClassNames.Bulletin, LexonMigrationConstants.SourceFieldNames.DateOfItem, true,
                (val, ctx) =>
                {
                    if (val is DateTime dt)
                    {
                        return dt;
                    }
                    if (val is string s && DateTime.TryParse(s, out var parsed))
                    {
                        return parsed;
                    }
                    return DateTime.UtcNow;
                })
            .WithFieldPatch(f => { f.Caption = "Date"; f.AllowEmpty = false; });

        m.BuildField(LexonMigrationConstants.FieldNames.RiskUpdateBody)
            .ConvertFrom(LexonMigrationConstants.SourceClassNames.Bulletin, LexonMigrationConstants.SourceFieldNames.Body, true,
                (val, ctx) => val?.ToString() ?? string.Empty)
            .WithFieldPatch(f => { f.Caption = "Body"; f.AllowEmpty = false; });

        m.AddChildLinkField(LexonMigrationConstants.FieldNames.RiskUpdateRelatedResources, "Related Resources");

        return m;
    }
}

