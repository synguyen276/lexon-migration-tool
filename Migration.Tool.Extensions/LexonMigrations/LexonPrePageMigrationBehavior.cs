using CMS.DataEngine;
using CMS.FormEngine;
using MediatR;
using Microsoft.Extensions.Logging;
using Migration.Tool.Common;
using Migration.Tool.Common.Abstractions;
using Migration.Tool.Common.MigrationProtocol;
using Migration.Tool.Source;

namespace Migration.Tool.Extensions.LexonMigrations;

/// <summary>
/// Pipeline behavior that runs prior to page migration to pre-populate taxonomy tags and hierarchy lookups,
/// and ensure target content type database schemas and XML definitions are synchronized.
/// </summary>
public sealed class LexonPrePageMigrationBehavior(
    ILogger<LexonPrePageMigrationBehavior> logger,
    ModelFacade modelFacade,
    LexonTaxonomyService taxonomyService)
    : IPipelineBehavior<MigratePagesCommand, CommandResult>
{
    public async Task<CommandResult> Handle(
        MigratePagesCommand request,
        RequestHandlerDelegate<CommandResult> next,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Initializing Lexon taxonomy and hierarchy caches...");
            taxonomyService.Initialize(modelFacade);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize Lexon taxonomy service.");
        }

        try
        {
            EnsureSchemaIntegrity();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to reconcile Lexon page content type schemas.");
        }

        return await next();
    }

    private void EnsureSchemaIntegrity()
    {
        string[] websiteClassNames = [
            LexonMigrationConstants.TargetClassNames.AreaOfPracticePage,
            LexonMigrationConstants.TargetClassNames.AreaOfPracticeLandingPage,
            LexonMigrationConstants.TargetClassNames.ResourceAreaPage
        ];

        foreach (string className in websiteClassNames)
        {
            var classInfo = DataClassInfoProvider.GetDataClassInfo(className);
            if (classInfo is null || string.IsNullOrWhiteSpace(classInfo.ClassTableName))
            {
                continue;
            }

            var tm = new TableManager(classInfo.ClassConnectionString);
            if (!tm.TableExists(classInfo.ClassTableName))
            {
                continue;
            }

            bool changed = false;

            // 1. Remove DocumentName from form definition if present
            if (!string.IsNullOrWhiteSpace(classInfo.ClassFormDefinition))
            {
                var fi = new FormInfo(classInfo.ClassFormDefinition);
                if (fi.GetFormField("DocumentName") is { } docField)
                {
                    logger.LogInformation("Removing 'DocumentName' from form definition of '{ClassName}'", className);
                    fi.RemoveFormField(docField.Name);
                    classInfo.ClassFormDefinition = fi.GetXmlDefinition();
                    changed = true;
                }

                if (className.Equals(LexonMigrationConstants.TargetClassNames.AreaOfPracticeLandingPage, StringComparison.OrdinalIgnoreCase) &&
                    fi.GetFormField("ResourceAreaRelatedResources") is { } relField)
                {
                    logger.LogInformation("Removing 'ResourceAreaRelatedResources' from form definition of '{ClassName}'", className);
                    fi.RemoveFormField(relField.Name);
                    classInfo.ClassFormDefinition = fi.GetXmlDefinition();
                    changed = true;
                }
            }

            // 2. Drop obsolete column DocumentName from SQL table if present
            var colDoc = tm.GetColumnInformation(classInfo.ClassTableName, "DocumentName");
            if (colDoc?.Tables.Count > 0 && colDoc.Tables[0].Rows.Count > 0)
            {
                logger.LogInformation("Dropping obsolete column 'DocumentName' from table '{TableName}'", classInfo.ClassTableName);
                tm.DropTableColumn(classInfo.ClassTableName, "DocumentName");
                changed = true;
            }

            // Drop obsolete column ResourceAreaRelatedResources from AreaOfPracticeLandingPage if present
            if (className.Equals(LexonMigrationConstants.TargetClassNames.AreaOfPracticeLandingPage, StringComparison.OrdinalIgnoreCase))
            {
                var colRel = tm.GetColumnInformation(classInfo.ClassTableName, "ResourceAreaRelatedResources");
                if (colRel?.Tables.Count > 0 && colRel.Tables[0].Rows.Count > 0)
                {
                    logger.LogInformation("Dropping obsolete column 'ResourceAreaRelatedResources' from table '{TableName}'", classInfo.ClassTableName);
                    tm.DropTableColumn(classInfo.ClassTableName, "ResourceAreaRelatedResources");
                    changed = true;
                }
            }

            // 3. Ensure ClassXmlSchema matches actual physical SQL table
            string currentXmlSchema = tm.GetXmlSchema(classInfo.ClassTableName);
            if (!string.Equals(classInfo.ClassXmlSchema, currentXmlSchema, StringComparison.Ordinal))
            {
                logger.LogInformation("Synchronizing ClassXmlSchema with physical table for '{ClassName}'", className);
                classInfo.ClassXmlSchema = currentXmlSchema;
                changed = true;
            }

            if (changed)
            {
                DataClassInfoProvider.SetDataClassInfo(classInfo);
            }
        }
    }
}

