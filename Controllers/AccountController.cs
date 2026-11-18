using KirkukInstitute.Models;
using KirkukInstitute.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace KirkukInstitute.Controllers;

public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signIn;
    private readonly Localizer _l;
    private readonly AuditService _audit;

    public AccountController(UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signIn,
        Localizer l, AuditService audit)
    {
        _users = users;
        _signIn = signIn;
        _l = l;
        _audit = audit;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Dashboard", "Home");
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid) return View(model);

        var user = await _users.FindByEmailAsync(model.Email.Trim());
        if (user is null || !user.IsActive)
        {
            ModelState.AddModelError(string.Empty, _l.T("Incorrect email or password, or the account is disabled.",
                "البريد الإلكتروني أو كلمة المرور غير صحيحة، أو الحساب معطّل."));
            return View(model);
        }

        var result = await _signIn.PasswordSignInAsync(user, model.Password, model.RememberMe, lockoutOnFailure: true);
        if (result.Succeeded)
        {
            await _audit.LogAsync("Login", "ApplicationUser", user.Id);
            if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                return Redirect(model.ReturnUrl);
            return RedirectToAction("Dashboard", "Home");
        }
        if (result.IsLockedOut)
        {
            ModelState.AddModelError(string.Empty, _l.T("This account is temporarily locked. Please try again later.",
                "تم قفل الحساب مؤقتاً بسبب محاولات دخول متكررة. حاول لاحقاً."));
        }
        else
        {
            ModelState.AddModelError(string.Empty, _l.T("Incorrect email or password.",
                "البريد الإلكتروني أو كلمة المرور غير صحيحة."));
        }
        return View(model);
    }

    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _audit.LogAsync("Logout", "ApplicationUser", User.CurrentUserId());
        await _signIn.SignOutAsync();
        return RedirectToAction("Index", "Home");
    }

    [AllowAnonymous]
    public IActionResult AccessDenied() => View();

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var user = await _users.GetUserAsync(User);
        if (user is null) return NotFound();
        return View(new ProfileViewModel
        {
            FullName = user.FullName,
            Email = user.Email ?? "",
            PhoneNumber = user.PhoneNumber,
            PreferredLanguage = user.PreferredLanguage
        });
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(ProfileViewModel model)
    {
        var user = await _users.GetUserAsync(User);
        if (user is null) return NotFound();
        model.Email = user.Email ?? "";
        if (!ModelState.IsValid) return View(model);

        user.FullName = model.FullName.Trim();
        user.PhoneNumber = model.PhoneNumber?.Trim();
        var lang = model.PreferredLanguage == "en" ? "en" : "ar";
        user.PreferredLanguage = lang;
        var result = await _users.UpdateAsync(user);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, string.Join(" ", result.Errors.Select(e => e.Description)));
            return View(model);
        }
        SetCultureCookie(lang);
        await _signIn.RefreshSignInAsync(user);
        await _audit.LogAsync("Update profile", "ApplicationUser", user.Id);
        TempData["Success"] = _l.T("Your profile has been updated.", "تم تحديث ملفك الشخصي.");
        return RedirectToAction(nameof(Profile));
    }

    [Authorize]
    [HttpGet]
    public IActionResult ChangePassword() => View(new ChangePasswordViewModel());

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = await _users.GetUserAsync(User);
        if (user is null) return NotFound();

        var result = await _users.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return View(model);
        }
        await _signIn.RefreshSignInAsync(user);
        await _audit.LogAsync("Change password", "ApplicationUser", user.Id);
        TempData["Success"] = _l.T("Your password has been changed.", "تم تغيير كلمة المرور بنجاح.");
        return RedirectToAction(nameof(Profile));
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> SetLanguage(string lang, string? returnUrl = null)
    {
        lang = lang == "en" ? "en" : "ar";
        SetCultureCookie(lang);
        // Must be awaited: the request-scoped DbContext is disposed as soon as the action returns.
        await UpdateUserLanguageAsync(lang);
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
        return RedirectToAction("Index", "Home");
    }

    private async Task UpdateUserLanguageAsync(string lang)
    {
        if (User.Identity?.IsAuthenticated != true) return;
        var user = await _users.GetUserAsync(User);
        if (user is null) return;
        user.PreferredLanguage = lang;
        await _users.UpdateAsync(user);
    }

    private void SetCultureCookie(string lang)
    {
        Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(lang, lang)),
            new CookieOptions
            {
                Path = "/",
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                IsEssential = true,
                SameSite = SameSiteMode.Lax
            });
    }
}
