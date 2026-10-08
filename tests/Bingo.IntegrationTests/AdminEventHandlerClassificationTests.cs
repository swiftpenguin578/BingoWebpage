using System.Reflection;
using System.Text.RegularExpressions;
using Bingo.Infrastructure.Persistence;
using Bingo.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Bingo.Domain.Events;
using Bingo.Web.Pages.Admin.Events;
using Bingo.Web.Security;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Bingo.IntegrationTests;

public sealed class AdminEventHandlerClassificationTests
{
    // Baseline 89d9382 route contract. Explicitly includes [NonAction] methods:
    // Razor Pages excludes [NonHandler], not MVC's [NonAction]. Existing HTTP
    // PrepareDestructiveConfirmation proof exercises this distinction.
    // U3 D17 enables Schedule/SignupSetup terminal reads; C4 moves existing operations.
    // U7 D17 (brief 88) enables Board terminal reads (page, EditorData, Readback); POST gates unchanged.
    // U7-Q3: the retired BoardPreview route redirects to the Board, also on terminal events.
    // D16 mutation gates and test14 remain unchanged.
    // U4 (brief 85 planner default "Transport", 42c §1.5 item 14): Manage adds the
    // authorized no-store GET:Current read; no mutation gate changes.
    private const string Expected = """
Index|false|false|Read=GET:
Create|false|false|Read=GET:,GET:CheckAgain;Service=POST:
Manage|true|true|Read=GET:,GET:Current;Signup=POST:State,POST:OpenSignup,POST:CloseSignup,POST:ReopenSignup,POST:Capacity,POST:SignupWindow,POST:ConfirmSignup,POST:RestoreHidden,POST:PrepareSignupConfirmation,POST:PrepareStartConfirmation,POST:PrepareEndConfirmation,POST:PrepareResumeConfirmation,POST:PrepareDestructiveConfirmation;Service=POST:StartEvent,POST:EndEvent,POST:Discard,POST:Cancel;Hide=POST:Hide;Resume=POST:ResumeEvent;Review=POST:ReopenSubmissions;EvidenceCodes=POST:EnableEvidenceCodes,POST:DisableEvidenceCodes,POST:CreateEvidenceCode
Identity|true|true|Read=GET:,GET:Current;Identity=POST:
Schedule|true|true|Read=GET:,GET:Current;Schedule=POST:
Questions|true|true|Read=GET:;Signup=POST:
SignupSetup|true|true|Read=GET:,GET:Current;QuestionAdd=POST:,POST:AddAccount;Signup=POST:EditAccount,POST:Deactivate,POST:CoCaptain,POST:Move,POST:Edit,POST:Replace;Setup=POST:SignupAdministration,POST:SignupCode
Participants|true|true|Read=GET:,GET:SearchOwnerAccounts,GET:Current,GET:OwnerAccounts;Setup=POST:Withdraw,POST:Confirm,POST:MoveToWaiting,POST:Restore,POST:Add,POST:SignupAdministration;Service=POST:Payment,POST:SaveParticipant
Participant|true|true|Read=GET:
Board|true|true|Read=GET:,GET:EditorData,GET:Readback;RetainedArtwork=GET:TileImage;Board=POST:Create,POST:TakeEditing,POST:AcquireEditing,POST:ReleaseEditing,POST:RenewEditing,POST:CreateTile,POST:EditTile,POST:Move,POST:Resize,POST:TeamSize,POST:Publish,POST:DiscardCorrection,POST:Approve,POST:Unapprove,POST:Remove,POST:ApproveState,POST:PublishState;BoardCorrection=POST:CorrectPublished
BoardPreview|true|true|Read=GET:
Draft|true|true|Read=GET:,GET:Readback,GET:State,GET:TeamImage;Setup=POST:AddTeam,POST:RemoveDraftTeam,POST:UpdateTeam,POST:AddMember,POST:RemoveMember,POST:MoveMember,POST:Scramble,POST:Start,POST:Configure,POST:Pick,POST:Undo,POST:Cancel,POST:Finalize,POST:AcquireControl,POST:TakeControl,POST:ReleaseControl;Service=POST:ChangeRole
Finalize|true|true|Read=GET:,GET:Current;Service=POST:Resolve,POST:AcknowledgeCompletion,POST:CorrectCompletion,POST:Finalize,POST:Unfinalize,POST:Archive
WiseOldMan|true|true|Read=GET:,GET:Current;WomSetup=POST:Competition,POST:DisconnectCompetition,POST:CreateManagedCompetition,POST:AdoptCompetitionCredential,POST:DeleteManagedCompetition;WomFetch=POST:FetchCompetition;WomDevelopment=POST:MakeDevelopmentCompetitionDue
""";

    // RC08 / RC09: cached Current reads use the existing read-only capability.
    [Fact]
    public void EveryEventPageAndHttpHandlerHasItsExplicitGateAndTerminalViewContract()
    {
        // Enumerate actual Razor page files recursively, independent of their model namespace.
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Bingo.slnx"))) directory = directory.Parent;
        var root = Assert.IsType<DirectoryInfo>(directory).FullName;
        var models = Directory.EnumerateFiles(Path.Combine(root, "src/Bingo.Web/Pages/Admin/Events"), "*.cshtml", SearchOption.AllDirectories)
            .Select(File.ReadAllText).Where(source => Regex.IsMatch(source, @"(?m)^@page(?:\s|$)"))
            .Select(source => Assert.IsAssignableFrom<Type>(typeof(ManageModel).Assembly.GetType(Regex.Match(source, @"(?m)^@model\s+(\S+)").Groups[1].Value)))
            .OrderBy(type => type.Name).ToArray();
        Assert.Equal(models, AdminEventPagePolicies.All.OrderBy(entry => entry.Key.Name).Select(entry => entry.Key));
        var expectedPages = Expected.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .ToDictionary(line => line.Split('|')[0]);
        Assert.Equal(models.Select(type => type.Name[..^5]), expectedPages.Keys.Order());
        foreach (var model in models)
        {
            var fields = expectedPages[model.Name[..^5]].Split('|');
            var policy = Assert.IsType<AdminEventPagePolicy>(AdminEventPagePolicies.For(model));
            Assert.Equal(fields[0], policy.Kind.ToString());
            Assert.Equal(bool.Parse(fields[1]), policy.HasEventContext);
            Assert.Equal(bool.Parse(fields[2]), policy.ViewableOnTerminalEvents);
            var expectedHandlers = fields[3].Split(';').SelectMany(group =>
            {
                var pair = group.Split('=');
                return pair[1].Split(',').Select(key => (Key: key, Gate: Enum.Parse<AdminEventHandlerGate>(pair[0])));
            }).ToDictionary(item => item.Key, item => item.Gate);
            var handlers = model.GetMethods(BindingFlags.Instance | BindingFlags.Public)
                .Where(method => (method.Name.StartsWith("OnGet", StringComparison.Ordinal) || method.Name.StartsWith("OnPost", StringComparison.Ordinal))
                    && method.GetCustomAttribute<NonHandlerAttribute>() is null)
                .Select(method =>
                {
                    var name = method.Name.EndsWith("Async", StringComparison.Ordinal) ? method.Name[..^5] : method.Name;
                    return name.StartsWith("OnGet", StringComparison.Ordinal) ? $"GET:{name[5..]}" : $"POST:{name[6..]}";
                }).Order().ToArray();
            Assert.Equal(handlers, expectedHandlers.Keys.Order());
            Assert.Equal(handlers, policy.Handlers.Keys.Order());
            foreach (var (key, expectedGate) in expectedHandlers)
            {
                var keyParts = key.Split(':');
                Assert.Equal(expectedGate, policy.Handler(keyParts[0], keyParts[1]));
            }
            Assert.Throws<InvalidOperationException>(() => policy.Handler("POST", "UnclassifiedPaymentWithdraw"));
        }
    }

    [Theory]
    [InlineData("/Pages/Admin/Events/Unclassified.cshtml", false)]
    [InlineData("/Pages/Admin/Events/Nested/Unclassified.cshtml", false)]
    [InlineData("/Pages/Admin/Elsewhere/Unclassified.cshtml", true)]
    public async Task UnclassifiedEventPageFailsClosedIndependentOfModelNamespace(string path, bool allowed)
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().Options);
        var filter = new EventMutationCapabilityPageFilter(db, new NoText());
        var action = new CompiledPageActionDescriptor { RelativePath = path };
        var page = new PageContext(new ActionContext(new DefaultHttpContext(), new RouteData(), action, new ModelStateDictionary()));
        var context = new PageHandlerExecutingContext(page, [], null, new Dictionary<string, object?>(), new UnclassifiedPage());
        var called = false;
        await filter.OnPageHandlerExecutionAsync(context, () => { called = true; return Task.FromResult(new PageHandlerExecutedContext(page, [], null, context.HandlerInstance)); });
        Assert.Equal(allowed, called);
        if (allowed) Assert.Null(context.Result); else Assert.IsType<NotFoundResult>(context.Result);
    }
    private sealed class UnclassifiedPage : PageModel;
    private sealed class NoText : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => throw new InvalidOperationException("No localization or data access is needed to refuse an unclassified page.");
        public LocalizedString this[string name, params object[] arguments] => this[name];
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    [Theory]
    [InlineData(AdminEventHandlerGate.Signup, "Draft,SignupOpen,SignupClosed")]
    [InlineData(AdminEventHandlerGate.Identity, "Draft,SignupOpen,SignupClosed,Live,AwaitingFinalReview")]
    [InlineData(AdminEventHandlerGate.Setup, "Draft,SignupOpen,SignupClosed")]
    [InlineData(AdminEventHandlerGate.Schedule, "Draft,SignupOpen,SignupClosed,Live")]
    [InlineData(AdminEventHandlerGate.Resume, "AwaitingFinalReview")]
    [InlineData(AdminEventHandlerGate.Review, "Live,AwaitingFinalReview")]
    [InlineData(AdminEventHandlerGate.EvidenceCodes, "Draft,SignupOpen,SignupClosed,Live,AwaitingFinalReview")]
    [InlineData(AdminEventHandlerGate.Board, "Draft,SignupOpen,SignupClosed")]
    [InlineData(AdminEventHandlerGate.BoardCorrection, "SignupClosed,Live,AwaitingFinalReview")]
    [InlineData(AdminEventHandlerGate.WomSetup, "Draft,SignupOpen,SignupClosed,Live")]
    [InlineData(AdminEventHandlerGate.WomFetch, "Live,AwaitingFinalReview")]
    [InlineData(AdminEventHandlerGate.WomDevelopment, "Live")]
    public void GatedHandlersKeepTheExactLifecycleStateSets(AdminEventHandlerGate gate, string states)
    {
        var expected = states.Split(',').Select(Enum.Parse<EventState>).Order().ToArray();
        Assert.Equal(expected, Enum.GetValues<EventState>().Where(state => AdminEventPagePolicies.Allows(gate, state)).Order());
    }

    [Theory]
    [InlineData(AdminEventHandlerGate.Read)]
    [InlineData(AdminEventHandlerGate.RetainedArtwork)]
    [InlineData(AdminEventHandlerGate.Service)]
    [InlineData(AdminEventHandlerGate.Hide)]
    [InlineData(AdminEventHandlerGate.QuestionAdd)]
    public void SpecialHandlersRemainServiceOrContextOwned(AdminEventHandlerGate gate)
    {
        // Hidden/Discarded, terminal reads, exact Hide, retained artwork and D16
        // committed replay are checked in the filter before this generic matrix.
        Assert.All(Enum.GetValues<EventState>(), state => Assert.True(AdminEventPagePolicies.Allows(gate, state)));
    }
}
