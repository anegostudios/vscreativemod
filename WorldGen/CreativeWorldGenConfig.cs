using Newtonsoft.Json;
using Vintagestory.API.Common;

#nullable disable

namespace Vintagestory.ServerMods.NoObf
{
    [JsonObject(MemberSerialization.OptIn)]
    public class FlatWorldGenConfig
    {
        [JsonProperty]
        public AssetLocation[] blockCodes;

    }
}
