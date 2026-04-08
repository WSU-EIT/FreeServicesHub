namespace FreeExamples;

public partial class DataAccess
{
    // ── Seed: Projects ──
    private void SeedProjects()
    {
        var projects = new List<DataObjects.Project> {
            new() { RecordId = Guid.Parse("a0000001-0000-0000-0000-000000000001"), Name = "Website Redesign", ProjectKey = "WEB", LeadName = "Alice Chen", LeadEmail = "achen@example.edu", Department = "Engineering", Status = DataObjects.ProjectStatus.Active, StartDate = DateTime.UtcNow.AddDays(-60), TargetEndDate = DateTime.UtcNow.AddDays(30), Color = "#0d6efd", SortOrder = 1, NextTicketNumber = 13 },
            new() { RecordId = Guid.Parse("a0000001-0000-0000-0000-000000000002"), Name = "Frontend", ProjectKey = "FE", LeadName = "Bob Martinez", Department = "Engineering", Status = DataObjects.ProjectStatus.Active, ParentProjectId = Guid.Parse("a0000001-0000-0000-0000-000000000001"), Color = "#198754", SortOrder = 1, NextTicketNumber = 8 },
            new() { RecordId = Guid.Parse("a0000001-0000-0000-0000-000000000003"), Name = "Backend API", ProjectKey = "API", LeadName = "Carol Davis", Department = "Engineering", Status = DataObjects.ProjectStatus.Active, ParentProjectId = Guid.Parse("a0000001-0000-0000-0000-000000000001"), Color = "#6f42c1", SortOrder = 2, NextTicketNumber = 6 },
            new() { RecordId = Guid.Parse("a0000001-0000-0000-0000-000000000004"), Name = "Navigation", ProjectKey = "NAV", LeadName = "Bob Martinez", Department = "Engineering", Status = DataObjects.ProjectStatus.Active, ParentProjectId = Guid.Parse("a0000001-0000-0000-0000-000000000002"), Color = "#20c997", SortOrder = 1, NextTicketNumber = 4 },
            new() { RecordId = Guid.Parse("a0000001-0000-0000-0000-000000000005"), Name = "HR Portal", ProjectKey = "HR", LeadName = "Diana Lopez", LeadEmail = "dlopez@example.edu", Department = "Human Resources", Status = DataObjects.ProjectStatus.Planning, StartDate = DateTime.UtcNow.AddDays(15), TargetEndDate = DateTime.UtcNow.AddDays(120), Color = "#dc3545", SortOrder = 2, NextTicketNumber = 5 },
            new() { RecordId = Guid.Parse("a0000001-0000-0000-0000-000000000006"), Name = "Campus Safety App", ProjectKey = "SAF", LeadName = "Ed Thompson", Department = "Campus Safety", Status = DataObjects.ProjectStatus.Completed, StartDate = DateTime.UtcNow.AddDays(-120), TargetEndDate = DateTime.UtcNow.AddDays(-10), Color = "#ffc107", SortOrder = 3, NextTicketNumber = 9 },
            new() { RecordId = Guid.Parse("a0000001-0000-0000-0000-000000000007"), Name = "Data Migration", ProjectKey = "MIG", LeadName = "Fran Nguyen", Department = "IT", Status = DataObjects.ProjectStatus.OnHold, Color = "#6c757d", SortOrder = 4, NextTicketNumber = 3 },
            new() { RecordId = Guid.Parse("a0000001-0000-0000-0000-000000000008"), Name = "Student Portal", ProjectKey = "STU", LeadName = "Grace Kim", LeadEmail = "gkim@example.edu", Department = "Student Services", Status = DataObjects.ProjectStatus.Active, StartDate = DateTime.UtcNow.AddDays(-30), TargetEndDate = DateTime.UtcNow.AddDays(90), Color = "#0dcaf0", SortOrder = 5, NextTicketNumber = 7 },
        };
        SaveJsonRecords<DataObjects.Project>(projects, null);
    }

    // ── Seed: Tickets ──
    private void SeedTickets()
    {
        var webId = Guid.Parse("a0000001-0000-0000-0000-000000000001");
        var feId = Guid.Parse("a0000001-0000-0000-0000-000000000002");
        var apiId = Guid.Parse("a0000001-0000-0000-0000-000000000003");
        var hrId = Guid.Parse("a0000001-0000-0000-0000-000000000005");
        var stuId = Guid.Parse("a0000001-0000-0000-0000-000000000008");
        var sprint1 = Guid.Parse("b0000001-0000-0000-0000-000000000001");
        var sprint2 = Guid.Parse("b0000001-0000-0000-0000-000000000002");

        var tickets = new List<DataObjects.Ticket> {
            // Website Redesign tickets
            new() { RecordId = Guid.Parse("c0000001-0000-0000-0000-000000000001"), ProjectId = webId, TicketNumber = "WEB-1", Title = "Design new homepage layout", Type = DataObjects.TicketType.Story, Status = DataObjects.TicketStatus.Done, Priority = DataObjects.TicketPriority.High, AssignedTo = "Bob Martinez", ReporterName = "Alice Chen", StoryPoints = 8, SprintId = sprint1, Labels = "design,frontend", CompletedDate = DateTime.UtcNow.AddDays(-20), SortOrder = 1,
                Comments = [ new() { CommentId = Guid.NewGuid(), AuthorName = "Alice Chen", Body = "Great work on the hero section!", CreatedDate = DateTime.UtcNow.AddDays(-21) } ] },
            new() { RecordId = Guid.Parse("c0000001-0000-0000-0000-000000000002"), ProjectId = webId, TicketNumber = "WEB-2", Title = "Implement responsive navigation", Type = DataObjects.TicketType.Story, Status = DataObjects.TicketStatus.InProgress, Priority = DataObjects.TicketPriority.High, AssignedTo = "Bob Martinez", ReporterName = "Alice Chen", StoryPoints = 5, SprintId = sprint2, Labels = "frontend,nav", StartedDate = DateTime.UtcNow.AddDays(-5), SortOrder = 2 },
            new() { RecordId = Guid.Parse("c0000001-0000-0000-0000-000000000003"), ProjectId = webId, TicketNumber = "WEB-3", Title = "Fix broken image links on About page", Type = DataObjects.TicketType.Bug, Status = DataObjects.TicketStatus.ToDo, Priority = DataObjects.TicketPriority.Medium, ReporterName = "Diana Lopez", SprintId = sprint2, SortOrder = 3 },
            new() { RecordId = Guid.Parse("c0000001-0000-0000-0000-000000000004"), ProjectId = webId, TicketNumber = "WEB-4", Title = "Add dark mode support", Type = DataObjects.TicketType.Story, Status = DataObjects.TicketStatus.Backlog, Priority = DataObjects.TicketPriority.Low, ReporterName = "Bob Martinez", StoryPoints = 13, Labels = "enhancement,frontend", SortOrder = 4 },
            new() { RecordId = Guid.Parse("c0000001-0000-0000-0000-000000000005"), ProjectId = webId, TicketNumber = "WEB-5", Title = "Performance audit and optimization", Type = DataObjects.TicketType.Task, Status = DataObjects.TicketStatus.Backlog, Priority = DataObjects.TicketPriority.Medium, ReporterName = "Carol Davis", StoryPoints = 8, Labels = "performance", SortOrder = 5 },
            new() { RecordId = Guid.Parse("c0000001-0000-0000-0000-000000000006"), ProjectId = webId, TicketNumber = "WEB-6", Title = "Website Redesign Epic", Type = DataObjects.TicketType.Epic, Status = DataObjects.TicketStatus.InProgress, Priority = DataObjects.TicketPriority.High, AssignedTo = "Alice Chen", ReporterName = "Alice Chen", StoryPoints = 21, SortOrder = 0 },

            // Frontend tickets
            new() { RecordId = Guid.Parse("c0000001-0000-0000-0000-000000000007"), ProjectId = feId, TicketNumber = "FE-1", Title = "Create reusable card component", Type = DataObjects.TicketType.Task, Status = DataObjects.TicketStatus.Done, Priority = DataObjects.TicketPriority.Medium, AssignedTo = "Bob Martinez", ReporterName = "Bob Martinez", StoryPoints = 3, SprintId = sprint1, CompletedDate = DateTime.UtcNow.AddDays(-18), SortOrder = 1 },
            new() { RecordId = Guid.Parse("c0000001-0000-0000-0000-000000000008"), ProjectId = feId, TicketNumber = "FE-2", Title = "Form validation not showing errors", Type = DataObjects.TicketType.Bug, Status = DataObjects.TicketStatus.InReview, Priority = DataObjects.TicketPriority.High, AssignedTo = "Bob Martinez", ReporterName = "Diana Lopez", StoryPoints = 3, SprintId = sprint2, StartedDate = DateTime.UtcNow.AddDays(-3), SortOrder = 2 },
            new() { RecordId = Guid.Parse("c0000001-0000-0000-0000-000000000009"), ProjectId = feId, TicketNumber = "FE-3", Title = "Accessibility audit for all forms", Type = DataObjects.TicketType.Task, Status = DataObjects.TicketStatus.Backlog, Priority = DataObjects.TicketPriority.Medium, ReporterName = "Grace Kim", StoryPoints = 5, Labels = "accessibility", SortOrder = 3 },

            // Backend API tickets
            new() { RecordId = Guid.Parse("c0000001-0000-0000-0000-000000000010"), ProjectId = apiId, TicketNumber = "API-1", Title = "Design REST API schema", Type = DataObjects.TicketType.Story, Status = DataObjects.TicketStatus.Done, Priority = DataObjects.TicketPriority.Critical, AssignedTo = "Carol Davis", ReporterName = "Alice Chen", StoryPoints = 8, SprintId = sprint1, CompletedDate = DateTime.UtcNow.AddDays(-15), SortOrder = 1 },
            new() { RecordId = Guid.Parse("c0000001-0000-0000-0000-000000000011"), ProjectId = apiId, TicketNumber = "API-2", Title = "Implement authentication middleware", Type = DataObjects.TicketType.Story, Status = DataObjects.TicketStatus.Testing, Priority = DataObjects.TicketPriority.Critical, AssignedTo = "Carol Davis", ReporterName = "Carol Davis", StoryPoints = 8, SprintId = sprint2, StartedDate = DateTime.UtcNow.AddDays(-7), SortOrder = 2 },
            new() { RecordId = Guid.Parse("c0000001-0000-0000-0000-000000000012"), ProjectId = apiId, TicketNumber = "API-3", Title = "Rate limiting returns wrong HTTP status", Type = DataObjects.TicketType.Bug, Status = DataObjects.TicketStatus.ToDo, Priority = DataObjects.TicketPriority.High, ReporterName = "Ed Thompson", SprintId = sprint2, StoryPoints = 2, SortOrder = 3 },

            // HR Portal tickets
            new() { RecordId = Guid.Parse("c0000001-0000-0000-0000-000000000013"), ProjectId = hrId, TicketNumber = "HR-1", Title = "Employee directory page", Type = DataObjects.TicketType.Story, Status = DataObjects.TicketStatus.Backlog, Priority = DataObjects.TicketPriority.High, ReporterName = "Diana Lopez", StoryPoints = 8, SortOrder = 1 },
            new() { RecordId = Guid.Parse("c0000001-0000-0000-0000-000000000014"), ProjectId = hrId, TicketNumber = "HR-2", Title = "PTO request form", Type = DataObjects.TicketType.Story, Status = DataObjects.TicketStatus.Backlog, Priority = DataObjects.TicketPriority.Medium, ReporterName = "Diana Lopez", StoryPoints = 5, SortOrder = 2 },
            new() { RecordId = Guid.Parse("c0000001-0000-0000-0000-000000000015"), ProjectId = hrId, TicketNumber = "HR-3", Title = "Benefits enrollment wizard", Type = DataObjects.TicketType.Epic, Status = DataObjects.TicketStatus.Backlog, Priority = DataObjects.TicketPriority.Medium, ReporterName = "Diana Lopez", StoryPoints = 21, SortOrder = 3 },

            // Student Portal tickets
            new() { RecordId = Guid.Parse("c0000001-0000-0000-0000-000000000016"), ProjectId = stuId, TicketNumber = "STU-1", Title = "Course registration flow", Type = DataObjects.TicketType.Story, Status = DataObjects.TicketStatus.InProgress, Priority = DataObjects.TicketPriority.Critical, AssignedTo = "Grace Kim", ReporterName = "Grace Kim", StoryPoints = 13, SprintId = sprint2, StartedDate = DateTime.UtcNow.AddDays(-4), SortOrder = 1 },
            new() { RecordId = Guid.Parse("c0000001-0000-0000-0000-000000000017"), ProjectId = stuId, TicketNumber = "STU-2", Title = "Transcript download crashes on Safari", Type = DataObjects.TicketType.Bug, Status = DataObjects.TicketStatus.Backlog, Priority = DataObjects.TicketPriority.High, ReporterName = "Fran Nguyen", Labels = "browser,safari", SortOrder = 2 },
            new() { RecordId = Guid.Parse("c0000001-0000-0000-0000-000000000018"), ProjectId = stuId, TicketNumber = "STU-3", Title = "Add GPA calculator widget", Type = DataObjects.TicketType.Improvement, Status = DataObjects.TicketStatus.Backlog, Priority = DataObjects.TicketPriority.Low, ReporterName = "Grace Kim", StoryPoints = 5, SortOrder = 3 },
        };
        SaveJsonRecords<DataObjects.Ticket>(tickets, null);
    }

    // ── Seed: Sprints ──
    private void SeedSprints()
    {
        var webId = Guid.Parse("a0000001-0000-0000-0000-000000000001");
        var stuId = Guid.Parse("a0000001-0000-0000-0000-000000000008");

        var sprints = new List<DataObjects.Sprint> {
            new() { RecordId = Guid.Parse("b0000001-0000-0000-0000-000000000001"), ProjectId = webId, Name = "Sprint 1 — Foundation", Goal = "Core layout and API schema done", StartDate = DateTime.UtcNow.AddDays(-28), EndDate = DateTime.UtcNow.AddDays(-14), Status = DataObjects.SprintStatus.Completed, CapacityPoints = 21 },
            new() { RecordId = Guid.Parse("b0000001-0000-0000-0000-000000000002"), ProjectId = webId, Name = "Sprint 2 — Navigation & Auth", Goal = "Responsive nav and auth middleware", StartDate = DateTime.UtcNow.AddDays(-14), EndDate = DateTime.UtcNow.AddDays(0), Status = DataObjects.SprintStatus.Active, CapacityPoints = 26 },
            new() { RecordId = Guid.Parse("b0000001-0000-0000-0000-000000000003"), ProjectId = webId, Name = "Sprint 3 — Polish", Goal = "Bug fixes and performance", StartDate = DateTime.UtcNow.AddDays(1), EndDate = DateTime.UtcNow.AddDays(15), Status = DataObjects.SprintStatus.Planning, CapacityPoints = 21 },
            new() { RecordId = Guid.Parse("b0000001-0000-0000-0000-000000000004"), ProjectId = stuId, Name = "STU Sprint 1", Goal = "Course registration MVP", StartDate = DateTime.UtcNow.AddDays(-14), EndDate = DateTime.UtcNow.AddDays(0), Status = DataObjects.SprintStatus.Active, CapacityPoints = 18 },
            // Historical sprints for velocity charts
            new() { RecordId = Guid.Parse("b0000001-0000-0000-0000-000000000005"), ProjectId = webId, Name = "Sprint 0 — Setup", Goal = "Project setup and tooling", StartDate = DateTime.UtcNow.AddDays(-56), EndDate = DateTime.UtcNow.AddDays(-42), Status = DataObjects.SprintStatus.Completed, CapacityPoints = 13 },
            new() { RecordId = Guid.Parse("b0000001-0000-0000-0000-000000000006"), ProjectId = webId, Name = "Sprint -1 — Discovery", Goal = "Requirements gathering", StartDate = DateTime.UtcNow.AddDays(-70), EndDate = DateTime.UtcNow.AddDays(-56), Status = DataObjects.SprintStatus.Completed, CapacityPoints = 8 },
        };
        SaveJsonRecords<DataObjects.Sprint>(sprints, null);
    }

    // ── Seed: Board Configs ──
    private void SeedBoardConfigs()
    {
        var webId = Guid.Parse("a0000001-0000-0000-0000-000000000001");
        var configs = new List<DataObjects.BoardConfig> {
            new() { RecordId = Guid.NewGuid(), ProjectId = webId, BoardName = "Dev Board", BoardType = DataObjects.BoardType.Kanban, ColumnConfig = "[\"Backlog\",\"ToDo\",\"InProgress\",\"InReview\",\"Done\"]", WipLimits = "{\"InProgress\":5,\"InReview\":3}", CreatedBy = "Alice Chen" },
            new() { RecordId = Guid.NewGuid(), ProjectId = webId, BoardName = "Sprint Board", BoardType = DataObjects.BoardType.Sprint, ColumnConfig = "[\"ToDo\",\"InProgress\",\"InReview\",\"Testing\",\"Done\"]", SwimlaneField = "assignee", CreatedBy = "Alice Chen" },
        };
        SaveJsonRecords<DataObjects.BoardConfig>(configs, null);
    }

    // ── Seed: Work Orders ──
    private void SeedWorkOrders()
    {
        var rng = new Random(100);
        var buildings = new[] { "Science Hall", "Library", "Student Union", "Engineering Bldg", "Admin Tower" };
        var floors = new[] { "1st Floor", "2nd Floor", "3rd Floor", "Basement" };
        var rooms = new[] { "101", "205", "310", "B12", "118", "220", "315" };
        var techs = new[] { "Mike Johnson", "Sarah Wilson", "Tom Brown", null };
        var teams = new[] { "Plumbing Crew", "Electrical Crew", "HVAC Team", "General Maintenance" };
        var requesters = new[] { ("Jane Faculty", "jfaculty@example.edu"), ("Mark Staff", "mstaff@example.edu"), ("Lisa Admin", "ladmin@example.edu"), ("Prof. Garcia", "garcia@example.edu"), ("Dean Roberts", "droberts@example.edu") };

        var orders = new List<DataObjects.WorkOrder>();
        var titles = new[] {
            "Leaking faucet in restroom", "Flickering lights in hallway", "AC not working — room too hot",
            "Broken door lock", "Clogged drain in lab sink", "Power outlet sparking", "Heating vent blocked",
            "Window won't close properly", "Elevator stuck on 2nd floor", "Parking lot light out",
            "Graffiti on exterior wall", "Water stain on ceiling tile", "Thermostat reads wrong temp",
            "Emergency exit sign burnt out", "Toilet running continuously", "Carpet stain near entrance",
            "Mouse spotted in break room", "Squeaky door hinge", "Broken blinds in classroom"
        };
        var categories = new[] { DataObjects.WorkOrderCategory.Plumbing, DataObjects.WorkOrderCategory.Electrical, DataObjects.WorkOrderCategory.HVAC, DataObjects.WorkOrderCategory.Custodial, DataObjects.WorkOrderCategory.Plumbing, DataObjects.WorkOrderCategory.Electrical, DataObjects.WorkOrderCategory.HVAC, DataObjects.WorkOrderCategory.Other, DataObjects.WorkOrderCategory.Other, DataObjects.WorkOrderCategory.Grounds, DataObjects.WorkOrderCategory.Custodial, DataObjects.WorkOrderCategory.Plumbing, DataObjects.WorkOrderCategory.HVAC, DataObjects.WorkOrderCategory.Electrical, DataObjects.WorkOrderCategory.Plumbing, DataObjects.WorkOrderCategory.Custodial, DataObjects.WorkOrderCategory.Other, DataObjects.WorkOrderCategory.Other, DataObjects.WorkOrderCategory.Other };
        var statuses = new[] { DataObjects.WorkOrderStatus.Submitted, DataObjects.WorkOrderStatus.Assigned, DataObjects.WorkOrderStatus.InProgress, DataObjects.WorkOrderStatus.Completed, DataObjects.WorkOrderStatus.Closed };

        for (int i = 0; i < titles.Length; i++) {
            var req = requesters[rng.Next(requesters.Length)];
            var status = statuses[rng.Next(statuses.Length)];
            var tech = status >= DataObjects.WorkOrderStatus.Assigned ? techs[rng.Next(techs.Length)] : null;
            orders.Add(new DataObjects.WorkOrder {
                RecordId = Guid.NewGuid(),
                Title = titles[i],
                Description = $"Reported issue: {titles[i]}. Please address as soon as possible.",
                Building = buildings[rng.Next(buildings.Length)],
                Floor = floors[rng.Next(floors.Length)],
                RoomNumber = rooms[rng.Next(rooms.Length)],
                Category = categories[i],
                Urgency = (DataObjects.WorkOrderUrgency)rng.Next(4),
                Status = status,
                AssignedTo = tech,
                AssignedTeam = tech != null ? teams[rng.Next(teams.Length)] : null,
                RequestedBy = req.Item1,
                RequestedByEmail = req.Item2,
                RequestedDate = DateTime.UtcNow.AddDays(-rng.Next(1, 45)),
                CompletedDate = status >= DataObjects.WorkOrderStatus.Completed ? DateTime.UtcNow.AddDays(-rng.Next(0, 5)) : null,
                EstimatedHours = Math.Round((decimal)(rng.NextDouble() * 4 + 0.5), 1),
                ActualHours = status >= DataObjects.WorkOrderStatus.Completed ? Math.Round((decimal)(rng.NextDouble() * 5 + 0.5), 1) : null,
            });
        }
        SaveJsonRecords<DataObjects.WorkOrder>(orders, null);
    }

    // ── Seed: Budget Requests ──
    private void SeedBudgetRequests()
    {
        var requests = new List<DataObjects.BudgetRequest> {
            new() { RecordId = Guid.NewGuid(), Title = "New Lab Laptops", Justification = "Current laptops are 5+ years old and cannot run required software.", Department = "Engineering", FiscalYear = "FY2025", RequestedBy = "Alice Chen", RequestedDate = DateTime.UtcNow.AddDays(-30), Status = DataObjects.BudgetRequestStatus.Approved, AccountCode = "GL-5200", ApprovedAmount = 12500m, SupervisorName = "Dean Roberts", SupervisorDate = DateTime.UtcNow.AddDays(-25), FinanceReviewerName = "CFO Williams", FinanceDate = DateTime.UtcNow.AddDays(-20),
                LineItems = [
                    new() { LineItemId = Guid.NewGuid(), Description = "Dell Latitude 5540", Vendor = "Dell", Quantity = 10, UnitPrice = 1150m, Category = DataObjects.BudgetLineCategory.Equipment },
                    new() { LineItemId = Guid.NewGuid(), Description = "Docking Stations", Vendor = "Dell", Quantity = 10, UnitPrice = 95m, Category = DataObjects.BudgetLineCategory.Equipment },
                    new() { LineItemId = Guid.NewGuid(), Description = "Setup & Imaging", Vendor = "IT Services", Quantity = 1, UnitPrice = 500m, Category = DataObjects.BudgetLineCategory.Services },
                ], TotalAmount = 12450m },
            new() { RecordId = Guid.NewGuid(), Title = "Conference Travel — SIGCSE 2025", Justification = "Two faculty presenting papers at SIGCSE conference.", Department = "Computer Science", FiscalYear = "FY2025", RequestedBy = "Prof. Garcia", RequestedDate = DateTime.UtcNow.AddDays(-15), Status = DataObjects.BudgetRequestStatus.SupervisorApproved, AccountCode = "GL-6100", SupervisorName = "Dean Roberts", SupervisorDate = DateTime.UtcNow.AddDays(-12),
                LineItems = [
                    new() { LineItemId = Guid.NewGuid(), Description = "Airfare (2 travelers)", Quantity = 2, UnitPrice = 450m, Category = DataObjects.BudgetLineCategory.Travel },
                    new() { LineItemId = Guid.NewGuid(), Description = "Hotel (3 nights × 2 rooms)", Quantity = 6, UnitPrice = 189m, Category = DataObjects.BudgetLineCategory.Travel },
                    new() { LineItemId = Guid.NewGuid(), Description = "Registration Fee", Quantity = 2, UnitPrice = 375m, Category = DataObjects.BudgetLineCategory.Services },
                    new() { LineItemId = Guid.NewGuid(), Description = "Per Diem", Quantity = 6, UnitPrice = 75m, Category = DataObjects.BudgetLineCategory.Travel },
                ], TotalAmount = 3384m },
            new() { RecordId = Guid.NewGuid(), Title = "Office Supplies — Q3", Justification = "Quarterly supply order for department.", Department = "Human Resources", FiscalYear = "FY2025", RequestedBy = "Diana Lopez", RequestedDate = DateTime.UtcNow.AddDays(-5), Status = DataObjects.BudgetRequestStatus.Submitted, AccountCode = "GL-4100",
                LineItems = [
                    new() { LineItemId = Guid.NewGuid(), Description = "Copy Paper (cases)", Quantity = 20, UnitPrice = 45m, Category = DataObjects.BudgetLineCategory.Supplies },
                    new() { LineItemId = Guid.NewGuid(), Description = "Toner Cartridges", Vendor = "Staples", Quantity = 8, UnitPrice = 62m, Category = DataObjects.BudgetLineCategory.Supplies },
                    new() { LineItemId = Guid.NewGuid(), Description = "Binder Clips & Folders", Quantity = 1, UnitPrice = 120m, Category = DataObjects.BudgetLineCategory.Supplies },
                ], TotalAmount = 1516m },
            new() { RecordId = Guid.NewGuid(), Title = "Software License Renewal — JetBrains", Justification = "Annual renewal for development team IDE licenses.", Department = "Engineering", FiscalYear = "FY2025", RequestedBy = "Carol Davis", RequestedDate = DateTime.UtcNow.AddDays(-45), Status = DataObjects.BudgetRequestStatus.Completed, AccountCode = "GL-5300", ApprovedAmount = 2997m, SupervisorName = "Alice Chen", SupervisorDate = DateTime.UtcNow.AddDays(-43), FinanceReviewerName = "CFO Williams", FinanceDate = DateTime.UtcNow.AddDays(-40),
                LineItems = [
                    new() { LineItemId = Guid.NewGuid(), Description = "IntelliJ IDEA Ultimate", Vendor = "JetBrains", Quantity = 3, UnitPrice = 599m, Category = DataObjects.BudgetLineCategory.Software },
                    new() { LineItemId = Guid.NewGuid(), Description = "ReSharper", Vendor = "JetBrains", Quantity = 5, UnitPrice = 240m, Category = DataObjects.BudgetLineCategory.Software },
                ], TotalAmount = 2997m },
            new() { RecordId = Guid.NewGuid(), Title = "New Standing Desks", Justification = "Ergonomic improvement per HR wellness initiative.", Department = "Student Services", FiscalYear = "FY2025", RequestedBy = "Grace Kim", RequestedDate = DateTime.UtcNow.AddDays(-3), Status = DataObjects.BudgetRequestStatus.Draft, AccountCode = "GL-5400",
                LineItems = [
                    new() { LineItemId = Guid.NewGuid(), Description = "Electric Standing Desk", Vendor = "Uplift", Quantity = 6, UnitPrice = 649m, Category = DataObjects.BudgetLineCategory.Equipment },
                    new() { LineItemId = Guid.NewGuid(), Description = "Anti-Fatigue Mats", Quantity = 6, UnitPrice = 39m, Category = DataObjects.BudgetLineCategory.Supplies },
                ], TotalAmount = 4128m },
        };
        SaveJsonRecords<DataObjects.BudgetRequest>(requests, null);
    }

    // ── Seed: Equipment ──
    private void SeedEquipment()
    {
        var items = new List<DataObjects.Equipment> {
            new() { RecordId = Guid.NewGuid(), Name = "Dell Latitude 5540 #1", Category = DataObjects.EquipmentCategory.Laptop, AssetTag = "EQ-2024-0001", SerialNumber = "DL5540-A001", Location = "IT Office — Room 105", Condition = DataObjects.EquipmentCondition.Good, PurchaseDate = DateTime.UtcNow.AddDays(-365), PurchasePrice = 1150m,
                Checkouts = [
                    new() { CheckoutId = Guid.NewGuid(), BorrowerName = "Mark Staff", BorrowerEmail = "mstaff@example.edu", BorrowerDepartment = "Marketing", CheckoutDate = DateTime.UtcNow.AddDays(-10), DueDate = DateTime.UtcNow.AddDays(4), ConditionAtCheckout = DataObjects.EquipmentCondition.Good },
                ] },
            new() { RecordId = Guid.NewGuid(), Name = "Dell Latitude 5540 #2", Category = DataObjects.EquipmentCategory.Laptop, AssetTag = "EQ-2024-0002", SerialNumber = "DL5540-A002", Location = "IT Office — Room 105", Condition = DataObjects.EquipmentCondition.Good, PurchaseDate = DateTime.UtcNow.AddDays(-365), PurchasePrice = 1150m },
            new() { RecordId = Guid.NewGuid(), Name = "Epson PowerLite 2250U", Category = DataObjects.EquipmentCategory.Projector, AssetTag = "EQ-2023-0010", SerialNumber = "EP2250U-B010", Location = "AV Closet — Library 2F", Condition = DataObjects.EquipmentCondition.Good, PurchaseDate = DateTime.UtcNow.AddDays(-500), PurchasePrice = 1299m,
                Checkouts = [
                    new() { CheckoutId = Guid.NewGuid(), BorrowerName = "Prof. Garcia", BorrowerEmail = "garcia@example.edu", BorrowerDepartment = "CS", CheckoutDate = DateTime.UtcNow.AddDays(-30), DueDate = DateTime.UtcNow.AddDays(-16), ReturnDate = DateTime.UtcNow.AddDays(-17), ConditionAtCheckout = DataObjects.EquipmentCondition.Good, ConditionAtReturn = DataObjects.EquipmentCondition.Good, Notes = "Used for guest lecture" },
                ] },
            new() { RecordId = Guid.NewGuid(), Name = "Canon EOS R6 Mark II", Category = DataObjects.EquipmentCategory.Camera, AssetTag = "EQ-2024-0015", SerialNumber = "CEOSR6-C015", Location = "Media Lab — Student Union", Condition = DataObjects.EquipmentCondition.New, PurchaseDate = DateTime.UtcNow.AddDays(-60), PurchasePrice = 2499m },
            new() { RecordId = Guid.NewGuid(), Name = "Blue Yeti Microphone", Category = DataObjects.EquipmentCategory.Microphone, AssetTag = "EQ-2023-0020", Location = "Podcast Studio — Library B1", Condition = DataObjects.EquipmentCondition.Fair, PurchaseDate = DateTime.UtcNow.AddDays(-600), PurchasePrice = 129m,
                Checkouts = [
                    new() { CheckoutId = Guid.NewGuid(), BorrowerName = "Lisa Admin", BorrowerEmail = "ladmin@example.edu", CheckoutDate = DateTime.UtcNow.AddDays(-5), DueDate = DateTime.UtcNow.AddDays(-1), ConditionAtCheckout = DataObjects.EquipmentCondition.Fair, Notes = "For accessibility committee recording — OVERDUE" },
                ] },
            new() { RecordId = Guid.NewGuid(), Name = "iPad Pro 12.9\"", Category = DataObjects.EquipmentCategory.Tablet, AssetTag = "EQ-2024-0025", SerialNumber = "IPADPRO-D025", Location = "IT Office — Room 105", Condition = DataObjects.EquipmentCondition.Good, PurchaseDate = DateTime.UtcNow.AddDays(-180), PurchasePrice = 1099m },
            new() { RecordId = Guid.NewGuid(), Name = "T-Mobile 5G Hotspot", Category = DataObjects.EquipmentCategory.Hotspot, AssetTag = "EQ-2024-0030", Location = "Front Desk — Library", Condition = DataObjects.EquipmentCondition.Good, PurchaseDate = DateTime.UtcNow.AddDays(-90), PurchasePrice = 199m },
            new() { RecordId = Guid.NewGuid(), Name = "USB-C Hub Adapter", Category = DataObjects.EquipmentCategory.Adapter, AssetTag = "EQ-2024-0035", Location = "IT Office — Room 105", Condition = DataObjects.EquipmentCondition.Good, PurchaseDate = DateTime.UtcNow.AddDays(-200), PurchasePrice = 45m },
            new() { RecordId = Guid.NewGuid(), Name = "Logitech Webcam C920", Category = DataObjects.EquipmentCategory.Camera, AssetTag = "EQ-2022-0040", Location = "IT Office — Room 105", Condition = DataObjects.EquipmentCondition.NeedsRepair, PurchaseDate = DateTime.UtcNow.AddDays(-900), PurchasePrice = 79m, Description = "USB connector loose — intermittent connection" },
            new() { RecordId = Guid.NewGuid(), Name = "Dell XPS 15 (Retired)", Category = DataObjects.EquipmentCategory.Laptop, AssetTag = "EQ-2020-0050", Location = "Storage — Room B05", Condition = DataObjects.EquipmentCondition.Retired, PurchaseDate = DateTime.UtcNow.AddDays(-1800), PurchasePrice = 1499m, Description = "Battery no longer holds charge. Decommissioned." },
        };
        SaveJsonRecords<DataObjects.Equipment>(items, null);
    }

    // ── Seed: Evaluations ──
    private void SeedEvaluations()
    {
        var templateId = Guid.Parse("d0000001-0000-0000-0000-000000000001");
        var questions = new List<DataObjects.EvalQuestion> {
            new() { QuestionId = Guid.Parse("e0000001-0000-0000-0000-000000000001"), QuestionText = "The instructor explained concepts clearly.", QuestionType = DataObjects.EvalQuestionType.Likert5, IsRequired = true, DisplayOrder = 1 },
            new() { QuestionId = Guid.Parse("e0000001-0000-0000-0000-000000000002"), QuestionText = "Course materials were helpful and well-organized.", QuestionType = DataObjects.EvalQuestionType.Likert5, IsRequired = true, DisplayOrder = 2 },
            new() { QuestionId = Guid.Parse("e0000001-0000-0000-0000-000000000003"), QuestionText = "Assignments were relevant to learning objectives.", QuestionType = DataObjects.EvalQuestionType.Likert5, IsRequired = true, DisplayOrder = 3 },
            new() { QuestionId = Guid.Parse("e0000001-0000-0000-0000-000000000004"), QuestionText = "Overall, how would you rate this course?", QuestionType = DataObjects.EvalQuestionType.Rating10, IsRequired = true, DisplayOrder = 4 },
            new() { QuestionId = Guid.Parse("e0000001-0000-0000-0000-000000000005"), QuestionText = "What did the instructor do well?", QuestionType = DataObjects.EvalQuestionType.FreeText, IsRequired = false, DisplayOrder = 5 },
            new() { QuestionId = Guid.Parse("e0000001-0000-0000-0000-000000000006"), QuestionText = "What could be improved?", QuestionType = DataObjects.EvalQuestionType.FreeText, IsRequired = false, DisplayOrder = 6 },
            new() { QuestionId = Guid.Parse("e0000001-0000-0000-0000-000000000007"), QuestionText = "Would you recommend this course to a friend?", QuestionType = DataObjects.EvalQuestionType.YesNo, IsRequired = true, DisplayOrder = 7 },
        };

        var rng = new Random(200);
        var likertValues = new[] { "1", "2", "3", "4", "5" };
        var freeTextGood = new[] { "Great examples", "Very organized lectures", "Always available for office hours", "Made complex topics simple", "Engaging classroom discussions" };
        var freeTextImprove = new[] { "More hands-on projects", "Slower pace on difficult topics", "Better textbook", "More practice exams", "Earlier feedback on assignments" };

        List<DataObjects.EvalResponse> MakeResponses(int count)
        {
            var responses = new List<DataObjects.EvalResponse>();
            for (int i = 0; i < count; i++) {
                responses.Add(new DataObjects.EvalResponse {
                    ResponseId = Guid.NewGuid(),
                    SubmittedDate = DateTime.UtcNow.AddDays(-rng.Next(1, 20)),
                    Answers = [
                        new() { QuestionId = questions[0].QuestionId, Value = likertValues[rng.Next(2, 5)] },
                        new() { QuestionId = questions[1].QuestionId, Value = likertValues[rng.Next(2, 5)] },
                        new() { QuestionId = questions[2].QuestionId, Value = likertValues[rng.Next(1, 5)] },
                        new() { QuestionId = questions[3].QuestionId, Value = rng.Next(6, 10).ToString() },
                        new() { QuestionId = questions[4].QuestionId, Value = freeTextGood[rng.Next(freeTextGood.Length)] },
                        new() { QuestionId = questions[5].QuestionId, Value = freeTextImprove[rng.Next(freeTextImprove.Length)] },
                        new() { QuestionId = questions[6].QuestionId, Value = rng.NextDouble() > 0.15 ? "Yes" : "No" },
                    ]
                });
            }
            return responses;
        }

        var evals = new List<DataObjects.Evaluation> {
            new() { RecordId = Guid.NewGuid(), Title = "Fall 2024 — CS 101 — Dr. Smith", CourseCode = "CS 101", CourseName = "Intro to Computer Science", InstructorName = "Dr. Smith", Term = "Fall 2024", Department = "Computer Science", OpenDate = DateTime.UtcNow.AddDays(-60), CloseDate = DateTime.UtcNow.AddDays(-30), IsAnonymous = true, EnrollmentCount = 35, TemplateId = templateId, Questions = questions, Responses = MakeResponses(28) },
            new() { RecordId = Guid.NewGuid(), Title = "Fall 2024 — MATH 201 — Prof. Garcia", CourseCode = "MATH 201", CourseName = "Calculus II", InstructorName = "Prof. Garcia", Term = "Fall 2024", Department = "Mathematics", OpenDate = DateTime.UtcNow.AddDays(-60), CloseDate = DateTime.UtcNow.AddDays(-30), IsAnonymous = true, EnrollmentCount = 28, TemplateId = templateId, Questions = questions, Responses = MakeResponses(22) },
            new() { RecordId = Guid.NewGuid(), Title = "Spring 2025 — CS 301 — Dr. Smith", CourseCode = "CS 301", CourseName = "Data Structures", InstructorName = "Dr. Smith", Term = "Spring 2025", Department = "Computer Science", OpenDate = DateTime.UtcNow.AddDays(-10), CloseDate = DateTime.UtcNow.AddDays(10), IsAnonymous = true, EnrollmentCount = 30, TemplateId = templateId, Questions = questions, Responses = MakeResponses(12) },
            new() { RecordId = Guid.NewGuid(), Title = "Spring 2025 — ENG 102 — Ms. Davis", CourseCode = "ENG 102", CourseName = "English Composition", InstructorName = "Ms. Davis", Term = "Spring 2025", Department = "English", OpenDate = DateTime.UtcNow.AddDays(-10), CloseDate = DateTime.UtcNow.AddDays(10), IsAnonymous = true, EnrollmentCount = 25, TemplateId = templateId, Questions = questions, Responses = MakeResponses(8) },
            new() { RecordId = Guid.NewGuid(), Title = "Spring 2025 — BIO 110 — Dr. Nguyen", CourseCode = "BIO 110", CourseName = "General Biology", InstructorName = "Dr. Nguyen", Term = "Spring 2025", Department = "Biology", OpenDate = DateTime.UtcNow.AddDays(5), CloseDate = DateTime.UtcNow.AddDays(25), IsAnonymous = true, EnrollmentCount = 40, TemplateId = templateId, Questions = questions },
        };
        SaveJsonRecords<DataObjects.Evaluation>(evals, null);
    }

    // ── Seed: Onboarding ──
    private void SeedOnboarding()
    {
        List<DataObjects.ChecklistItem> MakeChecklist(DateTime startDate) {
            return [
                new() { ChecklistItemId = Guid.NewGuid(), TaskName = "Complete I-9 form", Category = DataObjects.ChecklistCategory.HR, AssignedTo = "HR Office", IsRequired = true, DueDate = startDate.AddDays(-3), DisplayOrder = 1 },
                new() { ChecklistItemId = Guid.NewGuid(), TaskName = "Submit W-4 tax form", Category = DataObjects.ChecklistCategory.HR, AssignedTo = "HR Office", IsRequired = true, DueDate = startDate.AddDays(-3), DisplayOrder = 2 },
                new() { ChecklistItemId = Guid.NewGuid(), TaskName = "Set up direct deposit", Category = DataObjects.ChecklistCategory.HR, AssignedTo = "HR Office", IsRequired = true, DueDate = startDate.AddDays(-1), DisplayOrder = 3 },
                new() { ChecklistItemId = Guid.NewGuid(), TaskName = "Emergency contact form", Category = DataObjects.ChecklistCategory.HR, AssignedTo = "HR Office", IsRequired = true, DueDate = startDate, DisplayOrder = 4 },
                new() { ChecklistItemId = Guid.NewGuid(), TaskName = "Provision laptop", Category = DataObjects.ChecklistCategory.IT, AssignedTo = "IT Helpdesk", IsRequired = true, DueDate = startDate.AddDays(-1), DisplayOrder = 5 },
                new() { ChecklistItemId = Guid.NewGuid(), TaskName = "Create email account", Category = DataObjects.ChecklistCategory.IT, AssignedTo = "IT Helpdesk", IsRequired = true, DueDate = startDate.AddDays(-2), DisplayOrder = 6 },
                new() { ChecklistItemId = Guid.NewGuid(), TaskName = "Set up VPN access", Category = DataObjects.ChecklistCategory.IT, AssignedTo = "IT Helpdesk", IsRequired = false, DueDate = startDate.AddDays(3), DisplayOrder = 7 },
                new() { ChecklistItemId = Guid.NewGuid(), TaskName = "Assign office/desk", Category = DataObjects.ChecklistCategory.Facilities, AssignedTo = "Facilities Manager", IsRequired = true, DueDate = startDate.AddDays(-1), DisplayOrder = 8 },
                new() { ChecklistItemId = Guid.NewGuid(), TaskName = "Issue building access card", Category = DataObjects.ChecklistCategory.Facilities, AssignedTo = "Facilities Manager", IsRequired = true, DueDate = startDate, DisplayOrder = 9 },
                new() { ChecklistItemId = Guid.NewGuid(), TaskName = "Parking permit", Category = DataObjects.ChecklistCategory.Facilities, AssignedTo = "Facilities Manager", IsRequired = false, DueDate = startDate.AddDays(5), DisplayOrder = 10 },
                new() { ChecklistItemId = Guid.NewGuid(), TaskName = "Department orientation", Category = DataObjects.ChecklistCategory.Department, AssignedTo = "Supervisor", IsRequired = true, DueDate = startDate, DisplayOrder = 11 },
                new() { ChecklistItemId = Guid.NewGuid(), TaskName = "Meet the team lunch", Category = DataObjects.ChecklistCategory.Department, AssignedTo = "Supervisor", IsRequired = false, DueDate = startDate.AddDays(2), DisplayOrder = 12 },
                new() { ChecklistItemId = Guid.NewGuid(), TaskName = "Safety training video", Category = DataObjects.ChecklistCategory.Training, AssignedTo = "Training Portal", IsRequired = true, DueDate = startDate.AddDays(5), DisplayOrder = 13 },
                new() { ChecklistItemId = Guid.NewGuid(), TaskName = "FERPA compliance training", Category = DataObjects.ChecklistCategory.Compliance, AssignedTo = "Training Portal", IsRequired = true, DueDate = startDate.AddDays(10), DisplayOrder = 14 },
                new() { ChecklistItemId = Guid.NewGuid(), TaskName = "Cybersecurity awareness training", Category = DataObjects.ChecklistCategory.Compliance, AssignedTo = "Training Portal", IsRequired = true, DueDate = startDate.AddDays(10), DisplayOrder = 15 },
            ];
        }

        void MarkComplete(List<DataObjects.ChecklistItem> items, int count) {
            for (int i = 0; i < Math.Min(count, items.Count); i++) {
                items[i].IsCompleted = true;
                items[i].CompletedDate = DateTime.UtcNow.AddDays(-new Random(i).Next(1, 10));
                items[i].CompletedBy = items[i].AssignedTo;
            }
        }

        var cl1 = MakeChecklist(DateTime.UtcNow.AddDays(-14)); MarkComplete(cl1, 15);
        var cl2 = MakeChecklist(DateTime.UtcNow.AddDays(-7)); MarkComplete(cl2, 10);
        var cl3 = MakeChecklist(DateTime.UtcNow.AddDays(-3)); MarkComplete(cl3, 5);
        var cl4 = MakeChecklist(DateTime.UtcNow.AddDays(3));
        var cl5 = MakeChecklist(DateTime.UtcNow.AddDays(-10)); MarkComplete(cl5, 12);
        var cl6 = MakeChecklist(DateTime.UtcNow.AddDays(7));

        var onboardings = new List<DataObjects.Onboarding> {
            new() { RecordId = Guid.NewGuid(), EmployeeName = "James Wilson", EmployeeEmail = "jwilson@example.edu", EmployeeTitle = "Software Developer", Department = "Engineering", HireDate = DateTime.UtcNow.AddDays(-21), StartDate = DateTime.UtcNow.AddDays(-14), SupervisorName = "Alice Chen", MentorName = "Carol Davis", EmploymentType = DataObjects.EmploymentType.FullTime, Status = DataObjects.OnboardingStatus.Completed, ChecklistItems = cl1 },
            new() { RecordId = Guid.NewGuid(), EmployeeName = "Maria Santos", EmployeeEmail = "msantos@example.edu", EmployeeTitle = "HR Coordinator", Department = "Human Resources", HireDate = DateTime.UtcNow.AddDays(-14), StartDate = DateTime.UtcNow.AddDays(-7), SupervisorName = "Diana Lopez", EmploymentType = DataObjects.EmploymentType.FullTime, Status = DataObjects.OnboardingStatus.InProgress, ChecklistItems = cl2 },
            new() { RecordId = Guid.NewGuid(), EmployeeName = "Kevin Park", EmployeeEmail = "kpark@example.edu", EmployeeTitle = "Research Assistant", Department = "Biology", HireDate = DateTime.UtcNow.AddDays(-10), StartDate = DateTime.UtcNow.AddDays(-3), SupervisorName = "Dr. Nguyen", EmploymentType = DataObjects.EmploymentType.GradAssistant, Status = DataObjects.OnboardingStatus.InProgress, ChecklistItems = cl3 },
            new() { RecordId = Guid.NewGuid(), EmployeeName = "Emily Turner", EmployeeEmail = "eturner@example.edu", EmployeeTitle = "Academic Advisor", Department = "Student Services", HireDate = DateTime.UtcNow.AddDays(-3), StartDate = DateTime.UtcNow.AddDays(3), SupervisorName = "Grace Kim", MentorName = "Fran Nguyen", EmploymentType = DataObjects.EmploymentType.FullTime, Status = DataObjects.OnboardingStatus.Pending, ChecklistItems = cl4 },
            new() { RecordId = Guid.NewGuid(), EmployeeName = "Tyler Brooks", EmployeeEmail = "tbrooks@example.edu", EmployeeTitle = "Lab Technician", Department = "Engineering", HireDate = DateTime.UtcNow.AddDays(-17), StartDate = DateTime.UtcNow.AddDays(-10), SupervisorName = "Alice Chen", EmploymentType = DataObjects.EmploymentType.PartTime, Status = DataObjects.OnboardingStatus.InProgress, ChecklistItems = cl5 },
            new() { RecordId = Guid.NewGuid(), EmployeeName = "Aisha Rahman", EmployeeEmail = "arahman@example.edu", EmployeeTitle = "Student Worker", Department = "Library", HireDate = DateTime.UtcNow, StartDate = DateTime.UtcNow.AddDays(7), SupervisorName = "Lisa Admin", EmploymentType = DataObjects.EmploymentType.StudentWorker, Status = DataObjects.OnboardingStatus.Pending, ChecklistItems = cl6 },
        };
        SaveJsonRecords<DataObjects.Onboarding>(onboardings, null);
    }
}
