﻿
using ArkWebMapMasterServer.NetEntities;
using IdentityModel.OidcClient;
using LibDeltaSystem.Db.System;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace ArkWebMapMasterServer.SteamAuth
{
    public class SteamOpenID
    {
        /// <summary>
        /// Starts a session and returns a URL to redirect to.
        /// </summary>
        /// <param name="returner"></param>
        /// <returns></returns>
        public static string Begin(string state)
        {
            //Now, construct a URL to send the user to.
            string return_url = $"{Program.connection.config.hosts.master}/api/auth/steam_auth_return?state={state}";
            string encoded_return_url = System.Web.HttpUtility.UrlEncode(return_url);
            string url = $"https://steamcommunity.com/openid/login?openid.return_to={encoded_return_url}&openid.mode=checkid_setup&openid.ns=http%3A%2F%2Fspecs.openid.net%2Fauth%2F2.0&openid.identity=http%3A%2F%2Fspecs.openid.net%2Fauth%2F2.0%2Fidentifier_select&openid.claimed_id=http%3A%2F%2Fspecs.openid.net%2Fauth%2F2.0%2Fidentifier_select&openid.ns.sreg=http%3A%2F%2Fopenid.net%2Fextensions%2Fsreg%2F1.1&openid.realm={encoded_return_url}";
            return url;
        }

        /// <summary>
        /// This is old and due for some updates
        /// </summary>
        /// <param name="e"></param>
        /// <returns></returns>
        public static async Task<DbUser> Finish(Microsoft.AspNetCore.Http.HttpContext e)
        {
            //We'll now validate this with Steam. Create the request back to Steam servers
            string validation_url = "https://steamcommunity.com/openid/login"+e.Request.QueryString.Value.Replace("openid.mode=id_res", "openid.mode=check_authentication");
            string validation_return;
            try
            {
                using (WebClient hc = new WebClient())
                    validation_return = hc.DownloadString(validation_url);
            } catch
            {
                throw new StandardError("Steam server returned an error.", StandardErrorCode.ExternalAuthError);
            }

            //Return validation is really gross. We're just going to use a find.
            bool validation_failed = validation_return.Contains("is_valid:false");
            bool validation_ok = validation_return.Contains("is_valid:true");
            if (!validation_ok && !validation_failed)
                return null;

            //If return validation failed, throw an error
            if (validation_failed)
                return null;

            //Now, we have their ID and have validated it. Extract it from the URL.
            string steam_id = e.Request.Query["openid.claimed_id"].ToString().Substring("https://steamcommunity.com/openid/id/".Length);

            //Request this users' Steam profile.
            var profile = await Program.connection.GetSteamProfileById(steam_id);
            if (profile == null)
                return null;

            //Get user account
            DbUser user = await DbUser.GetUserBySteamID(Program.connection, profile);

            //Run callback. This'll allow a client to handle this themselves
            return user;
        }
    }
}
