using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using NovaWallet.Application.Abstraction;
using NovaWallet.Application.Common.Responses;
using NovaWallet.Application.Models.DemoToken;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace NovaWallet.Infrastructure.Services
{
    public class DemoTokenService : IDemoTokenService
    {
        private readonly IConfiguration _configuration;

        public DemoTokenService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<ApiResult> CreateTokenAsync(string customerId, string[]? roles)
        {
            if (string.IsNullOrWhiteSpace(customerId))
            {
                return ApiProblem.Validation("CustomerId is required.");
            }

            var jwtSettings = _configuration.GetSection("Jwt");
            var signingKey = jwtSettings["SigningKey"];

            if (string.IsNullOrWhiteSpace(signingKey))
            {
                throw new InvalidOperationException("JWT signing key is not configured.");
            }

            var expiresAtUtc = DateTimeOffset.UtcNow.AddHours(2);

            var claims = new List<Claim>
                {
                    new Claim(JwtRegisteredClaimNames.Sub, customerId),
                    new Claim("customer_id", customerId)
                };

            if (roles != null)
            {
                foreach (var role in roles)
                {
                    if (!string.IsNullOrWhiteSpace(role))
                    {
                        claims.Add(new Claim(ClaimTypes.Role, role));
                    }
                }
            }

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
            var signingCredentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                claims: claims,
                expires: expiresAtUtc.UtcDateTime,
                signingCredentials: signingCredentials);

            var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

            var result = new DemoTokenResult
            {
                AccessToken = accessToken,
                ExpiresAtUtc = expiresAtUtc
            };

            return ApiSuccess<DemoTokenResult>.Success(result, "Demo token generated successfully.");
        }
    }
}
