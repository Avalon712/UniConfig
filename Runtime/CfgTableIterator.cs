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
        private readonly IReadOnlyList<object> _cfgItems;

        internal CfgTableIterator(IReadOnlyList<object> cfgItems)
        {
            _cfgItems = cfgItems ?? Array.Empty<object>();
        }

        public int Count => _cfgItems.Count;

        private TRequireType GetItem(int index)
        {
            return _cfgItems[index] is TRequireType item ? item : default;
        }

        public List<TRequireType> ToList()
        {
            List<TRequireType> list = new(Count);
            for (int i = 0; i < Count; i++)
                if (_cfgItems[i] is TRequireType item)
                    list.Add(item);

            return list;
        }

        public TRequireType[] ToArray()
        {
            int matched = 0;
            for (int i = 0; i < Count; i++)
                if (_cfgItems[i] is TRequireType)
                    matched++;

            TRequireType[] array = new TRequireType[matched];
            int index = 0;
            for (int i = 0; i < Count; i++)
                if (_cfgItems[i] is TRequireType item)
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
            return ReferenceEquals(_cfgItems, other._cfgItems);
        }

        public override bool Equals(object obj)
        {
            return obj is CfgTableIterator<TRequireType> other && Equals(other);
        }

        public override int GetHashCode()
        {
            return _cfgItems != null ? _cfgItems.GetHashCode() : 0;
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