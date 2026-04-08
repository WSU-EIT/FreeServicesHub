using System.Net.NetworkInformation;

namespace FreeExamples.Client;

public static partial class Helpers
{
    public static List<DataObjects.MenuItem> MyMenuItemsApp {
        get {
            var output = new List<DataObjects.MenuItem>();

            // ── 1. Dashboard & Data ──
            output.Add(new DataObjects.MenuItem {
                Title = "Dashboard & Data",
                Icon = "DashboardData",
                PageNames = new List<string> { "examplesdashboard", "sampleitems", "editsampleitem", "sampleitemsv1", "sampleitemsv2", "sampleitemsv3", "sampleitemsv4", "sampleitemsv5", "searchautocomplete", "comparisontable", "itemcards" },
                SortOrder = 100,
                url = Helpers.BuildUrl("Examples/Dashboard"),
                AppAdminOnly = false,
                DropdownItems = new List<DataObjects.MenuItem> {
                    MakeSubItem("Sample Items", "sampleitems", "Examples/SampleItems", "Files"),
                    MakeSubItem("V1: Card View", "sampleitemsv1", "Examples/SampleItemsV1"),
                    MakeSubItem("V2: Split Panel", "sampleitemsv2", "Examples/SampleItemsV2"),
                    MakeSubItem("V3: Grouped View", "sampleitemsv3", "Examples/SampleItemsV3"),
                    MakeSubItem("V4: Timeline", "sampleitemsv4", "Examples/SampleItemsV4"),
                    MakeSubItem("V5: Stats Dashboard", "sampleitemsv5", "Examples/SampleItemsV5"),
                    MakeSubItem("Search & Autocomplete", "searchautocomplete", "Examples/SearchAutocomplete"),
                    MakeSubItem("Comparison Table", "comparisontable", "Examples/ComparisonTable"),
                    MakeSubItem("Item Cards", "itemcards", "Examples/ItemCards"),
                },
            });

            // ── 2. Files & Media ──
            output.Add(new DataObjects.MenuItem {
                Title = "Files & Media",
                Icon = "FilesMedia",
                PageNames = new List<string> { "filedemo", "filedemov1", "filedemov2", "filedemov3", "filedemov4", "filedemov5", "filedemov6", "imagegallery", "carousel", "signaturedemo", "signaturev1", "signaturev2", "signaturev3", "signaturev4", "signaturev5" },
                SortOrder = 200,
                url = Helpers.BuildUrl("Examples/FileDemo"),
                AppAdminOnly = false,
                DropdownItems = new List<DataObjects.MenuItem> {
                    MakeSubItem("V1: Profile Photo", "filedemov1", "Examples/FileDemoV1"),
                    MakeSubItem("V2: Document Library", "filedemov2", "Examples/FileDemoV2"),
                    MakeSubItem("V3: Resume Upload", "filedemov3", "Examples/FileDemoV3"),
                    MakeSubItem("V4: Bulk Import", "filedemov4", "Examples/FileDemoV4"),
                    MakeSubItem("V5: Case Attachments", "filedemov5", "Examples/FileDemoV5"),
                    MakeSubItem("V6: Upload Policy", "filedemov6", "Examples/FileDemoV6"),
                    MakeSubItem("Image Gallery", "imagegallery", "Examples/ImageGallery"),
                    MakeSubItem("Carousel", "carousel", "Examples/Carousel"),
                    MakeSubItem("Signature Demo", "signaturedemo", "Examples/SignatureDemo"),
                    MakeSubItem("Sig V1: Job Application", "signaturev1", "Examples/SignatureV1"),
                    MakeSubItem("Sig V2: Doc Acknowledgment", "signaturev2", "Examples/SignatureV2"),
                    MakeSubItem("Sig V3: Upload & Sign", "signaturev3", "Examples/SignatureV3"),
                    MakeSubItem("Sig V4: GLBA Consent", "signaturev4", "Examples/SignatureV4"),
                    MakeSubItem("Sig V5: Contract Signing", "signaturev5", "Examples/SignatureV5"),
                },
            });

            // ── 3. UI Components ──
            output.Add(new DataObjects.MenuItem {
                Title = "UI Components",
                Icon = "UIComponents",
                PageNames = new List<string> { "bootstrapshowcase", "bootstrapv1", "bootstrapv2", "bootstrapv3", "bootstrapv4", "bootstrapv5", "bootstrapv6", "bootstrapv7", "bootstrapv8", "bootstrapv9", "bootstrapv10", "bootstrapv11", "bootstrapv12", "kanbanboard", "statusboard", "pipelinetracker", "wizarddemo", "commandpalette", "commentthread", "chatview" },
                SortOrder = 300,
                url = Helpers.BuildUrl("Examples/BootstrapShowcase"),
                AppAdminOnly = false,
                DropdownItems = new List<DataObjects.MenuItem> {
                    MakeSubItem("V1: Email Builder", "bootstrapv1", "Examples/BootstrapV1"),
                    MakeSubItem("V2: Settings Page", "bootstrapv2", "Examples/BootstrapV2"),
                    MakeSubItem("V3: Error Pages", "bootstrapv3", "Examples/BootstrapV3"),
                    MakeSubItem("V4: User Profile", "bootstrapv4", "Examples/BootstrapV4"),
                    MakeSubItem("V5: Pricing Page", "bootstrapv5", "Examples/BootstrapV5"),
                    MakeSubItem("V6: Filter Panel", "bootstrapv6", "Examples/BootstrapV6"),
                    MakeSubItem("V7: Settings Admin", "bootstrapv7", "Examples/BootstrapV7"),
                    MakeSubItem("V8: Offcanvas Sidebar", "bootstrapv8", "Examples/BootstrapV8"),
                    MakeSubItem("V9: Master-Detail", "bootstrapv9", "Examples/BootstrapV9"),
                    MakeSubItem("V10: Modals & Toasts", "bootstrapv10", "Examples/BootstrapV10"),
                    MakeSubItem("V11: Data Table", "bootstrapv11", "Examples/BootstrapV11"),
                    MakeSubItem("V12: Request Form", "bootstrapv12", "Examples/BootstrapV12"),
                    MakeSubItem("Kanban Board", "kanbanboard", "Examples/KanbanBoard"),
                    MakeSubItem("Status Board", "statusboard", "Examples/StatusBoard"),
                    MakeSubItem("Pipeline Tracker", "pipelinetracker", "Examples/PipelineTracker"),
                    MakeSubItem("Wizard Demo", "wizarddemo", "Examples/WizardDemo"),
                    MakeSubItem("Command Palette", "commandpalette", "Examples/CommandPalette"),
                    MakeSubItem("Comment Thread", "commentthread", "Examples/CommentThread"),
                    MakeSubItem("Chat View", "chatview", "Examples/ChatView"),
                },
            });

            // ── 4. Charts & Visualizations ──
            output.Add(new DataObjects.MenuItem {
                Title = "Charts & Viz",
                Icon = "ChartsViz",
                PageNames = new List<string> { "chartsdashboard", "chartsv1", "chartsv2", "chartsv3", "chartsv4", "chartsv5", "networkgraph", "networkgraphv1", "networkgraphv2" },
                SortOrder = 400,
                url = Helpers.BuildUrl("Examples/ChartsDashboard"),
                AppAdminOnly = false,
                DropdownItems = new List<DataObjects.MenuItem> {
                    MakeSubItem("V1: Sales Analytics", "chartsv1", "Examples/ChartsV1"),
                    MakeSubItem("V2: Enrollment Stats", "chartsv2", "Examples/ChartsV2"),
                    MakeSubItem("V3: System Health", "chartsv3", "Examples/ChartsV3"),
                    MakeSubItem("V4: HR Analytics", "chartsv4", "Examples/ChartsV4"),
                    MakeSubItem("V5: Web Analytics", "chartsv5", "Examples/ChartsV5"),
                    MakeSubItem("Network Graph", "networkgraph", "Examples/NetworkGraph"),
                    MakeSubItem("Net V1: Org Chart", "networkgraphv1", "Examples/NetworkGraphV1"),
                    MakeSubItem("Net V2: Dependency Map", "networkgraphv2", "Examples/NetworkGraphV2"),
                },
            });

            // ── 5. Code & Real-Time ──
            output.Add(new DataObjects.MenuItem {
                Title = "Code & Real-Time",
                Icon = "CodeRealTime",
                PageNames = new List<string> { "codeeditor", "codeeditorv1", "codeeditorv2", "codeeditorv3", "codeeditorv4", "codeeditorv5", "codeplayground", "signalrdemo", "signalrv1", "signalrv2", "signalrv3", "signalrv4", "signalrv5", "timerdemo", "timerv1", "timerv2", "timerv3", "timerv4", "timerv5", "gitbrowser", "apikeydemo" },
                SortOrder = 500,
                url = Helpers.BuildUrl("Examples/CodeEditor"),
                AppAdminOnly = false,
                DropdownItems = new List<DataObjects.MenuItem> {
                    MakeSubItem("V1: SQL Query Builder", "codeeditorv1", "Examples/CodeEditorV1"),
                    MakeSubItem("V2: API Tester", "codeeditorv2", "Examples/CodeEditorV2"),
                    MakeSubItem("V3: Config Editor", "codeeditorv3", "Examples/CodeEditorV3"),
                    MakeSubItem("V4: Diff Viewer", "codeeditorv4", "Examples/CodeEditorV4"),
                    MakeSubItem("V5: Template Engine", "codeeditorv5", "Examples/CodeEditorV5"),
                    MakeSubItem("Code Playground", "codeplayground", "Examples/CodePlayground"),
                    MakeSubItem("SignalR Demo", "signalrdemo", "Examples/SignalRDemo"),
                    MakeSubItem("SR V1: Live Notifications", "signalrv1", "Examples/SignalRV1"),
                    MakeSubItem("SR V2: Online Users", "signalrv2", "Examples/SignalRV2"),
                    MakeSubItem("SR V3: Live Poll", "signalrv3", "Examples/SignalRV3"),
                    MakeSubItem("SR V4: Activity Feed", "signalrv4", "Examples/SignalRV4"),
                    MakeSubItem("SR V5: Live Scoreboard", "signalrv5", "Examples/SignalRV5"),
                    MakeSubItem("Timer Demo", "timerdemo", "Examples/TimerDemo"),
                    MakeSubItem("Timer V1: Pomodoro", "timerv1", "Examples/TimerV1"),
                    MakeSubItem("Timer V2: Session Timeout", "timerv2", "Examples/TimerV2"),
                    MakeSubItem("Timer V3: Auto-Refresh", "timerv3", "Examples/TimerV3"),
                    MakeSubItem("Timer V4: Quiz Timer", "timerv4", "Examples/TimerV4"),
                    MakeSubItem("Timer V5: Event Countdown", "timerv5", "Examples/TimerV5"),
                    MakeSubItem("Git Browser", "gitbrowser", "Examples/GitBrowser"),
                    MakeSubItem("API Key Demo", "apikeydemo", "Examples/ApiKeyDemo"),
                },
            });

            // ── 6. Project Management ──
            output.Add(new DataObjects.MenuItem {
                Title = "Project Management",
                Icon = "ProjectManagement",
                PageNames = new List<string> { "projects", "projectsv1", "projectsv2", "projectsv3", "projectsv4", "tickets", "ticketsv1", "ticketsv2", "ticketsv3", "ticketsv4", "boardviews", "boardviewsv1", "boardviewsv2", "boardviewsv3", "boardviewsv4", "sprintplanning", "sprintplanningv1", "sprintplanningv2", "sprintplanningv3", "sprintplanningv4", "backlog", "backlogv1", "backlogv2", "backlogv3", "backlogv4" },
                SortOrder = 600,
                url = Helpers.BuildUrl("Examples/Projects"),
                AppAdminOnly = false,
                DropdownItems = new List<DataObjects.MenuItem> {
                    MakeSubItem("Projects", "projects", "Examples/Projects"),
                    MakeSubItem("V1: Project Tree", "projectsv1", "Examples/ProjectsV1"),
                    MakeSubItem("V2: Project Form", "projectsv2", "Examples/ProjectsV2"),
                    MakeSubItem("V3: Project Cards", "projectsv3", "Examples/ProjectsV3"),
                    MakeSubItem("V4: Project Comparison", "projectsv4", "Examples/ProjectsV4"),
                    MakeSubItem("Tickets", "tickets", "Examples/Tickets"),
                    MakeSubItem("V1: Ticket Form", "ticketsv1", "Examples/TicketsV1"),
                    MakeSubItem("V2: Ticket Detail", "ticketsv2", "Examples/TicketsV2"),
                    MakeSubItem("V3: Quick Create", "ticketsv3", "Examples/TicketsV3"),
                    MakeSubItem("V4: Bulk Edit", "ticketsv4", "Examples/TicketsV4"),
                    MakeSubItem("Board Views", "boardviews", "Examples/BoardViews"),
                    MakeSubItem("V1: Kanban Board", "boardviewsv1", "Examples/BoardViewsV1"),
                    MakeSubItem("V2: Sprint Board", "boardviewsv2", "Examples/BoardViewsV2"),
                    MakeSubItem("V3: Swimlane Board", "boardviewsv3", "Examples/BoardViewsV3"),
                    MakeSubItem("V4: Board Settings", "boardviewsv4", "Examples/BoardViewsV4"),
                    MakeSubItem("Sprint Planning", "sprintplanning", "Examples/SprintPlanning"),
                    MakeSubItem("V1: Planning View", "sprintplanningv1", "Examples/SprintPlanningV1"),
                    MakeSubItem("V2: Active Sprint", "sprintplanningv2", "Examples/SprintPlanningV2"),
                    MakeSubItem("V3: Retrospective", "sprintplanningv3", "Examples/SprintPlanningV3"),
                    MakeSubItem("V4: Velocity Report", "sprintplanningv4", "Examples/SprintPlanningV4"),
                    MakeSubItem("Backlog", "backlog", "Examples/Backlog"),
                    MakeSubItem("V1: Grooming View", "backlogv1", "Examples/BacklogV1"),
                    MakeSubItem("V2: Bulk Operations", "backlogv2", "Examples/BacklogV2"),
                    MakeSubItem("V3: Grouped Backlog", "backlogv3", "Examples/BacklogV3"),
                    MakeSubItem("V4: Saved Views", "backlogv4", "Examples/BacklogV4"),
                },
            });

            // ── 7. Domain Workflows ──
            output.Add(new DataObjects.MenuItem {
                Title = "Domain Workflows",
                Icon = "DomainWorkflows",
                PageNames = new List<string> { "workorders", "workordersv1", "workordersv2", "workordersv3", "workordersv4", "budgetrequests", "budgetrequestsv1", "budgetrequestsv2", "budgetrequestsv3", "budgetrequestsv4", "equipmentcheckout", "equipmentcheckoutv1", "equipmentcheckoutv2", "equipmentcheckoutv3", "equipmentcheckoutv4", "courseevaluations", "courseevaluationsv1", "courseevaluationsv2", "courseevaluationsv3", "courseevaluationsv4", "employeeonboarding", "employeeonboardingv1", "employeeonboardingv2", "employeeonboardingv3", "employeeonboardingv4" },
                SortOrder = 700,
                url = Helpers.BuildUrl("Examples/WorkOrders"),
                AppAdminOnly = false,
                DropdownItems = new List<DataObjects.MenuItem> {
                    MakeSubItem("Work Orders", "workorders", "Examples/WorkOrders"),
                    MakeSubItem("V1: Submit Request", "workordersv1", "Examples/WorkOrdersV1"),
                    MakeSubItem("V2: Dispatch Board", "workordersv2", "Examples/WorkOrdersV2"),
                    MakeSubItem("V3: Technician View", "workordersv3", "Examples/WorkOrdersV3"),
                    MakeSubItem("V4: Facilities Dashboard", "workordersv4", "Examples/WorkOrdersV4"),
                    MakeSubItem("Budget Requests", "budgetrequests", "Examples/BudgetRequests"),
                    MakeSubItem("V1: Request Builder", "budgetrequestsv1", "Examples/BudgetRequestsV1"),
                    MakeSubItem("V2: Approval Queue", "budgetrequestsv2", "Examples/BudgetRequestsV2"),
                    MakeSubItem("V3: Budget Overview", "budgetrequestsv3", "Examples/BudgetRequestsV3"),
                    MakeSubItem("V4: Request Detail", "budgetrequestsv4", "Examples/BudgetRequestsV4"),
                    MakeSubItem("Equipment Checkout", "equipmentcheckout", "Examples/EquipmentCheckout"),
                    MakeSubItem("V1: Checkout Form", "equipmentcheckoutv1", "Examples/EquipmentCheckoutV1"),
                    MakeSubItem("V2: My Checkouts", "equipmentcheckoutv2", "Examples/EquipmentCheckoutV2"),
                    MakeSubItem("V3: Overdue Report", "equipmentcheckoutv3", "Examples/EquipmentCheckoutV3"),
                    MakeSubItem("V4: Asset Detail", "equipmentcheckoutv4", "Examples/EquipmentCheckoutV4"),
                    MakeSubItem("Course Evaluations", "courseevaluations", "Examples/CourseEvaluations"),
                    MakeSubItem("V1: Take Evaluation", "courseevaluationsv1", "Examples/CourseEvaluationsV1"),
                    MakeSubItem("V2: Results Summary", "courseevaluationsv2", "Examples/CourseEvaluationsV2"),
                    MakeSubItem("V3: Template Builder", "courseevaluationsv3", "Examples/CourseEvaluationsV3"),
                    MakeSubItem("V4: Department Report", "courseevaluationsv4", "Examples/CourseEvaluationsV4"),
                    MakeSubItem("Employee Onboarding", "employeeonboarding", "Examples/EmployeeOnboarding"),
                    MakeSubItem("V1: Onboarding Setup", "employeeonboardingv1", "Examples/EmployeeOnboardingV1"),
                    MakeSubItem("V2: Task Tracker", "employeeonboardingv2", "Examples/EmployeeOnboardingV2"),
                    MakeSubItem("V3: My Onboarding", "employeeonboardingv3", "Examples/EmployeeOnboardingV3"),
                    MakeSubItem("V4: Department Dashboard", "employeeonboardingv4", "Examples/EmployeeOnboardingV4"),
                },
            });

            return output;
        }
    }

    private static DataObjects.MenuItem MakeSubItem(string title, string pageName, string route, string? icon = null)
    {
        return new DataObjects.MenuItem {
            Title = title,
            Icon = icon ?? "",
            PageNames = new List<string> { pageName },
            url = Helpers.BuildUrl(route),
            AppAdminOnly = false,
        };
    }

}
