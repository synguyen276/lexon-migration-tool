using CMS.ContentEngine;
using CMS.ContentEngine.Internal;
using CMS.Core;
using CMS.DataEngine;
using Kentico.Xperience.UMT.Services;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Migration.Tool.Common;
using Migration.Tool.Common.MigrationProtocol;
using Migration.Tool.Common.Services;
using Migration.Tool.Source.Auxiliary;
using Migration.Tool.Source.Model;

namespace Migration.Tool.Source.Services;

public class AttachmentMigratorToContentItem(
    ILogger<AttachmentMigratorToContentItem> logger,
    IProtocol protocol,
    ModelFacade modelFacade,
    EntityIdentityFacade entityIdentityFacade,
    IAssetFacade assetFacade,
    IImporter importer,
    UserService userService,
    ToolConfiguration toolConfiguration
) : IAttachmentMigrator
{
    private byte[]? TryGetAttachmentBinary(ICmsAttachment ksAttachment, ICmsSite site)
    {
        if (ksAttachment.AttachmentBinary != null)
        {
            return ksAttachment.AttachmentBinary;
        }

        if (!string.IsNullOrWhiteSpace(toolConfiguration.KxCmsDirPath) && Directory.Exists(toolConfiguration.KxCmsDirPath))
        {
            string guidStr = ksAttachment.AttachmentGUID.ToString();
            string guidPrefix = guidStr.Length >= 2 ? guidStr[..2] : "";
            string ext = ksAttachment.AttachmentExtension?.TrimStart('.') ?? "";
            string fileNameWithExt = !string.IsNullOrEmpty(ext) ? $"{guidStr}.{ext}" : guidStr;

            string[] candidatePaths = [
                Path.Combine(toolConfiguration.KxCmsDirPath, site.SiteName, "files", guidPrefix, fileNameWithExt),
                Path.Combine(toolConfiguration.KxCmsDirPath, site.SiteName, "files", fileNameWithExt),
                Path.Combine(toolConfiguration.KxCmsDirPath, "App_Data", "Persistent", "CMSFiles", guidPrefix, fileNameWithExt),
                Path.Combine(toolConfiguration.KxCmsDirPath, "App_Data", "CMSModules", "Content", "Attachments", fileNameWithExt),
                Path.Combine(toolConfiguration.KxCmsDirPath, "CMSFiles", site.SiteName, guidPrefix, fileNameWithExt),
                Path.Combine(toolConfiguration.KxCmsDirPath, "CMSFiles", guidPrefix, fileNameWithExt),
                Path.Combine(toolConfiguration.KxCmsDirPath, "CMSFiles", fileNameWithExt)
            ];

            foreach (string path in candidatePaths)
            {
                if (File.Exists(path))
                {
                    try
                    {
                        logger.LogInformation("Attachment '{AttachmentName}' ({AttachmentGUID}) binary loaded from filesystem: {Path}", ksAttachment.AttachmentName, ksAttachment.AttachmentGUID, path);
                        return File.ReadAllBytes(path);
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to read attachment file from filesystem at {Path}", path);
                    }
                }
            }
        }

        return null;
    }
    public async Task<IMigrateAttachmentResult> TryMigrateAttachmentByPath(string documentPath, string additionalPath)
    {
        if (string.IsNullOrWhiteSpace(documentPath))
        {
            return new MigrateAttachmentResultContentItem(false, null);
        }

        documentPath = $"/{documentPath.Trim('/')}";

        var attachments =
                modelFacade.SelectWhere<ICmsAttachment>(
                    """
                    EXISTS (
                        SELECT T.NodeAliasPath
                        FROM CMS_Document [D] JOIN
                             CMS_Tree [T] ON D.DocumentNodeID = T.NodeID
                        WHERE D.DocumentID = AttachmentDocumentID AND T.NodeAliasPath = @nodeAliasPath
                    )
                    """, new SqlParameter("nodeAliasPath", documentPath)).ToList()
            ;

        var attachment = attachments.FirstOrDefault();
        return attachment is not null
            ? await MigrateAttachment(attachment, additionalPath)
            : new MigrateAttachmentResultContentItem(false, null);
    }

    public async IAsyncEnumerable<IMigrateAttachmentResult> MigrateGroupedAttachments(int documentId, Guid attachmentGroupGuid, string fieldName)
    {
        var groupedAttachment = modelFacade.SelectWhere<ICmsAttachment>(
            """
            AttachmentGroupGuid = @attachmentGroupGuid AND
            AttachmentDocumentId = @attachmentDocumentId
            """,
            new SqlParameter("attachmentGroupGuid", attachmentGroupGuid),
            new SqlParameter("attachmentDocumentId", documentId)
        );

        foreach (var cmsAttachment in groupedAttachment)
        {
            yield return await MigrateAttachment(cmsAttachment, $"__{fieldName}");
        }
    }

    public async Task<IMigrateAttachmentResult> MigrateAttachment(Guid ksAttachmentGuid, string additionalPath, int siteId)
    {
        var attachments = modelFacade
            .SelectWhere<ICmsAttachment>("AttachmentGuid = @attachmentGuid AND AttachmentSiteID = @siteId",
                new SqlParameter("attachmentGuid", ksAttachmentGuid),
                new SqlParameter("siteId", siteId)
            )
            .ToList();

        switch (attachments)
        {
            case { Count: 0 }:
            {
                logger.LogWarning("Attachment '{AttachmentGuid}' not found! => skipping", ksAttachmentGuid);
                protocol.Append(HandbookReferences.TemporaryAttachmentMigrationIsNotSupported.WithData(new { AttachmentGuid = ksAttachmentGuid }));
                return new MigrateAttachmentResultContentItem(false, null);
            }
            case [var attachment]:
            {
                return await MigrateAttachment(attachment, additionalPath);
            }
            default:
            {
                logger.LogWarning("Attachment '{AttachmentGuid}' found multiple times! => skipping", ksAttachmentGuid);
                protocol.Append(HandbookReferences.NonUniqueEntityGuid.WithData(new { AttachmentGuid = ksAttachmentGuid, AttachmentIds = attachments.Select(a => a.AttachmentID) }));
                return new MigrateAttachmentResultContentItem(false, null);
            }
        }
    }

    private bool assetFacadeInitialized;
    public async Task<IMigrateAttachmentResult> MigrateAttachment(ICmsAttachment ksAttachment, string? additionalMediaPath = null)
    {
        if (!assetFacadeInitialized)
        {
            await assetFacade.PreparePrerequisites();
            assetFacadeInitialized = true;
        }

        protocol.FetchedSource(ksAttachment);

        if (ksAttachment.AttachmentFormGUID != null)
        {
            logger.LogWarning("Attachment '{AttachmentGuid}' is temporary => skipping", ksAttachment.AttachmentGUID);
            protocol.Append(HandbookReferences.TemporaryAttachmentMigrationIsNotSupported.WithData(new { ksAttachment.AttachmentID, ksAttachment.AttachmentGUID, ksAttachment.AttachmentName, ksAttachment.AttachmentSiteID }));
            return new MigrateAttachmentResultContentItem(false, null);
        }

        var ksAttachmentDocument = ksAttachment.AttachmentDocumentID is { } attachmentDocumentId
            ? modelFacade.SelectById<ICmsDocument>(attachmentDocumentId)
            : null;

        var ksNode = ksAttachmentDocument?.DocumentNodeID is { } documentNodeId
            ? modelFacade.SelectById<ICmsTree>(documentNodeId)
            : null;

        var site = modelFacade.SelectById<ICmsSite>(ksAttachment.AttachmentSiteID) ?? throw new InvalidOperationException("Site not exists!");

        (bool isFixed, var newAttachmentGuid) = entityIdentityFacade.Translate(ksAttachment);
        if (isFixed)
        {
            logger.LogWarning("Attachment {Attachment} link will be broken, new guid {Guid} was required", new { ksAttachment.AttachmentSiteID, ksAttachment.AttachmentID, ksAttachment.AttachmentGUID }, newAttachmentGuid);
        }

        byte[]? binary = TryGetAttachmentBinary(ksAttachment, site);
        if (binary is null)
        {
            logger.LogWarning("Attachment binary data is null for '{AttachmentName}' (AttachmentSiteID: {AttachmentSiteID}, AttachmentID: {AttachmentID}, GUID: {AttachmentGUID}). " +
                "KxCmsDirPath is empty or file not found on disk => skipping attachment migration. " +
                "Option 1: Via admin web interface of your source instance navigate to the attachment and update the data. " +
                "Option 2: Update the database directly - table CMS_Attachment, column AttachmentBinary. " +
                "Option 3: Via admin web interface of your source instance remove all attachment references, then remove the attachment.",
                ksAttachment.AttachmentName, ksAttachment.AttachmentSiteID, ksAttachment.AttachmentID, ksAttachment.AttachmentGUID);

            protocol.Append(HandbookReferences.AttachmentBinaryDataIsNull.WithData(new
            {
                ksAttachment.AttachmentName,
                ksAttachment.AttachmentSiteID,
                ksAttachment.AttachmentID,
                ksAttachment.AttachmentGUID
            }));

            return new MigrateAttachmentResultContentItem(false, null);
        }

        if (binary != ksAttachment.AttachmentBinary)
        {
            ksAttachment = ksAttachment switch
            {
                CmsAttachmentK11 a => a with { AttachmentBinary = binary },
                CmsAttachmentK12 a => a with { AttachmentBinary = binary },
                CmsAttachmentK13 a => a with { AttachmentBinary = binary },
                _ => ksAttachment
            };
        }

        // language of the migrated attachment = language of the document where the attachment is attached or default site language
        string languageName =
            (ksAttachmentDocument != null
                ? Service.Resolve<IInfoProvider<ContentLanguageInfo>>().Get()
                    .WhereEquals(nameof(ContentLanguageInfo.ContentLanguageCultureFormat), ksAttachmentDocument.DocumentCulture)
                    .FirstOrDefault()?.ContentLanguageName
                : null)
            ?? (await Service.Resolve<IContentLanguageRetriever>().GetDefaultContentLanguage()).ContentLanguageName;

        var asset = await assetFacade.FromAttachment(ksAttachment, site, ksNode, [languageName]);

        foreach (var item in asset.LanguageData)
        {
            item.UserGuid = (item.UserGuid.HasValue && userService.UserExists(item.UserGuid.Value))
                ? item.UserGuid
                : userService.DefaultAdminUser?.UserGUID;
        }

        switch (await importer.ImportAsync(asset))
        {
            case { Success: true }:
            {
                logger.LogInformation("Media file '{File}' imported", ksAttachment.AttachmentGUID);
                return new MigrateAttachmentResultContentItem(true, asset.ContentItemGUID);
            }
            case { Success: false, Exception: { } exception }:
            {
                logger.LogError("Media file '{File}' not migrated: {Error}", ksAttachment.AttachmentGUID, exception);
                return new MigrateAttachmentResultContentItem(false, null);
            }
            case { Success: false, ModelValidationResults: { } validation }:
            {
                foreach (var validationResult in validation)
                {
                    logger.LogError("Media file '{File}' not migrated: {Members}: {Error}", ksAttachment.AttachmentGUID, string.Join(",", validationResult.MemberNames), validationResult.ErrorMessage);
                }

                return new MigrateAttachmentResultContentItem(false, null);
            }
            default:
                return new MigrateAttachmentResultContentItem(false, null);
        }
    }
}
