using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Bingo.Web.Pages.Errors;

[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public sealed class StatusCodeModel : PageModel
{
    public int Code { get; private set; }

    public void OnGet(int code)
    {
        Code = code;
        Response.StatusCode = code;
    }
}
