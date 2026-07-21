#region

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

#endregion

namespace UniConfig
{
    /// <summary>
    /// 单文件多表二进制协议：
    /// 每张表：魔数 UCFG(4) + 表唯一 Id(int32) + 行数(int32) + N × (payloadLen(int32) + MemoryPack bytes)
    /// </summary>
    public static class ConfigBinaryProtocol
    {
        public const string FileName = "configs.bytes";
        public static readonly byte[] Magic = { (byte)'U', (byte)'C', (byte)'F', (byte)'G' };

        public static Dictionary<int, object> ReadAll(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            Dictionary<int, object> result = new();
            if (data.Length == 0) return result;

            int offset = 0;
            int end = data.Length;

            while (offset < end)
            {
                if (end - offset < 4)
                    throw new InvalidDataException("配置文件在魔数处截断");

                if (data[offset] != Magic[0] || data[offset + 1] != Magic[1] ||
                    data[offset + 2] != Magic[2] || data[offset + 3] != Magic[3])
                    throw new InvalidDataException(
                        $"配置文件魔数不匹配，期望 UCFG，实际 {(char)data[offset]}{(char)data[offset + 1]}{(char)data[offset + 2]}{(char)data[offset + 3]}");

                offset += 4;

                if (end - offset < 4)
                    throw new InvalidDataException("配置文件在表 Id 处截断");

                int tableId = ReadInt32LittleEndian(data, ref offset);
                if (tableId <= 0)
                    throw new InvalidDataException($"表 Id 非法: {tableId}");

                if (end - offset < 4)
                    throw new InvalidDataException($"表 Id={tableId} 行数处截断");

                int rowCount = ReadInt32LittleEndian(data, ref offset);
                if (rowCount < 0)
                    throw new InvalidDataException($"表 Id={tableId} 行数非法: {rowCount}");

                List<object> rows = new(rowCount);
                for (int i = 0; i < rowCount; i++)
                {
                    if (end - offset < 4)
                        throw new InvalidDataException($"表 Id={tableId} 第 {i} 行长度字段处截断");

                    int payloadLen = ReadInt32LittleEndian(data, ref offset);
                    if (payloadLen < 0 || end - offset < payloadLen)
                        throw new InvalidDataException($"表 Id={tableId} 第 {i} 行 payload 截断或长度非法: {payloadLen}");

                    ReadOnlySpan<byte> payload = new(data, offset, payloadLen);
                    offset += payloadLen;

                    IConfigTable item = DeserializeRow(tableId, payload);
                    if (item != null)
                        rows.Add(item);
                }

                result[tableId] = rows;
            }

            return result;
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