using CMS.ContentEngine;
using CMS.ContentEngine.Internal;
using CMS.Core;
using CMS.DataEngine;
using CMS.FormEngine;
using Kentico.Xperience.UMT.Model;
using Kentico.Xperience.UMT.Services;
using MediatR;

using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

using Migration.Tool.Common;
using Migration.Tool.Common.Abstractions;
using Migration.Tool.Common.Builders;
using Migration.Tool.Common.Enumerations;
using Migration.Tool.Common.Helpers;
using Migration.Tool.Common.MigrationProtocol;
using Migration.Tool.KXP.Api;
using Migration.Tool.KXP.Api.Auxiliary;
using Migration.Tool.KXP.Api.Services.CmsClass;
using Migration.Tool.Source.Contexts;
using Migration.Tool.Source.Helpers;
using Migration.Tool.Source.Mappers;
using Migration.Tool.Source.Mappers.ContentItemMapperDirectives;
using Migration.Tool.Source.Model;
using Migration.Tool.Source.Providers;
using Migration.Tool.Source.Services;
using Newtonsoft.Json;

namespace Migration.Tool.Source.Handlers;

/// <summary>
/// =========================================================================================
/// FLOW 1: MIGRATE PAGE TYPE -> CONTENT TYPE (CLI: migrate --page-types)
/// =========================================================================================
/// This handler orchestrates the migration of legacy Kentico 12/13 Page Types (CMS_Class where
/// ClassIsDocumentType = 1) into Xperience by Kentico (XbyK) Content Types.
///
/// Execution Stages:
///   1. Query source page types from CMS_Class (ordered and deferrable for inheritance).
///   2. Apply exclusions (CMS.Root, CMS.Site, appsettings ExcludeCodeNames).
///   3. Resolve inheritance dependencies (defer derived types until parent types exist).
///   4. Map schema & form definitions (CmsClassMapper) into XbyK DataClassInfo:
///        - ClassContentTypeType.REUSABLE (Content Hub item) if listed in ClassNamesConvertToContentHub
///        - ClassContentTypeType.WEBSITE (Web page item) otherwise
///   5. Save to target database via KXP Class Facade.
///   6. Channel assignment: For WEBSITE types, link to channels via ContentTypeChannelInfo.
///   7. Migrate Page Template configurations.
///   8. Bypass or configure allowed child content types.
/// =========================================================================================
/// </summary>
public class MigratePageTypesCommandHandler(
    ILogger<MigratePageTypesCommandHandler> logger,
    IEntityMapper<ICmsClass, DataClassInfo> dataClassMapper,
    PrimaryKeyMappingContext primaryKeyMappingContext,
    KxpClassFacade kxpClassFacade,
    IProtocol protocol,
    ToolConfiguration toolConfiguration,
    ModelFacade modelFacade,
    PageTemplateMigrator pageTemplateMigrator,
    ReusableSchemaService reusableSchemaService,
    ClassMappingProvider classMappingProvider,
    IImporter importer,
    IEnumerable<IReusableSchemaBuilder> reusableSchemaBuilders,
    IWrapperPageRuleProvider? wrapperRuleProvider = null,
    IChildLinkRuleProvider? childLinkRuleProvider = null,
    IEnumerable<ContentItemDirectorBase>? directors = null
    )
    : IRequestHandler<MigratePageTypesCommand, CommandResult>
{
    private const string CLASS_CMS_ROOT = "CMS.Root";

    public async Task<CommandResult> Handle(MigratePageTypesCommand request, CancellationToken cancellationToken)
    {
        var entityConfiguration = toolConfiguration.EntityConfigurations.GetEntityConfiguration<DataClassInfo>();

        // ---------------------------------------------------------------------------------
        // STAGE 1: Load all source Page Types (CMS_Class where ClassIsDocumentType = 1)
        // Wrapped in DeferrableItemWrapper so classes whose inheritance parents are not yet
        // migrated can be deferred and re-evaluated at the end of the loop.
        // ---------------------------------------------------------------------------------
        using var ksClasses = EnumerableHelper.CreateDeferrableItemWrapper(
            modelFacade.Select<ICmsClass>("ClassIsDocumentType=1", "ClassID")
                .OrderBy(x => x.ClassID)
        );

        // Check for custom manual class mappings or reusable schema builders
        var manualMappings = classMappingProvider.ExecuteMappings();
        var manuallyMappedSourceClassIDs = new HashSet<int>();
        var manuallyMappedSourceClassNames = manualMappings.Values.SelectMany(x => x.mappping.SourceClassNames).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var droppedClassNames = (directors ?? Enumerable.Empty<ContentItemDirectorBase>())
            .SelectMany(d => d.DroppedSourceClassNames)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (reusableSchemaBuilders.Any() && !string.IsNullOrWhiteSpace(toolConfiguration.CreateReusableFieldSchemaForClasses))
        {
            logger.LogError("Conversion to reusable field schema using appsettings configuration " +
                                "is not allowed when custom class mapping reusable schema builders are " +
                                "used. Use one option or the other. Terminating migration of page types.");
            return new CommandFailureResult();
        }

        // ---------------------------------------------------------------------------------
        // STAGE 2: Iterate and process each page type
        // ---------------------------------------------------------------------------------
        while (ksClasses.GetNext(out var di))
        {
            var (_, ksClass) = di;

            if (manuallyMappedSourceClassNames.Contains(ksClass.ClassName))
            {
                manuallyMappedSourceClassIDs.Add(ksClass.ClassID);
            }

            // Skip classes handled by custom manual class mappings
            if (manualMappings.ContainsKey(ksClass.ClassName))
            {
                continue;
            }

            // Skip intermediate container classes dropped by content item directors
            if (droppedClassNames.Contains(ksClass.ClassName))
            {
                logger.LogInformation("Page type: class {ClassName} was skipped => it is an intermediate container dropped by content item director", ksClass.ClassName);
                continue;
            }

            // Skip classes explicitly excluded in appsettings.json
            if (entityConfiguration.ExcludeCodeNames.Contains(ksClass.ClassName, StringComparer.InvariantCultureIgnoreCase))
            {
                logger.LogInformation("Class {ClassName} explicitly excluded in appsettings", ksClass.ClassName);
                continue;
            }

            // Skip classes converted via reusable schema builders without custom mapping
            var producedReusableSchemas = reusableSchemaBuilders.Where(x =>
                string.Equals(x.SourceClassName, ksClass.ClassName, StringComparison.InvariantCultureIgnoreCase)).ToArray();
            if (producedReusableSchemas.Length != 0)  // and no custom mapping is registered for this class, but that is assured by one of the previous guards
            {
                logger.LogInformation("Class {ClassName} skipped. The class is converted to the following reusable field schemas: {ReusableFieldSchemas}. " +
                                      "In such situation only custom class mapping of the class is supported and no mapping was provided.",
                    ksClass.ClassName, string.Join(", ", producedReusableSchemas.Select(x => x.SchemaName).ToArray()));
                continue;
            }

            // ---------------------------------------------------------------------------------
            // STAGE 3: Class Inheritance Dependency Check
            // If this class inherits from another class (ClassInheritsFromClassID), ensure the
            // parent class has already been migrated in the target instance. If not, defer this
            // class to be re-evaluated later once its parent is created.
            // ---------------------------------------------------------------------------------
            if (ksClass.ClassInheritsFromClassID is { } classInheritsFromClassId && !(primaryKeyMappingContext.HasMapping<ICmsClass>(c => c.ClassID, classInheritsFromClassId) || manuallyMappedSourceClassIDs.Contains(classInheritsFromClassId)))
            {
                // Defer migration to later stage
                if (ksClasses.TryDeferItem(di))
                {
                    logger.LogTrace("Class {Class} inheritance parent not found, deferring migration to end. Attempt {Attempt}", Printer.GetEntityIdentityPrint(ksClass), di.Recurrence);
                }
                else
                {
                    logger.LogErrorMissingDependency(ksClass, nameof(ksClass.ClassInheritsFromClassID), ksClass.ClassInheritsFromClassID, typeof(DataClassInfo));
                    protocol.Append(HandbookReferences
                        .MissingRequiredDependency<DataClassInfo>(nameof(DataClassInfo.ClassID), classInheritsFromClassId)
                        .NeedsManualAction()
                    );
                }

                continue;
            }

            protocol.FetchedSource(ksClass);

            // Filter out system classes that cannot or should not be migrated as page types
            if (string.Equals(ksClass.ClassName, CLASS_CMS_ROOT, StringComparison.InvariantCultureIgnoreCase))
            {
                protocol.Warning(HandbookReferences.CmsClassCmsRootClassTypeSkip, ksClass);
                logger.LogInformation("CmsClass: {ClassName} was skipped => CMS.Root cannot be migrated", ksClass.ClassName);
                continue;
            }

            if (string.Equals(ksClass.ClassName, "cms.site", StringComparison.InvariantCultureIgnoreCase))
            {
                continue;
            }

            // Handle legacy CMS.Folder (skipped unless UseDeprecatedFolderPageType is explicitly true)
            if (ksClass.ClassName.Equals("cms.folder", StringComparison.InvariantCultureIgnoreCase))
            {
                if (!toolConfiguration.UseDeprecatedFolderPageType.GetValueOrDefault(false))
                {
                    logger.LogInformation("Class {Class} is deprecated, skipping", Printer.GetEntityIdentityPrint(ksClass));
                    continue;
                }

                logger.LogWarning("Class {Class} is deprecated, but migration is enabled with configuration flag 'UseDeprecatedFolderPageType'", Printer.GetEntityIdentityPrint(ksClass));
            }

            // ---------------------------------------------------------------------------------
            // STAGE 4: Map and save Page Type into XbyK Content Type
            // ---------------------------------------------------------------------------------
            var kxoDataClass = kxpClassFacade.GetClass(ksClass.ClassGUID);
            protocol.FetchedTarget(kxoDataClass);

            if (SaveUsingKxoApi(ksClass, kxoDataClass) is { } targetClass)
            {
                // ---------------------------------------------------------------------------------
                // STAGE 5: Channel Assignment for Website Content Types
                // In KX12/13, page types were assigned to Sites via CMS_ClassSite.
                // In XbyK, Website Content Types must be assigned to Website Channels via
                // ContentTypeChannelInfo. We look up the channel matching each source site GUID.
                // ---------------------------------------------------------------------------------
                if (targetClass.ClassContentTypeType is ClassContentTypeType.WEBSITE)
                {
                    foreach (var cmsClassSite in modelFacade.SelectWhere<ICmsClassSite>("ClassID = @classId", new SqlParameter("classId", ksClass.ClassID)))
                    {
                        if (modelFacade.SelectById<ICmsSite>(cmsClassSite.SiteID) is { SiteGUID: var siteGuid })
                        {
                            if (ChannelInfo.Provider.Get(siteGuid) is { ChannelID: var channelId })
                            {
                                var info = new ContentTypeChannelInfo { ContentTypeChannelChannelID = channelId, ContentTypeChannelContentTypeID = targetClass.ClassID };
                                ContentTypeChannelInfo.Provider.Set(info);
                            }
                            else
                            {
                                logger.LogWarning("Channel for site with SiteGUID '{SiteGuid}' not found", siteGuid);
                            }
                        }
                        else
                        {
                            logger.LogWarning("Source site with SiteID '{SiteId}' not found", cmsClassSite.SiteID);
                        }
                    }
                }

                if (wrapperRuleProvider?.TryGetRule(ksClass.ClassName, out var wrapperRule) == true && wrapperRule != null)
                {
                    EnsureWrapperPageType(wrapperRule, targetClass, ksClass);
                }
            }
        }

        // ---------------------------------------------------------------------------------
        // STAGE 5b: Ensure Wrapper Page Types for manually-mapped classes
        // Custom IClassMapping classes are skipped in the main loop (STAGE 2), so wrapper
        // page rules targeting those source classes never fire. Process them here using the
        // target DataClassInfo created by ExecuteMappings().
        // ---------------------------------------------------------------------------------
        if (wrapperRuleProvider is not null)
        {
            foreach (var wrapperRule in wrapperRuleProvider.GetRules())
            {
                if (manualMappings.TryGetValue(wrapperRule.SourceClassName, out var mappingEntry))
                {
                    var ksClass = modelFacade.SelectWhere<ICmsClass>("ClassName=@className", new SqlParameter("className", wrapperRule.SourceClassName)).FirstOrDefault();
                    if (ksClass is not null)
                    {
                        EnsureWrapperPageType(wrapperRule, mappingEntry.target, ksClass);
                    }
                    else
                    {
                        logger.LogWarning("Wrapper page rule for source class '{SourceClassName}' skipped: source class not found in K13 database", wrapperRule.SourceClassName);
                    }
                }
            }
        }

        // ---------------------------------------------------------------------------------
        // STAGE 5c: Ensure Child Link Reference Fields on target classes
        // ContentItemReference fields specified in child link rules are configured on the
        // target classes with AllowedContentItemTypeIdentifiers matching resolved child classes.
        // ---------------------------------------------------------------------------------
        if (childLinkRuleProvider is not null)
        {
            EnsureChildLinkFields(childLinkRuleProvider.GetRules(), manualMappings);
        }

        // ---------------------------------------------------------------------------------
        // STAGE 6: Migrate Page Template Configurations
        // Migrates template settings defined for page types in KX12/KX13
        // ---------------------------------------------------------------------------------
        await MigratePageTemplateConfigurations();

        // ---------------------------------------------------------------------------------
        // STAGE 7: Bypass Allowed Child Content Types
        // By default all restrictions are preliminarily bypassed to allow flexible page hierarchy.
        // To enforce strict restrictions, include --type-restrictions in the migrate command.
        // ---------------------------------------------------------------------------------
        await BypassAllowedChildClasses();

        return new GenericCommandResult();
    }

    private async Task BypassAllowedChildClasses()
    {
        var websiteContentTypes = kxpClassFacade.GetClasses(ClassContentTypeType.WEBSITE);
        foreach (var parent in websiteContentTypes)
        {
            logger.LogInformation("Preliminarily allowing any child content type for parent type {ParentTypeName} ({ParentTypeGuid})", parent.ClassName, parent.ClassGUID);
            foreach (var child in websiteContentTypes)
            {
                var model = new AllowedChildContentTypeModel
                {
                    AllowedChildContentTypeParentGuid = parent.ClassGUID,
                    AllowedChildContentTypeChildGuid = child.ClassGUID
                };

                var importResult = await importer.ImportAsync(model);
                if (importResult is { Success: false })
                {
                    logger.LogError("Failed to define allowed child content type '{ChildClassGuid}' of parent type '{ParentClassGuid}'", model.AllowedChildContentTypeChildGuid, model.AllowedChildContentTypeParentGuid);
                }
            }
        }
    }

    private async Task MigratePageTemplateConfigurations()
    {
        if (modelFacade.IsAvailable<ICmsPageTemplateConfiguration>())
        {
            foreach (var ksPageTemplateConfiguration in modelFacade.SelectAll<ICmsPageTemplateConfiguration>())
            {
                await pageTemplateMigrator.MigratePageTemplateConfigurationAsync(ksPageTemplateConfiguration);
            }
        }
    }

    /// <summary>
    /// Transforms the source ICmsClass into target DataClassInfo (via CmsClassMapper),
    /// handles reusable field schema conversion if configured, persists the class into
    /// the target XbyK database, and registers the ClassID translation mapping.
    /// </summary>
    private DataClassInfo? SaveUsingKxoApi(ICmsClass ksClass, DataClassInfo kxoDataClass)
    {
        // 1. Invoke CmsClassMapper to convert form definitions, field controls, and class type
        var mapped = dataClassMapper.Map(ksClass, kxoDataClass);
        protocol.MappedTarget(mapped);

        try
        {
            if (mapped is { Success: true })
            {
                (var dataClassInfo, bool newInstance) = mapped;
                ArgumentNullException.ThrowIfNull(dataClassInfo, nameof(dataClassInfo));

                // 2. Convert to reusable schema if configured in appsettings
                if (reusableSchemaService.IsConversionToReusableFieldSchemaRequested(dataClassInfo.ClassName))
                {
                    dataClassInfo = reusableSchemaService.ConvertToReusableSchema(dataClassInfo, dataClassInfo.ClassName,
                        dataClassInfo.ClassDisplayName);
                }

                // 3. Persist Content Type into target database (creates/updates SQL table and metadata)
                kxpClassFacade.SetClass(dataClassInfo);

                protocol.Success(ksClass, dataClassInfo, mapped);
                logger.LogEntitySetAction(newInstance, dataClassInfo);

                // 4. Record ClassID mapping (Source ClassID -> Target ClassID) for child entity dependencies
                primaryKeyMappingContext.SetMapping<DataClassInfo>(
                    r => r.ClassID,
                    ksClass.ClassID,
                    dataClassInfo.ClassID
                );

                return dataClassInfo;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error while saving page type {ClassName}", ksClass.ClassName);
        }

        return null;
    }

    private void EnsureWrapperPageType(WrapperPageRule rule, DataClassInfo reusableClass, ICmsClass ksClass)
    {
        var wrapperClass = kxpClassFacade.GetClass(rule.WrapperClassName) ?? DataClassInfo.New();
        wrapperClass.ClassName = rule.WrapperClassName;
        wrapperClass.ClassDisplayName = rule.WrapperDisplayName;
        if (string.IsNullOrWhiteSpace(wrapperClass.ClassTableName))
        {
            wrapperClass.ClassTableName = rule.WrapperClassName.Replace('.', '_');
        }
        wrapperClass.ClassType = ClassType.CONTENT_TYPE;
        wrapperClass.ClassContentTypeType = ClassContentTypeType.WEBSITE;
        wrapperClass.ClassWebPageHasUrl = true;

        if (wrapperClass.ClassGUID == Guid.Empty)
        {
            wrapperClass.ClassGUID = GuidHelper.CreateDataClassGuid(rule.WrapperClassName.ToLowerInvariant());
        }

        var contentTypeManager = Service.Resolve<IContentTypeManager>();
        var fi = new FormInfo(wrapperClass.ClassFormDefinition);
        if (!fi.ItemsList.OfType<FormFieldInfo>().Any(x => x.Name == nameof(ContentItemDataInfo.ContentItemDataGUID)))
        {
            contentTypeManager.Initialize(wrapperClass);
            fi = new FormInfo(wrapperClass.ClassFormDefinition);
        }

        var refField = fi.GetFormField(rule.ContentItemReferenceField);
        if (refField is null)
        {
            refField = new FormFieldInfo
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
                    { FormDefinitionPatcher.SettingsElemControlname, FormComponents.AdminContentItemSelectorComponent }
                }
            };
            fi.AddFormItem(refField);
        }

        refField.Settings[FormDefinitionPatcher.AllowedContentItemTypeIdentifiers] = JsonConvert.SerializeObject(new[] { reusableClass.ClassGUID });
        refField.Settings[FormDefinitionPatcher.SettingsMaximumpages] = "1";

        if (rule.IncludeLegacyMetadata)
        {
            var version = modelFacade.SelectVersion();
            var includedMetadata = toolConfiguration.IncludeExtendedMetadata.GetValueOrDefault(false) ? CmsClassMapper.IncludedMetadata.Extended : CmsClassMapper.IncludedMetadata.Basic;
            foreach (var field in CmsClassMapper.GetLegacyMetadataFields(version, includedMetadata))
            {
                CmsClassMapper.AppendLegacyMetadataField(fi, wrapperClass.ClassName, field, out _);
            }
        }

        wrapperClass.ClassFormDefinition = fi.GetXmlDefinition();
        kxpClassFacade.SetClass(wrapperClass);
        logger.LogInformation("Ensured wrapper page type {WrapperClassName} for source class {SourceClassName} referencing reusable class {ReusableClassName} ({ReusableGuid})",
            rule.WrapperClassName, ksClass.ClassName, reusableClass.ClassName, reusableClass.ClassGUID);

        foreach (var cmsClassSite in modelFacade.SelectWhere<ICmsClassSite>("ClassID = @classId", new SqlParameter("classId", ksClass.ClassID)))
        {
            if (modelFacade.SelectById<ICmsSite>(cmsClassSite.SiteID) is { SiteGUID: var siteGuid })
            {
                if (ChannelInfo.Provider.Get(siteGuid) is { ChannelID: var channelId })
                {
                    var existingLink = ContentTypeChannelInfo.Provider.Get()
                        .WhereEquals(nameof(ContentTypeChannelInfo.ContentTypeChannelChannelID), channelId)
                        .WhereEquals(nameof(ContentTypeChannelInfo.ContentTypeChannelContentTypeID), wrapperClass.ClassID)
                        .FirstOrDefault();

                    if (existingLink == null)
                    {
                        var info = new ContentTypeChannelInfo
                        {
                            ContentTypeChannelChannelID = channelId,
                            ContentTypeChannelContentTypeID = wrapperClass.ClassID
                        };
                        ContentTypeChannelInfo.Provider.Set(info);
                    }
                }
                else
                {
                    logger.LogWarning("Channel for site with SiteGUID '{SiteGuid}' not found", siteGuid);
                }
            }
            else
            {
                logger.LogWarning("Source site with SiteID '{SiteId}' not found", cmsClassSite.SiteID);
            }
        }
    }

    private void EnsureChildLinkFields(
        IReadOnlyList<ChildLinkRule> childLinkRules,
        Dictionary<string, (DataClassInfo target, IClassMapping mappping)> manualMappings)
    {
        foreach (var rule in childLinkRules)
        {
            string parentTargetClassName = manualMappings.TryGetValue(rule.ParentClassName, out var parentEntry)
                ? parentEntry.target.ClassName
                : rule.ParentClassName;

            var parentClass = DataClassInfoProvider.GetDataClassInfo(parentTargetClassName);
            if (parentClass is null)
            {
                logger.LogWarning("Child link rule for parent class '{ParentClassName}' skipped: target class '{TargetClassName}' not found",
                    rule.ParentClassName, parentTargetClassName);
                continue;
            }

            string childTargetClassName = manualMappings.TryGetValue(rule.ChildClassName, out var childEntry)
                ? childEntry.target.ClassName
                : rule.ChildClassName;

            var childClass = DataClassInfoProvider.GetDataClassInfo(childTargetClassName);

            var fi = new FormInfo(parentClass.ClassFormDefinition);
            var refField = fi.GetFormField(rule.TargetFieldName);
            if (refField is null)
            {
                refField = new FormFieldInfo
                {
                    Guid = GuidHelper.CreateFieldGuid($"{rule.TargetFieldName.ToLowerInvariant()}|{parentClass.ClassName}"),
                    AllowEmpty = true,
                    Caption = rule.FieldCaption ?? rule.TargetFieldName,
                    DataType = FieldDataType.ContentItemReference,
                    Name = rule.TargetFieldName,
                    Enabled = true,
                    Visible = true,
                    System = false,
                    DefaultValue = null,
                    Settings =
                    {
                        { FormDefinitionPatcher.SettingsElemControlname, FormComponents.AdminContentItemSelectorComponent }
                    }
                };
                fi.AddFormItem(refField);
            }
            else
            {
                refField.DataType = FieldDataType.ContentItemReference;
                refField.Visible = true;
                refField.Enabled = true;
                refField.Settings[FormDefinitionPatcher.SettingsElemControlname] = FormComponents.AdminContentItemSelectorComponent;
                if (!string.IsNullOrWhiteSpace(rule.FieldCaption))
                {
                    refField.Caption = rule.FieldCaption;
                }
            }

            if (childClass is not null)
            {
                var allowedGuids = new HashSet<Guid>();
                if (refField.Settings[FormDefinitionPatcher.AllowedContentItemTypeIdentifiers] is string existingAllowed &&
                    !string.IsNullOrWhiteSpace(existingAllowed))
                {
                    try
                    {
                        var deserialized = JsonConvert.DeserializeObject<Guid[]>(existingAllowed);
                        if (deserialized != null)
                        {
                            foreach (var g in deserialized)
                            {
                                allowedGuids.Add(g);
                            }
                        }
                    }
                    catch { }
                }

                allowedGuids.Add(childClass.ClassGUID);
                refField.Settings[FormDefinitionPatcher.AllowedContentItemTypeIdentifiers] = JsonConvert.SerializeObject(allowedGuids.ToArray());
            }
            else
            {
                logger.LogWarning("Child class '{ChildClassName}' (target: '{ChildTargetClassName}') not found when configuring child link field '{FieldName}' on '{ParentClassName}'",
                    rule.ChildClassName, childTargetClassName, rule.TargetFieldName, parentClass.ClassName);
            }

            parentClass.ClassFormDefinition = fi.GetXmlDefinition();
            kxpClassFacade.SetClass(parentClass);
            logger.LogInformation("Ensured child link reference field '{FieldName}' on class '{ParentClassName}' (allowed child: {ChildClassName})",
                rule.TargetFieldName, parentClass.ClassName, childClass?.ClassName ?? childTargetClassName);
        }
    }
}

