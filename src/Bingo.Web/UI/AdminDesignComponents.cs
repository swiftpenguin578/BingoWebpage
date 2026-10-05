namespace Bingo.Web.UI;

// Small view models for reference components shared by Razor and client templates.
public sealed record AdminDesignIcon(string Name, string Class = "ic");
public sealed record AdminDesignFieldLabel(string Field, string For, string Text, string? Optional = null, bool Counter = true);
public sealed record AdminDesignFieldNote(string Text = "", string? Field = null, bool Hidden = false, bool UseCurrent = false, string? ActionLabel = null);
public sealed record AdminDesignFieldError(string Field, string Text = "");
public sealed record AdminDesignFieldLock(string Text);
public sealed record AdminDesignBanner(string Tone, string Icon, string Text = "", string? Lead = null, string? Id = null, bool Hidden = false);
public sealed record AdminDesignSaveBar(string Save, string NoChanges, string Unsaved, string? Saved = null);
public sealed record AdminDesignToast(string Text = "", bool Error = false);
public sealed record AdminDesignMenuHeader(string Text, string? Hint = null);
