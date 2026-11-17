using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.GameContent;
using Vintagestory.API.Server;
using ProtoBuf;

namespace chisel.src
{
    class BELockedMicroblock: BlockEntityChisel
    {
        string playerID;
        string playerName;
        bool locked = false;
        public string LockedBy => playerID;
        public string LockedByID => playerID;
        public bool IsLocked => locked;
        ICoreServerAPI sapi;

        public override void Initialize(ICoreAPI api)
        {
            if (api is ICoreServerAPI) { sapi = api as ICoreServerAPI; }
            base.Initialize(api);
        }

        public void SetLock(string playerID, string playerName, bool lockstate)
        {
            if (sapi == null) { return; }
            
            this.playerID = playerID;
            this.playerName = playerName;
            locked = lockstate;
            MarkDirty(false);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetString("playerID", playerID);
            tree.SetString("playerName", playerName);
            tree.SetBool("locked", locked);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            playerID = tree.GetAsString("playerID");
            playerName = tree.GetAsString("playerName");
            locked = tree.GetBool("locked");
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            if (locked) { dsc.AppendLine("LOCKED BY " + playerName); }
            base.GetBlockInfo(forPlayer, dsc);
        }
    }
}
