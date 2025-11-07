using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Web.Reportly.Controllers.Filters 
{
   
    public class BaseController : Controller
    {
        // Hàm này sẽ tự động chạy TRƯỚC khi bất kỳ Action nào (Index, Submit, History...) được gọi
        public override void OnActionExecuting(ActionExecutingContext context)
        {
            //  Kiểm tra bằng UserId (INT) 
            var userId = HttpContext.Session.GetInt32("UserId");

            if (!userId.HasValue || userId.Value == 0)
            {
                // Nếu không có, "đá" người dùng về trang Login
                context.Result = new RedirectToActionResult("Login", "Account", null);
            }

            base.OnActionExecuting(context);
        }
    }
}