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
using System.Diagnostics;

using System.Collections;

namespace chiseltools.src
{
    public class BEDecoGeneric:BlockEntity
    {
        public BEBChiseledCover cover;
        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            cover = GetBehavior<BEBChiseledCover>();
        }

        
    }
}
