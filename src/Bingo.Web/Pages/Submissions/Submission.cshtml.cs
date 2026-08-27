using Bingo.Application.Evidence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Bingo.Web.Pages.Submissions;

[Authorize]
[RequestSizeLimit(11 * 1024 * 1024)]
public sealed class SubmissionModel(
    Bingo.Infrastructure.Persistence.ApplicationDbContext db,
    ISubmissionService service,
    IEvidenceAuthority evidenceAuthority,
    TimeProvider time,
    Microsoft.Extensions.Localization.IStringLocalizer<SharedResource> text,
    ILogger<Bingo.Web.Pages.Captain.SubmissionModel> logger) : Bingo.Web.Pages.Captain.SubmissionModel(db, service, evidenceAuthority, time, text, logger)
{
    protected override string DetailPagePath => "/Submissions/Submission";
    protected override string LedgerPagePath => "/Submissions/Index";
    protected override bool OwnerOnlyMutations => true;
    protected override bool CanOpenLedger(EvidenceActorScope scope) => scope.Kind is EvidenceActorKind.Participant or EvidenceActorKind.Captain;
    protected override bool CanReadSubmission(EvidenceActorScope scope, Bingo.Domain.Evidence.Submission submission) => scope.Kind is EvidenceActorKind.Participant or EvidenceActorKind.Captain;
}
