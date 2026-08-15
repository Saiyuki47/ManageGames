using ManageGames.Service;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ManageGames.Filters
{
    /// <summary>
    /// Marker attribute applied to actions as <c>[RequireLogin]</c>. It runs
    /// <see cref="RequireLoginFilter"/> through the DI container (TypeFilter) so the filter
    /// can constructor-inject its dependencies instead of using a service locator.
    /// </summary>
    public class RequireLoginAttribute : TypeFilterAttribute
    {
        public RequireLoginAttribute() : base(typeof(RequireLoginFilter))
        {
        }
    }

    /// <summary>
    /// Server-side authorization gate. Ensures the request carries a valid "GameSort+" cookie
    /// whose user id and cookie id match a user in the database. On success the validated user
    /// id is stored in <see cref="HttpContext.Items"/> so the action can trust it instead of
    /// re-reading request input; otherwise the request is bounced to the public start page.
    ///
    /// This enforces on the server what the client-side JavaScript only *hid* before, so the
    /// management endpoints can no longer be called by anonymous clients.
    /// </summary>
    public class RequireLoginFilter : IActionFilter
    {
        private readonly DataBase_Service _service;

        public RequireLoginFilter(DataBase_Service service)
        {
            _service = service;
        }

        public void OnActionExecuting(ActionExecutingContext context)
        {
            var http = context.HttpContext;
            http.Request.Cookies.TryGetValue(SessionCookie.CookieName, out var cookie);
            if (SessionCookie.TryParse(cookie, out var userId, out var cookieId)
                && _service.IsValidSession(userId, cookieId))
            {
                // Hand the authenticated id to the action so it never has to trust request input.
                http.Items[SessionCookie.UserIdItemKey] = userId;
                return;
            }

            context.Result = new RedirectToActionResult("Index", "Home", null);
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
        }
    }
}
