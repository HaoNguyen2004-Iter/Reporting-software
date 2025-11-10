using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Authorization;

namespace Web.Reportly.Controllers.Filters
{
    public class BaseController : Controller
    {
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            // 1. Kiểm tra xem Action hiện tại có cho phép ẩn danh (AllowAnonymous) không
            if (HasAllowAnonymous(context))
            {
                // Nếu có, bỏ qua kiểm tra Session 
                base.OnActionExecuting(context);
                return;
            }

            // 2. Nếu không có [AllowAnonymous], mới kiểm tra Session
            var userId = HttpContext.Session.GetInt32("UserId");
            if (!userId.HasValue || userId.Value == 0)
            {
                // Đá về trang Login nếu chưa đăng nhập
                context.Result = new RedirectToActionResult("Login", "Account", null);
            }

            base.OnActionExecuting(context);
        }

        // Hàm helper để kiểm tra attribute [AllowAnonymous]
        private bool HasAllowAnonymous(ActionExecutingContext context)
        {
            if (context.ActionDescriptor is ControllerActionDescriptor actionDescriptor)
            {
                // Kiểm tra xem action hoặc controller có gắn [AllowAnonymous] không
                var hasAllowAnonymous = actionDescriptor.MethodInfo.GetCustomAttributes(inherit: true)
                    .Any(a => a.GetType() == typeof(AllowAnonymousAttribute));
                
                if (hasAllowAnonymous) return true;

                // Kiểm tra thêm ở cấp Controller
                return actionDescriptor.ControllerTypeInfo.GetCustomAttributes(inherit: true)
                    .Any(a => a.GetType() == typeof(AllowAnonymousAttribute));
            }
            return false;
        }
    }
}