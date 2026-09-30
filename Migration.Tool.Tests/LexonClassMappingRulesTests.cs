using Microsoft.Extensions.DependencyInjection;
using Migration.Tool.Common.Abstractions;
using Migration.Tool.Common.Builders;
using Migration.Tool.Extensions.LexonMigrations;
using Migration.Tool.Extensions.LexonMigrations.ClassMappingRules;

namespace Migration.Tool.Tests;

public class LexonClassMappingRulesTests
{
    // =====================================================================================
    // Aggregate registration test
    // =====================================================================================

    [Fact]
    public void AddClassMappingRules_RegistersAllSixMappingsAndWrapperProvider()
    {
        var services = new ServiceCollection();
        services.AddClassMappingRules();
        var provider = services.BuildServiceProvider();
        var mappings = provider.GetServices<IClassMapping>().ToList();

        Assert.Equal(6, mappings.Count);
        Assert.Contains(mappings, m => m.TargetClassName == LexonMigrationConstants.TargetClassNames.AreaOfPracticeLandingPage);
        Assert.Contains(mappings, m => m.TargetClassName == LexonMigrationConstants.TargetClassNames.AreaOfPractice);
        Assert.Contains(mappings, m => m.TargetClassName == LexonMigrationConstants.TargetClassNames.ResourceArea);
        Assert.Contains(mappings, m => m.TargetClassName == LexonMigrationConstants.TargetClassNames.ResourceAreaDocumentGroup);
        Assert.Contains(mappings, m => m.TargetClassName == LexonMigrationConstants.TargetClassNames.ResourceAreaDocument);
        Assert.Contains(mappings, m => m.TargetClassName == LexonMigrationConstants.TargetClassNames.ResourceAreaUpdate);

        // Wrapper page rules are also registered by AddClassMappingRules
        var wrapperProvider = provider.GetService<IWrapperPageRuleProvider>();
        Assert.NotNull(wrapperProvider);
        Assert.Equal(2, wrapperProvider.GetRules().Count);

        // Child link rules are also registered by AddClassMappingRules
        var childLinkProvider = provider.GetService<IChildLinkRuleProvider>();
        Assert.NotNull(childLinkProvider);
        Assert.Equal(4, childLinkProvider.GetRules().Count);
    }

    // =====================================================================================
    // NavigationSectionMapping (-> AreaOfPracticeLandingPage)
    // =====================================================================================

    [Fact]
    public void NavigationSectionMapping_HasCorrectTarget()
    {
        IClassMapping mapping = NavigationSectionMapping.Create();

        Assert.Equal(LexonMigrationConstants.TargetClassNames.AreaOfPracticeLandingPage, mapping.TargetClassName);
        Assert.True(mapping.IsMatch(LexonMigrationConstants.SourceClassNames.NavigationSection));
        Assert.Equal("AreaOfPracticeLandingPageID", mapping.PrimaryKey);
    }

    // =====================================================================================
    // RiskItemSetMapping (-> AreaOfPractice)
    // =====================================================================================

    [Fact]
    public void RiskItemSetMapping_HasCorrectConfiguration()
    {
        IClassMapping mapping = RiskItemSetMapping.Create();

        Assert.Equal(LexonMigrationConstants.TargetClassNames.AreaOfPractice, mapping.TargetClassName);
        Assert.True(mapping.IsMatch(LexonMigrationConstants.SourceClassNames.RiskItemSet));
        Assert.Equal("AreaOfPracticeID", mapping.PrimaryKey);

        Assert.Equal(LexonMigrationConstants.FieldNames.Name,
            mapping.GetTargetFieldName(LexonMigrationConstants.SourceFieldNames.RiskSetName, LexonMigrationConstants.SourceClassNames.RiskItemSet));
        Assert.Equal(LexonMigrationConstants.FieldNames.Description,
            mapping.GetTargetFieldName(LexonMigrationConstants.SourceFieldNames.Description, LexonMigrationConstants.SourceClassNames.RiskItemSet));
        Assert.Equal(LexonMigrationConstants.FieldNames.Image,
            mapping.GetTargetFieldName(LexonMigrationConstants.SourceFieldNames.ImagePath, LexonMigrationConstants.SourceClassNames.RiskItemSet));

        Assert.Contains(LexonMigrationConstants.FieldNames.Name, mapping.TargetFieldPatchers.Keys);
        Assert.Contains(LexonMigrationConstants.FieldNames.Description, mapping.TargetFieldPatchers.Keys);
        Assert.Contains(LexonMigrationConstants.FieldNames.Image, mapping.TargetFieldPatchers.Keys);
    }

    [Fact]
    public void RiskItemSetMapping_TargetMatchesWrapperRuleReusableType()
    {
        IClassMapping mapping = RiskItemSetMapping.Create();
        var rule = RiskItemSetMapping.WrapperRule;

        Assert.Equal(mapping.TargetClassName, rule.ReusableContentTypeName);
        Assert.NotEqual(mapping.TargetClassName, rule.WrapperClassName);
    }

    // =====================================================================================
    // RiskItemMapping (-> ResourceArea)
    // =====================================================================================

    [Fact]
    public void RiskItemMapping_HasCorrectConfiguration()
    {
        IClassMapping mapping = RiskItemMapping.Create();

        Assert.Equal(LexonMigrationConstants.TargetClassNames.ResourceArea, mapping.TargetClassName);
        Assert.True(mapping.IsMatch(LexonMigrationConstants.SourceClassNames.RiskItem));
        Assert.Equal("ResourceAreaID", mapping.PrimaryKey);

        Assert.Equal(LexonMigrationConstants.FieldNames.Title,
            mapping.GetTargetFieldName(LexonMigrationConstants.SourceFieldNames.ArticleTitle, LexonMigrationConstants.SourceClassNames.RiskItem));
        Assert.Equal(LexonMigrationConstants.FieldNames.Summary,
            mapping.GetTargetFieldName(LexonMigrationConstants.SourceFieldNames.ArticleSummary, LexonMigrationConstants.SourceClassNames.RiskItem));
        Assert.Equal(LexonMigrationConstants.FieldNames.Text,
            mapping.GetTargetFieldName(LexonMigrationConstants.SourceFieldNames.ArticleText, LexonMigrationConstants.SourceClassNames.RiskItem));
        Assert.Equal(LexonMigrationConstants.FieldNames.Teaser,
            mapping.GetTargetFieldName(LexonMigrationConstants.SourceFieldNames.ArticleTeaser, LexonMigrationConstants.SourceClassNames.RiskItem));
        Assert.Equal(LexonMigrationConstants.FieldNames.Image,
            mapping.GetTargetFieldName(LexonMigrationConstants.SourceFieldNames.ImagePath, LexonMigrationConstants.SourceClassNames.RiskItem));

        Assert.Contains(LexonMigrationConstants.FieldNames.Title, mapping.TargetFieldPatchers.Keys);
        Assert.Contains(LexonMigrationConstants.FieldNames.Summary, mapping.TargetFieldPatchers.Keys);
        Assert.Contains(LexonMigrationConstants.FieldNames.Text, mapping.TargetFieldPatchers.Keys);
        Assert.Contains(LexonMigrationConstants.FieldNames.Teaser, mapping.TargetFieldPatchers.Keys);
        Assert.Contains(LexonMigrationConstants.FieldNames.Image, mapping.TargetFieldPatchers.Keys);

        // Child-link reference fields kept during mapping
        Assert.Contains(LexonMigrationConstants.FieldNames.ResourceAreaDocumentGroups, mapping.TargetFieldPatchers.Keys);
        Assert.Contains(LexonMigrationConstants.FieldNames.ResourceAreaRelatedResources, mapping.TargetFieldPatchers.Keys);
        Assert.Contains(LexonMigrationConstants.FieldNames.ResourceAreaUpdates, mapping.TargetFieldPatchers.Keys);
    }

    [Fact]
    public void RiskItemMapping_HasChildLinkRules()
    {
        var rules = RiskItemMapping.ChildLinkRules;
        Assert.Equal(3, rules.Count);
        Assert.Contains(rules, r => r.TargetFieldName == LexonMigrationConstants.FieldNames.ResourceAreaDocumentGroups &&
                                    r.ChildClassName == LexonMigrationConstants.SourceClassNames.RiskAttachmentSet);
        Assert.Contains(rules, r => r.TargetFieldName == LexonMigrationConstants.FieldNames.ResourceAreaRelatedResources &&
                                    r.ChildClassName == LexonMigrationConstants.SourceClassNames.RiskItem &&
                                    r.IntermediateClassName == LexonMigrationConstants.SourceClassNames.RiskLinkSet);
        Assert.Contains(rules, r => r.TargetFieldName == LexonMigrationConstants.FieldNames.ResourceAreaUpdates &&
                                    r.ChildClassName == LexonMigrationConstants.SourceClassNames.Bulletin &&
                                    r.IntermediateClassName == LexonMigrationConstants.SourceClassNames.BulletinSet);
    }

    [Fact]
    public void RiskItemMapping_TargetMatchesWrapperRuleReusableType()
    {
        IClassMapping mapping = RiskItemMapping.Create();
        var rule = RiskItemMapping.WrapperRule;

        Assert.Equal(mapping.TargetClassName, rule.ReusableContentTypeName);
        Assert.NotEqual(mapping.TargetClassName, rule.WrapperClassName);
    }


    // =====================================================================================
    // RiskAttachmentSetMapping (-> ResourceAreaDocumentGroup)
    // =====================================================================================

    [Fact]
    public void RiskAttachmentSetMapping_HasCorrectConfiguration()
    {
        IClassMapping mapping = RiskAttachmentSetMapping.Create();

        Assert.Equal(LexonMigrationConstants.TargetClassNames.ResourceAreaDocumentGroup, mapping.TargetClassName);
        Assert.True(mapping.IsMatch(LexonMigrationConstants.SourceClassNames.RiskAttachmentSet));
        Assert.Equal("ResourceAreaDocumentGroupID", mapping.PrimaryKey);

        Assert.Equal(LexonMigrationConstants.FieldNames.Name,
            mapping.GetTargetFieldName(LexonMigrationConstants.SourceFieldNames.RiskSetName, LexonMigrationConstants.SourceClassNames.RiskAttachmentSet));
        Assert.Equal(LexonMigrationConstants.FieldNames.Description,
            mapping.GetTargetFieldName(LexonMigrationConstants.SourceFieldNames.Description, LexonMigrationConstants.SourceClassNames.RiskAttachmentSet));
        Assert.Equal(LexonMigrationConstants.FieldNames.MediaLibraryFolder,
            mapping.GetTargetFieldName(LexonMigrationConstants.SourceFieldNames.MLFolder, LexonMigrationConstants.SourceClassNames.RiskAttachmentSet));

        Assert.Contains(LexonMigrationConstants.FieldNames.Name, mapping.TargetFieldPatchers.Keys);
        Assert.Contains(LexonMigrationConstants.FieldNames.Description, mapping.TargetFieldPatchers.Keys);
        Assert.Contains(LexonMigrationConstants.FieldNames.MediaLibraryFolder, mapping.TargetFieldPatchers.Keys);

        // Child-link reference field kept during mapping
        Assert.Contains(LexonMigrationConstants.FieldNames.ResourceAreaDocuments, mapping.TargetFieldPatchers.Keys);
    }

    [Fact]
    public void RiskAttachmentSetMapping_HasChildLinkRules()
    {
        var rules = RiskAttachmentSetMapping.ChildLinkRules;
        Assert.Single(rules);
        Assert.Equal(LexonMigrationConstants.FieldNames.ResourceAreaDocuments, rules[0].TargetFieldName);
        Assert.Equal(LexonMigrationConstants.SourceClassNames.RiskAttachment, rules[0].ChildClassName);
    }

    // =====================================================================================
    // RiskAttachmentMapping (-> ResourceAreaDocument)
    // =====================================================================================

    [Fact]
    public void RiskAttachmentMapping_HasCorrectConfiguration()
    {
        IClassMapping mapping = RiskAttachmentMapping.Create();

        Assert.Equal(LexonMigrationConstants.TargetClassNames.ResourceAreaDocument, mapping.TargetClassName);
        Assert.True(mapping.IsMatch(LexonMigrationConstants.SourceClassNames.RiskAttachment));
        Assert.Equal("ResourceAreaDocumentID", mapping.PrimaryKey);

        Assert.Equal(LexonMigrationConstants.FieldNames.FileName,
            mapping.GetTargetFieldName(LexonMigrationConstants.SourceFieldNames.FileName, LexonMigrationConstants.SourceClassNames.RiskAttachment));
        Assert.Equal(LexonMigrationConstants.FieldNames.Attachment,
            mapping.GetTargetFieldName(LexonMigrationConstants.SourceFieldNames.Attachment, LexonMigrationConstants.SourceClassNames.RiskAttachment));
        Assert.Equal(LexonMigrationConstants.FieldNames.Description,
            mapping.GetTargetFieldName(LexonMigrationConstants.SourceFieldNames.Description, LexonMigrationConstants.SourceClassNames.RiskAttachment));

        Assert.Contains(LexonMigrationConstants.FieldNames.FileName, mapping.TargetFieldPatchers.Keys);
        Assert.Contains(LexonMigrationConstants.FieldNames.Attachment, mapping.TargetFieldPatchers.Keys);
        Assert.Contains(LexonMigrationConstants.FieldNames.Description, mapping.TargetFieldPatchers.Keys);
    }


    // =====================================================================================
    // BulletinMapping (-> ResourceAreaUpdate)
    // =====================================================================================

    [Fact]
    public void BulletinMapping_HasCorrectConfiguration()
    {
        IClassMapping mapping = BulletinMapping.Create();

        Assert.Equal(LexonMigrationConstants.TargetClassNames.ResourceAreaUpdate, mapping.TargetClassName);
        Assert.True(mapping.IsMatch(LexonMigrationConstants.SourceClassNames.Bulletin));
        Assert.Equal("ResourceAreaUpdateID", mapping.PrimaryKey);

        Assert.Equal(LexonMigrationConstants.FieldNames.Title,
            mapping.GetTargetFieldName(LexonMigrationConstants.SourceFieldNames.Title, LexonMigrationConstants.SourceClassNames.Bulletin));
        Assert.Equal(LexonMigrationConstants.FieldNames.Summary,
            mapping.GetTargetFieldName(LexonMigrationConstants.SourceFieldNames.Summary, LexonMigrationConstants.SourceClassNames.Bulletin));
        Assert.Equal(LexonMigrationConstants.FieldNames.Text,
            mapping.GetTargetFieldName(LexonMigrationConstants.SourceFieldNames.Body, LexonMigrationConstants.SourceClassNames.Bulletin));
        Assert.Equal(LexonMigrationConstants.FieldNames.Date,
            mapping.GetTargetFieldName(LexonMigrationConstants.SourceFieldNames.DateOfItem, LexonMigrationConstants.SourceClassNames.Bulletin));
        Assert.Equal(LexonMigrationConstants.FieldNames.Image,
            mapping.GetTargetFieldName(LexonMigrationConstants.SourceFieldNames.TeaserImage, LexonMigrationConstants.SourceClassNames.Bulletin));
        Assert.Equal(LexonMigrationConstants.FieldNames.Attachment,
            mapping.GetTargetFieldName(LexonMigrationConstants.SourceFieldNames.Attachments, LexonMigrationConstants.SourceClassNames.Bulletin));

        Assert.Contains(LexonMigrationConstants.FieldNames.Title, mapping.TargetFieldPatchers.Keys);
        Assert.Contains(LexonMigrationConstants.FieldNames.Summary, mapping.TargetFieldPatchers.Keys);
        Assert.Contains(LexonMigrationConstants.FieldNames.Text, mapping.TargetFieldPatchers.Keys);
        Assert.Contains(LexonMigrationConstants.FieldNames.Date, mapping.TargetFieldPatchers.Keys);
        Assert.Contains(LexonMigrationConstants.FieldNames.Image, mapping.TargetFieldPatchers.Keys);
        Assert.Contains(LexonMigrationConstants.FieldNames.Attachment, mapping.TargetFieldPatchers.Keys);
    }

    // =====================================================================================
    // ClassRenameMapping helper tests
    // =====================================================================================

    [Fact]
    public void AllRegisteredMappings_HaveSourceClassNamesPopulated()
    {
        var services = new ServiceCollection();
        services.AddClassMappingRules();
        var provider = services.BuildServiceProvider();
        var mappings = provider.GetServices<IClassMapping>().ToList();

        foreach (var mapping in mappings)
        {
            Assert.NotEmpty(mapping.SourceClassNames);
        }
    }

    [Fact]
    public void NavigationSectionMapping_RegistersSourceClassName()
    {
        IClassMapping mapping = NavigationSectionMapping.Create();
        Assert.Contains(LexonMigrationConstants.SourceClassNames.NavigationSection, mapping.SourceClassNames);
    }

    [Theory]
    [InlineData("Lexon.Attachment", "Lexon_Attachment")]
    [InlineData("Lexon.ResourceAreaDocument", "Lexon_ResourceAreaDocument")]
    [InlineData("NoNamespace", "NoNamespace")]
    public void DeriveTableName_ProducesExpectedResult(string className, string expected)
    {
        Assert.Equal(expected, ClassRenameMapping.DeriveTableName(className));
    }
}

