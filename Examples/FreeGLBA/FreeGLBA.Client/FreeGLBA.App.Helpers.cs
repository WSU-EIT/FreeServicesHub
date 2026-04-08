using System.Net.NetworkInformation;

namespace FreeGLBA.Client;

public static partial class Helpers
{
    
    public static List<DataObjects.MenuItem> FreeGLBAMenuItemsApp {
        get {
            // Add any app-specific top-level menu items here.
            var output = new List<DataObjects.MenuItem>();

            // GLBA Dashboard - visible to all logged in users
            output.Add(new DataObjects.MenuItem {
                Title = "GLBA Dashboard",
                Icon = "fa-solid fa-chart-line",
                PageNames = new List<string> { "glbadashboard" },
                SortOrder = 100,
                url = Helpers.BuildUrl("GlbaDashboard"),
                AppAdminOnly = false,
            });

            // Source Systems - Admin only
            if (Model.User.Admin) {
                output.Add(new DataObjects.MenuItem {
                    Title = "Source Systems",
                    Icon = "fa-solid fa-server",
                    PageNames = new List<string> { "sourcesystems" },
                    SortOrder = 200,
                    url = Helpers.BuildUrl("SourceSystems"),
                    AppAdminOnly = false,
                });
            }

            // Access Events - visible to all logged in users
            output.Add(new DataObjects.MenuItem {
                Title = "Access Events",
                Icon = "fa-solid fa-list-check",
                PageNames = new List<string> { "accessevents" },
                SortOrder = 300,
                url = Helpers.BuildUrl("AccessEvents"),
                AppAdminOnly = false,
            });

            // Accessors (Users who accessed data) - visible to all logged in users
            output.Add(new DataObjects.MenuItem {
                Title = "Accessors",
                Icon = "fa-solid fa-user-shield",
                PageNames = new List<string> { "accessors" },
                SortOrder = 350,
                url = Helpers.BuildUrl("Accessors"),
                AppAdminOnly = false,
            });

            // Data Subjects - visible to all logged in users
            output.Add(new DataObjects.MenuItem {
                Title = "Data Subjects",
                Icon = "fa-solid fa-users",
                PageNames = new List<string> { "datasubjects" },
                SortOrder = 400,
                url = Helpers.BuildUrl("DataSubjects"),
                AppAdminOnly = false,
            });

            // Compliance Reports - Admin only
            if (Model.User.Admin) {
                output.Add(new DataObjects.MenuItem {
                    Title = "Compliance Reports",
                    Icon = "fa-solid fa-file-contract",
                    PageNames = new List<string> { "compliancereports" },
                    SortOrder = 500,
                    url = Helpers.BuildUrl("ComplianceReports"),
                    AppAdminOnly = false,
                });
            }

            return output;
        }
    }
}
