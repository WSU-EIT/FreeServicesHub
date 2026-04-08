namespace FreeExamples;

public partial class DataObjects
{
    /// <summary>
    /// A comment in a thread attached to a SampleItem.
    /// In-memory via CommentService (same pattern as ApiKeyDemoService).
    /// </summary>
    public class SampleComment
    {
        public Guid CommentId { get; set; }
        public Guid SampleItemId { get; set; }
        public string AuthorName { get; set; } = "";
        public string AuthorInitials { get; set; } = "";
        public string Text { get; set; } = "";
        public DateTime Created { get; set; }
        public DateTime? Edited { get; set; }
        public bool Deleted { get; set; }
    }
}
