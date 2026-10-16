using Microsoft.Extensions.DependencyInjection;
using Migration.Tool.Common.Abstractions;
using Migration.Tool.Extensions.CommunityMigrations;
using Migration.Tool.Extensions.DefaultMigrations;
using Migration.Tool.Extensions.LexonMigrations;
using Migration.Tool.KXP.Api.Services.CmsClass;
using Migration.Tool.Source.Mappers.ContentItemMapperDirectives;

namespace Migration.Tool.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection UseCustomizations(this IServiceCollection services)
    {
        services.AddTransient<IFieldMigration, AssetMigration>();
        services.AddTransient<IFieldMigration, SampleTextMigration>();

        services.AddTransient<IWidgetPropertyMigration, WidgetFileMigration>();
        services.AddTransient<IWidgetPropertyMigration, WidgetPathSelectorMigration>();
        services.AddTransient<IWidgetPropertyMigration, WidgetPageSelectorMigration>();

        // ── Lexon migration registrations ──────────────────────────────────────
        // ORDER MATTERS: child link rules → class mapping rules (incl. wrappers).
        //   1. Child link rules  – LexonChildLinkDirector resolves parent/child
        //      relationships and linked-node references before content items are
        //      mapped, so it must be registered first.
        //   2. Class mapping rules – rename/remodel source classes into target
        //      reusable content types AND register wrapper page rules for those
        //      that need a website-page wrapper. Wrapper rules are co-located
        //      with their class mapping definitions.
        // ───────────────────────────────────────────────────────────────────────
        services.AddTransient<ContentItemDirectorBase, LexonChildLinkDirector>();
        services.AddClassMappingRules();
        services.AddSingleton(LexonTaxonomyService.Instance);
        services.AddTransient(
            typeof(MediatR.IPipelineBehavior<Migration.Tool.Common.MigratePagesCommand, CommandResult>),
            typeof(LexonPrePageMigrationBehavior));

        // services.AddClassMergeExample();
        // services.AddClassMergeExampleAsReusable();
        // services.AddSimpleRemodelingSample();
        // services.AddReusableRemodelingSample();
        // services.AddReusableSchemaIntegrationSample();
        // services.AddReusableSchemaAutoGenerationSample();
        // services.AddTransient<ContentItemDirectorBase, SamplePageToWidgetDirector>();
        // services.AddTransient<ContentItemDirectorBase, SampleChildLinkDirector>();

        // Routing content items to prefabricated content types (i.e., types not created by Migration Tool --page-types CLI argument)
        //
        // The following two methods may be combined, but one particular content type should be covered by only one of them.
        //
        //   1. Content item director method is applicable if each target field has a matching source field that has the same name.
        //      You may use JsonBasedTypeRemapDirector or drive the mapping directly from your own director using IContentItemActionProvider.OverrideTargetType method.
        //
        //      services.AddTransient<ContentItemDirectorBase>(sp => new JsonBasedTypeRemapDirector("migration-mapping.json"));
        //
        //   2. Custom mapping method gives you the highest flexibility if the prefabricated type doesn't match the source type exactly.
        //
        //      services.AddMappingToPrefabricatedContentTypeSample();

        return services;
    }
}
