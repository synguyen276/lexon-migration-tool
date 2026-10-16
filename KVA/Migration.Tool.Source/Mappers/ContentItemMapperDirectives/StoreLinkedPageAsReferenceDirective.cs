namespace Migration.Tool.Source.Mappers.ContentItemMapperDirectives;
internal class StoreLinkedPageAsReferenceDirective : LinkedPageDirectiveBase
{
    public int ParentLevel { get; set; }
    public required string FieldName { get; set; }
    public bool ReferenceWebsiteItem { get; set; }
}
