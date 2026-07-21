#region

using System;

#endregion

namespace UniConfig
{
    /// <summary>
    /// 反序列化表的 Item 数据（按表唯一 Id）。
    /// </summary>
    /// <param name="tableId">表唯一 Id</param>
    /// <param name="data">表的单行数据</param>
    /// <returns>反序列化后的对象；无法处理时返回 null</returns>
    public delegate IConfigTable DeserializeCfgTableItemMethod(int tableId, in ReadOnlySpan<byte> data);

    /// <summary>
    /// 由配置类型解析表唯一 Id；未知类型返回 0。
    /// </summary>
    public delegate int ResolveCfgTableIdMethod(Type mapCsharpType);
}