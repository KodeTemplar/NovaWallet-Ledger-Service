using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using NovaWallet.Application.Common.Responses;
using NovaWallet.Application.Common.Settings;

namespace NovaWallet.Api.Extensions
{
    public static class ControllerExtensions
    {
        public static IActionResult ToActionResult(this ControllerBase controller, ApiResult result)
        {
            if (result is ApiProblem problem)
            {
                return controller.ApiProblem(problem);
            }

            return controller.StatusCode(result.StatusCode, result);
        }

        public static ObjectResult ApiProblem(this ControllerBase controller, ApiProblem problem)
        {
            var settings = controller.HttpContext.RequestServices.GetRequiredService<IOptions<ProblemDetailsSettings>>().Value;
            var typeUrl = $"{settings.TypeBaseUrl.TrimEnd('/')}/{problem.Type}";

            var problemDetails = new ProblemDetails
            {
                Status = problem.StatusCode,
                Type = typeUrl,
                Title = problem.Title,
                Detail = problem.Detail,
                Instance = controller.HttpContext.Request.Path
            };

            problemDetails.Extensions["errorCode"] = problem.ErrorCode;
            problemDetails.Extensions["traceId"] = controller.HttpContext.TraceIdentifier;

            return new ObjectResult(problemDetails)
            {
                StatusCode = problem.StatusCode
            };
        }
    }
}
