namespace FreeExamples;

public partial class DataObjects
{
    /// <summary>
    /// A saved code snippet for the Code Playground demo.
    /// Stored in-memory via CodeSnippetService.
    /// </summary>
    public class CodeSnippet
    {
        public Guid SnippetId { get; set; }
        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
        public string Language { get; set; } = "plaintext";
        public DateTime LastSaved { get; set; }
    }

    /// <summary>
    /// Request DTO for the API Notebook "execute" feature.
    /// Sends editor content to a selected endpoint and returns the response.
    /// </summary>
    public class CodePlaygroundRequest
    {
        public string Endpoint { get; set; } = "";
        public string Body { get; set; } = "";
    }

    /// <summary>
    /// Response DTO wrapping whatever the target endpoint returns.
    /// </summary>
    public class CodePlaygroundResponse
    {
        public bool Success { get; set; }
        public int StatusCode { get; set; }
        public string ResponseBody { get; set; } = "";
        public long DurationMs { get; set; }
    }
}
