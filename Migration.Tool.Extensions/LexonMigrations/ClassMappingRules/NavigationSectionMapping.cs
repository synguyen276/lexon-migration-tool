using CMS.FormEngine;
using Migration.Tool.Common.Builders;

namespace Migration.Tool.Extensions.LexonMigrations.ClassMappingRules;

/// <summary>
/// Maps source Lexon.NavigationSection to target Lexon.AreaOfPracticeLandingPage (Website Page).
/// </summary>
public static class NavigationSectionMapping
{
    public static MultiClassMapping Create()
    {
        var m = ClassRenameMapping.WebsitePage(
            LexonMigrationConstants.SourceClassNames.NavigationSection,
            LexonMigrationConstants.TargetClassNames.AreaOfPracticeLandingPage,
            "Area of Practice Landing Page",
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

        return m;
    }

    /// <summary>
    /// Removes extraneous fields (ResourceAreaRelatedResources and DocumentName)
    /// from the AreaOfPracticeLandingPage form definition.
    /// </summary>
    public static void CleanFormDefinition(FormInfo fi)
    {
        if (fi.GetFormField(LexonMigrationConstants.FieldNames.ResourceAreaRelatedResources) is { } refField)
        {
            fi.RemoveFormField(refField.Name);
        }
        if (fi.GetFormField(LexonMigrationConstants.SourceFieldNames.DocumentName) is { } docField)
        {
            fi.RemoveFormField(docField.Name);
        }
    }
}
