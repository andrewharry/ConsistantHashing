using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CommonTools.MemoryStorage;
using HashTableHashing;

namespace CommonTools
{
    public class ConsistentHashing<T>
    {
        struct Point
        {
            public readonly UInt32 key;
            public readonly T nodeId;

            public Point(UInt32 k, T v)
            {
                key = k;
                nodeId = v;
            }
        }

        class ComparePoints : Comparer<Point>
        {
            public override int Compare(Point x, Point y)
            {
                return (int)x.key - (int)y.key;
            }
        }

        public ConsistentHashing(T node)
        {
            store = new KeyValueStore<uint, T, object>();
            circle = new List<Point>(circlePoints * 4);
            AddNode(node);
        }

        public ConsistentHashing(T[] nodeIds)
        {
            store = new KeyValueStore<uint, T, object>();
            circle = new List<Point>((nodeIds.Length + 1) * circlePoints);
            for (int i = 0; i < nodeIds.Length; i++)
                AddNode(nodeIds[i]);
        }

        MurmurHash2Simple hash = new MurmurHash2Simple();
        private List<Point> circle;
        private KeyValueStore<uint, T, object> store;

        public T Get(string input)
        {
            var data = Encoding.UTF8.GetBytes(input);
            var val = hash.Hash(data);
            return Get(val);
        }

        public T Get(byte[] input)
        {
            var val = hash.Hash(input);
            return Get(val);
        }

        public T Get(uint val)
        {
            Point[] points = null;
            try
            {
                if (circle.IsNullOrEmpty())
                    throw new NullReferenceException("No Nodes Found");               

                points = circle.ToArray();
                UInt32 top = (UInt32)points.Length;
                UInt32 high = top;
                UInt32 low = 0;

                while (true)
                {
                    UInt32 mid = (high - low) / 2 + low;
                    var pt = points[mid];
                    if (val > pt.key)
                        low = mid;
                    else if (val < pt.key)
                        high = mid;
                    if (mid == top)
                        return points[0].nodeId;
                    else if (high - low <= 1)
                        return points[mid].nodeId;
                }
            }
            finally
            {
                points = null;
            }            
        }
        
        public void Add(T nodeId)
        {
            if (store.ContainsKey(nodeId)) return;
            AddNode(nodeId);
        }

        public void Remove(T nodeId)
        {
            if (!store.ContainsKey(nodeId)) return;
            RemoveNode(nodeId);
        }

        private const int circlePoints = 160;
        private static ComparePoints compare = new ComparePoints();
        private static object locker = new object();

        private void AddNode(T nodeId)
        {
            if (store.ContainsKey(nodeId)) return;
            byte[] input = null;
            string name = null;
            string node = nodeId.ToString();
            uint[] keys = new uint[circlePoints];
            List<Point> points = null;
            
            try
            {
                lock (locker)
                {
                    points = new List<Point>(circle.Count + circlePoints);
                    points.AddRange(circle);


                    for (int i = 0; i < circlePoints; i++)
                    {
                        name = node + i.ToPadded(3);
                        input = Encoding.UTF8.GetBytes(name);
                        keys[i] = hash.Hash(input);
                        points.Add(new Point(keys[i], nodeId));
                    }

                    points.Sort(compare);
                    circle = points;
                    store.Add(nodeId, keys);    //Should this be using Upsert?
                }
            }
            finally
            {
                input = null;
                name = null;
                points = null;
                node = null;
                keys = null;
            }            
        }

        private void RemoveNode(T nodeId)
        {
            if (!store.ContainsKey(nodeId)) return;
            uint[] keys = null;
            List<Point> points = null;

            try
            {
                lock (locker)
                {
                    points = new List<Point>(circle);
                    keys = store.Get(nodeId).ToArray();
                    store.Remove(nodeId);
                    if (keys.IsNullOrEmpty()) return;
                    points.RemoveAll(v => keys.Contains(v.key));

                    points.Sort(compare);
                    circle = points;
                }
            }
            finally
            {
                keys = null;
                points = null;
            }            
        }
    }
}
