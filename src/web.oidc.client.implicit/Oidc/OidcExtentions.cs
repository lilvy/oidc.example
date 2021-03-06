﻿using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web.Helpers;
using Microsoft.IdentityModel.Protocols;
using Microsoft.Owin;
using Microsoft.Owin.Security.Cookies;
using Microsoft.Owin.Security.Notifications;
using Microsoft.Owin.Security.OpenIdConnect;
using Owin;

namespace ClientSite.Oidc
{
    public static class OidcExtentions
    {
        public static void UseOidcAuthentication(this IAppBuilder app)
        {
            AntiForgeryConfig.UniqueClaimTypeIdentifier = Constants.ClaimTypes.Subject;
            JwtSecurityTokenHandler.InboundClaimTypeMap = new Dictionary<string, string>();

            app.UseCookieAuthentication(new CookieAuthenticationOptions
            {
                AuthenticationType = Constants.AuthenticationTypeOfCookies,
                CookieHttpOnly = true,
                CookieName = Constants.CookieName,
                // 方便调试，明文。
                //TicketDataFormat = new AuthenticationTicketCleartextDataFormat()
            });

            app.UseOpenIdConnectAuthentication(new OpenIdConnectAuthenticationOptions
            {
                AuthenticationType = Constants.AuthenticationTypeOfOidc,
                Authority = "http://oidc-server.dev",
                ClientId = "implicit-client",
                ResponseType = "id_token",
                CallbackPath = new PathString("/oidc/login-callback"),
                SignInAsAuthenticationType = Constants.AuthenticationTypeOfCookies,
                Notifications = new OpenIdConnectAuthenticationNotifications
                {
                    SecurityTokenValidated = HandleSecurityTokenValidatedNotification,
                    RedirectToIdentityProvider = HandleRedirectToIdentityProviderNotification
                }
            });
        }

        private static Task HandleSecurityTokenValidatedNotification(SecurityTokenValidatedNotification<OpenIdConnectMessage, OpenIdConnectAuthenticationOptions> context)
        {
            context.AuthenticationTicket.Identity.AddClaim(new Claim(Constants.ClaimTypes.IdToken, context.ProtocolMessage.IdToken));
            return Task.FromResult(0);
        }

        private static Task HandleRedirectToIdentityProviderNotification(RedirectToIdentityProviderNotification<OpenIdConnectMessage, OpenIdConnectAuthenticationOptions> context)
        {
            context.ProtocolMessage.RedirectUri = context.Request.Uri.GetLeftPart(UriPartial.Authority);
            if (context.ProtocolMessage.RequestType == OpenIdConnectRequestType.LogoutRequest)
            {
                var idToken = context.OwinContext.Authentication.User.FindFirst(Constants.ClaimTypes.IdToken);
                if (idToken != null)
                {
                    context.ProtocolMessage.IdTokenHint = idToken.Value;
                }
            }
            if (context.ProtocolMessage.RequestType == OpenIdConnectRequestType.AuthenticationRequest)
            {
                context.ProtocolMessage.RedirectUri += context.Options.CallbackPath.ToString();
                var idp = context.OwinContext.Get<string>("idp");
                if (string.IsNullOrWhiteSpace(idp) == false)
                {
                    context.ProtocolMessage.AcrValues = "idp:" + idp;
                }
            }
            return Task.FromResult(0);
        }
    }
}