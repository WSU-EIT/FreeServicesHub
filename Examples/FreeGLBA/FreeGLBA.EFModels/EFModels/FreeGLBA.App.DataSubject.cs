using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FreeGLBA.EFModels.EFModels;

/// <summary>
/// DataSubject entity - stored in [DataSubjects] table.
/// </summary>
[Table("DataSubjects")]
public partial class DataSubjectItem
{
    [Key]
    public Guid DataSubjectId { get; set; }

    [MaxLength(200)]
    public string ExternalId { get; set; } = string.Empty;

    [MaxLength(50)]
    public string SubjectType { get; set; } = string.Empty;

    public DateTime FirstAccessedAt { get; set; }

    public DateTime LastAccessedAt { get; set; }

    public long TotalAccessCount { get; set; } = 0;

    public int UniqueAccessorCount { get; set; } = 0;

}
