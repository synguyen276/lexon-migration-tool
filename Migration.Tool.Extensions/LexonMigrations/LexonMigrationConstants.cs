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
    }

    public static class FieldNames
    {
        public const string Title = "Title";
        public const string Summary = "Summary";
        public const string Text = "Text";
        public const string Teaser = "Teaser";
        public const string Image = "Image";
        public const string Name = "Name";
        public const string FileName = "FileName";
        public const string Attachment = "Attachment";
        public const string Description = "Description";
        public const string Date = "Date";
        public const string ContentItem = "ContentItem";

        // Area of Practice / Resource Area field names
        public const string ResourceAreaRelatedResources = "ResourceAreaRelatedResources";
        public const string ResourceAreaDocumentGroups = "ResourceAreaDocumentGroups";
        public const string ResourceAreaDocuments = "ResourceAreaDocuments";
        public const string ResourceAreaUpdates = "ResourceAreaUpdates";
        public const string MediaLibraryFolder = "MediaLibraryFolder";
    }
}


