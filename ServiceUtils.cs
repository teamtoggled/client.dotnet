using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Toggled.Client
{
    internal sealed class ServiceUtils
    {
        private static readonly JwtSecurityTokenHandler JwtTokenHandler = new JwtSecurityTokenHandler();

        internal string Endpoint {get; private set;}

        private readonly string _accessKey;

        internal ServiceUtils(string connectionString) =>
            (Endpoint, _accessKey) = ParseConnectionString(connectionString);

        internal string GenerateAccessToken(string audience, string userId, TimeSpan? lifetime = null)
        {
            IEnumerable<Claim> claims = null;
            if (userId != null)
            {
                claims = new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userId)
                };
            }

            return GenerateAccessTokenInternal(audience, claims, lifetime ?? TimeSpan.FromHours(1));
        }

        private string GenerateAccessTokenInternal(string audience, IEnumerable<Claim> claims, TimeSpan lifetime)
        {
            var expire = DateTime.UtcNow.Add(lifetime);

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_accessKey));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var token = JwtTokenHandler.CreateJwtSecurityToken(
                issuer: null,
                audience: audience,
                subject: claims == null ? null : new ClaimsIdentity(claims),
                expires: expire,
                signingCredentials: credentials);
            return JwtTokenHandler.WriteToken(token);
        }

        private static readonly char[] PropertySeparator = { ';' };
        private static readonly char[] KeyValueSeparator = { '=' };
        private const string EndpointProperty = "endpoint";
        private const string AccessKeyProperty = "accesskey";

        private static (string, string) ParseConnectionString(string connectionString)
        {
            var properties = connectionString.Split(PropertySeparator, StringSplitOptions.RemoveEmptyEntries);
            if (properties.Length > 1)
            {
                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var property in properties)
                {
                    var kvp = property.Split(KeyValueSeparator, 2);
                    if (kvp.Length != 2) continue;

                    var key = kvp[0].Trim();
                    if (dict.ContainsKey(key))
                    {
                        throw new ArgumentException($"Duplicate properties found in connection string: {key}.");
                    }

                    dict.Add(key, kvp[1].Trim());
                }

                if (dict.ContainsKey(EndpointProperty) && dict.ContainsKey(AccessKeyProperty))
                {
                    return (dict[EndpointProperty].TrimEnd('/'), dict[AccessKeyProperty]);
                }
            }

            throw new ArgumentException($"Connection string missing required properties {EndpointProperty} and {AccessKeyProperty}.");
        }
    }
}