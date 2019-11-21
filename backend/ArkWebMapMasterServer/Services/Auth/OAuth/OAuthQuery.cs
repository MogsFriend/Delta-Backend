﻿using LibDeltaSystem.Db.System;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using static ArkWebMapMasterServer.Services.Auth.OAuth.OAuthScopeStatics;

namespace ArkWebMapMasterServer.Services.Auth.OAuth
{
    public static class OAuthQuery
    {
        /// <summary>
        /// Used to query app info from an ID
        /// </summary>
        /// <param name="e"></param>
        /// <param name="path"></param>
        /// <returns></returns>
        public static async Task OnQueryRequest(Microsoft.AspNetCore.Http.HttpContext e)
        {
            //Decode request
            OAuthInfoRequest request = Program.DecodePostBody<OAuthInfoRequest>(e);

            //Find the application
            DbOauthApp app = await Program.connection.GetOAuthAppByAppID(request.client_id);
            if (app == null)
                throw new StandardError("App not found.", StandardErrorCode.NotFound);

            //Get all scopes that are usable
            List<OAuthScopeEntry> scopes = OAuthScopeStatics.GetOAuthScopes(request.scopes);
            if (scopes.Count == 0)
                throw new StandardError("No scopes found.", StandardErrorCode.InvalidInput);

            //Determine if this is dangerous
            bool is_dangerous = false;
            foreach (var s in scopes)
                is_dangerous = is_dangerous || s.is_dangerous;

            //Respond
            await Program.QuickWriteJsonToDoc(e, new OAuthInfoResponse
            {
                name = app.name,
                description = app.description,
                icon = app.icon_url,
                is_dangerous = is_dangerous,
                scopes = scopes
            });
        }

        class OAuthInfoRequest
        {
            public string client_id;
            public string[] scopes;
        }

        class OAuthInfoResponse
        {
            public string name;
            public string description;
            public string icon;
            public List<OAuthScopeEntry> scopes;
            public bool is_dangerous;
        }
    }
}
