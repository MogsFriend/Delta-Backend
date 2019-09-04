﻿using ArkWebMapMasterServer.PresistEntities;
using System;
using System.Collections.Generic;
using System.Text;

namespace ArkWebMapMasterServer.NetEntities
{
    public class ServerSetupWizard_BeginReply
    {
        public string display_id;
        public bool ok;
        public string request_url;
        public ArkServer server;
    }

    public class ServerSetupWizard_BeginReplyHeadless : ServerSetupWizard_BeginReply
    {
        public string headless_config_url;
    }
}
