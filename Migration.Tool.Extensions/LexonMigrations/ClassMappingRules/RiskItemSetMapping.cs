using CMS.FormEngine;
using Migration.Tool.Common.Builders;
using Migration.Tool.Common.Helpers;

namespace Migration.Tool.Extensions.LexonMigrations.ClassMappingRules;

/// <summary>
/// Maps source Lexon.riskitemset to target Lexon.AreaOfPracticePage (Website Page).
/// </summary>
public static class RiskItemSetMapping
{
    public static MultiClassMapping Create()
    {
        var m = ClassRenameMapping.WebsitePage(
            LexonMigrationConstants.SourceClassNames.RiskItemSet,
            LexonMigrationConstants.TargetClassNames.AreaOfPracticePage,
            "Area of Practice Page",
            classPatcher: target =>
            {
                if (!string.IsNullOrWhiteSpace(target.ClassFormDefinition))
                {
                    var fi = new FormInfo(target.ClassFormDefinition);
                    CleanFormDefinition(fi);
                    target.ClassFormDefinition = fi.GetXmlDefinition();
                }
            });

        m.BuildField("ContentItemDataID").AsPrimaryKey();

        m.BuildField(LexonMigrationConstants.FieldNames.AreaOfPracticePagePageCategory)
            .ConvertFrom(LexonMigrationConstants.SourceClassNames.RiskItemSet, LexonMigrationConstants.SourceFieldNames.DocumentName, true,
                (val, ctx) =>
                {
                    var nodeName = val?.ToString()?.Trim();
                    if (string.IsNullOrWhiteSpace(nodeName))
                    {
                        return null;
                    }
                    return LexonTaxonomyService.Instance.GetTagReferenceJson(nodeName);
                })
            .WithFieldPatch(f =>
            {
                f.Caption = "Resource Category";
                f.AllowEmpty = true;
                f.Guid = GuidHelper.CreateFieldGuid($"{LexonMigrationConstants.FieldNames.AreaOfPracticePagePageCategory.ToLower()}|{LexonMigrationConstants.TargetClassNames.AreaOfPracticePage}");
            });

        return m;
    }

    /// <summary>
    /// Removes DocumentName from the AreaOfPracticePage form definition if present.
    /// </summary>
    public static void CleanFormDefinition(FormInfo fi)
    {
        if (fi.GetFormField(LexonMigrationConstants.SourceFieldNames.DocumentName) is { } docField)
        {
            fi.RemoveFormField(docField.Name);
        }
    }
}


