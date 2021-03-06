﻿/*
 * Copyright 2015 Dominick Baier, Brock Allen
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using Microsoft.Owin.Security.Jwt;
using Microsoft.Owin.Security.OAuth;
using System;
using System.IdentityModel.Tokens;
using System.Linq;
using Thinktecture.IdentityServer.AccessTokenValidation;

namespace Owin
{
    /// <summary>
    /// Extension method for wiring up the access token validation middleware to the OWIN pipeline
    /// </summary>
    public static class IdentityServerAccessTokenValidationAppBuilderExtensions
    {
        /// <summary>
        /// Adds the access token validation middleware to the OWIN pipeline.
        /// </summary>
        /// <param name="app">The application.</param>
        /// <param name="options">The options.</param>
        /// <returns></returns>
        /// <exception cref="System.ArgumentNullException">options</exception>
        public static IAppBuilder UseIdentityServerBearerTokenAuthentication(this IAppBuilder app, IdentityServerBearerTokenAuthenticationOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException("options");
            }

            if (options.ValidationMode == ValidationMode.Local)
            {
                app.UseLocalValidation(options);
            }
            else if (options.ValidationMode == ValidationMode.ValidationEndpoint)
            {
                app.UseValidationEndpoint(options);
            }

            if (options.RequiredScopes.Any())
            {
                app.Use<ScopeRequirementMiddleware>(options.RequiredScopes);
            }

            return app;
        }

        internal static void UseLocalValidation(this IAppBuilder app, IdentityServerBearerTokenAuthenticationOptions options)
        {
            var discoveryEndpoint = options.Authority;
            if (!discoveryEndpoint.EndsWith("/"))
            {
                discoveryEndpoint += "/";
            }

            discoveryEndpoint += ".well-known/openid-configuration";
            
            var provider = new DiscoveryCachingSecurityTokenProvider(
                discoveryEndpoint,
                options.BackchannelCertificateValidator,
                options.BackchannelHttpHandler);

            JwtFormat jwtFormat;
            if (options.TokenValidationParameters != null)
            {
                jwtFormat = new JwtFormat(options.TokenValidationParameters, provider);
            }
            else
            {
                var valParams = new TokenValidationParameters
                {
                    ValidAudience = provider.Audience,
                    NameClaimType = options.NameClaimType,
                    RoleClaimType = options.RoleClaimType
                };

                jwtFormat = new JwtFormat(valParams, provider);
            }

            if (options.TokenHandler != null)
            {
                jwtFormat.TokenHandler = options.TokenHandler;
            }

            var bearerOptions = new OAuthBearerAuthenticationOptions
            {
                Realm = provider.Audience,
                Provider = options.Provider,
                AccessTokenFormat = jwtFormat,
                AuthenticationMode = options.AuthenticationMode,
                AuthenticationType = options.AuthenticationType,
                Description = options.Description
            };

            app.UseOAuthBearerAuthentication(bearerOptions);
        }

        internal static void UseValidationEndpoint(this IAppBuilder app, IdentityServerBearerTokenAuthenticationOptions options)
        {
            if (options.EnableValidationResultCache)
            {
                if (options.ValidationResultCache == null)
                {
                    options.ValidationResultCache = new InMemoryValidationResultCache(options);
                }
            }

            app.UseOAuthBearerAuthentication(new OAuthBearerAuthenticationOptions
            {
                AccessTokenProvider = new ValidationEndpointTokenProvider(options),
                Provider = options.Provider
            });
        }
    }
}