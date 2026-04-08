using System.Net.NetworkInformation;

namespace FreeCICD.Client;

public static partial class Helpers
{
    public static Dictionary<string, List<string>> AppIcons {
        get {
            Dictionary<string, List<string>> icons = new Dictionary<string, List<string>> {
                { "fa:fa-solid fa-home", new List<string> { "IconName1", "IconName2" }},
                { "fa:fa-rocket", new List<string> { "Pipelines", "PipelineDashboard" }},
                { "fa:fa-magic", new List<string> { "PipelineWizard", "Wizard" }},
                { "fa:fa-plug", new List<string> { "Plug", "SignalR" }},
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
        }

        return output;
    }

    public static List<DataObjects.MenuItem> MenuItemsApp {
        get {
            // Add any app-specific top-level menu items here.
            var output = new List<DataObjects.MenuItem>();

            // Pipeline Dashboard - Now the home page (SortOrder: 100 = leftmost)
            output.Add(new DataObjects.MenuItem {
                Title = "Pipelines",
                Icon = "Pipelines",
                PageNames = new List<string> { "pipelines" },
                SortOrder = 100,
                url = Helpers.BuildUrl(""),
                AppAdminOnly = false,
            });

            // Pipeline Wizard - Create/Edit pipeline (SortOrder: 200)
            output.Add(new DataObjects.MenuItem {
                Title = "Pipeline Wizard",
                Icon = "PipelineWizard",
                PageNames = new List<string> { "home" },
                SortOrder = 200,
                url = Helpers.BuildUrl("Wizard"),
                AppAdminOnly = false,
            });

            return output;
        }
    }

    public static List<DataObjects.MenuItem> MenuItemsAdminApp {
        get {
            // Add any app-specific admin menu items here.
            var output = new List<DataObjects.MenuItem>();

            // SignalR Connections viewer (App Admin only)
            output.Add(new DataObjects.MenuItem {
                Title = "SignalR Connections",
                Icon = "Plug",
                PageNames = new List<string> { "signalr-connections" },
                SortOrder = 9000,
                url = Helpers.BuildUrl("Admin/SignalRConnections"),
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
                case DataObjects.SignalRUpdateType.AdminAlert:
                    // Admin sent an alert message - show it as a toast
                    var alertMessage = update.Message;
                    var senderName = update.UserDisplayName ?? "Admin";
                    
                    // Parse message type from ObjectAsString
                    var messageType = MessageType.Info;
                    if (!string.IsNullOrWhiteSpace(update.ObjectAsString)) {
                        messageType = update.ObjectAsString.ToLower() switch {
                            "primary" => MessageType.Primary,
                            "secondary" => MessageType.Secondary,
                            "success" => MessageType.Success,
                            "danger" => MessageType.Danger,
                            "warning" => MessageType.Warning,
                            "info" => MessageType.Info,
                            "light" => MessageType.Light,
                            "dark" => MessageType.Dark,
                            _ => MessageType.Info
                        };
                    }
                    
                    // Determine auto-hide (default true)
                    bool autoHide = true;
                    if (update.Object != null) {
                        try {
                            var objJson = System.Text.Json.JsonSerializer.Serialize(update.Object);
                            var obj = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object>>(objJson);
                            if (obj != null && obj.TryGetValue("AutoHide", out var autoHideVal)) {
                                if (autoHideVal is System.Text.Json.JsonElement jsonEl) {
                                    autoHide = jsonEl.GetBoolean();
                                } else if (autoHideVal is bool boolVal) {
                                    autoHide = boolVal;
                                }
                            }
                        } catch { }
                    }
                    
                    // Show the message with sender info
                    var displayMessage = $"<strong>📢 {senderName}:</strong> {alertMessage}";
                    Model.AddMessage(displayMessage, messageType, autoHide);
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
        // Called from the main ReloadModel method in Helpers to load app-specific data.
    }

    private static void UpdateModelDeletedRecordCountsForAppItems(DataObjects.DeletedRecords deletedRecords)
    {
        // Model.DeletedRecordCounts.MyValue = deletedRecords.MyValue.Count();
    }

}
