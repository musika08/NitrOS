namespace NitrOS.Core.Model;

public enum Risk { Safe, Caution, Breaks }

public enum Mode { Live, Image }

public enum CardId { Recommended, Gamer, Streamer, MaxLean, Custom }

/// <summary>Pending means "decided by measurement"; treated as Off until measured.</summary>
public enum CardDefault { Off, On, Pending }

public enum DeviceTrait { Battery, Touchscreen, Handheld }

public enum ServiceStartType { Boot, System, Automatic, AutomaticDelayed, Manual, Disabled }

public enum RegistryKind { Dword, Qword, String, ExpandString, MultiString, Binary }
