using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NovaWallet.Application.Common.Responses
{
    public class ApiSuccess<T> : ApiResult
    {
        public bool Status { get; set; }
        public string Message { get; set; } = string.Empty;
        public T? Data { get; set; }

        public static ApiSuccess<T> Success(T data, string message)
        {
            return new ApiSuccess<T>
            {
                StatusCode = 200,
                Status = true,
                Message = message,
                Data = data
            };
        }
    }
}
