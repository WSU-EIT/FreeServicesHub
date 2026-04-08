namespace FreeCICD.Client;

public partial class BlazorDataModel
{
    private List<string> _MyValues = new List<string>();

    private bool HaveDeletedRecordsApp {
        get {
            bool output = false;

            // Check your app-specific deleted records here.
            //if (DeletedRecordCounts.MyValue > 0 ) {
            //    output = true;
            //}

            return output;
        }
    }

    public bool MyCustomDataModelMethod()
    {
        return true;
    }

    /// <summary>
    /// An example of implementing a custom property in your data model.
    /// </summary>
    public List<string> MyValues {
        get {
            return _MyValues;
        }

        set {
            if (!ObjectsAreEqual(_MyValues, value)) {
                _MyValues = value;
                _ModelUpdated = DateTime.UtcNow;
                NotifyDataChanged();
            }
        }
    }

    private DataObjects.SignalrClientRegistration _SignalrClientRegistration = new();

    /// <summary>
    /// Gets or sets the SignalR client registration for real-time updates.
    /// </summary>
    public DataObjects.SignalrClientRegistration SignalrClientRegistration {
        get { return _SignalrClientRegistration == null ? new() : _SignalrClientRegistration; }
        set {
            if (!ObjectsAreEqual(_SignalrClientRegistration?.RegistrationId, value?.RegistrationId)
                ||
                !ObjectsAreEqual(_SignalrClientRegistration?.ConnectionId, value?.ConnectionId)
                ) {
                _SignalrClientRegistration = new DataObjects.SignalrClientRegistration {
                    RegistrationId = (string.Empty + value?.RegistrationId).Trim(),
                    ConnectionId = (string.Empty + value?.ConnectionId).Trim(),
                };
                _ModelUpdated = DateTime.UtcNow;
                NotifyDataChanged();
            }
        }
    }
}
