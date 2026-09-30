using CMS.DataEngine;
using CMS.FormEngine;
using Migration.Tool.Common.Abstractions;
using Migration.Tool.Common.Helpers;
using Migration.Tool.Extensions.LexonMigrations;
using Migration.Tool.Extensions.LexonMigrations.ClassMappingRules;
using Migration.Tool.KXP.Api.Auxiliary;
using Migration.Tool.KXP.Api.Services.CmsClass;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Migration.Tool.Tests;

public class LexonWrapperPageRulesTests
{
    [Fact]
    public void WrapperPageRuleProvider_ResolvesRegisteredRule_BySourceClassName()
    {
        var provider = new WrapperPageRuleProvider(LexonClassMappingRules.WrapperRules);

        bool found = provider.TryGetRule(LexonMigrationConstants.SourceClassNames.RiskItemSet, out var rule);

        Assert.True(found);
        Assert.NotNull(rule);
        Assert.Equal(LexonMigrationConstants.SourceClassNames.RiskItemSet, rule.SourceClassName);
        Assert.Equal(LexonMigrationConstants.TargetClassNames.AreaOfPractice, rule.ReusableContentTypeName);
        Assert.Equal(LexonMigrationConstants.TargetClassNames.AreaOfPracticePage, rule.WrapperClassName);
        Assert.Equal("Area of Practice Page", rule.WrapperDisplayName);
        Assert.Equal(LexonMigrationConstants.FieldNames.ContentItem, rule.ContentItemReferenceField);
        Assert.False(rule.IncludeLegacyMetadata);
    }

    [Fact]
    public void WrapperPageRuleProvider_IsCaseInsensitive()
    {
        var provider = new WrapperPageRuleProvider(LexonClassMappingRules.WrapperRules);

        // SourceClassNames.RiskItemSet is "Lexon.riskitemset" — look up with different casing
        bool found = provider.TryGetRule("LEXON.RISKITEMSET", out var rule);

        Assert.True(found);
        Assert.NotNull(rule);
        Assert.Equal(LexonMigrationConstants.TargetClassNames.AreaOfPracticePage, rule.WrapperClassName);
    }

    [Fact]
    public void WrapperPageRuleProvider_ResolvesRegisteredRule_ByWrapperClassName()
    {
        var provider = new WrapperPageRuleProvider(LexonClassMappingRules.WrapperRules);

        bool found = provider.TryGetRuleByWrapper(LexonMigrationConstants.TargetClassNames.AreaOfPracticePage, out var rule);

        Assert.True(found);
        Assert.NotNull(rule);
        Assert.Equal(LexonMigrationConstants.SourceClassNames.RiskItemSet, rule.SourceClassName);
    }

    [Fact]
    public void WrapperPageRuleProvider_ResolvesResourceAreaRule_BySourceClassName()
    {
        var provider = new WrapperPageRuleProvider(LexonClassMappingRules.WrapperRules);

        bool found = provider.TryGetRule(LexonMigrationConstants.SourceClassNames.RiskItem, out var rule);

        Assert.True(found);
        Assert.NotNull(rule);
        Assert.Equal(LexonMigrationConstants.SourceClassNames.RiskItem, rule.SourceClassName);
        Assert.Equal(LexonMigrationConstants.TargetClassNames.ResourceArea, rule.ReusableContentTypeName);
        Assert.Equal(LexonMigrationConstants.TargetClassNames.ResourceAreaPage, rule.WrapperClassName);
        Assert.Equal("Resource Area Page", rule.WrapperDisplayName);
        Assert.Equal(LexonMigrationConstants.FieldNames.ContentItem, rule.ContentItemReferenceField);
        Assert.False(rule.IncludeLegacyMetadata);
    }

    [Fact]
    public void WrapperPageRuleProvider_ResolvesResourceAreaRule_ByWrapperClassName()
    {
        var provider = new WrapperPageRuleProvider(LexonClassMappingRules.WrapperRules);

        bool found = provider.TryGetRuleByWrapper(LexonMigrationConstants.TargetClassNames.ResourceAreaPage, out var rule);

        Assert.True(found);
        Assert.NotNull(rule);
        Assert.Equal(LexonMigrationConstants.SourceClassNames.RiskItem, rule.SourceClassName);
    }

    [Fact]
    public void WrapperPageRuleProvider_ReturnsFalse_ForUnregisteredSourceClass()
    {
        var provider = new WrapperPageRuleProvider(LexonClassMappingRules.WrapperRules);

        bool found = provider.TryGetRule("Some.UnknownClass", out var rule);

        Assert.False(found);
        Assert.Null(rule);
    }

    [Fact]
    public void WrapperPageRule_IncludeLegacyMetadata_DefaultsToTrue()
    {
        var rule = new WrapperPageRule(
            SourceClassName: "Test.Source",
            ReusableContentTypeName: "Test.Reusable",
            WrapperClassName: "Test.Wrapper",
            WrapperDisplayName: "Test Wrapper"
        );

        Assert.True(rule.IncludeLegacyMetadata);
    }

    [Fact]
    public void ReusableGuid_IsDeterministic_AndDifferentFromWrapperGuid()
    {
        var wrapperGuid = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var reusableGuid1 = GuidHelper.CreateContentItemGuid($"reusable|{wrapperGuid}");
        var reusableGuid2 = GuidHelper.CreateContentItemGuid($"reusable|{wrapperGuid}");

        Assert.Equal(reusableGuid1, reusableGuid2);
        Assert.NotEqual(wrapperGuid, reusableGuid1);
    }

    [Fact]
    public void WrapperCommonDataGuid_IsDeterministic()
    {
        var documentGuid = Guid.Parse("99999999-8888-7777-6666-555555555555");
        var guid1 = GuidHelper.CreateContentItemCommonDataGuid($"wrapper-commondata|{documentGuid}");
        var guid2 = GuidHelper.CreateContentItemCommonDataGuid($"wrapper-commondata|{documentGuid}");

        Assert.Equal(guid1, guid2);
        Assert.NotEqual(documentGuid, guid1);
    }


    [Fact]
    public void WrapperContentItemReferenceField_SerializesValidJsonArray()
    {
        var reusableItemGuid = Guid.Parse("A1B2C3D4-E5F6-7890-ABCD-EF1234567890");
        string serialized = JsonConvert.SerializeObject(new[] { new { Identifier = reusableItemGuid } });

        var jArray = JArray.Parse(serialized);
        Assert.Single(jArray);
        Assert.Equal(reusableItemGuid.ToString(), jArray[0]["Identifier"]?.ToString());
    }

    [Fact]
    public void ContentItemSelectorField_HasCorrectConfiguration()
    {
        var reusableClassGuid = Guid.NewGuid();
        var rule = RiskItemSetMapping.WrapperRule;

        var refField = new FormFieldInfo
        {
            Guid = GuidHelper.CreateFieldGuid($"{rule.ContentItemReferenceField.ToLowerInvariant()}|{rule.WrapperClassName}"),
            AllowEmpty = true,
            Caption = "Content item",
            DataType = FieldDataType.ContentItemReference,
            Name = rule.ContentItemReferenceField,
            Enabled = true,
            Visible = true,
            System = false,
            DefaultValue = null,
            Settings =
            {
                { FormDefinitionPatcher.SettingsElemControlname, FormComponents.AdminContentItemSelectorComponent },
                { FormDefinitionPatcher.AllowedContentItemTypeIdentifiers, JsonConvert.SerializeObject(new[] { reusableClassGuid }) },
                { FormDefinitionPatcher.SettingsMaximumpages, "1" }
            }
        };

        Assert.Equal("ContentItem", refField.Name);
        Assert.Equal(FieldDataType.ContentItemReference, refField.DataType);
        Assert.Equal("Kentico.Administration.ContentItemSelector", refField.Settings[FormDefinitionPatcher.SettingsElemControlname]);
        Assert.Equal("1", refField.Settings[FormDefinitionPatcher.SettingsMaximumpages]);

        string? allowedSerialized = refField.Settings[FormDefinitionPatcher.AllowedContentItemTypeIdentifiers] as string;
        Assert.NotNull(allowedSerialized);
        var allowedTypes = JsonConvert.DeserializeObject<Guid[]>(allowedSerialized);
        Assert.NotNull(allowedTypes);
        Assert.Single(allowedTypes);
        Assert.Equal(reusableClassGuid, allowedTypes[0]);
    }

    [Fact]
    public void WrapperRules_ContainsExpectedRuleCount()
    {
        Assert.Equal(2, LexonClassMappingRules.WrapperRules.Count);
    }
}

