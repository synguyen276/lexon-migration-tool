namespace Migration.Tool.Extensions.LexonMigrations;

public static class LexonMigrationConstants
{
    public static class SourceClassNames
    {
        public const string NavigationSection = "Lexon.NavigationSection";
        public const string RiskItem = "Lexon.RiskItem";
        public const string RiskAttachmentSet = "Lexon.riskattachmentset";
        public const string RiskAttachment = "Lexon.riskattachment";
        public const string RiskLinkSet = "Lexon.risklinkset";
        public const string RiskItemSet = "Lexon.riskitemset";
        public const string BulletinSet = "Lexon.BulletinSet";
        public const string Bulletin = "Lexon.Bulletin";
        public const string ExternalURL = "Lexon.ExternalURL";
    }

    public static class TargetClassNames
    {
        public const string AreaOfPracticeLandingPage = "Lexon.AreaOfPracticeLandingPage";
        public const string AreaOfPractice = "Lexon.AreaOfPractice";
        public const string AreaOfPracticePage = "Lexon.AreaOfPracticePage";
        public const string ResourceArea = "Lexon.ResourceArea";
        public const string ResourceAreaPage = "Lexon.ResourceAreaPage";
        public const string ResourceAreaDocumentGroup = "Lexon.ResourceAreaDocumentGroup";
        public const string ResourceAreaDocument = "Lexon.ResourceAreaDocument";
        public const string ResourceAreaUpdate = "Lexon.ResourceAreaUpdate";
    }

    public static class SourceFieldNames
    {
        public const string ArticleTitle = "ArticleTitle";
        public const string ArticleSummary = "ArticleSummary";
        public const string ArticleText = "ArticleText";
        public const string ArticleTeaser = "ArticleTeaser";
        public const string ImagePath = "ImagePath";
        public const string RiskSetName = "RiskSetName";
        public const string FileName = "FileName";
        public const string Attachment = "Attachment";
        public const string Description = "Description";
        public const string Title = "Title";
        public const string DateOfItem = "DateOfItem";
        public const string TeaserImage = "TeaserImage";
        public const string Summary = "Summary";
        public const string Body = "Body";
        public const string Attachments = "Attachments";
        public const string MLFolder = "MLFolder";

        // Document legacy metadata field names
        public const string DocumentModifiedWhen = "DocumentModifiedWhen";
        public const string DocumentCreatedWhen = "DocumentCreatedWhen";
        public const string DocumentName = "DocumentName";
    }

    public static class TargetFieldNames
    {
        public const string AreaOfPracticePagePageCategory = "AreaOfPracticePagePageCategory";
        public const string ResourceAreaTitle = "ResourceAreaTitle";
        public const string ResourceAreaSummary = "ResourceAreaSummary";
        public const string ResourceAreaLastUpdated = "ResourceAreaLastUpdated";
        public const string ResourceAreaBody = "ResourceAreaBody";
        public const string ResourceAreaDocumentGroups = "ResourceAreaDocumentGroups";
        public const string ResourceAreaRelatedResources = "ResourceAreaRelatedResources";
        public const string ResourceAreaCategories = "ResourceAreaCategories";
        public const string ResourceAreaPageResource = "ResourceAreaPageResource";
        public const string ResourceAreaDocumentGroupTitle = "ResourceAreaDocumentGroupTitle";
        public const string ResourceAreaDocumentGroupDocuments = "ResourceAreaDocumentGroupDocuments";
        public const string ResourceAreaDocumentTitle = "ResourceAreaDocumentTitle";
        public const string ResourceAreaDocumentFile = "ResourceAreaDocumentFile";
        public const string RiskUpdateTitle = "RiskUpdateTitle";
        public const string RiskUpdateDate = "RiskUpdateDate";
        public const string RiskUpdateBody = "RiskUpdateBody";
        public const string RiskUpdateRelatedResources = "RiskUpdateRelatedResources";
    }

    public static class Taxonomies
    {
        public static readonly Guid ResourceCategoryGuid = new("b589e015-11d3-4b13-8748-ca857b60a112");
        public const string ResourceCategoryName = "ResourceCategory";
        public const string ResourceCategoryTitle = "Resource Category";
    }

    public static class FieldNames
    {
        // Primary Target Field Names
        public const string AreaOfPracticePagePageCategory = TargetFieldNames.AreaOfPracticePagePageCategory;
        public const string ResourceAreaTitle = TargetFieldNames.ResourceAreaTitle;
        public const string ResourceAreaSummary = TargetFieldNames.ResourceAreaSummary;
        public const string ResourceAreaLastUpdated = TargetFieldNames.ResourceAreaLastUpdated;
        public const string ResourceAreaBody = TargetFieldNames.ResourceAreaBody;
        public const string ResourceAreaDocumentGroups = TargetFieldNames.ResourceAreaDocumentGroups;
        public const string ResourceAreaRelatedResources = TargetFieldNames.ResourceAreaRelatedResources;
        public const string ResourceAreaCategories = TargetFieldNames.ResourceAreaCategories;
        public const string ResourceAreaPageResource = TargetFieldNames.ResourceAreaPageResource;
        public const string ResourceAreaDocumentGroupTitle = TargetFieldNames.ResourceAreaDocumentGroupTitle;
        public const string ResourceAreaDocumentGroupDocuments = TargetFieldNames.ResourceAreaDocumentGroupDocuments;
        public const string ResourceAreaDocumentTitle = TargetFieldNames.ResourceAreaDocumentTitle;
        public const string ResourceAreaDocumentFile = TargetFieldNames.ResourceAreaDocumentFile;
        public const string RiskUpdateTitle = TargetFieldNames.RiskUpdateTitle;
        public const string RiskUpdateDate = TargetFieldNames.RiskUpdateDate;
        public const string RiskUpdateBody = TargetFieldNames.RiskUpdateBody;
        public const string RiskUpdateRelatedResources = TargetFieldNames.RiskUpdateRelatedResources;

        // Backward compatibility aliases
        public const string Title = ResourceAreaTitle;
        public const string Summary = ResourceAreaSummary;
        public const string Text = ResourceAreaBody;
        public const string Teaser = "Teaser";
        public const string Image = "Image";
        public const string Name = ResourceAreaDocumentGroupTitle;
        public const string FileName = ResourceAreaDocumentTitle;
        public const string Attachment = ResourceAreaDocumentFile;
        public const string Description = "Description";
        public const string Date = RiskUpdateDate;
        public const string ContentItem = ResourceAreaPageResource;
        public const string ResourceAreaDocuments = ResourceAreaDocumentGroupDocuments;
        public const string ResourceAreaUpdates = RiskUpdateRelatedResources;
        public const string MediaLibraryFolder = "MediaLibraryFolder";
    }
}


