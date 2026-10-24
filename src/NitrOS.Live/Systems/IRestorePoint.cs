namespace NitrOS.Live.Systems;

/// <summary>CreatedUnknown: Windows accepted the request but the new point could not be confirmed (its list could not be read).</summary>
public enum RestorePointOutcome { Created, SkippedRecent, Unavailable, CreatedUnknown }

public interface IRestorePoint { RestorePointOutcome Create(string description); }
