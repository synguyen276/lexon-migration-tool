using System.Collections.Concurrent;
using Microsoft.Data.SqlClient;
using Migration.Tool.Common.Abstractions;
using Migration.Tool.Source;
using Migration.Tool.Source.Mappers.ContentItemMapperDirectives;
using Migration.Tool.Source.Model;

namespace Migration.Tool.Extensions.LexonMigrations;

public class LexonChildLinkDirector : ContentItemDirectorBase
{
    private readonly Func<int, string?> getClassNameById;
    private readonly Func<int, IEnumerable<ICmsTree>>? getChildrenByParentId;
    private readonly Func<int, ICmsTree?>? getNodeById;
    private readonly IReadOnlyList<ChildLinkRule> rules;

    public override IEnumerable<string> DroppedSourceClassNames =>
        rules.Where(r => !string.IsNullOrEmpty(r.IntermediateClassName))
             .Select(r => r.IntermediateClassName!)
             .Distinct(StringComparer.OrdinalIgnoreCase);

    public LexonChildLinkDirector(ModelFacade modelFacade)
        : this(modelFacade, LexonChildLinkRules.Rules)
    {
    }

    public LexonChildLinkDirector(ModelFacade modelFacade, IChildLinkRuleProvider? childLinkRuleProvider)
        : this(modelFacade, childLinkRuleProvider?.GetRules() ?? LexonChildLinkRules.Rules)
    {
    }

    public LexonChildLinkDirector(ModelFacade modelFacade, IReadOnlyList<ChildLinkRule> rules)
    {
        this.rules = rules;
        var classCache = new ConcurrentDictionary<int, string?>();
        getClassNameById = id => classCache.GetOrAdd(id, classId => modelFacade.SelectById<ICmsClass>(classId)?.ClassName);
        var nodeCache = new ConcurrentDictionary<int, ICmsTree?>();
        getNodeById = id => nodeCache.GetOrAdd(id, nodeId => modelFacade.SelectById<ICmsTree>(nodeId));
        getChildrenByParentId = parentId => modelFacade.SelectWhere<ICmsTree>("NodeParentID = @parentId", new SqlParameter("parentId", parentId));
    }

    public LexonChildLinkDirector(
        Func<int, string?> getClassNameById,
        IReadOnlyList<ChildLinkRule>? rules = null,
        Func<int, IEnumerable<ICmsTree>>? getChildrenByParentId = null,
        Func<int, ICmsTree?>? getNodeById = null)
    {
        this.getClassNameById = getClassNameById;
        this.rules = rules ?? LexonChildLinkRules.Rules;
        this.getChildrenByParentId = getChildrenByParentId;
        this.getNodeById = getNodeById;
    }

    public override void Direct(ContentItemSource source, IContentItemActionProvider options)
    {
        var droppedClasses = DroppedSourceClassNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (droppedClasses.Contains(source.SourceClassName))
        {
            options.Drop();
            return;
        }

        var matchingRules = rules.Where(r =>
            string.Equals(r.ParentClassName, source.SourceClassName, StringComparison.OrdinalIgnoreCase)
        );

        foreach (var rule in matchingRules)
        {
            if (rule.IsAncestorLink)
            {
                if (source.SourceNode?.NodeParentID is not null && getNodeById is not null)
                {
                    ICmsTree? targetAncestor = null;
                    if (!string.IsNullOrEmpty(rule.IntermediateClassName))
                    {
                        var parentNode = getNodeById(source.SourceNode.NodeParentID.Value);
                        if (parentNode is not null)
                        {
                            var parentClass = getClassNameById(parentNode.NodeClassID);
                            if (string.Equals(parentClass, rule.IntermediateClassName, StringComparison.OrdinalIgnoreCase) && parentNode.NodeParentID is not null)
                            {
                                var grandparentNode = getNodeById(parentNode.NodeParentID.Value);
                                if (grandparentNode is not null && string.Equals(getClassNameById(grandparentNode.NodeClassID), rule.ChildClassName, StringComparison.OrdinalIgnoreCase))
                                {
                                    targetAncestor = grandparentNode;
                                }
                            }
                            else if (string.Equals(parentClass, rule.ChildClassName, StringComparison.OrdinalIgnoreCase))
                            {
                                targetAncestor = parentNode;
                            }
                        }
                    }
                    else
                    {
                        var parentNode = getNodeById(source.SourceNode.NodeParentID.Value);
                        if (parentNode is not null && string.Equals(getClassNameById(parentNode.NodeClassID), rule.ChildClassName, StringComparison.OrdinalIgnoreCase))
                        {
                            targetAncestor = parentNode;
                        }
                    }

                    if (targetAncestor is not null)
                    {
                        options.LinkChildren(rule.TargetFieldName, [targetAncestor]);
                    }
                }

                continue;
            }

            if (source.ChildNodes is null)
            {
                continue;
            }

            List<ICmsTree> matchingChildren;

            if (!string.IsNullOrEmpty(rule.IntermediateClassName))
            {
                var intermediateNodes = source.ChildNodes.Where(child =>
                {
                    var className = getClassNameById(child.NodeClassID);
                    return string.Equals(className, rule.IntermediateClassName, StringComparison.OrdinalIgnoreCase);
                });

                matchingChildren = [];
                if (getChildrenByParentId is not null)
                {
                    foreach (var intermediate in intermediateNodes)
                    {
                        var grandChildren = getChildrenByParentId(intermediate.NodeID);
                        matchingChildren.AddRange(grandChildren.Where(gc =>
                        {
                            var className = getClassNameById(gc.NodeClassID);
                            return string.Equals(className, rule.ChildClassName, StringComparison.OrdinalIgnoreCase);
                        }));
                    }
                }
            }
            else
            {
                matchingChildren = source.ChildNodes.Where(child =>
                {
                    var className = getClassNameById(child.NodeClassID);
                    return string.Equals(className, rule.ChildClassName, StringComparison.OrdinalIgnoreCase);
                }).ToList();
            }

            if (matchingChildren.Count != 0)
            {
                options.LinkChildren(rule.TargetFieldName, matchingChildren);
            }
        }
    }

    public override void DirectLinkedNode(LinkedPageSource source, ILinkedPageActionProvider options)
    {
        var linkedClassName = getClassNameById(source.LinkedNode.NodeClassID);
        var sourceClassName = getClassNameById(source.SourceNode.NodeClassID);

        // Check whether the linked node's parent (or the source node itself) is a risklinkset container
        string? parentClassName = null;
        if (source.SourceNode?.NodeParentID is int parentId && getNodeById is not null)
        {
            var parentNode = getNodeById(parentId);
            if (parentNode is not null)
            {
                parentClassName = getClassNameById(parentNode.NodeClassID);
            }
        }

        bool isUnderRiskLinkSet =
            string.Equals(parentClassName, LexonMigrationConstants.SourceClassNames.RiskLinkSet, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(sourceClassName, LexonMigrationConstants.SourceClassNames.RiskLinkSet, StringComparison.OrdinalIgnoreCase);

        // Linked RiskItem under risklinkset -> store reference in grandparent (-2) as ResourceAreaRelatedResources (referencing website page GUID)
        if (isUnderRiskLinkSet &&
            (string.Equals(linkedClassName, LexonMigrationConstants.SourceClassNames.RiskItem, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(sourceClassName, LexonMigrationConstants.SourceClassNames.RiskItem, StringComparison.OrdinalIgnoreCase)))
        {
            options.StoreReferenceInAncestor(-2, LexonMigrationConstants.FieldNames.ResourceAreaRelatedResources, referenceWebsiteItem: true);
            return;
        }

        base.DirectLinkedNode(source, options);
    }
}

