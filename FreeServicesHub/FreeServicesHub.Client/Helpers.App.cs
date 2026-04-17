using System.Net.NetworkInformation;

namespace FreeServicesHub.Client;

public static partial class Helpers
{
    public static Dictionary<string, List<string>> AppIcons {
        get {
            Dictionary<string, List<string>> icons = new Dictionary<string, List<string>> {
                { "fa:fa-solid fa-home", new List<string> { "IconName1", "IconName2" }},
                { "fa:fa-solid fa-server", new List<string> { "AgentDashboard", "Agents" }},
                { "fa:fa-solid fa-heartbeat", new List<string> { "AgentHeartbeat" }},
                { "fa:fa-solid fa-key", new List<string> { "AgentApiKey", "RegistrationKey" }},
                { "fa:fa-solid fa-gears", new List<string> { "BackgroundServices" }},
                { "fa:fa-solid fa-sliders", new List<string> { "AgentSettings" }},
                { "fa:fa-solid fa-users-cog", new List<string> { "AgentManagement" }},
            };

            return icons;
        }
    }

    public static bool AppMethod()
    {
        return true;
    }

    // {{ModuleItemStart:Tags}}
    public static List<DataObjects.Tag> AvailableTagListApp(DataObjects.TagModule? Module, List<Guid> ExcludeTags)
    {
        var output = new List<DataObjects.Tag>();

        if (Module != null) {
            switch (Module) {
                //case DataObjects.TagModule.AppTagType:
                //    output = Model.Tags.Where(x => !ExcludeTags.Contains(x.TagId) && x.UseInAppTagType == true)
                //        .OrderBy(x => x.Name)
                //        .ToList();
                //    break;

                default:
                    break;
            }
        }

        return output;
    }
    // {{ModuleItemEnd:Tags}}

    private static List<string> GetDeletedRecordTypesApp()
    {
        var output = new List<string>();

        // Add any app-specific deleted record types here.

        return output;
    }

    /// <summary>
    /// Gets the deleted records for a specific app type.
    /// </summary>
    /// <param name="deletedRecords">The DeletedRecords object.</param>
    /// <param name="type">The item type.</param>
    /// <returns>A nullable list of DeletedRecordItem objects.</returns>
    public static List<DataObjects.DeletedRecordItem>? GetDeletedRecordsForAppType(DataObjects.DeletedRecords deletedRecords, string type)
    {
        List<DataObjects.DeletedRecordItem>? output = null;

        switch (StringLower(type)) {
            //case "this":
            //    output = deletedRecords.That;
            //    break;

            default:
                break;
        }

        return output;
    }

    /// <summary>
    /// Gets the language tag for deleted records based on the app type.
    /// </summary>
    /// <param name="type">The item type.</param>
    /// <returns>The language tag for the item type.</returns>
    public static string GetDeletedRecordsLanguageTagForAppType(string type)
    {
        string output = String.Empty;

        switch (StringLower(type)) {
            //case "this":
            //    output = "That";
            //    break;

            default:
                break;
        }

        return output;
    }

    public static List<DataObjects.MenuItem> MenuItemsApp {
        get {
            // Add any app-specific top-level menu items here.
            var output = new List<DataObjects.MenuItem>();

            output.Add(new DataObjects.MenuItem {
                Title = "Agent Dashboard",
                Icon = "AgentDashboard",
                PageNames = new List<string> { "agentdashboard" },
                SortOrder = 100,
                url = Helpers.BuildUrl("AgentDashboard"),
                AppAdminOnly = false,
            });

            output.Add(new DataObjects.MenuItem {
                Title = "Background Services",
                Icon = "BackgroundServices",
                PageNames = new List<string> { "backgroundservices" },
                SortOrder = 110,
                url = Helpers.BuildUrl("BackgroundServices"),
                AppAdminOnly = false,
            });

            output.Add(new DataObjects.MenuItem {
                Title = "Agent Settings",
                Icon = "AgentSettings",
                PageNames = new List<string> { "agentsettings" },
                SortOrder = 120,
                url = Helpers.BuildUrl("AgentSettings"),
                AppAdminOnly = false,
            });

            return output;
        }
    }

    public static List<DataObjects.MenuItem> MenuItemsAdminApp {
        get {
            // Add any app-specific admin menu items here.
            var output = new List<DataObjects.MenuItem>();

            output.Add(new DataObjects.MenuItem {
                Title = "Agent Management",
                Icon = "AgentManagement",
                PageNames = new List<string> { "agentmanagement" },
                SortOrder = 10,
                url = Helpers.BuildUrl("AgentManagement"),
                AppAdminOnly = true,
            });

            return output;
        }
    }

    public static async Task ProcessSignalRUpdateApp(DataObjects.SignalRUpdate update)
    {
        // Process any SignalR updates specific to your app here. See the main ProcessSignalRUpdate method for an example in the MainLayout.razor page.

        if (update != null && (update.TenantId == null || update.TenantId == Model.TenantId)) {
            var itemId = update.ItemId;
            string message = update.Message.ToLower();
            var userId = update.UserId;

            switch (update.UpdateType) {
                case DataObjects.SignalRUpdateType.AgentHeartbeat:
                    // Full agent list refresh from monitor service heartbeat
                    if (update.Object is List<DataObjects.Agent> allAgents) {
                        Model.AgentStatuses = allAgents;
                    }
                    break;

                case DataObjects.SignalRUpdateType.AgentConnected:
                case DataObjects.SignalRUpdateType.AgentDisconnected:
                case DataObjects.SignalRUpdateType.AgentStatusChanged:
                    // Partial update — merge changed agents into the current list
                    if (update.Object is List<DataObjects.Agent> changedAgents) {
                        List<DataObjects.Agent> current = new(Model.AgentStatuses);
                        foreach (DataObjects.Agent changed in changedAgents) {
                            int idx = current.FindIndex(a => a.AgentId == changed.AgentId);
                            if (idx >= 0) {
                                current[idx] = changed;
                            } else {
                                current.Add(changed);
                            }
                        }
                        Model.AgentStatuses = current;
                    }
                    break;

                case DataObjects.SignalRUpdateType.BackgroundServiceLog:
                    // Handled by the BackgroundServices page's own SignalRUpdate handler.
                    break;

                case DataObjects.SignalRUpdateType.AgentSettingsReport:
                case DataObjects.SignalRUpdateType.AgentSettingsUpdated:
                    // Handled by the AgentSettings page's own SignalRUpdate handler.
                    break;

                case DataObjects.SignalRUpdateType.JobUpdated:
                case DataObjects.SignalRUpdateType.JobCompleted:
                    // Handled by the AgentDashboard page's own SignalRUpdate handler.
                    break;

                default:
                    // Since this is called only from the default method in the main handler here,
                    // we can assume that the update type is not recognized by this app.
                    await Helpers.ConsoleLog("Unknown SignalR Update Type Received");
                    break;
            }
        }
    }

    public static async Task ProcessSignalRUpdateAppUndelete(DataObjects.SignalRUpdate update)
    {
        await Task.Delay(0); // Simulate a delay since this method has to be async. This can be removed once you implement your await logic.

        switch (Helpers.StringLower(update.Message)) {
            case "this":
                // Add code to reload your app-specific data based on the undelete type.
                break;
        }
    }

    private async static Task ReloadModelApp(DataObjects.BlazorDataModelLoader? blazorDataModelLoader)
    {
        await Task.CompletedTask;

        // Load agent summary data from the initial model hydration
        if (blazorDataModelLoader != null) {
            Model.AgentStatuses = blazorDataModelLoader.AgentStatuses ?? new List<DataObjects.Agent>();
        }
    }

    private static void UpdateModelDeletedRecordCountsForAppItems(DataObjects.DeletedRecords deletedRecords)
    {
        // Model.DeletedRecordCounts.MyValue = deletedRecords.MyValue.Count();
    }

}
