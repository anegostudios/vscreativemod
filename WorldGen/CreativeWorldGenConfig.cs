using Newtonsoft.Json;
using Vintagestory.API.Common;

namespace Vintagestory.ServerMods.NoObf
{
    [JsonObject(MemberSerialization.OptIn)]
    public class FlatWorldGenConfig
    {
        [JsonProperty]
        public AssetLocation[] blockCodes;

    }
}
