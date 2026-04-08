namespace FreeExamples;

public partial class DataObjects
{
    // ── Category 9: Course Evaluations ──

    public class Evaluation : IJsonEntity
    {
        public Guid RecordId { get; set; }
        public Guid TenantId { get; set; }
        public static string EntityType => "Evaluation";
        public static int CurrentSchemaVersion => 1;

        public string Title { get; set; } = "";
        public string CourseCode { get; set; } = "";
        public string CourseName { get; set; } = "";
        public string InstructorName { get; set; } = "";
        public string Term { get; set; } = "";
        public string Department { get; set; } = "";
        public DateTime OpenDate { get; set; }
        public DateTime CloseDate { get; set; }
        public bool IsAnonymous { get; set; } = true;
        public int EnrollmentCount { get; set; }
        public Guid TemplateId { get; set; }
        public List<EvalQuestion> Questions { get; set; } = [];
        public List<EvalResponse> Responses { get; set; } = [];
    }

    public class EvalQuestion
    {
        public Guid QuestionId { get; set; }
        public string QuestionText { get; set; } = "";
        public EvalQuestionType QuestionType { get; set; }
        public bool IsRequired { get; set; } = true;
        public int DisplayOrder { get; set; }
        public string? Options { get; set; }
    }

    public enum EvalQuestionType { Likert5, Likert7, MultipleChoice, YesNo, FreeText, Rating10 }

    public class EvalResponse
    {
        public Guid ResponseId { get; set; }
        public DateTime SubmittedDate { get; set; }
        public List<EvalAnswer> Answers { get; set; } = [];
    }

    public class EvalAnswer
    {
        public Guid QuestionId { get; set; }
        public string Value { get; set; } = "";
    }

    public class FilterEvaluations : FilterJsonRecords<Evaluation>
    {
        public string? Term { get; set; }
        public string? Department { get; set; }
        public string? Status { get; set; }
    }
}
