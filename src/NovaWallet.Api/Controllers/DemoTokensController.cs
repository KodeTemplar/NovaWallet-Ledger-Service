using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NovaWallet.Api.Extensions;
using NovaWallet.Application.Abstraction;
using NovaWallet.Application.Common.Responses;
using NovaWallet.Application.Models.DemoToken;
using NovaWallet.Domain.Constants;

namespace NovaWallet.Api.Controllers;

[ApiController]
[Route("api/auth/demo-token")]
public class DemoTokensController : ControllerBase
{
    private readonly IDemoTokenService _demoTokenService;
    private readonly IWebHostEnvironment _environment;

    public DemoTokensController(IDemoTokenService demoTokenService, IWebHostEnvironment environment)
    {
        _demoTokenService = demoTokenService;
        _environment = environment;
    }

    [AllowAnonymous]
    [HttpPost]
    public async Task<IActionResult> Create(DemoTokenRequest request)
    {
        if (!_environment.IsDevelopment())
        {
            return this.ApiProblem(ApiProblem.NotFound("Demo tokens can only be created in the development environment."));
        }

        var result = await _demoTokenService.CreateTokenAsync(request.CustomerId, request.Roles);

        return StatusCode(result.StatusCode, result);
    }

    [AllowAnonymous]
    [HttpGet("roles")]
    public Task<IActionResult> GetRoles()
    {
        if (!_environment.IsDevelopment())
        {
            return Task.FromResult<IActionResult>(this.ApiProblem(ApiProblem.NotFound("Demo roles are only available in the development environment.")));
        }

        var response = ApiSuccess<string[]>.Success(ApplicationRoles.All, "Roles retrieved successfully.");

        return Task.FromResult<IActionResult>(Ok(response));
    }
}