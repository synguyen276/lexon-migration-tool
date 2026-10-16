using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using CMS.ContentEngine;
using CMS.DataEngine;
using Microsoft.Data.SqlClient;
using Migration.Tool.Common;
using Migration.Tool.Common.Builders;
using Migration.Tool.Common.Helpers;
using Migration.Tool.Source;
using Migration.Tool.Source.Model;
using Newtonsoft.Json;

namespace Migration.Tool.Extensions.LexonMigrations;

/// <summary>
/// Service managing Lexon category taxonomies, tag resolution, and parent/child hierarchy lookups.
/// </summary>
public class LexonTaxonomyService
{
    private static readonly Lazy<LexonTaxonomyService> lazyInstance = new(() => new LexonTaxonomyService());
    public static LexonTaxonomyService Instance => lazyInstance.Value;

    private readonly ConcurrentDictionary<string, Guid> tagGuidCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<Guid, string> nodeGuidToParentRiskItemSetName = new();
    private readonly ConcurrentDictionary<int, DateTime> documentIdToModifiedWhen = new();
    private ModelFacade? modelFacade;

    public IReadOnlyDictionary<Guid, string> ParentCategories => nodeGuidToParentRiskItemSetName;

    /// <summary>
    /// Initializes caches and target taxonomy from source and target databases.
    /// </summary>
    public void Initialize(ModelFacade modelFacade)
    {
        this.modelFacade = modelFacade;
        try
        {
            const string hierarchyQuery = """
                SELECT Child.NodeGUID AS ChildGuid, Parent.NodeName AS ParentName
                FROM CMS_Tree Child
                JOIN CMS_Tree Parent ON Child.NodeParentID = Parent.NodeID
                JOIN CMS_Class ParentClass ON Parent.NodeClassID = ParentClass.ClassID
                JOIN CMS_Class ChildClass ON Child.NodeClassID = ChildClass.ClassID
                WHERE ChildClass.ClassName = 'Lexon.RiskItem' AND ParentClass.ClassName = 'Lexon.riskitemset'
                """;

            var pairs = modelFacade.Select(hierarchyQuery, (reader, _) => new
            {
                ChildGuid = reader.Unbox<Guid>("ChildGuid"),
                ParentName = reader.Unbox<string>("ParentName")
            });

            foreach (var pair in pairs)
            {
                if (!string.IsNullOrWhiteSpace(pair.ParentName))
                {
                    nodeGuidToParentRiskItemSetName[pair.ChildGuid] = pair.ParentName;
                }
            }
        }
        catch { }

        try
        {
            const string docQuery = """
                SELECT DocumentID, DocumentModifiedWhen
                FROM CMS_Document
                WHERE DocumentNodeID IN (
                    SELECT NodeID FROM CMS_Tree WHERE NodeClassID IN (
                        SELECT ClassID FROM CMS_Class WHERE ClassName = 'Lexon.RiskItem'
                    )
                )
                """;

            var docDates = modelFacade.Select(docQuery, (reader, _) => new
            {
                DocumentID = reader.Unbox<int>("DocumentID"),
                DocumentModifiedWhen = reader.Unbox<DateTime>("DocumentModifiedWhen")
            });

            foreach (var dd in docDates)
            {
                documentIdToModifiedWhen[dd.DocumentID] = dd.DocumentModifiedWhen;
            }
        }
        catch { }

        EnsureTargetTaxonomyAndTags(modelFacade);
    }

    private void EnsureTargetTaxonomyAndTags(ModelFacade modelFacade)
    {
        try
        {
            var taxonomyGuid = LexonMigrationConstants.Taxonomies.ResourceCategoryGuid;
            var taxonomy = TaxonomyInfo.Provider.Get()
                .WhereEquals(nameof(TaxonomyInfo.TaxonomyGUID), taxonomyGuid)
                .FirstOrDefault();

            if (taxonomy == null)
            {
                taxonomy = new TaxonomyInfo
                {
                    TaxonomyName = LexonMigrationConstants.Taxonomies.ResourceCategoryName,
                    TaxonomyTitle = LexonMigrationConstants.Taxonomies.ResourceCategoryTitle,
                    TaxonomyGUID = taxonomyGuid
                };
                TaxonomyInfo.Provider.Set(taxonomy);
            }

            var existingTags = TagInfo.Provider.Get()
                .WhereEquals(nameof(TagInfo.TagTaxonomyID), taxonomy.TaxonomyID)
                .ToList();

            foreach (var tag in existingTags)
            {
                tagGuidCache[tag.TagTitle] = tag.TagGUID;
                tagGuidCache[tag.TagName] = tag.TagGUID;
            }

            var categoryNames = modelFacade.Select(
                "SELECT DISTINCT NodeName FROM View_CMS_Tree_Joined WHERE ClassName = 'Lexon.riskitemset'",
                (reader, _) => reader.Unbox<string>("NodeName")
            ).Where(name => !string.IsNullOrWhiteSpace(name)).ToList();

            foreach (var name in categoryNames)
            {
                if (!string.IsNullOrWhiteSpace(name) && !tagGuidCache.ContainsKey(name))
                {
                    var safeCodeName = Regex.Replace(name, @"[^\w]", "");
                    var tagGuid = GuidV5.NewNameBased(taxonomyGuid, $"tag|{safeCodeName.ToLowerInvariant()}");
                    var tag = new TagInfo
                    {
                        TagName = safeCodeName,
                        TagTitle = name,
                        TagTaxonomyID = taxonomy.TaxonomyID,
                        TagGUID = tagGuid,
                        TagOrder = 0
                    };
                    TagInfo.Provider.Set(tag);

                    tagGuidCache[name] = tagGuid;
                    tagGuidCache[safeCodeName] = tagGuid;
                }
            }
        }
        catch { }
    }

    public Guid GetOrCreateTagGuid(string? categoryName)
    {
        if (string.IsNullOrWhiteSpace(categoryName)) return Guid.Empty;

        var trimmed = categoryName.Trim();
        if (tagGuidCache.TryGetValue(trimmed, out var cachedGuid)) return cachedGuid;

        var safeCodeName = Regex.Replace(trimmed, @"[^\w]", "");
        if (tagGuidCache.TryGetValue(safeCodeName, out var codeCachedGuid)) return codeCachedGuid;

        try
        {
            var taxonomyGuid = LexonMigrationConstants.Taxonomies.ResourceCategoryGuid;
            var taxonomy = TaxonomyInfo.Provider.Get()
                .WhereEquals(nameof(TaxonomyInfo.TaxonomyGUID), taxonomyGuid)
                .FirstOrDefault();

            if (taxonomy != null)
            {
                var existingTag = TagInfo.Provider.Get()
                    .WhereEquals(nameof(TagInfo.TagTaxonomyID), taxonomy.TaxonomyID)
                    .WhereEquals(nameof(TagInfo.TagTitle), trimmed)
                    .FirstOrDefault();

                if (existingTag != null)
                {
                    tagGuidCache[trimmed] = existingTag.TagGUID;
                    return existingTag.TagGUID;
                }

                var deterministicGuid = GuidV5.NewNameBased(taxonomyGuid, $"tag|{safeCodeName.ToLowerInvariant()}");
                var newTag = new TagInfo
                {
                    TagName = safeCodeName,
                    TagTitle = trimmed,
                    TagTaxonomyID = taxonomy.TaxonomyID,
                    TagGUID = deterministicGuid,
                    TagOrder = 0
                };
                TagInfo.Provider.Set(newTag);

                tagGuidCache[trimmed] = deterministicGuid;
                tagGuidCache[safeCodeName] = deterministicGuid;
                return deterministicGuid;
            }
        }
        catch { }

        var fallbackGuid = GuidV5.NewNameBased(LexonMigrationConstants.Taxonomies.ResourceCategoryGuid, $"tag|{safeCodeName.ToLowerInvariant()}");
        tagGuidCache[trimmed] = fallbackGuid;
        tagGuidCache[safeCodeName] = fallbackGuid;
        return fallbackGuid;
    }

    public string? GetTagReferenceJson(string? categoryName)
    {
        var guid = GetOrCreateTagGuid(categoryName);
        return guid == Guid.Empty ? null : JsonConvert.SerializeObject(new[] { new { Identifier = guid } });
    }

    public string? GetParentRiskItemSetName(Guid childNodeGuid)
    {
        if (nodeGuidToParentRiskItemSetName.TryGetValue(childNodeGuid, out var parentName))
        {
            return parentName;
        }

        if (modelFacade != null)
        {
            try
            {
                const string query = """
                    WITH Hierarchy AS (
                        SELECT Parent.NodeID, Parent.NodeParentID, Parent.NodeName, Parent.NodeClassID, 1 AS Level
                        FROM CMS_Tree Child
                        JOIN CMS_Tree Parent ON Child.NodeParentID = Parent.NodeID
                        WHERE Child.NodeGUID = @childGuid

                        UNION ALL

                        SELECT Parent.NodeID, Parent.NodeParentID, Parent.NodeName, Parent.NodeClassID, H.Level + 1
                        FROM CMS_Tree Parent
                        JOIN Hierarchy H ON H.NodeParentID = Parent.NodeID
                    )
                    SELECT TOP 1 H.NodeName AS ParentName
                    FROM Hierarchy H
                    JOIN CMS_Class C ON H.NodeClassID = C.ClassID
                    WHERE C.ClassName = 'Lexon.riskitemset'
                    ORDER BY H.Level
                    """;

                var result = modelFacade.Select(query, (reader, _) => reader.Unbox<string>("ParentName"),
                    new SqlParameter("childGuid", childNodeGuid)).FirstOrDefault();

                if (!string.IsNullOrWhiteSpace(result))
                {
                    nodeGuidToParentRiskItemSetName[childNodeGuid] = result;
                    return result;
                }
            }
            catch { }
        }

        return null;
    }

    public string? GetParentCategoryTagJson(Guid childNodeGuid)
    {
        var parentCategory = GetParentRiskItemSetName(childNodeGuid);
        return string.IsNullOrWhiteSpace(parentCategory) ? null : GetTagReferenceJson(parentCategory);
    }

    public DateTime? GetDocumentModifiedWhen(int documentId) =>
        documentIdToModifiedWhen.TryGetValue(documentId, out var modified) ? modified : null;

    public static string ExtractLastUpdated(string? text, IConvertorContext? ctx = null)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            var m1 = Regex.Match(text, @"(?i)Last updated:\s*([A-Za-z]+(?:\s+\d{1,2},?)?\s+\d{4}|\d{1,2}\s+[A-Za-z]+\s+\d{4})");
            if (m1.Success)
            {
                var dateStr = m1.Groups[1].Value.Trim();
                return dateStr.Length > 50 ? dateStr[..50] : dateStr;
            }

            var m2 = Regex.Match(text, @"(?i)Last updated:\s*([^.<\r\n]+)");
            if (m2.Success)
            {
                var extracted = m2.Groups[1].Value.Trim().TrimEnd('.', ',');
                if (!string.IsNullOrWhiteSpace(extracted))
                {
                    return extracted.Length > 50 ? extracted[..50] : extracted;
                }
            }
        }

        if (ctx is ConvertorTreeNodeContext { DocumentId: { } docId } && docId > 0)
        {
            var modified = Instance.GetDocumentModifiedWhen(docId);
            if (modified.HasValue)
            {
                var dateStr = modified.Value.ToString("d MMMM yyyy");
                return dateStr.Length > 50 ? dateStr[..50] : dateStr;
            }
        }

        var fallback = DateTime.UtcNow.ToString("d MMMM yyyy");
        return fallback.Length > 50 ? fallback[..50] : fallback;
    }

    #region Test Helpers

    public void SetParentCategory(Guid childNodeGuid, string parentCategoryName) =>
        nodeGuidToParentRiskItemSetName[childNodeGuid] = parentCategoryName;

    public void SetDocumentModifiedWhen(int documentId, DateTime modifiedWhen) =>
        documentIdToModifiedWhen[documentId] = modifiedWhen;

    public void SetTagGuid(string categoryName, Guid tagGuid)
    {
        tagGuidCache[categoryName] = tagGuid;
        var safeCodeName = Regex.Replace(categoryName, @"[^\w]", "");
        tagGuidCache[safeCodeName] = tagGuid;
    }

    public void ClearCaches()
    {
        tagGuidCache.Clear();
        nodeGuidToParentRiskItemSetName.Clear();
        documentIdToModifiedWhen.Clear();
        modelFacade = null;
    }

    #endregion
}
