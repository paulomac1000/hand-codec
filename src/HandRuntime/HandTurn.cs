namespace HandRuntime;

/// <summary>One chat turn (role + content). Role is "system", "user" or "assistant".</summary>
public sealed record HandTurn(string Role, string Content);
