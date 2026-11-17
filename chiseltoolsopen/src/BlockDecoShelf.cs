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

namespace chiseltools.src
{
    public class BlockDecoShelf:BlockDeco
    {
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            // Todo: Add interaction help
        }

        //public override bool DoParticalSelection(IWorldAccessor world, BlockPos pos)
        //{
        //    return true;
        //}

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            BBChiseledCover mybb = this.GetBehavior<BBChiseledCover>();
            if (mybb != null)
            {

                bool handled = TryDecoInteract(world, byPlayer, blockSel);
                if (handled) { return true; }

            }
            BEDecoShelf beshelf = world.BlockAccessor.GetBlockEntity(blockSel.Position) as BEDecoShelf;
            if (beshelf != null) return beshelf.OnInteract(byPlayer, blockSel);


            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }
       
    }
}
