using Microsoft.AspNetCore.Mvc;
using PortalWeb.Models;
using PortalWeb.Services;

namespace PortalWeb.Controllers;

public class AccountController(RegistrationApiClient api) : Controller
{
    [HttpGet]
    public IActionResult Register() => View(new RegisterFormModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterFormModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return View(model);

        var result = await api.SubmitAsync(model, ct);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            model.Password = model.ConfirmPassword = string.Empty; // never echo passwords back
            return View(model);
        }

        return RedirectToAction(nameof(RegistrationSuccess));
    }

    [HttpGet]
    public IActionResult RegistrationSuccess() => View();
}
