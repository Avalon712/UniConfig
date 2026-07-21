#region

using System.Collections.Generic;

#endregion

namespace UniConfig
{
    internal static class ResourcesLoader
    {
        public static Dictionary<int, object> ReadAllConfigs(byte[] data)
        {
            return ConfigBinaryProtocol.ReadAll(data);
        }
    }
}