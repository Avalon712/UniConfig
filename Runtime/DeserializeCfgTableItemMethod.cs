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

    /// <summary>
    /// 按已知行数创建具体类型的数组，例如 CfgSkill[length]。未知表返回 null。
    /// </summary>
    public delegate Array CreateCfgTableArrayMethod(int tableId, int length);

    /// <summary>
    /// 把一行 MemoryPack 填进已创建的数组下标。
    /// </summary>
    public delegate void SetCfgTableItemMethod(int tableId, Array rows, int index, in ReadOnlySpan<byte> data);

    /// <summary>
    /// 把一整块 MemoryPack 直接反序列化成具体类型的数组。未知表返回 null。
    /// </summary>
    public delegate Array DeserializeCfgTableArrayMethod(int tableId, in ReadOnlySpan<byte> data);

    /// <summary>
    /// 把杂项表的单个对象写入 CfgMisc。处理了该 tableId 时返回 true。
    /// </summary>
    public delegate bool ApplyMiscCfgMethod(int tableId, in ReadOnlySpan<byte> data);
}