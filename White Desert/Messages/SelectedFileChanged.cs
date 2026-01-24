using White_Desert.Models;
using White_Desert.Models.GhostBridge;

namespace White_Desert.Messages;

public record SelectedFileChanged(PazFile? File,BdoNode Node ,byte[]? Content, string TempFilePath);