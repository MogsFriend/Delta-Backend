﻿using LibDeltaSystem.Db.System;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ArkWebMapMasterServer.Services.Users
{
    public static class UserClustersRequest
    {
        public static async Task OnHttpRequest(Microsoft.AspNetCore.Http.HttpContext e, DbUser u)
        {
            //Check scope
            await Program.CheckTokenScope(u, null);

            //Get method
            var method = Program.FindRequestMethod(e);
            if (method == RequestHttpMethod.get)
                await OnGETRequest(e, u);
            else if (method == RequestHttpMethod.post)
                await OnPOSTRequest(e, u);
            else
                throw new StandardError("This method was not expected.", StandardErrorCode.BadMethod);
        }

        public static async Task OnGETRequest(Microsoft.AspNetCore.Http.HttpContext e, DbUser u)
        {
            //Get clusters
            var clusters = await DbCluster.GetClustersForUser(Program.connection, u._id);

            //Convert all
            List<ClusterResponseData> response = new List<ClusterResponseData>();
            foreach(var c in clusters)
            {
                response.Add(new ClusterResponseData
                {
                    id = c.id,
                    name = c.name
                });
            }

            await Program.QuickWriteJsonToDoc(e, response);
        }

        public static async Task OnPOSTRequest(Microsoft.AspNetCore.Http.HttpContext e, DbUser u)
        {
            //Decode data
            var request = Program.DecodePostBody<ClusterCreateData>(e);

            //Check
            if (request.name == null)
                throw new StandardError("Missing name field.", StandardErrorCode.InvalidInput);
            if (request.name.Length > 24 || request.name.Length < 2)
                throw new StandardError("Name field is too long or too short.", StandardErrorCode.InvalidInput);

            //Add cluster
            var cluster = new DbCluster
            {
                _id = MongoDB.Bson.ObjectId.GenerateNewId(),
                name = request.name,
                owner = u._id
            };
            await Program.connection.system_clusters.InsertOneAsync(cluster);

            //Write the response
            await Program.QuickWriteJsonToDoc(e, new ClusterResponseData
            {
                id = cluster.id,
                name = cluster.name
            });
        }

        class ClusterCreateData
        {
            public string name;
        }

        class ClusterResponseData
        {
            public string id;
            public string name;
        }
    }
}
