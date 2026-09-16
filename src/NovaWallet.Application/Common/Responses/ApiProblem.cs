using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NovaWallet.Application.Common.Responses
{
    public class ApiProblem : ApiResult
    {
        public string Type { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Detail { get; set; } = string.Empty;
        public string ErrorCode { get; set; } = string.Empty;

        public static ApiProblem Validation(string detail)
        {
            return new ApiProblem
            {
                StatusCode = 400,
                Type = "validation",
                Title = "Validation failed",
                Detail = detail,
                ErrorCode = "VALIDATION_ERROR"
            };
        }

        public static ApiProblem Validation(string detail, string errorCode)
        {
            return new ApiProblem
            {
                StatusCode = 400,
                Type = "validation",
                Title = "Validation failed",
                Detail = detail,
                ErrorCode = errorCode
            };
        }

        public static ApiProblem NotFound(string detail)
        {
            return new ApiProblem
            {
                StatusCode = 404,
                Type = "not-found",
                Title = "Resource not found",
                Detail = detail,
                ErrorCode = "NOT_FOUND"
            };
        }

        public static ApiProblem NotFound(string detail, string errorCode)
        {
            return new ApiProblem
            {
                StatusCode = 404,
                Type = "not-found",
                Title = "Resource not found",
                Detail = detail,
                ErrorCode = errorCode
            };
        }

        public static ApiProblem Conflict(string detail, string errorCode)
        {
            return new ApiProblem
            {
                StatusCode = 409,
                Type = "conflict",
                Title = "Conflict",
                Detail = detail,
                ErrorCode = errorCode
            };
        }

        public static ApiProblem Unprocessable(string detail, string errorCode)
        {
            return new ApiProblem
            {
                StatusCode = 422,
                Type = "unprocessable",
                Title = "Unable to process request",
                Detail = detail,
                ErrorCode = errorCode
            };
        }
    }
}
