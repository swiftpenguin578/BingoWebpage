using System.Reflection;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Bingo.Web.UI;

// Marks a page bound to the redesigned Admin shell. Since U10 part 2 every Admin page renders in that shell (_ViewStart).
[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class AdminDesignAttribute : Attribute
{
    public static bool AppliesTo(ActionDescriptor descriptor) =>
        descriptor.EndpointMetadata.OfType<AdminDesignAttribute>().Any()
        || descriptor is CompiledPageActionDescriptor page
            && page.ModelTypeInfo?.IsDefined(typeof(AdminDesignAttribute), true) == true;
}
