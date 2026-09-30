using CMS.FormEngine;
using Migration.Tool.Common.Abstractions;
using Migration.Tool.Extensions.LexonMigrations;
using Migration.Tool.KXP.Api.Auxiliary;
using Migration.Tool.KXP.Api.Services.CmsClass;

namespace Migration.Tool.Tests;

public class LexonChildLinkRulesTests
{
    [Fact]
    public void ChildLinkRuleProvider_ResolvesAllRegisteredRules()
    {
        var provider = new ChildLinkRuleProvider(LexonClassMappingRules.ChildLinkRules);
        var rules = provider.GetRules();

        Assert.Equal(4, rules.Count);
        Assert.Contains(rules, r => r.TargetFieldName == LexonMigrationConstants.FieldNames.ResourceAreaDocumentGroups);
        Assert.Contains(rules, r => r.TargetFieldName == LexonMigrationConstants.FieldNames.ResourceAreaDocuments);
        Assert.Contains(rules, r => r.TargetFieldName == LexonMigrationConstants.FieldNames.ResourceAreaRelatedResources);
        Assert.Contains(rules, r => r.TargetFieldName == LexonMigrationConstants.FieldNames.ResourceAreaUpdates);
    }

    [Fact]
    public void ChildLinkRuleProvider_GetRulesForParent_IsCaseInsensitive()
    {
        var provider = new ChildLinkRuleProvider(LexonClassMappingRules.ChildLinkRules);

        var riskItemRules = provider.GetRulesForParent("lexon.riskitem");
        Assert.Equal(3, riskItemRules.Count);

        var attachmentSetRules = provider.GetRulesForParent("LEXON.RISKATTACHMENTSET");
        Assert.Single(attachmentSetRules);
        Assert.Equal(LexonMigrationConstants.FieldNames.ResourceAreaDocuments, attachmentSetRules[0].TargetFieldName);
    }

    [Fact]
    public void ChildLinkRuleProvider_ReturnsEmpty_ForUnknownParent()
    {
        var provider = new ChildLinkRuleProvider(LexonClassMappingRules.ChildLinkRules);

        var unknownRules = provider.GetRulesForParent("Lexon.UnknownClass");
        Assert.Empty(unknownRules);
    }

    [Fact]
    public void FormFieldInfo_VisibleAndSettings_SerializesVisibleAttribute()
    {
        const string initialXml = "<form><field column=\"ResourceAreaDocumentGroups\" columntype=\"contentitemreference\" enabled=\"true\" /></form>";
        var fi = new FormInfo(initialXml);
        var field = fi.GetFormField("ResourceAreaDocumentGroups");
        Assert.NotNull(field);
        Assert.False(field.Visible);

        field.Visible = true;
        field.Enabled = true;
        field.Settings[FormDefinitionPatcher.SettingsElemControlname] = FormComponents.AdminContentItemSelectorComponent;

        string updatedXml = fi.GetXmlDefinition();
        Assert.Contains("visible=\"true\"", updatedXml);
        Assert.Contains(FormComponents.AdminContentItemSelectorComponent, updatedXml);
    }

    [Fact]
    public void RiskItemMapping_ChildLinkFieldPatchers_SetVisibleAndFormComponent()
    {
        var mapping = Migration.Tool.Extensions.LexonMigrations.ClassMappingRules.RiskItemMapping.Create();
        string[] childLinkFields = [
            LexonMigrationConstants.FieldNames.ResourceAreaDocumentGroups,
            LexonMigrationConstants.FieldNames.ResourceAreaRelatedResources,
            LexonMigrationConstants.FieldNames.ResourceAreaUpdates
        ];

        foreach (string fieldName in childLinkFields)
        {
            Assert.Contains(fieldName, mapping.TargetFieldPatchers.Keys);
            var formField = new FormFieldInfo { Name = fieldName, Visible = false, Enabled = false };
            mapping.TargetFieldPatchers[fieldName].Invoke(formField);

            Assert.True(formField.Visible);
            Assert.True(formField.Enabled);
            Assert.Equal(FormComponents.AdminContentItemSelectorComponent, formField.Settings[FormDefinitionPatcher.SettingsElemControlname]);
        }
    }

    [Fact]
    public void RiskAttachmentSetMapping_ChildLinkFieldPatcher_SetsVisibleAndFormComponent()
    {
        var mapping = Migration.Tool.Extensions.LexonMigrations.ClassMappingRules.RiskAttachmentSetMapping.Create();
        string fieldName = LexonMigrationConstants.FieldNames.ResourceAreaDocuments;

        Assert.Contains(fieldName, mapping.TargetFieldPatchers.Keys);
        var formField = new FormFieldInfo { Name = fieldName, Visible = false, Enabled = false };
        mapping.TargetFieldPatchers[fieldName].Invoke(formField);

        Assert.True(formField.Visible);
        Assert.True(formField.Enabled);
        Assert.Equal("Documents", formField.Caption);
        Assert.Equal(FormComponents.AdminContentItemSelectorComponent, formField.Settings[FormDefinitionPatcher.SettingsElemControlname]);
    }
}


