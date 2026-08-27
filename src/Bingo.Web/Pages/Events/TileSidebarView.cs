using Bingo.Application.Boards;

namespace Bingo.Web.Pages.Events;

public sealed record TileSidebarView(PublicTileDetails Tile, bool CanSubmit, Guid EventId, Guid TeamId, int SequenceNumber);
