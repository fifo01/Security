// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Net;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Microsoft.AspNetCore.Authentication.Test.OpenIdConnect
{
    public class OpenIdConnectConfigurationTests
    {
        [Fact]
        public async Task MetadataAddressIsGeneratedFromAuthorityWhenMissing()
        {
            var builder = new WebHostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddAuthentication()
                        .AddCookie()
                        .AddOpenIdConnect(o =>
                    {
                        o.Authority = TestServerBuilder.DefaultAuthority;
                        o.ClientId = Guid.NewGuid().ToString();
                        o.SignInScheme = Guid.NewGuid().ToString();
                    });
                })
                .Configure(app =>
                {
                    app.UseAuthentication();
                    app.Run(async context =>
                    {
                        var resolver = context.RequestServices.GetRequiredService<IAuthenticationHandlerProvider>();
                        var handler = await resolver.GetHandlerAsync(context, OpenIdConnectDefaults.AuthenticationScheme) as OpenIdConnectHandler;
                        Assert.Equal($"{TestServerBuilder.DefaultAuthority}/.well-known/openid-configuration", handler.Options.MetadataAddress);
                    });
                });
            var server = new TestServer(builder);
            var transaction = await server.SendAsync(@"https://example.com");
            Assert.Equal(HttpStatusCode.OK, transaction.Response.StatusCode);
        }

        [Fact]
        public async Task TargetsSelfDoesntStackOverflow()
        {
            var services = new ServiceCollection().AddOptions().AddLogging();

            services.AddAuthentication(OpenIdConnectDefaults.AuthenticationScheme)
                .AddOpenIdConnect(o =>
                {
                    o.ForwardDefault = OpenIdConnectDefaults.AuthenticationScheme;
                    o.ClientId = "Test Id";
                    o.ClientSecret = "Test Secret";
                    o.Authority = TestServerBuilder.DefaultAuthority;
                    o.SignInScheme = "Cookies";
                })
                .AddCookie()
                .AddScheme("alias", "alias", p => p.ForwardDefault = OpenIdConnectDefaults.AuthenticationScheme);

            var sp = services.BuildServiceProvider();
            var context = new DefaultHttpContext();
            context.RequestServices = sp;

            const string error = "resulted in a recursive call back to itself";

            var e = await Assert.ThrowsAsync<InvalidOperationException>(() => context.AuthenticateAsync());
            Assert.Contains(error, e.Message);
            e = await Assert.ThrowsAsync<InvalidOperationException>(() => context.AuthenticateAsync(OpenIdConnectDefaults.AuthenticationScheme));
            Assert.Contains(error, e.Message);
            e = await Assert.ThrowsAsync<InvalidOperationException>(() => context.AuthenticateAsync("alias"));
            Assert.Contains(error, e.Message);
            e = await Assert.ThrowsAsync<InvalidOperationException>(() => context.ChallengeAsync());
            Assert.Contains(error, e.Message);
            e = await Assert.ThrowsAsync<InvalidOperationException>(() => context.ChallengeAsync(OpenIdConnectDefaults.AuthenticationScheme));
            Assert.Contains(error, e.Message);
            e = await Assert.ThrowsAsync<InvalidOperationException>(() => context.ChallengeAsync("alias"));
            Assert.Contains(error, e.Message);
            e = await Assert.ThrowsAsync<InvalidOperationException>(() => context.ForbidAsync());
            Assert.Contains(error, e.Message);
            e = await Assert.ThrowsAsync<InvalidOperationException>(() => context.ForbidAsync(OpenIdConnectDefaults.AuthenticationScheme));
            Assert.Contains(error, e.Message);
            e = await Assert.ThrowsAsync<InvalidOperationException>(() => context.ForbidAsync("alias"));
            Assert.Contains(error, e.Message);
            e = await Assert.ThrowsAsync<InvalidOperationException>(() => context.SignOutAsync());
            Assert.Contains(error, e.Message);
            e = await Assert.ThrowsAsync<InvalidOperationException>(() => context.SignOutAsync(OpenIdConnectDefaults.AuthenticationScheme));
            Assert.Contains(error, e.Message);
            e = await Assert.ThrowsAsync<InvalidOperationException>(() => context.SignOutAsync("alias"));
            Assert.Contains(error, e.Message);

            const string noHandlerError = "is configured to handle sign";

            e = await Assert.ThrowsAsync<InvalidOperationException>(() => context.SignInAsync(new ClaimsPrincipal()));
            Assert.Contains(noHandlerError, e.Message);
            e = await Assert.ThrowsAsync<InvalidOperationException>(() => context.SignInAsync(OpenIdConnectDefaults.AuthenticationScheme, new ClaimsPrincipal()));
            Assert.Contains(noHandlerError, e.Message);
            e = await Assert.ThrowsAsync<InvalidOperationException>(() => context.SignInAsync("alias", new ClaimsPrincipal()));
            Assert.Contains(noHandlerError, e.Message);
        }


        [Fact]
        public Task ThrowsWhenSignInSchemeIsSetToSelf()
        {
            return TestConfigurationException<InvalidOperationException>(
                o =>
                {
                    o.SignInScheme = OpenIdConnectDefaults.AuthenticationScheme;
                    o.Authority = TestServerBuilder.DefaultAuthority;
                    o.ClientId = "Test Id";
                    o.ClientSecret = "Test Secret";
                },
                ex => Assert.Contains("cannot be set to itself", ex.Message));
        }

        [Fact]
        public Task ThrowsWhenClientIdIsMissing()
        {
            return TestConfigurationException<ArgumentException>(
                o =>
                {
                    o.SignInScheme = "TestScheme";
                    o.Authority = TestServerBuilder.DefaultAuthority;
                },
                ex => Assert.Equal("ClientId", ex.ParamName));
        }

        [Fact]
        public Task ThrowsWhenAuthorityIsMissing()
        {
            return TestConfigurationException<InvalidOperationException>(
                o =>
                {
                    o.SignInScheme = "TestScheme";
                    o.ClientId = "Test Id";
                    o.CallbackPath = "/";
                },
                ex => Assert.Equal("Provide Authority, MetadataAddress, Configuration, or ConfigurationManager to OpenIdConnectOptions", ex.Message)
            );
        }

        [Fact]
        public Task ThrowsWhenAuthorityIsNotHttps()
        {
            return TestConfigurationException<InvalidOperationException>(
                o =>
                {
                    o.SignInScheme = "TestScheme";
                    o.ClientId = "Test Id";
                    o.MetadataAddress = "http://example.com";
                    o.CallbackPath = "/";
                },
                ex => Assert.Equal("The MetadataAddress or Authority must use HTTPS unless disabled for development by setting RequireHttpsMetadata=false.", ex.Message)
            );
        }

        [Fact]
        public Task ThrowsWhenMetadataAddressIsNotHttps()
        {
            return TestConfigurationException<InvalidOperationException>(
                o =>
                {
                    o.SignInScheme = "TestScheme";
                    o.ClientId = "Test Id";
                    o.MetadataAddress = "http://example.com";
                    o.CallbackPath = "/";
                },
                ex => Assert.Equal("The MetadataAddress or Authority must use HTTPS unless disabled for development by setting RequireHttpsMetadata=false.", ex.Message)
            );
        }

        [Fact]
        public Task ThrowsWhenMaxAgeIsNegative()
        {
            return TestConfigurationException<ArgumentOutOfRangeException>(
                o =>
                {
                    o.SignInScheme = "TestScheme";
                    o.ClientId = "Test Id";
                    o.Authority = TestServerBuilder.DefaultAuthority;
                    o.MaxAge = TimeSpan.FromSeconds(-1);
                },
                ex => Assert.StartsWith("The value must not be a negative TimeSpan.", ex.Message)
            );
        }

        private TestServer BuildTestServer(Action<OpenIdConnectOptions> options)
        {
            var builder = new WebHostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddAuthentication()
                        .AddCookie()
                        .AddOpenIdConnect(options);
                })
                .Configure(app => app.UseAuthentication());

            return new TestServer(builder);
        }

        private async Task TestConfigurationException<T>(
            Action<OpenIdConnectOptions> options,
            Action<T> verifyException)
            where T : Exception
        {
            var exception = await Assert.ThrowsAsync<T>(() => BuildTestServer(options).SendAsync(@"https://example.com"));
            verifyException(exception);
        }
    }
}
