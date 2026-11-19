#region

using System;
using System.Collections.Generic;
using MemoryPack;

#endregion

namespace UniConfig.Editor
{
    internal enum ExportBlobKind
    {
        PerRow = 0,
        Array = 1,
        Misc = 2
    }

    public sealed class ExportTableData
    {
        private readonly List<byte[]> _items;
        private readonly List<object> _objects;

        /// <summary>
        /// 允许空表（Count=0），仍写入表头。逐行：每行一块 MemoryPack。
        /// </summary>
        internal ExportTableData(string moduleName, string tableName, int tableId, Type csharpType,
            List<object> objects)
            : this(moduleName, tableName, tableId, csharpType, objects, null, ExportBlobKind.PerRow)
        {
        }

        /// <summary>
        /// 整表数组或杂项表：objects 仍给编辑器缓存用，写出时只用 singlePayload。
        /// </summary>
        internal ExportTableData(string moduleName, string tableName, int tableId, Type csharpType,
            List<object> objects, byte[] singlePayload, ExportBlobKind kind)
        {
            if (tableId <= 0)
                throw new ArgumentOutOfRangeException(nameof(tableId), tableId, "tableId 必须 > 0");
            ExportCsharpType = csharpType ?? throw new ArgumentNullException(nameof(csharpType));
            ModuleName = moduleName;
            TableName = tableName;
            TableId = tableId;
            Kind = kind;
            SinglePayload = singlePayload ?? Array.Empty<byte>();
            _objects = objects ?? new List<object>();
            _items = new List<byte[]>(kind == ExportBlobKind.PerRow ? _objects.Count : 0);
            if (kind == ExportBlobKind.PerRow)
            {
                foreach (object obj in _objects)
                    _items.Add(SerializeObject(ExportCsharpType, obj));
            }
        }

        public string ModuleName { get; }
        public string TableName { get; }
        public int TableId { get; }
        public int Count => _objects.Count;
        public Type ExportCsharpType { get; }
        public string TypeFullName => ExportCsharpType.FullName;
        internal ExportBlobKind Kind { get; }
        internal byte[] SinglePayload { get; }

        public object GetObject(int index)
        {
            return _objects[index];
        }

        public byte[] GetBytes(int index)
        {
            return _items[index];
        }

        private static byte[] SerializeObject(Type type, object obj)
        {
            return MemoryPackSerializer.Serialize(type, obj);
        }
    }
}