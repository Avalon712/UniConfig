#region

using System;
using System.Collections.Generic;
using MemoryPack;

#endregion

namespace UniConfig.Editor
{
    public sealed class ExportTableData
    {
        private readonly List<byte[]> _items;
        private readonly List<object> _objects;

        /// <summary>
        /// 允许空表（Count=0），仍写入表头。
        /// </summary>
        internal ExportTableData(string moduleName, string tableName, int tableId, Type csharpType,
            List<object> objects)
        {
            if (tableId <= 0)
                throw new ArgumentOutOfRangeException(nameof(tableId), tableId, "tableId 必须 > 0");
            ExportCsharpType = csharpType ?? throw new ArgumentNullException(nameof(csharpType));
            ModuleName = moduleName;
            TableName = tableName;
            TableId = tableId;
            _objects = objects ?? new List<object>();
            _items = new List<byte[]>(_objects.Count);
            foreach (object obj in _objects)
                _items.Add(SerializeObject(ExportCsharpType, obj));
        }

        public string ModuleName { get; }
        public string TableName { get; }
        public int TableId { get; }
        public int Count => _items.Count;
        public Type ExportCsharpType { get; }
        public string TypeFullName => ExportCsharpType.FullName;

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