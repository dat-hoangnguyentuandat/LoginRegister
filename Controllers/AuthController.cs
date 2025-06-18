using Microsoft.AspNetCore.Mvc;
using FirstApp.Models;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace FirstApp.Controllers
{
    public class AuthController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AuthController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public IActionResult Login()
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string username, string password, bool rememberMe = false)
        {
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                return Json(new { success = false, message = "điền đầy đủ username và password", error = "empty" });
            }

            // Kiểm tra ký tự Unicode trong username
            if (ContainsUnicode(username))
            {
                return Json(new { success = false, message = "username không được dùng kí tự unicode", error = "username" });
            }

            // Kiểm tra ký tự Unicode trong password
            if (ContainsUnicode(password))
            {
                return Json(new { success = false, message = "password không được dùng kí tự unicode", error = "password" });
            }

            var user = _context.Users.FirstOrDefault(u => u.Username == username);
            if (user == null || user.Password != password)
            {
                return Json(new { success = false, message = "sai thông tin đăng nhập", error = "invalid" });
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            var authProperties = new AuthenticationProperties
            {
                IsPersistent = rememberMe,
                ExpiresUtc = rememberMe ? DateTimeOffset.UtcNow.AddDays(7) : null, // Cookie sẽ hết hạn sau 7 ngày nếu remember me = true
                IssuedUtc = DateTimeOffset.UtcNow,
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            // Cập nhật trạng thái RememberMe trong database
            user.RememberMe = rememberMe;
            await _context.SaveChangesAsync();

            return Json(new { success = true, redirectUrl = "/home" });
        }

        [HttpGet]
        public IActionResult Register()
        {
            if (User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Register(string username, string password)
        {
            // Kiểm tra trống
            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                return Json(new { success = false, message = "Vui lòng điền đầy đủ thông tin" });
            }

            // Kiểm tra độ dài username
            if (username.Length < 3 || username.Length > 50)
            {
                return Json(new { success = false, message = "Username phải từ 3-50 ký tự", fields = new[] { "username" } });
            }

            // Kiểm tra Unicode trong username
            if (ContainsUnicode(username))
            {
                return Json(new { success = false, message = "Username không được dùng ký tự unicode", fields = new[] { "username" } });
            }

            // Kiểm tra độ dài password
            if (password.Length < 6 || password.Length > 100)
            {
                return Json(new { success = false, message = "Password phải từ 6-100 ký tự", fields = new[] { "password" } });
            }

            // Kiểm tra Unicode trong password
            if (ContainsUnicode(password))
            {
                return Json(new { success = false, message = "Password không được dùng ký tự unicode", fields = new[] { "password" } });
            }

            // Kiểm tra username đã tồn tại
            if (await _context.Users.AnyAsync(u => u.Username == username))
            {
                return Json(new { success = false, message = "Username đã tồn tại", fields = new[] { "username" } });
            }

            // Tạo user mới
            var user = new User
            {
                Username = username,
                Password = password,
                RememberMe = false
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Tự động đăng nhập sau khi đăng ký
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString())
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = false,
                ExpiresUtc = null,
                IssuedUtc = DateTimeOffset.UtcNow
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            return Json(new { success = true, redirectUrl = "/home" });
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            // Xóa cookie xác thực
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            
            // Tìm và cập nhật RememberMe của user thành false
            if (User.Identity.IsAuthenticated)
            {
                var username = User.Identity.Name;
                var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
                if (user != null)
                {
                    user.RememberMe = false;
                    await _context.SaveChangesAsync();
                }
            }

            return RedirectToAction("Login", "Auth");
        }

        private bool ContainsUnicode(string input)
        {
            return Regex.IsMatch(input, @"[^\u0000-\u007F]");
        }
    }
} 