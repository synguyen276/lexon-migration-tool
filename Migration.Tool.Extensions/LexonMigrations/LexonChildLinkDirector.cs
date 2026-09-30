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
        var cache = new ConcurrentDictionary<int, string?>();
        getClassNameById = id => cache.GetOrAdd(id, classId => modelFacade.SelectById<ICmsClass>(classId)?.ClassName);
        getChildrenByParentId = parentId => modelFacade.SelectWhere<ICmsTree>("NodeParentID = @parentId", new SqlParameter("parentId", parentId));
    }

    public LexonChildLinkDirector(
        Func<int, string?> getClassNameById,
        IReadOnlyList<ChildLinkRule>? rules = null,
        Func<int, IEnumerable<ICmsTree>>? getChildrenByParentId = null)
    {
        this.getClassNameById = getClassNameById;
        this.rules = rules ?? LexonChildLinkRules.Rules;
        this.getChildrenByParentId = getChildrenByParentId;
    }

    public override void Direct(ContentItemSource source, IContentItemActionProvider options)
    {
        var droppedClasses = DroppedSourceClassNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (droppedClasses.Contains(source.SourceClassName))
        {
            options.Drop();
            return;
        }

        if (source.ChildNodes is null)
        {
            return;
        }

        var matchingRules = rules.Where(r =>
            string.Equals(r.ParentClassName, source.SourceClassName, StringComparison.OrdinalIgnoreCase)
        );

        foreach (var rule in matchingRules)
        {
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

        // Linked RiskItem under risklinkset -> store reference in grandparent (-2) as ResourceAreaRelatedResources
        if (string.Equals(linkedClassName, LexonMigrationConstants.SourceClassNames.RiskItem, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(sourceClassName, LexonMigrationConstants.SourceClassNames.RiskItem, StringComparison.OrdinalIgnoreCase))
        {
            options.StoreReferenceInAncestor(-2, LexonMigrationConstants.FieldNames.ResourceAreaRelatedResources);
            return;
        }

        base.DirectLinkedNode(source, options);
    }
}

