﻿using System;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using AspNet.Security.OpenIdConnect.Extensions;
using AspNet.Security.OpenIdConnect.Server;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Authentication;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using Moq;
using Newtonsoft.Json;

namespace OpenIddict.Core.Tests.Infrastructure {
    public partial class OpenIddictProviderTests {
        public const string AuthorizationEndpoint = "/connect/authorize";
        public const string ConfigurationEndpoint = "/.well-known/openid-configuration";
        public const string IntrospectionEndpoint = "/connect/introspect";
        public const string LogoutEndpoint = "/connect/logout";
        public const string RevocationEndpoint = "/connect/revoke";
        public const string TokenEndpoint = "/connect/token";
        public const string UserinfoEndpoint = "/connect/userinfo";

        private static TestServer CreateAuthorizationServer(Action<OpenIddictBuilder> configuration = null) {
            var builder = new WebHostBuilder();

            builder.UseEnvironment("Testing");

            builder.ConfigureLogging(options => options.AddDebug());

            builder.ConfigureServices(services => {
                var instance = services.AddOpenIddict<object, object, object, object>()
                    // Disable the transport security requirement during testing.
                    .DisableHttpsRequirement()

                    // Enable the tested endpoints.
                    .EnableAuthorizationEndpoint(AuthorizationEndpoint)
                    .EnableIntrospectionEndpoint(IntrospectionEndpoint)
                    .EnableLogoutEndpoint(LogoutEndpoint)
                    .EnableRevocationEndpoint(RevocationEndpoint)
                    .EnableTokenEndpoint(TokenEndpoint)
                    .EnableUserinfoEndpoint(UserinfoEndpoint)

                    // Enable the tested flows.
                    .AllowAuthorizationCodeFlow()
                    .AllowClientCredentialsFlow()
                    .AllowImplicitFlow()
                    .AllowPasswordFlow()
                    .AllowRefreshTokenFlow()

                    // Register the X.509 certificate used to sign the identity tokens.
                    .AddSigningCertificate(
                        assembly: typeof(OpenIddictProviderTests).GetTypeInfo().Assembly,
                        resource: "OpenIddict.Core.Tests.Certificate.pfx",
                        password: "OpenIddict")

                    // Note: overriding the default data protection provider is not necessary for the tests to pass,
                    // but is useful to ensure unnecessary keys are not persisted in testing environments, which also
                    // helps make the unit tests run faster, as no registry or disk access is required in this case.
                    .UseDataProtectionProvider(new EphemeralDataProtectionProvider());

                // Run the configuration delegate
                // registered by the unit tests.
                configuration?.Invoke(instance);
            });

            builder.Configure(app => {
                app.UseStatusCodePages(context => {
                    context.HttpContext.Response.Headers[HeaderNames.ContentType] = "application/json";

                    return context.HttpContext.Response.WriteAsync(JsonConvert.SerializeObject(new {
                        error_custom = OpenIdConnectConstants.Errors.InvalidRequest
                    }));
                });

                app.Use(next => context => {
                    if (context.Request.Path != "/authorize-status-code-middleware" &&
                        context.Request.Path != "/logout-status-code-middleware") {
                        var feature = context.Features.Get<IStatusCodePagesFeature>();
                        feature.Enabled = false;
                    }

                    return next(context);
                });

                app.UseOpenIddict();

                app.Run(context => {
                    if (context.Request.Path == AuthorizationEndpoint ||
                        context.Request.Path == TokenEndpoint) {
                        var request = context.GetOpenIdConnectRequest();

                        var identity = new ClaimsIdentity(OpenIdConnectServerDefaults.AuthenticationScheme);
                        identity.AddClaim(ClaimTypes.NameIdentifier, "Bob le Magnifique");

                        var ticket = new AuthenticationTicket(
                            new ClaimsPrincipal(identity),
                            new AuthenticationProperties(),
                            OpenIdConnectServerDefaults.AuthenticationScheme);

                        ticket.SetScopes(request.GetScopes());

                        return context.Authentication.SignInAsync(ticket.AuthenticationScheme, ticket.Principal, ticket.Properties);
                    }

                    else if (context.Request.Path == UserinfoEndpoint) {
                        context.Response.Headers[HeaderNames.ContentType] = "application/json";

                        return context.Response.WriteAsync(JsonConvert.SerializeObject(new {
                            sub = "Bob le Bricoleur"
                        }));
                    }

                    return Task.FromResult(0);
                });
            });

            return new TestServer(builder);
        }

        private static OpenIddictApplicationManager<object> CreateApplicationManager(Action<Mock<OpenIddictApplicationManager<object>>> setup = null) {
            var manager = new Mock<OpenIddictApplicationManager<object>>(
                Mock.Of<IServiceProvider>(),
                Mock.Of<IOpenIddictApplicationStore<object>>(),
                Mock.Of<ILogger<OpenIddictApplicationManager<object>>>());

            setup?.Invoke(manager);

            return manager.Object;
        }

        private static OpenIddictTokenManager<object> CreateTokenManager(Action<Mock<OpenIddictTokenManager<object>>> setup = null) {
            var manager = new Mock<OpenIddictTokenManager<object>>(
                Mock.Of<IServiceProvider>(),
                Mock.Of<IOpenIddictTokenStore<object>>(),
                Mock.Of<ILogger<OpenIddictTokenManager<object>>>());

            setup?.Invoke(manager);

            return manager.Object;
        }
    }
}
