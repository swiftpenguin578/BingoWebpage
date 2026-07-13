namespace Bingo.Web.Pages.Admin.Events;

public sealed record BoardRequirementEditorViewModel(int Index, IReadOnlyList<BoardModel.BossView> Bosses, IReadOnlyList<BoardModel.DropView> Drops);
