using System.Reflection;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Bingo.Web.UI;

// Opt-in metadata only. Unbound pages retain the existing Admin layout.
[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class AdminDesignAttribute : Attribute
{
    public static bool AppliesTo(ActionDescriptor descriptor) =>
        descriptor.EndpointMetadata.OfType<AdminDesignAttribute>().Any()
        || descriptor is CompiledPageActionDescriptor page
            && page.ModelTypeInfo?.IsDefined(typeof(AdminDesignAttribute), true) == true;
}
