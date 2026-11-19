#region

using System;
using System.Collections;
using System.Collections.Generic;

#endregion

namespace UniConfig
{
    /// <summary>
    /// 配置表只读遍历器。实现标准枚举接口，可在 foreach / LINQ / 迭代器方法中使用。
    /// </summary>
    public readonly struct CfgTableIterator<TRequireType> :
        IEnumerable<TRequireType>,
        IEquatable<CfgTableIterator<TRequireType>>
        where TRequireType : IConfigTable
    {
        private readonly Array _array;
        private readonly IReadOnlyList<object> _cfgItems;

        public CfgTableIterator(IReadOnlyList<object> cfgItems)
        {
            _cfgItems = cfgItems ?? Array.Empty<object>();
            _array = null;
        }

        /// <summary>
        /// 运行时保存的是具体数组，例如 CfgSkill[]。调用方不知道那个数组类型。
        /// </summary>
        public static CfgTableIterator<TRequireType> FromArray(Array items)
        {
            return new CfgTableIterator<TRequireType>(items, 0);
        }

        private CfgTableIterator(Array items, int unused)
        {
            _array = items ?? Array.Empty<object>();
            _cfgItems = null;
            _ = unused;
        }

        public int Count => _array != null ? _array.Length : _cfgItems.Count;

        private object GetRaw(int index)
        {
            return _array != null ? _array.GetValue(index) : _cfgItems[index];
        }

        private TRequireType GetItem(int index)
        {
            return GetRaw(index) is TRequireType item ? item : default;
        }

        public List<TRequireType> ToList()
        {
            List<TRequireType> list = new(Count);
            for (int i = 0; i < Count; i++)
                if (GetRaw(i) is TRequireType item)
                    list.Add(item);

            return list;
        }

        public TRequireType[] ToArray()
        {
            int matched = 0;
            for (int i = 0; i < Count; i++)
                if (GetRaw(i) is TRequireType)
                    matched++;

            TRequireType[] array = new TRequireType[matched];
            int index = 0;
            for (int i = 0; i < Count; i++)
                if (GetRaw(i) is TRequireType item)
                    array[index++] = item;

            return array;
        }

        public Enumerator GetEnumerator()
        {
            return new Enumerator(this);
        }

        IEnumerator<TRequireType> IEnumerable<TRequireType>.GetEnumerator()
        {
            return GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public bool Equals(CfgTableIterator<TRequireType> other)
        {
            return ReferenceEquals(_array, other._array) && ReferenceEquals(_cfgItems, other._cfgItems);
        }

        public override bool Equals(object obj)
        {
            return obj is CfgTableIterator<TRequireType> other && Equals(other);
        }

        public override int GetHashCode()
        {
            object key = _array ?? (object)_cfgItems;
            return key != null ? key.GetHashCode() : 0;
        }

        public struct Enumerator : IEnumerator<TRequireType>
        {
            private readonly CfgTableIterator<TRequireType> _table;
            private int _index;

            internal Enumerator(CfgTableIterator<TRequireType> table)
            {
                _table = table;
                _index = -1;
                Current = default;
            }

            public TRequireType Current { get; private set; }

            object IEnumerator.Current => Current;

            public bool MoveNext()
            {
                int next = _index + 1;
                if (next >= _table.Count)
                    return false;

                _index = next;
                Current = _table.GetItem(_index);
                return true;
            }

            public void Reset()
            {
                _index = -1;
                Current = default;
            }

            public void Dispose()
            {
                Current = default;
                _index = _table.Count;
            }
        }
    }
}