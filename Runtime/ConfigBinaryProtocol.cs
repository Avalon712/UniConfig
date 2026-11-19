#region

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

#endregion

namespace UniConfig
{
    /// <summary>
    /// 单文件多表二进制协议。
    /// 整表数组文件以 UCFH + int32 最大块长度开头；逐行文件没有这个头。
    /// 每张表：魔数 UCFG(4) + 表唯一 Id(int32) + 行数(int32) + 数据。
    /// 行数 ≥ 0：N × (payloadLen + 单行 MemoryPack)，读入 new XX[length]。
    /// 行数 = -1：一块 T[] 的 MemoryPack。
    /// 行数 = -2：一块杂项对象的 MemoryPack，全部普通表解析完后再写入 CfgMisc。
    /// </summary>
    public static class ConfigBinaryProtocol
    {
        public const string FileName = "configs.bytes";
        public static readonly byte[] Magic = { (byte)'U', (byte)'C', (byte)'F', (byte)'G' };
        public static readonly byte[] HeaderMagic = { (byte)'U', (byte)'C', (byte)'F', (byte)'H' };

        /// <summary>整表数组。后面只有一块 T[] 的 MemoryPack。</summary>
        public const int RowCountArray = -1;

        /// <summary>杂项表。后面只有一块单个对象的 MemoryPack。</summary>
        public const int RowCountMisc = -2;

        public static Dictionary<int, object> ReadAll(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            Dictionary<int, object> result = new();
            if (data.Length == 0) return result;

            int offset = 0;
            int end = data.Length;
            List<(int tableId, byte[] payload)> misc = new();

            if (end >= 4 && HasMagic(data, 0, HeaderMagic))
            {
                offset = 4;
                if (end - offset < 4)
                    throw new InvalidDataException("配置文件在最大表长度处截断");
                int maxPayload = ReadInt32LittleEndian(data, ref offset);
                if (maxPayload < 0)
                    throw new InvalidDataException($"最大表长度非法: {maxPayload}");
            }

            while (offset < end)
            {
                if (end - offset < 4)
                    throw new InvalidDataException("配置文件在魔数处截断");

                if (!HasMagic(data, offset, Magic))
                    throw new InvalidDataException(
                        $"配置文件魔数不匹配，期望 UCFG，实际 {data[offset]:X2} {data[offset + 1]:X2} {data[offset + 2]:X2} {data[offset + 3]:X2}");

                offset += 4;

                if (end - offset < 4)
                    throw new InvalidDataException("配置文件在表 Id 处截断");

                int tableId = ReadInt32LittleEndian(data, ref offset);
                if (tableId <= 0)
                    throw new InvalidDataException($"表 Id 非法: {tableId}");

                if (end - offset < 4)
                    throw new InvalidDataException($"表 Id={tableId} 行数处截断");

                int rowCount = ReadInt32LittleEndian(data, ref offset);
                if (rowCount == RowCountArray || rowCount == RowCountMisc)
                {
                    ReadOnlySpan<byte> payload = ReadPayload(data, ref offset, end, tableId, "数据块");
                    if (rowCount == RowCountArray)
                    {
                        Array array = ConfigMgr.DeserializeTableArray(tableId, payload);
                        result[tableId] = array ?? Array.Empty<object>();
                    }
                    else
                    {
                        misc.Add((tableId, payload.ToArray()));
                    }

                    continue;
                }

                if (rowCount < 0)
                    throw new InvalidDataException($"表 Id={tableId} 行数非法: {rowCount}");

                Array rows = ConfigMgr.CreateTableArray(tableId, rowCount) ?? new object[rowCount];
                bool loose = rows.GetType() == typeof(object[]);
                for (int i = 0; i < rowCount; i++)
                {
                    ReadOnlySpan<byte> payload = ReadPayload(data, ref offset, end, tableId, $"第 {i} 行");
                    if (loose)
                    {
                        IConfigTable item = DeserializeRow(tableId, payload);
                        if (item != null)
                            rows.SetValue(item, i);
                    }
                    else
                    {
                        ConfigMgr.SetTableItem(tableId, rows, i, payload);
                    }
                }

                result[tableId] = rows;
            }

            for (int i = 0; i < misc.Count; i++)
            {
                ReadOnlySpan<byte> payload = misc[i].payload;
                ConfigMgr.ApplyMisc(misc[i].tableId, payload);
            }

            return result;
        }

        private static bool HasMagic(byte[] data, int offset, byte[] magic)
        {
            return data[offset] == magic[0] && data[offset + 1] == magic[1] &&
                   data[offset + 2] == magic[2] && data[offset + 3] == magic[3];
        }

        private static ReadOnlySpan<byte> ReadPayload(byte[] data, ref int offset, int end, int tableId, string label)
        {
            if (end - offset < 4)
                throw new InvalidDataException($"表 Id={tableId} {label}长度字段处截断");

            int payloadLen = ReadInt32LittleEndian(data, ref offset);
            if (payloadLen < 0 || end - offset < payloadLen)
                throw new InvalidDataException($"表 Id={tableId} {label}截断或长度非法: {payloadLen}");

            ReadOnlySpan<byte> payload = new(data, offset, payloadLen);
            offset += payloadLen;
            return payload;
        }

        /// <summary>
        /// 通过已注册的泛型反序列化方法解析一行；失败时打 Error 并返回 null。
        /// </summary>
        internal static IConfigTable DeserializeRow(int tableId, in ReadOnlySpan<byte> payload)
        {
            if (ConfigMgr.TryDeserialize(tableId, payload, out IConfigTable item))
                return item;

            Debug.LogError(
                $"配置反序列化失败: tableId={tableId}。请确认已生成 C#（含 Cfg 自动注册），且 BeforeSceneLoad 已完成 RegisterDeserializeMethod。");
            return null;
        }

        private static int ReadInt32LittleEndian(byte[] data, ref int offset)
        {
            int value = data[offset]
                        | (data[offset + 1] << 8)
                        | (data[offset + 2] << 16)
                        | (data[offset + 3] << 24);
            offset += 4;
            return value;
        }

        public static int NextPowerOfTwo(int value)
        {
            if (value < 1) return 1;
            uint v = (uint)value;
            v--;
            v |= v >> 1;
            v |= v >> 2;
            v |= v >> 4;
            v |= v >> 8;
            v |= v >> 16;
            v++;
            if (v > int.MaxValue) return int.MaxValue;
            return (int)v;
        }
    }
}