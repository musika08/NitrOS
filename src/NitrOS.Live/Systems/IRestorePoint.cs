namespace NitrOS.Live.Systems;

public enum RestorePointOutcome { Created, SkippedRecent, Unavailable }

public interface IRestorePoint { RestorePointOutcome Create(string description); }
