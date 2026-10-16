using CMS.DataEngine;
using Migration.Tool.Common.Builders;
using Migration.Tool.KXP.Api.Auxiliary;
using Migration.Tool.KXP.Api.Services.CmsClass;

namespace Migration.Tool.Extensions.LexonMigrations;

/// <summary>
/// Factory for creating <see cref="MultiClassMapping"/> instances that rename a K13 class
/// to an XbyK target class with sensible defaults. Only class name, display name and table name
/// need to be specified; <c>ClassType</c>, <c>ClassContentTypeType</c>, and <c>ClassWebPageHasUrl</c>
/// are set automatically based on the chosen factory method.
/// </summary>
public static class ClassRenameMapping
{
    /// <summary>
    /// Creates a mapping that targets a <b>Reusable</b> content type (Content Hub item).
    /// <c>ClassWebPageHasUrl</c> is set to <c>false</c>.
    /// </summary>
    /// <param name="sourceClassName">The K13 source class code name (e.g. "Lexon.riskattachment").</param>
    /// <param name="targetClassName">The XbyK target class code name (e.g. "Lexon.Attachment").</param>
    /// <param name="targetDisplayName">The human-readable display name shown in XbyK admin.</param>
    /// <param name="targetTableName">
    /// Optional explicit table name. When <c>null</c>, derived from <paramref name="targetClassName"/>
    /// by replacing the first dot with an underscore (e.g. "Lexon.Attachment" → "Lexon_Attachment").
    /// </param>
    public static MultiClassMapping Reusable(
        string sourceClassName,
        string targetClassName,
        string targetDisplayName,
        string? targetTableName = null,
        Action<DataClassInfo>? classPatcher = null)
    {
        return Create(sourceClassName, targetClassName, targetDisplayName, targetTableName,
            ClassContentTypeType.REUSABLE, classWebPageHasUrl: false, classPatcher);
    }

    /// <summary>
    /// Creates a mapping that targets a <b>Website</b> page content type.
    /// <c>ClassWebPageHasUrl</c> is set to <c>true</c>.
    /// </summary>
    /// <param name="sourceClassName">The K13 source class code name.</param>
    /// <param name="targetClassName">The XbyK target class code name.</param>
    /// <param name="targetDisplayName">The human-readable display name shown in XbyK admin.</param>
    /// <param name="targetTableName">
    /// Optional explicit table name. When <c>null</c>, derived from <paramref name="targetClassName"/>.
    /// </param>
    /// <param name="classPatcher">Optional action to customize the target <see cref="DataClassInfo"/>.</param>
    public static MultiClassMapping WebsitePage(
        string sourceClassName,
        string targetClassName,
        string targetDisplayName,
        string? targetTableName = null,
        Action<DataClassInfo>? classPatcher = null)
    {
        return Create(sourceClassName, targetClassName, targetDisplayName, targetTableName,
            ClassContentTypeType.WEBSITE, classWebPageHasUrl: true, classPatcher);
    }

    /// <summary>
    /// Adds a child-link ContentItemReference field to the target class schema
    /// so the field exists during --page-types migration and is preserved when mapping.
    /// </summary>
    /// <param name="m">The target multi-class mapping.</param>
    /// <param name="targetFieldName">The reference field name (e.g. "ResourceAreaDocumentGroups").</param>
    /// <param name="caption">The display caption for the field.</param>
    /// <returns>The multi-class mapping instance for chaining.</returns>
    public static MultiClassMapping AddChildLinkField(
        this MultiClassMapping m,
        string targetFieldName,
        string caption)
    {
        m.BuildField(targetFieldName)
            .WithoutSource(dataType: FieldDataType.ContentItemReference, allowEmpty: true)
            .WithFieldPatch(f =>
            {
                f.Caption = caption;
                f.Visible = true;
                f.Enabled = true;
                f.Settings ??= new System.Collections.Hashtable();
                f.Settings[FormDefinitionPatcher.SettingsElemControlname] = FormComponents.AdminContentItemSelectorComponent;
            });

        return m;
    }

    private static MultiClassMapping Create(
        string sourceClassName,
        string targetClassName,
        string targetDisplayName,
        string? targetTableName,
        string classContentTypeType,
        bool classWebPageHasUrl,
        Action<DataClassInfo>? classPatcher = null)
    {
        string tableName = targetTableName ?? DeriveTableName(targetClassName);

        var m = new MultiClassMapping(targetClassName, target =>
        {
            target.ClassName = targetClassName;
            if (string.IsNullOrWhiteSpace(target.ClassTableName))
            {
                target.ClassTableName = tableName;
            }
            target.ClassDisplayName = targetDisplayName;
            target.ClassType = ClassType.CONTENT_TYPE;
            target.ClassContentTypeType = classContentTypeType;
            target.ClassWebPageHasUrl = classWebPageHasUrl;

            classPatcher?.Invoke(target);
        });

        // Register the source class so IsMatch() works
        m.SourceClassNames.Add(sourceClassName);

        return m;
    }

    /// <summary>
    /// Derives a SQL table name from a dotted class code name.
    /// "Lexon.ResourceArea" → "Lexon_ResourceArea".
    /// </summary>
    public static string DeriveTableName(string className)
    {
        int dotIndex = className.IndexOf('.');
        if (dotIndex >= 0)
        {
            return string.Concat(className.AsSpan(0, dotIndex), "_", className.AsSpan(dotIndex + 1));
        }

        return className;
    }
}

