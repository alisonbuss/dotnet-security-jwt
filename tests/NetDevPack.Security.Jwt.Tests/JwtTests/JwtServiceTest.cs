﻿using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Bogus;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using NetDevPack.Security.Jwt.Core.Interfaces;
using NetDevPack.Security.Jwt.Tests.Warmups;
using Xunit;

namespace NetDevPack.Security.Jwt.Tests.JwtTests
{
    public class JwtServiceTest : IClassFixture<WarmupInMemoryStore>
    {
        private readonly IJwtService _jwksService;

        public JwtServiceTest(WarmupInMemoryStore warmup)
        {
            _jwksService = warmup.Services.GetRequiredService<IJwtService>();
        }


        [Fact]
        public async Task ShouldGenerateDefaultSigning()
        {
            var sign = await _jwksService.GenerateKey();
            var current = await _jwksService.GetCurrentSigningCredentials();
            current.Kid.Should().Be(sign.KeyId);
        }

        [Fact]
        public async Task ShouldNotThrowExceptionWhenGetSignManyTimes()
        {
            var currentA = await _jwksService.GetCurrentSigningCredentials();
            var currentB = await _jwksService.GetCurrentSigningCredentials();
            var currentCg = await _jwksService.GetCurrentSigningCredentials();

            var token = new SecurityTokenDescriptor()
            {
                Issuer = "test.jwt",
                Subject = new ClaimsIdentity(),
                Expires = DateTime.UtcNow.AddMinutes(3),
                SigningCredentials = await _jwksService.GetCurrentSigningCredentials()
            };
        }

        [Fact]
        public async Task ShouldGenerateFiveKeys()
        {
            var keysGenerated = new List<SecurityKey>();
            for (int i = 0; i < 5; i++)
            {
                var sign = await _jwksService.GenerateKey();
                keysGenerated.Add(sign);
            }

            var current = await _jwksService.GetLastKeys(5);
            foreach (var securityKey in current)
            {
                keysGenerated.Should().Contain(s => s.KeyId == securityKey.KeyId);
            }
        }


        [Fact]
        public async Task ShouldValidateJweAndJws()
        {

            var encryptingCredentials = await _jwksService.GetCurrentEncryptingCredentials();
            var signingCredentials = await _jwksService.GetCurrentSigningCredentials();

            var handler = new JsonWebTokenHandler();
            var now = DateTime.Now;
            var jwtE = new SecurityTokenDescriptor
            {
                Issuer = "me",
                Audience = "you",
                IssuedAt = now,
                NotBefore = now,
                Expires = now.AddMinutes(5),
                Subject = new ClaimsIdentity(GenerateClaim().Generate(5)),
                EncryptingCredentials = encryptingCredentials
            };
            var jwtS = new SecurityTokenDescriptor
            {
                Issuer = "me",
                Audience = "you",
                IssuedAt = now,
                NotBefore = now,
                Expires = now.AddMinutes(5),
                Subject = new ClaimsIdentity(GenerateClaim().Generate(5)),
                SigningCredentials = signingCredentials
            };


            var jwe = handler.CreateToken(jwtE);
            var jws = handler.CreateToken(jwtS);

            var jweResult = handler.ValidateToken(jwe,
                new TokenValidationParameters
                {
                    ValidIssuer = "me",
                    ValidAudience = "you",
                    RequireSignedTokens = false,
                    TokenDecryptionKey = encryptingCredentials.Key
                });
            var jwsResult = handler.ValidateToken(jws,
                new TokenValidationParameters
                {
                    ValidIssuer = "me",
                    ValidAudience = "you",
                    RequireSignedTokens = false,
                    TokenDecryptionKey = encryptingCredentials.Key
                });

            jweResult.IsValid.Should().BeTrue();
        }



        public Faker<Claim> GenerateClaim()
        {
            return new Faker<Claim>().CustomInstantiator(f => new Claim(f.Internet.DomainName(), f.Lorem.Text()));
        }
    }
}
