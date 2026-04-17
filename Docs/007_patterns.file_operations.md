# FreeCRM: File Upload & Download Patterns

> Upload files via MudBlazor drag-drop, download files to browser via JS interop, CSV export/import.

**Source:** nForm (UploadFile component), Helpdesk4 (CSV export), FreeCRM-main (DownloadFileToBrowser)

> **API Convention:** File data should always come from API endpoints, never generated in the browser.
> For CRUD operations, follow the three-endpoint pattern per [007_patterns.crud_api.md](007_patterns.crud_api.md).

---

## Pattern 1: File Upload with MudBlazor Drag-Drop

### Component (from nForm)

```razor
@typeparam UploadType

<div class="@(_uploading ? "hidden" : "")">
    <MudBlazor.MudStack Style="width: 100%;">
        <MudBlazor.MudFileUpload T="UploadType"
            OnFilesChanged="OnInputFileChanged"
            Accept="@SupportedFileTypesList"
            AppendMultipleFiles="true"
            MaximumFileCount="10"
            Class="flex-1"
            InputClass="absolute mud-width-full mud-height-full overflow-hidden z-20"
            InputStyle="opacity:0"
            @ondragenter="@SetDragClass"
            @ondragleave="@ClearDragClass"
            @ondragend="@ClearDragClass">
            <ActivatorContent>
                <MudBlazor.MudPaper Height="200px" Outlined="true" Class="@_dragClass">
                    <div class="drag-and-drop-instructions">
                        Drag files here or click to browse
                    </div>
                    @if (!string.IsNullOrWhiteSpace(SupportedFileTypesList)) {
                        <div class="drag-and-drop-instructions-file-types">
                            Supported: @SupportedFileTypesList.ToUpper()
                        </div>
                    }
                </MudBlazor.MudPaper>
            </ActivatorContent>
        </MudBlazor.MudFileUpload>
    </MudBlazor.MudStack>
</div>

@if (_uploading) {
    <LoadingMessage />
}

@code {
    [Parameter] public Delegate? OnUploadComplete { get; set; }
    [Parameter] public List<string>? SupportedFileTypes { get; set; }

    private static string DefaultDragClass = "drag-and-drop-upload";
    private string _dragClass = DefaultDragClass;
    private bool _uploading = false;

    private string SupportedFileTypesList =>
        SupportedFileTypes != null
            ? string.Join(", ", SupportedFileTypes.Select(x => x.StartsWith(".") ? x : "." + x))
            : "";

    private void SetDragClass() => _dragClass = DefaultDragClass + " mud-border-primary";
    private void ClearDragClass() => _dragClass = DefaultDragClass;

    private async Task OnInputFileChanged(InputFileChangeEventArgs e)
    {
        ClearDragClass();
        _uploading = true;

        var files = new List<DataObjects.FileStorage>();

        foreach (var file in e.GetMultipleFiles()) {
            var byteData = new byte[file.Size];
            await file.OpenReadStream(52_428_800).ReadAsync(byteData);  // 50MB max

            files.Add(new DataObjects.FileStorage {
                Bytes = file.Size,
                FileName = file.Name,
                Extension = Path.GetExtension(file.Name),
                TenantId = Model.TenantId,
                Value = byteData,
            });
        }

        _uploading = false;
        OnUploadComplete?.DynamicInvoke(files);
    }
}
```

### Usage

```razor
<UploadFile UploadType="IBrowserFile"
    SupportedFileTypes="@(new List<string> { ".csv", ".xlsx" })"
    OnUploadComplete="@((List<DataObjects.FileStorage> files) => ProcessUploads(files))" />
```

---

## Pattern 2: Download File to Browser

### JS Interop Function (in site.js)

```javascript
async function DownloadFileFromStream(fileName, contentStreamReference) {
    const arrayBuffer = await contentStreamReference.arrayBuffer();
    const blob = new Blob([arrayBuffer]);
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = fileName ?? "";
    a.click();
    URL.revokeObjectURL(url);
}
```

### C# Helper (in Helpers.cs)

```csharp
public static async Task DownloadFileToBrowser(string fileName, byte[]? fileData)
{
    if (fileData != null && fileData.Length > 0) {
        using var stream = new MemoryStream(fileData);
        using var streamRef = new DotNetStreamReference(stream: stream);
        await jsRuntime.InvokeVoidAsync("DownloadFileFromStream", fileName, streamRef);
    }
}
```

### Usage

```csharp
// Download a string as CSV
await Helpers.DownloadFileToBrowser(
    "report_" + Helpers.SafeFileDate() + ".csv",
    Encoding.UTF8.GetBytes(csvContent));

// Download a file from API
var file = await Helpers.GetOrPost<DataObjects.FileStorage>("api/Data/GetFile/" + fileId);
await Helpers.DownloadFileToBrowser(file.FileName, file.Value);
```

---

## Pattern 3: CSV Export with CsvHelper

### Serialize to CSV

```csharp
public static byte[]? GetCsvData<T>(IEnumerable<T> records)
{
    using var ms = new MemoryStream();
    using var sw = new StreamWriter(ms);
    using var csv = new CsvHelper.CsvWriter(sw, new CultureInfo("en-US"));
    csv.WriteRecords(records);
    sw.Flush();
    return ms.ToArray();
}
```

### Deserialize from CSV

```csharp
public static List<T> GetCsvFromData<T>(string csvData)
{
    var config = new CsvHelper.Configuration.CsvConfiguration(new CultureInfo("en-US")) {
        IgnoreBlankLines = true,
        MissingFieldFound = null,
    };

    using var reader = new StringReader(csvData);
    using var csv = new CsvHelper.CsvReader(reader, config);
    return csv.GetRecords<T>().ToList();
}
```

### Complete Export Flow

```csharp
protected async Task Export()
{
    // Clone filter without records (don't send data back to server)
    var postFilter = Helpers.DuplicateObject<DataObjects.FilterItems>(Filter);
    postFilter.Records = null;

    Model.ClearMessages();
    Model.Message_Processing();

    var results = await Helpers.GetOrPost<DataObjects.FilterItems>(
        "api/Data/GetItemsExport", postFilter);

    Model.ClearMessages();

    if (results != null && Helpers.ActionResponse(results.ActionResponse).Result) {
        await Helpers.DownloadFileToBrowser(
            "Export_" + Helpers.SafeFileDate() + ".csv",
            Encoding.UTF8.GetBytes(Helpers.StringValue(results.Export)));
    } else {
        Model.UnknownError();
    }
}
```

---

*Category: 007_patterns*
*Source: nForm, Helpdesk4, FreeCRM-main*
