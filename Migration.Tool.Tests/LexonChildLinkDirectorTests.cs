using Migration.Tool.Common.ContentItemOptions;
using Migration.Tool.Extensions.LexonMigrations;
using Migration.Tool.Source.Mappers.ContentItemMapperDirectives;
using Migration.Tool.Source.Model;
using Newtonsoft.Json.Linq;

namespace Migration.Tool.Tests;

public class LexonChildLinkDirectorTests
{
    private class FakeActionProvider : IContentItemActionProvider
    {
        public List<(string fieldName, List<ICmsTree> children)> Links { get; } = [];
        public bool Dropped { get; private set; }
        public void LinkChildren(string fieldName, IEnumerable<ICmsTree> children) => Links.Add((fieldName, children.ToList()));
        public void AsWidget(string w, Guid? g1, Guid? g2, Action<IConvertToWidgetOptions> o) => throw new NotImplementedException();
        public void Drop() => Dropped = true;
        public void OverridePageTemplate(string t, JObject? p = null) => throw new NotImplementedException();
        public void RegenerateUrlPath() => throw new NotImplementedException();
        public void OverrideFormerUrlPaths(IEnumerable<FormerPageUrlPath> f) => throw new NotImplementedException();
        public void OverrideTargetType(string t) => throw new NotImplementedException();
        public void OverrideContentFolder(Guid g) => throw new NotImplementedException();
        public void OverrideContentFolder(string p) => throw new NotImplementedException();
        public void OverrideWorkspace(string n, string d) => throw new NotImplementedException();
        public void OverrideWorkspace(Guid g) => throw new NotImplementedException();
    }

    private record TestCmsTree(int NodeID, int NodeClassID, string NodeName = "") : ICmsTree
    {
        public string NodeAliasPath => "";
        public string NodeAlias => "";
        public int? NodeParentID => null;
        public int NodeLevel => 0;
        public int? NodeACLID => null;
        public int NodeSiteID => 1;
        public Guid NodeGUID { get; } = Guid.NewGuid();
        public int? NodeOrder => null;
        public bool? IsSecuredNode => null;
        public int? NodeSKUID => null;
        public int? NodeLinkedNodeID => null;
        public int? NodeOwner => null;
        public string? NodeCustomData => null;
        public int? NodeLinkedNodeSiteID => null;
        public bool? NodeHasChildren => null;
        public bool? NodeHasLinks => null;
        public int? NodeOriginalNodeID => null;
        public bool NodeIsACLOwner => false;
    }


    // =====================================================================================
    // Direct child-linking tests (no intermediate)
    // =====================================================================================

    [Fact]
    public void Director_LinksResourceAreaDocumentGroups_UnderRiskItem()
    {
        var classNames = new Dictionary<int, string>
        {
            [100] = LexonMigrationConstants.SourceClassNames.RiskItem,
            [200] = LexonMigrationConstants.SourceClassNames.RiskAttachmentSet
        };

        var child1 = new TestCmsTree(201, 200, "AttachmentSet1");
        var child2 = new TestCmsTree(202, 200, "AttachmentSet2");

        var director = new LexonChildLinkDirector(
            id => classNames.GetValueOrDefault(id),
            LexonChildLinkRules.Rules,
            _ => []);

        var source = new ContentItemSource(
            new TestCmsTree(1, 100, "RiskItem1"),
            LexonMigrationConstants.SourceClassNames.RiskItem,
            LexonMigrationConstants.SourceClassNames.RiskItem,
            null, null, [child1, child2]
        );

        var actionProvider = new FakeActionProvider();
        director.Direct(source, actionProvider);

        var docGroupLink = actionProvider.Links.First(l => l.fieldName == LexonMigrationConstants.FieldNames.ResourceAreaDocumentGroups);
        Assert.Equal(2, docGroupLink.children.Count);
        Assert.Contains(child1, docGroupLink.children);
        Assert.Contains(child2, docGroupLink.children);
    }

    [Fact]
    public void Director_LinksResourceAreaDocuments_UnderRiskAttachmentSet()
    {
        var classNames = new Dictionary<int, string>
        {
            [300] = LexonMigrationConstants.SourceClassNames.RiskAttachmentSet,
            [400] = LexonMigrationConstants.SourceClassNames.RiskAttachment
        };

        var child1 = new TestCmsTree(401, 400, "Attachment1");

        var director = new LexonChildLinkDirector(
            id => classNames.GetValueOrDefault(id),
            LexonChildLinkRules.Rules,
            _ => []);

        var source = new ContentItemSource(
            new TestCmsTree(301, 300, "AttachmentSet1"),
            LexonMigrationConstants.SourceClassNames.RiskAttachmentSet,
            LexonMigrationConstants.SourceClassNames.RiskAttachmentSet,
            null, null, [child1]
        );

        var actionProvider = new FakeActionProvider();
        director.Direct(source, actionProvider);

        Assert.Single(actionProvider.Links);
        Assert.Equal(LexonMigrationConstants.FieldNames.ResourceAreaDocuments, actionProvider.Links[0].fieldName);
        Assert.Equal([child1], actionProvider.Links[0].children);
    }

    // =====================================================================================
    // Intermediate child-linking tests (skips container sets)
    // =====================================================================================

    [Fact]
    public void Director_LinksResourceAreaRelatedResources_ViaIntermediateRiskLinkSet()
    {
        var classNames = new Dictionary<int, string>
        {
            [100] = LexonMigrationConstants.SourceClassNames.RiskItem,
            [500] = LexonMigrationConstants.SourceClassNames.RiskLinkSet
        };

        var intermediateNode = new TestCmsTree(501, 500, "LinkSet1");
        var grandChild = new TestCmsTree(102, 100, "RelatedRiskItem");

        var director = new LexonChildLinkDirector(
            id => classNames.GetValueOrDefault(id),
            LexonChildLinkRules.Rules,
            parentId => parentId == 501 ? [grandChild] : []);

        var source = new ContentItemSource(
            new TestCmsTree(1, 100, "RiskItem1"),
            LexonMigrationConstants.SourceClassNames.RiskItem,
            LexonMigrationConstants.SourceClassNames.RiskItem,
            null, null, [intermediateNode]
        );

        var actionProvider = new FakeActionProvider();
        director.Direct(source, actionProvider);

        var relatedLink = actionProvider.Links.First(l => l.fieldName == LexonMigrationConstants.FieldNames.ResourceAreaRelatedResources);
        Assert.Single(relatedLink.children);
        Assert.Contains(grandChild, relatedLink.children);
    }

    [Fact]
    public void Director_LinksResourceAreaUpdates_ViaIntermediateBulletinSet()
    {
        var classNames = new Dictionary<int, string>
        {
            [100] = LexonMigrationConstants.SourceClassNames.RiskItem,
            [600] = LexonMigrationConstants.SourceClassNames.BulletinSet,
            [700] = LexonMigrationConstants.SourceClassNames.Bulletin
        };

        var intermediateNode = new TestCmsTree(601, 600, "BulletinSet1");
        var grandChild = new TestCmsTree(701, 700, "Bulletin1");

        var director = new LexonChildLinkDirector(
            id => classNames.GetValueOrDefault(id),
            LexonChildLinkRules.Rules,
            parentId => parentId == 601 ? [grandChild] : []);

        var source = new ContentItemSource(
            new TestCmsTree(1, 100, "RiskItem1"),
            LexonMigrationConstants.SourceClassNames.RiskItem,
            LexonMigrationConstants.SourceClassNames.RiskItem,
            null, null, [intermediateNode]
        );

        var actionProvider = new FakeActionProvider();
        director.Direct(source, actionProvider);

        var updatesLink = actionProvider.Links.First(l => l.fieldName == LexonMigrationConstants.FieldNames.ResourceAreaUpdates);
        Assert.Single(updatesLink.children);
        Assert.Contains(grandChild, updatesLink.children);
    }


    // =====================================================================================
    // Drop intermediate container sets
    // =====================================================================================

    [Fact]
    public void Director_DropsRiskLinkSet_AsIntermediateContainer()
    {
        var director = new LexonChildLinkDirector(
            _ => LexonMigrationConstants.SourceClassNames.RiskLinkSet,
            LexonChildLinkRules.Rules,
            _ => []);

        var source = new ContentItemSource(
            new TestCmsTree(1, 500, "LinkSet1"),
            LexonMigrationConstants.SourceClassNames.RiskLinkSet,
            LexonMigrationConstants.SourceClassNames.RiskLinkSet,
            null, null, []
        );

        var actionProvider = new FakeActionProvider();
        director.Direct(source, actionProvider);

        Assert.True(actionProvider.Dropped);
        Assert.Empty(actionProvider.Links);
    }

    [Fact]
    public void Director_DropsBulletinSet_AsIntermediateContainer()
    {
        var director = new LexonChildLinkDirector(
            _ => LexonMigrationConstants.SourceClassNames.BulletinSet,
            LexonChildLinkRules.Rules,
            _ => []);

        var source = new ContentItemSource(
            new TestCmsTree(1, 600, "BulletinSet1"),
            LexonMigrationConstants.SourceClassNames.BulletinSet,
            LexonMigrationConstants.SourceClassNames.BulletinSet,
            null, null, []
        );

        var actionProvider = new FakeActionProvider();
        director.Direct(source, actionProvider);

        Assert.True(actionProvider.Dropped);
        Assert.Empty(actionProvider.Links);
    }


    // =====================================================================================
    // Edge cases
    // =====================================================================================

    [Fact]
    public void Director_DoesNotLink_WhenNoMatchingChildren()
    {
        var director = new LexonChildLinkDirector(
            _ => "Other.Class",
            LexonChildLinkRules.Rules,
            _ => []);

        var child1 = new TestCmsTree(201, 999);

        var source = new ContentItemSource(
            new TestCmsTree(101, 300),
            LexonMigrationConstants.SourceClassNames.RiskAttachmentSet,
            LexonMigrationConstants.SourceClassNames.RiskAttachmentSet,
            null, null, [child1]
        );

        var actionProvider = new FakeActionProvider();
        director.Direct(source, actionProvider);

        Assert.Empty(actionProvider.Links);
    }

    [Fact]
    public void Director_DoesNotLink_WhenChildNodesNull()
    {
        var director = new LexonChildLinkDirector(
            _ => LexonMigrationConstants.SourceClassNames.RiskAttachmentSet,
            LexonChildLinkRules.Rules,
            _ => []);

        var source = new ContentItemSource(
            new TestCmsTree(1, 100, "RiskItem1"),
            LexonMigrationConstants.SourceClassNames.RiskItem,
            LexonMigrationConstants.SourceClassNames.RiskItem,
            null, null, null
        );

        var actionProvider = new FakeActionProvider();
        director.Direct(source, actionProvider);

        Assert.Empty(actionProvider.Links);
    }


    [Fact]
    public void Director_ExecutesMultipleRules_ForSameParent()
    {
        var classNames = new Dictionary<int, string>
        {
            [100] = LexonMigrationConstants.SourceClassNames.RiskItem,
            [200] = LexonMigrationConstants.SourceClassNames.RiskAttachmentSet,
            [500] = LexonMigrationConstants.SourceClassNames.RiskLinkSet,
            [600] = LexonMigrationConstants.SourceClassNames.BulletinSet,
            [700] = LexonMigrationConstants.SourceClassNames.Bulletin
        };

        var attachmentSetChild = new TestCmsTree(201, 200, "AttachmentSet1");
        var linkSetChild = new TestCmsTree(501, 500, "LinkSet1");
        var bulletinSetChild = new TestCmsTree(601, 600, "BulletinSet1");
        var relatedRiskItem = new TestCmsTree(102, 100, "RelatedRiskItem");
        var bulletin = new TestCmsTree(701, 700, "Bulletin1");

        var director = new LexonChildLinkDirector(
            id => classNames.GetValueOrDefault(id),
            LexonChildLinkRules.Rules,
            parentId => parentId switch
            {
                501 => [relatedRiskItem],
                601 => [bulletin],
                _ => []
            });

        var source = new ContentItemSource(
            new TestCmsTree(1, 100, "RiskItem1"),
            LexonMigrationConstants.SourceClassNames.RiskItem,
            LexonMigrationConstants.SourceClassNames.RiskItem,
            null, null, [attachmentSetChild, linkSetChild, bulletinSetChild]
        );

        var actionProvider = new FakeActionProvider();
        director.Direct(source, actionProvider);

        Assert.Equal(3, actionProvider.Links.Count);

        Assert.Equal(LexonMigrationConstants.FieldNames.ResourceAreaDocumentGroups, actionProvider.Links[0].fieldName);
        Assert.Equal([attachmentSetChild], actionProvider.Links[0].children);

        Assert.Equal(LexonMigrationConstants.FieldNames.ResourceAreaRelatedResources, actionProvider.Links[1].fieldName);
        Assert.Equal([relatedRiskItem], actionProvider.Links[1].children);

        Assert.Equal(LexonMigrationConstants.FieldNames.ResourceAreaUpdates, actionProvider.Links[2].fieldName);
        Assert.Equal([bulletin], actionProvider.Links[2].children);
    }

    [Fact]
    public void Director_AcceptsCustomRules()
    {
        var customRules = new List<LexonChildLinkRule>
        {
            new(LexonMigrationConstants.SourceClassNames.RiskItem, LexonMigrationConstants.FieldNames.ResourceAreaDocumentGroups, LexonMigrationConstants.SourceClassNames.RiskAttachmentSet)
        };

        var classNames = new Dictionary<int, string>
        {
            [100] = LexonMigrationConstants.SourceClassNames.RiskItem,
            [200] = LexonMigrationConstants.SourceClassNames.RiskAttachmentSet
        };

        var director = new LexonChildLinkDirector(id => classNames.GetValueOrDefault(id), customRules);
        var child1 = new TestCmsTree(201, 200, "Set1");

        var source = new ContentItemSource(
            new TestCmsTree(1, 100, "RiskItem1"),
            LexonMigrationConstants.SourceClassNames.RiskItem,
            LexonMigrationConstants.SourceClassNames.RiskItem,
            null, null, [child1]
        );

        var actionProvider = new FakeActionProvider();
        director.Direct(source, actionProvider);

        Assert.Single(actionProvider.Links);
        Assert.Equal(LexonMigrationConstants.FieldNames.ResourceAreaDocumentGroups, actionProvider.Links[0].fieldName);
        Assert.Equal([child1], actionProvider.Links[0].children);
    }

    // =====================================================================================
    // Rules definition tests
    // =====================================================================================

    [Fact]
    public void Rules_ContainsFourEntries()
    {
        Assert.Equal(4, LexonChildLinkRules.Rules.Count);
    }

    [Fact]
    public void Rules_TwoHaveIntermediateClassName()
    {
        var intermediateRules = LexonChildLinkRules.Rules.Where(r => !string.IsNullOrEmpty(r.IntermediateClassName)).ToList();
        Assert.Equal(2, intermediateRules.Count);
        Assert.Contains(intermediateRules, r => r.IntermediateClassName == LexonMigrationConstants.SourceClassNames.RiskLinkSet);
        Assert.Contains(intermediateRules, r => r.IntermediateClassName == LexonMigrationConstants.SourceClassNames.BulletinSet);
    }

    [Fact]
    public void Rules_TwoAreDirectChildLinks()
    {
        var directRules = LexonChildLinkRules.Rules.Where(r => string.IsNullOrEmpty(r.IntermediateClassName)).ToList();
        Assert.Equal(2, directRules.Count);
        Assert.Contains(directRules, r => r.TargetFieldName == LexonMigrationConstants.FieldNames.ResourceAreaDocumentGroups);
        Assert.Contains(directRules, r => r.TargetFieldName == LexonMigrationConstants.FieldNames.ResourceAreaDocuments);
    }

    // =====================================================================================
    // DroppedSourceClassNames tests
    // =====================================================================================

    [Fact]
    public void DroppedSourceClassNames_ReturnsIntermediateContainerClasses()
    {
        var director = new LexonChildLinkDirector(_ => null, LexonChildLinkRules.Rules);
        var dropped = director.DroppedSourceClassNames.ToList();

        Assert.Equal(2, dropped.Count);
        Assert.Contains(LexonMigrationConstants.SourceClassNames.RiskLinkSet, dropped);
        Assert.Contains(LexonMigrationConstants.SourceClassNames.BulletinSet, dropped);
    }

    [Fact]
    public void DroppedSourceClassNames_ReturnsEmpty_WhenNoIntermediateRulesExist()
    {
        var directOnlyRules = LexonChildLinkRules.Rules.Where(r => string.IsNullOrEmpty(r.IntermediateClassName)).ToList();
        var director = new LexonChildLinkDirector(_ => null, directOnlyRules);

        Assert.Empty(director.DroppedSourceClassNames);
    }
}

