using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ManageGames.Auth
{
    /// <summary>
    /// Marks actions a user may still reach while a password change is pending
    /// (the change-password page itself, logout and the error page).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public sealed class AllowPendingPasswordChangeAttribute : Attribute
    {
    }

    /// <summary>
    /// Global filter: a signed-in user who still has to replace a generated, admin-assigned or
    /// reset password is sent to the change-password page until they have done so.
    /// </summary>
    public class RequirePasswordChangeFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            if (context.HttpContext.User.HasPendingPasswordChange()
                && !context.ActionDescriptor.EndpointMetadata.OfType<AllowPendingPasswordChangeAttribute>().Any())
            {
                context.Result = new RedirectToActionResult("ChangePassword", "Account", null);
            }
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
        }
    }
}
