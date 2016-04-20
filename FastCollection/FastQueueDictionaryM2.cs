﻿/*
Copyright (c) Luchunpen (bwolf88).  All rights reserved.
Date: 04.03.2016 7:07:14
*/

using System;
using System.Collections;
using System.Collections.Generic;

namespace Nano3.HashCollection
{
    public class FastQueueDictionaryM2<TKey, TValue> : IQueueDictionary<TKey, TValue>
        where TKey : struct, IEquatable<TKey>
    {
        //private static readonly string stringUID = "BC12A1ACDE9EA403";

        protected int[] _bucket;

        protected TKey[] _keys;
        protected TValue[] _values;
        protected int[] _next;
        protected bool[] _fillmarker;
        protected int[] _queue;

        protected int _count;
        protected int _freeCount;

        protected int _nextFree;
        protected int _size;
        protected int _mask;

        protected int _qmask;
        protected int _head;
        protected int _tail;

        public FastQueueDictionaryM2() : this (4) { }
        public FastQueueDictionaryM2(int capacity)
        {
            int cap = HashPrimes.GetPrimeM2(capacity);
            _size = cap;

            _bucket = new int[_size];

            _keys = new TKey[_size];
            _next = new int[_size];
            _values = new TValue[_size];
            _fillmarker = new bool[_size];
            _queue = new int[_size];

            _count = 1;
            _nextFree = 0;
            _mask = _size - 1;
            _qmask = _size - 1;
            _head = 0;
            _tail = 0;
        }

        public virtual TValue this[TKey key]
        {
            get
            {
                int itempos = _bucket[(key.GetHashCode() & _mask)];

                FINDMATCH:
                if (itempos == 0) { throw new Exception("This id not exist"); }
                if (_keys[itempos].Equals(key)) { return _values[itempos]; }
                else
                {
                    itempos = _next[itempos];
                    goto FINDMATCH;
                }
            }
            set
            {
                int itempos = _bucket[(key.GetHashCode() & _mask)];

                FINDMATCH:
                if (itempos == 0) throw new Exception("This id not exist");
                if (_keys[itempos].Equals(key)) { _values[itempos] = value; }
                else
                {
                    itempos = _next[itempos];
                    goto FINDMATCH;
                }
            }
        }
        public int Count
        {
            get { return _count - _freeCount - 1; }
        }

        public bool ContainsKey(TKey key)
        {
            int itempos = _bucket[(key.GetHashCode() & _mask)];

            FINDMATCH:
            if (itempos == 0) { return false; }
            if (_keys[itempos].Equals(key)) { return true; }
            else
            {
                itempos = _next[itempos];
                goto FINDMATCH;
            }
        }
        public bool Enqueue(TKey key, TValue value)
        {
            int hash = key.GetHashCode() & _mask;
            int itempos = _bucket[hash];

            int next = 0, pos = _count;
            if (itempos > 0)
            {
                next = itempos;
                //проверка на совпадение ===================
                for (int i = itempos; i > 0; i = _next[i])
                {
                    if (_keys[i].Equals(key)) { _values[i] = value; return false; }
                }
                //=========================================
            }

            if (_freeCount > 0)
            {
                pos = _bucket[hash] = _nextFree;
                _nextFree = _next[_nextFree];

                _next[pos] = next;
                _values[pos] = value;
                _keys[pos] = key;
                _fillmarker[pos] = true;

                _queue[_tail] = pos;
                _tail = (_tail + 1) & _qmask;

                _freeCount--;
            }
            else
            {
                _next[pos] = next;
                _values[pos] = value;
                _keys[pos] = key;
                _fillmarker[pos] = true;

                _queue[_tail] = pos;

                _tail = (_tail + 1) & _qmask;

                _bucket[hash] = _count;
                _count = _count + 1;
                if (_count >= _size) { Resize(_size * 2); }
                if (Count >= _queue.Length) { ResizeQueue(_queue.Length * 2); }
            }
            return true;
        }
        public TValue Dequeue()
        {
            int qkey = _queue[_head];
            _queue[_head] = 0;
            _head = (_head + 1) & _qmask;

            TKey key = _keys[qkey];

            int hash = key.GetHashCode() & _mask;
            int itempos = _bucket[hash];

            int prev = 0;

            FINDMATCH:
            if (itempos == 0) { throw new ArgumentOutOfRangeException("No items"); }
            if (_keys[itempos].Equals(key))
            {
                if (prev == 0) { _bucket[hash] = _next[itempos]; }
                else { _next[prev] = _next[itempos]; }

                TValue result = _values[itempos];

                _values[itempos] = default(TValue);
                _keys[itempos] = default(TKey);
                _next[itempos] = _nextFree;
                _fillmarker[itempos] = false;

                _nextFree = itempos;
                _freeCount++;

                return result;
            }
            else
            {
                prev = itempos;
                itempos = _next[itempos];
                goto FINDMATCH;
            }
        }
        public TValue Peek()
        {
            if (Count == 0) { throw new ArgumentOutOfRangeException("No items"); }
            int qkey = _queue[_head];
            return _values[qkey];
        }

        protected void Resize(int nsize)
        {
            int newSize = nsize;
            int newMask = newSize - 1;
            int[] newBucket = new int[newSize];
            int[] newnext = new int[newSize];

            bool[] newfillmarker = new bool[newSize];
            Array.Copy(_fillmarker, newfillmarker, _count);

            TKey[] newkeys = new TKey[newSize];
            Array.Copy(_keys, newkeys, _count);

            TValue[] newvalues = new TValue[newSize];
            Array.Copy(_values, newvalues, _count);

            for (int i = 1; i < _size; i++)
            {
                int bucket = newkeys[i].GetHashCode() & newMask;
                newnext[i] = newBucket[bucket];
                newBucket[bucket] = i;
            }

            _bucket = newBucket;
            _next = newnext;
            _keys = newkeys;
            _values = newvalues;
            _fillmarker = newfillmarker;

            _size = newSize;
            _mask = newMask;
        }
        protected void ResizeQueue(int newsize)
        {
            int[] newQueue = new int[newsize];
            if (_head < _tail) { Array.Copy(_queue, _head, newQueue, 0, Count); }
            else
            {
                Array.Copy(_queue, _head, newQueue, 0, _queue.Length - _head);
                Array.Copy(_queue, 0, newQueue, _queue.Length - _head, _tail);
            }
            _head = 0;
            _tail = (Count == newsize) ? 0 : Count;
            _queue = newQueue;
            _qmask = newsize - 1;
        }

        public void Clear()
        {
            if (Count > 0)
            {
                Array.Clear(_bucket, 0, _count);
                Array.Clear(_next, 0, _count);
                Array.Clear(_keys, 0, _count);
                Array.Clear(_values, 0, _count);
                Array.Clear(_fillmarker, 0, _count);

                _count = 1;
                _freeCount = 0;
                _nextFree = 0;

                Array.Clear(_queue, 0, _queue.Length);
                _head = 0;
                _tail = 0;
            }
        }

        public TValue[] DequeueAll()
        {
            TValue[] v = GetValues();
            Clear();
            return v;
        }

        public TValue[] GetValues()
        {
            TValue[] v = new TValue[Count];
            int id = 0;
            for (int i = 0; i < _count; i++)
            {
                if (!_fillmarker[i]) continue;
                v[id++] = _values[i];
            }
            return v;
        }

        public TKey[] GetKeys()
        {
            TKey[] k = new TKey[Count];
            int id = 0;
            for (int i = 0; i < _count; i++)
            {
                if (!_fillmarker[i]) continue;
                k[id] = _keys[i];
                id++;
            }
            return k;
        }

        public IEnumerator<TValue> GetEnumerator()
        {
            for (int i = 0; i < _count; i++)
            {
                if (!_fillmarker[i]) continue;
                yield return _values[i];
            }
        }
        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}