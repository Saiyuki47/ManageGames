using ManageGames.Service;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ManageGames.Filters
{
    /// <summary>
    /// Applied to actions as <c>[RequireAdmin]</c>. Runs <see cref="RequireAdminFilter"/> through
    /// the DI container (TypeFilter) so it can constructor-inject its dependencies.
    /// </summary>
    public class RequireAdminAttribute : TypeFilterAttribute
    {
        public RequireAdminAttribute() : base(typeof(RequireAdminFilter))
        {
        }
    }

    /// <summary>
    /// Like <see cref="RequireLoginFilter"/> but additionally requires the authenticated user to
    /// be an admin. Guards management of globally-shared data (consoles, companies, users) so a
    /// normal logged-in user cannot mutate or delete records other users depend on. On success the
    /// validated user id is stashed in <see cref="Microsoft.AspNetCore.Http.HttpContext.Items"/>
    /// just like the login filter; otherwise the request is bounced to the public start page.
    /// </summary>
    public class RequireAdminFilter : IActionFilter
    {
        private readonly DataBase_Service _service;

        public RequireAdminFilter(DataBase_Service service)
        {
            _service = service;
        }

        public void OnActionExecuting(ActionExecutingContext context)
        {
            var http = context.HttpContext;
            http.Request.Cookies.TryGetValue(SessionCookie.CookieName, out var cookie);
            if (SessionCookie.TryParse(cookie, out var userId, out var cookieId)
                && _service.IsValidAdminSession(userId, cookieId))
            {
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
