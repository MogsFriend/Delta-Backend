﻿using ArkWebMapGatewayClient.Messages;
using ArkWebMapGatewayClient.Messages.Entities;
using ArkWebMapMasterServer.PresistEntities;
using ArkWebMapMasterServer.Tools;
using System;
using System.Collections.Generic;
using System.Text;

namespace ArkWebMapMasterServer.MirrorEntities
{
    public abstract class MirroredMessage
    {
        //Base class for messages
        public MirroredOpcode opcode;

        public abstract void ReadMsg(MirrorProtocolReader reader);
        public abstract Tuple<UpdateEntityRealtimePosition, int> ProcessMsg(ArkServer s, ArkMirrorToken auth);
    }
}