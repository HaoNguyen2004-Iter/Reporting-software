using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;
using Service.Reportly.Model;

namespace Web.Reportly.Controllers
{
    public class AccountController : Controller
    {
        private readonly IHttpClientFactory _httpFactory;
        // private const string AuthServerUrl = "http://192.168.1.86:5000";
        private const string AuthServerUrl = "http://10.40.77.154:5000";

        public AccountController(IHttpClientFactory httpFactory)
        {
            _httpFactory = httpFactory;
        }

        [HttpGet]
        public IActionResult Login()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string email, string password)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                TempData["Error"] = "Vui lòng nhập đầy đủ thông tin.";
                return View();
            }

            try
            {
                var client = _httpFactory.CreateClient();
                
                var loginRequest = new { Email = email, Password = password };
                var response = await client.PostAsJsonAsync($"{AuthServerUrl}/api/Account/sign-in", loginRequest);

                if (!response.IsSuccessStatusCode)
                {
                    TempData["Error"] = "Email hoặc mật khẩu không đúng.";
                    return View();
                }

                var apiUrl = $"{AuthServerUrl}/api/Account/employee/email/{email}";
                var user = await client.GetFromJsonAsync<EmployeeResponse>(apiUrl);

                if (user == null)
                {
                    TempData["Error"] = "Không tìm thấy thông tin người dùng.";
                    return View();
                }

               
            
                HttpContext.Session.SetInt32("UserId", user.Id); 
                HttpContext.Session.SetString("UserEmail", user.Email ?? "");
                HttpContext.Session.SetString("FullName", user.Fullname ?? "");
                HttpContext.Session.SetString("DepartmentName", user.DepartmentName ?? "");
                HttpContext.Session.SetInt32("Phone", user.Phone ?? 0);
                HttpContext.Session.SetString("Position", user.Position ?? "");
                HttpContext.Session.SetString("JobPositionName", user.JobPositionName ?? "");

                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Lỗi khi đăng nhập: {ex.Message}";
                return View();
            }
        }

        [HttpGet("logout")]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }
    }
}